using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ISO8583Net.Header;
using ISO8583Net.Message;
using ISO8583Net.Packager;
using ISO8583Net.Server.Pipeline.Handlers;
using ISO8583Net.Server.Pipeline.Messages;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Server.Pipeline;

/// <summary>
/// Reads <see cref="ParsedMessage"/> instances from the input channel,
/// routes them to registered <see cref="IMessageHandler"/> instances,
/// and tracks in-flight handler tasks for graceful shutdown.
///
/// Messages are dispatched by MTI. Handlers run in parallel as fire-and-forget
/// tasks; responses flow back through the outbound channel.
/// </summary>
internal static class DispatcherStage
{
    /// <summary>
    /// Run the dispatcher loop until the input channel is completed or cancelled.
    /// </summary>
    public static async Task RunAsync(
        ChannelReader<ParsedMessage> input,
        ChannelWriter<OutboundMessage> outbound,
        Handlers.HandlerRegistry registry,
        PipelineStats stats,
        ILogger logger,
        PipelineOptions options,
        IMessageTracer? tracer,
        CancellationToken ct)
    {
        var inFlight = new List<Task>();
        var drainTimeout = TimeSpan.FromSeconds(options.DrainTimeoutSeconds);
        logger.LogDebug("Dispatcher stage started");

        try
        {
            await foreach (var parsed in input.ReadAllAsync(ct))
            {
                string mti = GetMtiSafe(parsed);

                // ── Inbound 9xxx format-error recognition ─────────────────────────────
                // A "9xxx" MTI is a peer's format-error response telling us that OUR earlier
                // message was invalid. It is terminal: log it and stop, never bounce a reply
                // (which would produce an endless 9800↔9800 ping-pong loop).
                if (parsed.Message.Header is ISOHeaderD8 d8Header && IsFormatErrorMti(mti))
                {
                    string fieldInError = d8Header.FieldInError;
                    if (mti == "9800")
                    {
                        logger.LogWarning(
                            "Received D8 format-error response [{Mti}] (unknown-MTI / invalid-header), FieldInError={FieldInError}, conn={ConnNum}; not responding",
                            mti, fieldInError, parsed.ConnectionNumber);
                    }
                    else
                    {
                        // The version digit is always '1' for G2B-ISO-1.00, so the original MTI
                        // is recovered by restoring '1' and keeping the class/function/originator
                        // digits that survive the 9xxx transformation.
                        string originalMti = "1" + mti.Substring(1);
                        logger.LogWarning(
                            "Received D8 format-error response [{Mti}] for original MTI [{OriginalMti}], FieldInError={FieldInError}, conn={ConnNum}; not responding",
                            mti, originalMti, fieldInError, parsed.ConnectionNumber);
                    }
                    continue;
                }

                // ── Inbound dialect enforcement ─────────────────────────────────────
                // A message whose MTI is not defined in the dialect must never be silently
                // dropped: reply with the dialect's format-error response (D8 "9800",
                // FieldInError=999, bitmap-less) and log a structured diagnostic.
                if (parsed.Message.Header is ISOHeaderD8 && parsed.ValidationResult is { IsMtiKnown: false })
                {
                    logger.LogWarning(
                        "Rejecting inbound message with unknown MTI [{MTI}], conn={ConnNum}: {Reason}",
                        mti, parsed.ConnectionNumber, parsed.ValidationResult.Message);
                    await SendFormatErrorResponseAsync(parsed, outbound, logger, ct);
                    continue;
                }

                // A known MTI that fails field-level validation (missing mandatory fields or
                // disallowed fields present) is rejected with a spec-complete "9xxx" format-error
                // response whose header "Field in Error" carries the first offending field number.
                if (parsed.Message.Header is ISOHeaderD8 && parsed.ValidationResult is { IsMtiKnown: true, IsValid: false } fieldError)
                {
                    logger.LogWarning(
                        "Inbound MTI [{MTI}] failed field validation [{Reason}], first offending field={Field}, conn={ConnNum}",
                        mti, fieldError.Message, FirstOffendingField(fieldError), parsed.ConnectionNumber);
                    await SendFieldErrorResponseAsync(parsed, outbound, logger, ct);
                    continue;
                }

                var handlers = registry.GetHandlers(mti);

                if (handlers.Count == 0)
                {
                    continue;
                }

                var ctx = new MessageContext(
                    request: parsed.Message,
                    connectionNumber: parsed.ConnectionNumber,
                    remoteEndpoint: parsed.RemoteEndpoint,
                    receivedAt: parsed.ParsedAt,
                    writer: outbound);

                // Fire handlers in parallel
                foreach (var handler in handlers)
                {
                    var task = HandleMessageAsync(handler, ctx, stats, logger, tracer, ct);
                    inFlight.Add(task);
                }

                // Clean up completed tasks periodically
                if (inFlight.Count > 64)
                {
                    inFlight.RemoveAll(t => t.IsCompleted);
                }
            }
        }
        catch (OperationCanceledException) { /* graceful */ }
        finally
        {
            // Wait for in-flight handlers to complete (with drain timeout)
            if (inFlight.Count > 0)
            {
                logger.LogDebug("Draining {Count} in-flight handlers (timeout={Timeout}s)",
                    inFlight.Count, drainTimeout.TotalSeconds);

                var drainCts = new CancellationTokenSource(drainTimeout);
                try
                {
                    await Task.WhenAll(inFlight).WaitAsync(drainCts.Token);
                }
                catch (OperationCanceledException)
                {
                    logger.LogWarning("Handler drain timed out after {Timeout}s, {Remaining} still in-flight",
                        drainTimeout.TotalSeconds, inFlight.FindAll(t => !t.IsCompleted).Count);
                }
                catch (TimeoutException)
                {
                    logger.LogWarning("Handler drain timed out after {Timeout}s, {Remaining} still in-flight",
                        drainTimeout.TotalSeconds, inFlight.FindAll(t => !t.IsCompleted).Count);
                }
                catch
                {
                    // Handlers should handle their own exceptions
                }
            }

            logger.LogDebug("Dispatcher stage completed");
        }
    }

    private static async Task HandleMessageAsync(
        IMessageHandler handler, MessageContext ctx, PipelineStats stats,
        ILogger logger, IMessageTracer? tracer, CancellationToken ct)
    {
        stats.IncrementInFlight();
        var sw = Stopwatch.StartNew();
        string requestMti = GetMtiSafe(ctx.Request);

        try
        {
            var response = await handler.HandleAsync(ctx, ct);
            sw.Stop();

            if (response != null)
            {
                await ctx.SendResponseAsync(response, ct);

                string responseMti = GetMtiSafe(response);
                string f39 = response.GetFieldValue(39) ?? "???";

                tracer?.OnMessageResponded(requestMti, responseMti, f39,
                    ctx.ConnectionNumber, sw.ElapsedMilliseconds);
            }
            else
            {
                tracer?.OnNoResponse(requestMti, ctx.ConnectionNumber);
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            stats.IncrementHandlerErrors();
            logger.LogError(ex, "Handler error on MTI {MTI}, conn={ConnNum}",
                requestMti, ctx.ConnectionNumber);
            tracer?.OnHandlerError(requestMti, ctx.ConnectionNumber, ex.Message);
        }
        finally
        {
            stats.DecrementInFlight();
        }
    }

    private static string GetMtiSafe(ParsedMessage parsed)
    {
        try
        {
            return parsed.Message.GetFieldValue(0) ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static string GetMtiSafe(ISOMessage msg)
    {
        try
        {
            return msg.GetFieldValue(0) ?? "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Returns true when <paramref name="mti"/> is a 4-digit message type identifier whose
    /// first digit is '9' — i.e. a D8 format-error response (e.g. 9200, 9400, 9800, 9804).
    /// </summary>
    private static bool IsFormatErrorMti(string mti)
    {
        return mti is { Length: 4 }
            && mti[0] == '9'
            && IsAsciiDigit(mti[1]) && IsAsciiDigit(mti[2]) && IsAsciiDigit(mti[3]);
    }

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    /// <summary>
    /// Sends a bitmap-less D8 "9800" format-error response for an inbound message whose
    /// MTI is not defined in the dialect.
    /// </summary>
    private static async Task SendFormatErrorResponseAsync(
        ParsedMessage parsed, ChannelWriter<OutboundMessage> outbound, ILogger logger, CancellationToken ct)
    {
        byte[]? frame = ErrorResponseBuilder.BuildD8FormatErrorFrame(parsed.Message, logger);
        if (frame == null)
        {
            logger.LogWarning("Cannot build D8 format-error frame (no D8 header) for unknown-MTI message, conn={ConnNum}",
                parsed.ConnectionNumber);
            return;
        }

        await outbound.WriteAsync(
            OutboundMessage.FromPreFramed(frame, parsed.ConnectionNumber), ct);
    }

    /// <summary>
    /// Sends a spec-complete D8 "9xxx" field-error response for an inbound message with a
    /// known MTI that failed field-level validation. The header's <c>Field in Error</c>
    /// carries the first offending field number (000-128).
    /// </summary>
    private static async Task SendFieldErrorResponseAsync(
        ParsedMessage parsed, ChannelWriter<OutboundMessage> outbound, ILogger logger, CancellationToken ct)
    {
        string mti = GetMtiSafe(parsed);
        int firstOffendingField = FirstOffendingField(parsed.ValidationResult);

        byte[]? frame = ErrorResponseBuilder.BuildD8FieldErrorFrame(
            parsed.Message, mti, firstOffendingField, logger);
        if (frame == null)
        {
            // No D8 header (or unrecognized MTI): fall back to the unknown-MTI "9800"/"999" frame.
            logger.LogWarning(
                "Cannot build D8 field-error frame, falling back to format-error frame, conn={ConnNum}",
                parsed.ConnectionNumber);
            await SendFormatErrorResponseAsync(parsed, outbound, logger, ct);
            return;
        }

        await outbound.WriteAsync(
            OutboundMessage.FromPreFramed(frame, parsed.ConnectionNumber), ct);
    }

    /// <summary>
    /// Returns the numerically smallest offending field across the missing-mandatory and
    /// disallowed-field sets, or -1 when there are none.
    /// </summary>
    private static int FirstOffendingField(DialectValidationResult? result)
    {
        if (result == null)
            return -1;

        int min = int.MaxValue;
        foreach (int fieldNumber in result.MissingMandatoryFields)
            min = Math.Min(min, fieldNumber);
        foreach (int fieldNumber in result.DisallowedFields)
            min = Math.Min(min, fieldNumber);

        return min == int.MaxValue ? -1 : min;
    }
}

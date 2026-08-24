using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ISO8583Net.Header;
using ISO8583Net.Message;
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

                // A known MTI that is missing mandatory fields is also rejected with a
                // format-error response (F39=902) instead of being routed to handlers.
                if (parsed.Message.Header is ISOHeaderD8 && parsed.ValidationResult is { IsMtiKnown: true, MissingMandatoryFields.Count: > 0 } missing)
                {
                    logger.LogWarning(
                        "Inbound MTI [{MTI}] missing mandatory fields [{Missing}], conn={ConnNum}",
                        mti, string.Join(",", missing.MissingMandatoryFields), parsed.ConnectionNumber);
                    await SendMissingMandatoryResponseAsync(parsed, outbound, logger, ct);
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

    private static readonly Dictionary<string, string> ResponseMtiMap = new()
    {
        ["1100"] = "1110",
        ["1120"] = "1130",
        ["1200"] = "1210",
        ["1220"] = "1230",
        ["1400"] = "1410",
        ["1420"] = "1430",
        ["1804"] = "1814",
    };

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
    /// Sends a normal format-error response (F39=902) for an inbound message that is a
    /// known MTI but is missing mandatory fields.
    /// </summary>
    private static async Task SendMissingMandatoryResponseAsync(
        ParsedMessage parsed, ChannelWriter<OutboundMessage> outbound, ILogger logger, CancellationToken ct)
    {
        string mti = GetMtiSafe(parsed);
        if (!ResponseMtiMap.TryGetValue(mti, out string? responseMti))
        {
            logger.LogWarning("No response MTI mapping for [{MTI}], cannot send format-error response, conn={ConnNum}",
                mti, parsed.ConnectionNumber);
            return;
        }

        ISOMessage response = parsed.Message.CreateCleanResponse();
        response.Set(0, responseMti);
        CopyField(parsed.Message, response, 7);   // Transmission Date/Time
        CopyField(parsed.Message, response, 11);  // STAN
        CopyField(parsed.Message, response, 24);  // Function Code
        response.Set(39, "902");                  // Format Error

        await outbound.WriteAsync(
            OutboundMessage.FromISOMessage(response, parsed.ConnectionNumber), ct);
    }

    /// <summary>Copies a field value from request to response if present in the request.</summary>
    private static void CopyField(ISOMessage request, ISOMessage response, int fieldNumber)
    {
        try
        {
            string? value = request.GetFieldValue(fieldNumber);
            if (value != null)
                response.Set(fieldNumber, value);
        }
        catch
        {
            // Field may not participate in the message type; ignore for the error response.
        }
    }
}

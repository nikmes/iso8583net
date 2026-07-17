using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
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
                    var task = HandleMessageAsync(handler, ctx, stats, logger, ct);
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
        ILogger logger, CancellationToken ct)
    {
        stats.IncrementInFlight();
        try
        {
            var response = await handler.HandleAsync(ctx, ct);
            if (response != null)
            {
                await ctx.SendResponseAsync(response, ct);
            }
        }
        catch (Exception ex)
        {
            stats.IncrementHandlerErrors();
            logger.LogError(ex, "Handler error on MTI {MTI}, conn={ConnNum}",
                GetMtiSafe(ctx.Request), ctx.ConnectionNumber);
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
}

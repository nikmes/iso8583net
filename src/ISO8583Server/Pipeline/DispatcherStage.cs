using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ISO8583Net.Server.Pipeline.Handlers;
using ISO8583Net.Server.Pipeline.Messages;

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
        CancellationToken ct)
    {
        var inFlight = new List<Task>();

        try
        {
            await foreach (var parsed in input.ReadAllAsync(ct))
            {
                string mti = GetMtiSafe(parsed);
                var handlers = registry.GetHandlers(mti);

                if (handlers.Count == 0)
                {
                    // No handler for this MTI — skip
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
                    var task = HandleMessageAsync(handler, ctx, stats, ct);
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
            // Wait for in-flight handlers to complete (with timeout)
            if (inFlight.Count > 0)
            {
                try
                {
                    await Task.WhenAll(inFlight);
                }
                catch { /* handlers should handle their own exceptions */ }
            }
        }
    }

    private static async Task HandleMessageAsync(
        IMessageHandler handler, MessageContext ctx, PipelineStats stats, CancellationToken ct)
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
        catch (Exception)
        {
            stats.IncrementHandlerErrors();
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
}

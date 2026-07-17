using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ISO8583Net.Server.Pipeline.Messages;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Server.Pipeline;

/// <summary>
/// Reads <see cref="OutboundMessage"/> instances from an input channel,
/// frames them with a 2-byte length prefix, and writes to the stream.
/// Runs as a fire-and-forget task owned by <see cref="ConnectionPipeline"/>.
/// </summary>
internal static class WriterStage
{
    private const int LengthPrefixSize = 2;

    /// <summary>
    /// Run the writer loop until the channel is completed or cancelled.
    /// </summary>
    /// <param name="stream">The TLS-wrapped (or raw) network stream.</param>
    /// <param name="input">Channel to read outbound messages from.</param>
    /// <param name="stats">Per-connection statistics to update.</param>
    /// <param name="logger">Structured logger for this stage.</param>
    /// <param name="ct">Cancellation token for shutdown.</param>
    public static async Task RunAsync(
        Stream stream,
        ChannelReader<OutboundMessage> input,
        PipelineStats stats,
        ILogger logger,
        CancellationToken ct)
    {
        logger.LogDebug("Writer stage started");

        try
        {
            await foreach (var msg in input.ReadAllAsync(ct))
            {
                await WriteMessageAsync(stream, msg, stats, ct);
            }
        }
        catch (OperationCanceledException) { /* graceful shutdown */ }
        catch (Exception ex)
        {
            logger.LogError(ex, "Writer stage error");
        }
        finally
        {
            logger.LogDebug("Writer stage completed");
        }
    }

    private static async Task WriteMessageAsync(
        Stream stream, OutboundMessage msg, PipelineStats stats, CancellationToken ct)
    {
        byte[] framed;
        int frameLength;

        if (msg.PreFramed != null)
        {
            // Pre-framed: LI already included — write directly
            framed = msg.PreFramed;
            frameLength = framed.Length;
        }
        else if (msg.Message != null)
        {
            // Pack ISOMessage, then frame
            byte[] packed = msg.Message.Pack();
            frameLength = LengthPrefixSize + packed.Length;
            framed = new byte[frameLength];
            framed[0] = (byte)(packed.Length >> 8);
            framed[1] = (byte)(packed.Length & 0xFF);
            Array.Copy(packed, 0, framed, LengthPrefixSize, packed.Length);
        }
        else
        {
            return; // nothing to send
        }

        await stream.WriteAsync(framed, 0, frameLength, ct);
        await stream.FlushAsync(ct);
        stats.AddBytesSent(frameLength);
        stats.IncrementMessagesSent();
    }
}

using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ISO8583Net.Server.Pipeline.Messages;

namespace ISO8583Net.Server.Pipeline;

/// <summary>
/// Reads length-prefixed ISO 8583 frames from a stream and pushes
/// <see cref="RawMessage"/> instances into an output channel.
/// Runs as a fire-and-forget task owned by <see cref="ConnectionPipeline"/>.
/// </summary>
internal static class ReaderStage
{
    private const int LengthPrefixSize = 2;
    private const int MaxMessageSize = 4096;

    /// <summary>
    /// Run the reader loop until cancelled or stream closes.
    /// </summary>
    /// <param name="stream">The TLS-wrapped (or raw) network stream.</param>
    /// <param name="output">Channel to push raw frames into.</param>
    /// <param name="stats">Per-connection statistics to update.</param>
    /// <param name="ct">Cancellation token for shutdown.</param>
    public static async Task RunAsync(
        Stream stream,
        ChannelWriter<RawMessage> output,
        PipelineStats stats,
        CancellationToken ct)
    {
        var lengthBuf = new byte[LengthPrefixSize];

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // ── Read 2-byte length prefix ─────────────────────────
                try
                {
                    await ReadExactlyAsync(stream, lengthBuf, LengthPrefixSize, ct);
                }
                catch (EndOfStreamException)
                {
                    break; // client disconnected cleanly
                }

                int msgLen = (lengthBuf[0] << 8) | lengthBuf[1];

                // LI=0 is a keepalive heartbeat — silently ignore
                if (msgLen == 0)
                    continue;

                if (msgLen > MaxMessageSize)
                {
                    // Skip oversized payload to stay in sync (capped to prevent DoS)
                    int toSkip = Math.Min(msgLen, MaxMessageSize);
                    var skipBuf = ArrayPool<byte>.Shared.Rent(toSkip);
                    try
                    {
                        await ReadExactlyAsync(stream, skipBuf, toSkip, ct);
                    }
                    catch (EndOfStreamException) { break; }
                    finally { ArrayPool<byte>.Shared.Return(skipBuf); }
                    continue;
                }

                // ── Read message body ─────────────────────────────────
                byte[] buf = ArrayPool<byte>.Shared.Rent(msgLen);
                bool readOk = false;
                try
                {
                    await ReadExactlyAsync(stream, buf, msgLen, ct);
                    readOk = true;
                    stats.AddBytesReceived(LengthPrefixSize + msgLen);

                    var raw = new RawMessage(buf, msgLen, stats.ConnectionNumber, DateTime.UtcNow);

                    // Push to channel — will asynchronously wait if bounded channel is full
                    await output.WriteAsync(raw, ct);
                    stats.IncrementMessagesReceived();
                    // Buffer ownership transfers to consumer — do NOT return here
                }
                catch (EndOfStreamException)
                {
                    if (!readOk) ArrayPool<byte>.Shared.Return(buf);
                    break;
                }
                catch
                {
                    if (!readOk) ArrayPool<byte>.Shared.Return(buf);
                    throw;
                }
            }
        }
        catch (OperationCanceledException) { /* graceful shutdown */ }
        catch (Exception ex)
        {
            // Log via callback? For now, just mark complete.
            // Future: inject ILogger or use OnLog callback
        }
        finally
        {
            output.Complete();
        }
    }

    /// <summary>Reads exactly <paramref name="count"/> bytes from the stream.</summary>
    private static async Task ReadExactlyAsync(
        Stream stream, byte[] buf, int count, CancellationToken ct)
    {
        int off = 0;
        while (off < count)
        {
            int n = await stream.ReadAsync(buf.AsMemory(off, count - off), ct);
            if (n == 0) throw new EndOfStreamException($"Stream closed after {off}/{count} bytes.");
            off += n;
        }
    }
}

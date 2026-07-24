using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ISO8583Net.Server.Pipeline.Messages;
using Microsoft.Extensions.Logging;

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
    private static readonly TimeSpan CircuitBreakerPollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Run the reader loop until cancelled or stream closes.
    /// </summary>
    /// <param name="stream">The TLS-wrapped (or raw) network stream.</param>
    /// <param name="output">Channel to push raw frames into.</param>
    /// <param name="stats">Per-connection statistics to update.</param>
    /// <param name="logger">Structured logger for this stage.</param>
    /// <param name="circuitBreaker">Optional connection-level circuit breaker.</param>
    /// <param name="ct">Cancellation token for shutdown.</param>
    public static async Task RunAsync(
        Stream stream,
        ChannelWriter<RawMessage> output,
        PipelineStats stats,
        ILogger logger,
        CircuitBreakerState? circuitBreaker,
        CancellationToken ct)
    {
        var lengthBuf = new byte[LengthPrefixSize];
        logger.LogDebug("Reader stage started");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // ── Check circuit breaker ─────────────────────────────
                if (circuitBreaker is { IsOpen: true })
                {
                    logger.LogWarning("Circuit breaker open — reader paused for parser cooldown");
                    try
                    {
                        await Task.Delay(CircuitBreakerPollInterval, ct);
                    }
                    catch (OperationCanceledException) { break; }
                    continue;
                }

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
                    logger.LogWarning("Oversized message ({Len}B) — skipping", msgLen);
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

                    // ── Detailed hex dump (Info level for diagnostics) ──
                    if (logger.IsEnabled(LogLevel.Information))
                    {
                        string hexDump = FormatHexDump(lengthBuf, buf, msgLen, stats.ConnectionNumber);
                        logger.LogInformation("{HexDump}", hexDump);
                    }

                    var raw = new RawMessage(buf, msgLen, stats.ConnectionNumber, DateTime.UtcNow);

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
            logger.LogError(ex, "Reader stage error");
        }
        finally
        {
            output.Complete();
            logger.LogDebug("Reader stage completed");
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

    /// <summary>
    /// Formats a hex dump like the old build:
    ///   [#1] Received 41 bytes (LI=0x0029)
    ///   [#1] ── Hex Dump (43 bytes) ──
    ///   0000  00 29 47 32 42 ...  |·)G2B...|
    ///           LI = 0x0029 = 41
    /// </summary>
    private static string FormatHexDump(byte[] lengthPrefix, byte[] data, int dataLen, int connNum)
    {
        int totalLen = LengthPrefixSize + dataLen;
        var sb = new StringBuilder(4096);

        sb.AppendLine($"[#{connNum}] Received {dataLen} bytes (LI=0x{dataLen:X4})");
        sb.AppendLine($"[#{connNum}] ── Hex Dump ({totalLen} bytes) ──");

        // Combine LI + data for a single hex dump
        byte[] combined = new byte[totalLen];
        Buffer.BlockCopy(lengthPrefix, 0, combined, 0, LengthPrefixSize);
        Buffer.BlockCopy(data, 0, combined, LengthPrefixSize, dataLen);

        for (int offset = 0; offset < totalLen; offset += 16)
        {
            sb.Append($"{offset:X4}  ");

            // Hex bytes
            for (int j = 0; j < 16; j++)
            {
                int pos = offset + j;
                if (pos < totalLen)
                    sb.Append($"{combined[pos]:X2} ");
                else
                    sb.Append("   ");
            }

            sb.Append(' ');

            // ASCII representation
            sb.Append('|');
            for (int j = 0; j < 16; j++)
            {
                int pos = offset + j;
                if (pos < totalLen)
                {
                    byte b = combined[pos];
                    sb.Append(b >= 32 && b <= 126 ? (char)b : '.');
                }
                else
                {
                    sb.Append(' ');
                }
            }
            sb.Append('|');

            if (offset + 16 < totalLen)
                sb.AppendLine();
        }

        sb.AppendLine();
        sb.Append($"        LI = 0x{dataLen:X4} = {dataLen}");

        return sb.ToString();
    }
}

using System.Buffers;
using Microsoft.Extensions.Logging;
using ISO8583Net.Message;
using ISO8583Net.Packager;

namespace ISO8583Net.Simulator.Framing;

/// <summary>
/// Reads length-prefixed ISO 8583 frames from a stream.
/// Mirrors the server-side ReaderStage but simplified for client use:
/// 2-byte big-endian length prefix, max 4096 byte message body.
/// Each read frame is unpacked into an ISOMessage and dispatched via callback.
/// </summary>
public sealed class FrameReader
{
    private const int LengthPrefixSize = 2;
    private const int MaxMessageSize = 4096;

    private readonly ILogger _logger;
    private readonly Func<ISOMessage, Task> _onMessageReceived;

    public FrameReader(ILogger<FrameReader> logger, Func<ISOMessage, Task> onMessageReceived)
    {
        _logger = logger;
        _onMessageReceived = onMessageReceived;
    }

    /// <summary>
    /// Run the reader loop until cancelled or stream closes.
    /// </summary>
    public async Task RunAsync(
        Stream stream,
        ISOMessagePackager packager,
        CancellationToken ct)
    {
        var lengthBuf = new byte[LengthPrefixSize];
        _logger.LogDebug("FrameReader started");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Read 2-byte length prefix
                try
                {
                    await ReadExactlyAsync(stream, lengthBuf, LengthPrefixSize, ct);
                }
                catch (EndOfStreamException)
                {
                    _logger.LogDebug("Stream closed — FrameReader exiting");
                    break;
                }

                int msgLen = (lengthBuf[0] << 8) | lengthBuf[1];

                // LI=0 is a keepalive — ignore
                if (msgLen == 0)
                    continue;

                if (msgLen > MaxMessageSize)
                {
                    _logger.LogWarning("Oversized message ({Len}B) — skipping", msgLen);
                    int toSkip = Math.Min(msgLen, MaxMessageSize);
                    var skipBuf = ArrayPool<byte>.Shared.Rent(toSkip);
                    try { await ReadExactlyAsync(stream, skipBuf, toSkip, ct); }
                    catch (EndOfStreamException) { break; }
                    finally { ArrayPool<byte>.Shared.Return(skipBuf); }
                    continue;
                }

                // Read message body
                byte[] buf = ArrayPool<byte>.Shared.Rent(msgLen);
                bool readOk = false;
                try
                {
                    await ReadExactlyAsync(stream, buf, msgLen, ct);
                    readOk = true;

                    // Copy to exact-size array for unpacking
                    byte[] body = buf.AsSpan(0, msgLen).ToArray();

                    try
                    {
                        // Create a temporary ISOMessage and unpack
                        // Note: ISOMessage constructor takes (ILogger, ISOMessagePackager)
                        var message = new ISOMessage(
                            _logger as ILogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
                            packager);
                        message.UnPack(body);
                        await _onMessageReceived(message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to unpack received frame");
                    }
                }
                catch (EndOfStreamException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "FrameReader error reading body");
                    break;
                }
                finally
                {
                    if (readOk) ArrayPool<byte>.Shared.Return(buf);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("FrameReader cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FrameReader fatal error — reader shutting down");
        }
        finally
        {
            _logger.LogDebug("FrameReader completed");
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

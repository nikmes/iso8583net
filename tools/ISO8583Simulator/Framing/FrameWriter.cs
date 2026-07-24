using ISO8583Net.Message;

namespace ISO8583Net.Simulator.Framing;

/// <summary>
/// Frames and writes ISO 8583 messages to a stream.
/// Mirrors the server-side WriterStage: packs the message, prepends a 2-byte
/// big-endian length prefix, and writes the complete frame.
/// </summary>
public sealed class FrameWriter
{
    private const int LengthPrefixSize = 2;

    /// <summary>
    /// Pack the message, frame with 2-byte length prefix, and write to stream.
    /// </summary>
    /// <param name="stream">The network stream to write to.</param>
    /// <param name="message">The ISOMessage to pack and send.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task WriteAsync(Stream stream, ISOMessage message, CancellationToken ct = default)
    {
        var mti = message.GetFieldValue(0);
        byte[] packed = message.Pack();
        int frameLength = LengthPrefixSize + packed.Length;
        byte[] framed = new byte[frameLength];

        // Big-endian length prefix
        framed[0] = (byte)(packed.Length >> 8);
        framed[1] = (byte)(packed.Length & 0xFF);

        // Copy packed body after length prefix
        Array.Copy(packed, 0, framed, LengthPrefixSize, packed.Length);

        await stream.WriteAsync(framed, 0, frameLength, ct);
        await stream.FlushAsync(ct);
    }
}

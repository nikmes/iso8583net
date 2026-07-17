using System;
using System.Buffers;

namespace ISO8583Net.Server.Pipeline.Messages;

/// <summary>
/// Raw bytes read from the socket, backed by an <see cref="ArrayPool{T}"/> lease.
/// The consumer MUST call <see cref="Return"/> after unpacking.
/// </summary>
public readonly struct RawMessage : IDisposable
{
    private readonly byte[]? _buffer;

    /// <summary>The raw message bytes (length-prefix already stripped).</summary>
    public ReadOnlyMemory<byte> Data => _buffer != null
        ? new ReadOnlyMemory<byte>(_buffer, 0, Length)
        : ReadOnlyMemory<byte>.Empty;

    /// <summary>Actual message length within the buffer.</summary>
    public int Length { get; }

    /// <summary>Connection number (monotonically incrementing).</summary>
    public int ConnectionNumber { get; }

    /// <summary>Timestamp when the frame was fully read from the socket.</summary>
    public DateTime ReceivedAt { get; }

    public RawMessage(byte[] buffer, int length, int connectionNumber, DateTime receivedAt)
    {
        _buffer = buffer;
        Length = length;
        ConnectionNumber = connectionNumber;
        ReceivedAt = receivedAt;
    }

    /// <summary>Return the rented buffer to the pool.</summary>
    public void Return()
    {
        if (_buffer != null)
            ArrayPool<byte>.Shared.Return(_buffer);
    }

    /// <summary>Convenience — same as <see cref="Return"/>.</summary>
    public void Dispose() => Return();
}

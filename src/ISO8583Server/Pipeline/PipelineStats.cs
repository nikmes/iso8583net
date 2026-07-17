using System;
using System.Threading;

namespace ISO8583Net.Server.Pipeline;

/// <summary>
/// Thread-safe per-connection pipeline statistics for monitoring and diagnostics.
/// </summary>
public sealed class PipelineStats
{
    private long _messagesReceived;
    private long _messagesSent;
    private long _parseErrors;
    private long _handlerErrors;
    private long _bytesReceived;
    private long _bytesSent;

    /// <summary>Connection number.</summary>
    public int ConnectionNumber { get; init; }

    /// <summary>Remote endpoint (IP:port).</summary>
    public string RemoteEndpoint { get; init; } = "";

    /// <summary>When the connection was established.</summary>
    public DateTime ConnectedAt { get; init; }

    /// <summary>Total messages received on this connection.</summary>
    public long MessagesReceived => Interlocked.Read(ref _messagesReceived);

    /// <summary>Total messages sent on this connection.</summary>
    public long MessagesSent => Interlocked.Read(ref _messagesSent);

    /// <summary>Number of parse failures.</summary>
    public long ParseErrors => Interlocked.Read(ref _parseErrors);

    /// <summary>Number of unhandled handler exceptions.</summary>
    public long HandlerErrors => Interlocked.Read(ref _handlerErrors);

    /// <summary>Total bytes received.</summary>
    public long BytesReceived => Interlocked.Read(ref _bytesReceived);

    /// <summary>Total bytes sent.</summary>
    public long BytesSent => Interlocked.Read(ref _bytesSent);

    /// <summary>Currently in-flight messages (being processed by handlers).</summary>
    public int InFlight { get; set; }

    /// <summary>Current write queue depth (messages waiting to be written).</summary>
    public int WriteQueueLength { get; set; }

    // ── Increment helpers ──────────────────────────────────────────────

    public void IncrementMessagesReceived() => Interlocked.Increment(ref _messagesReceived);
    public void IncrementMessagesSent() => Interlocked.Increment(ref _messagesSent);
    public void IncrementParseErrors() => Interlocked.Increment(ref _parseErrors);
    public void IncrementHandlerErrors() => Interlocked.Increment(ref _handlerErrors);
    public void AddBytesReceived(long count) => Interlocked.Add(ref _bytesReceived, count);
    public void AddBytesSent(long count) => Interlocked.Add(ref _bytesSent, count);
}

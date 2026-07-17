using ISO8583Net.Message;

namespace ISO8583Net.Server.Pipeline.Messages;

/// <summary>
/// A message ready to be framed and written to the socket.
/// Use <see cref="FromISOMessage"/> for unpacked messages (packed by writer),
/// or <see cref="FromPreFramed"/> for pre-packed byte arrays.
/// </summary>
public readonly struct OutboundMessage
{
    /// <summary>The ISO message to pack (null if pre-framed).</summary>
    public ISOMessage? Message { get; }

    /// <summary>Pre-framed bytes (null if ISOMessage is used).</summary>
    public byte[]? PreFramed { get; }

    /// <summary>Connection number to send to.</summary>
    public int ConnectionNumber { get; }

    /// <summary>
    /// Priority flag. When true, the writer may bypass the queue for urgent
    /// messages like SignOn responses (future optimization hook).
    /// </summary>
    public bool IsPriority { get; }

    private OutboundMessage(ISOMessage? message, byte[]? preFramed, int connectionNumber, bool isPriority)
    {
        Message = message;
        PreFramed = preFramed;
        ConnectionNumber = connectionNumber;
        IsPriority = isPriority;
    }

    /// <summary>Create an outbound message from an ISOMessage (writer will pack it).</summary>
    public static OutboundMessage FromISOMessage(ISOMessage message, int connectionNumber, bool isPriority = false)
        => new(message, null, connectionNumber, isPriority);

    /// <summary>Create an outbound message from pre-framed bytes (2-byte LI already included).</summary>
    public static OutboundMessage FromPreFramed(byte[] preFramed, int connectionNumber, bool isPriority = false)
        => new(null, preFramed, connectionNumber, isPriority);
}

using System;
using ISO8583Net.Message;
using ISO8583Net.Packager;

namespace ISO8583Net.Server.Pipeline.Messages;

/// <summary>
/// A fully parsed ISO 8583 message ready for routing to handlers.
/// </summary>
public sealed class ParsedMessage
{
    /// <summary>The unpacked ISO message.</summary>
    public ISOMessage Message { get; }

    /// <summary>Connection number.</summary>
    public int ConnectionNumber { get; }

    /// <summary>Hex dump of the raw frame (for logging/diagnostics).</summary>
    public string HexDump { get; }

    /// <summary>Timestamp when parsing completed.</summary>
    public DateTime ParsedAt { get; }

    /// <summary>Remote endpoint string (IP:port).</summary>
    public string RemoteEndpoint { get; }

    /// <summary>The dialect-validation outcome for this inbound message, or null.</summary>
    public DialectValidationResult? ValidationResult { get; }

    public ParsedMessage(
        ISOMessage message,
        int connectionNumber,
        string hexDump,
        string remoteEndpoint,
        DateTime parsedAt,
        DialectValidationResult? validationResult)
    {
        Message = message;
        ConnectionNumber = connectionNumber;
        HexDump = hexDump;
        RemoteEndpoint = remoteEndpoint;
        ParsedAt = parsedAt;
        ValidationResult = validationResult;
    }
}

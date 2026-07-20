using System;

namespace ISO8583Service.Tracing;

/// <summary>
/// Entity for persisting ISO 8583 message traces to a relational database.
/// </summary>
public sealed class TracedMessage
{
    public long Id { get; set; }

    /// <summary>UTC timestamp when the trace was recorded.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Trace type: RECV, SEND, PARSE_ERR, NO_RESP, HANDLER_ERR.</summary>
    public string TraceType { get; set; } = string.Empty;

    /// <summary>Message Type Indicator (e.g. "0100", "0110").</summary>
    public string? Mti { get; set; }

    /// <summary>Connection identifier.</summary>
    public int ConnectionNumber { get; set; }

    /// <summary>Number of populated fields in the parsed message.</summary>
    public int? FieldsCount { get; set; }

    /// <summary>Raw hex dump of received bytes (truncated).</summary>
    public string? RawHex { get; set; }

    /// <summary>Hex dump of the parsed ISOMessage (truncated).</summary>
    public string? ParsedHex { get; set; }

    /// <summary>Hex dump of the response ISOMessage (truncated).</summary>
    public string? ResponseHex { get; set; }

    /// <summary>Name of the handler that processed the message.</summary>
    public string? HandlerName { get; set; }

    /// <summary>Handler elapsed time in milliseconds.</summary>
    public double? ElapsedMs { get; set; }

    /// <summary>Error message if handling failed.</summary>
    public string? ErrorMessage { get; set; }
}

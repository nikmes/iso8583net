namespace ISO8583Net.Simulator.Models;

/// <summary>DTO for POST /api/messages/send.</summary>
public sealed class SendMessageRequest
{
    /// <summary>Message Type Indicator (e.g., "0100", "0200").</summary>
    public string Mti { get; set; } = string.Empty;

    /// <summary>Optional field overrides. Key = field number as string.</summary>
    public Dictionary<string, string>? FieldOverrides { get; set; }

    /// <summary>Response timeout in milliseconds. Default 30000.</summary>
    public int TimeoutMs { get; set; } = 30000;
}

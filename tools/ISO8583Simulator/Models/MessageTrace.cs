namespace ISO8583Net.Simulator.Models;

/// <summary>DTO for a single message exchange in the history.</summary>
public sealed class MessageTrace
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string RequestMti { get; set; } = string.Empty;
    public string? ResponseMti { get; set; }
    public string? Stan { get; set; }
    public string? F39 { get; set; }
    public double ElapsedMs { get; set; }
}

/// <summary>DTO for the message history query response.</summary>
public sealed class MessageHistoryResponse
{
    public List<MessageTrace> Messages { get; set; } = new();
    public int Total { get; set; }
}

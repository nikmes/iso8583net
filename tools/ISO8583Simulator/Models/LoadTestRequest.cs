namespace ISO8583Net.Simulator.Models;

/// <summary>DTO for POST /api/loadtest/start.</summary>
public sealed class LoadTestRequest
{
    /// <summary>MTI to send for each request.</summary>
    public string Mti { get; set; } = "0100";

    /// <summary>Total number of requests to send.</summary>
    public int TotalCount { get; set; } = 10000;

    /// <summary>Number of concurrent senders.</summary>
    public int Concurrency { get; set; } = 10;

    /// <summary>Per-request timeout in milliseconds.</summary>
    public int TimeoutMs { get; set; } = 30000;
}

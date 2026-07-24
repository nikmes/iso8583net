namespace ISO8583Net.Simulator.Models;

/// <summary>DTO for GET /api/simulator/status.</summary>
public sealed class SimulatorStatus
{
    public string State { get; set; } = "Disconnected";
    public DateTime? ConnectedAt { get; set; }
    public double UptimeSeconds { get; set; }
    public SimulatorStatsDto Stats { get; set; } = new();
}

/// <summary>Statistics snapshot.</summary>
public sealed class SimulatorStatsDto
{
    public long MessagesSent { get; set; }
    public long ResponsesReceived { get; set; }
    public long Errors { get; set; }
    public double AvgLatencyMs { get; set; }
    public double P99LatencyMs { get; set; }
    public double ThroughputMsgPerSec { get; set; }
}

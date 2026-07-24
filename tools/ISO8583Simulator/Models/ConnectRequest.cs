namespace ISO8583Net.Simulator.Models;

/// <summary>DTO for POST /api/simulator/connect.</summary>
public sealed class ConnectRequest
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 9443;
    public bool TlsEnabled { get; set; } = true;
    public bool TlsAllowUntrusted { get; set; } = true;
    public string? DialectPath { get; set; }
}

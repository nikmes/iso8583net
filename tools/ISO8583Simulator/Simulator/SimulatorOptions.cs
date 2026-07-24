namespace ISO8583Net.Simulator;

public sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 9443;
    public bool TlsEnabled { get; set; } = true;
    public string? TlsCertPath { get; set; }
    public bool TlsAllowUntrusted { get; set; } = true;
    public int ConnectTimeoutSeconds { get; set; } = 10;
    public int ResponseTimeoutSeconds { get; set; } = 30;
    public string DialectPath { get; set; } = "Dialects/d8-iso8583.json";
    public List<string> Scenarios { get; set; } = new() { "FullLifecycle" };
}

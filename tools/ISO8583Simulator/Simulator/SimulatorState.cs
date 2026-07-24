namespace ISO8583Net.Simulator;

/// <summary>
/// Represents the connection state of the simulator session.
/// </summary>
public enum SimulatorState
{
    /// <summary>Not connected to the ISO8583 server.</summary>
    Disconnected,

    /// <summary>TCP/TLS connection in progress.</summary>
    Connecting,

    /// <summary>Connected and idle — ready to send/receive messages.</summary>
    Connected,

    /// <summary>Connected and actively running scenarios or load tests.</summary>
    Running
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ISO8583Net.Server;

/// <summary>
/// Abstraction for an ISO 8583 TCP server. Decouples networking/parsing from the UI layer.
/// </summary>
public interface IIso8583Server
{
    /// <summary>Whether the server is currently listening.</summary>
    bool IsRunning { get; }

    /// <summary>Number of currently connected clients.</summary>
    int ConnectionCount { get; }

    /// <summary>Starts listening on the specified port using the given dialect.</summary>
    /// <param name="port">TCP port to bind.</param>
    /// <param name="dialectPath">Path to JSON dialect file, or null for built-in VISA.</param>
    /// <param name="ct">Cancellation token to stop the server.</param>
    Task StartAsync(int port, string? dialectPath, CancellationToken ct = default);

    /// <summary>Stops the server gracefully.</summary>
    Task StopAsync();

    /// <summary>
    /// Manually send a SignOn request (MTI 1800, F24=801) to all connected clients.
    /// </summary>
    Task SendSignOnAsync(CancellationToken ct = default);

    /// <summary>
    /// Manually send an Echo message (MTI 1800, F24=831) to all connected clients.
    /// </summary>
    Task SendEchoAsync(CancellationToken ct = default);

    /// <summary>
    /// Manually send a SignOff request (MTI 1800, F24=803) to all connected clients
    /// and optionally disconnect them afterwards.
    /// </summary>
    Task SendSignOffAsync(bool disconnectAfter = false, CancellationToken ct = default);

    /// <summary>
    /// Interval in seconds for periodically sending a SignOn request (MTI 1800)
    /// to each connected client. Set to 0 to disable.
    /// </summary>
    int SignOnIntervalSeconds { get; set; }

    /// <summary>Send a SignOn request immediately when a client connects.</summary>
    bool SendSignOnOnConnect { get; set; }

    /// <summary>Enable/disable periodic SignOn requests. When false, SignOnIntervalSeconds is ignored.</summary>
    bool EnablePeriodicSignOn { get; set; }

    /// <summary>TLS configuration. Set <see cref="TlsOptions.IsEnabled"/> to true for TLS.</summary>
    TlsOptions Tls { get; set; }

    /// <summary>Snapshot of active connections for monitoring.</summary>
    IReadOnlyList<(int ConnNum, string RemoteEndpoint, DateTime ConnectedAt)> GetConnections();

    /// <summary>Callback for log messages (thread-safe, may be called from any thread).</summary>
    Action<string>? OnLog { get; set; }

    /// <summary>Callback for status bar text changes.</summary>
    Action<string>? OnStatusChanged { get; set; }

    /// <summary>
    /// Callback invoked when a complete ISO message has been received and parsed.
    /// Parameters: connection number, raw bytes, hex dump string, parsed field dump string.
    /// </summary>
    Action<int, byte[], string, string>? OnMessageParsed { get; set; }
}

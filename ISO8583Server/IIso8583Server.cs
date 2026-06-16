using System;
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

    /// <summary>Starts listening on the specified port using the given dialect.</summary>
    /// <param name="port">TCP port to bind.</param>
    /// <param name="dialectPath">Path to JSON dialect file, or null for built-in VISA.</param>
    /// <param name="ct">Cancellation token to stop the server.</param>
    Task StartAsync(int port, string? dialectPath, CancellationToken ct = default);

    /// <summary>Stops the server gracefully.</summary>
    Task StopAsync();

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

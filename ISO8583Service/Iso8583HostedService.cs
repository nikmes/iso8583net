using System;
using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Server;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ISO8583Service;

/// <summary>
/// Wraps <see cref="IIso8583Server"/> as an <see cref="IHostedService"/>
/// so it starts/stops with the generic host lifetime.
/// </summary>
public sealed class Iso8583HostedService : IHostedService
{
    private readonly IIso8583Server _server;
    private readonly ILogger<Iso8583HostedService> _logger;
    private readonly ServerOptions _options;

    public Iso8583HostedService(
        IIso8583Server server,
        ILogger<Iso8583HostedService> logger,
        IOptions<ServerOptions> options)
    {
        _server = server;
        _logger = logger;
        _options = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Wire server callbacks to Serilog
        _server.OnLog = msg =>
        {
            // Split multi-line messages into individual log entries
            foreach (var line in msg.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                _logger.LogInformation("{Line}", line.TrimEnd('\r'));
        };

        _server.OnStatusChanged = status =>
            _logger.LogInformation("Status: {Status}", status);

        _server.OnMessageParsed = (connNum, bytes, hexDump, parsed) =>
        {
            _logger.LogInformation("[#{ConnNum}] Received {Bytes} bytes", connNum, bytes.Length);
            _logger.LogInformation("[#{ConnNum}] ── Parsed ──\n{Dump}", connNum, parsed);
        };

        string? dialectPath = string.IsNullOrWhiteSpace(_options.DialectPath)
            ? null
            : _options.DialectPath;

        await _server.StartAsync(_options.Port, dialectPath, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _server.StopAsync();
    }
}

/// <summary>
/// Configuration options for the ISO 8583 server.
/// </summary>
public sealed class ServerOptions
{
    public const string SectionName = "Iso8583Server";

    public int Port { get; set; } = 9090;
    public string? DialectPath { get; set; }
}

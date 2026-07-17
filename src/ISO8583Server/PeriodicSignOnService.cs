using System;
using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Server.Pipeline;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Server;

/// <summary>
/// Background service that periodically sends SignOn (or Echo) requests
/// to all connected clients using a <see cref="PeriodicTimer"/>.
///
/// Replaces the old 1-second polling loop. The timer fires at the configured
/// interval and pushes messages to each connection's writer channel.
/// </summary>
public sealed class PeriodicSignOnService : BackgroundService
{
    private readonly IIso8583Server _server;
    private readonly PipelineHost _pipelineHost;
    private readonly ILogger<PeriodicSignOnService> _logger;

    public PeriodicSignOnService(
        IIso8583Server server,
        PipelineHost pipelineHost,
        ILogger<PeriodicSignOnService> logger)
    {
        _server = server;
        _pipelineHost = pipelineHost;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Wait until the server is running and periodic SignOn is enabled
            while (!_server.IsRunning || !_server.EnablePeriodicSignOn)
            {
                await Task.Delay(1000, ct);
            }

            int interval = _server.SignOnIntervalSeconds;
            if (interval <= 0)
            {
                await Task.Delay(1000, ct);
                continue;
            }

            _logger.LogDebug("Periodic SignOn timer started (interval={Interval}s)", interval);

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));

            try
            {
                while (await timer.WaitForNextTickAsync(ct))
                {
                    if (!_server.IsRunning || !_server.EnablePeriodicSignOn)
                        break; // config changed — re-read

                    try
                    {
                        _logger.LogInformation(
                            "Sending periodic Echo to {Count} connection(s)",
                            _pipelineHost.ConnectionCount);

                        await _pipelineHost.BroadcastSignOnRequestAsync("831", ct);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Periodic SignOn broadcast failed");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // timer stopped or shutdown
            }
        }
    }
}

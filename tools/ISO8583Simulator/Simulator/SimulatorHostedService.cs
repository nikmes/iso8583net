using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ISO8583Net.Simulator;

/// <summary>
/// ASP.NET Core hosted service that manages the SimulatorSession lifecycle.
/// Does not auto-connect — connection is triggered via the REST API.
/// Handles graceful shutdown by disconnecting and draining pending requests.
/// </summary>
public sealed class SimulatorHostedService : BackgroundService
{
    private readonly SimulatorSession _session;
    private readonly SimulatorOptions _options;
    private readonly ILogger<SimulatorHostedService> _logger;

    public SimulatorHostedService(
        SimulatorSession session,
        IOptions<SimulatorOptions> options,
        ILogger<SimulatorHostedService> logger)
    {
        _session = session;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// The hosted service does not auto-connect. It simply waits for shutdown.
    /// Connection is triggered via POST /api/simulator/connect.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SimulatorHostedService started. Waiting for connection commands...");

        try
        {
            // Keep the service alive until shutdown is requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SimulatorHostedService stopping...");
        }
    }

    /// <summary>
    /// Graceful shutdown: disconnect from the server and drain pending requests.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SimulatorHostedService stopping...");

        try
        {
            await _session.DisconnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disconnect on shutdown");
        }

        await base.StopAsync(cancellationToken);
        _logger.LogInformation("SimulatorHostedService stopped");
    }
}

using System.Diagnostics;
using ISO8583Net.Message;
using ISO8583Net.Simulator.Builders;
using ISO8583Net.Simulator.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Simulator.Scenarios;

/// <summary>
/// Orchestrates execution of registered <see cref="IScenario"/> instances,
/// collecting results into a <see cref="ScenarioReport"/>.
/// </summary>
public class ScenarioRunner
{
    private readonly IEnumerable<IScenario> _scenarios;
    private readonly ILogger<ScenarioRunner> _logger;
    private readonly IHubContext<SimulatorHub>? _hubContext;

    public ScenarioRunner(IEnumerable<IScenario> scenarios, ILogger<ScenarioRunner> logger,
        IHubContext<SimulatorHub>? hubContext = null)
    {
        _scenarios = scenarios;
        _logger = logger;
        _hubContext = hubContext;
    }

    /// <summary>Run all registered scenarios and return a report.</summary>
    public async Task<ScenarioReport> RunAllAsync(SimulatorSession session, CancellationToken ct = default)
    {
        var report = new ScenarioReport();
        var sw = Stopwatch.StartNew();

        foreach (var scenario in _scenarios)
        {
            var result = await RunOneAsync(scenario, session, ct);
            report.Results.Add(result);
        }

        sw.Stop();
        report.TotalDuration = sw.Elapsed;
        report.Total = report.Results.Count;
        report.Passed = report.Results.Count(r => r.Passed);
        report.Failed = report.Results.Count(r => !r.Passed);

        _logger.LogInformation("Scenarios complete: {Passed}/{Total} passed in {Elapsed:F1}s",
            report.Passed, report.Total, sw.Elapsed.TotalSeconds);

        return report;
    }

    /// <summary>Run a single scenario by name. Returns null if not found.</summary>
    public async Task<ScenarioResult?> RunAsync(string name, SimulatorSession session, CancellationToken ct = default)
    {
        var scenario = _scenarios.FirstOrDefault(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        if (scenario is null)
        {
            _logger.LogWarning("Scenario '{Name}' not found", name);
            return null;
        }

        return await RunOneAsync(scenario, session, ct);
    }

    private async Task<ScenarioResult> RunOneAsync(IScenario scenario, SimulatorSession session, CancellationToken ct)
    {
        _logger.LogInformation("Running scenario: {Name}", scenario.Name);
        var sw = Stopwatch.StartNew();
        bool passed;
        string? error = null;

        try
        {
            passed = await scenario.RunAsync(session, ct);
        }
        catch (Exception ex)
        {
            passed = false;
            error = ex.Message;
            _logger.LogError(ex, "Scenario '{Name}' failed with exception", scenario.Name);
        }

        sw.Stop();
        var result = new ScenarioResult
        {
            ScenarioName = scenario.Name,
            Passed = passed,
            Duration = sw.Elapsed,
            ErrorMessage = error
        };

        // Fire ScenarioCompleted event
        _ = NotifyScenarioCompletedAsync(scenario.Name, passed, sw.Elapsed.TotalMilliseconds);

        _logger.LogInformation("Scenario '{Name}': {Result} ({Duration:F1}s)",
            scenario.Name, passed ? "PASS" : "FAIL", sw.Elapsed.TotalSeconds);

        return result;
    }

    private async Task NotifyScenarioCompletedAsync(
        string name, bool passed, double durationMs)
    {
        if (_hubContext is null) return;
        try
        {
            await _hubContext.Clients.All.SendAsync("ScenarioCompleted", new ScenarioCompletedDto
            {
                ScenarioName = name,
                Passed = passed,
                DurationMs = durationMs
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to push ScenarioCompleted event");
        }
    }
}

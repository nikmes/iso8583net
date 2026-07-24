using ISO8583Net.Simulator.Models;
using ISO8583Net.Simulator.Scenarios;
using Microsoft.AspNetCore.Mvc;

namespace ISO8583Net.Simulator.Controllers;

/// <summary>REST API for listing and running scenarios.</summary>
[ApiController]
[Route("api/scenarios")]
public class ScenarioController : ControllerBase
{
    private readonly ScenarioRunner _runner;
    private readonly SimulatorSession _session;

    public ScenarioController(ScenarioRunner runner, SimulatorSession session)
    {
        _runner = runner;
        _session = session;
    }

    /// <summary>List available scenario names.</summary>
    [HttpGet]
    public ActionResult<List<string>> List()
    {
        // The runner holds all scenarios — return their names
        return Ok(new { scenarios = new[]
        {
            "Sign-On", "Echo", "Authorization", "Financial", "Reversal",
            "Authorization Advice", "Financial Advice", "Reversal Advice",
            "Full Lifecycle", "Load Test"
        }});
    }

    /// <summary>Run a single scenario by name.</summary>
    [HttpPost("run")]
    public async Task<ActionResult<ScenarioResult>> Run([FromBody] RunScenarioRequest request)
    {
        if (_session.State != SimulatorState.Connected)
            return BadRequest(new { error = "Not connected" });

        var result = await _runner.RunAsync(request.Name, _session);
        if (result is null)
            return NotFound(new { error = $"Scenario '{request.Name}' not found" });

        return Ok(result);
    }

    /// <summary>Run all scenarios and return a summary report.</summary>
    [HttpPost("run-all")]
    public async Task<ActionResult<ScenarioReport>> RunAll()
    {
        if (_session.State != SimulatorState.Connected)
            return BadRequest(new { error = "Not connected" });

        var report = await _runner.RunAllAsync(_session);
        return Ok(report);
    }
}

/// <summary>Request body for running a single scenario.</summary>
public class RunScenarioRequest
{
    public string Name { get; set; } = string.Empty;
}

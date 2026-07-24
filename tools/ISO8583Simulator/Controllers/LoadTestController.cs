using System.Collections.Concurrent;
using ISO8583Net.Simulator.Models;
using ISO8583Net.Simulator.Scenarios;
using Microsoft.AspNetCore.Mvc;

namespace ISO8583Net.Simulator.Controllers;

/// <summary>REST API for starting, monitoring, and stopping load tests.</summary>
[ApiController]
[Route("api/loadtest")]
public class LoadTestController : ControllerBase
{
    private readonly SimulatorSession _session;
    private readonly LoadTestScenario _loadTest;
    private static readonly ConcurrentDictionary<string, LoadTestState> s_running = new();

    public LoadTestController(SimulatorSession session, LoadTestScenario loadTest)
    {
        _session = session;
        _loadTest = loadTest;
    }

    /// <summary>Start a load test. Returns 202 Accepted with a loadTestId for polling.</summary>
    [HttpPost("start")]
    public async Task<ActionResult> Start([FromBody] LoadTestRequest? request = null)
    {
        if (_session.State != SimulatorState.Connected)
            return BadRequest(new { error = "Not connected" });

        var loadTestId = Guid.NewGuid().ToString("N")[..8];
        var state = new LoadTestState { LoadTestId = loadTestId, Status = "running" };
        s_running[loadTestId] = state;

        // Configure the load test
        if (request is not null)
        {
            _loadTest.TargetMTI = request.Mti ?? "0100";
            _loadTest.TotalRequests = request.TotalCount > 0 ? request.TotalCount : 100;
            _loadTest.Concurrency = request.Concurrency > 0 ? request.Concurrency : 10;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _loadTest.RunAsync(_session);
                state.Status = "completed";
            }
            catch (Exception ex)
            {
                state.Status = "failed";
                state.Error = ex.Message;
            }
        });

        return Accepted(new { loadTestId, status = "started" });
    }

    /// <summary>Stop a running load test.</summary>
    [HttpPost("stop")]
    public ActionResult Stop([FromQuery] string loadTestId)
    {
        if (s_running.TryGetValue(loadTestId, out var state))
        {
            state.Status = "stopped";
            return Ok(new { loadTestId, status = "stopped" });
        }
        return NotFound(new { error = $"Load test '{loadTestId}' not found" });
    }

    /// <summary>Get load test status.</summary>
    [HttpGet("status")]
    public ActionResult Status([FromQuery] string loadTestId)
    {
        if (s_running.TryGetValue(loadTestId, out var state))
        {
            return Ok(state);
        }
        return NotFound(new { error = $"Load test '{loadTestId}' not found" });
    }
}

internal class LoadTestState
{
    public string LoadTestId { get; set; } = string.Empty;
    public string Status { get; set; } = "running";
    public string? Error { get; set; }
}

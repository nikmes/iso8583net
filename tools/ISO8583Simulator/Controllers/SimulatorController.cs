using ISO8583Net.Simulator.Models;
using Microsoft.AspNetCore.Mvc;

namespace ISO8583Net.Simulator.Controllers;

/// <summary>REST API for simulator lifecycle: connect, disconnect, status, health.</summary>
[ApiController]
[Route("api/simulator")]
public class SimulatorController : ControllerBase
{
    private readonly SimulatorSession _session;

    public SimulatorController(SimulatorSession session)
    {
        _session = session;
    }

    /// <summary>Connect to the configured ISO8583Server.</summary>
    [HttpPost("connect")]
    public async Task<ActionResult<SimulatorStatus>> Connect([FromBody] ConnectRequest? request = null)
    {
        if (_session.State == SimulatorState.Connected)
            return Conflict(new { error = "Already connected" });

        try
        {
            await _session.ConnectAsync();
            return Ok(BuildStatus());
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Disconnect from the server.</summary>
    [HttpPost("disconnect")]
    public async Task<ActionResult<SimulatorStatus>> Disconnect()
    {
        if (_session.State == SimulatorState.Disconnected)
            return Conflict(new { error = "Not connected" });

        await _session.DisconnectAsync();
        return Ok(BuildStatus());
    }

    /// <summary>Get current simulator status and statistics.</summary>
    [HttpGet("status")]
    public ActionResult<SimulatorStatus> Status()
    {
        return Ok(BuildStatus());
    }

    /// <summary>Simple health check (returns 200 OK).</summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", simulatorState = _session.State.ToString() });
    }

    private SimulatorStatus BuildStatus()
    {
        return new SimulatorStatus
        {
            State = _session.State.ToString(),
            ConnectedAt = _session.ConnectedAt,
            UptimeSeconds = _session.UptimeSeconds,
            Stats = new SimulatorStatsDto
            {
                MessagesSent = _session.Stats.MessagesSent,
                ResponsesReceived = _session.Stats.ResponsesReceived,
                Errors = _session.Stats.Errors,
                AvgLatencyMs = _session.Stats.AvgLatencyMs,
                P99LatencyMs = _session.Stats.P99LatencyMs,
                ThroughputMsgPerSec = _session.Stats.ThroughputMsgPerSec
            }
        };
    }
}

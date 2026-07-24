using ISO8583Net.Simulator.Builders;
using ISO8583Net.Simulator.Models;
using ISO8583Net.Simulator.Scenarios;
using Microsoft.AspNetCore.Mvc;

namespace ISO8583Net.Simulator.Controllers;

/// <summary>REST API for sending individual ISO 8583 messages and browsing history.</summary>
[ApiController]
[Route("api/messages")]
public class MessageController : ControllerBase
{
    private readonly SimulatorSession _session;
    private readonly MessageBuilderRegistry _registry;
    private readonly MessageHistory _history;

    public MessageController(
        SimulatorSession session,
        MessageBuilderRegistry registry,
        MessageHistory history)
    {
        _session = session;
        _registry = registry;
        _history = history;
    }

    /// <summary>Send a request message by MTI and await the response.</summary>
    [HttpPost("send")]
    public async Task<ActionResult<MessageTrace>> Send([FromBody] SendMessageRequest request)
    {
        if (_session.State != SimulatorState.Connected)
            return BadRequest(new { error = "Not connected" });

        var builder = _registry.GetBuilder(request.Mti);
        if (builder is null)
            return BadRequest(new { error = $"No builder for MTI '{request.Mti}'" });

        try
        {
            var timeoutSec = request.TimeoutMs > 0
                ? (int)Math.Ceiling(request.TimeoutMs / 1000.0)
                : 30;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));

            var message = _session.CreateMessage();
            builder.BuildRequest(message);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _session.SendMessageAsync(message, cts.Token);
            sw.Stop();

            var trace = new MessageTrace
            {
                Timestamp = DateTime.UtcNow,
                RequestMti = request.Mti,
                ResponseMti = response?.GetFieldValue(0),
                Stan = message.GetFieldValue(11),
                F39 = response?.GetFieldValue(39),
                ElapsedMs = sw.Elapsed.TotalMilliseconds
            };

            _history.Add(trace);
            return Ok(trace);
        }
        catch (TimeoutException ex)
        {
            return StatusCode(504, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Send an advice message (fire-and-forget, no response expected).</summary>
    [HttpPost("send-advice")]
    public async Task<ActionResult> SendAdvice([FromBody] SendMessageRequest request)
    {
        if (_session.State != SimulatorState.Connected)
            return BadRequest(new { error = "Not connected" });

        if (!_registry.IsAdvice(request.Mti))
            return BadRequest(new { error = $"MTI '{request.Mti}' is not an advice" });

        try
        {
            var builder = _registry.GetBuilder(request.Mti);
            if (builder is null)
                return BadRequest(new { error = $"No builder for MTI '{request.Mti}'" });

            var message = _session.CreateMessage();
            builder.BuildRequest(message);
            await _session.SendMessageAsync(message);

            return Ok(new { mti = request.Mti, stan = message.GetFieldValue(11), sent = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Get recent message history.</summary>
    [HttpGet("recent")]
    public ActionResult<MessageHistoryResponse> Recent(
        [FromQuery] int count = 50,
        [FromQuery] string? mti = null)
    {
        return Ok(_history.GetRecent(Math.Clamp(count, 1, 500), mti));
    }
}

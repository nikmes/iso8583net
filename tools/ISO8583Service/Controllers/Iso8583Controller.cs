using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Server;
using ISO8583Net.Server.Pipeline;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ISO8583Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class Iso8583Controller : ControllerBase
{
    private readonly IIso8583Server _server;
    private readonly PipelineHost _pipelineHost;
    private readonly ServerOptions _options;
    private readonly ILogger<Iso8583Controller> _logger;

    public Iso8583Controller(
        IIso8583Server server,
        PipelineHost pipelineHost,
        IOptions<ServerOptions> options,
        ILogger<Iso8583Controller> logger)
    {
        _server = server;
        _pipelineHost = pipelineHost;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/iso8583/status — Returns current server status, pipeline stats, and configuration.
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var pipelineStats = _pipelineHost.GetStats();

        return Ok(new
        {
            IsRunning = _server.IsRunning,
            ConnectionCount = _server.ConnectionCount,
            HandlerCount = _pipelineHost.HandlerCount,
            ConnectedClients = _server.GetConnections()
                .Select(c => new
                {
                    ConnectionNumber = c.ConnNum,
                    RemoteEndpoint = c.RemoteEndpoint,
                    ConnectedAt = c.ConnectedAt.ToString("O")
                }),
            PipelineStats = new
            {
                TotalConnections = pipelineStats.Count,
                TotalBytesRead = pipelineStats.Sum(s => s.BytesReceived),
                TotalMessagesRead = pipelineStats.Sum(s => s.MessagesReceived),
                TotalBytesWritten = pipelineStats.Sum(s => s.BytesSent),
                TotalMessagesWritten = pipelineStats.Sum(s => s.MessagesSent),
                TotalParseErrors = pipelineStats.Sum(s => s.ParseErrors),
                InFlight = pipelineStats.Sum(s => s.InFlight),
                HandlerErrors = pipelineStats.Sum(s => s.HandlerErrors)
            },
            Config = new
            {
                _options.Port,
                _options.DialectPath,
                _options.SignOnIntervalSeconds,
                _options.SendSignOnOnConnect,
                _options.EnablePeriodicSignOn,
                _options.TlsEnabled
            }
        });
    }

    /// <summary>
    /// POST /api/iso8583/signon — Manually send a SignOn request (MTI 1800, F24=801)
    /// to all connected clients.
    /// </summary>
    [HttpPost("signon")]
    public async Task<IActionResult> SendSignOn(CancellationToken ct)
    {
        if (!_server.IsRunning)
            return BadRequest(new { Error = "Server is not running." });

        if (_server.ConnectionCount == 0)
            return Ok(new { Message = "No connected clients to send SignOn to." });

        await _server.SendSignOnAsync(ct);

        _logger.LogInformation("Manual SignOn sent to {Count} clients.", _server.ConnectionCount);

        return Ok(new
        {
            Message = $"SignOn request sent to {_server.ConnectionCount} client(s).",
            ClientsNotified = _server.ConnectionCount
        });
    }

    /// <summary>
    /// POST /api/iso8583/signoff — Manually send a SignOff request (MTI 1800, F24=803)
    /// to all connected clients. Optionally pass ?disconnect=true to stop the server.
    /// </summary>
    [HttpPost("signoff")]
    public async Task<IActionResult> SendSignOff(
        [FromQuery] bool disconnect = false,
        CancellationToken ct = default)
    {
        if (!_server.IsRunning)
            return BadRequest(new { Error = "Server is not running." });

        if (_server.ConnectionCount == 0)
            return Ok(new { Message = "No connected clients to send SignOff to." });

        await _server.SendSignOffAsync(disconnect, ct);

        _logger.LogInformation("Manual SignOff sent to clients. DisconnectAfter: {Disconnect}", disconnect);

        return Ok(new
        {
            Message = $"SignOff request sent to {_server.ConnectionCount} client(s)." +
                      (disconnect ? " Server has been stopped." : ""),
            ServerStopped = disconnect
        });
    }

    /// <summary>
    /// POST /api/iso8583/echo — Manually send an Echo message (MTI 1800, F24=831)
    /// to all connected clients.
    /// </summary>
    [HttpPost("echo")]
    public async Task<IActionResult> SendEcho(CancellationToken ct)
    {
        if (!_server.IsRunning)
            return BadRequest(new { Error = "Server is not running." });

        if (_server.ConnectionCount == 0)
            return Ok(new { Message = "No connected clients to send Echo to." });

        await _server.SendEchoAsync(ct);

        _logger.LogInformation("Manual Echo sent to {Count} clients.", _server.ConnectionCount);

        return Ok(new
        {
            Message = $"Echo message sent to {_server.ConnectionCount} client(s).",
            ClientsNotified = _server.ConnectionCount
        });
    }

    /// <summary>
    /// PUT /api/iso8583/config — Update runtime configuration.
    /// </summary>
    [HttpPut("config")]
    public IActionResult UpdateConfig([FromBody] ConfigUpdate update)
    {
        if (update.SignOnIntervalSeconds.HasValue)
            _server.SignOnIntervalSeconds = update.SignOnIntervalSeconds.Value;

        if (update.EnablePeriodicSignOn.HasValue)
            _server.EnablePeriodicSignOn = update.EnablePeriodicSignOn.Value;

        _logger.LogInformation(
            "Config updated: SignOnInterval={Interval}s, PeriodicSignOn={Enabled}",
            _server.SignOnIntervalSeconds, _server.EnablePeriodicSignOn);

        return Ok(new
        {
            Message = "Configuration updated.",
            SignOnIntervalSeconds = _server.SignOnIntervalSeconds,
            EnablePeriodicSignOn = _server.EnablePeriodicSignOn
        });
    }

    /// <summary>DTO for runtime config updates.</summary>
    public class ConfigUpdate
    {
        public int? SignOnIntervalSeconds { get; set; }
        public bool? EnablePeriodicSignOn { get; set; }
    }
}

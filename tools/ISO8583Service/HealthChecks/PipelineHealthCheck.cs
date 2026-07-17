using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Server;
using ISO8583Net.Server.Pipeline;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ISO8583Service.HealthChecks;

/// <summary>
/// Reports pipeline health: active connections, backpressure, error rates.
/// Returns Healthy/Degraded/Unhealthy based on thresholds.
/// </summary>
internal sealed class PipelineHealthCheck : IHealthCheck
{
    private readonly IIso8583Server _server;
    private readonly PipelineHost _pipelineHost;

    public PipelineHealthCheck(IIso8583Server server, PipelineHost pipelineHost)
    {
        _server = server;
        _pipelineHost = pipelineHost;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var status = HealthStatus.Healthy;
        var description = "All systems operational";

        int connCount = _pipelineHost.ConnectionCount;
        data["ConnectionCount"] = connCount;
        data["IsRunning"] = _server.IsRunning;
        data["HandlerCount"] = _pipelineHost.HandlerCount;

        // Collect pipeline stats
        var stats = _pipelineHost.GetStats();
        long totalRecv = 0, totalSent = 0, totalParseErrs = 0;
        int maxWriteQueue = 0, maxInFlight = 0;

        foreach (var s in stats)
        {
            totalRecv += s.MessagesReceived;
            totalSent += s.MessagesSent;
            totalParseErrs += s.ParseErrors;
            if (s.MaxWriteQueueLength > maxWriteQueue) maxWriteQueue = s.MaxWriteQueueLength;
            if (s.MaxInFlight > maxInFlight) maxInFlight = s.MaxInFlight;
        }

        data["TotalMessagesReceived"] = totalRecv;
        data["TotalMessagesSent"] = totalSent;
        data["TotalParseErrors"] = totalParseErrs;
        data["MaxWriteQueueLength"] = maxWriteQueue;
        data["MaxInFlight"] = maxInFlight;

        // ── Thresholds ───────────────────────────────────────────────
        if (!_server.IsRunning)
        {
            status = HealthStatus.Unhealthy;
            description = "ISO 8583 server is not running";
        }
        else if (maxWriteQueue > 200)
        {
            status = HealthStatus.Degraded;
            description = $"Write queue backpressure: {maxWriteQueue} messages queued";
        }
        else if (connCount == 0)
        {
            status = HealthStatus.Degraded;
            description = "Server is running but has no active connections";
        }

        return Task.FromResult(new HealthCheckResult(status, description, data: data));
    }
}

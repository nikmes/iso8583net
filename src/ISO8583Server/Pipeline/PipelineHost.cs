using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Packager;

namespace ISO8583Net.Server.Pipeline;

/// <summary>
/// Singleton host that manages all active <see cref="ConnectionPipeline"/> instances.
/// Creates a new pipeline for each accepted TCP connection and handles graceful shutdown.
/// </summary>
public sealed class PipelineHost
{
    private readonly PipelineOptions _options;
    private readonly ISOMessagePackager _packager;
    private readonly ConcurrentDictionary<int, ConnectionPipeline> _pipelines = new();

    public PipelineHost(PipelineOptions options, ISOMessagePackager packager)
    {
        _options = options;
        _packager = packager;
    }

    /// <summary>
    /// Create and start a new pipeline for an accepted connection.
    /// </summary>
    /// <param name="stream">TLS-wrapped (or raw) network stream.</param>
    /// <param name="connectionNumber">Monotonically incrementing connection ID.</param>
    /// <param name="remoteEndpoint">Remote IP:port string.</param>
    /// <param name="ct">Parent cancellation token for shutdown.</param>
    /// <returns>A handle that can be used to stop this connection.</returns>
    public ConnectionPipeline Accept(
        Stream stream,
        int connectionNumber,
        string remoteEndpoint,
        CancellationToken ct)
    {
        var pipeline = new ConnectionPipeline(
            stream, connectionNumber, remoteEndpoint, _packager, _options, ct);

        _pipelines.TryAdd(connectionNumber, pipeline);
        return pipeline;
    }

    /// <summary>
    /// Remove a pipeline when its connection closes.
    /// </summary>
    public void Remove(int connectionNumber)
    {
        _pipelines.TryRemove(connectionNumber, out _);
    }

    /// <summary>
    /// Snapshot of all active pipeline statistics (for monitoring/REST API).
    /// </summary>
    public IReadOnlyList<PipelineStats> GetStats()
    {
        return _pipelines.Values.Select(p => p.Stats).ToList();
    }

    /// <summary>
    /// Active connection count.
    /// </summary>
    public int ConnectionCount => _pipelines.Count;

    /// <summary>
    /// Gracefully stop all pipelines and wait for them to drain.
    /// </summary>
    public async Task StopAllAsync(CancellationToken ct = default)
    {
        var drainTimeout = TimeSpan.FromSeconds(_options.DrainTimeoutSeconds);
        var stopTasks = _pipelines.Values.Select(p => p.StopAsync(drainTimeout));
        await Task.WhenAll(stopTasks);

        foreach (var pipeline in _pipelines.Values)
            await pipeline.DisposeAsync();

        _pipelines.Clear();
    }
}

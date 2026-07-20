using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Message;
using ISO8583Net.Packager;
using ISO8583Net.Server.Pipeline.Handlers;
using ISO8583Net.Server.Pipeline.Messages;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Server.Pipeline;

/// <summary>
/// Singleton host that manages all active <see cref="ConnectionPipeline"/> instances.
/// Creates a new pipeline for each accepted TCP connection and handles graceful shutdown.
/// Registered in DI and shared between <see cref="Iso8583TcpServer"/> and the REST API controller.
/// </summary>
public sealed class PipelineHost
{
    private readonly PipelineOptions _options;
    private readonly HandlerRegistry _handlerRegistry;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IMessageTracer? _tracer;
    private readonly ConcurrentDictionary<int, ConnectionPipeline> _pipelines = new();
    private ISOMessagePackager? _packager;
    private int _sequenceCounter;
    private readonly ILogger _logger;

    private static readonly ILogger NullLoggerInstance = new NullSessionLogger();

    private sealed class NullSessionLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    /// <summary>
    /// Constructs the host. The packager is set later via <see cref="SetPackager"/>
    /// once the dialect is loaded in <see cref="Iso8583TcpServer.StartAsync"/>.
    /// </summary>
    public PipelineHost(PipelineOptions options, HandlerRegistry handlerRegistry,
        ILoggerFactory loggerFactory, IMessageTracer? tracer = null)
    {
        _options = options;
        _handlerRegistry = handlerRegistry;
        _loggerFactory = loggerFactory;
        _tracer = tracer;
        _logger = loggerFactory.CreateLogger<PipelineHost>();
    }

    /// <summary>
    /// Set the message packager after dialect has been loaded.
    /// Must be called before any connections are accepted.
    /// </summary>
    public void SetPackager(ISOMessagePackager packager)
    {
        _packager = packager;
    }

    /// <summary>
    /// Registered handler count (for diagnostics).
    /// </summary>
    public int HandlerCount => _handlerRegistry.HandlerCount;

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
        _logger.LogInformation("Accepting connection #{ConnNum} from {Endpoint}",
            connectionNumber, remoteEndpoint);

        var pipeline = new ConnectionPipeline(
            stream, connectionNumber, remoteEndpoint, _packager!, _handlerRegistry, _options, _loggerFactory,
            _tracer, ct);

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

    // ═══════════════════════════════════════════════════════════════════════
    //  Message sending (for SignOn/Echo/SignOff and periodic timers)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Enqueue an outbound message to a specific connection's writer channel.
    /// Safe to call from any thread (REST API, timer, handler).
    /// </summary>
    public async ValueTask SendToConnectionAsync(int connNum, OutboundMessage msg, CancellationToken ct = default)
    {
        if (_pipelines.TryGetValue(connNum, out var pipeline))
            await pipeline.SendAsync(msg, ct);
    }

    /// <summary>
    /// Broadcast a SignOn/Echo/SignOff request to all active connections.
    /// Builds one message per connection (with unique F11 per connNum).
    /// </summary>
    public async Task BroadcastSignOnRequestAsync(string f24Value, CancellationToken ct = default)
    {
        var pipelines = _pipelines.Values.ToArray();
        var tasks = new List<Task>(pipelines.Length);

        foreach (var pipeline in pipelines)
        {
            var msg = BuildRequest(pipeline.ConnectionNumber, f24Value);
            var framed = FrameMessage(msg);
            var outbound = OutboundMessage.FromPreFramed(framed, pipeline.ConnectionNumber);
            tasks.Add(pipeline.SendAsync(outbound, ct).AsTask());
        }

        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Build a standard SignOn/Echo/SignOff ISO 8583 message (MTI 1800).
    /// Public for SendSignOnOnConnect usage.
    /// </summary>
    public ISOMessage BuildSignOnMessage(int connNum, string f24Value)
    {
        return BuildRequest(connNum, f24Value);
    }

    /// <summary>
    /// Build a standard SignOn/Echo/SignOff ISO 8583 message (MTI 1800).
    /// </summary>
    private ISOMessage BuildRequest(int connNum, string f24Value)
    {
        var seq = Interlocked.Increment(ref _sequenceCounter);
        var msg = new ISOMessage(NullLoggerInstance, _packager!);
        msg.Set(0, "1800");
        msg.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));
        msg.Set(11, $"{seq:D6}");
        msg.Set(24, f24Value);
        return msg;
    }

    /// <summary>
    /// Pack + frame an ISOMessage with 2-byte big-endian length prefix.
    /// </summary>
    private static byte[] FrameMessage(ISOMessage msg)
    {
        byte[] packed = msg.Pack();
        byte[] framed = new byte[2 + packed.Length];
        framed[0] = (byte)(packed.Length >> 8);
        framed[1] = (byte)(packed.Length & 0xFF);
        Array.Copy(packed, 0, framed, 2, packed.Length);
        return framed;
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

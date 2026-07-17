using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ISO8583Net.Packager;
using ISO8583Net.Server.Pipeline.Handlers;
using ISO8583Net.Server.Pipeline.Messages;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Server.Pipeline;

/// <summary>
/// Manages the full per-connection SEDA pipeline:
/// reader → parser → dispatcher → handlers → writer.
/// </summary>
public sealed class ConnectionPipeline : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly Task _readerTask;
    private readonly Task _parserTask;
    private readonly Task _dispatcherTask;
    private readonly Task _writerTask;

    // Channels — owned by this pipeline
    private readonly Channel<RawMessage> _rawChannel;
    private readonly Channel<ParsedMessage> _parsedChannel;
    private readonly Channel<OutboundMessage> _outboundChannel;

    private readonly ILogger _logger;

    public PipelineStats Stats { get; }

    public ConnectionPipeline(
        Stream stream,
        int connectionNumber,
        string remoteEndpoint,
        ISOMessagePackager packager,
        HandlerRegistry handlerRegistry,
        PipelineOptions options,
        ILoggerFactory loggerFactory,
        CancellationToken parentCt)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(parentCt);
        _logger = loggerFactory.CreateLogger($"Pipeline.Conn{connectionNumber}");

        Stats = new PipelineStats
        {
            ConnectionNumber = connectionNumber,
            RemoteEndpoint = remoteEndpoint,
            ConnectedAt = DateTime.UtcNow
        };

        // ── Circuit breaker for parser errors ─────────────────────────
        var circuitBreaker = options.MaxParseErrorsBeforePause > 0
            ? new CircuitBreakerState(options.MaxParseErrorsBeforePause,
                TimeSpan.FromSeconds(options.ParserCooldownSeconds))
            : null;

        // ── Create bounded channels ───────────────────────────────────
        _rawChannel = Channel.CreateBounded<RawMessage>(
            new BoundedChannelOptions(options.RawMessageCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false  // N parser tasks may read
            });

        _parsedChannel = Channel.CreateBounded<ParsedMessage>(
            new BoundedChannelOptions(options.ParsedMessageCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,  // N parser tasks may write
                SingleReader = true
            });

        _outboundChannel = Channel.CreateBounded<OutboundMessage>(
            new BoundedChannelOptions(options.OutboundMessageCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,  // multiple handlers may write
                SingleReader = true    // single writer task
            });

        // ── Create stage loggers ──────────────────────────────────────
        var readerLogger = loggerFactory.CreateLogger($"Pipeline.Conn{connectionNumber}.Reader");
        var parserLogger = loggerFactory.CreateLogger($"Pipeline.Conn{connectionNumber}.Parser");
        var dispatcherLogger = loggerFactory.CreateLogger($"Pipeline.Conn{connectionNumber}.Dispatcher");
        var writerLogger = loggerFactory.CreateLogger($"Pipeline.Conn{connectionNumber}.Writer");

        // ── Start stages ──────────────────────────────────────────────
        _readerTask = ReaderStage.RunAsync(stream, _rawChannel.Writer, Stats, readerLogger,
            circuitBreaker, _cts.Token);
        _writerTask = WriterStage.RunAsync(stream, _outboundChannel.Reader, Stats, writerLogger, _cts.Token);

        _parserTask = ParserStage.RunAsync(
            _rawChannel.Reader, _parsedChannel.Writer, packager, Stats, options, parserLogger,
            circuitBreaker, _cts.Token);

        _dispatcherTask = DispatcherStage.RunAsync(
            _parsedChannel.Reader, _outboundChannel.Writer, handlerRegistry, Stats, dispatcherLogger,
            options, _cts.Token);

        _logger.LogInformation("Pipeline created: conn={ConnNum}, endpoint={Endpoint}, " +
            "parserConcurrency={Concurrency}, rawCap={RawCap}, outCap={OutCap}",
            connectionNumber, remoteEndpoint, options.ParserConcurrency,
            options.RawMessageCapacity, options.OutboundMessageCapacity);
    }

    /// <summary>
    /// Connection number.
    /// </summary>
    public int ConnectionNumber => Stats.ConnectionNumber;

    /// <summary>
    /// Current write queue depth (for monitoring).
    /// </summary>
    public int WriteQueueLength => _outboundChannel.Reader.Count;

    /// <summary>
    /// Enqueue an outbound message for writing. Thread-safe — may be called
    /// from REST API handlers, periodic timers, or message handlers.
    /// </summary>
    public async ValueTask SendAsync(OutboundMessage msg, CancellationToken ct = default)
    {
        Stats.UpdateWriteQueueLength(_outboundChannel.Reader.Count);
        await _outboundChannel.Writer.WriteAsync(msg, ct);
        Stats.UpdateWriteQueueLength(_outboundChannel.Reader.Count);
    }

    /// <summary>
    /// Graceful shutdown: cancel stages, drain remaining writes, wait for tasks.
    /// </summary>
    public async Task StopAsync(TimeSpan drainTimeout)
    {
        _logger.LogInformation("Pipeline conn={ConnNum} stopping, drainTimeout={Timeout}s",
            ConnectionNumber, drainTimeout.TotalSeconds);

        // Signal all stages to stop
        _cts.Cancel();

        // Wait for reader + parser + dispatcher to finish (with timeout)
        var drainTasks = new[] { _readerTask, _parserTask, _dispatcherTask };
        var drainCts = new CancellationTokenSource(drainTimeout);
        try
        {
            await Task.WhenAll(drainTasks).WaitAsync(drainCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Pipeline conn={ConnNum} drain timed out after {Timeout}s",
                ConnectionNumber, drainTimeout.TotalSeconds);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Pipeline conn={ConnNum} drain timed out after {Timeout}s",
                ConnectionNumber, drainTimeout.TotalSeconds);
        }

        // Complete the outbound channel so writer drains and exits
        _outboundChannel.Writer.TryComplete();
        await _writerTask;

        _logger.LogInformation("Pipeline conn={ConnNum} stopped: recv={Recv}msgs/{RecvBytes}B, " +
            "sent={Sent}msgs/{SentBytes}B, parseErrs={ParseErrs}, handlerErrs={HandlerErrs}",
            ConnectionNumber,
            Stats.MessagesReceived, Stats.BytesReceived,
            Stats.MessagesSent, Stats.BytesSent,
            Stats.ParseErrors, Stats.HandlerErrors);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(TimeSpan.FromSeconds(5));
        _cts.Dispose();
    }
}

/// <summary>
/// Connection-level circuit breaker for parser errors.
/// Shared between ReaderStage (checks IsOpen) and ParserStage (reports errors).
/// Thread-safe via Interlocked.
/// </summary>
internal sealed class CircuitBreakerState
{
    private int _consecutiveErrors;
    private readonly int _threshold;
    private readonly TimeSpan _cooldown;
    private long _openedAtTicks;

    public CircuitBreakerState(int threshold, TimeSpan cooldown)
    {
        _threshold = threshold;
        _cooldown = cooldown;
    }

    /// <summary>Whether the circuit is open (blocking reads).</summary>
    public bool IsOpen
    {
        get
        {
            var openedAt = Interlocked.Read(ref _openedAtTicks);
            if (openedAt == 0) return false;
            var elapsed = DateTime.UtcNow - new DateTime(openedAt, DateTimeKind.Utc);
            if (elapsed >= _cooldown)
            {
                Reset();
                return false;
            }
            return true;
        }
    }

    /// <summary>Called by parser on parse failure.</summary>
    public void RecordError()
    {
        if (Interlocked.Increment(ref _consecutiveErrors) >= _threshold)
        {
            Interlocked.Exchange(ref _openedAtTicks, DateTime.UtcNow.Ticks);
        }
    }

    /// <summary>Called by parser on parse success.</summary>
    public void RecordSuccess()
    {
        Interlocked.Exchange(ref _consecutiveErrors, 0);
    }

    /// <summary>Reset the circuit breaker to closed state.</summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _consecutiveErrors, 0);
        Interlocked.Exchange(ref _openedAtTicks, 0);
    }
}

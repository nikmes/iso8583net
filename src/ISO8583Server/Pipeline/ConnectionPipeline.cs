using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ISO8583Net.Packager;
using ISO8583Net.Server.Pipeline.Handlers;
using ISO8583Net.Server.Pipeline.Messages;

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

    public PipelineStats Stats { get; }

    public ConnectionPipeline(
        Stream stream,
        int connectionNumber,
        string remoteEndpoint,
        ISOMessagePackager packager,
        HandlerRegistry handlerRegistry,
        PipelineOptions options,
        CancellationToken parentCt)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(parentCt);

        Stats = new PipelineStats
        {
            ConnectionNumber = connectionNumber,
            RemoteEndpoint = remoteEndpoint,
            ConnectedAt = DateTime.UtcNow
        };

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

        // ── Start stages ──────────────────────────────────────────────
        _readerTask = ReaderStage.RunAsync(stream, _rawChannel.Writer, Stats, _cts.Token);
        _writerTask = WriterStage.RunAsync(stream, _outboundChannel.Reader, Stats, _cts.Token);

        _parserTask = ParserStage.RunAsync(
            _rawChannel.Reader, _parsedChannel.Writer, packager, Stats, options, _cts.Token);

        _dispatcherTask = DispatcherStage.RunAsync(
            _parsedChannel.Reader, _outboundChannel.Writer, handlerRegistry, Stats, _cts.Token);
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
        await _outboundChannel.Writer.WriteAsync(msg, ct);
    }

    /// <summary>
    /// Graceful shutdown: cancel stages, drain remaining writes, wait for tasks.
    /// </summary>
    public async Task StopAsync(TimeSpan drainTimeout)
    {
        // Signal all stages to stop
        _cts.Cancel();

        // Wait for reader + parser + dispatcher to finish (with timeout)
        var drainTasks = new[] { _readerTask, _parserTask, _dispatcherTask };
        var drainCts = new CancellationTokenSource(drainTimeout);
        try
        {
            await Task.WhenAll(drainTasks).WaitAsync(drainCts.Token);
        }
        catch (OperationCanceledException) { /* timeout */ }
        catch (TimeoutException) { /* timeout */ }

        // Complete the outbound channel so writer drains and exits
        _outboundChannel.Writer.TryComplete();
        await _writerTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(TimeSpan.FromSeconds(5));
        _cts.Dispose();
    }
}

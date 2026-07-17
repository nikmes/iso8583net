using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ISO8583Net.Packager;
using ISO8583Net.Server.Pipeline.Messages;

namespace ISO8583Net.Server.Pipeline;

/// <summary>
/// Manages the full per-connection SEDA pipeline:
/// reader → parser → (dispatcher → handlers) → writer.
///
/// Sprint 2: reader → parser → (echo bridge: re-pack parsed messages) → writer.
/// Sprint 3: replaces the echo bridge with dispatcher + handlers.
/// </summary>
public sealed class ConnectionPipeline : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly Task _readerTask;
    private readonly Task _parserTask;
    private readonly Task _bridgeTask;
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

        // Sprint 2: reader → parser → (echo bridge: re-pack) → writer
        _parserTask = ParserStage.RunAsync(
            _rawChannel.Reader, _parsedChannel.Writer, packager, Stats, options, _cts.Token);

        _bridgeTask = RunEchoBridgeAsync(_cts.Token);
    }

    /// <summary>
    /// Sprint 2 echo bridge: reads parsed messages and echoes them back
    /// by re-packing and framing. Replaced by dispatcher + handlers in Sprint 3.
    /// </summary>
    private async Task RunEchoBridgeAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var parsed in _parsedChannel.Reader.ReadAllAsync(ct))
            {
                // Re-pack the ISO message and frame it
                byte[] packed = parsed.Message.Pack();
                int frameLength = 2 + packed.Length;
                byte[] framed = new byte[frameLength];
                framed[0] = (byte)(packed.Length >> 8);
                framed[1] = (byte)(packed.Length & 0xFF);
                Array.Copy(packed, 0, framed, 2, packed.Length);

                var outbound = OutboundMessage.FromPreFramed(framed, Stats.ConnectionNumber);
                await _outboundChannel.Writer.WriteAsync(outbound, ct);
            }
        }
        catch (OperationCanceledException) { /* graceful */ }
    }

    /// <summary>
    /// Current write queue depth (for monitoring).
    /// </summary>
    public int WriteQueueLength => _outboundChannel.Reader.Count;

    /// <summary>
    /// Graceful shutdown: cancel stages, drain remaining writes, wait for tasks.
    /// </summary>
    public async Task StopAsync(TimeSpan drainTimeout)
    {
        // Signal all stages to stop
        _cts.Cancel();

        // Wait for reader + parser + bridge to finish (with timeout)
        var drainTasks = new[] { _readerTask, _parserTask, _bridgeTask };
        var drainCts = new CancellationTokenSource(drainTimeout);
        try
        {
            await Task.WhenAll(drainTasks).WaitAsync(drainCts.Token);
        }
        catch (OperationCanceledException) { /* timeout */ }
        catch (TimeoutException) { /* timeout */ }

        // Complete the outbound channel so writer drains and exits
        _outboundChannel.Writer.Complete();
        await _writerTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(TimeSpan.FromSeconds(5));
        _cts.Dispose();
    }
}

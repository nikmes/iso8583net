using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ISO8583Net.Server.Pipeline.Messages;

namespace ISO8583Net.Server.Pipeline;

/// <summary>
/// Manages the full per-connection SEDA pipeline: reader → (parser → dispatcher → handlers) → writer.
/// In Sprint 1, only reader and writer are wired; a pass-through bridge echoes raw bytes.
/// </summary>
public sealed class ConnectionPipeline : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly Task _readerTask;
    private readonly Task _passthroughTask;
    private readonly Task _writerTask;

    // Channels — owned by this pipeline
    private readonly Channel<RawMessage> _rawChannel;
    private readonly Channel<OutboundMessage> _outboundChannel;

    public PipelineStats Stats { get; }

    public ConnectionPipeline(
        Stream stream,
        int connectionNumber,
        string remoteEndpoint,
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

        // Sprint 1 pass-through: echo raw bytes back
        // Reader → RawMessage → (pass-through: re-frame) → OutboundMessage → Writer
        _passthroughTask = RunPassThroughAsync(_cts.Token);
    }

    /// <summary>
    /// Sprint 1 pass-through: reads raw frames, immediately echoes them back.
    /// Replaced by parser + dispatcher + handlers in Sprint 2–3.
    /// </summary>
    private async Task RunPassThroughAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var raw in _rawChannel.Reader.ReadAllAsync(ct))
            {
                // Re-frame: prepend 2-byte LI + raw data
                int frameLength = 2 + raw.Length;
                byte[] framed = new byte[frameLength];
                framed[0] = (byte)(raw.Length >> 8);
                framed[1] = (byte)(raw.Length & 0xFF);
                raw.Data.Span.CopyTo(framed.AsSpan(2));

                var outbound = OutboundMessage.FromPreFramed(framed, Stats.ConnectionNumber);

                await _outboundChannel.Writer.WriteAsync(outbound, ct);

                // Return the rented buffer now that we've copied the data
                raw.Return();
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
        // Signal reader to stop
        _cts.Cancel();

        // Wait for reader + pass-through to finish (with timeout)
        var drainTasks = new[] { _readerTask, _passthroughTask };
        var drainCts = new CancellationTokenSource(drainTimeout);
        try
        {
            await Task.WhenAll(drainTasks).WaitAsync(drainCts.Token);
        }
        catch (OperationCanceledException) { /* timeout — force complete */ }
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

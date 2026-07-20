using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ISO8583Net.Server.Pipeline.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ISO8583Service.Tracing;

/// <summary>
/// EF Core-backed message tracer that asynchronously persists ISO 8583 traces
/// to PostgreSQL via a bounded in-memory channel and batch consumer.
///
/// Pipeline throughput is not affected: each trace method does a lock-free
/// <see cref="ChannelWriter{T}.TryWrite"/> and returns immediately. A background
/// consumer flushes batches every 100ms or 500 messages.
/// </summary>
public sealed class EfMessageTracer : IMessageTracer, IDisposable
{
    private readonly Channel<TracedMessage> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EfMessageTracer> _logger;
    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;
    private readonly CancellationTokenSource _cts;
    private readonly Task _consumerTask;
    private int _droppedCount;

    public EfMessageTracer(
        IServiceScopeFactory scopeFactory,
        ILogger<EfMessageTracer> logger,
        int capacity = 10_000,
        int batchSize = 500,
        int flushIntervalMs = 100)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _batchSize = batchSize;
        _flushInterval = TimeSpan.FromMilliseconds(flushIntervalMs);

        _channel = Channel.CreateBounded<TracedMessage>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _cts = new CancellationTokenSource();
        _consumerTask = Task.Run(() => ConsumerLoopAsync(_cts.Token));

        _logger.LogInformation(
            "EfMessageTracer started: capacity={Capacity}, batchSize={BatchSize}, flushInterval={FlushInterval}ms",
            capacity, batchSize, flushIntervalMs);
    }

    // ── IMessageTracer (fire-and-forget into channel) ────────────────

    public void OnMessageReceived(string mti, string rawHex, int fieldCount,
        int connNum, string remoteEndpoint)
    {
        TryWrite(new TracedMessage
        {
            Timestamp = DateTime.UtcNow,
            TraceType = "RECV",
            Mti = mti,
            ConnectionNumber = connNum,
            FieldsCount = fieldCount,
            RawHex = Truncate(rawHex, 500)
        });
    }

    public void OnMessageResponded(string requestMti, string responseMti,
        string f39, int connNum, long elapsedMs)
    {
        TryWrite(new TracedMessage
        {
            Timestamp = DateTime.UtcNow,
            TraceType = "SEND",
            Mti = responseMti,
            ConnectionNumber = connNum,
            ResponseHex = Truncate($"F39={f39}", 500),
            ElapsedMs = elapsedMs
        });
    }

    public void OnParseError(string rawHex, int connNum, string error)
    {
        TryWrite(new TracedMessage
        {
            Timestamp = DateTime.UtcNow,
            TraceType = "PARSE_ERR",
            ConnectionNumber = connNum,
            RawHex = Truncate(rawHex, 500),
            ErrorMessage = Truncate(error, 1024)
        });
    }

    public void OnNoResponse(string mti, int connNum)
    {
        TryWrite(new TracedMessage
        {
            Timestamp = DateTime.UtcNow,
            TraceType = "NO_RESP",
            Mti = mti,
            ConnectionNumber = connNum
        });
    }

    public void OnHandlerError(string mti, int connNum, string error)
    {
        TryWrite(new TracedMessage
        {
            Timestamp = DateTime.UtcNow,
            TraceType = "HANDLER_ERR",
            Mti = mti,
            ConnectionNumber = connNum,
            ErrorMessage = Truncate(error, 1024)
        });
    }

    // ── Background consumer ──────────────────────────────────────────

    private async Task ConsumerLoopAsync(CancellationToken ct)
    {
        var batch = new List<TracedMessage>(_batchSize);

        while (!ct.IsCancellationRequested)
        {
            batch.Clear();
            var batchDeadline = DateTime.UtcNow + _flushInterval;

            try
            {
                // Fill the batch
                while (batch.Count < _batchSize && DateTime.UtcNow < batchDeadline)
                {
                    if (_channel.Reader.TryRead(out var msg))
                    {
                        batch.Add(msg);
                    }
                    else
                    {
                        // Brief wait for more messages
                        using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        waitCts.CancelAfter(10);
                        try
                        {
                            await _channel.Reader.WaitToReadAsync(waitCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break; // Time to flush
                        }
                    }
                }

                if (batch.Count > 0)
                {
                    await FlushBatchAsync(batch, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EfMessageTracer consumer error, dropping {Count} traces", batch.Count);
            }
        }

        // Drain remaining on shutdown
        var remaining = new List<TracedMessage>();
        while (_channel.Reader.TryRead(out var msg))
            remaining.Add(msg);

        if (remaining.Count > 0)
        {
            try
            {
                await FlushBatchAsync(remaining, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EfMessageTracer failed to drain {Count} traces on shutdown", remaining.Count);
            }
        }

        _logger.LogInformation("EfMessageTracer consumer stopped. Total dropped: {Dropped}", _droppedCount);
    }

    private async Task FlushBatchAsync(List<TracedMessage> batch, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MessageTraceDbContext>();

        db.TracedMessages.AddRange(batch);
        await db.SaveChangesAsync(ct);

        _logger.LogDebug("Flushed {Count} traces to database", batch.Count);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private void TryWrite(TracedMessage message)
    {
        if (!_channel.Writer.TryWrite(message))
        {
            var dropped = Interlocked.Increment(ref _droppedCount);
            if (dropped % 1000 == 0)
            {
                _logger.LogWarning("EfMessageTracer channel full, dropped {Dropped} traces so far", dropped);
            }
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    // ── Cleanup ──────────────────────────────────────────────────────

    public void Dispose()
    {
        _cts.Cancel();

        try
        {
            _consumerTask.Wait(TimeSpan.FromSeconds(30));
        }
        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
        {
            // Expected on cancellation
        }

        _cts.Dispose();
    }
}

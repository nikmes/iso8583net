using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ISO8583Net.Message;
using ISO8583Net.Packager;
using ISO8583Net.Server.Pipeline.Messages;
using ISO8583Net.Utilities;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Server.Pipeline;

/// <summary>
/// Reads <see cref="RawMessage"/> instances from an input channel,
/// unpacks them into <see cref="ISOMessage"/> objects, and pushes
/// <see cref="ParsedMessage"/> results to the output channel.
///
/// Supports multiple concurrent consumer tasks via <see cref="ParserConcurrency"/>.
/// </summary>
internal static class ParserStage
{
    /// <summary>
    /// Run one or more parser tasks consuming from the same input channel.
    /// </summary>
    /// <param name="input">Channel of raw frames from the reader stage.</param>
    /// <param name="output">Channel for parsed messages to the dispatcher.</param>
    /// <param name="packager">The loaded dialect packager.</param>
    /// <param name="stats">Per-connection statistics.</param>
    /// <param name="options">Pipeline options (concurrency, error thresholds).</param>
    /// <param name="logger">Structured logger for this stage.</param>
    /// <param name="circuitBreaker">Optional shared circuit breaker.</param>
    /// <param name="tracer">Optional message tracer for diagnostics.</param>
    /// <param name="ct">Cancellation token for shutdown.</param>
    public static async Task RunAsync(
        ChannelReader<RawMessage> input,
        ChannelWriter<ParsedMessage> output,
        ISOMessagePackager packager,
        PipelineStats stats,
        PipelineOptions options,
        ILogger logger,
        CircuitBreakerState? circuitBreaker,
        IMessageTracer? tracer,
        CancellationToken ct)
    {
        int concurrency = Math.Max(1, options.ParserConcurrency);
        var tasks = new Task[concurrency];

        logger.LogDebug("Parser stage starting with {Concurrency} task(s)", concurrency);

        for (int i = 0; i < concurrency; i++)
        {
            tasks[i] = RunSingleParserAsync(input, output, packager, stats, logger,
                circuitBreaker, tracer, ct);
        }

        await Task.WhenAll(tasks);
        output.Complete();
        logger.LogDebug("Parser stage completed");
    }

    private static async Task RunSingleParserAsync(
        ChannelReader<RawMessage> input,
        ChannelWriter<ParsedMessage> output,
        ISOMessagePackager packager,
        PipelineStats stats,
        ILogger logger,
        CircuitBreakerState? circuitBreaker,
        IMessageTracer? tracer,
        CancellationToken ct)
    {
        try
        {
            await foreach (var raw in input.ReadAllAsync(ct))
            {
                try
                {
                    var parsed = Parse(raw, packager, stats);
                    await output.WriteAsync(parsed, ct);
                    circuitBreaker?.RecordSuccess();

                    // Log parsed message content like old build
                    string mti = ExtractMti(parsed.Message);
                    if (mti != "???")
                    {
                        logger.LogInformation("[#{ConnNum}] ── Parsed Message ──\n{Message}",
                            raw.ConnectionNumber, parsed.Message.ToString());
                    }

                    // Trace successful parse
                    if (tracer != null)
                    {
                        string mti2 = ExtractMti(parsed.Message);
                        int fieldCount = CountFields(parsed.Message);
                        tracer.OnMessageReceived(mti2, parsed.HexDump, fieldCount,
                            raw.ConnectionNumber, stats.RemoteEndpoint);
                    }
                }
                catch (Exception ex)
                {
                    stats.IncrementParseErrors();
                    circuitBreaker?.RecordError();

                    // Trace parse error
                    tracer?.OnParseError(
                        ISOUtils.Bytes2Hex(raw.Data.ToArray()),
                        raw.ConnectionNumber, ex.Message);

                    if (circuitBreaker is { IsOpen: true })
                    {
                        logger.LogWarning("Circuit breaker tripped — {Errors} consecutive parse errors",
                            stats.ParseErrors);
                    }
                    else
                    {
                        logger.LogDebug(ex, "Parse error on conn {ConnNum}", stats.ConnectionNumber);
                    }
                }
                finally
                {
                    raw.Return();
                }
            }
        }
        catch (OperationCanceledException) { /* graceful */ }
    }

    private static ParsedMessage Parse(
        RawMessage raw, ISOMessagePackager packager, PipelineStats stats)
    {
        // Use a silent logger — parsing shouldn't produce log noise in the hot path
        var logger = NullParseLogger.Instance;
        var msg = new ISOMessage(logger, packager);

        // Copy data to a new array for UnPack (it expects byte[])
        byte[] data = raw.Data.ToArray();
        msg.UnPack(data);

        string hexDump = ISOUtils.Bytes2Hex(data);

        return new ParsedMessage(
            message: msg,
            connectionNumber: raw.ConnectionNumber,
            hexDump: hexDump,
            remoteEndpoint: stats.RemoteEndpoint,
            parsedAt: DateTime.UtcNow);
    }

    /// <summary>Silent logger to avoid parse noise in the hot path.</summary>
    private sealed class NullParseLogger : ILogger
    {
        public static readonly NullParseLogger Instance = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    /// <summary>Extract MTI from a parsed message (safe — returns "???" on failure).</summary>
    private static string ExtractMti(ISOMessage msg)
    {
        try { return msg.GetFieldValue(0) ?? "???"; }
        catch { return "???"; }
    }

    /// <summary>Count populated fields in a parsed message.</summary>
    private static int CountFields(ISOMessage msg)
    {
        int count = 0;
        try
        {
            // Count fields 1–128 in the bitmap
            for (int i = 1; i <= 128; i++)
            {
                try { if (msg.GetFieldValue(i) != null) count++; }
                catch { /* field not in bitmap */ }
            }
        }
        catch { /* best effort */ }
        return count;
    }
}

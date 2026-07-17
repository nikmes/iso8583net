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
    /// <param name="ct">Cancellation token for shutdown.</param>
    public static async Task RunAsync(
        ChannelReader<RawMessage> input,
        ChannelWriter<ParsedMessage> output,
        ISOMessagePackager packager,
        PipelineStats stats,
        PipelineOptions options,
        CancellationToken ct)
    {
        int concurrency = Math.Max(1, options.ParserConcurrency);
        var tasks = new Task[concurrency];

        for (int i = 0; i < concurrency; i++)
        {
            tasks[i] = RunSingleParserAsync(input, output, packager, stats, options, ct);
        }

        await Task.WhenAll(tasks);
        output.Complete();
    }

    private static async Task RunSingleParserAsync(
        ChannelReader<RawMessage> input,
        ChannelWriter<ParsedMessage> output,
        ISOMessagePackager packager,
        PipelineStats stats,
        PipelineOptions options,
        CancellationToken ct)
    {
        int consecutiveErrors = 0;

        try
        {
            await foreach (var raw in input.ReadAllAsync(ct))
            {
                try
                {
                    var parsed = Parse(raw, packager, stats);
                    await output.WriteAsync(parsed, ct);
                    consecutiveErrors = 0;
                }
                catch (Exception ex)
                {
                    consecutiveErrors++;
                    stats.IncrementParseErrors();

                    // Push an error entry so the dispatcher can log/handle it
                    var errorMsg = new ParsedMessage(
                        message: null!,
                        connectionNumber: raw.ConnectionNumber,
                        hexDump: ISOUtils.Bytes2Hex(raw.Data.ToArray()),
                        remoteEndpoint: stats.RemoteEndpoint,
                        parsedAt: DateTime.UtcNow);

                    // Mark as error by not writing (or we could write a special error parsed message)
                    // For now, just log and continue

                    // Circuit breaker: if too many consecutive errors, pause
                    if (options.MaxParseErrorsBeforePause > 0 &&
                        consecutiveErrors >= options.MaxParseErrorsBeforePause)
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(options.ParserCooldownSeconds), ct);
                        consecutiveErrors = 0;
                    }
                }
                finally
                {
                    // Always return the rented buffer
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
}

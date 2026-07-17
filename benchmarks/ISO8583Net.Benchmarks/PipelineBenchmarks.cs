using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using ISO8583Net.Message;
using ISO8583Net.Packager;
using ISO8583Net.Server.Pipeline;
using ISO8583Net.Server.Pipeline.Handlers;
using ISO8583Net.Server.Pipeline.Messages;
using Microsoft.Extensions.Logging;

namespace ISO8583NetBenchmark
{
    // ═══════════════════════════════════════════════════════════════════
    //  Shared helpers
    // ═══════════════════════════════════════════════════════════════════

    internal sealed class NullBenchLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    /// <summary>
    /// Echoes the request back unchanged — minimal handler overhead for benchmarks.
    /// </summary>
    internal sealed class BenchmarkEchoHandler : IMessageHandler
    {
        public IReadOnlySet<string> SupportedMTIs { get; } = new HashSet<string> { "*" };
        public Task<ISOMessage?> HandleAsync(MessageContext context, CancellationToken ct)
            => Task.FromResult<ISOMessage?>(context.Request);
    }

    /// <summary>
    /// Wraps an inner handler and records per-message processing latency
    /// (microseconds from dispatch start to response ready).
    /// Used by percentile benchmarks.
    /// </summary>
    internal sealed class LatencyRecordingHandler : IMessageHandler
    {
        private readonly IMessageHandler _inner;
        private readonly ConcurrentBag<long> _latenciesUs;

        public LatencyRecordingHandler(IMessageHandler inner, ConcurrentBag<long> latenciesUs)
        {
            _inner = inner;
            _latenciesUs = latenciesUs;
        }

        public IReadOnlySet<string> SupportedMTIs => _inner.SupportedMTIs;

        public async Task<ISOMessage?> HandleAsync(MessageContext context, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var result = await _inner.HandleAsync(context, ct);
            _latenciesUs.Add(sw.ElapsedTicks * 1_000_000L / Stopwatch.Frequency);
            return result;
        }
    }

    /// <summary>
    /// Splits a single bidirectional stream into separate read/write streams.
    /// Reads come from <paramref name="readStream"/>, writes go to <paramref name="writeStream"/>.
    /// This prevents the pipeline's echo responses from being re-read as new incoming messages.
    /// </summary>
    internal sealed class SplitStream : Stream
    {
        private readonly Stream _readStream;
        private readonly Stream _writeStream;
        public SplitStream(Stream readStream, Stream writeStream)
        {
            _readStream = readStream;
            _writeStream = writeStream;
        }
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => _readStream.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => _readStream.ReadAsync(buffer, offset, count, ct);
        public override void Write(byte[] buffer, int offset, int count) => _writeStream.Write(buffer, offset, count);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => _writeStream.WriteAsync(buffer, offset, count, ct);
        public override void Flush() => _writeStream.Flush();
        public override Task FlushAsync(CancellationToken ct) => _writeStream.FlushAsync(ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _readStream.Dispose();
                _writeStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Latency: single message round-trip
    // ═══════════════════════════════════════════════════════════════════

    [MemoryDiagnoser]
    [MarkdownExporter]
    [WarmupCount(3)]
    [IterationCount(10)]
    public class PipelineLatencyBenchmarks
    {
        [Params(1, 2, 4)]
        public int ParserConcurrency { get; set; }

        private ISOMessagePackager _packager = null!;
        private HandlerRegistry _registry = null!;
        private byte[] _framedMessage = null!;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var logger = new NullBenchLogger();
            _packager = new ISOMessagePackager(logger);
            _registry = new HandlerRegistry(new IMessageHandler[] { new BenchmarkEchoHandler() });

            var msg = new ISOMessage(logger, _packager);
            msg.Set(0, "1800");
            msg.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));
            msg.Set(11, "000001");
            msg.Set(24, "801");
            byte[] packed = msg.Pack();
            _framedMessage = new byte[2 + packed.Length];
            _framedMessage[0] = (byte)(packed.Length >> 8);
            _framedMessage[1] = (byte)(packed.Length & 0xFF);
            Array.Copy(packed, 0, _framedMessage, 2, packed.Length);
        }

        [Benchmark]
        public async Task SingleMessageRoundTrip()
        {
            var options = new PipelineOptions
            {
                RawMessageCapacity = 64,
                ParsedMessageCapacity = 64,
                OutboundMessageCapacity = 64,
                ParserConcurrency = ParserConcurrency,
                DrainTimeoutSeconds = 5
            };

            var host = new PipelineHost(options, _registry);
            host.SetPackager(_packager);

            using var clientStream = new MemoryStream();
            clientStream.Write(_framedMessage, 0, _framedMessage.Length);
            clientStream.Position = 0;
            using var serverToClient = new MemoryStream();
            using var serverStream = new SplitStream(clientStream, serverToClient);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

            while (pipeline.Stats.MessagesSent < 1)
                await Task.Yield();

            cts.Cancel();
            await pipeline.StopAsync(TimeSpan.FromSeconds(3));
            await pipeline.DisposeAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Throughput: batch processing
    // ═══════════════════════════════════════════════════════════════════

    [MemoryDiagnoser]
    [MarkdownExporter]
    [WarmupCount(2)]
    [IterationCount(5)]
    public class PipelineThroughputBenchmarks
    {
        [Params(100, 500, 1000)]
        public int MessageCount { get; set; }

        [Params(1, 2, 4)]
        public int ParserConcurrency { get; set; }

        private ISOMessagePackager _packager = null!;
        private HandlerRegistry _registry = null!;
        private byte[][] _framedMessages = null!;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var logger = new NullBenchLogger();
            _packager = new ISOMessagePackager(logger);
            _registry = new HandlerRegistry(new IMessageHandler[] { new BenchmarkEchoHandler() });

            // Pre-build max message count worth of frames
            int max = 1000;
            _framedMessages = new byte[max][];
            for (int i = 0; i < max; i++)
            {
                var msg = new ISOMessage(logger, _packager);
                msg.Set(0, "1800");
                msg.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));
                msg.Set(11, $"{i + 1:D6}");
                msg.Set(24, "801");
                byte[] packed = msg.Pack();
                byte[] frame = new byte[2 + packed.Length];
                frame[0] = (byte)(packed.Length >> 8);
                frame[1] = (byte)(packed.Length & 0xFF);
                Array.Copy(packed, 0, frame, 2, packed.Length);
                _framedMessages[i] = frame;
            }
        }

        [Benchmark]
        public async Task ProcessBatch()
        {
            var options = new PipelineOptions
            {
                RawMessageCapacity = 4096,
                ParsedMessageCapacity = 4096,
                OutboundMessageCapacity = 4096,
                ParserConcurrency = ParserConcurrency,
                DrainTimeoutSeconds = 10
            };

            var host = new PipelineHost(options, _registry);
            host.SetPackager(_packager);

            using var clientStream = new MemoryStream();
            for (int i = 0; i < MessageCount; i++)
                clientStream.Write(_framedMessages[i], 0, _framedMessages[i].Length);
            clientStream.Position = 0;
            using var serverToClient = new MemoryStream();
            using var serverStream = new SplitStream(clientStream, serverToClient);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

            while (pipeline.Stats.MessagesSent < MessageCount)
                await Task.Yield();

            // Validate
            if (pipeline.Stats.ParseErrors > 0 || pipeline.Stats.HandlerErrors > 0)
                throw new InvalidOperationException(
                    $"Errors detected: parse={pipeline.Stats.ParseErrors}, handler={pipeline.Stats.HandlerErrors}");

            cts.Cancel();
            await pipeline.StopAsync(TimeSpan.FromSeconds(5));
            await pipeline.DisposeAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Parser scaling: fixed 1000 msg, compare concurrency levels
    // ═══════════════════════════════════════════════════════════════════

    [MemoryDiagnoser]
    [MarkdownExporter]
    [WarmupCount(2)]
    [IterationCount(5)]
    public class ParserScalingBenchmarks
    {
        [Params(1, 2, 4, 8)]
        public int ParserConcurrency { get; set; }

        private const int MessageCount = 500;

        private ISOMessagePackager _packager = null!;
        private HandlerRegistry _registry = null!;
        private byte[][] _framedMessages = null!;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var logger = new NullBenchLogger();
            _packager = new ISOMessagePackager(logger);
            _registry = new HandlerRegistry(new IMessageHandler[] { new BenchmarkEchoHandler() });

            _framedMessages = new byte[MessageCount][];
            for (int i = 0; i < MessageCount; i++)
            {
                var msg = new ISOMessage(logger, _packager);
                msg.Set(0, "1800");
                msg.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));
                msg.Set(11, $"{i + 1:D6}");
                msg.Set(24, "801");
                byte[] packed = msg.Pack();
                byte[] frame = new byte[2 + packed.Length];
                frame[0] = (byte)(packed.Length >> 8);
                frame[1] = (byte)(packed.Length & 0xFF);
                Array.Copy(packed, 0, frame, 2, packed.Length);
                _framedMessages[i] = frame;
            }
        }

        [Benchmark]
        public async Task Run()
        {
            var options = new PipelineOptions
            {
                RawMessageCapacity = 4096,
                ParsedMessageCapacity = 4096,
                OutboundMessageCapacity = 4096,
                ParserConcurrency = ParserConcurrency,
                DrainTimeoutSeconds = 10
            };

            var host = new PipelineHost(options, _registry);
            host.SetPackager(_packager);

            using var clientStream = new MemoryStream();
            for (int i = 0; i < MessageCount; i++)
                clientStream.Write(_framedMessages[i], 0, _framedMessages[i].Length);
            clientStream.Position = 0;
            using var serverToClient = new MemoryStream();
            using var serverStream = new SplitStream(clientStream, serverToClient);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

            while (pipeline.Stats.MessagesSent < MessageCount)
                await Task.Yield();

            if (pipeline.Stats.ParseErrors > 0 || pipeline.Stats.HandlerErrors > 0)
                throw new InvalidOperationException(
                    $"Pipeline errors: parse={pipeline.Stats.ParseErrors}, handler={pipeline.Stats.HandlerErrors}");

            cts.Cancel();
            await pipeline.StopAsync(TimeSpan.FromSeconds(5));
            await pipeline.DisposeAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Percentile latency: measure P50/P95/P99 at batch sizes
    // ═══════════════════════════════════════════════════════════════════

    [MemoryDiagnoser]
    [MarkdownExporter]
    [WarmupCount(1)]
    [IterationCount(3)]
    public class PercentileLatencyBenchmarks
    {
        [Params(100, 1000, 5000)]
        public int MessageCount { get; set; }

        private ISOMessagePackager _packager = null!;
        private byte[][] _framedMessages = null!;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var logger = new NullBenchLogger();
            _packager = new ISOMessagePackager(logger);

            _framedMessages = new byte[5000][];
            for (int i = 0; i < 5000; i++)
            {
                var msg = new ISOMessage(logger, _packager);
                msg.Set(0, "1800");
                msg.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));
                msg.Set(11, $"{i + 1:D6}");
                msg.Set(24, "801");
                byte[] packed = msg.Pack();
                byte[] frame = new byte[2 + packed.Length];
                frame[0] = (byte)(packed.Length >> 8);
                frame[1] = (byte)(packed.Length & 0xFF);
                Array.Copy(packed, 0, frame, 2, packed.Length);
                _framedMessages[i] = frame;
            }
        }

        [Benchmark]
        public async Task<(double P50, double P95, double P99)> MeasurePercentiles()
        {
            var latencies = new ConcurrentBag<long>();
            var registry = new HandlerRegistry(new IMessageHandler[]
                { new LatencyRecordingHandler(new BenchmarkEchoHandler(), latencies) });

            var options = new PipelineOptions
            {
                RawMessageCapacity = 8192,
                ParsedMessageCapacity = 8192,
                OutboundMessageCapacity = 8192,
                ParserConcurrency = 2,
                DrainTimeoutSeconds = 10
            };

            var host = new PipelineHost(options, registry);
            host.SetPackager(_packager);

            // Feed messages into the pipeline as a batch
            using var clientStream = new MemoryStream();
            for (int i = 0; i < MessageCount; i++)
                clientStream.Write(_framedMessages[i], 0, _framedMessages[i].Length);
            clientStream.Position = 0;
            using var serverToClient = new MemoryStream();
            using var serverStream = new SplitStream(clientStream, serverToClient);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

            while (pipeline.Stats.MessagesSent < MessageCount)
                await Task.Yield();

            cts.Cancel();
            await pipeline.StopAsync(TimeSpan.FromSeconds(5));
            await pipeline.DisposeAsync();

            // Compute percentiles
            var sorted = latencies.OrderBy(l => l).ToArray();
            if (sorted.Length == 0) return (0, 0, 0);

            double P50 = sorted[(int)(sorted.Length * 0.50)];
            double P95 = sorted[(int)(sorted.Length * 0.95)];
            double P99 = sorted[(int)(sorted.Length * 0.99)];
            return (P50, P95, P99);
        }
    }
}

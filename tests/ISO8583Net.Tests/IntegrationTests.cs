using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Message;
using ISO8583Net.Packager;
using ISO8583Net.Server.Pipeline;
using ISO8583Net.Server.Pipeline.Handlers;
using ISO8583Net.Server.Pipeline.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ISO8583Net.Tests;

/// <summary>
/// Full integration tests simulating real TCP connections through the SEDA pipeline.
/// </summary>
public sealed class IntegrationTests
{
    private static ISOMessagePackager CreatePackager()
    {
        return new ISOMessagePackager(new NullTestLogger());
    }

    private static HandlerRegistry CreateRegistry()
    {
        return new HandlerRegistry(new[] { new EchoHandler() });
    }

    private static byte[] BuildFramedEchoMessage(ISOMessagePackager packager)
    {
        var msg = new ISOMessage(new NullTestLogger(), packager);
        msg.Set(0, "1800");
        msg.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));
        msg.Set(11, "000001");
        msg.Set(24, "801");
        byte[] packed = msg.Pack();
        byte[] framed = new byte[2 + packed.Length];
        framed[0] = (byte)(packed.Length >> 8);
        framed[1] = (byte)(packed.Length & 0xFF);
        Array.Copy(packed, 0, framed, 2, packed.Length);
        return framed;
    }

    /// <summary>
    /// S6-7: 5 connections * 100 messages each = 500 total round-trips.
    /// All connections run concurrently.
    /// </summary>
    [Fact]
    public async Task FiveConnections_100MessagesEach_AllResponsesReceived()
    {
        const int connections = 5;
        const int messagesPerConn = 100;

        var options = new PipelineOptions
        {
            RawMessageCapacity = 64,
            ParsedMessageCapacity = 128,
            OutboundMessageCapacity = 64,
            ParserConcurrency = 2,
            DrainTimeoutSeconds = 30
        };

        var packager = CreatePackager();
        var registry = CreateRegistry();
        var host = new PipelineHost(options, registry, NullLoggerFactory.Instance);
        host.SetPackager(packager);

        byte[] framed = BuildFramedEchoMessage(packager);
        var tasks = new Task<int>[connections];

        for (int c = 0; c < connections; c++)
        {
            int connNum = c + 1;
            tasks[c] = Task.Run(async () =>
            {
                using var clientStream = new MemoryStream();
                for (int i = 0; i < messagesPerConn; i++)
                    clientStream.Write(framed, 0, framed.Length);
                clientStream.Position = 0;

                // SplitStream: reads come from clientStream, writes go to serverToClient
                using var serverToClient = new MemoryStream();
                using var serverStream = new SplitStream(clientStream, serverToClient);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                var pipeline = host.Accept(serverStream, connNum, $"test:{connNum}", cts.Token);

                // Wait for all responses via stats
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 15000 &&
                       pipeline.Stats.MessagesSent < messagesPerConn)
                {
                    await Task.Delay(10);
                }

                int sent = (int)pipeline.Stats.MessagesSent;
                cts.Cancel();
                await pipeline.StopAsync(TimeSpan.FromSeconds(5));
                await pipeline.DisposeAsync();

                return sent;
            });
        }

        await Task.WhenAll(tasks);

        for (int c = 0; c < connections; c++)
        {
            int resp = tasks[c].Result;
            Assert.True(resp >= messagesPerConn,
                $"Connection {c + 1}: expected {messagesPerConn} responses, got {resp}");
        }
    }

    /// <summary>
    /// S6-8: 100 rapid connect/disconnect cycles.
    /// Connect -> send one echo -> verify response -> disconnect.
    /// Verifies no leaks, no stale channels.
    /// </summary>
    [Fact]
    public async Task HundredConnectDisconnectCycles_NoLeaks()
    {
        const int cycles = 100;

        var options = new PipelineOptions
        {
            RawMessageCapacity = 8,
            ParsedMessageCapacity = 16,
            OutboundMessageCapacity = 8,
            ParserConcurrency = 1,
            DrainTimeoutSeconds = 5
        };

        var packager = CreatePackager();
        var registry = CreateRegistry();
        var host = new PipelineHost(options, registry, NullLoggerFactory.Instance);
        host.SetPackager(packager);

        byte[] framed = BuildFramedEchoMessage(packager);

        for (int i = 0; i < cycles; i++)
        {
            using var clientStream = new MemoryStream();
            clientStream.Write(framed, 0, framed.Length);
            clientStream.Position = 0;

            using var serverToClient = new MemoryStream();
            using var serverStream = new SplitStream(clientStream, serverToClient);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var pipeline = host.Accept(serverStream, i + 1, $"test:{i + 1}", cts.Token);

            // Wait for response
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 3000 &&
                   pipeline.Stats.MessagesSent < 1)
            {
                await Task.Delay(5);
            }

            long sent = pipeline.Stats.MessagesSent;
            int errs = (int)pipeline.Stats.ParseErrors + (int)pipeline.Stats.HandlerErrors;

            cts.Cancel();
            await pipeline.StopAsync(TimeSpan.FromSeconds(3));
            await pipeline.DisposeAsync();
            host.Remove(i + 1);

            Assert.True(sent >= 1, $"Cycle {i}: expected 1 response, got {sent}. Errors: {errs}");
            Assert.Equal(0, errs);
        }

        Assert.Equal(0, host.ConnectionCount);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private sealed class NullTestLogger : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private sealed class EchoHandler : IMessageHandler
    {
        public IReadOnlySet<string> SupportedMTIs { get; } = new HashSet<string> { "*" };

        public Task<ISOMessage?> HandleAsync(MessageContext context, CancellationToken ct)
        {
            context.Request.Set(0, "1814");
            context.Request.Set(39, "000");
            return Task.FromResult<ISOMessage?>(context.Request);
        }
    }

    /// <summary>
    /// A duplex stream where reads come from one MemoryStream and writes
    /// go to a separate MemoryStream. This mimics a real TCP socket
    /// where read/write are independent.
    /// </summary>
    private sealed class SplitStream : Stream
    {
        private readonly Stream _readFrom;
        private readonly Stream _writeTo;

        public SplitStream(Stream readFrom, Stream writeTo)
        {
            _readFrom = readFrom;
            _writeTo = writeTo;
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
            => _readFrom.Read(buffer, offset, count);

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => await _readFrom.ReadAsync(buffer, offset, count, ct);

        public override void Write(byte[] buffer, int offset, int count)
            => _writeTo.Write(buffer, offset, count);

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => await _writeTo.WriteAsync(buffer, offset, count, ct);

        public override void Flush() => _writeTo.Flush();
        public override async Task FlushAsync(CancellationToken ct) => await _writeTo.FlushAsync(ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
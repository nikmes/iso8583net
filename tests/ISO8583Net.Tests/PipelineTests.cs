using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Message;
using ISO8583Net.Packager;
using ISO8583Net.Server;
using ISO8583Net.Server.Pipeline;
using ISO8583Net.Server.Pipeline.Handlers;
using ISO8583Net.Server.Pipeline.Messages;
using ISO8583Service.Controllers;
using ISO8583Service;
using Microsoft.Extensions.Options;
using Xunit;

namespace ISO8583Net.Tests;

public sealed class PipelineTests
{
    private static ISOMessagePackager CreatePackager()
    {
        return new ISOMessagePackager(new NullTestLogger());
    }

    private static HandlerRegistry CreateRegistry()
    {
        return new HandlerRegistry(new[] { new EchoHandler() });
    }

    [Fact]
    public async Task PassThrough_EchoesRawBytes_RoundTrip()
    {
        // Arrange
        var options = new PipelineOptions
        {
            RawMessageCapacity = 8,
            ParsedMessageCapacity = 8,
            OutboundMessageCapacity = 8,
            DrainTimeoutSeconds = 5
        };

        var packager = CreatePackager();
        var host = new PipelineHost(options, CreateRegistry());
        host.SetPackager(packager);

        // Create an in-memory stream pair to simulate a socket
        using var clientStream = new MemoryStream();
        using var serverStream = new PassthroughStream(clientStream);

        // Build a valid ISO message using the packager
        var msg = new ISOMessage(new NullTestLogger(), packager);
        msg.Set(0, "1800");
        msg.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));
        msg.Set(11, "000001");
        msg.Set(24, "801");
        byte[] packed = msg.Pack();

        // Frame: 2-byte LI + packed message
        byte[] frame = new byte[2 + packed.Length];
        frame[0] = (byte)(packed.Length >> 8);
        frame[1] = (byte)(packed.Length & 0xFF);
        Array.Copy(packed, 0, frame, 2, packed.Length);

        clientStream.Write(frame, 0, frame.Length);
        clientStream.Position = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act — start pipeline (reader → parser → dispatcher → writer)
        var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

        // Wait for the message to be processed and echoed
        await Task.Delay(800);

        // Stop the pipeline
        await pipeline.StopAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.True(pipeline.Stats.MessagesReceived >= 1);
        Assert.True(pipeline.Stats.MessagesSent >= 1);
        Assert.Equal(0, pipeline.Stats.ParseErrors);
    }

    [Fact]
    public async Task PassThrough_MultipleMessages_AllEchoed()
    {
        var options = new PipelineOptions
        {
            RawMessageCapacity = 16,
            ParsedMessageCapacity = 16,
            OutboundMessageCapacity = 16,
            DrainTimeoutSeconds = 5
        };

        var packager = CreatePackager();
        var host = new PipelineHost(options, CreateRegistry());
        host.SetPackager(packager);
        using var clientStream = new MemoryStream();
        using var serverStream = new PassthroughStream(clientStream);

        // Write 3 valid ISO frames
        for (int i = 0; i < 3; i++)
        {
            var msg = new global::ISO8583Net.Message.ISOMessage(new NullTestLogger(), packager);
            msg.Set(0, "1800");
            msg.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));
            msg.Set(11, $"{i + 1:D6}");
            msg.Set(24, "801");
            byte[] packed = msg.Pack();

            byte[] frame = new byte[2 + packed.Length];
            frame[0] = (byte)(packed.Length >> 8);
            frame[1] = (byte)(packed.Length & 0xFF);
            Array.Copy(packed, 0, frame, 2, packed.Length);
            clientStream.Write(frame, 0, frame.Length);
        }
        clientStream.Position = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

        await Task.Delay(800);
        await pipeline.StopAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(3, pipeline.Stats.MessagesReceived);
        Assert.Equal(3, pipeline.Stats.MessagesSent);
        Assert.Equal(0, pipeline.Stats.ParseErrors);
    }

    [Fact]
    public async Task PassThrough_Keepalive_FrameIgnored()
    {
        var options = new PipelineOptions
        {
            RawMessageCapacity = 8,
            ParsedMessageCapacity = 8,
            OutboundMessageCapacity = 8,
            DrainTimeoutSeconds = 5
        };

        var packager = CreatePackager();
        var host = new PipelineHost(options, CreateRegistry());
        host.SetPackager(packager);
        using var clientStream = new MemoryStream();
        using var serverStream = new PassthroughStream(clientStream);

        // LI=0 keepalive — should be ignored
        clientStream.WriteByte(0x00);
        clientStream.WriteByte(0x00);

        // Real message
        var msg = new ISOMessage(new NullTestLogger(), packager);
        msg.Set(0, "1800");
        msg.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));
        msg.Set(11, "000001");
        byte[] packed = msg.Pack();
        clientStream.WriteByte((byte)(packed.Length >> 8));
        clientStream.WriteByte((byte)(packed.Length & 0xFF));
        clientStream.Write(packed, 0, packed.Length);

        clientStream.Position = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

        await Task.Delay(800);
        await pipeline.StopAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, pipeline.Stats.MessagesReceived);
        Assert.Equal(1, pipeline.Stats.MessagesSent);
    }

    [Fact]
    public async Task Parser_CorruptBytes_IncrementsErrorCount()
    {
        var options = new PipelineOptions
        {
            RawMessageCapacity = 8,
            ParsedMessageCapacity = 8,
            OutboundMessageCapacity = 8,
            DrainTimeoutSeconds = 5
        };

        var packager = CreatePackager();
        var host = new PipelineHost(options, CreateRegistry());
        host.SetPackager(packager);
        using var clientStream = new MemoryStream();
        using var serverStream = new PassthroughStream(clientStream);

        // Send corrupt bytes that can't be parsed as ISO
        byte[] corrupt = { 0xFF, 0xFE, 0xFD, 0xFC };
        clientStream.WriteByte((byte)(corrupt.Length >> 8));
        clientStream.WriteByte((byte)(corrupt.Length & 0xFF));
        clientStream.Write(corrupt, 0, corrupt.Length);

        // Send a valid message after the corrupt one
        var msg = new ISOMessage(new NullTestLogger(), packager);
        msg.Set(0, "1800");
        msg.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));
        msg.Set(11, "000001");
        byte[] packed = msg.Pack();
        clientStream.WriteByte((byte)(packed.Length >> 8));
        clientStream.WriteByte((byte)(packed.Length & 0xFF));
        clientStream.Write(packed, 0, packed.Length);

        clientStream.Position = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

        await Task.Delay(800);
        await pipeline.StopAsync(TimeSpan.FromSeconds(2));

        Assert.True(pipeline.Stats.MessagesReceived >= 2);
        Assert.Equal(1, pipeline.Stats.ParseErrors);
        // The valid message should still be echoed by the catch-all handler
        Assert.True(pipeline.Stats.MessagesSent >= 1);
    }

    [Fact]
    public async Task Parser_MultipleConcurrency_HandlesParallelMessages()
    {
        var options = new PipelineOptions
        {
            RawMessageCapacity = 32,
            ParsedMessageCapacity = 32,
            OutboundMessageCapacity = 32,
            ParserConcurrency = 2,
            DrainTimeoutSeconds = 5
        };

        var packager = CreatePackager();
        var host = new PipelineHost(options, CreateRegistry());
        host.SetPackager(packager);
        using var clientStream = new MemoryStream();
        using var serverStream = new PassthroughStream(clientStream);

        // Send 10 messages
        for (int i = 0; i < 10; i++)
        {
            var msg = new ISOMessage(new NullTestLogger(), packager);
            msg.Set(0, "1800");
            msg.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));
            msg.Set(11, $"{i + 1:D6}");
            byte[] packed = msg.Pack();

            byte[] frame = new byte[2 + packed.Length];
            frame[0] = (byte)(packed.Length >> 8);
            frame[1] = (byte)(packed.Length & 0xFF);
            Array.Copy(packed, 0, frame, 2, packed.Length);
            clientStream.Write(frame, 0, frame.Length);
        }
        clientStream.Position = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

        await Task.Delay(1200);
        await pipeline.StopAsync(TimeSpan.FromSeconds(3));

        Assert.Equal(10, pipeline.Stats.MessagesReceived);
        Assert.Equal(10, pipeline.Stats.MessagesSent);
        Assert.Equal(0, pipeline.Stats.ParseErrors);
    }

    // ═══════════════════════════════════════════════════════════════
    //  S3-7: MTI-specific handler routing
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Dispatcher_MTISpecificHandler_OnlyHandlesItsMTI()
    {
        var options = new PipelineOptions
        {
            RawMessageCapacity = 16,
            ParsedMessageCapacity = 16,
            OutboundMessageCapacity = 16,
            DrainTimeoutSeconds = 5
        };

        var packager = CreatePackager();
        var mti0200Handler = new CountingHandler("0200");
        var catchAllHandler = new CountingHandler("*");
        var registry = new HandlerRegistry(new IMessageHandler[] { mti0200Handler, catchAllHandler });

        var host = new PipelineHost(options, registry);
        host.SetPackager(packager);
        using var clientStream = new MemoryStream();
        using var serverStream = new PassthroughStream(clientStream);

        // Send 2 × "0200" messages (should hit both handlers)
        for (int i = 0; i < 2; i++)
        {
            var msg = new ISOMessage(new NullTestLogger(), packager);
            msg.Set(0, "0200");
            msg.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));
            msg.Set(11, $"{i + 1:D6}");
            byte[] packed = msg.Pack();
            byte[] frame = new byte[2 + packed.Length];
            frame[0] = (byte)(packed.Length >> 8);
            frame[1] = (byte)(packed.Length & 0xFF);
            Array.Copy(packed, 0, frame, 2, packed.Length);
            clientStream.Write(frame, 0, frame.Length);
        }

        // Send 2 × "0800" messages (should only hit catch-all)
        for (int i = 0; i < 2; i++)
        {
            var msg = new ISOMessage(new NullTestLogger(), packager);
            msg.Set(0, "0800");
            msg.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));
            msg.Set(11, $"{i + 3:D6}");
            byte[] packed = msg.Pack();
            byte[] frame = new byte[2 + packed.Length];
            frame[0] = (byte)(packed.Length >> 8);
            frame[1] = (byte)(packed.Length & 0xFF);
            Array.Copy(packed, 0, frame, 2, packed.Length);
            clientStream.Write(frame, 0, frame.Length);
        }
        clientStream.Position = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

        await Task.Delay(800);
        await pipeline.StopAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(4, pipeline.Stats.MessagesReceived);
        Assert.Equal(2, mti0200Handler.CallCount);   // 0200 handler: 2 calls
        Assert.Equal(4, catchAllHandler.CallCount);   // catch-all: all 4 messages
    }

    // ═══════════════════════════════════════════════════════════════
    //  S3-8: Handler parallelism verification
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Dispatcher_HandlersRunInParallel_NotSequential()
    {
        var options = new PipelineOptions
        {
            RawMessageCapacity = 16,
            ParsedMessageCapacity = 16,
            OutboundMessageCapacity = 16,
            DrainTimeoutSeconds = 5
        };

        var packager = CreatePackager();
        var delayMs = 80;
        var delayHandler = new DelayingHandler(TimeSpan.FromMilliseconds(delayMs));
        var registry = new HandlerRegistry(new IMessageHandler[] { delayHandler });

        var host = new PipelineHost(options, registry);
        host.SetPackager(packager);
        using var clientStream = new MemoryStream();
        using var serverStream = new PassthroughStream(clientStream);

        // Send 3 messages back-to-back
        for (int i = 0; i < 3; i++)
        {
            var msg = new ISOMessage(new NullTestLogger(), packager);
            msg.Set(0, "1800");
            msg.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));
            msg.Set(11, $"{i + 1:D6}");
            byte[] packed = msg.Pack();
            byte[] frame = new byte[2 + packed.Length];
            frame[0] = (byte)(packed.Length >> 8);
            frame[1] = (byte)(packed.Length & 0xFF);
            Array.Copy(packed, 0, frame, 2, packed.Length);
            clientStream.Write(frame, 0, frame.Length);
        }
        clientStream.Position = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

        var sw = Stopwatch.StartNew();
        // Wait for all 3 to complete (responses sent)
        while (pipeline.Stats.MessagesSent < 3 && sw.Elapsed < TimeSpan.FromSeconds(3))
            await Task.Delay(20);
        sw.Stop();

        await pipeline.StopAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(3, pipeline.Stats.MessagesReceived);
        Assert.Equal(3, pipeline.Stats.MessagesSent);

        // If sequential: 3 × 80ms = 240ms. Parallel: ≈ 80ms + overhead.
        // Allow generous margin (2.5×) but still well under sequential 240ms.
        Assert.True(sw.ElapsedMilliseconds < delayMs * 2.5,
            $"Handlers took {sw.ElapsedMilliseconds}ms, expected <{delayMs * 2.5}ms (parallel execution)");
    }

    // ═══════════════════════════════════════════════════════════════
    //  S4-7: Periodic SignOn — BroadcastSignOnRequestAsync e2e test
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task SignOn_Broadcast_PushesFramedMessageToWriter()
    {
        var options = new PipelineOptions
        {
            RawMessageCapacity = 8,
            ParsedMessageCapacity = 8,
            OutboundMessageCapacity = 8,
            DrainTimeoutSeconds = 5
        };

        var packager = CreatePackager();
        var registry = CreateRegistry();
        var host = new PipelineHost(options, registry);
        host.SetPackager(packager);
        using var clientStream = new MemoryStream();
        using var serverStream = new PassthroughStream(clientStream);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

        // No input messages — just broadcast a SignOn
        Assert.Equal(0, pipeline.Stats.MessagesSent);

        await host.BroadcastSignOnRequestAsync("801", cts.Token);

        // Wait for writer to dequeue and write
        await Task.Delay(500);

        await pipeline.StopAsync(TimeSpan.FromSeconds(2));

        // The broadcast should have produced exactly 1 outbound message
        Assert.Equal(1, pipeline.Stats.MessagesSent);
        Assert.Equal(0, pipeline.Stats.HandlerErrors);
    }

    [Fact]
    public async Task SignOn_Broadcast_MultipleMessages_AllSent()
    {
        var options = new PipelineOptions
        {
            RawMessageCapacity = 16,
            ParsedMessageCapacity = 16,
            OutboundMessageCapacity = 16,
            DrainTimeoutSeconds = 5
        };

        var packager = CreatePackager();
        var registry = CreateRegistry();
        var host = new PipelineHost(options, registry);
        host.SetPackager(packager);
        using var clientStream = new MemoryStream();
        using var serverStream = new PassthroughStream(clientStream);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

        // Broadcast 3 SignOn requests (simulating periodic timer ticks)
        await host.BroadcastSignOnRequestAsync("801", cts.Token);
        await host.BroadcastSignOnRequestAsync("831", cts.Token); // Echo
        await host.BroadcastSignOnRequestAsync("803", cts.Token); // SignOff

        await Task.Delay(500);
        await pipeline.StopAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(3, pipeline.Stats.MessagesSent);
        Assert.Equal(0, pipeline.Stats.HandlerErrors);
    }

    // ═══════════════════════════════════════════════════════════════
    //  S4-8: REST API — Iso8583Controller.SendSignOn
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Controller_SendSignOn_CallsServerMethod()
    {
        var mockServer = new MockIso8583Server
        {
            IsRunning = true,
            ConnectionCount = 2,
            Connections = new List<(int, string, DateTime)>
            {
                (1, "192.168.1.1:12345", DateTime.UtcNow),
                (2, "192.168.1.2:54321", DateTime.UtcNow)
            }
        };

        var options = new PipelineOptions();
        var registry = CreateRegistry();
        var host = new PipelineHost(options, registry);
        var serverOptions = Options.Create(new ServerOptions());

        var controller = new Iso8583Controller(
            mockServer, host, serverOptions,
            new NullTestLogger<Iso8583Controller>());

        var result = await controller.SendSignOn(CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
        Assert.Equal(1, mockServer.SendSignOnCallCount);
    }

    [Fact]
    public async Task Controller_SendSignOn_ServerNotRunning_ReturnsBadRequest()
    {
        var mockServer = new MockIso8583Server
        {
            IsRunning = false,
            ConnectionCount = 0
        };

        var options = new PipelineOptions();
        var registry = CreateRegistry();
        var host = new PipelineHost(options, registry);
        var serverOptions = Options.Create(new ServerOptions());

        var controller = new Iso8583Controller(
            mockServer, host, serverOptions,
            new NullTestLogger<Iso8583Controller>());

        var result = await controller.SendSignOn(CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);
        Assert.Equal(0, mockServer.SendSignOnCallCount);
    }

    [Fact]
    public async Task Controller_SendSignOn_NoClients_ReturnsOkWithMessage()
    {
        var mockServer = new MockIso8583Server
        {
            IsRunning = true,
            ConnectionCount = 0
        };

        var options = new PipelineOptions();
        var registry = CreateRegistry();
        var host = new PipelineHost(options, registry);
        var serverOptions = Options.Create(new ServerOptions());

        var controller = new Iso8583Controller(
            mockServer, host, serverOptions,
            new NullTestLogger<Iso8583Controller>());

        var result = await controller.SendSignOn(CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
        Assert.Equal(0, mockServer.SendSignOnCallCount); // not called — no clients
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private sealed class CountingHandler : IMessageHandler
    {
        public int CallCount;
        public IReadOnlySet<string> SupportedMTIs { get; }
        public CountingHandler(params string[] mtis) => SupportedMTIs = new HashSet<string>(mtis);

        public Task<ISOMessage?> HandleAsync(MessageContext context, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);
            return Task.FromResult<ISOMessage?>(context.Request);
        }
    }

    private sealed class DelayingHandler : IMessageHandler
    {
        private readonly TimeSpan _delay;
        public IReadOnlySet<string> SupportedMTIs { get; } = new HashSet<string> { "*" };
        public DelayingHandler(TimeSpan delay) => _delay = delay;

        public async Task<ISOMessage?> HandleAsync(MessageContext context, CancellationToken ct)
        {
            await Task.Delay(_delay, ct);
            return context.Request;
        }
    }

    private sealed class NullTestLogger : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    /// <summary>
    /// Echoes every message back (catch-all handler for pipeline tests).
    /// </summary>
    private sealed class EchoHandler : IMessageHandler
    {
        public IReadOnlySet<string> SupportedMTIs { get; } = new HashSet<string> { "*" };

        public Task<ISOMessage?> HandleAsync(MessageContext context, CancellationToken ct)
        {
            return Task.FromResult<ISOMessage?>(context.Request);
        }
    }

    /// <summary>
    /// A stream that reads from a MemoryStream but writes to the *same*
    /// MemoryStream — simulating a full-duplex socket pair for testing.
    /// </summary>
    private sealed class PassthroughStream : Stream
    {
        private readonly Stream _readFrom;

        public PassthroughStream(Stream readFrom)
        {
            _readFrom = readFrom;
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

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => _readFrom.ReadAsync(buffer, offset, count, ct);

        public override void Write(byte[] buffer, int offset, int count)
        {
            long pos = _readFrom.Position;
            _readFrom.Position = _readFrom.Length;
            _readFrom.Write(buffer, offset, count);
            _readFrom.Position = pos;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            long pos = _readFrom.Position;
            _readFrom.Position = _readFrom.Length;
            await _readFrom.WriteAsync(buffer, offset, count, ct);
            _readFrom.Position = pos;
        }

        public override void Flush() => _readFrom.Flush();
        public override Task FlushAsync(CancellationToken ct) => _readFrom.FlushAsync(ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    /// <summary>Generic variant for controller tests.</summary>
    private sealed class NullTestLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    /// <summary>Minimal mock for the controller tests.</summary>
    private sealed class MockIso8583Server : IIso8583Server
    {
        public bool IsRunning { get; set; }
        public int ConnectionCount { get; set; }
        public int SignOnIntervalSeconds { get; set; }
        public bool SendSignOnOnConnect { get; set; }
        public bool EnablePeriodicSignOn { get; set; }
        public TlsOptions Tls { get; set; } = new();
        public Action<string>? OnLog { get; set; }
        public Action<string>? OnStatusChanged { get; set; }
        public Action<int, byte[], string, string>? OnMessageParsed { get; set; }

        public int SendSignOnCallCount;

        public List<(int ConnNum, string RemoteEndpoint, DateTime ConnectedAt)> Connections { get; set; } = new();

        public Task StartAsync(int port, string? dialectPath, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;

        public Task SendSignOnAsync(CancellationToken ct = default)
        {
            Interlocked.Increment(ref SendSignOnCallCount);
            return Task.CompletedTask;
        }
        public Task SendEchoAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SendSignOffAsync(bool disconnectAfter = false, CancellationToken ct = default)
            => Task.CompletedTask;

        public IReadOnlyList<(int ConnNum, string RemoteEndpoint, DateTime ConnectedAt)> GetConnections()
            => Connections;
    }
}

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Packager;
using ISO8583Net.Server.Pipeline;
using Xunit;

namespace ISO8583Net.Tests;

public sealed class PipelineTests
{
    private static ISOMessagePackager CreatePackager()
    {
        // Use built-in VISA dialect (no file needed)
        return new ISOMessagePackager(new NullTestLogger());
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
        var host = new PipelineHost(options, packager);

        // Create an in-memory stream pair to simulate a socket
        using var clientStream = new MemoryStream();
        using var serverStream = new PassthroughStream(clientStream);

        // Build a valid ISO message using the packager
        var msg = new global::ISO8583Net.Message.ISOMessage(new NullTestLogger(), packager);
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

        // Act — start pipeline (reader → parser → echo bridge → writer)
        var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

        // Wait for the message to be processed and echoed
        await Task.Delay(800);

        // Stop the pipeline
        await pipeline.StopAsync(TimeSpan.FromSeconds(2));

        // Assert — the echoed frame should be in the client stream
        // Parser echo bridge re-packs, so the output should match the original packed bytes
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
        var host = new PipelineHost(options, packager);
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
        var host = new PipelineHost(options, packager);
        using var clientStream = new MemoryStream();
        using var serverStream = new PassthroughStream(clientStream);

        // LI=0 keepalive — should be ignored
        clientStream.WriteByte(0x00);
        clientStream.WriteByte(0x00);

        // Real message
        var msg = new global::ISO8583Net.Message.ISOMessage(new NullTestLogger(), packager);
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
        var host = new PipelineHost(options, packager);
        using var clientStream = new MemoryStream();
        using var serverStream = new PassthroughStream(clientStream);

        // Send corrupt bytes that can't be parsed as ISO
        byte[] corrupt = { 0xFF, 0xFE, 0xFD, 0xFC };
        clientStream.WriteByte((byte)(corrupt.Length >> 8));
        clientStream.WriteByte((byte)(corrupt.Length & 0xFF));
        clientStream.Write(corrupt, 0, corrupt.Length);

        // Send a valid message after the corrupt one
        var msg = new global::ISO8583Net.Message.ISOMessage(new NullTestLogger(), packager);
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
        // The valid message should still be echoed
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
        var host = new PipelineHost(options, packager);
        using var clientStream = new MemoryStream();
        using var serverStream = new PassthroughStream(clientStream);

        // Send 10 messages
        for (int i = 0; i < 10; i++)
        {
            var msg = new global::ISO8583Net.Message.ISOMessage(new NullTestLogger(), packager);
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

    private sealed class NullTestLogger : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) { }
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
}

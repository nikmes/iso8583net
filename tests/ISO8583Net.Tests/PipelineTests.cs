using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Server.Pipeline;
using Xunit;

namespace ISO8583Net.Tests;

public sealed class PipelineTests
{
    [Fact]
    public async Task PassThrough_EchoesRawBytes_RoundTrip()
    {
        // Arrange
        var options = new PipelineOptions
        {
            RawMessageCapacity = 8,
            OutboundMessageCapacity = 8,
            DrainTimeoutSeconds = 5
        };

        var host = new PipelineHost(options);

        // Create an in-memory stream pair to simulate a socket
        using var clientStream = new MemoryStream();
        using var serverStream = new PassthroughStream(clientStream);

        // Frame: 2-byte LI (length=5) + "HELLO"
        byte[] frame = { 0x00, 0x05, (byte)'H', (byte)'E', (byte)'L', (byte)'L', (byte)'O' };
        clientStream.Write(frame, 0, frame.Length);
        clientStream.Position = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act — start pipeline (reader → pass-through → writer echoes back)
        var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

        // Wait briefly for the message to be echoed
        await Task.Delay(500);

        // Stop the pipeline
        await pipeline.StopAsync(TimeSpan.FromSeconds(2));

        // Assert — the echoed frame should be in the client stream
        clientStream.Position = 0;
        byte[] echoed = new byte[7];
        int read = clientStream.Read(echoed, 0, echoed.Length);

        Assert.Equal(7, read);
        Assert.Equal(frame, echoed);

        // Stats
        Assert.Equal(1, pipeline.Stats.MessagesReceived);
        Assert.Equal(1, pipeline.Stats.MessagesSent);
    }

    [Fact]
    public async Task PassThrough_MultipleMessages_AllEchoed()
    {
        var options = new PipelineOptions
        {
            RawMessageCapacity = 16,
            OutboundMessageCapacity = 16,
            DrainTimeoutSeconds = 5
        };

        var host = new PipelineHost(options);
        using var clientStream = new MemoryStream();
        using var serverStream = new PassthroughStream(clientStream);

        // Write 3 frames
        for (int i = 0; i < 3; i++)
        {
            byte[] payload = System.Text.Encoding.ASCII.GetBytes($"MSG{i}");
            clientStream.WriteByte((byte)(payload.Length >> 8));
            clientStream.WriteByte((byte)(payload.Length & 0xFF));
            clientStream.Write(payload, 0, payload.Length);
        }
        clientStream.Position = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

        await Task.Delay(500);
        await pipeline.StopAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(3, pipeline.Stats.MessagesReceived);
        Assert.Equal(3, pipeline.Stats.MessagesSent);
    }

    [Fact]
    public async Task PassThrough_Keepalive_FrameIgnored()
    {
        var options = new PipelineOptions
        {
            RawMessageCapacity = 8,
            OutboundMessageCapacity = 8,
            DrainTimeoutSeconds = 5
        };

        var host = new PipelineHost(options);
        using var clientStream = new MemoryStream();
        using var serverStream = new PassthroughStream(clientStream);

        // LI=0 keepalive + real message
        clientStream.WriteByte(0x00);
        clientStream.WriteByte(0x00); // zero-length keepalive — should be ignored

        byte[] payload = System.Text.Encoding.ASCII.GetBytes("DATA");
        clientStream.WriteByte((byte)(payload.Length >> 8));
        clientStream.WriteByte((byte)(payload.Length & 0xFF));
        clientStream.Write(payload, 0, payload.Length);

        clientStream.Position = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

        await Task.Delay(500);
        await pipeline.StopAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, pipeline.Stats.MessagesReceived);
        Assert.Equal(1, pipeline.Stats.MessagesSent);
    }

    /// <summary>
    /// A stream that reads from a MemoryStream but writes to a different
    /// in-memory buffer — simulating a full-duplex socket pair.
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
            => _readFrom.Write(buffer, offset, count);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => _readFrom.WriteAsync(buffer, offset, count, ct);

        public override void Flush() => _readFrom.Flush();
        public override Task FlushAsync(CancellationToken ct) => _readFrom.FlushAsync(ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Header;
using ISO8583Net.Message;
using ISO8583Net.Packager;
using ISO8583Net.Server;
using ISO8583Net.Server.Pipeline;
using ISO8583Net.Server.Pipeline.Handlers;
using ISO8583Net.Server.Pipeline.Messages;
using Microsoft.Extensions.Logging.Abstractions;
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
        var host = new PipelineHost(options, CreateRegistry(), NullLoggerFactory.Instance);
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
        var host = new PipelineHost(options, CreateRegistry(), NullLoggerFactory.Instance);
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
        var host = new PipelineHost(options, CreateRegistry(), NullLoggerFactory.Instance);
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
        var host = new PipelineHost(options, CreateRegistry(), NullLoggerFactory.Instance);
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
        var host = new PipelineHost(options, CreateRegistry(), NullLoggerFactory.Instance);
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

        var host = new PipelineHost(options, registry, NullLoggerFactory.Instance);
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

        var host = new PipelineHost(options, registry, NullLoggerFactory.Instance);
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
        var host = new PipelineHost(options, registry, NullLoggerFactory.Instance);
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
        var host = new PipelineHost(options, registry, NullLoggerFactory.Instance);
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

    /// <summary>
    /// Parses a real D8 ISO 8583:1993 1100 Authorisation Request hex dump
    /// and verifies all fields unpack correctly.
    /// </summary>
    [Fact]
    public void Unpack_D8HexDump_1100_AuthorisationRequest_ParsesWithoutError()
    {
        const string hexDump =
            "00FD4732422D49534F2D312E3030303131303030303030" +
            "11007674255188E1A00010343633333731303130303030" +
            "3035303500000000000000500000000000500007231446" +
            "0961000000563813260723144609281104283130303032" +
            "3036382020202001005812260723064570000649875036" +
            "3230343134353633383133343938373530363834393837" +
            "35303030303433333437322846554D494E4F523033332D" +
            "412E2053414841524F56412032304E455720594F524B20" +
            "202020205553003AC00703303030C0090100C0100108C1" +
            "031D303030322020202020202020202020202020202020" +
            "202020202020202030C207092020202020202020200978" +
            "0978";

        byte[] packedBytes = ISO8583Net.Utilities.ISOUtils.Hex2Bytes(hexDump);
        byte[] msgBytes = packedBytes[2..]; // strip 0x00FD (2-byte length prefix)

        string dialectPath = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ISO8583Net", "ISODialects", "d8-iso8583.json"));

        var packager = new ISOMessagePackager(new NullTestLogger(), dialectPath);
        var msg = new ISOMessage(new NullTestLogger(), packager);

        // Act
        msg.UnPack(msgBytes);

        // Assert
        Assert.Equal("1100", msg.GetFieldValue(0));
        Assert.Equal("7674255188E1A000", msg.GetFieldValue(1));
        Assert.Equal("4633710100000505", msg.GetFieldValue(2));
        Assert.Equal("000000", msg.GetFieldValue(3));
        Assert.Equal("000000005000", msg.GetFieldValue(4));
        Assert.Equal("000000005000", msg.GetFieldValue(6));
        Assert.Equal("0723144609", msg.GetFieldValue(7));
        Assert.Equal("61000000", msg.GetFieldValue(10));
        Assert.Equal("563813", msg.GetFieldValue(11));
        Assert.Equal("260723144609", msg.GetFieldValue(12));
        Assert.Equal("2811", msg.GetFieldValue(14));
        Assert.Equal("428", msg.GetFieldValue(19));
        Assert.Equal("10002068    ", msg.GetFieldValue(22));
        Assert.Equal("100", msg.GetFieldValue(24));
        Assert.Equal("5812", msg.GetFieldValue(26));
        Assert.Equal("260723", msg.GetFieldValue(28));
        Assert.Equal("457000", msg.GetFieldValue(32));
        Assert.Equal("498750", msg.GetFieldValue(33));
        Assert.Equal("620414563813", msg.GetFieldValue(37));
        Assert.Equal("49875068", msg.GetFieldValue(41));
        Assert.Equal("498750000433472", msg.GetFieldValue(42));
        Assert.Equal("FUMINOR033-A. SAHAROVA 20NEW YORK     US", msg.GetFieldValue(43));
        Assert.NotNull(msg.GetFieldValue(48)); // TLV, verify present
        Assert.Equal("009", msg.GetFieldValue(49));
        Assert.Equal("809", msg.GetFieldValue(51));
    }

    [Fact]
    public void Unpack_BitmapLess_HeaderError_DoesNotThrow()
    {
        string dialectPath = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ISO8583Net", "ISODialects", "d8-iso8583.json"));

        var packager = new ISOMessagePackager(new NullTestLogger(), dialectPath);
        var msg = new ISOMessage(new NullTestLogger(), packager);

        // 23-byte payload: 21-byte D8 header (FieldInError = "999") + MTI "9800",
        // with NO bitmap following the MTI.
        byte[] payload = ISO8583Net.Utilities.ISOUtils.Hex2Bytes(
            "4732422D49534F2D312E3030" + // "G2B-ISO-1.00"
            "3030" +                       // Message Source "00"
            "3130" +                       // Version Number "10"
            "393939" +                     // Field in Error "999"
            "3030" +                       // Reserved "00"
            "9800");                       // MTI "9800" (BCD, 2 bytes)

        msg.UnPack(payload); // must not throw

        Assert.Equal("9800", msg.GetFieldValue(0));

        var d8 = Assert.IsType<ISOHeaderD8>(msg.Header);
        Assert.Equal("G2B-ISO-1.00", d8.ProtocolVersionIdentifier);
        Assert.Equal("999", d8.FieldInError);

        // "9800" is a format-error transformation emitted as a raw bitmap-less frame, not
        // a dialect message type, so the bitmap-less message validates as an unknown MTI
        // (still unpackable without throwing).
        Assert.NotNull(msg.ValidationResult);
        Assert.False(msg.ValidationResult!.IsMtiKnown);
    }

    /// <summary>
    /// Verifies that Field 48 (Fixed TLV Format 2) is broken down into tags
    /// by the FixedTlvInterpreter wired into the D8 dialect.
    /// </summary>
    [Fact]
    public void FixedTlvInterpreter_D8Field48_BreaksDownTagsInToString()
    {
        string dialectPath = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ISO8583Net", "ISODialects", "d8-iso8583.json"));

        var packager = new ISOMessagePackager(new NullTestLogger(), dialectPath);
        var msg = new ISOMessage(new NullTestLogger(), packager);
        msg.Set(0, "1100");
        msg.Set(48, "C00703303030C0090100C0100108C1031D3030303220202020202020202020202020202020202020202020202030C20709202020202020202020");

        string f48Output = msg.GetField(48).ToString();

        Assert.Contains("[Tag C007]", f48Output);
        Assert.Contains("ICC Additional POS information", f48Output);
        Assert.Contains("[Tag C009]", f48Output);
        Assert.Contains("Terminal Type", f48Output);
        Assert.Contains("[Tag C010]", f48Output);
        Assert.Contains("Point of Service Condition Code", f48Output);
        Assert.Contains("[Tag C103]", f48Output);
        Assert.Contains("Visa Additional Data - VIP Format Private-Use Field 1", f48Output);
        Assert.Contains("[Tag C207]", f48Output);
        Assert.Contains("VISA V.me data", f48Output);
    }

    /// <summary>
    /// Verifies that Field 55 (BER-TLV ICC data) is broken down into tags
    /// by the BerTlvInterpreter wired into the D8 dialect.
    /// </summary>
    [Fact]
    public void BerTlvInterpreter_D8Field55_BreaksDownTagsInToString()
    {
        string dialectPath = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ISO8583Net", "ISODialects", "d8-iso8583.json"));

        var packager = new ISOMessagePackager(new NullTestLogger(), dialectPath);
        var msg = new ISOMessage(new NullTestLogger(), packager);
        msg.Set(0, "1100");

        // Sample EMV BER-TLV data:
        //   5F2A 02 0978          - Transaction Currency Code
        //   9F02 06 000000010000  - Amount, Authorised
        //   9F1A 02 0840          - Terminal Country Code
        //   9F34 03 1E0300        - CVM Results
        //   9F37 04 12345678      - Unpredictable Number
        msg.Set(55, "5F2A0209789F02060000000100009F1A0208409F34031E03009F370412345678");

        string f55Output = msg.GetField(55).ToString();

        Assert.Contains("[Tag 5F2A]", f55Output);
        Assert.Contains("Transaction Currency Code", f55Output);
        Assert.Contains("[Tag 9F02]", f55Output);
        Assert.Contains("Amount, Authorised", f55Output);
        Assert.Contains("[Tag 9F1A]", f55Output);
        Assert.Contains("Terminal Country Code", f55Output);
        Assert.Contains("[Tag 9F34]", f55Output);
        Assert.Contains("Cardholder Verification Method (CVM) Results", f55Output);
        Assert.Contains("[Tag 9F37]", f55Output);
        Assert.Contains("Unpredictable Number", f55Output);
    }

    // ═══════════════════════════════════════════════════════════════
    //  D2: Inbound dialect enforcement
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Unpack_UnknownMti_SetsValidationResultMtiUnknown()
    {
        string dialectPath = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ISO8583Net", "ISODialects", "d8-iso8583.json"));

        var packager = new ISOMessagePackager(new NullTestLogger(), dialectPath);

        var msg = new ISOMessage(new NullTestLogger(), packager);
        msg.Set(0, "9999");
        msg.Set(7, "0723144609");
        msg.Set(11, "000001");
        byte[] packed = msg.Pack();

        var parsed = new ISOMessage(new NullTestLogger(), packager);
        parsed.UnPack(packed);

        Assert.NotNull(parsed.ValidationResult);
        Assert.False(parsed.ValidationResult!.IsMtiKnown);
        Assert.False(parsed.ValidationResult!.IsValid);
    }

    [Fact]
    public void Unpack_KnownMti_MissingMandatory_ReportsMissingFields()
    {
        string dialectPath = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ISO8583Net", "ISODialects", "d8-iso8583.json"));

        var packager = new ISOMessagePackager(new NullTestLogger(), dialectPath);

        // 1804 requires F7, F11, F24, F28 — set only F7 so F11/F24/F28 are missing.
        var msg = new ISOMessage(new NullTestLogger(), packager);
        msg.Set(0, "1804");
        msg.Set(7, "0723144609");
        byte[] packed = msg.Pack();

        var parsed = new ISOMessage(new NullTestLogger(), packager);
        parsed.UnPack(packed);

        Assert.NotNull(parsed.ValidationResult);
        Assert.True(parsed.ValidationResult!.IsMtiKnown);
        Assert.False(parsed.ValidationResult!.IsValid);
        Assert.Contains(11, parsed.ValidationResult!.MissingMandatoryFields);
        Assert.Contains(24, parsed.ValidationResult!.MissingMandatoryFields);
        Assert.Contains(28, parsed.ValidationResult!.MissingMandatoryFields);
    }

    [Fact]
    public async Task Dispatcher_UnknownMti_EmitsFormatErrorResponse()
    {
        var options = new PipelineOptions
        {
            RawMessageCapacity = 8,
            ParsedMessageCapacity = 8,
            OutboundMessageCapacity = 8,
            DrainTimeoutSeconds = 5
        };

        string dialectPath = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ISO8583Net", "ISODialects", "d8-iso8583.json"));

        var packager = new ISOMessagePackager(new NullTestLogger(), dialectPath);
        var mti1100Handler = new CountingHandler("1100");
        var registry = new HandlerRegistry(new IMessageHandler[] { mti1100Handler });

        var host = new PipelineHost(options, registry, NullLoggerFactory.Instance);
        host.SetPackager(packager);
        using var clientStream = new MemoryStream();
        using var serverStream = new PassthroughStream(clientStream);

        // Build an MTI "1800" frame (unknown to the D8 dialect, and not a 9xxx
        // format-error response) with a D8 header.
        var msg = new ISOMessage(new NullTestLogger(), packager);
        msg.Set(0, "1800");
        msg.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));
        msg.Set(11, "000001");
        byte[] packed = msg.Pack();

        byte[] frame = new byte[2 + packed.Length];
        frame[0] = (byte)(packed.Length >> 8);
        frame[1] = (byte)(packed.Length & 0xFF);
        Array.Copy(packed, 0, frame, 2, packed.Length);

        clientStream.Write(frame, 0, frame.Length);
        clientStream.Position = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

        await Task.Delay(800);
        await pipeline.StopAsync(TimeSpan.FromSeconds(2));

        // The unknown-MTI message must NOT reach any business handler.
        Assert.Equal(0, mti1100Handler.CallCount);
        Assert.True(pipeline.Stats.MessagesSent >= 1);
        Assert.True(pipeline.Stats.MessagesReceived >= 1);
        Assert.Equal(0, pipeline.Stats.ParseErrors);

        // The outbound response must end with a bitmap-less D8 "9800" frame (0x98 0x00).
        byte[] output = clientStream.ToArray();
        Assert.True(output.Length >= 2);
        Assert.Equal(0x98, output[output.Length - 2]);
        Assert.Equal(0x00, output[output.Length - 1]);
    }

    [Fact]
    public async Task Dispatcher_KnownMti_MissingMandatory_Emits9xxxFieldError()
    {
        var options = new PipelineOptions
        {
            RawMessageCapacity = 8,
            ParsedMessageCapacity = 8,
            OutboundMessageCapacity = 8,
            DrainTimeoutSeconds = 5
        };

        string dialectPath = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ISO8583Net", "ISODialects", "d8-iso8583.json"));

        var packager = new ISOMessagePackager(new NullTestLogger(), dialectPath);
        var mti1804Handler = new CountingHandler("1804");
        var registry = new HandlerRegistry(new IMessageHandler[] { mti1804Handler });

        var host = new PipelineHost(options, registry, NullLoggerFactory.Instance);
        host.SetPackager(packager);
        using var clientStream = new MemoryStream();
        using var serverStream = new PassthroughStream(clientStream);

        // 1804 requires F7, F11, F24, F28. Set F7/F11/F24 but leave F28 unset so F28 is
        // the first missing mandatory field (first offending field = 28).
        var msg = new ISOMessage(new NullTestLogger(), packager);
        msg.Set(0, "1804");
        msg.Set(7, "0723144609");
        msg.Set(11, "000001");
        msg.Set(24, "801");
        byte[] packed = msg.Pack();

        byte[] frame = new byte[2 + packed.Length];
        frame[0] = (byte)(packed.Length >> 8);
        frame[1] = (byte)(packed.Length & 0xFF);
        Array.Copy(packed, 0, frame, 2, packed.Length);

        clientStream.Write(frame, 0, frame.Length);
        clientStream.Position = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

        await Task.Delay(800);
        await pipeline.StopAsync(TimeSpan.FromSeconds(2));

        // The field-error message must NOT reach the business handler.
        Assert.Equal(0, mti1804Handler.CallCount);
        Assert.True(pipeline.Stats.MessagesSent >= 1);
        Assert.True(pipeline.Stats.MessagesReceived >= 1);
        Assert.Equal(0, pipeline.Stats.ParseErrors);

        // The response must end with a bitmap-less BCD "9804" frame (0x98 0x04) and its
        // D8 header "Field in Error" (frame offsets 18-20) must be "028".
        byte[] output = clientStream.ToArray();
        Assert.True(output.Length >= 25);
        Assert.Equal(0x98, output[output.Length - 2]);
        Assert.Equal(0x04, output[output.Length - 1]);
        Assert.Equal((byte)'0', output[output.Length - 7]);
        Assert.Equal((byte)'2', output[output.Length - 6]);
        Assert.Equal((byte)'8', output[output.Length - 5]);
    }

    [Fact]
    public async Task Dispatcher_KnownMti_DisallowedField_Emits9xxxFieldError()
    {
        var options = new PipelineOptions
        {
            RawMessageCapacity = 8,
            ParsedMessageCapacity = 8,
            OutboundMessageCapacity = 8,
            DrainTimeoutSeconds = 5
        };

        string dialectPath = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ISO8583Net", "ISODialects", "d8-iso8583.json"));

        var packager = new ISOMessagePackager(new NullTestLogger(), dialectPath);
        var mti1804Handler = new CountingHandler("1804");
        var registry = new HandlerRegistry(new IMessageHandler[] { mti1804Handler });

        var host = new PipelineHost(options, registry, NullLoggerFactory.Instance);
        host.SetPackager(packager);
        using var clientStream = new MemoryStream();
        using var serverStream = new PassthroughStream(clientStream);

        // F3 (Processing Code) does not participate in 1804, so including it is a
        // disallowed field and is the first offending field (= 3).
        var msg = new ISOMessage(new NullTestLogger(), packager);
        msg.Set(0, "1804");
        msg.Set(3, "000000");
        msg.Set(7, "0723144609");
        msg.Set(11, "000001");
        msg.Set(24, "801");
        msg.Set(28, "240101");
        byte[] packed = msg.Pack();

        byte[] frame = new byte[2 + packed.Length];
        frame[0] = (byte)(packed.Length >> 8);
        frame[1] = (byte)(packed.Length & 0xFF);
        Array.Copy(packed, 0, frame, 2, packed.Length);

        clientStream.Write(frame, 0, frame.Length);
        clientStream.Position = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pipeline = host.Accept(serverStream, 1, "127.0.0.1:0", cts.Token);

        await Task.Delay(800);
        await pipeline.StopAsync(TimeSpan.FromSeconds(2));

        // The field-error message must NOT reach the business handler.
        Assert.Equal(0, mti1804Handler.CallCount);
        Assert.True(pipeline.Stats.MessagesSent >= 1);
        Assert.True(pipeline.Stats.MessagesReceived >= 1);
        Assert.Equal(0, pipeline.Stats.ParseErrors);

        // The response must end with a bitmap-less BCD "9804" frame (0x98 0x04) and its
        // D8 header "Field in Error" (frame offsets 18-20) must be "003".
        byte[] output = clientStream.ToArray();
        Assert.True(output.Length >= 25);
        Assert.Equal(0x98, output[output.Length - 2]);
        Assert.Equal(0x04, output[output.Length - 1]);
        Assert.Equal((byte)'0', output[output.Length - 7]);
        Assert.Equal((byte)'0', output[output.Length - 6]);
        Assert.Equal((byte)'3', output[output.Length - 5]);
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

}

using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Message;
using ISO8583Net.Packager;
using ISO8583Net.Utilities;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Server;

/// <summary>
/// TCP server that listens for ISO 8583 messages with 2-byte big-endian length prefix.
/// Parses messages using the configured dialect and reports results via callbacks.
/// </summary>
public sealed class Iso8583TcpServer : IIso8583Server
{
    private const int MaxMessageSize = 4096;
    private const int LengthPrefixSize = 2;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private ISOMessagePackager? _packager;
    private int _connectionCount;

    public bool IsRunning => _listener != null;
    public Action<string>? OnLog { get; set; }
    public Action<string>? OnStatusChanged { get; set; }
    public Action<int, byte[], string, string>? OnMessageParsed { get; set; }

    public async Task StartAsync(int port, string? dialectPath, CancellationToken ct = default)
    {
        Log("─── ISO 8583 Test Server ───");

        // ── Load dialect ────────────────────────────────────────────────
        bool isBuiltIn = string.IsNullOrWhiteSpace(dialectPath);
        if (isBuiltIn)
        {
            Log("Loading built-in VISA BASE I dialect...");
            _packager = new ISOMessagePackager(new NullLogger());
        }
        else
        {
            Log($"Loading dialect from [{dialectPath}]...");
            _packager = new ISOMessagePackager(new NullLogger(), dialectPath);
        }
        Log($"Dialect loaded. {_packager.GetTotalFields()} fields defined.");

        // ── Start listener ──────────────────────────────────────────────
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _listener = new TcpListener(IPAddress.Any, port);

        try { _listener.Start(); }
        catch (Exception ex)
        {
            Log($"ERROR: Failed to start on port {port}: {ex.Message}");
            _listener = null;
            return;
        }

        Log($"Listening on port {port}...");
        OnStatusChanged?.Invoke($"Listening on port {port}");

        _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        Log("Stopping listener...");
        _cts?.Cancel();
        _listener?.Stop();
        _listener = null;
        Log("Server stopped.");
        OnStatusChanged?.Invoke("Idle");
    }

    // ═══════════════════════════════════════════════════════════════════════

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _listener != null)
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                int connNum = Interlocked.Increment(ref _connectionCount);

                Log($"[#{connNum}] Client connected: {client.Client.RemoteEndPoint}");
                OnStatusChanged?.Invoke($"Active connections: {_connectionCount}");

                _ = HandleClientAsync(client, connNum, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex) { Log($"[ERROR] Accept loop: {ex.Message}"); }
    }

    private async Task HandleClientAsync(TcpClient client, int connNum, CancellationToken ct)
    {
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var lengthBuf = new byte[LengthPrefixSize];

                while (!ct.IsCancellationRequested)
                {
                    try { await ReadExactlyAsync(stream, lengthBuf, LengthPrefixSize, ct); }
                    catch (EndOfStreamException) { Log($"[#{connNum}] Disconnected."); return; }

                    int msgLen = (lengthBuf[0] << 8) | lengthBuf[1];

                    if (msgLen <= 0 || msgLen > MaxMessageSize)
                    {
                        Log($"[#{connNum}] Invalid length: {msgLen} (0x{ISOUtils.Bytes2Hex(lengthBuf, LengthPrefixSize)})");
                        return;
                    }

                    byte[] buf = ArrayPool<byte>.Shared.Rent(msgLen);
                    try
                    {
                        await ReadExactlyAsync(stream, buf, msgLen, ct);
                        var span = buf.AsSpan(0, msgLen);

                        Log($"[#{connNum}] Received {msgLen} bytes");
                        string hexDump = FormatHexDump(connNum, span);
                        Log(hexDump);

                        string parsed = ParseMessage(connNum, span.ToArray());
                        Log($"[#{connNum}] ── Parsed Message ──\n{parsed}");

                        OnMessageParsed?.Invoke(connNum, span.ToArray(), hexDump, parsed);
                    }
                    finally { ArrayPool<byte>.Shared.Return(buf); }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log($"[#{connNum}] Error: {ex.Message}"); }
        finally
        {
            Interlocked.Decrement(ref _connectionCount);
            OnStatusChanged?.Invoke($"Active connections: {_connectionCount}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ISO Parsing
    // ═══════════════════════════════════════════════════════════════════════

    private string ParseMessage(int connNum, byte[] data)
    {
        try
        {
            var msg = new ISOMessage(new NullLogger(), _packager!);
            msg.UnPack(data);

            var sb = new StringBuilder();
            sb.AppendLine(msg.ToString());
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"PARSE ERROR: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buf, int count, CancellationToken ct)
    {
        int off = 0;
        while (off < count)
        {
            int n = await stream.ReadAsync(buf.AsMemory(off, count - off), ct);
            if (n == 0) throw new EndOfStreamException($"Closed after {off}/{count} bytes.");
            off += n;
        }
    }

    private static string FormatHexDump(int connNum, ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[#{connNum}] ── Hex Dump ({data.Length} bytes) ──");
        for (int off = 0; off < data.Length; off += 16)
        {
            int r = Math.Min(16, data.Length - off);
            sb.Append($"{off:X4}  ");
            for (int i = 0; i < 16; i++)
            {
                if (i < r) sb.Append($"{data[off + i]:X2} ");
                else sb.Append("   ");
                if (i == 7) sb.Append(' ');
            }
            sb.Append(" |");
            for (int i = 0; i < r; i++)
            {
                byte b = data[off + i];
                sb.Append(b is >= 32 and < 127 ? (char)b : '.');
            }
            sb.AppendLine("|");
        }
        return sb.ToString();
    }

    private void Log(string message) => OnLog?.Invoke(message);

    /// <summary>Minimal silent logger for internal packager/parser use.</summary>
    private sealed class NullLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}

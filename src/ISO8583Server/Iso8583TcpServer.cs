using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
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

    /// <summary>Tracks active client connections (connNum → metadata + stream).</summary>
    private readonly ConcurrentDictionary<int, ClientConnection> _connections = new();

    public bool IsRunning => _listener != null;
    public int ConnectionCount => _connections.Count;
    public int SignOnIntervalSeconds { get; set; }
    public bool SendSignOnOnConnect { get; set; }
    public bool EnablePeriodicSignOn { get; set; }
    public TlsOptions Tls { get; set; } = new();
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

        // ── Log supported message types ─────────────────────────────────
        LogMessageTypes();

        // ── Load TLS certificate ───────────────────────────────────────
        if (Tls.IsEnabled)
        {
            Log($"Loading TLS certificate: {Tls.CertPath}");
            Tls.LoadCertificate();
            Log($"TLS enabled. Client cert required: {Tls.RequireClientCert}");
        }

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

    public async Task SendSignOnAsync(CancellationToken ct = default)
    {
        Log("─── Manual SignOn Request (all connections) ───");
        var tasks = _connections.Select(kvp =>
            SendSignOnRequestToStreamAsync(kvp.Value.Stream, kvp.Key, "801", "SignOn", ct));
        await Task.WhenAll(tasks);
    }

    public async Task SendEchoAsync(CancellationToken ct = default)
    {
        Log("─── Manual Echo Message (all connections) ───");
        var tasks = _connections.Select(kvp =>
            SendSignOnRequestToStreamAsync(kvp.Value.Stream, kvp.Key, "831", "Echo", ct));
        await Task.WhenAll(tasks);
    }

    public async Task SendSignOffAsync(bool disconnectAfter = false, CancellationToken ct = default)
    {
        Log("─── Manual SignOff Request (all connections) ───");
        var tasks = _connections.Select(kvp =>
            SendSignOnRequestToStreamAsync(kvp.Value.Stream, kvp.Key, "803", "SignOff", ct));
        await Task.WhenAll(tasks);

        if (disconnectAfter)
        {
            Log("─── Disconnecting all clients per SignOff ───");
            await StopAsync();
        }
    }

    public IReadOnlyList<(int ConnNum, string RemoteEndpoint, DateTime ConnectedAt)> GetConnections()
    {
        return _connections.Select(kvp =>
            (kvp.Key, kvp.Value.RemoteEndpoint, kvp.Value.ConnectedAt)).ToList();
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
        Stream? stream = null;
        try
        {
            stream = WrapWithTls(client, connNum);
            if (stream == null) return;

            string remoteEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            var connInfo = new ClientConnection(stream, remoteEndpoint);
            _connections.TryAdd(connNum, connInfo);

            try
            {
                var lengthBuf = new byte[LengthPrefixSize];
                DateTime lastSignOn = DateTime.MinValue;

                // ── Send initial SignOn on connect ───────────────────
                if (SendSignOnOnConnect)
                {
                    await SendInitialSignOnAsync(stream, connNum, ct);
                    lastSignOn = DateTime.UtcNow;
                }

                while (!ct.IsCancellationRequested)
                {
                    // ── Periodic Echo sender ─────────────────────────
                    if (EnablePeriodicSignOn &&
                        SignOnIntervalSeconds > 0 &&
                        (DateTime.UtcNow - lastSignOn).TotalSeconds >= SignOnIntervalSeconds)
                    {
                        await SendEchoToStreamAsync(stream, connNum, ct);
                        lastSignOn = DateTime.UtcNow;
                    }

                    // ── Read next message (1s timeout to check SignOn) ─
                    bool hasMsg;
                    try
                    {
                        using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, readCts.Token);
                        await ReadExactlyAsync(stream, lengthBuf, LengthPrefixSize, linked.Token);
                        hasMsg = true;
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        hasMsg = false;
                    }
                    catch (EndOfStreamException) { Log($"[#{connNum}] Disconnected."); return; }

                    if (!hasMsg) continue;

                    int msgLen = (lengthBuf[0] << 8) | lengthBuf[1];

                    if (msgLen <= 0)
                    {
                        // LI=0 is a common ISO8583/TCP keepalive/heartbeat — silently ignore
                        continue;
                    }

                    if (msgLen > MaxMessageSize)
                    {
                        Log($"[#{connNum}] LI bytes: 0x{lengthBuf[0]:X2}{lengthBuf[1]:X2} → {msgLen} bytes expected");
                        Log($"[#{connNum}] ⚠ Invalid length: {msgLen} (0x{ISOUtils.Bytes2Hex(lengthBuf, LengthPrefixSize)}), skipping...");

                        // Skip the oversized payload to stay in sync (capped to prevent DoS)
                        int toSkip = Math.Min(msgLen, MaxMessageSize);
                        var skipBuf = ArrayPool<byte>.Shared.Rent(toSkip);
                        try { await ReadExactlyAsync(stream, skipBuf, toSkip, ct); }
                        catch (EndOfStreamException) { Log($"[#{connNum}] Disconnected during skip."); return; }
                        finally { ArrayPool<byte>.Shared.Return(skipBuf); }
                        Log($"[#{connNum}] Skipped {toSkip} oversized bytes.");
                        continue;
                    }

                    byte[] buf = ArrayPool<byte>.Shared.Rent(msgLen);
                    try
                    {
                        await ReadExactlyAsync(stream, buf, msgLen, ct);

                        Log($"[#{connNum}] Received {msgLen} bytes (LI=0x{lengthBuf[0]:X2}{lengthBuf[1]:X2})");
                        string hexDump = FormatHexDumpWithLI(connNum, lengthBuf, buf.AsSpan(0, msgLen));
                        Log(hexDump);

                        // ── Diagnostic: check if more data is immediately available ──
                        try
                        {
                            using var peekCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));
                            var peekBuf = new byte[1];
                            int extra = await stream.ReadAsync(peekBuf, 0, 1, peekCts.Token);
                            if (extra > 0)
                                Log($"[#{connNum}] ⚠ EXTRA DATA available after reading {msgLen} bytes (at least 1 more byte: 0x{peekBuf[0]:X2})");
                        }
                        catch (OperationCanceledException) { /* no extra data — expected */ }

                        var span = buf.AsSpan(0, msgLen);

                        var (parsed, msg) = ParseMessage(connNum, span.ToArray());
                        Log($"[#{connNum}] ── Parsed Message ──\n{parsed}");

                        OnMessageParsed?.Invoke(connNum, span.ToArray(), hexDump, parsed);

                        // ── Auto-respond to SignOn (MTI 1800) ───────────
                        if (msg != null && msg.GetFieldValue(0) == "1800")
                        {
                            await SendSignOnResponse(stream, connNum, msg, ct);
                        }
                    }
                    finally { ArrayPool<byte>.Shared.Return(buf); }
                }
            }
            finally
            {
                _connections.TryRemove(connNum, out _);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log($"[#{connNum}] Error: {ex.Message}"); }
        finally
        {
            stream?.Dispose();
            client.Dispose();
            Interlocked.Decrement(ref _connectionCount);
            OnStatusChanged?.Invoke($"Active connections: {_connectionCount}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ISO Parsing
    // ═══════════════════════════════════════════════════════════════════════

    private (string dump, ISOMessage? msg) ParseMessage(int connNum, byte[] data)
    {
        try
        {
            var msg = new ISOMessage(new NullLogger(), _packager!);
            msg.UnPack(data);

            var sb = new StringBuilder();
            sb.AppendLine(msg.ToString());
            return (sb.ToString(), msg);
        }
        catch (Exception ex)
        {
            return ($"PARSE ERROR: {ex.Message}", null);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  SignOn Response
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Builds and sends a SignOn response (MTI 1814, Field 39 = "00")
    /// by copying all fields from the incoming SignOn request.
    /// </summary>
    private async Task SendSignOnResponse(
        Stream stream, int connNum, ISOMessage request, CancellationToken ct)
    {
        try
        {
            Log($"[#{connNum}] Building SignOn response (1800 → 1814)...");

            // Copy incoming message and modify MTI + response code
            request.Set(0, "1814");
            request.Set(39, "000");

            byte[] responseBytes = request.Pack();

            Log($"[#{connNum}] SignOn response content:");
            Log(request.ToString());

            Log($"[#{connNum}] Sending SignOn response ({responseBytes.Length} bytes, LI=0x{responseBytes.Length:X4})...");

            // Prepend 2-byte big-endian length prefix and send
            var framed = new byte[LengthPrefixSize + responseBytes.Length];
            framed[0] = (byte)(responseBytes.Length >> 8);
            framed[1] = (byte)(responseBytes.Length & 0xFF);
            Array.Copy(responseBytes, 0, framed, LengthPrefixSize, responseBytes.Length);

            Log(FormatHexDump(connNum, framed));

            await stream.WriteAsync(framed, 0, framed.Length, ct);
            await stream.FlushAsync(ct);

            Log($"[#{connNum}] SignOn response sent.");
        }
        catch (Exception ex)
        {
            Log($"[#{connNum}] Failed to send SignOn response: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Periodic SignOn
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sends a SignOn request (MTI 1800, F24=801) on initial connect.
    /// </summary>
    private async Task SendInitialSignOnAsync(Stream stream, int connNum, CancellationToken ct)
    {
        await SendSignOnRequestToStreamAsync(stream, connNum, "801", "SignOn (Initial)", ct);
    }

    /// <summary>
    /// Sends an Echo message (MTI 1800, F24=831) to a specific stream.
    /// </summary>
    private async Task SendEchoToStreamAsync(Stream stream, int connNum, CancellationToken ct)
    {
        await SendSignOnRequestToStreamAsync(stream, connNum, "831", "Echo (Periodic)", ct);
    }

    /// <summary>
    /// Builds, frames, and sends a SignOn-style request (MTI 1800) to a stream.
    /// </summary>
    private async Task SendSignOnRequestToStreamAsync(
        Stream stream, int connNum, string f24Value, string label, CancellationToken ct)
    {
        try
        {
            Log($"[#{connNum}] ── {label} ──");

            var request = BuildSignOnRequest(connNum, f24Value);
            Log($"[#{connNum}] {label} content:");
            Log(request.ToString());

            byte[] reqBytes = request.Pack();

            var framed = new byte[LengthPrefixSize + reqBytes.Length];
            framed[0] = (byte)(reqBytes.Length >> 8);
            framed[1] = (byte)(reqBytes.Length & 0xFF);
            Array.Copy(reqBytes, 0, framed, LengthPrefixSize, reqBytes.Length);

            Log($"[#{connNum}] Sending {label} ({reqBytes.Length} bytes, LI=0x{reqBytes.Length:X4})...");
            Log(FormatHexDump(connNum, framed));

            await stream.WriteAsync(framed, 0, framed.Length, ct);
            await stream.FlushAsync(ct);
        }
        catch (Exception ex)
        {
            Log($"[#{connNum}] {label} send failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds a standard SignOn request (MTI 1800) using the loaded dialect.
    /// </summary>
    private ISOMessage BuildSignOnRequest(int connNum, string f24Value = "801")
    {
        var msg = new ISOMessage(new NullLogger(), _packager!);
        msg.Set(0, "1800");
        msg.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));
        msg.Set(11, $"{connNum:D6}");
        msg.Set(24, f24Value);
        return msg;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  TLS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wraps the client's TCP stream in an SslStream if TLS is enabled.
    /// Returns the stream to use (raw NetworkStream or SslStream), or null if auth fails.
    /// </summary>
    private Stream? WrapWithTls(TcpClient client, int connNum)
    {
        var networkStream = client.GetStream();

        if (!Tls.IsEnabled || Tls.Certificate == null)
            return networkStream;

        var sslStream = new SslStream(networkStream, false, ValidateClientCertificate);

        try
        {
            var sslOptions = new SslServerAuthenticationOptions
            {
                ServerCertificate = Tls.Certificate,
                ClientCertificateRequired = Tls.RequireClientCert,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                // Require mutual TLS for client cert validation
                RemoteCertificateValidationCallback = ValidateClientCertificate
            };

            sslStream.AuthenticateAsServerAsync(sslOptions).GetAwaiter().GetResult();

            Log($"[#{connNum}] TLS authenticated. Cipher: {sslStream.CipherAlgorithm}");
            return sslStream;
        }
        catch (Exception ex)
        {
            Log($"[#{connNum}] TLS handshake failed: {ex.Message}");
            sslStream.Dispose();
            client.Close();
            return null;
        }
    }

    private bool ValidateClientCertificate(
        object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors)
    {
        if (!Tls.RequireClientCert)
            return true; // client cert not required — accept any

        if (cert == null)
            return false;

        // If CA cert is configured, validate against it using custom trust
        if (Tls.CaCertificate != null && chain != null)
        {
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(Tls.CaCertificate);
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

            bool built = chain.Build(new X509Certificate2(cert));
            if (!built)
            {
                // Check if the only error was untrusted root (we used custom trust)
                var chainErrors = chain.ChainStatus
                    .Where(s => s.Status != X509ChainStatusFlags.UntrustedRoot)
                    .ToArray();
                return chainErrors.Length == 0;
            }
            return true;
        }

        return errors == SslPolicyErrors.None;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private static async Task ReadExactlyAsync(Stream stream, byte[] buf, int count, CancellationToken ct)
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
        return FormatHexDumpImpl(connNum, data, hasLI: false);
    }

    private static string FormatHexDumpWithLI(int connNum, ReadOnlySpan<byte> li, ReadOnlySpan<byte> data)
    {
        // Combine LI + data into a single buffer for the dump
        Span<byte> combined = stackalloc byte[li.Length + data.Length];
        li.CopyTo(combined);
        data.CopyTo(combined.Slice(li.Length));
        return FormatHexDumpImpl(connNum, combined, hasLI: true);
    }

    private static string FormatHexDumpImpl(int connNum, ReadOnlySpan<byte> data, bool hasLI)
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
                // Mark LI bytes with a different indicator
                if (hasLI && off + i < 2)
                    sb.Append(b is >= 32 and < 127 ? (char)b : '·');
                else
                    sb.Append(b is >= 32 and < 127 ? (char)b : '.');
            }
            sb.AppendLine("|");
        }
        if (hasLI) sb.AppendLine($"        LI = 0x{data[0]:X2}{data[1]:X2} = {((data[0] << 8) | data[1])}");
        return sb.ToString();
    }

    private void Log(string message) => OnLog?.Invoke(message);

    // ═══════════════════════════════════════════════════════════════════════
    //  Dialect Info Logging
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Logs the supported message types (grouped by Req/Resp pairs) and total fields.
    /// </summary>
    private void LogMessageTypes()
    {
        if (_packager == null) return;

        var msgTypesPackager = _packager.GetISOMessageFieldsPackager()?.GetMessageTypesPackager();
        if (msgTypesPackager == null) return;

        var msgTypes = msgTypesPackager.GetMessageTypes().ToList();
        if (msgTypes.Count == 0)
        {
            Log("No message types defined in dialect.");
            return;
        }

        Log("─── Supported Message Types ───");
        Log($"  Total: {msgTypes.Count} message type(s)");
        Log($"  Total fields: {_packager.GetTotalFields()}");
        Log("");

        // Group by first 3 digits to find Req/Resp pairs
        // ISO 8583 convention: xxy0=Request, xxy1=Response, xxy2=Advice, xxy3=AdviceResponse, etc.
        var groups = msgTypes
            .GroupBy(mt => mt.messageTypeIdentifier.Length >= 4
                ? mt.messageTypeIdentifier[..3]
                : mt.messageTypeIdentifier)
            .OrderBy(g => g.Key);

        var printed = new HashSet<string>();

        foreach (var group in groups)
        {
            var sorted = group.OrderBy(mt => mt.messageTypeIdentifier).ToList();

            if (sorted.Count == 1)
            {
                var mt = sorted[0];
                Log($"  {mt.messageTypeIdentifier}: {mt.messageTypeName}" +
                    (string.IsNullOrWhiteSpace(mt.messageTypeDescription) ? "" : $" ({mt.messageTypeDescription})"));
            }
            else
            {
                // Print pairs: e.g. 0100 ↔ 0110
                for (int i = 0; i < sorted.Count; i += 2)
                {
                    if (i + 1 < sorted.Count)
                    {
                        var req = sorted[i];
                        var resp = sorted[i + 1];
                        Log($"  {req.messageTypeIdentifier}: {req.messageTypeName.PadRight(28)} ↔  {resp.messageTypeIdentifier}: {resp.messageTypeName}");
                    }
                    else
                    {
                        var mt = sorted[i];
                        Log($"  {mt.messageTypeIdentifier}: {mt.messageTypeName}" +
                            (string.IsNullOrWhiteSpace(mt.messageTypeDescription) ? "" : $" ({mt.messageTypeDescription})"));
                    }
                }
            }
        }

        Log("");
    }

    /// <summary>Holds per-connection metadata and the stream for sending messages.</summary>
    private sealed class ClientConnection
    {
        public Stream Stream { get; }
        public string RemoteEndpoint { get; }
        public DateTime ConnectedAt { get; }

        public ClientConnection(Stream stream, string remoteEndpoint)
        {
            Stream = stream;
            RemoteEndpoint = remoteEndpoint;
            ConnectedAt = DateTime.UtcNow;
        }
    }

    /// <summary>Minimal silent logger for internal packager/parser use.</summary>
    private sealed class NullLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}

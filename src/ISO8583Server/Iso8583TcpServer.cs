using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Message;
using ISO8583Net.Packager;
using ISO8583Net.Server.Pipeline;
using ISO8583Net.Server.Pipeline.Messages;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Server;

/// <summary>
/// TCP server that listens for ISO 8583 messages with 2-byte big-endian length prefix.
/// Parses messages using the configured dialect and reports results via callbacks.
/// </summary>
public sealed class Iso8583TcpServer : IIso8583Server
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private ISOMessagePackager? _packager;
    private readonly PipelineHost _pipelineHost;

    public Iso8583TcpServer(PipelineHost pipelineHost)
    {
        _pipelineHost = pipelineHost;
    }
    public bool IsRunning => _listener != null;
    public int ConnectionCount => _pipelineHost.ConnectionCount;
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

        // ── Initialize pipeline host with packager ─────────────────────
        _pipelineHost.SetPackager(_packager!);

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

        if (_pipelineHost != null)
        {
            Log("Draining pipelines...");
            await _pipelineHost.StopAllAsync();
        }

        Log("Server stopped.");
        OnStatusChanged?.Invoke("Idle");
    }

    public async Task SendSignOnAsync(CancellationToken ct = default)
    {
        Log("─── Manual SignOn Request (all connections) ───");
        await _pipelineHost.BroadcastSignOnRequestAsync("801", ct);
    }

    public async Task SendEchoAsync(CancellationToken ct = default)
    {
        Log("─── Manual Echo Message (all connections) ───");
        await _pipelineHost.BroadcastSignOnRequestAsync("831", ct);
    }

    public async Task SendSignOffAsync(bool disconnectAfter = false, CancellationToken ct = default)
    {
        Log("─── Manual SignOff Request (all connections) ───");
        await _pipelineHost.BroadcastSignOnRequestAsync("803", ct);

        if (disconnectAfter)
        {
            Log("─── Disconnecting all clients per SignOff ───");
            await StopAsync();
        }
    }

    public IReadOnlyList<(int ConnNum, string RemoteEndpoint, DateTime ConnectedAt)> GetConnections()
    {
        return _pipelineHost.GetStats()
            .Select(s => (s.ConnectionNumber, s.RemoteEndpoint, s.ConnectedAt))
            .ToList();
    }

    // ═══════════════════════════════════════════════════════════════════════

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        int connectionCount = 0;
        try
        {
            while (!ct.IsCancellationRequested && _listener != null)
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                int connNum = Interlocked.Increment(ref connectionCount);

                Log($"[#{connNum}] Client connected: {client.Client.RemoteEndPoint}");
                OnStatusChanged?.Invoke($"Active connections: {_pipelineHost.ConnectionCount}");

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

            try
            {
                // ── Delegate to pipeline host ────────────────────────
                var pipeline = _pipelineHost.Accept(stream, connNum, remoteEndpoint, ct);

                // ── Send initial SignOn on connect ───────────────────
                if (SendSignOnOnConnect)
                {
                    var signOnMsg = _pipelineHost.BuildSignOnMessage(connNum, "801");
                    var outbound = OutboundMessage.FromISOMessage(signOnMsg, connNum);
                    await pipeline.SendAsync(outbound, ct);
                }

                // Wait until reader exits (client disconnects)
                await pipeline.WaitForCloseAsync();
            }
            finally
            {
                _pipelineHost.Remove(connNum);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log($"[#{connNum}] Error: {ex.Message}"); }
        finally
        {
            stream?.Dispose();
            client.Dispose();
            OnStatusChanged?.Invoke($"Active connections: {_pipelineHost.ConnectionCount}");
        }
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
    /// <summary>Minimal silent logger for internal packager/parser use.</summary>
    private sealed class NullLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}

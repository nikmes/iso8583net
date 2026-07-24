using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using ISO8583Net.Message;
using ISO8583Net.Packager;
using ISO8583Net.Simulator.Framing;
using ISO8583Net.Simulator.Hubs;
using ISO8583Net.Simulator.Models;
using ISO8583Net.Simulator.Scenarios;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ISO8583Net.Simulator;

/// <summary>
/// Per-connection session that manages a single TCP/TLS connection to an ISO8583Server.
/// Handles connect, disconnect, message send/receive with STAN-based correlation,
/// and background frame reading.
/// </summary>
public sealed class SimulatorSession : IAsyncDisposable
{
    private readonly SimulatorOptions _options;
    private readonly ISOMessagePackager _packager;
    private readonly ILogger<SimulatorSession> _logger;
    private readonly SimulatorStats _stats;
    private readonly FrameWriter _frameWriter;
    private readonly ResponseMatcher _matcher;
    private readonly IHubContext<SimulatorHub>? _hubContext;
    private readonly MessageHistory? _history;

    private TcpClient? _tcpClient;
    private Stream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _readerTask;
    private Task? _statsTimerTask;
    private CancellationTokenSource? _statsTimerCts;
    private FrameReader? _frameReader;
    private SimulatorState _state = SimulatorState.Disconnected;
    private readonly object _stateLock = new();
    private DateTime _connectedAt;
    private readonly Stopwatch _uptimeStopwatch = new();

    public SimulatorStats Stats => _stats;

    public DateTime? ConnectedAt => State >= SimulatorState.Connected ? _connectedAt : null;
    public double UptimeSeconds => _uptimeStopwatch.Elapsed.TotalSeconds;

    public SimulatorState State
    {
        get { lock (_stateLock) return _state; }
        private set
        {
            lock (_stateLock)
            {
                if (_state != value)
                {
                    var oldState = _state;
                    _state = value;
                    OnStateChanged?.Invoke(oldState, value);
                    _ = NotifyStateChangedAsync(oldState, value);
                }
            }
        }
    }

    /// <summary>Fired when the connection state changes.</summary>
    public event Action<SimulatorState, SimulatorState>? OnStateChanged;

    public SimulatorSession(
        SimulatorOptions options,
        ISOMessagePackager packager,
        ILogger<SimulatorSession> logger,
        IHubContext<SimulatorHub>? hubContext = null,
        MessageHistory? history = null)
    {
        _options = options;
        _packager = packager;
        _logger = logger;
        _hubContext = hubContext;
        _history = history;
        _stats = new SimulatorStats();
        _frameWriter = new FrameWriter();
        _matcher = new ResponseMatcher();
    }

    /// <summary>The ISO 8583 message packager shared by this session.</summary>
    public ISOMessagePackager Packager => _packager;

    /// <summary>Create a new <see cref="ISOMessage"/> pre-configured with this session's packager.</summary>
    public ISOMessage CreateMessage()
    {
        return new ISOMessage(
            _logger as ILogger ?? NullLoggerFactory.Instance.CreateLogger<ISOMessage>(),
            _packager);
    }

    /// <summary>
    /// Open a TCP connection, optionally upgrade to TLS, and start the background reader.
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (State != SimulatorState.Disconnected)
            throw new InvalidOperationException($"Cannot connect: session is {State}");

        State = SimulatorState.Connecting;
        _logger.LogInformation("Connecting to {Host}:{Port} (TLS={Tls})",
            _options.Host, _options.Port, _options.TlsEnabled);

        try
        {
            _tcpClient = new TcpClient();
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds));

            await _tcpClient.ConnectAsync(_options.Host, _options.Port, connectCts.Token);

            Stream stream = _tcpClient.GetStream();

            if (_options.TlsEnabled)
            {
                var sslStream = new SslStream(
                    stream,
                    leaveInnerStreamOpen: false,
                    userCertificateValidationCallback: _options.TlsAllowUntrusted
                        ? (sender, cert, chain, errors) => true  // Accept all in dev
                        : null); // Use system default validation

                var sslOptions = new SslClientAuthenticationOptions
                {
                    TargetHost = _options.Host,
                    // For production with cert path: load and use client cert if needed
                };

                await sslStream.AuthenticateAsClientAsync(sslOptions, connectCts.Token);
                stream = sslStream;
                _logger.LogDebug("TLS handshake completed");
            }

            _stream = stream;
            _cts = new CancellationTokenSource();

            // Start background reader
            _frameReader = new FrameReader(
                _logger as ILogger<FrameReader> ?? NullLoggerFactory.Instance.CreateLogger<FrameReader>(),
                OnMessageReceivedAsync);

            _readerTask = Task.Run(() =>
                _frameReader.RunAsync(_stream, _packager, _cts.Token), _cts.Token);

            _connectedAt = DateTime.UtcNow;
            _uptimeStopwatch.Restart();
            State = SimulatorState.Connected;
            _logger.LogInformation("Connected to {Host}:{Port}", _options.Host, _options.Port);

            // Start periodic stats timer
            StartStatsTimer();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection failed to {Host}:{Port}", _options.Host, _options.Port);
            State = SimulatorState.Disconnected;
            await CleanupConnectionAsync();
            throw;
        }
    }

    /// <summary>
    /// Gracefully disconnect: cancel the reader, drain pending matchers, close the socket.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (State == SimulatorState.Disconnected)
            return;

        _logger.LogInformation("Disconnecting...");
        State = SimulatorState.Disconnected;

        StopStatsTimer();
        _matcher.CancelAll();
        await CleanupConnectionAsync();
        _uptimeStopwatch.Stop();
        _logger.LogInformation("Disconnected");
    }

    /// <summary>
    /// Send a request message for the given MTI and await the response.
    /// Looks up the MTI in the MessageBuilderRegistry, builds the request,
    /// registers a TCS by STAN, writes the frame, and awaits the response.
    /// For advice MTIs (fire-and-forget), returns null after sending.
    /// </summary>
    public async Task<ISOMessage?> SendAsync(string mti, CancellationToken ct = default)
    {
        if (State < SimulatorState.Connected)
            throw new InvalidOperationException($"Cannot send: session is {State}");

        if (_stream == null || _cts == null)
            throw new InvalidOperationException("Stream is not available");

        // Create a new ISOMessage, set MTI in F0
        var message = new ISOMessage(
            _logger as ILogger ?? NullLoggerFactory.Instance.CreateLogger<ISOMessage>(),
            _packager);
        message.Set(0, mti);

        // Generate a simple STAN for correlation (will be replaced by builders in Sprint 2)
        var stan = GenerateStan();
        message.Set(11, stan);

        // Set transmission date/time
        message.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));

        var sw = Stopwatch.StartNew();

        // Determine if this is a request (await response) or advice (fire-and-forget)
        bool isAdvice = mti.EndsWith("20") && mti != "0820"; // 0120, 0220, 0420 are advices

        Task<ISOMessage>? responseTask = null;
        CancellationTokenSource? timeoutCts = null;
        try
        {
            if (!isAdvice)
            {
                // Register for response
                timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.ResponseTimeoutSeconds));
                responseTask = _matcher.RegisterAsync(stan, timeoutCts.Token);
            }

            // Write the frame
            await _frameWriter.WriteAsync(_stream, message, ct);
            _stats.IncrementMessagesSent();

            if (isAdvice)
            {
                _logger.LogDebug("Advice {Mti} STAN={Stan} sent (fire-and-forget)", mti, stan);
                return null;
            }

            // Await the response
            var response = await responseTask!;
            sw.Stop();
            _stats.IncrementResponsesReceived();
            _stats.RecordLatencyMs(sw.Elapsed.TotalMilliseconds);
            _logger.LogDebug("Response for {Mti} STAN={Stan} received in {Elapsed:F2}ms",
                mti, stan, sw.Elapsed.TotalMilliseconds);
            return response;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _stats.IncrementErrors();
            _logger.LogWarning("Timeout waiting for response to {Mti} STAN={Stan}", mti, stan);
            throw new TimeoutException($"Timeout waiting for response to STAN {stan}");
        }
        finally
        {
            timeoutCts?.Dispose();
        }
    }

    /// <summary>
    /// Send an advice message (fire-and-forget, no response expected).
    /// </summary>
    public Task SendAdviceAsync(string mti, CancellationToken ct = default)
    {
        // Advices already handled in SendAsync; this is a convenience alias
        return SendAsync(mti, ct);
    }

    /// <summary>
    /// Send a pre-built <see cref="ISOMessage"/> (e.g. from a <see cref="IMessageBuilder"/>).
    /// Reads MTI from F0 and STAN from F11. Returns the response message for request MTIs,
    /// or null for advice/fire-and-forget MTIs.
    /// </summary>
    public async Task<ISOMessage?> SendMessageAsync(ISOMessage message, CancellationToken ct = default)
    {
        if (State < SimulatorState.Connected)
            throw new InvalidOperationException($"Cannot send: session is {State}");

        if (_stream == null || _cts == null)
            throw new InvalidOperationException("Stream is not available");

        var mti = message.GetFieldValue(0);
        var stan = message.GetFieldValue(11);
        ArgumentException.ThrowIfNullOrEmpty(mti);
        ArgumentException.ThrowIfNullOrEmpty(stan);

        var sw = Stopwatch.StartNew();
        bool isAdvice = mti.EndsWith("20") && mti != "0820";

        Task<ISOMessage>? responseTask = null;
        CancellationTokenSource? timeoutCts = null;
        try
        {
            if (!isAdvice)
            {
                timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.ResponseTimeoutSeconds));
                responseTask = _matcher.RegisterAsync(stan, timeoutCts.Token);
            }

            await _frameWriter.WriteAsync(_stream, message, ct);
            _stats.IncrementMessagesSent();

            // Fire MessageSent event
            _ = NotifyMessageSentAsync(mti, stan);

            if (isAdvice)
            {
                _logger.LogDebug("Advice {Mti} STAN={Stan} sent (fire-and-forget)", mti, stan);
                return null;
            }

            var response = await responseTask!;
            sw.Stop();
            _stats.IncrementResponsesReceived();
            _stats.RecordLatencyMs(sw.Elapsed.TotalMilliseconds);
            _logger.LogDebug("Response for {Mti} STAN={Stan} received in {Elapsed:F2}ms",
                mti, stan, sw.Elapsed.TotalMilliseconds);

            // Fire ResponseReceived event
            var responseMti = response.GetFieldValue(0);
            var f39 = response.GetFieldValue(39);
            _ = NotifyResponseReceivedAsync(mti, responseMti, stan, f39, sw.Elapsed.TotalMilliseconds);

            // Add to history
            _history?.Add(new MessageTrace
            {
                Timestamp = DateTime.UtcNow,
                RequestMti = mti,
                ResponseMti = responseMti,
                Stan = stan,
                F39 = f39,
                ElapsedMs = sw.Elapsed.TotalMilliseconds
            });

            return response;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _stats.IncrementErrors();
            _logger.LogWarning("Timeout waiting for response to {Mti} STAN={Stan}", mti, stan);
            _ = NotifyErrorOccurredAsync(stan, "Timeout", $"Timeout waiting for response to STAN {stan}");
            throw new TimeoutException($"Timeout waiting for response to STAN {stan}");
        }
        finally
        {
            timeoutCts?.Dispose();
        }
    }

    /// <summary>
    /// Called by the FrameReader for each received message.
    /// Extracts STAN from field 11 and completes the matching TCS.
    /// </summary>
    private Task OnMessageReceivedAsync(ISOMessage message)
    {
        try
        {
            var stan = message.GetFieldValue(11);
            if (!string.IsNullOrEmpty(stan))
            {
                if (!_matcher.TryComplete(stan, message))
                {
                    _logger.LogDebug("Unsolicited response for STAN={Stan} (already timed out or unexpected)", stan);
                }
            }
            else
            {
                _logger.LogWarning("Received message without STAN (F11)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching received message");
        }
        return Task.CompletedTask;
    }

    private async Task CleanupConnectionAsync()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
        catch { /* ignore */ }

        if (_readerTask != null)
        {
            try { await _readerTask; }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _logger.LogDebug(ex, "Reader task ended with error"); }
            _readerTask = null;
        }

        _frameReader = null;

        if (_stream != null)
        {
#if NET10_0_OR_GREATER
            await _stream.DisposeAsync();
#else
            _stream.Dispose();
#endif
            _stream = null;
        }

        _tcpClient?.Dispose();
        _tcpClient = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    private static string GenerateStan()
    {
        var stan = (uint)(DateTime.UtcNow.Ticks % 1_000_000);
        return stan.ToString("D6");
    }

    // ── SignalR event helpers ─────────────────────────────────

    private async Task NotifyMessageSentAsync(string mti, string stan)
    {
        if (_hubContext is null) return;
        try
        {
            await _hubContext.Clients.All.SendAsync("MessageSent", new MessageSentDto
            {
                Mti = mti,
                Stan = stan,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to push MessageSent event");
        }
    }

    private async Task NotifyResponseReceivedAsync(
        string requestMti, string? responseMti, string stan, string? f39, double elapsedMs)
    {
        if (_hubContext is null) return;
        try
        {
            await _hubContext.Clients.All.SendAsync("ResponseReceived", new ResponseReceivedDto
            {
                RequestMti = requestMti,
                ResponseMti = responseMti,
                Stan = stan,
                F39 = f39,
                ElapsedMs = elapsedMs
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to push ResponseReceived event");
        }
    }

    private async Task NotifyErrorOccurredAsync(string? stan, string errorType, string? message)
    {
        if (_hubContext is null) return;
        try
        {
            await _hubContext.Clients.All.SendAsync("ErrorOccurred", new ErrorOccurredDto
            {
                Stan = stan,
                ErrorType = errorType,
                Message = message
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to push ErrorOccurred event");
        }
    }

    private async Task NotifyStateChangedAsync(SimulatorState oldState, SimulatorState newState)
    {
        if (_hubContext is null) return;
        try
        {
            await _hubContext.Clients.All.SendAsync("StateChanged", new StateChangedDto
            {
                OldState = oldState.ToString(),
                NewState = newState.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to push StateChanged event");
        }
    }

    private void StartStatsTimer()
    {
        _statsTimerCts = new CancellationTokenSource();
        _statsTimerTask = Task.Run(async () =>
        {
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            try
            {
                while (await timer.WaitForNextTickAsync(_statsTimerCts.Token))
                {
                    await NotifyStatsAsync();
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    private void StopStatsTimer()
    {
        _statsTimerCts?.Cancel();
        _statsTimerCts?.Dispose();
        _statsTimerCts = null;
        _statsTimerTask = null;
    }

    private async Task NotifyStatsAsync()
    {
        if (_hubContext is null) return;
        try
        {
            await _hubContext.Clients.All.SendAsync("StatsUpdate", new StatsUpdateDto
            {
                MsgsSent = _stats.MessagesSent,
                MsgsRecv = _stats.ResponsesReceived,
                Errors = _stats.Errors,
                AvgMs = _stats.AvgLatencyMs,
                P99Ms = _stats.P99LatencyMs
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to push StatsUpdate event");
        }
    }
}

using Microsoft.AspNetCore.SignalR.Client;

namespace ISO8583Net.Simulator.Hubs;

/// <summary>
/// Typed client wrapper for consuming SimulatorHub SignalR events.
/// Manages connection lifecycle with automatic reconnect.
/// Blazor components bind to the Action events.
/// </summary>
public sealed class SimulatorHubClient : IAsyncDisposable
{
    private HubConnection? _hub;

    // ── Events that Blazor components bind to ─────────────────────

    /// <summary>Fired when a message is framed and written to the socket.</summary>
    public event Action<MessageSentDto>? OnMessageSent;

    /// <summary>Fired when a response is unpacked from the socket.</summary>
    public event Action<ResponseReceivedDto>? OnResponseReceived;

    /// <summary>Fired on parse errors, timeouts, or handler errors.</summary>
    public event Action<ErrorOccurredDto>? OnErrorOccurred;

    /// <summary>Fired on each step in a scenario.</summary>
    public event Action<ScenarioProgressDto>? OnScenarioProgress;

    /// <summary>Fired when a scenario completes.</summary>
    public event Action<ScenarioCompletedDto>? OnScenarioCompleted;

    /// <summary>Periodic load test progress (every 500ms).</summary>
    public event Action<LoadTestProgressDto>? OnLoadTestProgress;

    /// <summary>Fired on connect/disconnect/error state transitions.</summary>
    public event Action<StateChangedDto>? OnStateChanged;

    /// <summary>Periodic stats snapshot (every 2s).</summary>
    public event Action<StatsUpdateDto>? OnStatsUpdate;

    /// <summary>
    /// Start the SignalR connection with automatic reconnect.
    /// </summary>
    public async Task StartAsync(string hubUrl)
    {
        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hub.On<MessageSentDto>("MessageSent", d => OnMessageSent?.Invoke(d));
        _hub.On<ResponseReceivedDto>("ResponseReceived", d => OnResponseReceived?.Invoke(d));
        _hub.On<ErrorOccurredDto>("ErrorOccurred", d => OnErrorOccurred?.Invoke(d));
        _hub.On<ScenarioProgressDto>("ScenarioProgress", d => OnScenarioProgress?.Invoke(d));
        _hub.On<ScenarioCompletedDto>("ScenarioCompleted", d => OnScenarioCompleted?.Invoke(d));
        _hub.On<LoadTestProgressDto>("LoadTestProgress", d => OnLoadTestProgress?.Invoke(d));
        _hub.On<StateChangedDto>("StateChanged", d => OnStateChanged?.Invoke(d));
        _hub.On<StatsUpdateDto>("StatsUpdate", d => OnStatsUpdate?.Invoke(d));

        await _hub.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub != null)
        {
            await _hub.DisposeAsync();
        }
    }
}

// ── SignalR event DTOs ───────────────────────────────────────────

public sealed class MessageSentDto
{
    public string Mti { get; set; } = string.Empty;
    public string? Stan { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Hex { get; set; }
}

public sealed class ResponseReceivedDto
{
    public string RequestMti { get; set; } = string.Empty;
    public string? ResponseMti { get; set; }
    public string? Stan { get; set; }
    public string? F39 { get; set; }
    public double ElapsedMs { get; set; }
    public string? Hex { get; set; }
}

public sealed class ErrorOccurredDto
{
    public string? Stan { get; set; }
    public string ErrorType { get; set; } = string.Empty;
    public string? Message { get; set; }
}

public sealed class ScenarioProgressDto
{
    public string ScenarioName { get; set; } = string.Empty;
    public int Step { get; set; }
    public int TotalSteps { get; set; }
    public string? StepDescription { get; set; }
}

public sealed class ScenarioCompletedDto
{
    public string ScenarioName { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public double DurationMs { get; set; }
}

public sealed class LoadTestProgressDto
{
    public string LoadTestId { get; set; } = string.Empty;
    public int SentCount { get; set; }
    public int ReceivedCount { get; set; }
    public int Errors { get; set; }
    public double AvgMs { get; set; }
}

public sealed class StateChangedDto
{
    public string OldState { get; set; } = string.Empty;
    public string NewState { get; set; } = string.Empty;
}

public sealed class StatsUpdateDto
{
    public long MsgsSent { get; set; }
    public long MsgsRecv { get; set; }
    public long Errors { get; set; }
    public double AvgMs { get; set; }
    public double P99Ms { get; set; }
}

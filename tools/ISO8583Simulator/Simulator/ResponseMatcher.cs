using System.Collections.Concurrent;
using ISO8583Net.Message;

namespace ISO8583Net.Simulator;

/// <summary>
/// Correlates incoming response messages to pending request TaskCompletionSources
/// using the STAN (System Trace Audit Number, field 11) as the correlation key.
/// Thread-safe for concurrent access by caller (register) and reader (complete).
/// </summary>
internal sealed class ResponseMatcher
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ISOMessage>> _pending = new();

    /// <summary>
    /// Register a pending request by STAN. Returns a Task that completes when the
    /// response arrives or the cancellation token fires.
    /// </summary>
    public Task<ISOMessage> RegisterAsync(string stan, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<ISOMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _pending[stan] = tcs;

        // Clean up on cancellation/timeout
        ct.Register(() =>
        {
            if (_pending.TryRemove(stan, out var removed))
                removed.TrySetCanceled(ct);
        });

        return tcs.Task;
    }

    /// <summary>
    /// Complete a pending request. Called by the FrameReader when a response arrives.
    /// Returns true if the STAN was found and the TCS was resolved; false if the
    /// response is unsolicited or the request already timed out.
    /// </summary>
    public bool TryComplete(string stan, ISOMessage response)
    {
        if (_pending.TryRemove(stan, out var tcs))
        {
            tcs.TrySetResult(response);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Cancel all pending requests. Called during graceful shutdown.
    /// </summary>
    public void CancelAll()
    {
        foreach (var (stan, tcs) in _pending)
            tcs.TrySetCanceled();
        _pending.Clear();
    }

    /// <summary>Number of pending requests awaiting response.</summary>
    public int PendingCount => _pending.Count;
}

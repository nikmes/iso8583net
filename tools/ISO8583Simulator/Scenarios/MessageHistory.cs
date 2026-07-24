using System.Collections.Concurrent;
using ISO8583Net.Simulator.Models;

namespace ISO8583Net.Simulator.Scenarios;

/// <summary>
/// Thread-safe fixed-capacity ring buffer for recent message exchanges.
/// Used by the message history endpoint and SignalR event replay.
/// </summary>
public sealed class MessageHistory
{
    private readonly int _capacity;
    private readonly ConcurrentQueue<MessageTrace> _queue = new();
    private long _nextId;

    public MessageHistory(int capacity = 10_000)
    {
        _capacity = capacity;
    }

    /// <summary>Add a message trace, evicting oldest if over capacity.</summary>
    public void Add(MessageTrace trace)
    {
        trace.Id = Interlocked.Increment(ref _nextId);
        _queue.Enqueue(trace);

        while (_queue.Count > _capacity)
            _queue.TryDequeue(out _);
    }

    /// <summary>Get recent messages, optionally filtered by MTI.</summary>
    public MessageHistoryResponse GetRecent(int count = 50, string? mti = null)
    {
        var all = _queue.Reverse().AsEnumerable();

        if (!string.IsNullOrWhiteSpace(mti))
            all = all.Where(m =>
                string.Equals(m.RequestMti, mti, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.ResponseMti, mti, StringComparison.OrdinalIgnoreCase));

        var messages = all.Take(Math.Min(count, _capacity)).ToList();
        return new MessageHistoryResponse
        {
            Messages = messages,
            Total = messages.Count
        };
    }
}

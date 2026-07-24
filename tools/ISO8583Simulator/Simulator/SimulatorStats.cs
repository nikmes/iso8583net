using System.Threading;

namespace ISO8583Net.Simulator;

/// <summary>
/// Thread-safe counters and latency histogram for the simulator session.
/// Uses Interlocked for counters and a lightweight lock for the latency ring buffer.
/// </summary>
public sealed class SimulatorStats
{
    private long _messagesSent;
    private long _responsesReceived;
    private long _errors;
    private long _totalLatencyTicks; // sum of all latencies in Stopwatch ticks

    // Fixed-size ring buffer for latency samples (last 10,000)
    private readonly double[] _latencySamples;
    private int _sampleIndex;
    private int _sampleCount;
    private readonly object _sampleLock = new();

    public SimulatorStats(int capacity = 10_000)
    {
        _latencySamples = new double[capacity];
    }

    public long MessagesSent => Interlocked.Read(ref _messagesSent);
    public long ResponsesReceived => Interlocked.Read(ref _responsesReceived);
    public long Errors => Interlocked.Read(ref _errors);

    public void IncrementMessagesSent() => Interlocked.Increment(ref _messagesSent);
    public void IncrementResponsesReceived() => Interlocked.Increment(ref _responsesReceived);
    public void IncrementErrors() => Interlocked.Increment(ref _errors);

    /// <summary>Record a latency sample in milliseconds.</summary>
    public void RecordLatencyMs(double latencyMs)
    {
        Interlocked.Add(ref _totalLatencyTicks, (long)(latencyMs * TimeSpan.TicksPerMillisecond));

        lock (_sampleLock)
        {
            _latencySamples[_sampleIndex] = latencyMs;
            _sampleIndex = (_sampleIndex + 1) % _latencySamples.Length;
            if (_sampleCount < _latencySamples.Length) _sampleCount++;
        }
    }

    public double AvgLatencyMs
    {
        get
        {
            long total = Interlocked.Read(ref _totalLatencyTicks);
            long count = Interlocked.Read(ref _responsesReceived);
            if (count == 0) return 0;
            return (total / (double)TimeSpan.TicksPerMillisecond) / count;
        }
    }

    public double P50LatencyMs => GetPercentile(0.50);
    public double P99LatencyMs => GetPercentile(0.99);

    private double GetPercentile(double percentile)
    {
        double[] snapshot;
        int count;
        lock (_sampleLock)
        {
            count = _sampleCount;
            if (count == 0) return 0;
            snapshot = new double[count];
            Array.Copy(_latencySamples, snapshot, count);
        }

        Array.Sort(snapshot);
        int index = (int)Math.Ceiling(percentile * count) - 1;
        if (index < 0) index = 0;
        if (index >= count) index = count - 1;
        return snapshot[index];
    }

    public double ThroughputMsgPerSec
    {
        get
        {
            // Simple throughput: messages sent since last reset / uptime
            // The SimulatorSession will calculate this based on its own uptime
            return 0; // placeholder — calculated externally
        }
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _messagesSent, 0);
        Interlocked.Exchange(ref _responsesReceived, 0);
        Interlocked.Exchange(ref _errors, 0);
        Interlocked.Exchange(ref _totalLatencyTicks, 0);
        lock (_sampleLock)
        {
            Array.Clear(_latencySamples, 0, _latencySamples.Length);
            _sampleIndex = 0;
            _sampleCount = 0;
        }
    }
}

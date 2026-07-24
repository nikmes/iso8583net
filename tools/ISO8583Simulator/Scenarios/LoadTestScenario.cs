using System.Collections.Concurrent;
using System.Diagnostics;
using ISO8583Net.Simulator.Builders;
using ISO8583Net.Simulator.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Simulator.Scenarios;

/// <summary>
/// Load test scenario: sends <see cref="TotalRequests"/> messages at
/// <see cref="Concurrency"/> parallel workers, collecting latency percentiles.
/// Passes if P99 latency is below <see cref="P99ThresholdMs"/>.
/// </summary>
public class LoadTestScenario : IScenario
{
    private readonly MessageBuilderRegistry _registry;
    private readonly ILogger<LoadTestScenario> _logger;
    private readonly IHubContext<SimulatorHub>? _hubContext;

    public LoadTestScenario(MessageBuilderRegistry registry, ILogger<LoadTestScenario> logger,
        IHubContext<SimulatorHub>? hubContext = null)
    {
        _registry = registry;
        _logger = logger;
        _hubContext = hubContext;
    }

    /// <summary>MTI to send (e.g. "1100").</summary>
    public string TargetMTI { get; set; } = "1100";

    /// <summary>Total number of requests to send.</summary>
    public int TotalRequests { get; set; } = 100;

    /// <summary>Maximum concurrent requests.</summary>
    public int Concurrency { get; set; } = 10;

    /// <summary>Pass threshold for P99 latency in milliseconds.</summary>
    public double P99ThresholdMs { get; set; } = 5_000;

    public string Name => "Load Test";

    public async Task<bool> RunAsync(SimulatorSession session, CancellationToken ct = default)
    {
        var builder = _registry.GetBuilder(TargetMTI);
        if (builder is null)
        {
            _logger.LogError("No builder registered for MTI {Mti}", TargetMTI);
            return false;
        }

        var latencies = new ConcurrentBag<double>();
        int errors = 0;
        var semaphore = new SemaphoreSlim(Concurrency);
        var tasks = new List<Task>();

        var overallSw = Stopwatch.StartNew();
        int completed = 0;

        // Progress timer — fires every ~500ms
        var loadTestId = Guid.NewGuid().ToString("N")[..8];
        using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var progressTask = Task.Run(async () =>
        {
            var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
            try
            {
                while (await timer.WaitForNextTickAsync(progressCts.Token))
                {
                    int snapCompleted = Volatile.Read(ref completed);
                    int snapErrors = Volatile.Read(ref errors);
                    double snapAvg = 0;
                    var snap = latencies.ToArray();
                    if (snap.Length > 0) snapAvg = snap.Average();
                    _ = NotifyLoadTestProgressAsync(loadTestId, snapCompleted, TotalRequests,
                        snapCompleted, snapErrors, snapAvg);
                }
            }
            catch (OperationCanceledException) { }
        });

        for (int i = 0; i < TotalRequests; i++)
        {
            await semaphore.WaitAsync(ct);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    var message = session.CreateMessage();
                    builder.BuildRequest(message);
                    var response = await session.SendMessageAsync(message, ct);

                    sw.Stop();
                    if (response is not null)
                        latencies.Add(sw.Elapsed.TotalMilliseconds);
                    else
                        Interlocked.Increment(ref errors);
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }
                finally
                {
                    Interlocked.Increment(ref completed);
                    semaphore.Release();
                }
            }, ct));
        }

        await Task.WhenAll(tasks);
        progressCts.Cancel();
        try { await progressTask; } catch { /* ignore */ }
        overallSw.Stop();

        var sortedLatencies = latencies.OrderBy(l => l).ToList();
        int count = sortedLatencies.Count;

        double avg = count > 0 ? sortedLatencies.Average() : 0;
        double p50 = Percentile(sortedLatencies, 0.50);
        double p99 = Percentile(sortedLatencies, 0.99);

        _logger.LogInformation(
            "Load Test: {Total} requests, {Errors} errors, " +
            "Avg={Avg:F1}ms, P50={P50:F1}ms, P99={P99:F1}ms, Wall={Wall:F1}s",
            TotalRequests, errors, avg, p50, p99, overallSw.Elapsed.TotalSeconds);

        bool pass = errors == 0 && p99 <= P99ThresholdMs;
        if (!pass)
        {
            if (errors > 0)
                _logger.LogWarning("Load Test FAILED: {Errors} errors", errors);
            else
                _logger.LogWarning(
                    "Load Test FAILED: P99={P99:F1}ms exceeds threshold {Threshold:F1}ms",
                    p99, P99ThresholdMs);
        }

        return pass;
    }

    private async Task NotifyLoadTestProgressAsync(
        string loadTestId, int sentCount, int totalCount,
        int receivedCount, int errors, double avgMs)
    {
        if (_hubContext is null) return;
        try
        {
            await _hubContext.Clients.All.SendAsync("LoadTestProgress", new LoadTestProgressDto
            {
                LoadTestId = loadTestId,
                SentCount = sentCount,
                ReceivedCount = receivedCount,
                Errors = errors,
                AvgMs = avgMs
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to push LoadTestProgress event");
        }
    }

    private static double Percentile(List<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0;
        int index = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }
}

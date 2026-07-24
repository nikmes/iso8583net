namespace ISO8583Net.Simulator.Scenarios;

/// <summary>
/// Aggregate report for one or more scenario runs.
/// </summary>
public sealed class ScenarioReport
{
    /// <summary>Total number of scenarios executed.</summary>
    public int Total { get; set; }

    /// <summary>Number of scenarios that passed.</summary>
    public int Passed { get; set; }

    /// <summary>Number of scenarios that failed.</summary>
    public int Failed { get; set; }

    /// <summary>Total wall-clock duration of all scenarios.</summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>Per-scenario results, in execution order.</summary>
    public List<ScenarioResult> Results { get; init; } = new();

    /// <summary>True if all scenarios passed.</summary>
    public bool AllPassed => Failed == 0;
}

/// <summary>
/// Result of a single scenario execution.
/// </summary>
public sealed class ScenarioResult
{
    /// <summary>Name of the scenario (e.g., "Authorization (0100 → 0110)").</summary>
    public string ScenarioName { get; init; } = string.Empty;

    /// <summary>Whether all assertions in this scenario passed.</summary>
    public bool Passed { get; init; }

    /// <summary>Execution duration.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Error message if the scenario failed, null otherwise.</summary>
    public string? ErrorMessage { get; init; }
}

namespace ISO8583Net.Simulator.Scenarios;

/// <summary>
/// Contract for a named, self-contained test scenario that exercises one
/// or more ISO 8583 message flows against a connected <see cref="SimulatorSession"/>.
/// </summary>
public interface IScenario
{
    /// <summary>Human-readable scenario name (e.g. "Authorization").</summary>
    string Name { get; }

    /// <summary>
    /// Execute the scenario. Returns true if all assertions pass.
    /// </summary>
    Task<bool> RunAsync(SimulatorSession session, CancellationToken ct = default);
}

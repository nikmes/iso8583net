using ISO8583Net.Simulator.Builders;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Simulator.Scenarios;

/// <summary>Reversal scenario: sends 0400 and expects 0410 with F39="000".</summary>
public class ReversalScenario : IScenario
{
    private readonly ReversalBuilder _builder;
    private readonly ILogger<ReversalScenario> _logger;

    public ReversalScenario(ReversalBuilder builder, ILogger<ReversalScenario> logger)
    {
        _builder = builder;
        _logger = logger;
    }

    public string Name => "Reversal";

    public async Task<bool> RunAsync(SimulatorSession session, CancellationToken ct = default)
    {
        var message = session.CreateMessage();
        _builder.BuildRequest(message);
        var response = await session.SendMessageAsync(message, ct);
        if (response is null)
        {
            _logger.LogWarning("Reversal: no response received");
            return false;
        }

        var f39 = response.GetFieldValue(39);
        bool pass = f39 == "000";
        _logger.LogInformation("Reversal: F39={F39} → {Result}", f39, pass ? "PASS" : "FAIL");
        return pass;
    }
}

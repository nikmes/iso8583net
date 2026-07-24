using ISO8583Net.Simulator.Builders;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Simulator.Scenarios;

/// <summary>Financial scenario: sends 0200 and expects 0210 with F39="000".</summary>
public class FinancialScenario : IScenario
{
    private readonly FinancialBuilder _builder;
    private readonly ILogger<FinancialScenario> _logger;

    public FinancialScenario(FinancialBuilder builder, ILogger<FinancialScenario> logger)
    {
        _builder = builder;
        _logger = logger;
    }

    public string Name => "Financial";

    public async Task<bool> RunAsync(SimulatorSession session, CancellationToken ct = default)
    {
        var message = session.CreateMessage();
        _builder.BuildRequest(message);
        var response = await session.SendMessageAsync(message, ct);
        if (response is null)
        {
            _logger.LogWarning("Financial: no response received");
            return false;
        }

        var f39 = response.GetFieldValue(39);
        bool pass = f39 == "000";
        _logger.LogInformation("Financial: F39={F39} → {Result}", f39, pass ? "PASS" : "FAIL");
        return pass;
    }
}

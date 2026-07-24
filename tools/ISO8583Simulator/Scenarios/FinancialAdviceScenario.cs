using ISO8583Net.Simulator.Builders;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Simulator.Scenarios;

/// <summary>Financial Advice scenario: sends 0220 (fire-and-forget).</summary>
public class FinancialAdviceScenario : IScenario
{
    private readonly FinancialAdviceBuilder _builder;
    private readonly ILogger<FinancialAdviceScenario> _logger;

    public FinancialAdviceScenario(FinancialAdviceBuilder builder, ILogger<FinancialAdviceScenario> logger)
    {
        _builder = builder;
        _logger = logger;
    }

    public string Name => "Financial Advice";

    public async Task<bool> RunAsync(SimulatorSession session, CancellationToken ct = default)
    {
        try
        {
            var message = session.CreateMessage();
            _builder.BuildRequest(message);
            await session.SendMessageAsync(message, ct);
            _logger.LogInformation("Financial Advice: sent OK");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Financial Advice: send failed");
            return false;
        }
    }
}

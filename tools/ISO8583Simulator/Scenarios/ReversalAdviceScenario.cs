using ISO8583Net.Simulator.Builders;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Simulator.Scenarios;

/// <summary>Reversal Advice scenario: sends 0420 (fire-and-forget).</summary>
public class ReversalAdviceScenario : IScenario
{
    private readonly ReversalAdviceBuilder _builder;
    private readonly ILogger<ReversalAdviceScenario> _logger;

    public ReversalAdviceScenario(ReversalAdviceBuilder builder, ILogger<ReversalAdviceScenario> logger)
    {
        _builder = builder;
        _logger = logger;
    }

    public string Name => "Reversal Advice";

    public async Task<bool> RunAsync(SimulatorSession session, CancellationToken ct = default)
    {
        try
        {
            var message = session.CreateMessage();
            _builder.BuildRequest(message);
            await session.SendMessageAsync(message, ct);
            _logger.LogInformation("Reversal Advice: sent OK");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reversal Advice: send failed");
            return false;
        }
    }
}

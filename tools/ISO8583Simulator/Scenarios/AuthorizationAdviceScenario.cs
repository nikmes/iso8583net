using ISO8583Net.Simulator.Builders;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Simulator.Scenarios;

/// <summary>Authorization Advice scenario: sends 0120 (fire-and-forget).</summary>
public class AuthorizationAdviceScenario : IScenario
{
    private readonly AuthorizationAdviceBuilder _builder;
    private readonly ILogger<AuthorizationAdviceScenario> _logger;

    public AuthorizationAdviceScenario(AuthorizationAdviceBuilder builder, ILogger<AuthorizationAdviceScenario> logger)
    {
        _builder = builder;
        _logger = logger;
    }

    public string Name => "Authorization Advice";

    public async Task<bool> RunAsync(SimulatorSession session, CancellationToken ct = default)
    {
        try
        {
            var message = session.CreateMessage();
            _builder.BuildRequest(message);
            await session.SendMessageAsync(message, ct);
            // Advice — no response expected, success if no exception
            _logger.LogInformation("Authorization Advice: sent OK");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authorization Advice: send failed");
            return false;
        }
    }
}

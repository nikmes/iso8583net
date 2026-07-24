using ISO8583Net.Simulator.Builders;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Simulator.Scenarios;

/// <summary>Authorization scenario: sends 0100 and expects 0110 with F39="000".</summary>
public class AuthorizationScenario : IScenario
{
    private readonly AuthorizationBuilder _builder;
    private readonly ILogger<AuthorizationScenario> _logger;

    public AuthorizationScenario(AuthorizationBuilder builder, ILogger<AuthorizationScenario> logger)
    {
        _builder = builder;
        _logger = logger;
    }

    public string Name => "Authorization";

    public async Task<bool> RunAsync(SimulatorSession session, CancellationToken ct = default)
    {
        var message = session.CreateMessage();
        _builder.BuildRequest(message);
        var response = await session.SendMessageAsync(message, ct);
        if (response is null)
        {
            _logger.LogWarning("Authorization: no response received");
            return false;
        }

        var f39 = response.GetFieldValue(39);
        bool pass = f39 == "000";
        _logger.LogInformation("Authorization: F39={F39} → {Result}", f39, pass ? "PASS" : "FAIL");
        return pass;
    }
}

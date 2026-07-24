using ISO8583Net.Simulator.Builders;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Simulator.Scenarios;

/// <summary>Sign-On scenario: sends 0800 SignOn and expects 0810 with F39="000".</summary>
public class SignOnScenario : IScenario
{
    private readonly NetworkManagementBuilder _builder;
    private readonly ILogger<SignOnScenario> _logger;

    public SignOnScenario(NetworkManagementBuilder builder, ILogger<SignOnScenario> logger)
    {
        _builder = builder;
        _builder.Mode = NetworkManagementBuilder.Function.SignOn;
        _logger = logger;
    }

    public string Name => "Sign-On";

    public async Task<bool> RunAsync(SimulatorSession session, CancellationToken ct = default)
    {
        var message = session.CreateMessage();
        _builder.BuildRequest(message);
        var response = await session.SendMessageAsync(message, ct);
        if (response is null)
        {
            _logger.LogWarning("Sign-On: no response received");
            return false;
        }

        var f39 = response.GetFieldValue(39);
        bool pass = f39 == "000";
        _logger.LogInformation("Sign-On: F39={F39} → {Result}", f39, pass ? "PASS" : "FAIL");
        return pass;
    }
}

using ISO8583Net.Simulator.Builders;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Simulator.Scenarios;

/// <summary>Echo scenario: sends 0800 Echo and expects 0810 response.</summary>
public class EchoScenario : IScenario
{
    private readonly NetworkManagementBuilder _builder;
    private readonly ILogger<EchoScenario> _logger;

    public EchoScenario(NetworkManagementBuilder builder, ILogger<EchoScenario> logger)
    {
        _builder = builder;
        _builder.Mode = NetworkManagementBuilder.Function.Echo;
        _logger = logger;
    }

    public string Name => "Echo";

    public async Task<bool> RunAsync(SimulatorSession session, CancellationToken ct = default)
    {
        var message = session.CreateMessage();
        _builder.BuildRequest(message);
        var response = await session.SendMessageAsync(message, ct);
        if (response is null)
        {
            _logger.LogWarning("Echo: no response received");
            return false;
        }

        var f39 = response.GetFieldValue(39);
        bool pass = f39 == "000";
        _logger.LogInformation("Echo: F39={F39} → {Result}", f39, pass ? "PASS" : "FAIL");
        return pass;
    }
}

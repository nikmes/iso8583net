using ISO8583Net.Simulator.Builders;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Simulator.Scenarios;

/// <summary>
/// Full lifecycle scenario that runs a complete transaction flow:
/// Sign-On → Authorization → Financial → Reversal → Sign-Off.
/// All steps must pass for the scenario to pass.
/// </summary>
public class FullLifecycleScenario : IScenario
{
    private readonly NetworkManagementBuilder _networkBuilder;
    private readonly AuthorizationBuilder _authBuilder;
    private readonly FinancialBuilder _financialBuilder;
    private readonly ReversalBuilder _reversalBuilder;
    private readonly ILogger<FullLifecycleScenario> _logger;

    public FullLifecycleScenario(
        NetworkManagementBuilder networkBuilder,
        AuthorizationBuilder authBuilder,
        FinancialBuilder financialBuilder,
        ReversalBuilder reversalBuilder,
        ILogger<FullLifecycleScenario> logger)
    {
        _networkBuilder = networkBuilder;
        _authBuilder = authBuilder;
        _financialBuilder = financialBuilder;
        _reversalBuilder = reversalBuilder;
        _logger = logger;
    }

    public string Name => "Full Lifecycle";

    public async Task<bool> RunAsync(SimulatorSession session, CancellationToken ct = default)
    {
        // Step 1: Sign-On
        _logger.LogInformation("Step 1/5: Sign-On");
        _networkBuilder.Mode = NetworkManagementBuilder.Function.SignOn;
        if (!await SendAndCheck(session, _networkBuilder, ct))
            return false;

        // Step 2: Authorization
        _logger.LogInformation("Step 2/5: Authorization");
        if (!await SendAndCheck(session, _authBuilder, ct))
            return false;

        // Step 3: Financial
        _logger.LogInformation("Step 3/5: Financial");
        if (!await SendAndCheck(session, _financialBuilder, ct))
            return false;

        // Step 4: Reversal
        _logger.LogInformation("Step 4/5: Reversal");
        if (!await SendAndCheck(session, _reversalBuilder, ct))
            return false;

        // Step 5: Sign-Off
        _logger.LogInformation("Step 5/5: Sign-Off");
        _networkBuilder.Mode = NetworkManagementBuilder.Function.SignOff;
        if (!await SendAndCheck(session, _networkBuilder, ct))
            return false;

        _logger.LogInformation("Full Lifecycle: all steps PASSED");
        return true;
    }

    private async Task<bool> SendAndCheck(
        SimulatorSession session,
        IMessageBuilder builder,
        CancellationToken ct)
    {
        try
        {
            var message = session.CreateMessage();
            builder.BuildRequest(message);
            var response = await session.SendMessageAsync(message, ct);
            if (response is null)
            {
                _logger.LogWarning("No response received");
                return false;
            }

            var f39 = response.GetFieldValue(39);
            bool pass = f39 == "000";
            if (!pass)
                _logger.LogWarning("F39={F39} — expected 000", f39);
            return pass;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Step failed");
            return false;
        }
    }
}

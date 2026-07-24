using ISO8583Net.Message;

namespace ISO8583Net.Simulator.Builders;

/// <summary>Builds an Authorization Advice (0120) message.</summary>
public class AuthorizationAdviceBuilder : BaseAdviceBuilder
{
    /// <inheritdoc />
    protected override string AdviceMTI => "1120";
}

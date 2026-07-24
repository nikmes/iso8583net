using ISO8583Net.Message;

namespace ISO8583Net.Simulator.Builders;

/// <summary>Builds an Authorization Request (0100) message.</summary>
public class AuthorizationBuilder : BaseRequestBuilder
{
    /// <inheritdoc />
    protected override string RequestMTI => "1100";
}

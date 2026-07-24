using ISO8583Net.Message;

namespace ISO8583Net.Simulator.Builders;

/// <summary>Builds a Financial Request (0200) message.</summary>
public class FinancialBuilder : BaseRequestBuilder
{
    /// <inheritdoc />
    protected override string RequestMTI => "1200";
}

using ISO8583Net.Message;

namespace ISO8583Net.Simulator.Builders;

/// <summary>Builds a Financial Advice (0220) message.</summary>
public class FinancialAdviceBuilder : BaseAdviceBuilder
{
    /// <inheritdoc />
    protected override string AdviceMTI => "1220";
}

using ISO8583Net.Message;

namespace ISO8583Net.Simulator.Builders;

/// <summary>Builds a Reversal Advice (0420) message.</summary>
public class ReversalAdviceBuilder : BaseAdviceBuilder
{
    /// <inheritdoc />
    protected override string AdviceMTI => "1420";
}

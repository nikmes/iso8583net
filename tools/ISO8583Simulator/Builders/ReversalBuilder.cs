using ISO8583Net.Message;

namespace ISO8583Net.Simulator.Builders;

/// <summary>
/// Builds a Reversal Request (0400) message.
/// Overrides BuildRequest to populate F90 (Original Data Elements)
/// with the original transaction's details.
/// </summary>
public class ReversalBuilder : BaseRequestBuilder
{
    /// <summary>The MTI of the original transaction being reversed (e.g. "0100").</summary>
    public string OriginalMTI { get; set; } = "1100";

    /// <summary>The STAN of the original transaction.</summary>
    public string? OriginalStan { get; set; }

    /// <summary>The transmission date/time of the original transaction (MMDDhhmmss).</summary>
    public string? OriginalDateTime { get; set; }

    /// <summary>The RRN of the original transaction.</summary>
    public string? OriginalRrn { get; set; }

    /// <summary>The acquiring institution ID of the original transaction.</summary>
    public string OriginalAcquiringInst { get; set; } = "000001";

    /// <summary>The forwarding institution ID of the original transaction.</summary>
    public string OriginalForwardingInst { get; set; } = "000001";

    /// <inheritdoc />
    protected override string RequestMTI => "1400";

    /// <inheritdoc />
    public override void BuildRequest(ISOMessage message)
    {
        base.BuildRequest(message);

        // F90 – Original Data Elements (42 chars):
        //   Original MTI (4) + Original STAN (6) + Original Date/Time (10, MMDDhhmmss)
        //   + Original Acquirer (11) + Original Forwarder (11)
        var originalStan = OriginalStan ?? GenerateStan();
        var originalDateTime = OriginalDateTime ?? DateTime.UtcNow.AddMinutes(-5).ToString("MMddHHmmss");
        var originalRrn = OriginalRrn ?? GenerateRrn();

        // Build F90: OriginalMTI(4) + STAN(6) + DateTime(10) + Acquirer(11) + Forwarder(11) = 42
        var f90 = string.Concat(
            OriginalMTI.PadRight(4)[..4],
            originalStan.PadRight(6)[..6],
            originalDateTime.PadRight(10)[..10],
            OriginalAcquiringInst.PadRight(11)[..11],
            OriginalForwardingInst.PadRight(11)[..11]);

        message.Set(90, f90);
    }
}

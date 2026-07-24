using ISO8583Net.Message;

namespace ISO8583Net.Simulator.Builders;

/// <summary>
/// Abstract base for advice message builders (MTIs ending in "20", e.g. "0120").
/// Reuses the same mandatory defaults as <see cref="BaseRequestBuilder"/> and
/// additionally sets F39 (Response Code) to "400" (accepted).
/// </summary>
public abstract class BaseAdviceBuilder : IMessageBuilder
{
    /// <summary>The advice MTI this builder constructs (e.g. "0120").</summary>
    protected abstract string AdviceMTI { get; }

    /// <inheritdoc />
    public IReadOnlySet<string> SupportedMTIs => new HashSet<string> { AdviceMTI };

    /// <summary>
    /// Populate the message with F0, STAN, RRN, dates, mandatory defaults, and F39 = "400".
    /// Override to add MTI-specific fields after calling base.
    /// </summary>
    public virtual void BuildRequest(ISOMessage message)
    {
        // F0  – Message Type Indicator
        message.Set(0, AdviceMTI);

        // F7  – Transmission Date/Time (MMddHHmmss)
        message.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));

        // F11 – System Trace Audit Number
        message.Set(11, BaseRequestBuilder.GenerateStan());

        // F12 – Time, Local Transaction
        message.Set(12, DateTime.UtcNow.ToString("HHmmss"));

        // F37 – Retrieval Reference Number
        message.Set(37, BaseRequestBuilder.GenerateRrn());

        // Shared defaults (PAN, processing code, amount, etc.)
        DefaultFieldValues.FillMandatoryDefaults(message);

        // F39 – Response Code (400 = accepted)
        message.Set(39, "400");
    }
}

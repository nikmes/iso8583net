using ISO8583Net.Message;

namespace ISO8583Net.Simulator.Builders;

/// <summary>
/// Abstract base for request/response message builders (MTIs ending in "00" or "10").
/// Sets F0 = MTI, generates STAN (F11) and RRN (F37), sets transmission date/time (F7, F12),
/// and calls <see cref="DefaultFieldValues.FillMandatoryDefaults"/>.
/// </summary>
public abstract class BaseRequestBuilder : IMessageBuilder
{
    private static int s_stanCounter;
    private static int s_rrnCounter;

    /// <summary>The MTI this builder constructs (e.g. "0100").</summary>
    protected abstract string RequestMTI { get; }

    /// <inheritdoc />
    public IReadOnlySet<string> SupportedMTIs => new HashSet<string> { RequestMTI };

    /// <summary>
    /// Populate the message with F0, STAN, RRN, dates, and mandatory defaults.
    /// Override to add MTI-specific fields after calling base.
    /// </summary>
    public virtual void BuildRequest(ISOMessage message)
    {
        // F0  – Message Type Indicator
        message.Set(0, RequestMTI);

        // F7  – Transmission Date/Time (MMddHHmmss)
        message.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));

        // F11 – System Trace Audit Number
        message.Set(11, GenerateStan());

        // F12 – Time, Local Transaction
        message.Set(12, DateTime.UtcNow.ToString("HHmmss"));

        // F37 – Retrieval Reference Number
        message.Set(37, GenerateRrn());

        // Shared defaults (PAN, processing code, amount, etc.)
        DefaultFieldValues.FillMandatoryDefaults(message);
    }

    /// <summary>Generate a 6-digit STAN, thread-safe.</summary>
    public static string GenerateStan()
    {
        uint counter = (uint)Interlocked.Increment(ref s_stanCounter);
        // Mix counter with tick-based suffix for uniqueness across restarts
        return ((counter % 900_000) + 100_000).ToString("D6");
    }

    /// <summary>Generate a 12-digit RRN, thread-safe.</summary>
    public static string GenerateRrn()
    {
        var now = DateTime.UtcNow;
        uint counter = (uint)Interlocked.Increment(ref s_rrnCounter);
        // Format: yy(2) + DDD(3) + HHmmss(6) + 1-digit counter = 12 chars
        string prefix = now.ToString("yy") + now.DayOfYear.ToString("D3") + now.ToString("HHmmss");
        string suffix = (counter % 10).ToString("D1");
        return string.Concat(prefix, suffix);
    }
}

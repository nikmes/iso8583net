using ISO8583Net.Message;

namespace ISO8583Net.Simulator.Builders;

/// <summary>
/// Shared default field values used by both <see cref="BaseRequestBuilder"/>
/// and <see cref="BaseAdviceBuilder"/> to populate a new ISO 8583 message
/// with reasonable test-data defaults.
/// </summary>
public static class DefaultFieldValues
{
    /// <summary>
    /// Sets commonly required D8 G2B fields to default test values.
    /// Does NOT set F0 (MTI), F7 (date), F11 (STAN), or F37 (RRN) —
    /// those are set by <see cref="BaseRequestBuilder.BuildRequest"/>.
    /// </summary>
    public static void FillMandatoryDefaults(ISOMessage message)
    {
        // F2  – Primary Account Number
        message.Set(2, "4000000000000002");

        // F3  – Processing Code
        message.Set(3, "000000");

        // F4  – Amount, Transaction (10.00)
        message.Set(4, "000000001000");

        // F12 – Time, Local Transaction (set by caller via BuildRequest)
        // F22 – Point of Service Entry Mode
        message.Set(22, "022");

        // F24 – Function Code
        message.Set(24, "200");

        // F26 – Point of Service PIN Capture Code
        message.Set(26, "06");

        // F28 – Amount, Transaction Fee (0)
        message.Set(28, "000000");

        // F32 – Acquiring Institution ID
        message.Set(32, "000001");

        // F41 – Card Acceptor Terminal ID
        message.Set(41, "TERM001");

        // F42 – Card Acceptor ID Code
        message.Set(42, "ACQ001");

        // F49 – Currency Code (840 = USD)
        message.Set(49, "840");
    }
}

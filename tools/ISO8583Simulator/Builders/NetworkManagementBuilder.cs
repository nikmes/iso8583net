using ISO8583Net.Message;

namespace ISO8583Net.Simulator.Builders;

/// <summary>
/// Network management message builder for MTI 1804.
/// Supports Sign-On, Sign-Off, and Echo test functions via F24.
/// </summary>
public class NetworkManagementBuilder : BaseRequestBuilder
{
    /// <summary>Network management function codes sent in F24.</summary>
    public enum Function
    {
        SignOn  = 1,  // F24 = "801"
        SignOff = 2,  // F24 = "802"
        Echo    = 3   // F24 = "831"
    }

    /// <summary>The function this builder will use.</summary>
    public Function Mode { get; set; } = Function.Echo;

    /// <inheritdoc />
    protected override string RequestMTI => "1804";

    /// <inheritdoc />
    public override void BuildRequest(ISOMessage message)
    {
        // NOTE: Does NOT call base.BuildRequest() because FillMandatoryDefaults()
        // sets fields (F2/PAN, F3, F4, etc.) that the 1804 message type doesn't
        // define, which causes the server parser to crash.

        // F0  – Message Type Indicator
        message.Set(0, RequestMTI);

        // F7  – Transmission Date/Time
        message.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));

        // F11 – STAN
        message.Set(11, GenerateStan());

        // F24 – Function Code (801=SignOn, 802=SignOff, 831=Echo)
        message.Set(24, Mode switch
        {
            Function.SignOn  => "801",
            Function.SignOff => "802",
            Function.Echo    => "831",
            _ => "831"
        });

        // F28 – Transaction Fee (mandatory for 1804)
        message.Set(28, "000000000000");
    }
}

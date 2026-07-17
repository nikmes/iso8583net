using System.Threading.Tasks;
using ISO8583Net.Server.Pipeline.Handlers;
using Microsoft.Extensions.Logging;

namespace ISO8583Service.Handlers;

/// <summary>
/// Handles Authorization Advice (1120→1130). Acknowledges previously
/// completed authorizations sent via store-and-forward.
/// </summary>
public class AuthorizationAdviceHandler : BaseAdviceHandler
{
    public override string AdviceMTI => "1120";
    public override string ResponseMTI => "1130";

    public AuthorizationAdviceHandler(ILogger<AuthorizationAdviceHandler>? logger = null)
        : base(logger) { }
}

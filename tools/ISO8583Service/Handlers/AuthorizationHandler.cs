using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Server.Pipeline.Handlers;
using ISO8583Net.Server.Pipeline.Messages;
using Microsoft.Extensions.Logging;

namespace ISO8583Service.Handlers;

/// <summary>
/// Handles ISO 8583 Authorization Request (1100→1110). Verifies
/// cardholder eligibility before a financial transaction.
/// </summary>
public class AuthorizationHandler : BaseRequestHandler
{
    public override string RequestMTI => "1100";
    public override string ResponseMTI => "1110";

    public AuthorizationHandler(ILogger<AuthorizationHandler>? logger = null)
        : base(logger) { }

    /// <summary>
    /// Default: auto-approve. Override for real authorization logic.
    /// </summary>
    protected override Task<ProcessResult> ProcessAsync(
        MessageContext context, CancellationToken ct)
    {
        // TODO: Validate PAN, check limits, risk scoring
        return Task.FromResult(ProcessResult.Approved());
    }
}

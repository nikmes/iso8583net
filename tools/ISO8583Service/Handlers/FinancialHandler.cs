using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Server.Pipeline.Handlers;
using ISO8583Net.Server.Pipeline.Messages;
using Microsoft.Extensions.Logging;

namespace ISO8583Service.Handlers;

/// <summary>
/// Handles ISO 8583 Financial Request (1200→1210). Posts actual
/// transactions following an approved authorization (1100).
/// </summary>
public class FinancialHandler : BaseRequestHandler
{
    public override string RequestMTI => "1200";
    public override string ResponseMTI => "1210";

    public FinancialHandler(ILogger<FinancialHandler>? logger = null)
        : base(logger) { }

    /// <summary>
    /// Default: auto-approve. Override for real posting logic.
    /// </summary>
    protected override Task<ProcessResult> ProcessAsync(
        MessageContext context, CancellationToken ct)
    {
        // TODO: Verify approval code, match amount, post to ledger
        return Task.FromResult(ProcessResult.Approved());
    }
}

using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Server.Pipeline.Handlers;
using ISO8583Net.Server.Pipeline.Messages;
using Microsoft.Extensions.Logging;

namespace ISO8583Service.Handlers;

/// <summary>
/// Handles ISO 8583 Reversal Request (1400→1410). Reverses previously
/// approved transactions (full or partial via F24 function code).
/// </summary>
public class ReversalHandler : BaseRequestHandler
{
    public override string RequestMTI => "1400";
    public override string ResponseMTI => "1410";

    private readonly ILogger<ReversalHandler> _logger;

    public ReversalHandler(ILogger<ReversalHandler>? logger = null)
        : base(logger)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ReversalHandler>.Instance;
    }

    /// <summary>
    /// Default: auto-approve. Override for real reversal logic.
    /// </summary>
    protected override Task<ProcessResult> ProcessAsync(
        MessageContext context, CancellationToken ct)
    {
        var request = context.Request;
        _logger.LogInformation("Reversal F24={Function} RRN={RRN}",
            request.GetFieldValue(24) ?? "?", request.GetFieldValue(37) ?? "?");

        // TODO: Match RRN→original txn, validate amount, post reversing entry
        return Task.FromResult(ProcessResult.Approved());
    }
}

using System.Threading.Tasks;
using ISO8583Net.Server.Pipeline.Handlers;
using Microsoft.Extensions.Logging;

namespace ISO8583Service.Handlers;

/// <summary>
/// Handles Reversal Advice (1420→1430). Confirms previously
/// completed reversals via store-and-forward.
/// </summary>
public class ReversalAdviceHandler : BaseAdviceHandler
{
    public override string AdviceMTI => "1420";
    public override string ResponseMTI => "1430";

    public ReversalAdviceHandler(ILogger<ReversalAdviceHandler>? logger = null)
        : base(logger) { }
}

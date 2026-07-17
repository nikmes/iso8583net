using System.Threading.Tasks;
using ISO8583Net.Server.Pipeline.Handlers;
using Microsoft.Extensions.Logging;

namespace ISO8583Service.Handlers;

/// <summary>
/// Handles Financial Advice (1220→1230). Confirms previously
/// completed financial transactions via store-and-forward.
/// </summary>
public class FinancialAdviceHandler : BaseAdviceHandler
{
    public override string AdviceMTI => "1220";
    public override string ResponseMTI => "1230";

    public FinancialAdviceHandler(ILogger<FinancialAdviceHandler>? logger = null)
        : base(logger) { }
}

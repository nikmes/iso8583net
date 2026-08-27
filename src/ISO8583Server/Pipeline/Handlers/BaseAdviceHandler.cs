using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Message;
using ISO8583Net.Server.Pipeline.Messages;
using ISO8583Net.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ISO8583Net.Server.Pipeline.Handlers;

/// <summary>
/// Abstract base for advice handlers (1120→1130, 1220→1230, 1420→1430).
///
/// Advice messages are store-and-forward notifications of previously
/// completed transactions. The handler acknowledges receipt with
/// F39="400" (accepted). No business logic is required — if a derived
/// class needs to perform post-processing (logging, reconciliation,
/// SAF clearing), it can override <see cref="OnAcknowledgedAsync"/>.
/// </summary>
public abstract class BaseAdviceHandler : IMessageHandler
{
    /// <summary>The advice MTI this handler processes (e.g. "1120").</summary>
    public abstract string AdviceMTI { get; }

    /// <summary>The response MTI (e.g. "1130").</summary>
    public abstract string ResponseMTI { get; }

    /// <inheritdoc />
    public IReadOnlySet<string> SupportedMTIs { get; }

    private readonly ILogger _logger;

    protected BaseAdviceHandler(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        SupportedMTIs = new HashSet<string> { AdviceMTI };
    }

    /// <inheritdoc />
    public async Task<ISOMessage?> HandleAsync(MessageContext context, CancellationToken ct)
    {
        var request = context.Request;

        _logger.LogDebug("Acknowledging {MTI} advice, conn={ConnNum}", AdviceMTI, context.ConnectionNumber);

        try
        {
            await OnAcknowledgedAsync(context, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Advice post-processing error for {MTI}", AdviceMTI);
        }

        return BuildAcknowledgement(request);
    }

    /// <summary>
    /// Override to perform post-processing after acknowledging the advice
    /// (e.g. SAF clearing, reconciliation, audit logging).
    /// </summary>
    protected virtual Task OnAcknowledgedAsync(MessageContext context, CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Build the acknowledgment response.
    ///
    /// Builds a clean response (rather than mutating the request in place) and
    /// copies only the fields that participate in the response MTI's dialect
    /// definition. This keeps advice-only fields (e.g. 1420's F22/F24/F25/F26/
    /// F33/F43) out of the acknowledgment, which would otherwise fail outbound
    /// dialect validation in Warn/On mode. F39 is set to "400" (accepted).
    /// </summary>
    protected virtual ISOMessage BuildAcknowledgement(ISOMessage request)
    {
        var response = request.CreateCleanResponse();

        // Set response MTI (e.g. 1120 → 1130).
        response.Set(0, ResponseMTI);

        // Copy only request fields that participate (mandatory/optional/conditional)
        // in the response MTI. Fields absent from the request are skipped; fields the
        // response MTI does not allow are intentionally left out.
        var msgTypes = request.Packager.GetISOMessageFieldsPackager().GetMessageTypesPackager();
        var mandatory = msgTypes.GetMandatoryBitmap(ResponseMTI);
        var optional = msgTypes.GetOptionalBitmap(ResponseMTI);
        var conditional = msgTypes.GetConditionalBitmap(ResponseMTI);

        int totalFields = request.Packager.GetTotalFields();
        for (int fn = 2; fn < totalFields; fn++)
        {
            // Skip bitmap continuation flags (65/129), which are not data fields.
            if (fn == BitmapBoundaries.SecondaryBitmapFlag || fn == BitmapBoundaries.TertiaryBitmapFlag)
                continue;

            bool participates = mandatory.BitIsSet(fn) || optional.BitIsSet(fn) || conditional.BitIsSet(fn);
            if (!participates)
                continue;

            string? value = request.GetFieldValue(fn);
            if (value != null)
                response.Set(fn, value);
        }

        response.Set(39, "400");

        _logger.LogDebug("Advice ack {MTI}: F39=400", ResponseMTI);

        return response;
    }
}

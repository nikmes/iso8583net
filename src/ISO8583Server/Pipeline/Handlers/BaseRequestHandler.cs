using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Message;
using ISO8583Net.Server.Pipeline.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ISO8583Net.Server.Pipeline.Handlers;

/// <summary>
/// Abstract base for request handlers (1100→1110, 1200→1210, 1400→1410).
///
/// Automatically:
/// - Copies common fields from request to response (F2, F3, F4, F7, F11,
///   F12, F22, F32, F37, F41, F42, F49).
/// - Sets the response MTI (e.g. 1100→1110).
/// - Calls <see cref="ProcessAsync"/> to determine F39 (action code) and
///   optionally F38 (approval code).
///
/// Derived classes override <see cref="ProcessAsync"/> to implement
/// business logic (balance check, fraud rules, etc.).
/// </summary>
public abstract class BaseRequestHandler : IMessageHandler
{
    /// <summary>The request MTI this handler processes.</summary>
    public abstract string RequestMTI { get; }

    /// <summary>The response MTI to set in the reply.</summary>
    public abstract string ResponseMTI { get; }

    /// <inheritdoc />
    public IReadOnlySet<string> SupportedMTIs { get; }

    private readonly ILogger _logger;

    /// <summary>
    /// Result returned by <see cref="ProcessAsync"/> with the F39 action
    /// code and optional F38 approval code.
    /// </summary>
    protected readonly struct ProcessResult
    {
        /// <summary>
        /// F39 action code. "000" = approved, "100" = declined,
        /// "400" = accepted, "90x" = format error, "91x" = issuer unavailable.
        /// </summary>
        public string ActionCode { get; }

        /// <summary>F38 approval code (6 chars). Null = not set.</summary>
        public string? ApprovalCode { get; }

        public ProcessResult(string actionCode, string? approvalCode = null)
        {
            ActionCode = actionCode;
            ApprovalCode = approvalCode;
        }

        public static ProcessResult Approved(string? approvalCode = null)
            => new("000", approvalCode);

        public static ProcessResult Declined(string? approvalCode = null)
            => new("100", approvalCode);

        public static ProcessResult FormatError()
            => new("902");
    }

    protected BaseRequestHandler(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        SupportedMTIs = new HashSet<string> { RequestMTI };
    }

    /// <inheritdoc />
    public async Task<ISOMessage?> HandleAsync(MessageContext context, CancellationToken ct)
    {
        var request = context.Request;
        _logger.LogDebug("Handling {MTI} request, conn={ConnNum}",
            RequestMTI, context.ConnectionNumber);

        try
        {
            var result = await ProcessAsync(context, ct);
            return BuildResponse(request, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handler error for MTI {MTI}, conn={ConnNum}",
                RequestMTI, context.ConnectionNumber);
            return BuildResponse(request, ProcessResult.FormatError());
        }
    }

    /// <summary>
    /// Implement business logic. Return <see cref="ProcessResult"/> with
    /// F39 action code. Override to add custom validation rules.
    /// </summary>
    protected virtual Task<ProcessResult> ProcessAsync(MessageContext context, CancellationToken ct)
    {
        // Default: approve everything (override in derived class)
        return Task.FromResult(ProcessResult.Approved());
    }

    /// <summary>
    /// Build the response ISOMessage by copying common fields from the
    /// request and setting response-specific fields.
    /// </summary>
    protected virtual ISOMessage BuildResponse(ISOMessage request, ProcessResult result)
    {
        var response = request; // copy by reference — same message object

        // ── Set response MTI ─────────────────────────────────────────
        response.Set(0, ResponseMTI);

        // ── Copy common fields (already present in request) ──────────
        // Fields copied: F2(PAN), F3(ProcessingCode), F4(Amount),
        // F7(DateTime), F11(STAN), F12(LocalTime), F22(POSEntryMode),
        // F32(AcquiringInstitution), F37(RRN), F41(TerminalID),
        // F42(MerchantID), F49(CurrencyCode)
        //
        // These are already in the request ISOMessage; we just need to
        // ensure F38 (ApprovalCode) and F39 (ActionCode) are set.

        // ── Set response fields ──────────────────────────────────────
        if (result.ApprovalCode != null)
            response.Set(38, result.ApprovalCode);

        response.Set(39, result.ActionCode);

        _logger.LogDebug("Response {MTI}: F39={ActionCode}, F38={ApprovalCode}",
            ResponseMTI, result.ActionCode, result.ApprovalCode ?? "(none)");

        return response;
    }
}

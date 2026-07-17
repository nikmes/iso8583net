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
/// Handles ISO 8583 Network Management requests (MTI 1804).
///
/// Dispatches to virtual methods based on F24 (Function Code):
/// - 801: Logon (client session initialization)
/// - 802: Logoff (client graceful disconnect)
/// - 811: Key Change (crypto key rotation)
/// - 831: Echo Test (keep-alive/heartbeat)
///
/// Responses are MTI 1814. F39 is set based on handler result.
/// The existing DefaultHandler already echoes 1800→1814; this handler
/// provides richer network management lifecycle support.
/// </summary>
public class NetworkManagementHandler : IMessageHandler
{
    /// <inheritdoc />
    public IReadOnlySet<string> SupportedMTIs { get; } = new HashSet<string> { "1804" };

    private readonly ILogger _logger;

    public NetworkManagementHandler(ILogger<NetworkManagementHandler>? logger = null)
    {
        _logger = (ILogger?)logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public async Task<ISOMessage?> HandleAsync(MessageContext context, CancellationToken ct)
    {
        var request = context.Request;
        string functionCode = request.GetFieldValue(24) ?? "000";
        _logger.LogInformation("Network management {Function} from conn={ConnNum}",
            functionCode, context.ConnectionNumber);

        try
        {
            string f39 = functionCode switch
            {
                "801" => await HandleLogonAsync(context, ct),
                "802" => await HandleLogoffAsync(context, ct),
                "811" => await HandleKeyChangeAsync(context, ct),
                "831" => await HandleEchoAsync(context, ct),
                _ => "902" // unknown function code
            };

            return BuildResponse(request, f39);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Network management error for F24={Function}", functionCode);
            return BuildResponse(request, "909");
        }
    }

    /// <summary>
    /// Handle client logon (F24=801). Override to implement authentication,
    /// session tracking, or connection allow-listing.
    /// Default: always approved.
    /// </summary>
    protected virtual Task<string> HandleLogonAsync(MessageContext context, CancellationToken ct)
    {
        _logger.LogInformation("Client logon from conn={ConnNum}, endpoint={Endpoint}",
            context.ConnectionNumber, context.RemoteEndpoint);
        return Task.FromResult("000");
    }

    /// <summary>
    /// Handle client logoff (F24=802). Override to implement cleanup logic.
    /// Default: always accepted.
    /// </summary>
    protected virtual Task<string> HandleLogoffAsync(MessageContext context, CancellationToken ct)
    {
        _logger.LogInformation("Client logoff from conn={ConnNum}", context.ConnectionNumber);
        return Task.FromResult("000");
    }

    /// <summary>
    /// Handle key change (F24=811). Override to implement cryptographic
    /// key rotation (ZMK, ZPK, TMK, etc.). Default: not implemented.
    /// </summary>
    protected virtual Task<string> HandleKeyChangeAsync(MessageContext context, CancellationToken ct)
    {
        _logger.LogWarning("Key change requested but not implemented, conn={ConnNum}",
            context.ConnectionNumber);
        return Task.FromResult("906"); // not supported
    }

    /// <summary>
    /// Handle echo test (F24=831). Keep-alive/heartbeat.
    /// Default: always accepted.
    /// </summary>
    protected virtual Task<string> HandleEchoAsync(MessageContext context, CancellationToken ct)
    {
        return Task.FromResult("000");
    }

    /// <summary>
    /// Build the network management response (MTI 1814).
    /// Copies F7, F11, F24 from request and sets F39.
    /// </summary>
    protected virtual ISOMessage BuildResponse(ISOMessage request, string f39)
    {
        var response = request;
        response.Set(0, "1814");
        response.Set(39, f39);
        return response;
    }
}

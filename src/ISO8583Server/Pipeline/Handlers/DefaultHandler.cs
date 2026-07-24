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
/// Default catch-all handler registered with MTI "*".
///
/// Handles:
///   - 1800 (SignOn) → 1814 with F39="000" (legacy echo behavior)
///   - Everything else → no response (passthrough)
///
/// Specific handlers registered for MTIs (1100, 1200, 1400, etc.) take
/// precedence by providing a response; this handler still fires but
/// returns null, making it a no-op for handled MTIs.
/// </summary>
public sealed class DefaultHandler : IMessageHandler
{
    public IReadOnlySet<string> SupportedMTIs { get; } = new HashSet<string> { "*" };

    private readonly ILogger<DefaultHandler> _logger;

    public DefaultHandler(ILogger<DefaultHandler>? logger = null)
    {
        _logger = logger ?? NullLogger<DefaultHandler>.Instance;
    }

    public Task<ISOMessage?> HandleAsync(MessageContext context, CancellationToken ct)
    {
        string? mti = context.Request.GetFieldValue(0);

        if (mti is null)
        {
            _logger.LogWarning("DefaultHandler: Skipping header-only / corrupt message (no MTI), conn={ConnNum}",
                context.ConnectionNumber);
            return Task.FromResult<ISOMessage?>(null);
        }

        if (mti == "1800")
        {
            // Echo with MTI 1814 + F39="000"
            context.Request.Set(0, "1814");
            context.Request.Set(39, "000");

            _logger.LogDebug("DefaultHandler: 1800→1814 echo, conn={ConnNum}",
                context.ConnectionNumber);

            return Task.FromResult<ISOMessage?>(context.Request);
        }

        // D8 responds with 1810 to server-initiated SignOn (1800)
        if (mti == "1810")
        {
            string? f39 = context.Request.GetFieldValue(39);
            _logger.LogInformation("DefaultHandler: D8 SignOn response (MTI=1810, F39={F39}), conn={ConnNum}",
                f39 ?? "?", context.ConnectionNumber);
            // No further response — 1810 is the response to our 1800
            return Task.FromResult<ISOMessage?>(null);
        }

        // For MTIs handled by specific handlers, this still fires
        // (as catch-all) but returns null — a no-op. For truly unknown
        // MTIs, also return null (no response).
        _logger.LogTrace("DefaultHandler passthrough for MTI={MTI}", mti);

        return Task.FromResult<ISOMessage?>(null);
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Message;
using ISO8583Net.Server.Pipeline.Messages;

namespace ISO8583Net.Server.Pipeline.Handlers;

/// <summary>
/// Default handler that replicates current Iso8583TcpServer behavior:
/// auto-responds to SignOn (MTI 1800) with MTI 1814 and F39="000".
///
/// Registered in DI as a catch-all handler. Override by registering
/// a more specific handler for MTI "1800".
/// </summary>
public sealed class DefaultHandler : IMessageHandler
{
    public IReadOnlySet<string> SupportedMTIs { get; } = new HashSet<string> { "*" };

    public Task<ISOMessage?> HandleAsync(MessageContext context, CancellationToken ct)
    {
        string? mti = context.Request.GetFieldValue(0);

        if (mti == "1800")
        {
            // Copy incoming message, modify MTI + response code
            context.Request.Set(0, "1814");
            context.Request.Set(39, "000");

            return Task.FromResult<ISOMessage?>(context.Request);
        }

        // For all other MTIs, send no response (passthrough behavior)
        return Task.FromResult<ISOMessage?>(null);
    }
}

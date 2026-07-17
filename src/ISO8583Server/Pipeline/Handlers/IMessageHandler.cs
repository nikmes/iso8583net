using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Message;
using ISO8583Net.Server.Pipeline.Messages;

namespace ISO8583Net.Server.Pipeline.Handlers;

/// <summary>
/// Defines a handler for ISO 8583 messages. Implementations are discovered
/// via DI and registered by their <see cref="SupportedMTIs"/>.
///
/// <example>
/// Registration in Program.cs:
/// <code>
/// builder.Services.AddSingleton&lt;IMessageHandler, AuthorizationHandler&gt;();
/// </code>
/// </example>
/// </summary>
public interface IMessageHandler
{
    /// <summary>
    /// The set of MTIs (field 0 values) this handler processes.
    /// Use <c>["*"]</c> for a catch-all handler that receives every message.
    /// </summary>
    IReadOnlySet<string> SupportedMTIs { get; }

    /// <summary>
    /// Handle an incoming ISO 8583 message.
    /// </summary>
    /// <param name="context">The message context with request and response channel.</param>
    /// <param name="ct">Cancellation token triggered on shutdown.</param>
    /// <returns>
    /// An ISOMessage to send as a response, or null to skip sending a response.
    /// </returns>
    Task<ISOMessage?> HandleAsync(MessageContext context, CancellationToken ct);
}

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ISO8583Net.Message;

namespace ISO8583Net.Server.Pipeline.Messages;

/// <summary>
/// Context passed to <see cref="Handlers.IMessageHandler.HandleAsync"/>.
/// Provides access to the request message and a way to send a response
/// back through the pipeline.
/// </summary>
public sealed class MessageContext
{
    private readonly ChannelWriter<OutboundMessage> _writer;

    /// <summary>The incoming ISO 8583 request message.</summary>
    public ISOMessage Request { get; }

    /// <summary>Connection number.</summary>
    public int ConnectionNumber { get; }

    /// <summary>Remote endpoint (IP:port).</summary>
    public string RemoteEndpoint { get; }

    /// <summary>When the raw frame was received from the socket.</summary>
    public DateTime ReceivedAt { get; }

    /// <summary>
    /// Send a response back to the client through the pipeline writer stage.
    /// </summary>
    /// <param name="response">The ISO message to send. Will be packed by the writer.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the response is queued for writing.</returns>
    public ValueTask SendResponseAsync(ISOMessage response, CancellationToken ct = default)
    {
        var outbound = OutboundMessage.FromISOMessage(response, ConnectionNumber);
        return _writer.WriteAsync(outbound, ct);
    }

    /// <summary>
    /// Send pre-framed bytes directly (length prefix already included).
    /// </summary>
    public ValueTask SendRawResponseAsync(byte[] preFramed, CancellationToken ct = default)
    {
        var outbound = OutboundMessage.FromPreFramed(preFramed, ConnectionNumber);
        return _writer.WriteAsync(outbound, ct);
    }

    internal MessageContext(
        ISOMessage request,
        int connectionNumber,
        string remoteEndpoint,
        DateTime receivedAt,
        ChannelWriter<OutboundMessage> writer)
    {
        Request = request;
        ConnectionNumber = connectionNumber;
        RemoteEndpoint = remoteEndpoint;
        ReceivedAt = receivedAt;
        _writer = writer;
    }
}

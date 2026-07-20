namespace ISO8583Net.Server.Pipeline.Messages;

/// <summary>
/// Default no-op message tracer. All methods are empty and will be
/// eliminated by the JIT when used as the concrete type.
/// </summary>
public sealed class NoopMessageTracer : IMessageTracer
{
    public static readonly NoopMessageTracer Instance = new();

    public void OnMessageReceived(string mti, string rawHex, int fieldCount,
        int connNum, string remoteEndpoint) { }

    public void OnMessageResponded(string requestMti, string responseMti,
        string f39, int connNum, long elapsedMs) { }

    public void OnParseError(string rawHex, int connNum, string error) { }

    public void OnNoResponse(string mti, int connNum) { }

    public void OnHandlerError(string mti, int connNum, string error) { }
}

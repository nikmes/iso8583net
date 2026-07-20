namespace ISO8583Net.Server.Pipeline.Messages;

/// <summary>
/// Traces ISO 8583 messages flowing through the pipeline for
/// diagnostics, audit, and debugging. All methods return void —
/// implementations should use non-blocking I/O internally.
///
/// <para>
/// Hook points:
///   ParserStage → OnMessageReceived / OnParseError
///   DispatcherStage → OnMessageResponded / OnNoResponse / OnHandlerError
/// </para>
/// </summary>
public interface IMessageTracer
{
    /// <summary>
    /// Called after a message is successfully parsed.
    /// </summary>
    /// <param name="mti">Message Type Indicator (F0).</param>
    /// <param name="rawHex">Hex dump of the raw frame bytes.</param>
    /// <param name="fieldCount">Number of ISO fields extracted.</param>
    /// <param name="connNum">Connection number.</param>
    /// <param name="remoteEndpoint">Client IP:port.</param>
    void OnMessageReceived(string mti, string rawHex, int fieldCount,
        int connNum, string remoteEndpoint);

    /// <summary>
    /// Called after a handler produces a response.
    /// </summary>
    /// <param name="requestMti">Incoming MTI (e.g. "1100").</param>
    /// <param name="responseMti">Outgoing MTI (e.g. "1110").</param>
    /// <param name="f39">Action code (F39), e.g. "000".</param>
    /// <param name="connNum">Connection number.</param>
    /// <param name="elapsedMs">Handler processing time in milliseconds.</param>
    void OnMessageResponded(string requestMti, string responseMti,
        string f39, int connNum, long elapsedMs);

    /// <summary>
    /// Called when parsing fails.
    /// </summary>
    /// <param name="rawHex">Hex dump of the raw frame bytes (best effort).</param>
    /// <param name="connNum">Connection number.</param>
    /// <param name="error">Error message.</param>
    void OnParseError(string rawHex, int connNum, string error);

    /// <summary>
    /// Called when no handler is registered for the MTI, or all handlers returned null.
    /// </summary>
    /// <param name="mti">MTI with no response.</param>
    /// <param name="connNum">Connection number.</param>
    void OnNoResponse(string mti, int connNum);

    /// <summary>
    /// Called when a handler throws an exception.
    /// </summary>
    /// <param name="mti">MTI being processed.</param>
    /// <param name="connNum">Connection number.</param>
    /// <param name="error">Exception message.</param>
    void OnHandlerError(string mti, int connNum, string error);
}

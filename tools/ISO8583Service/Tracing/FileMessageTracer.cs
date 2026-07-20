using System;
using ISO8583Net.Server.Pipeline.Messages;
using Microsoft.Extensions.Logging;

namespace ISO8583Service.Tracing;

/// <summary>
/// Traces every ISO 8583 message through the pipeline using Serilog
/// structured logging. Events are written to the diagnostic sink
/// configured in appsettings.json (console + dedicated messages file).
///
/// <para>
/// Log event template: <c>{TraceType} | MTI={Mti} | Conn={ConnNum} | ...</c>
/// </para>
/// </summary>
public sealed class FileMessageTracer : IMessageTracer
{
    private readonly ILogger<FileMessageTracer> _logger;

    public FileMessageTracer(ILogger<FileMessageTracer> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void OnMessageReceived(string mti, string rawHex, int fieldCount,
        int connNum, string remoteEndpoint)
    {
        _logger.LogInformation(
            "RECV | MTI={Mti} | Conn={ConnNum} | Flds={FieldCount} | {Endpoint} | {Hex}",
            mti, connNum, fieldCount, remoteEndpoint, TruncateHex(rawHex));
    }

    /// <inheritdoc />
    public void OnMessageResponded(string requestMti, string responseMti,
        string f39, int connNum, long elapsedMs)
    {
        _logger.LogInformation(
            "SEND | MTI={ReqMti}→{RespMti} | F39={F39} | Conn={ConnNum} | {Elapsed}ms",
            requestMti, responseMti, f39, connNum, elapsedMs);
    }

    /// <inheritdoc />
    public void OnParseError(string rawHex, int connNum, string error)
    {
        _logger.LogWarning(
            "PARSE_ERR | Conn={ConnNum} | {Error} | {Hex}",
            connNum, error, TruncateHex(rawHex));
    }

    /// <inheritdoc />
    public void OnNoResponse(string mti, int connNum)
    {
        _logger.LogDebug(
            "NO_RESP | MTI={Mti} | Conn={ConnNum} | no handler or null response",
            mti, connNum);
    }

    /// <inheritdoc />
    public void OnHandlerError(string mti, int connNum, string error)
    {
        _logger.LogError(
            "HANDLER_ERR | MTI={Mti} | Conn={ConnNum} | {Error}",
            mti, connNum, error);
    }

    /// <summary>Truncate hex dump to 200 chars to avoid log bloat.</summary>
    private static string TruncateHex(string hex)
    {
        if (hex.Length <= 200)
            return hex;
        return hex[..200] + $"... ({hex.Length} total)";
    }
}

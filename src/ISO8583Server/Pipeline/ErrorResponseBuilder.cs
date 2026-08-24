using System;
using ISO8583Net.Header;
using ISO8583Net.Message;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Server.Pipeline;

/// <summary>
/// Builds raw, dialect-defined error responses for inbound messages that cannot be
/// routed to a business handler (unknown MTI, missing mandatory fields, etc.).
///
/// These frames are deliberately composed as raw bytes rather than packed via
/// <see cref="ISOMessage.Pack"/>, because they must reproduce the exact wire format the
/// peer produced (for example a bitmap-less "9800" format-error response).
/// </summary>
internal static class ErrorResponseBuilder
{
    private const int FrameLengthHeaderSize = 2;
    private const int BcdMtiLength = 2;

    /// <summary>
    /// Builds a D8 "format error" response frame for an inbound message that was rejected
    /// because its MTI is not defined in the dialect.
    ///
    /// Wire format (matching the observed peer transmission):
    ///   [2-byte big-endian length = 23] [21-byte D8 header, FieldInError="999"] [2-byte BCD "9800"]
    /// </summary>
    /// <param name="request">The rejected inbound message (used to echo source/version fidelity).</param>
    /// <param name="logger">Logger used to construct a header instance.</param>
    /// <returns>The full pre-framed byte array (2-byte LI included), or null if the request
    /// does not carry a D8 header.</returns>
    public static byte[]? BuildD8FormatErrorFrame(ISOMessage request, ILogger logger)
    {
        if (request.Header is not ISOHeaderD8 requestHeader)
            return null;

        var header = new ISOHeaderD8(logger);
        // Echo the sender's source/version back for fidelity, but flag field 999 in error.
        header.MessageSource = requestHeader.MessageSource;
        header.VersionNumber = requestHeader.VersionNumber;
        header.FieldInError = "999";

        int bodyLength = ISOHeaderD8.HeaderLength + BcdMtiLength;
        var body = new byte[bodyLength];

        Array.Copy(header.HeaderData, 0, body, 0, ISOHeaderD8.HeaderLength);

        // MTI "9800" as two BCD bytes (0x98 0x00), no bitmap.
        body[ISOHeaderD8.HeaderLength] = 0x98;
        body[ISOHeaderD8.HeaderLength + 1] = 0x00;

        var frame = new byte[FrameLengthHeaderSize + bodyLength];
        frame[0] = (byte)((bodyLength >> 8) & 0xFF);
        frame[1] = (byte)(bodyLength & 0xFF);
        Array.Copy(body, 0, frame, FrameLengthHeaderSize, bodyLength);

        return frame;
    }
}

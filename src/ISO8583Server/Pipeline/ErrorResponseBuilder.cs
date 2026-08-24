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
/// peer produced (a bitmap-less "9xxx" format-error response).
/// </summary>
internal static class ErrorResponseBuilder
{
    private const int FrameLengthHeaderSize = 2;
    private const int BcdMtiLength = 2;
    private const string HeaderInvalidFieldInError = "999";

    /// <summary>
    /// Builds a D8 "format error" response frame for an inbound message that was rejected
    /// because its MTI is not defined in the dialect (or its header is invalid).
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
        return BuildD8ErrorFrame(request, "9800", HeaderInvalidFieldInError, logger);
    }

    /// <summary>
    /// Builds a D8 field-error response frame for an inbound message with a known MTI whose
    /// first offending field is <paramref name="firstOffendingField"/>.
    ///
    /// Per the D8 spec (§4.3.5, §6.1.2) the response MTI is the request MTI with its first
    /// digit replaced by '9' (e.g. 1200 → 9200), and the header's <c>Field in Error</c>
    /// carries the offending field number zero-padded to three digits (000-128). The frame is
    /// bitmap-less.
    /// </summary>
    /// <param name="request">The rejected inbound message (used to echo source/version fidelity).</param>
    /// <param name="mti">The inbound message type identifier (e.g. "1200").</param>
    /// <param name="firstOffendingField">The first offending field number (000-128).</param>
    /// <param name="logger">Logger used to construct a header instance.</param>
    /// <returns>The full pre-framed byte array (2-byte LI included), or null if the request
    /// does not carry a D8 header.</returns>
    public static byte[]? BuildD8FieldErrorFrame(
        ISOMessage request, string mti, int firstOffendingField, ILogger logger)
    {
        if (request.Header is not ISOHeaderD8)
            return null;

        string errorMti = TransformToFormatErrorMti(mti);
        string fieldInError = NormalizeFieldInError(firstOffendingField);
        return BuildD8ErrorFrame(request, errorMti, fieldInError, logger);
    }

    /// <summary>
    /// Composes the shared bitmap-less D8 error frame: a 21-byte D8 header (echoing the
    /// sender's source/version and carrying <paramref name="fieldInError"/>) followed by a
    /// 2-byte packed-BCD MTI, all prefixed with a 2-byte big-endian length.
    /// </summary>
    private static byte[]? BuildD8ErrorFrame(
        ISOMessage request, string errorMti, string fieldInError, ILogger logger)
    {
        if (request.Header is not ISOHeaderD8 requestHeader)
            return null;

        var header = new ISOHeaderD8(logger);
        // Echo the sender's source/version back for fidelity, but flag the error field.
        header.MessageSource = requestHeader.MessageSource;
        header.VersionNumber = requestHeader.VersionNumber;
        header.FieldInError = fieldInError;

        int bodyLength = ISOHeaderD8.HeaderLength + BcdMtiLength;
        var body = new byte[bodyLength];

        Array.Copy(header.HeaderData, 0, body, 0, ISOHeaderD8.HeaderLength);

        EncodeBcdMti(errorMti, body, ISOHeaderD8.HeaderLength);

        var frame = new byte[FrameLengthHeaderSize + bodyLength];
        frame[0] = (byte)((bodyLength >> 8) & 0xFF);
        frame[1] = (byte)(bodyLength & 0xFF);
        Array.Copy(body, 0, frame, FrameLengthHeaderSize, bodyLength);

        return frame;
    }

    /// <summary>
    /// Replaces the first digit of a well-formed 4-digit MTI with '9' to derive the
    /// format-error response MTI (e.g. "1200" → "9200"). Falls back to "9800" when the
    /// MTI cannot be recognized.
    /// </summary>
    private static string TransformToFormatErrorMti(string mti)
    {
        if (mti is { Length: 4 }
            && IsDigit(mti[0]) && IsDigit(mti[1]) && IsDigit(mti[2]) && IsDigit(mti[3]))
        {
            return "9" + mti.Substring(1);
        }

        return "9800";
    }

    /// <summary>
    /// Formats a field number as the 3-digit "Field in Error" value (000-128), else "999".
    /// </summary>
    private static string NormalizeFieldInError(int fieldNumber)
    {
        return fieldNumber is >= 0 and <= 128
            ? fieldNumber.ToString("D3")
            : HeaderInvalidFieldInError;
    }

    /// <summary>
    /// Encodes a 4-digit MTI into two packed-BCD bytes at <paramref name="offset"/>.
    /// The MTI is guaranteed to be four ASCII digits by the caller paths.
    /// </summary>
    private static void EncodeBcdMti(string mti, byte[] body, int offset)
    {
        body[offset] = (byte)(((mti[0] - '0') << 4) | (mti[1] - '0'));
        body[offset + 1] = (byte)(((mti[2] - '0') << 4) | (mti[3] - '0'));
    }

    private static bool IsDigit(char c) => c is >= '0' and <= '9';
}

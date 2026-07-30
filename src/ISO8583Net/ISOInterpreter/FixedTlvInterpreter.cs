using Microsoft.Extensions.Logging;
using ISO8583Net.Utilities;
using System;
using System.Collections.Generic;
using System.Text;

namespace ISO8583Net.Interpreter
{
    /// <summary>
    /// Interpreter for Fixed TLV encoded fields (e.g. D8 G2B Field 48 Format 2).
    /// Parses a hex string into 2-byte tag + 1-byte length + value entries and
    /// renders a human-readable breakdown for logging.
    /// </summary>
    public class FixedTlvInterpreter : ISOInterpreter
    {
        private readonly int _tagWidthBytes;
        private readonly int _lengthWidthBytes;
        private readonly Dictionary<string, string> _tagDescriptions;

        public FixedTlvInterpreter(
            ILogger logger,
            int tagWidthBytes,
            int lengthWidthBytes,
            Dictionary<string, string> tagDescriptions)
            : base(logger)
        {
            _tagWidthBytes = tagWidthBytes;
            _lengthWidthBytes = lengthWidthBytes;
            _tagDescriptions = tagDescriptions ?? new Dictionary<string, string>();
        }

        public override string ToString(string fieldValue)
        {
            if (string.IsNullOrEmpty(fieldValue))
                return string.Empty;

            byte[] data;
            try
            {
                data = ISOUtils.Hex2Bytes(fieldValue);
            }
            catch
            {
                return "       [Invalid hex data]";
            }

            var sb = new StringBuilder();
            int offset = 0;

            while (offset < data.Length)
            {
                if (offset + _tagWidthBytes + _lengthWidthBytes > data.Length)
                {
                    sb.AppendLine("       [truncated/malformed TLV]");
                    break;
                }

                var tagBytes = new byte[_tagWidthBytes];
                Array.Copy(data, offset, tagBytes, 0, _tagWidthBytes);
                string tag = ISOUtils.Bytes2Hex(tagBytes);
                offset += _tagWidthBytes;

                int length = 0;
                for (int i = 0; i < _lengthWidthBytes; i++)
                {
                    length = (length << 8) | data[offset + i];
                }
                offset += _lengthWidthBytes;

                if (offset + length > data.Length)
                {
                    sb.AppendLine($"       [Tag {tag}] length {length} exceeds remaining data");
                    break;
                }

                var valueBytes = new byte[length];
                Array.Copy(data, offset, valueBytes, 0, length);
                offset += length;

                string valueHex = ISOUtils.Bytes2Hex(valueBytes);
                string valueAscii = TryToAscii(valueBytes);

                _tagDescriptions.TryGetValue(tag, out string description);

                sb.Append("       ");
                sb.Append($"[Tag {tag}]");
                if (!string.IsNullOrEmpty(description))
                    sb.Append($" [{description}]");
                sb.AppendLine($" [Len {length}]");
                sb.AppendLine($"            Hex:  {valueHex}");
                if (valueAscii != null)
                    sb.AppendLine($"            ASCII: {valueAscii}");
            }

            return sb.ToString();
        }

        private static string TryToAscii(byte[] data)
        {
            var sb = new StringBuilder(data.Length);
            bool hasPrintable = false;
            foreach (byte b in data)
            {
                if (b >= 32 && b <= 126)
                {
                    sb.Append((char)b);
                    hasPrintable = true;
                }
                else
                {
                    sb.Append('.');
                }
            }
            return hasPrintable ? sb.ToString() : null;
        }
    }
}

using Microsoft.Extensions.Logging;
using ISO8583Net.Field;
using ISO8583Net.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ISO8583Net.Interpreter
{
    /// <summary>
    /// Interpreter for BER-TLV encoded fields (e.g. D8 G2B Field 55).
    /// Parses a hex string into variable-length tag + length + value entries,
    /// recursively renders constructed tags, and shows human-readable tag
    /// descriptions for logging.
    /// </summary>
    public class BerTlvInterpreter : ISOInterpreter
    {
        private readonly Dictionary<string, string> _tagDescriptions;

        public BerTlvInterpreter(
            ILogger logger,
            Dictionary<string, string> tagDescriptions)
            : base(logger)
        {
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

            var tlv = new BerTLV();
            tlv.ParseAll(data);

            var sb = new StringBuilder();
            foreach (var obj in tlv.ObjectList.Where(o => o.Parent == null))
            {
                AppendObject(sb, obj, 0);
            }

            return sb.ToString();
        }

        private void AppendObject(StringBuilder sb, BerTLVObject obj, int depth)
        {
            string indent = new string(' ', 7 + depth * 4);
            bool constructed = obj.ChildList.Count > 0;

            _tagDescriptions.TryGetValue(obj.TagStr, out string description);

            sb.Append(indent);
            sb.Append($"[Tag {obj.TagStr}]");
            if (!string.IsNullOrEmpty(description))
                sb.Append($" [{description}]");
            sb.AppendLine($" [Len {obj.LengthInt}]");

            if (constructed)
            {
                foreach (var child in obj.ChildList)
                    AppendObject(sb, child, depth + 1);
            }
            else
            {
                string valueHex = ISOUtils.Bytes2Hex(obj.Value);
                sb.AppendLine($"{indent}    Hex:  {valueHex}");

                string valueAscii = TryToAscii(obj.Value);
                if (valueAscii != null)
                    sb.AppendLine($"{indent}    ASCII: {valueAscii}");
            }
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

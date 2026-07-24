using System.Text;
using ISO8583Net.Message;
using ISO8583Net.Packager;
using Microsoft.Extensions.Logging.Abstractions;

if (args.Length < 2)
{
    Console.WriteLine("Usage: hexparse <dialect.json> <hexdump>");
    Console.WriteLine("  dialect.json — full path to ISO 8583 dialect file");
    Console.WriteLine("  hexdump      — hex string with 2-byte length indicator prefix");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine(@"  hexparse C:\tmp\iso8583net\src\ISO8583Net\ISODialects\d8-iso8583.json 002949534F...");
    return 1;
}

string dialectPath = args[0];
string hex = args[1].Replace(" ", "").Replace("\t", "");

if (hex.Length % 2 != 0)
{
    Console.Error.WriteLine("ERROR: Hex must have an even number of digits.");
    return 1;
}

if (!File.Exists(dialectPath))
{
    Console.Error.WriteLine($"ERROR: Dialect file not found: {dialectPath}");
    return 1;
}

// ── Convert hex to bytes ──────────────────────────────────────────
byte[] raw = HexToBytes(hex);
Console.WriteLine($"Input: {raw.Length} bytes");

// ── Hex dump ──────────────────────────────────────────────────────
PrintHexDump(raw);

// ── Strip 2-byte length indicator (big-endian) ────────────────────
int li = (raw[0] << 8) | raw[1];
if (li < 2 || li > raw.Length - 2)
{
    Console.Error.WriteLine($"ERROR: Length indicator {li} is outside valid range [2, {raw.Length - 2}].");
    return 1;
}

byte[] msg = new byte[li];
Array.Copy(raw, 2, msg, 0, li);
Console.WriteLine($"\nLI prefix: 0x{li:X4} = {li} bytes → stripped, message body is {msg.Length} bytes");

// ── Parse ─────────────────────────────────────────────────────────
try
{
    var packager = new ISOMessagePackager(
        NullLogger<ISOMessagePackager>.Instance, dialectPath);
    Console.WriteLine($"Dialect: {dialectPath} ({packager.GetTotalFields()} fields)");

    var isoMsg = new ISOMessage(NullLogger<ISOMessage>.Instance, packager);
    isoMsg.UnPack(msg);

    Console.WriteLine();
    Console.WriteLine("═══ PARSE SUCCESS ═══");
    Console.WriteLine(isoMsg.ToString());
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine("═══ PARSE FAILED ═══");
    Console.Error.WriteLine($"  {ex.GetType().Name}: {ex.Message}");
    return 1;
}

return 0;

// ═══════════════════════════════════════════════════════════════════
//  Helpers
// ═══════════════════════════════════════════════════════════════════

static byte[] HexToBytes(string hex)
{
    var bytes = new byte[hex.Length / 2];
    for (int i = 0; i < hex.Length; i += 2)
        bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
    return bytes;
}

static void PrintHexDump(byte[] data)
{
    var sb = new StringBuilder();
    for (int off = 0; off < data.Length; off += 16)
    {
        int rowLen = Math.Min(16, data.Length - off);
        sb.Append($"{off:X4}  ");
        for (int i = 0; i < 16; i++)
        {
            if (i < rowLen) sb.Append($"{data[off + i]:X2} ");
            else sb.Append("   ");
            if (i == 7) sb.Append(' ');
        }
        sb.Append(" |");
        for (int i = 0; i < rowLen; i++)
        {
            byte b = data[off + i];
            sb.Append(b is >= 32 and < 127 ? (char)b : '.');
        }
        sb.Append('|');
        sb.AppendLine();
    }
    Console.Write(sb.ToString());
}

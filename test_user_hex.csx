using ISO8583Net.Message;
using ISO8583Net.Packager;
using ISO8583Net.Utilities;

// User's hex dump (with 010F length prefix)
const string hexDump =
    "010F" +
    "49534F383538332D313939333031313030303030" +
    "301420767425D58CE1A100" +
    "1046337101000005" +
    "0500000000000050000000000050000723" +
    "1446096100000000" +
    "35702607231446092811" +
    "04283130303032303638" +
    "2020202004" +
    "0040215812260723000000" +
    "0050000645700006498750" +
    "3632303431343536333831333039373838" +
    "3634393837353036383439383735303030" +
    "30343333343732" +
    "2846554D494E4F523033332D412E205341" +
    "4841524F56412032304E455720594F524B" +
    "20202020205553" +
    "0037" +
    "C0100108" +
    "C102102033303036303139333733383832" +
    "3430C1031D" +
    "2020202020202020202020202020202020" +
    "2020202020202020202030" +
    "09780978" +
    "1E11" +
    "0056381326072314460906457000";

string dialectPath = Path.GetFullPath(Path.Combine(
    AppDomain.CurrentDomain.BaseDirectory,
    "..", "..", "..", "..", "..",
    "src", "ISO8583Net", "ISODialects", "d8-iso8583.json"));

Console.WriteLine($"Dialect path: {dialectPath}");
Console.WriteLine($"Dialect exists: {File.Exists(dialectPath)}");

byte[] packedBytes = ISOUtils.Hex2Bytes(hexDump);
Console.WriteLine($"Total bytes: {packedBytes.Length}");

// Strip length prefix (2 bytes)
byte[] msgBytes = packedBytes[2..];
Console.WriteLine($"Message bytes (after stripping 010F): {msgBytes.Length}");

var packager = new ISOMessagePackager(null!, dialectPath);
var msg = new ISOMessage(null!, packager);

try
{
    msg.UnPack(msgBytes);
    Console.WriteLine("SUCCESS!");
    Console.WriteLine($"MTI: {msg.GetFieldValue(0)}");
    Console.WriteLine($"F2 (PAN): {msg.GetFieldValue(2)}");
    Console.WriteLine($"F48: {msg.GetFieldValue(48)}");
}
catch (Exception ex)
{
    Console.WriteLine($"FAILED: {ex.GetType().Name}: {ex.Message}");
    if (ex.InnerException != null)
        Console.WriteLine($"  Inner: {ex.InnerException.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
}

# iso8583net

A .net library for building and parsing iso8583 financial messages

The project home page can be found [here](https://nikmes.github.io/iso8583net/)

## Usage Example
``` csharp
static void Main(string[] args)
{
    ISOMessagePackager p = new ISOMessagePackager(logger);

    byte[] packedBytes = new byte[2048];

    ISOMessage m = new ISOMessage(logger, p);

    m.SetValue(000, "0100");
    m.SetValue(002, "4000400040004001");
    m.SetValue(003, "300000");
    m.SetValue(004, "000000002900");
    m.SetValue(007, "1234567890");
    m.SetValue(011, "123456");
    m.SetValue(012, "193012");
    m.SetValue(014, "1219");
    m.SetValue(018, "5999");
    m.SetValue(019, "196");
    m.SetValue(022, "9010");
    m.SetValue(025, "23");
    m.SetValue(037, "123456789012");
    m.SetValue(062, 01, "Y");
    m.SetValue(063, 01, "1222");
    m.SetValue(063, 03, "9999");
    m.SetValue(064, "ABCDEF1234567890");
    m.SetValue(070, "123");
    m.SetValue(132, "ABABABAB");

    Log.Debug(m.ToString());

    byte[] pBytes = m.Pack();

    Log.Information("Bytes: \n" + ISOUtils.PrintHex(pBytes, pBytes.Length));

    ISOMessage u = new ISOMessage(logger, p);

    u.UnPack(pBytes);

    Log.Debug(u.ToString());
}
```

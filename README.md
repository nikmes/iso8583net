# iso8583net

A highly configurable .net library for building and parsing iso8583 financial messages. 

The project home page can be found [here](https://nikmes.github.io/iso8583net/)

## Usage Example
```csharp
static void Main(string[] args)
{
    ISOMessagePackager mPackager = new ISOMessagePackager(logger);
    
    byte[] packedBytes = new byte[2048];
    
    ISOMessage m = new ISOMessage(logger, mPackager);
    
    m.Set(0, "0100");
    m.Set(2, "4000400040004001");
    m.Set(3, "300000");
    m.Set(4, "000000002900");
    m.Set(7, "1234567890");
    m.Set(11, "123456");
    m.Set(12, "193012");
    m.Set(14, "1219");
    m.Set(18, "5999");
    m.Set(19, "196");
    m.Set(22, "9010");
    m.Set(25, "23");
    m.Set(37, "123456789012");
    m.Set(62, 1, "Y");
    m.Set(63, 1, "1222");
    m.Set(63, 3, "9999");
    m.Set(64, "ABCDEF1234567890");
    m.Set(70, "123");
    m.Set(132, "ABABABAB");
    
    Log.Debug(m.ToString());
    
    byte[] pBytes = m.Pack();
    
    Log.Information("Bytes: \n" + ISOUtils.PrintHex(pBytes, pBytes.Length));
    
    ISOMessage u = new ISOMessage(logger, mPackager);
    
    u.UnPack(pBytes);
    
    Log.Debug(u.ToString());
}
```

## Sample Trace

![image](iso8583net/site/images/output.png)

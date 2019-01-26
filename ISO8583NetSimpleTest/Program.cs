using System;
using ISO8583Net.Message;
using ISO8583Net.Packager;
using ISO8583Net.Utilities;
using Serilog;
using Microsoft.Extensions.Logging;


namespace ISO8583NetSimpleTest
{
    class Program
    {
        static public Serilog.Core.Logger Log = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}").
                                            WriteTo.RollingFile("out.log", outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}").
                                            CreateLogger();

        static private ILoggerFactory loggerFactory = new LoggerFactory().AddSerilog(Log);

        static public Microsoft.Extensions.Logging.ILogger logger = loggerFactory.CreateLogger<Program>();



        static void Main(string[] args)
        {
            ISOMessagePackager p = new ISOMessagePackager(Program.logger);

            byte[] packedBytes = new byte[2048];

            ISOMessage m = new ISOMessage(Program.logger, p);

            m.SetFieldValue(000, "0100");
            m.SetFieldValue(002, "4000400040004001");
            m.SetFieldValue(003, "300000");
            m.SetFieldValue(004, "000000002900");
            m.SetFieldValue(007, "1234567890");
            m.SetFieldValue(011, "123456");
            m.SetFieldValue(012, "193012");
            m.SetFieldValue(014, "1219");
            m.SetFieldValue(018, "5999");
            m.SetFieldValue(019, "196");
            m.SetFieldValue(022, "9010");
            m.SetFieldValue(025, "23");
            m.SetFieldValue(037, "123456789012");
            m.SetFieldValue(062, 01, "Y");
            m.SetFieldValue(063, 01, "1222");
            m.SetFieldValue(063, 03, "9999");
            m.SetFieldValue(064, "ABCDEF1234567890");
            m.SetFieldValue(070, "123");
            m.SetFieldValue(132, "ABABABAB");

            Log.Debug(m.ToString());

            byte[] pBytes = m.Pack();

            Log.Information("Bytes: \n" + ISOUtils.PrintHex(pBytes, pBytes.Length));

            ISOMessage u = new ISOMessage(logger, p);

            u.UnPack(pBytes);

            Log.Debug(u.ToString());
        }
    }
}

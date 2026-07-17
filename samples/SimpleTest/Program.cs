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
                                            //WriteTo.RollingFile("out.log", outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}").
                                            CreateLogger();

        static private readonly ILoggerFactory loggerFactory = new LoggerFactory().AddSerilog(Log);

        static public Microsoft.Extensions.Logging.ILogger logger = loggerFactory.CreateLogger<Program>();



        static void Main(string[] args)
        {
            ISOMessagePackager mPackager = new ISOMessagePackager(logger);


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
    }
}

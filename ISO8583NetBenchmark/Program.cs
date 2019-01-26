using System;
using ISO8583Net.Message;
using ISO8583Net.Packager;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Csv;
using System.Linq;
using Serilog;

namespace ISO8583NetBenchmark
{
        public class Config : ManualConfig
        {
            public Config()
            {
                Add(new ConsoleLogger());
                Add(CsvMeasurementsExporter.Default);
                //Add(RPlotExporter.Default);
                Add(DefaultConfig.Instance.GetColumnProviders().ToArray());
                //Add(MarkdownExporter.Default);
                //Add(HtmlExporter.Default);
            }
        }
        [SimpleJob(RunStrategy.Throughput, targetCount: 20, id: "MonitoringJob")]
        //[MinColumn, Q1Column, Q3Column, MaxColumn]
        public class ISOUtilsTest
        {
            private byte[] packedBytes;

            static private ILoggerFactory loggerFactory;

            static public Microsoft.Extensions.Logging.ILogger logger;

            static public Serilog.Core.Logger Log;

            //[Params("NIKMES","NICHOLAS MESSARITIS")]
            public string asciiString { get; set; }

            private ISOMessagePackager mPackager;


            public ISOUtilsTest()
            {
                packedBytes = new byte[2048];

                Log = new LoggerConfiguration().MinimumLevel.Fatal().
                                                //WriteTo.RollingFile("out.log", outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}").
                                                CreateLogger();

                loggerFactory = new LoggerFactory().AddSerilog(Log);

                logger = loggerFactory.CreateLogger<Program>();

                mPackager = new ISOMessagePackager(logger); // initialize from default visa packager that is embeded as a resource in the library
            }

            [Benchmark]
            public void PU1STB()
            {

                ISOMessage m = new ISOMessage(logger, mPackager);

                m.SetValue(000, "0100");
                m.SetValue(002, "40004000400040001");
                m.SetValue(003, "000000");
                m.SetValue(004, "000000002900");
                m.SetValue(007, "1231231233");
                m.SetValue(011, "123123");
                m.SetValue(012, "193012");
                m.SetValue(014, "1219");
                m.SetValue(018, "5999");
                m.SetValue(019, "196");
                m.SetValue(022, "9010");
                m.SetValue(025, "23");
                m.SetValue(037, "123123123123");

                packedBytes = m.Pack();

                ISOMessage uM = new ISOMessage(logger, mPackager);

                uM.UnPack(packedBytes);
            }

            [Benchmark]
            public void PU2NDB()
            {
                ISOMessage m = new ISOMessage(logger, mPackager);

                m.SetValue(000, "0100");
                m.SetValue(002, "40004000400040001");
                m.SetValue(003, "000000");
                m.SetValue(004, "000000002900");
                m.SetValue(007, "1231231233");
                m.SetValue(011, "123123");
                m.SetValue(012, "193012");
                m.SetValue(014, "1219");
                m.SetValue(018, "5999");
                m.SetValue(019, "196");
                m.SetValue(022, "9010");
                m.SetValue(025, "23");
                m.SetValue(037, "123123123123");
                m.SetValue(070, "123");

                packedBytes = m.Pack();

                ISOMessage uM = new ISOMessage(logger, mPackager);

                uM.UnPack(packedBytes);

            }

            //[Benchmark]
            public void PU2NDBS()
            {
                ISOMessage m = new ISOMessage(logger, mPackager);
                m.SetValue(000, "0100");
                m.SetValue(002, "40004000400040001");
                m.SetValue(003, "000000");
                m.SetValue(004, "000000002900");
                m.SetValue(007, "1231231233");
                m.SetValue(011, "123123");
                m.SetValue(012, "193012");
                m.SetValue(014, "1219");
                m.SetValue(018, "5999");
                m.SetValue(019, "196");
                m.SetValue(022, "9010");
                m.SetValue(025, "23");
                m.SetValue(037, "123123123123");
                m.SetValue(062, 01, "Y");
                m.SetValue(063, 01, "1222");
                m.SetValue(063, 03, "9999");
                m.SetValue(070, "123");

                // byte[] packedBytes = m.Pack();
                m.Pack();
                ISOMessage uM = new ISOMessage(logger, mPackager);
                uM.UnPack(packedBytes);
            }

            //[Benchmark]
            //public void P1STB()
            //{
            //    ISOMessage m = new ISOMessage(logger, mPackager);

            //    m.SetFieldValue(000, "0100");
            //    m.SetFieldValue(002, "40004000400040001");
            //    m.SetFieldValue(003, "000000");
            //    m.SetFieldValue(004, "000000002900");
            //    m.SetFieldValue(007, "1231231233");
            //    m.SetFieldValue(011, "123123");
            //    m.SetFieldValue(012, "193012");
            //    m.SetFieldValue(014, "1219");
            //    m.SetFieldValue(018, "5999");
            //    m.SetFieldValue(019, "196");
            //    m.SetFieldValue(022, "9010");
            //    m.SetFieldValue(025, "23");
            //    m.SetFieldValue(037, "123123123123");
            //    byte[] packedBytes = m.Pack();
            //}

            //[Benchmark]
            //public void P2NDB()
            //{
            //    ISOMessage m = new ISOMessage(logger, mPackager);

            //    m.SetFieldValue(000, "0100");
            //    m.SetFieldValue(002, "40004000400040001");
            //    m.SetFieldValue(003, "000000");
            //    m.SetFieldValue(004, "000000002900");
            //    m.SetFieldValue(007, "1231231233");
            //    m.SetFieldValue(011, "123123");
            //    m.SetFieldValue(012, "193012");
            //    m.SetFieldValue(014, "1219");
            //    m.SetFieldValue(018, "5999");
            //    m.SetFieldValue(019, "196");
            //    m.SetFieldValue(022, "9010");
            //    m.SetFieldValue(025, "23");
            //    m.SetFieldValue(037, "123123123123");
            //    m.SetFieldValue(070, "123");
            //    byte[] packedBytes = m.Pack();
            //}

            //[Benchmark]
            //public void P2NDBS()
            //{
            //    ISOMessage m = new ISOMessage(logger, mPackager);
            //    m.SetFieldValue(000, "0100");
            //    m.SetFieldValue(002, "40004000400040001");
            //    m.SetFieldValue(003, "000000");
            //    m.SetFieldValue(004, "000000002900");
            //    m.SetFieldValue(007, "1231231233");
            //    m.SetFieldValue(011, "123123");
            //    m.SetFieldValue(012, "193012");
            //    m.SetFieldValue(014, "1219");
            //    m.SetFieldValue(018, "5999");
            //    m.SetFieldValue(019, "196");
            //    m.SetFieldValue(022, "9010");
            //    m.SetFieldValue(025, "23");
            //    m.SetFieldValue(037, "123123123123");
            //    m.SetFieldValue(062, 01, "Y");
            //    m.SetFieldValue(063, 01, "1222");
            //    m.SetFieldValue(063, 03, "9999");
            //    m.SetFieldValue(070, "123");
            //    byte[] packedBytes = m.Pack();
            //}

            public static void ascii2bytesv1(string strASCIIString, byte[] packedBytes)
            {
                int len = strASCIIString.Length;

                for (int i = 0; i < len; i++)
                {
                    packedBytes[i] = (byte)strASCIIString[i];
                }
            }


            public static void ascii2bytesv2(string strASCIIString, Span<byte> packedBytes)
            {
                int len = strASCIIString.Length;

                for (int i = 0; i < len; i++)
                {
                    packedBytes[i] = (byte)strASCIIString[i];
                }
            }

            //[Benchmark]
            public void UsingSpan() => ascii2bytesv1(asciiString, packedBytes);

            //[Benchmark]
            public void UsingArray() => ascii2bytesv2(asciiString, packedBytes);


        }

        class Program
        {
            static void Main(string[] args)
            {
                ISOUtilsTest myTest = new ISOUtilsTest();

                var summary = BenchmarkRunner.Run<ISOUtilsTest>(new Config());

            }
        }
}

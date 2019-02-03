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
        [SimpleJob(RunStrategy.Throughput, targetCount: 60, id: "MonitoringJob")]
        //[MinColumn, Q1Column, Q3Column, MaxColumn]
        public class ISOUtilsTest
        {
            private byte[] packedBytes;

            static private ILoggerFactory loggerFactory;

            static public Microsoft.Extensions.Logging.ILogger logger;

            static public Serilog.Core.Logger Log;

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
            public void PackUnpack1StBitmapOnly()
            {

                ISOMessage m = new ISOMessage(logger, mPackager);

                m.Set(0, "0100");
                m.Set(2, "40004000400040001");
                m.Set(3, "000000");
                m.Set(4, "000000002900");
                m.Set(7, "1231231233");
                m.Set(11, "123123");
                m.Set(12, "193012");
                m.Set(14, "1219");
                m.Set(18, "5999");
                m.Set(19, "196");
                m.Set(22, "9010");
                m.Set(25, "23");
                m.Set(37, "123123123123");

                packedBytes = m.Pack();

                ISOMessage uM = new ISOMessage(logger, mPackager);

                uM.UnPack(packedBytes);
            }

            [Benchmark]
            public void PackUnpack2ndBitmap()
            {
                ISOMessage m = new ISOMessage(logger, mPackager);

                m.Set(0, "0100");
                m.Set(2, "40004000400040001");
                m.Set(3, "000000");
                m.Set(4, "000000002900");
                m.Set(7, "1231231233");
                m.Set(11, "123123");
                m.Set(12, "193012");
                m.Set(14, "1219");
                m.Set(18, "5999");
                m.Set(19, "196");
                m.Set(22, "9010");
                m.Set(25, "23");
                m.Set(37, "123123123123");
                m.Set(70, "123");

                packedBytes = m.Pack();

                ISOMessage uM = new ISOMessage(logger, mPackager);

                uM.UnPack(packedBytes);

            }

            [Benchmark]
            public void PackUnpack2ndBitmapWithBitmapField()
            {
                ISOMessage m = new ISOMessage(logger, mPackager);

                m.Set(0, "0100");
                m.Set(2, "40004000400040001");
                m.Set(3, "000000");
                m.Set(4, "000000002900");
                m.Set(7, "1231231233");
                m.Set(11, "123123");
                m.Set(12, "193012");
                m.Set(14, "1219");
                m.Set(18, "5999");
                m.Set(19, "196");
                m.Set(22, "9010");
                m.Set(25, "23");
                m.Set(37, "123123123123");
                m.Set(62, 01, "Y");
                m.Set(63, 01, "1222");
                m.Set(63, 03, "9999");
                m.Set(70, "123");

                byte[] packedBytes = m.Pack();

                ISOMessage uM = new ISOMessage(logger, mPackager);

                uM.UnPack(packedBytes);
            }

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

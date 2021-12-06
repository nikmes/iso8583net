using System;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Logging;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Csv;
using System.Linq;
using Serilog;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Diagnostics.Windows.Configs;

namespace ISO8583NetBenchmark
{
    public class Config : ManualConfig
    {
        public Config()
        {
            Add(new ConsoleLogger());
            Add(CsvMeasurementsExporter.Default);
            Add(RPlotExporter.Default);
            Add(DefaultConfig.Instance.GetColumnProviders().ToArray());
            //Add(MarkdownExporter.Default);
            //Add(HtmlExporter.Default);

            //Add(Job.Default
            //.With(new GcMode()
            //{
            //    Force = false // tell BenchmarkDotNet not to force GC collections after every iteration
            //}));
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            
            //var summary = BenchmarkRunner.Run<ISOUtilsTest>(new Config());
            var summary = BenchmarkRunner.Run<BitmapTest>(new Config());
            //var summary = BenchmarkRunner.Run<HexUtilsTest>(new Config());

        }
    }
}

using System;
using System.Linq;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;

namespace ISO8583NetBenchmark
{
    /// <summary>
    /// Shared benchmark configuration: CSV + Markdown + RPlot exporters,
    /// memory diagnostics, default column providers.
    /// </summary>
    public class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddExporter(CsvMeasurementsExporter.Default);
            AddExporter(MarkdownExporter.GitHub);
            AddExporter(RPlotExporter.Default);
            AddExporter(HtmlExporter.Default);
            AddLogger(new ConsoleLogger());
            AddColumnProvider(DefaultConfig.Instance.GetColumnProviders().ToArray());
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // Run benchmarks with shared config, honoring --filter and other CLI args
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new BenchmarkConfig());
        }
    }
}

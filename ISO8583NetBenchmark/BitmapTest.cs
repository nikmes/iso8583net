using ISO8583Net.Message;
using ISO8583Net.Packager;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Attributes;
using ISO8583Net.Utilities;
using BenchmarkDotNet.Diagnostics.Windows.Configs;

namespace ISO8583NetBenchmark
{
    [MemoryDiagnoser]
    [EtwProfiler] //Create traces for perfview
    [SimpleJob(RunStrategy.Throughput, targetCount: 60, id: "MonitoringJob")]
    public class BitmapTest
    {        
        Microsoft.Extensions.Logging.ILogger logger;
        static private ISOMessagePackager mPackager;
        ISOMessage m;

        [GlobalSetup]
        public void GlobalSetup()
        {
            
            logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<BitmapTest>();
            mPackager = new ISOMessagePackager(logger); // initialize from default visa packager that is embeded as a resource in the library
            m = new ISOMessage(logger, mPackager);

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
        }

        [Benchmark(Baseline = true)]
        public bool[] IsBitSet()
        {
            bool[] fields = new bool[196]; 
            var bitmap = m.GetField(1) as ISO8583Net.Field.ISOFieldBitmap;
            int length = bitmap.GetByteArray().Length * 8;
            for (int i = 0; i < length; i++)
            {
                if (i != 1 && i != 65)
                {
                    fields[i] = bitmap.BitIsSet(i);
                }
            }
            return fields;
        }

        [Benchmark]
        public bool[] FieldEnumerator()
        {
            bool[] fields = new bool[196];
            var bitmap = m.GetField(1) as ISO8583Net.Field.ISOFieldBitmap;
            var enumerator = bitmap.GetByteArray().GetFieldIdEnumerator();
            foreach (var item in enumerator)
            {
                fields[item] = true;
            }
            return fields;
        }

        [Benchmark]
        public bool[] GetSetFields()
        {
            bool[] fields = new bool[196];
            var bitmap = m.GetField(1) as ISO8583Net.Field.ISOFieldBitmap;
            var setFields = bitmap.GetSetFields();
            for (int i = 0; i < setFields.Length; i++)
            {
                fields[setFields[i]] = true;
            }
            
            return fields;
        }


    }
}

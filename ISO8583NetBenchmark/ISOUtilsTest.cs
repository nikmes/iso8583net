using ISO8583Net.Message;
using ISO8583Net.Packager;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;

namespace ISO8583NetBenchmark
{
    [MemoryDiagnoser]
    //[EtwProfiler] //Create traces for perfview
    //[SimpleJob(RuntimeMoniker.NetCoreApp21)]
    //[SimpleJob(RuntimeMoniker.NetCoreApp30)]
    [SimpleJob(RunStrategy.Throughput, targetCount: 30, id: "MonitoringJob")]
    //[MinColumn, Q1Column, Q3Column, MaxColumn]
    public class HexUtilsTest
    {
        private byte[] bytes;
        
        [GlobalSetup]
        public void GlobalSetup()
        {
            string stringhex = "29001234567890123456193012121959";
            bytes = ISO8583Net.Utilities.ISOUtils.Hex2Bytes(stringhex);
        }

        [Benchmark]
        public string Bytes2Hex()
        {
            return ISO8583Net.Utilities.ISOUtils.Bytes2HexOld(bytes, bytes.Length);            
        }

        [Benchmark]
        public string Bytes2Hex2()
        {            
            return ISO8583Net.Utilities.ISOUtils.Bytes2Hex(bytes, bytes.Length);
        }

    }
    [MemoryDiagnoser]
    [EtwProfiler] //Create traces for perfview
    //[SimpleJob(RuntimeMoniker.NetCoreApp21)]
    //[SimpleJob(RuntimeMoniker.NetCoreApp30)]
    [SimpleJob(RunStrategy.Throughput, targetCount: 30, id: "MonitoringJob")]
    //[MinColumn, Q1Column, Q3Column, MaxColumn]
    public class ISOUtilsTest
    {
        private byte[] packedBytes;

        Microsoft.Extensions.Logging.ILogger logger;
        static private ISOMessagePackager mPackager;

        [GlobalSetup]
        public void GlobalSetup()
        {
            packedBytes = new byte[2048];

            logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<ISOUtilsTest>();

            mPackager = new ISOMessagePackager(logger); // initialize from default visa packager that is embeded as a resource in the library
        }

        [Benchmark]
        public void PackUnpack1stBmap()
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
        public void PackUnpack2ndBmap()
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
        public void PackUnpack3rdBmap()
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
            //m.Set(62, 01, "Y");
            //m.Set(63, 01, "1222");
            //m.Set(63, 03, "9999");
            m.Set(70, "123");
            m.Set(132, "ABABABAB");

            byte[] packedBytes = m.Pack();

            ISOMessage uM = new ISOMessage(logger, mPackager);

            uM.UnPack(packedBytes);
        }

        [Benchmark]
        public void PackUnpack1stBmapPool()
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

            packedBytes = m.PackPooled();

            ISOMessage uM = new ISOMessage(logger, mPackager);

            uM.UnPack(packedBytes);
        }

        [Benchmark]
        public void PackOnly1stBmap()
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
        }

        [Benchmark]
        public void PackOnly1stBmapPool()
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

            packedBytes = m.PackPooled();
        }

    }
}

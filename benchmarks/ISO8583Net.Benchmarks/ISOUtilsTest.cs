using System;
using BenchmarkDotNet.Attributes;
using ISO8583Net.Utilities;

namespace ISO8583NetBenchmark
{
    /// <summary>
    /// Low-level conversion utility benchmarks.
    /// </summary>
    [MemoryDiagnoser]
    [MarkdownExporter]
    public class ConversionBenchmarks
    {
        private byte[] _hexBytes16;
        private byte[] _hexBytes64;
        private string _hexString16;
        private string _hexString64;
        private string _numericString16;
        private string _asciiString32;

        [GlobalSetup]
        public void Setup()
        {
            _hexString16 = "0123456789ABCDEF";
            _hexString64 = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
            _hexBytes16 = ISOUtils.Hex2Bytes(_hexString16);
            _hexBytes64 = ISOUtils.Hex2Bytes(_hexString64);
            _numericString16 = "0000000000012345";
            _asciiString32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ012345";
        }

        [Benchmark] public byte[] Hex2Bytes_16() => ISOUtils.Hex2Bytes(_hexString16);
        [Benchmark] public byte[] Hex2Bytes_64() => ISOUtils.Hex2Bytes(_hexString64);
        [Benchmark] public string Bytes2Hex_8() => ISOUtils.Bytes2Hex(_hexBytes16, _hexBytes16.Length);
        [Benchmark] public string Bytes2Hex_32() => ISOUtils.Bytes2Hex(_hexBytes64, _hexBytes64.Length);
        [Benchmark] public byte[] Ascii2Bcd_16() { byte[] buf = new byte[16]; int i = 0; ISOUtils.Ascii2Bcd(_numericString16, buf, ref i, ISO8583Net.Types.ISOFieldPadding.LEFT); return buf; }
        [Benchmark] public string Bcd2Ascii_16() { byte[] buf = new byte[16]; int i = 0; ISOUtils.Ascii2Bcd(_numericString16, buf, ref i, ISO8583Net.Types.ISOFieldPadding.LEFT); int j = 0; return ISOUtils.Bcd2Ascii(buf, ref j, ISO8583Net.Types.ISOFieldPadding.LEFT, 16); }
        [Benchmark] public byte[] Ascii2Bytes_32() { byte[] buf = new byte[64]; int i = 0; ISOUtils.Ascii2Bytes(_asciiString32, buf, ref i); return buf; }
        [Benchmark] public string Bytes2Ascii_32() { byte[] buf = new byte[64]; int i = 0; ISOUtils.Ascii2Bytes(_asciiString32, buf, ref i); int j = 0; return ISOUtils.Bytes2Ascii(buf, ref j, 32); }
        [Benchmark] public byte[] HexToByteArray_32() => ISOUtils.HexToByteArray(_hexString64);
    }
}

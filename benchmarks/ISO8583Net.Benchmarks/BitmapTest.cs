using ISO8583Net.Message;
using ISO8583Net.Packager;
using ISO8583Net.Utilities;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using ISO8583Net.Field;

namespace ISO8583NetBenchmark
{
    /// <summary>
    /// Bitmap-level operations: bit checking, field enumeration, GetSetFields.
    /// </summary>
    [MemoryDiagnoser]
    [MarkdownExporter]
    public class BitmapBenchmarks
    {
        private ISOMessagePackager _packager;
        private ISOMessage _message;
        private ISOFieldBitmap _bitmap;

        [GlobalSetup]
        public void Setup()
        {
            var logger = NullLogger.Instance;
            _packager = new ISOMessagePackager(logger);
            _message = new ISOMessage(logger, _packager);

            // Fill a typical authorization request with primary+secondary bitmap fields
            _message.Set(0, "0100");
            _message.Set(2, "4000400040004001");
            _message.Set(3, "300000");
            _message.Set(4, "000000002900");
            _message.Set(7, "1234567890");
            _message.Set(11, "123456");
            _message.Set(12, "193012");
            _message.Set(14, "1219");
            _message.Set(18, "5999");
            _message.Set(19, "196");
            _message.Set(22, "9010");
            _message.Set(25, "23");
            _message.Set(37, "123456789012");
            _message.Set(64, "ABCDEF1234567890");
            _message.Set(70, "123");
            _message.Set(132, "ABABABAB");

            _bitmap = (ISOFieldBitmap)_message.GetField(1);
        }

        [Benchmark(Baseline = true)]
        public bool[] IsBitSet_Loop()
        {
            bool[] fields = new bool[196];
            int length = _bitmap.GetByteArray().Length * 8;
            for (int i = 0; i < length; i++)
            {
                if (i != 1 && i != 65)
                    fields[i] = _bitmap.BitIsSet(i);
            }
            return fields;
        }

        [Benchmark]
        public bool[] FieldIdEnumerator()
        {
            bool[] fields = new bool[196];
            foreach (var id in _bitmap.GetByteArray().GetFieldIdEnumerator())
                fields[id] = true;
            return fields;
        }

        [Benchmark]
        public bool[] GetSetFields_Alloc()
        {
            bool[] fields = new bool[196];
            int[] setFields = _bitmap.GetSetFields();
            for (int i = 0; i < setFields.Length; i++)
                fields[setFields[i]] = true;
            return fields;
        }

        [Benchmark]
        public bool[] BitEnumerator()
        {
            bool[] result = new bool[196];
            int i = 0;
            foreach (var b in _bitmap.GetByteArray().GetBitEnumerator())
            {
                if (i < 196) result[i] = b;
                i++;
            }
            return result;
        }

        [Benchmark]
        public string ToHumanReadable() => _bitmap.ToHumanReadable("  ");

        [Benchmark]
        public string ToHexString() => _bitmap.ToHexString();

        [Benchmark]
        public byte[] GetByteArray() => _bitmap.GetByteArray();

        [Benchmark]
        public string ToString_() => _bitmap.ToString();
    }
}

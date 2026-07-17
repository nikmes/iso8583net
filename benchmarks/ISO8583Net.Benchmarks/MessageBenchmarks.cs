using System;
using ISO8583Net.Message;
using ISO8583Net.Packager;
using ISO8583Net.Utilities;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;

namespace ISO8583NetBenchmark
{
    /// <summary>
    /// End-to-end message Pack/Unpack round-trip benchmarks across all bitmap levels.
    /// This is the primary benchmark suite for measuring throughput before/after improvements.
    /// </summary>
    [MemoryDiagnoser]
    [MarkdownExporter]
    public class MessageRoundtripBenchmarks
    {
        private ISOMessagePackager _packager;
        private byte[] _packed1Bmp;
        private byte[] _packed2Bmp;
        private byte[] _packed3Bmp;
        private byte[] _packedSubfields;

        [GlobalSetup]
        public void Setup()
        {
            var logger = NullLogger.Instance;
            _packager = new ISOMessagePackager(logger);

            // ── Primary bitmap only (13 fields) ──────────────────────────
            {
                var m = new ISOMessage(logger, _packager);
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
                _packed1Bmp = m.Pack();
            }

            // ── Primary + Secondary bitmap (14 fields) ───────────────────
            {
                var m = new ISOMessage(logger, _packager);
                m.Set(0, "0110");
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
                m.Set(64, "ABCDEF1234567890");
                m.Set(70, "123");
                _packed2Bmp = m.Pack();
            }

            // ── Triple bitmap (15 fields) ────────────────────────────────
            {
                var m = new ISOMessage(logger, _packager);
                m.Set(0, "0110");
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
                m.Set(64, "ABCDEF1234567890");
                m.Set(70, "123");
                m.Set(132, "ABABABAB");
                _packed3Bmp = m.Pack();
            }

            // ── With bitmap sub-fields (F62, F63) ────────────────────────
            {
                var m = new ISOMessage(logger, _packager);
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
                _packedSubfields = m.Pack();
            }
        }

        // ── Full round-trip: Pack + Unpack ──────────────────────────────

        [Benchmark(Baseline = true)]
        public byte[] PackUnpack_1stBitmap()
        {
            var m = BuildMessage1Bmp();
            var packed = m.Pack();
            var u = new ISOMessage(NullLogger.Instance, _packager);
            u.UnPack(packed);
            return packed;
        }

        [Benchmark]
        public byte[] PackUnpack_2ndBitmap()
        {
            var m = BuildMessage2Bmp();
            var packed = m.Pack();
            var u = new ISOMessage(NullLogger.Instance, _packager);
            u.UnPack(packed);
            return packed;
        }

        [Benchmark]
        public byte[] PackUnpack_3rdBitmap()
        {
            var m = BuildMessage3Bmp();
            var packed = m.Pack();
            var u = new ISOMessage(NullLogger.Instance, _packager);
            u.UnPack(packed);
            return packed;
        }

        [Benchmark]
        public byte[] PackUnpack_WithSubfields()
        {
            var m = BuildMessageSubfields();
            var packed = m.Pack();
            var u = new ISOMessage(NullLogger.Instance, _packager);
            u.UnPack(packed);
            return packed;
        }

        [Benchmark]
        public byte[] PackUnpack_1stBitmap_Pooled()
        {
            var m = BuildMessage1Bmp();
            var packed = m.PackPooled();
            var u = new ISOMessage(NullLogger.Instance, _packager);
            u.UnPack(packed);
            return packed;
        }

        // ── Pack only ───────────────────────────────────────────────────

        [Benchmark]
        public byte[] PackOnly_1stBitmap() => BuildMessage1Bmp().Pack();

        [Benchmark]
        public byte[] PackOnly_1stBitmap_Pooled() => BuildMessage1Bmp().PackPooled();

        [Benchmark]
        public byte[] PackOnly_2ndBitmap() => BuildMessage2Bmp().Pack();

        [Benchmark]
        public byte[] PackOnly_3rdBitmap() => BuildMessage3Bmp().Pack();

        [Benchmark]
        public byte[] PackOnly_WithSubfields() => BuildMessageSubfields().Pack();

        // ── Unpack only ─────────────────────────────────────────────────

        [Benchmark]
        public ISOMessage UnpackOnly_1stBitmap()
        {
            var m = new ISOMessage(NullLogger.Instance, _packager);
            m.UnPack(_packed1Bmp);
            return m;
        }

        [Benchmark]
        public ISOMessage UnpackOnly_2ndBitmap()
        {
            var m = new ISOMessage(NullLogger.Instance, _packager);
            m.UnPack(_packed2Bmp);
            return m;
        }

        [Benchmark]
        public ISOMessage UnpackOnly_3rdBitmap()
        {
            var m = new ISOMessage(NullLogger.Instance, _packager);
            m.UnPack(_packed3Bmp);
            return m;
        }

        [Benchmark]
        public ISOMessage UnpackOnly_WithSubfields()
        {
            var m = new ISOMessage(NullLogger.Instance, _packager);
            m.UnPack(_packedSubfields);
            return m;
        }

        // ── ToString (debug/monitoring) ─────────────────────────────────

        [Benchmark]
        public string ToString_1stBitmap()
        {
            var m = new ISOMessage(NullLogger.Instance, _packager);
            m.UnPack(_packed1Bmp);
            return m.ToString();
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private ISOMessage BuildMessage1Bmp()
        {
            var m = new ISOMessage(NullLogger.Instance, _packager);
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
            return m;
        }

        private ISOMessage BuildMessage2Bmp()
        {
            var m = BuildMessage1Bmp();
            m.Set(64, "ABCDEF1234567890");
            m.Set(70, "123");
            return m;
        }

        private ISOMessage BuildMessage3Bmp()
        {
            var m = BuildMessage2Bmp();
            m.Set(132, "ABABABAB");
            return m;
        }

        private ISOMessage BuildMessageSubfields()
        {
            var m = BuildMessage1Bmp();
            m.Set(62, 1, "Y");
            m.Set(63, 1, "1222");
            m.Set(63, 3, "9999");
            return m;
        }
    }
}

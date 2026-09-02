using Xunit;
using ISO8583Net.Utilities;

namespace ISO8583Tests
{
    public class UtilTests
    {
        [Fact]
        public void Ascii2BcdWorks()
        {

            byte[] buffer = new byte[50];
            byte[] oldbuffer = new byte[50];
            int currentIndex = 0;
            ISOUtils.Ascii2Bcd("12341234", buffer, ref currentIndex, ISO8583Net.Types.ISOFieldPadding.LEFT);
            currentIndex = 0;
            ISOUtils.Ascii2BcdOld("12341234", oldbuffer, ref currentIndex, ISO8583Net.Types.ISOFieldPadding.LEFT);
            Compare(buffer, oldbuffer, currentIndex);

            currentIndex = 0;
            ISOUtils.Ascii2Bcd("12341234", buffer, ref currentIndex, ISO8583Net.Types.ISOFieldPadding.RIGHT);
            currentIndex = 0;
            ISOUtils.Ascii2BcdOld("12341234", oldbuffer, ref currentIndex, ISO8583Net.Types.ISOFieldPadding.RIGHT);
            Compare(buffer, oldbuffer, currentIndex);

            currentIndex = 0;
            ISOUtils.Ascii2Bcd("1234123", buffer, ref currentIndex, ISO8583Net.Types.ISOFieldPadding.LEFT);
            currentIndex = 0;
            ISOUtils.Ascii2BcdOld("1234123", oldbuffer, ref currentIndex, ISO8583Net.Types.ISOFieldPadding.LEFT);
            Compare(buffer, oldbuffer, currentIndex);

            currentIndex = 0;
            ISOUtils.Ascii2Bcd("1234123", buffer, ref currentIndex, ISO8583Net.Types.ISOFieldPadding.RIGHT);
            currentIndex = 0;
            ISOUtils.Ascii2BcdOld("1234123", oldbuffer, ref currentIndex, ISO8583Net.Types.ISOFieldPadding.RIGHT);
            Compare(buffer, oldbuffer, currentIndex);
        }

        [Fact]
        public void Int2BytesWritesBigEndianAndRoundTrips()
        {
            // ISO 8583 BIN length prefixes are big-endian (most significant byte first).
            // Regression: Int2Bytes previously wrote little-endian, so a 2-byte length of
            // 55 (0x0037) was emitted as 37 00 instead of 00 37.
            byte[] buffer = new byte[8];
            int index = 0;
            ISOUtils.Int2Bytes(55, buffer, ref index, 4);
            Assert.Equal(0x00, buffer[0]);
            Assert.Equal(0x37, buffer[1]);
            Assert.Equal(2, index);

            // Round-trip: Bytes2Int reads big-endian, so the two must agree.
            index = 0;
            int parsed = ISOUtils.Bytes2Int(buffer, ref index, 4);
            Assert.Equal(55, parsed);
            Assert.Equal(2, index);
        }

        private static void Compare(byte[] newbuf, byte[] oldbuf, int length)
        {
            for (int i = 0; i < length; i++)
            {
                Assert.Equal(newbuf[i], oldbuf[i]);                    
            }
        }
    }
}

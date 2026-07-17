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

        private static void Compare(byte[] newbuf, byte[] oldbuf, int length)
        {
            for (int i = 0; i < length; i++)
            {
                Assert.Equal(newbuf[i], oldbuf[i]);                    
            }
        }
    }
}

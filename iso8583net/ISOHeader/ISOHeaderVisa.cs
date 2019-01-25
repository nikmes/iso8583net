using ISO8583Net.Packager;
using ISO8583Net.Types;
using ISO8583Net.Utilities;
using Microsoft.Extensions.Logging;
using System;

namespace ISO8583Net.Header
{
    public class ISOHeaderVisa : ISOHeader
    {
        public int m_length = 22;

        public string h01_HeaderLength { get; set; }                     // Byte 1         header len bytes   -   2HD

        public string h02_HeaderFlagAndFormat { get; set; }              // Byte 2         1B 8N Bit String   -   2HD

        public string h03_TextFormat { get; set; }                       // Byte 3         1B Binary          -   2HD

        public string h04_TotalMessageLength { get; set; }               // Bytes 4-5      2B Binary          -   4HD                                                          

        public string h05_DestinationStationId { get; set; }             // Bytes 6-8      3B 6 Numeric BCD   -   6N

        public string h06_SourceStationId { get; set; }                  // Bytes 9-11     3B 6 Numeric BCD   -   6N

        public string h07_RoundTripControlInformation { get; set; }      // Byte 12        1B 8Bit String     -   2HD

        public string h08_BaseIFlag { get; set; }                        // Bytes 13-14    2B 16Bit String    -   4HD

        public string h09_MessageStatusFlag { get; set; }                // Bytes 15-17    3B 24Bit String    -   6HD

        public string h10_BatchNumber { get; set; }                      // Bytes 18       1B Binary          -   2HD

        public string h11_Reserved { get; set; }                         // Byte 19-21     3B Binary          -   6HD

        public string h12_UserInformation { get; set; }                  // Byte 22        1B Binary          -   2HD

        public string h13_Bitmap { get; set; }                           // Byyte 23-24    2B Binary          -   4HD

        public string h14_RejectedGroupData { get; set; }                // Byyte 25-26    2B Binary          -   4HD

        public ISOHeaderVisa(ILogger logger) : base (logger)
        {
            h01_HeaderLength = "00";
            h02_HeaderFlagAndFormat = "00";
            h03_TextFormat = "00";
            h04_TotalMessageLength = "0000";
            h05_DestinationStationId = "000000";
            h06_SourceStationId = "000000";
            h07_RoundTripControlInformation = "00";
            h08_BaseIFlag = "0000";
            h09_MessageStatusFlag = "000000";
            h10_BatchNumber = "00";
            h11_Reserved = "000000";
            h12_UserInformation = "00";
            h13_Bitmap = "0000";
            h14_RejectedGroupData = "0000";
        }

        public ISOHeaderVisa(ILogger logger, ISOHeaderPackager isoHeaderPackager) : base (logger)
        {
            h01_HeaderLength = "00";
            h02_HeaderFlagAndFormat = "00";
            h03_TextFormat = "00";
            h04_TotalMessageLength = "0000";
            h05_DestinationStationId = "000000";
            h06_SourceStationId = "000000";
            h07_RoundTripControlInformation = "00";
            h08_BaseIFlag = "0000";
            h09_MessageStatusFlag = "000000";
            h10_BatchNumber = "00";
            h11_Reserved = "000000";
            h12_UserInformation = "00";
            h13_Bitmap = "0000";
            h14_RejectedGroupData = "0000";
        }

        public override int Length()
        {
            return m_length;
        }

        public override void SetMessageLength(int length)
        {
            // provision for leading zeros during conversion of length indicator
            h04_TotalMessageLength = (length).ToString("X4");
        }

        public override void SetValue(byte[] bytes)
        {
            // Unpack should check for existense of Header Field 13 always
            int index = 0;

            //if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("Unpacking VISA Header");

            string lenHex = ISOUtils.bytes2hex(bytes, ref index, 1);

            m_length = ISOUtils.hex2bytes(lenHex)[0]; 

            h02_HeaderFlagAndFormat = ISOUtils.bytes2hex(bytes, ref index, 1);

            h03_TextFormat = ISOUtils.bytes2hex(bytes, ref index, 1);

            h04_TotalMessageLength = ISOUtils.bytes2hex(bytes, ref index, 2);

            h05_DestinationStationId = ISOUtils.bcd2ascii(bytes, ref index, ISOFieldPadding.LEFT, 6);

            h06_SourceStationId = ISOUtils.bcd2ascii(bytes, ref index, ISOFieldPadding.LEFT, 6);

            h07_RoundTripControlInformation = ISOUtils.bytes2hex(bytes, ref index, 1);

            h08_BaseIFlag = ISOUtils.bytes2hex(bytes, ref index, 2);

            h09_MessageStatusFlag = ISOUtils.bytes2hex(bytes, ref index, 3);

            h10_BatchNumber = ISOUtils.bytes2hex(bytes, ref index, 1);

            h11_Reserved = ISOUtils.bytes2hex(bytes, ref index, 3);

            h12_UserInformation = ISOUtils.bytes2hex(bytes, ref index, 1);
        }

        public override void Pack(byte[] packedData, ref int index)
        {
            // should never be called, I ll deal with it later
            throw new NotImplementedException();
        }

        public override void UnPack(byte[] packedBytes, ref int index)
        {
            // should never be called, I ll deal with it later
            throw new NotImplementedException();
        }
    }
}

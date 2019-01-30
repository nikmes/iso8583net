using ISO8583Net.Header;
using ISO8583Net.Types;
using ISO8583Net.Utilities;
using Microsoft.Extensions.Logging;
using System;

namespace ISO8583Net.Packager
{
    /// <summary>
    /// 
    /// </summary>
    public class ISOHeaderVisaPackager : ISOHeaderPackager
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        public ISOHeaderVisaPackager(ILogger logger) : base (logger)
        {

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoHeader"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        public override void Pack(ISOHeader isoHeader, byte[] packedBytes, ref int index)
        {
            ISOHeaderVisa visaHeader = (ISOHeaderVisa)isoHeader;

            ISOUtils.Hex2Bytes(isoHeader.Length().ToString("X2"), packedBytes, ref index);

            ISOUtils.Hex2Bytes(visaHeader.h02_HeaderFlagAndFormat, packedBytes, ref index);

            ISOUtils.Hex2Bytes(visaHeader.h03_TextFormat, packedBytes, ref index);

            ISOUtils.Hex2Bytes(visaHeader.h04_TotalMessageLength, packedBytes, ref index);

            ISOUtils.Ascii2Bcd(visaHeader.h05_DestinationStationId, packedBytes, ref index, ISOFieldPadding.LEFT);

            ISOUtils.Ascii2Bcd(visaHeader.h06_SourceStationId, packedBytes, ref index, ISOFieldPadding.LEFT);

            ISOUtils.Hex2Bytes(visaHeader.h07_RoundTripControlInformation, packedBytes, ref index);

            ISOUtils.Hex2Bytes(visaHeader.h08_BaseIFlag, packedBytes, ref index);

            ISOUtils.Hex2Bytes(visaHeader.h09_MessageStatusFlag, packedBytes, ref index);

            ISOUtils.Hex2Bytes(visaHeader.h10_BatchNumber, packedBytes, ref index);

            ISOUtils.Hex2Bytes(visaHeader.h11_Reserved, packedBytes, ref index);

            ISOUtils.Hex2Bytes(visaHeader.h12_UserInformation, packedBytes, ref index);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        public override void Set(byte[] bytes)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 
        /// </summary>
        public override void Trace()
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return base.ToString();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoHeader"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        public override void UnPack(ISOHeader isoHeader, byte[] packedBytes, ref int index)
        {
            // Unpack should check for existense of Header Field 13 always

            ISOHeaderVisa visaHeader = (ISOHeaderVisa)isoHeader;

            if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("Unpacking VISA Header");

            string lenHex = ISOUtils.Bytes2Hex(packedBytes, ref index, 1);

            visaHeader.m_length = ISOUtils.Hex2Bytes(lenHex)[0]; 

            visaHeader.h02_HeaderFlagAndFormat = ISOUtils.Bytes2Hex(packedBytes, ref index, 1);

            visaHeader.h03_TextFormat = ISOUtils.Bytes2Hex(packedBytes, ref index, 1);


            visaHeader.h04_TotalMessageLength = ISOUtils.Bytes2Hex(packedBytes, ref index, 2);


            visaHeader.h05_DestinationStationId = ISOUtils.Bcd2Ascii(packedBytes, ref index, ISOFieldPadding.LEFT, 6);

            visaHeader.h06_SourceStationId = ISOUtils.Bcd2Ascii(packedBytes, ref index, ISOFieldPadding.LEFT, 6);


            visaHeader.h07_RoundTripControlInformation = ISOUtils.Bytes2Hex(packedBytes, ref index, 1);

            visaHeader.h08_BaseIFlag = ISOUtils.Bytes2Hex(packedBytes, ref index, 2);

            visaHeader.h09_MessageStatusFlag = ISOUtils.Bytes2Hex(packedBytes, ref index, 3);

            visaHeader.h10_BatchNumber = ISOUtils.Bytes2Hex(packedBytes, ref index, 1);

            visaHeader.h11_Reserved = ISOUtils.Bytes2Hex(packedBytes, ref index, 3);

            visaHeader.h12_UserInformation = ISOUtils.Bytes2Hex(packedBytes, ref index, 1);
        }
    }
}

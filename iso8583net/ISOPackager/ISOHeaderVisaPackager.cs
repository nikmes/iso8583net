using ISO8583Net.Header;
using ISO8583Net.Types;
using ISO8583Net.Utilities;
using Microsoft.Extensions.Logging;
using System;

namespace ISO8583Net.Packager
{
    public class ISOHeaderVisaPackager : ISOHeaderPackager
    {
        public ISOHeaderVisaPackager(ILogger logger) : base (logger)
        {

        }

        public override void Pack(ISOHeader isoHeader, byte[] packedBytes, ref int index)
        {
            ISOHeaderVisa visaHeader = (ISOHeaderVisa)isoHeader;

            ISOUtils.hex2bytes(isoHeader.Length().ToString("X2"), packedBytes, ref index);

            ISOUtils.hex2bytes(visaHeader.h02_HeaderFlagAndFormat, packedBytes, ref index);

            ISOUtils.hex2bytes(visaHeader.h03_TextFormat, packedBytes, ref index);

            ISOUtils.hex2bytes(visaHeader.h04_TotalMessageLength, packedBytes, ref index);

            ISOUtils.ascii2bcd(visaHeader.h05_DestinationStationId, packedBytes, ref index, ISOFieldPadding.LEFT);

            ISOUtils.ascii2bcd(visaHeader.h06_SourceStationId, packedBytes, ref index, ISOFieldPadding.LEFT);

            ISOUtils.hex2bytes(visaHeader.h07_RoundTripControlInformation, packedBytes, ref index);

            ISOUtils.hex2bytes(visaHeader.h08_BaseIFlag, packedBytes, ref index);

            ISOUtils.hex2bytes(visaHeader.h09_MessageStatusFlag, packedBytes, ref index);

            ISOUtils.hex2bytes(visaHeader.h10_BatchNumber, packedBytes, ref index);

            ISOUtils.hex2bytes(visaHeader.h11_Reserved, packedBytes, ref index);

            ISOUtils.hex2bytes(visaHeader.h12_UserInformation, packedBytes, ref index);
        }

        public override void Set(byte[] bytes)
        {
            throw new NotImplementedException();
        }

        public override void Trace()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return base.ToString();
        }

        public override void UnPack(ISOHeader isoHeader, byte[] packedBytes, ref int index)
        {
            // Unpack should check for existense of Header Field 13 always

            ISOHeaderVisa visaHeader = (ISOHeaderVisa)isoHeader;

            if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("Unpacking VISA Header");

            string lenHex = ISOUtils.bytes2hex(packedBytes, ref index, 1);

            visaHeader.m_length = ISOUtils.hex2bytes(lenHex)[0]; 

            visaHeader.h02_HeaderFlagAndFormat = ISOUtils.bytes2hex(packedBytes, ref index, 1);

            visaHeader.h03_TextFormat = ISOUtils.bytes2hex(packedBytes, ref index, 1);


            visaHeader.h04_TotalMessageLength = ISOUtils.bytes2hex(packedBytes, ref index, 2);


            visaHeader.h05_DestinationStationId = ISOUtils.bcd2ascii(packedBytes, ref index, ISOFieldPadding.LEFT, 6);

            visaHeader.h06_SourceStationId = ISOUtils.bcd2ascii(packedBytes, ref index, ISOFieldPadding.LEFT, 6);


            visaHeader.h07_RoundTripControlInformation = ISOUtils.bytes2hex(packedBytes, ref index, 1);

            visaHeader.h08_BaseIFlag = ISOUtils.bytes2hex(packedBytes, ref index, 2);

            visaHeader.h09_MessageStatusFlag = ISOUtils.bytes2hex(packedBytes, ref index, 3);

            visaHeader.h10_BatchNumber = ISOUtils.bytes2hex(packedBytes, ref index, 1);

            visaHeader.h11_Reserved = ISOUtils.bytes2hex(packedBytes, ref index, 3);

            visaHeader.h12_UserInformation = ISOUtils.bytes2hex(packedBytes, ref index, 1);
        }
    }
}

using ISO8583Net.Packager;
using Microsoft.Extensions.Logging;
using System;

namespace ISO8583Net.Field
{
    class ISOFieldBerTlv : ISOField
    {
        protected BerTLV m_tlvList = new BerTLV();

        public ISOFieldBerTlv(ILogger logger, ISOFieldPackager packager, int fieldNumber) : base (logger, packager, fieldNumber)
        {

        }

        public ISOFieldBerTlv(ILogger logger, ISOFieldPackager packager, int fieldNumber, String value) : base(logger, packager, fieldNumber, value)
        {

        }


    }
}

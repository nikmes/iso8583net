using ISO8583Net.Packager;
using Microsoft.Extensions.Logging;
using System;

namespace ISO8583Net.Field
{
    /// <summary>
    /// 
    /// </summary>
    class ISOFieldBerTlv : ISOField
    {
        protected BerTLV m_tlvList = new BerTLV();
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="packager"></param>
        /// <param name="fieldNumber"></param>
        public ISOFieldBerTlv(ILogger logger, ISOFieldPackager packager, int fieldNumber) : base (logger, packager, fieldNumber)
        {

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="packager"></param>
        /// <param name="fieldNumber"></param>
        /// <param name="value"></param>
        public ISOFieldBerTlv(ILogger logger, ISOFieldPackager packager, int fieldNumber, String value) : base(logger, packager, fieldNumber, value)
        {

        }


    }
}

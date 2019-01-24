using ISO8583Net.Field;
using Microsoft.Extensions.Logging;
using System;


namespace ISO8583Net.Packager
{
    public class ISOMessagePackager : ISOPackager
    {
        protected int m_totalFields;

        protected ISOMessageFieldsPackager m_msgFieldsPackager;

        public int GetTotalFields()
        {
            return m_totalFields;
        }

        public ISOMessagePackager(ILogger logger, string fileName) : base (logger)
        {
            ISOPackagerLoader isoPackagerLoader = new ISOPackagerLoader(Logger, fileName, ref m_msgFieldsPackager);

            m_totalFields = m_msgFieldsPackager.GetTotalFields();
        }

        public ISOMessagePackager(ILogger logger) : base(logger)
        {
            ISOPackagerLoader isoPackagerLoader = new ISOPackagerLoader(Logger, ref m_msgFieldsPackager);

            m_totalFields = m_msgFieldsPackager.GetTotalFields();
        }

        public override void Pack(ISOComponent isoMessage,byte[] packedBytes, ref int index)
        {
            m_msgFieldsPackager.Pack(isoMessage, packedBytes, ref index);
        }

        public override void UnPack(ISOComponent isoField, byte[] packedBytes, ref int index)
        {
            m_msgFieldsPackager.UnPack(isoField, packedBytes, ref index);
        }

        public override String ToString()
        {
            return ("ISOMessagePackager Definition: \n" + m_msgFieldsPackager.ToString());
        }

        public override void Trace()
        {
            m_msgFieldsPackager.Trace();
        }

        public ISOPackager GetFieldPackager(int fieldNumber)
        {
            return m_msgFieldsPackager.GetFieldPackager(fieldNumber);
        }

        public ISOPackager GetFieldPackager(int fieldNumber, int subFieldNumber)
        {
            return ((ISOMessageFieldsPackager)(m_msgFieldsPackager.GetFieldPackager(fieldNumber))).GetFieldPackager(subFieldNumber);
        }

        public ISOMessageFieldsPackager GetISOMessageFieldsPackager()
        {
            return m_msgFieldsPackager;
        }
    }
}

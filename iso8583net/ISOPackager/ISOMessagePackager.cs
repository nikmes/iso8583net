using ISO8583Net.Field;
using Microsoft.Extensions.Logging;
using System;


namespace ISO8583Net.Packager
{
    /// <summary>
    /// 
    /// </summary>
    public class ISOMessagePackager : ISOPackager
    {
        protected int m_totalFields;
        /// <summary>
        /// 
        /// </summary>

        protected ISOMessageFieldsPackager m_msgFieldsPackager;
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int GetTotalFields()
        {
            return m_totalFields;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="fileName"></param>
        public ISOMessagePackager(ILogger logger, string fileName) : base (logger)
        {
            ISOPackagerLoader isoPackagerLoader = new ISOPackagerLoader(Logger, fileName, ref m_msgFieldsPackager);

            m_totalFields = m_msgFieldsPackager.GetTotalFields();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        public ISOMessagePackager(ILogger logger) : base(logger)
        {
            ISOPackagerLoader isoPackagerLoader = new ISOPackagerLoader(Logger, ref m_msgFieldsPackager);

            m_totalFields = m_msgFieldsPackager.GetTotalFields();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoMessage"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        public override void Pack(ISOComponent isoMessage,byte[] packedBytes, ref int index)
        {
            m_msgFieldsPackager.Pack(isoMessage, packedBytes, ref index);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoField"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        public override void UnPack(ISOComponent isoField, byte[] packedBytes, ref int index)
        {
            m_msgFieldsPackager.UnPack(isoField, packedBytes, ref index);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return ("ISOMessagePackager Definition: \n" + m_msgFieldsPackager.ToString());
        }
        /// <summary>
        /// 
        /// </summary>
        public override void Trace()
        {
            m_msgFieldsPackager.Trace();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fieldNumber"></param>
        /// <returns></returns>
        public ISOPackager GetFieldPackager(int fieldNumber)
        {
            return m_msgFieldsPackager.GetFieldPackager(fieldNumber);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fieldNumber"></param>
        /// <param name="subFieldNumber"></param>
        /// <returns></returns>
        public ISOPackager GetFieldPackager(int fieldNumber, int subFieldNumber)
        {
            return ((ISOMessageFieldsPackager)(m_msgFieldsPackager.GetFieldPackager(fieldNumber))).GetFieldPackager(subFieldNumber);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ISOMessageFieldsPackager GetISOMessageFieldsPackager()
        {
            return m_msgFieldsPackager;
        }
    }
}

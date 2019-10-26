using ISO8583Net.Field;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace ISO8583Net.Packager
{

    /// <summary>
    /// Packagers Implemetation
    /// </summary>
    public class ISOMessageTypesPackager : ISOPackager
    {
        private int m_totalFields;

        private Dictionary<string, ISOMsgTypePackager> m_msgTypes = new Dictionary<string, ISOMsgTypePackager>(); 
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="totalFields"></param>
        public ISOMessageTypesPackager(ILogger logger, int totalFields) : base (logger)
        {
            m_totalFields = totalFields;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoField"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        public override void Pack(ISOComponent isoField, byte[] packedBytes, ref int index)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoField"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        public override void UnPack(ISOComponent isoField, byte[] packedBytes, ref int index)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder strBuilder = new StringBuilder("");

            strBuilder.Append("ISOMessageTypePackager Definition: \n");

            foreach (KeyValuePair<string, ISOMsgTypePackager> msgTypePackager in m_msgTypes)
            {
                strBuilder.Append(msgTypePackager.Value.ToString());
            }

            return strBuilder.ToString();
        }
        /// <summary>
        /// 
        /// </summary>
        public override void Trace()
        {
            if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("ISOMessageTypePackager Definition: ");

            foreach (KeyValuePair<string, ISOMsgTypePackager> msgTypePackager in m_msgTypes)
            {
                msgTypePackager.Value.Trace();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="msgType"></param>
        /// <param name="msgTypePackager"></param>
        public void Add(string msgType,ISOMsgTypePackager msgTypePackager)
        {
           
            m_msgTypes.Add(msgType, msgTypePackager);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="bitMapField"></param>
        /// <param name="msgType"></param>
        /// <returns></returns>
        public bool ValidateBitmap(ISOFieldBitmap bitMapField, string msgType)
        {
            if (m_msgTypes.ContainsKey(msgType))
            {
                return m_msgTypes[msgType].ValidateBitmap(bitMapField);
            }
            else
            {
                if (Logger.IsEnabled(LogLevel.Critical)) Logger.LogCritical("Message Type [" + msgType + "] not supported by packager!");
                return false;
            }
        }
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
        /// <param name="isoMsgType"></param>
        /// <returns></returns>
        public byte[] GetMandatoryByteArray(string isoMsgType)
        {
            return m_msgTypes[isoMsgType].GetMandatoryByteArray();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoMsgType"></param>
        /// <returns></returns>
        public byte[] GetOptionalByteArray(string isoMsgType)
        {
            return m_msgTypes[isoMsgType].GetOptionalByteArray();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoMsgType"></param>
        /// <returns></returns>
        public byte[] GetConditionalByteArray(string isoMsgType)
        {
            return m_msgTypes[isoMsgType].GetConditionalByteArray();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoMsgType"></param>
        /// <returns></returns>
        public ISOFieldBitmap GetMandatoryBitmap(string isoMsgType)
        {
            return m_msgTypes[isoMsgType].GetMandatoryBitmap();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoMsgType"></param>
        /// <returns></returns>
        public ISOFieldBitmap GetOptionalBitmap(string isoMsgType)
        {
            return m_msgTypes[isoMsgType].GetOptionalBitmap();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoMsgType"></param>
        /// <returns></returns>
        public ISOFieldBitmap GetConditionalBitmap(string isoMsgType)
        {
            return m_msgTypes[isoMsgType].GetConditionalBitmap();
        }

    }
}

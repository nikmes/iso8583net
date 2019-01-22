using ISO8583Net.Field;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace ISO8583Net.Packager
{
    // Packagers Implemetation
    public class ISOMessageTypesPackager : ISOPackager
    {
        private int m_totalFields;

        private Dictionary<String, ISOMsgTypePackager> m_msgTypes = new Dictionary<String, ISOMsgTypePackager>(); 

        public ISOMessageTypesPackager(ILogger logger, int totalFields) : base (logger)
        {
            m_totalFields = totalFields;
        }

        public override void Pack(ISOComponent isoField, byte[] packedBytes, ref int index)
        {
            throw new NotImplementedException();
        }

        public override void UnPack(ISOComponent isoField, byte[] packedBytes, ref int index)
        {
            throw new NotImplementedException();
        }

        public override String ToString()
        {
            StringBuilder strBuilder = new StringBuilder("");

            strBuilder.Append("ISOMessageTypePackager Definition: \n");

            foreach (KeyValuePair<String, ISOMsgTypePackager> msgTypePackager in m_msgTypes)
            {
                strBuilder.Append(msgTypePackager.Value.ToString());
            }

            return strBuilder.ToString();
        }

        public override void Trace()
        {
            if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("ISOMessageTypePackager Definition: ");

            foreach (KeyValuePair<String, ISOMsgTypePackager> msgTypePackager in m_msgTypes)
            {
                msgTypePackager.Value.Trace();
            }
        }

        public void Add(String msgType,ISOMsgTypePackager msgTypePackager)
        {
           
            m_msgTypes.Add(msgType, msgTypePackager);
        }

        public bool ValidateBitmap(ISOFieldBitmap bitMapField, String msgType)
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

        public int GetTotalFields()
        {
            return m_totalFields;
        }

        public byte[] GetMandatoryByteArray(String isoMsgType)
        {
            return m_msgTypes[isoMsgType].GetMandatoryByteArray();
        }

        public byte[] GetOptionalByteArray(String isoMsgType)
        {
            return m_msgTypes[isoMsgType].GetOptionalByteArray();
        }

        public byte[] GetConditionalByteArray(String isoMsgType)
        {
            return m_msgTypes[isoMsgType].GetConditionalByteArray();
        }

        public ISOFieldBitmap GetMandatoryBitmap(String isoMsgType)
        {
            return m_msgTypes[isoMsgType].GetMandatoryBitmap();
        }

        public ISOFieldBitmap GetOptionalBitmap(String isoMsgType)
        {
            return m_msgTypes[isoMsgType].GetOptionalBitmap();
        }

        public ISOFieldBitmap GetConditionalBitmap(String isoMsgType)
        {
            return m_msgTypes[isoMsgType].GetConditionalBitmap();
        }

    }
}

using ISO8583Net.Field;
using ISO8583Net.Header;
using ISO8583Net.Packager;
using ISO8583Net.Utilities;
using Microsoft.Extensions.Logging;
using System;
using System.Text;

namespace ISO8583Net.Message
{
    public class ISOMessage : ISOComponent
    {
        protected ISOMessagePackager m_isoMesssagePackager;

        protected ISOMessageFields m_isoMessageFields;

        protected ISOHeaderPackager m_isoHeaderPackager = null;

        protected ISOHeader m_isoHeader = null;

        protected int m_totalFields;

        public ISOMessage(ILogger logger, ISOMessagePackager messagePackager, ISOHeaderPackager isoHeaderPackager) : base(logger, 0)
        {
            m_isoMesssagePackager = messagePackager;

            m_isoHeaderPackager = isoHeaderPackager;

            m_isoMessageFields = new ISOMessageFields(Logger, m_isoMesssagePackager.GetISOMessageFieldsPackager(), 0);

            // based on isoHeaderPackager storage class initialize the correct ISOHeader
            m_isoHeader = new ISOHeaderVisa(Logger, m_isoHeaderPackager);

            m_totalFields = ((ISOMessagePackager)m_isoMesssagePackager).GetTotalFields();
        }

        public ISOMessage(ILogger logger, ISOMessagePackager isoMessagePackager) : base(logger, 0) 
        {
            m_isoMesssagePackager = isoMessagePackager;

            m_isoMessageFields = new ISOMessageFields(Logger, m_isoMesssagePackager.GetISOMessageFieldsPackager(), 0);

            m_isoHeaderPackager = new ISOHeaderVisaPackager(Logger);

            // based on isoHeaderPackager storage class initialize the correct ISOHeader
            m_isoHeader = new ISOHeaderVisa(Logger, m_isoHeaderPackager);

            m_totalFields = ((ISOMessagePackager)m_isoMesssagePackager).GetTotalFields();
        }

        public override void SetValue(string value)
        {
            if (Logger.IsEnabled(LogLevel.Error)) Logger.LogError("ISOMessage setValue is not implemented yet!");

            throw new NotImplementedException();
        }

        public override void SetFieldValue(int fieldNumber, String fieldValue)
        {
            if (fieldNumber >= 0 && fieldNumber <= m_totalFields) 
            {
                m_isoMessageFields.SetFieldValue(fieldNumber, fieldValue);
            }
            else
            {
                Logger.LogError("Attempt to set value for an out of range field[", fieldNumber.ToString().PadLeft(3, ' ') + "]"); 
            }
        }

        public void SetFieldValue(int fieldNumber, int subFieldNumber, String fieldValue)
        {
            if (fieldNumber >= 0 && fieldNumber <= m_totalFields && fieldNumber != 65 && fieldNumber != 129)
            {
                m_isoMessageFields.SetFieldValue(fieldNumber, subFieldNumber, fieldValue);                
            }
            else
            {
                Logger.LogError("Attempt to set value for an out of range field[", fieldNumber.ToString().PadLeft(3, ' ') + "]");
            }
        }

        public override string GetValue()
        {
            return m_isoMessageFields.GetValue();
        }

        public override string GetFieldValue(int fieldNumber)
        {
            return m_isoMessageFields.GetFieldValue(fieldNumber);
        }

        public ISOComponent GetField(int fieldNumber)
        {
            return m_isoMessageFields.GetField(fieldNumber);
        }

        public override string GetFieldValue(int fieldNumber, int subField)
        {
            return m_isoMessageFields.GetFieldValue(subField);
        }

        public byte[] Pack()
        {
            byte[] packedBytes = new Byte[2048];

            int index = 0;

            // if there is ISOMessage Header try pack it
            if (m_isoHeaderPackager != null && m_isoHeader != null)
            {
                // set total message legnth in header
                m_isoHeader.SetMessageLength(index + m_isoHeader.Length());

                // pack the isoHeader of the isoMessage
                m_isoHeaderPackager.Pack(m_isoHeader,packedBytes,ref index);
            }

            // pack the isoMessage after the isoHeader
            m_isoMesssagePackager.Pack(m_isoMessageFields, packedBytes, ref index);

            return packedBytes.SubArray(0, index);
        }

        public void UnPack(byte[] packedBytes)
        {
            int index = 0;

            // if there is ISOMessage Header then try unpack it
            if (m_isoHeaderPackager != null && m_isoHeader != null)
            {
                // unpack the isoHeader
                m_isoHeaderPackager.UnPack(m_isoHeader, packedBytes, ref index);
            }

            // unpack the isoMessage
            m_isoMesssagePackager.UnPack(m_isoMessageFields, packedBytes, ref index);
        }

        public override string ToString()
        {
            StringBuilder msgFieldValues = new StringBuilder();

            msgFieldValues.Append("ISO Message Content: \n");

            msgFieldValues.Append(m_isoMessageFields.ToString());

            return msgFieldValues.ToString();
        }

        public override void Trace()
        {
            m_isoMessageFields.Trace();
        }

        public ISOMessage ToCommonISO()
        {
            throw new NotImplementedException();
        }

        public void FromCommonISO(ISOMessage isoMessage)
        {
            throw new NotImplementedException();
        }
    }
}

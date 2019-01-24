using ISO8583Net.Field;
using ISO8583Net.Utilities;
using Microsoft.Extensions.Logging;
using System;
using System.Text;

namespace ISO8583Net.Packager
{
    public class ISOMessageSubFieldsPackager : ISOPackager
    {
        private ISOPackager[] m_fieldPackagerList;

        public int totalFields { get; set; }

        public ISOMessageSubFieldsPackager(ILogger logger, int fieldNumber, int totalFields, ISOFieldDefinition isoFieldDefinition) : base (logger, isoFieldDefinition)
        {
            this.totalFields = totalFields;

            m_number = fieldNumber;

            m_composite = true;

            m_fieldPackagerList = new ISOPackager[totalFields+1];
        }

        public ISOMessageSubFieldsPackager(ILogger logger, int fieldNumber, int totalFields) : base(logger)
        {
            this.totalFields = totalFields;

            m_number = fieldNumber;

            m_composite = true;

            m_fieldPackagerList = new ISOPackager[totalFields + 1];
        }

        public void Add(ISOPackager fieldPackager, int number)
        {
            m_fieldPackagerList[number] = fieldPackager;
        }

        public override void Pack(ISOComponent isoMessageFields, byte[] packedBytes, ref int index)
        {
            ISOComponent[] isoFields = ((ISOMessageSubFields)(isoMessageFields)).GetFields();

            // remember where to copy the length - once we know it
            int indexStarts = index;

            // reserve enough bytes to store the length
            index += (m_isoFieldDefinition.lengthLength/2);

            m_fieldPackagerList[0].Pack(isoFields[0], packedBytes, ref index);

            // bitmap was packed so get the total length in bits to determine up to what field number it expands
            int totFields = ((ISOFieldBitmap)(isoFields[0])).GetLengthInBits();

            for (int fieldNumber = 1; fieldNumber <= totFields; fieldNumber++)
            {
                if (((ISOFieldBitmap)(isoFields[0])).BitIsSet(fieldNumber)) 
                {
                    m_fieldPackagerList[fieldNumber].Pack(isoFields[fieldNumber], packedBytes, ref index);
                }
            }
            
            //!!! Hack always assumes length is in binary format !!!!
            //int bytesCopied = (i - (indexStarts - (m_isoFieldDefinition.m_lengthLength/2))); // bytes used for length not inclusive in length indicator

            ISOUtils.Int2Bytes((index - (indexStarts - (m_isoFieldDefinition.lengthLength / 2))), packedBytes, ref indexStarts, m_isoFieldDefinition.lengthLength);
        }

        public override void UnPack(ISOComponent isoField, byte[] packedBytes, ref int index)
        {
            /*!!! Hack Special Field - First Unpack my length (and ignore it for now) !!! */

            index += m_isoFieldDefinition.lengthLength / 2;

            ISOComponent[] isoFields = ((ISOMessageSubFields)(isoField)).GetFields();

            isoFields[0] = new ISOFieldBitmap(Logger, m_fieldPackagerList[0], m_fieldPackagerList[0].GetFieldNumber());

            m_fieldPackagerList[0].UnPack(isoFields[0], packedBytes, ref index);

            int totFields = ((ISOFieldBitmap)(isoFields[0])).GetLengthInBits();

            for (int fieldNumber = 1; fieldNumber <= totFields; fieldNumber++)
            {
                if (((ISOFieldBitmap)(isoFields[0])).BitIsSet(fieldNumber))
                {
                    isoFields[fieldNumber] = new ISOField(Logger, m_fieldPackagerList[fieldNumber], m_fieldPackagerList[fieldNumber].GetFieldNumber());

                    m_fieldPackagerList[fieldNumber].UnPack(isoFields[fieldNumber], packedBytes, ref index);
                }
            }
        }

        public override String ToString()
        {
            StringBuilder strBuilder = new StringBuilder();

            strBuilder.Append("ISOMessageSubFieldPackager: \n");

            strBuilder.Append("Field Number ["+m_number.ToString().PadLeft(3,' ') +"]\n");

            for (int i=0; i<= totalFields; i++)
            {
                if (m_fieldPackagerList[i]!=null)
                {
                    strBuilder.Append(m_fieldPackagerList[i].ToString());
                }
            }

            return strBuilder.ToString();
        }

        public override void Trace()
        {
            if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("ISOMessageSubFieldPackager: ");

            for (int i = 0; i <= totalFields; i++)
            {
                if (m_fieldPackagerList[i] != null)
                {
                    if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace(m_fieldPackagerList[i].ToString());
                }
            }
        }

        public ISOPackager GetFieldPackager(int fieldNumber)
        {
            return m_fieldPackagerList[fieldNumber];
        }
    }
}

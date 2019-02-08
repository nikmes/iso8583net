using ISO8583Net.Field;
using ISO8583Net.Types;
using ISO8583Net.Utilities;
using Microsoft.Extensions.Logging;
using System;
using System.Text;

namespace ISO8583Net.Packager
{
    public class ISOFieldBitmapSubFieldsPackager : ISOPackager
    {
        private ISOPackager[] m_fieldPackagerList;

        /// <summary>
        /// Total number of subfields  
        /// </summary>
        public int totalFields { get; set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="fieldNumber"></param>
        /// <param name="totalFields"></param>
        public ISOFieldBitmapSubFieldsPackager(ILogger logger, int fieldNumber, int totalFields) : base(logger)
        {
            this.totalFields = totalFields;

            m_number = fieldNumber;

            m_composite = true;

            m_fieldPackagerList = new ISOPackager[totalFields + 1];
        }
        /// <summary>
        /// Adds packager of subfield to the array of sub field packagers 
        /// </summary>
        /// <param name="fieldPackager"></param>
        /// <param name="number"></param>
        public void Add(ISOPackager fieldPackager, int number)
        {
            m_fieldPackagerList[number] = fieldPackager;
        }
        /// <summary>
        /// Packs all subfields of field 
        /// </summary>
        /// <param name="isoMessageFields"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        /// <remarks>
        /// The way the length is handled is completely wrong, needs to be coded correctly
        /// </remarks>
        public override void Pack(ISOComponent isoMessageFields, byte[] packedBytes, ref int index)
        {
            ISOComponent[] isoFields = ((ISOFieldBitmapSubFields)(isoMessageFields)).GetFields();

            // remember where to copy the length - once we know it
            int indexStarts = index;

            // based on coding of length decide how many bytes to move on packedBytes
            int advanceNumOfBytes = 0;

            // reserve enough bytes to store the length !! ASUMES FOR NOW THAT IS ALWAYS BINARY !!
            switch (m_isoFieldDefinition.lengthCoding)
            {
                case ISOFieldCoding.BIN:
                    advanceNumOfBytes = (m_isoFieldDefinition.lengthLength / 2);
                    break;

                case ISOFieldCoding.ASCII:
                    break;

                case ISOFieldCoding.EBCDIC:
                    break;

                case ISOFieldCoding.BCD:
                    break;

                default:
                    break;
            }

            index += advanceNumOfBytes; // (m_isoFieldDefinition.lengthLength / 2);

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
            //!!! Assumes length is excluding the length indicator !!NEED TO INTRODUCE PARAM!!

            ISOUtils.Int2Bytes(index - (indexStarts - advanceNumOfBytes), packedBytes, ref indexStarts, m_isoFieldDefinition.lengthLength);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoField"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        public override void UnPack(ISOComponent isoField, byte[] packedBytes, ref int index)
        {
            /*!!! Hack Special Field - First Unpack my length (and ignore it for now) AND HERE CURRENTLY ASSUME IS ALWAYS BINARY!!! */

            index += m_isoFieldDefinition.lengthLength / 2;

            ISOComponent[] isoFields = ((ISOFieldBitmapSubFields)(isoField)).GetFields();

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
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
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
        /// <summary>
        /// 
        /// </summary>
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
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fieldNumber"></param>
        /// <returns></returns>
        public ISOPackager GetFieldPackager(int fieldNumber)
        {
            return m_fieldPackagerList[fieldNumber];
        }
    }
}

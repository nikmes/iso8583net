using ISO8583Net.Field;
using ISO8583Net.Types;
using ISO8583Net.Utilities;
using Microsoft.Extensions.Logging;
using System;
using System.Text;

namespace ISO8583Net.Packager
{
    /// <summary>
    /// 
    /// </summary>
    public class ISOMessageFieldsPackager : ISOPackager
    {
        private ISOMessageTypesPackager m_isoMsgTypePackager;

        private ISOPackager[] m_fieldPackagerList;

        private int m_totalFields;

        private bool m_fieldParticipationValidations = false;

        /// <summary>
        /// The name of the header packager class as specified in the XML dialect (e.g. "ISOHeaderVisaPackager").
        /// </summary>
        public string HeaderPackagerName { get; set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="fieldNumber"></param>
        /// <param name="totalFields"></param>
        /// <param name="isoFieldDefinition"></param>
        public ISOMessageFieldsPackager(ILogger logger, int fieldNumber, int totalFields, ISOFieldDefinition isoFieldDefinition) : base (logger, isoFieldDefinition)
        {
            m_totalFields = totalFields;

            m_number = fieldNumber;

            m_composite = true;

            m_isoMsgTypePackager = new ISOMessageTypesPackager(logger, m_totalFields);

            m_fieldPackagerList = new ISOPackager[totalFields+1];
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="fieldNumber"></param>
        /// <param name="totalFields"></param>
        public ISOMessageFieldsPackager(ILogger logger, int fieldNumber, int totalFields) : base(logger)
        {
            m_totalFields = totalFields;

            m_number = fieldNumber;

            m_composite = true;

            m_isoMsgTypePackager = new ISOMessageTypesPackager(logger, m_totalFields);

            m_fieldPackagerList = new ISOPackager[totalFields + 1];
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoMessageTypesPackager"></param>
        public void SetMessageTypesPackager(ISOMessageTypesPackager isoMessageTypesPackager)
        {
            m_isoMsgTypePackager = isoMessageTypesPackager;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fieldPackager"></param>
        /// <param name="number"></param>
        public void Add(ISOPackager fieldPackager, int number)
        {
            m_fieldPackagerList[number]=fieldPackager;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="enabled"></param>
        public void EnableFieldParticipationValidations(bool enabled)
        {
            m_fieldParticipationValidations = enabled;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoMessageFields"></param>
        /// <param name="packedBytes"></param>
        /// <param name="i"></param>
        public override void Pack(ISOComponent isoMessageFields, byte[] packedBytes, ref int i)
        {
            ISOComponent[] isoFields = ((ISOMessageFields)(isoMessageFields)).GetFields();

            m_fieldPackagerList[0].Pack(isoFields[0], packedBytes, ref i);

            m_fieldPackagerList[1].Pack(isoFields[1], packedBytes, ref i);

            var bitmap = isoFields[1] as ISOFieldBitmap;
            int[] setFields = bitmap.GetSetFields();

            for (int k = 0; k < setFields.Length; k++)
            {
                int fieldNumber = setFields[k];
                // Skip bitmap indicator bits (fields 65 and 129)
                if (fieldNumber >= 2 && fieldNumber != BitmapBoundaries.SecondaryBitmapFlag && fieldNumber != BitmapBoundaries.TertiaryBitmapFlag)
                {
                    m_fieldPackagerList[fieldNumber].Pack(isoFields[fieldNumber], packedBytes, ref i);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoField"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        public override void UnPack(ISOComponent isoField, byte[] packedBytes, ref int index)
        {
            ISOComponent[] isoFields = ((ISOMessageFields)(isoField)).GetFields();

            // Unpack the message type from the byteArray for transmission

            isoFields[0] = new ISOField(Logger, m_fieldPackagerList[0], m_fieldPackagerList[0].GetFieldNumber());

            m_fieldPackagerList[0].UnPack(isoFields[0], packedBytes, ref index);

            string msgType = isoFields[0].value;

            // Unpack the Bitmap from the byteArray for transmission

            isoFields[1] = new ISOFieldBitmap(Logger, m_fieldPackagerList[1], m_fieldPackagerList[1].GetFieldNumber());

            m_fieldPackagerList[1].UnPack(isoFields[1], packedBytes, ref index);

            var bitmap = isoFields[1] as ISOFieldBitmap;
            int[] setFields = bitmap.GetSetFields();

            for (int k = 0; k < setFields.Length; k++)
            {
                int fieldNumber = setFields[k];
                // Skip bitmap indicator bits (fields 65 and 129)
                if (fieldNumber >= 2 && fieldNumber != BitmapBoundaries.SecondaryBitmapFlag && fieldNumber != BitmapBoundaries.TertiaryBitmapFlag)
                {
                    if (fieldNumber >= m_fieldPackagerList.Length || m_fieldPackagerList[fieldNumber] == null)
                    {
                        if (Logger.IsEnabled(LogLevel.Error))
                            Logger.LogError("Field [{FieldNumber}] has NO packager defined in the dialect! " +
                                "Total fields in dialect: {TotalFields}. " +
                                "Add this field to your dialect definition.",
                                fieldNumber, m_totalFields);
                        continue;
                    }

                    if (m_fieldPackagerList[fieldNumber].IsComposite())
                    {                       
                        isoFields[fieldNumber] = new ISOFieldBitmapSubFields(Logger, (ISOFieldBitmapSubFieldsPackager)m_fieldPackagerList[fieldNumber], m_fieldPackagerList[fieldNumber].GetFieldNumber());                     
                    }
                    else
                    {
                        isoFields[fieldNumber] = new ISOField(Logger, m_fieldPackagerList[fieldNumber], m_fieldPackagerList[fieldNumber].GetFieldNumber());
                    }

                    m_fieldPackagerList[fieldNumber].UnPack(isoFields[fieldNumber], packedBytes, ref index);
                    
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder strBuilder = new StringBuilder();

            strBuilder.Append("ISOMessageFieldPackager: \n");

            if (m_number>0)
            {
                strBuilder.Append("Field Number ["+m_number.ToString().PadLeft(3,' ') +"]\n");
            }

            for (int i=0; i<= m_totalFields; i++)
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
            if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("ISOMessageFieldPackager: ");

            for (int i = 0; i <= m_totalFields; i++)
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
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int GetTotalFields()
        {
            return m_totalFields;
        }
    }
}

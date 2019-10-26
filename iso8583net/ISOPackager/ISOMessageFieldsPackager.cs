using ISO8583Net.Field;
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
            //bool allMandatoryExist = true;

            ISOComponent[] isoFields = ((ISOMessageFields)(isoMessageFields)).GetFields();

            m_fieldPackagerList[0].Pack(isoFields[0], packedBytes, ref i); // pack the message type to the byteArray for transmission

            m_fieldPackagerList[1].Pack(isoFields[1], packedBytes, ref i); // pack the Bitmap to the byteArray for transmission

            var bitmap = isoFields[1] as ISOFieldBitmap;
            int[] setFields = bitmap.GetSetFields(); //Get all the set fields

            for (int k = 0; k < setFields.Length; k++)
            {
                int fieldNumber = setFields[k];
                if (fieldNumber >= 2 && (fieldNumber != 65 && fieldNumber != 129)) // special bit fields indicating existance of second and tird bitmap (VISA BASE I Specifications)
                {
                    m_fieldPackagerList[fieldNumber].Pack(isoFields[fieldNumber], packedBytes, ref i);
                }
                
            }
            //int totFields = bitmap.GetLengthInBits(); // get max number of fields that this message can have            

            //for (int fieldNumber = 2; fieldNumber <= totFields; fieldNumber++)
            //{
            //    if (fieldNumber != 65 && fieldNumber != 129) // special bit fields indicating existance of second and tird bitmap (VISA BASE I Specifications)
            //    {
            //        if (bitmap.BitIsSet(fieldNumber))
            //        {
            //            // the mandatory field is present so package it

            //            m_fieldPackagerList[fieldNumber].Pack(isoFields[fieldNumber], packedBytes, ref i);                        
            //        }
            //    }
            //}
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoField"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        public override void UnPack(ISOComponent isoField, byte[] packedBytes, ref int index)
        {
            bool allMandatoryExist = true;

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
                // special bit fields indicating existance of third and fourth bitmap should not try to pack them
                int fieldNumber = setFields[k];
                if (fieldNumber >= 2 && fieldNumber != 65 && fieldNumber != 129)
                {
                   
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
            //int totFields = bitmap.GetLengthInBits();

            //for (int fieldNumber = 2; fieldNumber <= totFields; fieldNumber++)
            //{
            //    // special bit fields indicating existance of third and fourth bitmap should not try to pack them

            //    if (fieldNumber != 65 && fieldNumber != 129)
            //    {
            //        // check if current field number is present on message bitmap

            //        if (bitmap.BitIsSet(fieldNumber)) 
            //        {
            //            if (m_fieldPackagerList[fieldNumber].IsComposite())
            //            {
            //                //if (m_fieldPackagerList[fieldNumber].GetStorageClass() == "ISO8583Net.ISOMessageSubFields")
            //                //{
            //                isoFields[fieldNumber] = new ISOFieldBitmapSubFields(Logger, (ISOFieldBitmapSubFieldsPackager)m_fieldPackagerList[fieldNumber], m_fieldPackagerList[fieldNumber].GetFieldNumber());
            //                //}
            //            }
            //            else
            //            {
            //                    isoFields[fieldNumber] = new ISOField(Logger, m_fieldPackagerList[fieldNumber], m_fieldPackagerList[fieldNumber].GetFieldNumber());
            //            }

            //            m_fieldPackagerList[fieldNumber].UnPack(isoFields[fieldNumber], packedBytes, ref index);
            //        }
            //    }
            //}
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

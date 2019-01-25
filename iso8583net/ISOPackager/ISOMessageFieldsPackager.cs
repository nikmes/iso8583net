using ISO8583Net.Field;
using Microsoft.Extensions.Logging;
using System;
using System.Text;

namespace ISO8583Net.Packager
{
    public class ISOMessageFieldsPackager : ISOPackager
    {
        private ISOMessageTypesPackager m_isoMsgTypePackager;

        private ISOPackager[] m_fieldPackagerList;

        private int m_totalFields;

        private bool m_fieldParticipationValidations = false;

        public ISOMessageFieldsPackager(ILogger logger, int fieldNumber, int totalFields, ISOFieldDefinition isoFieldDefinition) : base (logger, isoFieldDefinition)
        {
            m_totalFields = totalFields;

            m_number = fieldNumber;

            m_composite = true;

            m_isoMsgTypePackager = new ISOMessageTypesPackager(logger, m_totalFields);

            m_fieldPackagerList = new ISOPackager[totalFields+1];
        }

        public ISOMessageFieldsPackager(ILogger logger, int fieldNumber, int totalFields) : base(logger)
        {
            m_totalFields = totalFields;

            m_number = fieldNumber;

            m_composite = true;

            m_isoMsgTypePackager = new ISOMessageTypesPackager(logger, m_totalFields);

            m_fieldPackagerList = new ISOPackager[totalFields + 1];
        }

        public void SetMessageTypesPackager(ISOMessageTypesPackager isoMessageTypesPackager)
        {
            m_isoMsgTypePackager = isoMessageTypesPackager;
        }

        public void Add(ISOPackager fieldPackager, int number)
        {
            m_fieldPackagerList[number]=fieldPackager;
        }

        public void EnableFieldParticipationValidations(bool enabled)
        {
            m_fieldParticipationValidations = enabled;
        }

        public override void Pack(ISOComponent isoMessageFields, byte[] packedBytes, ref int i)
        {
            bool allMandatoryExist = true;

            ISOComponent[] isoFields = ((ISOMessageFields)(isoMessageFields)).GetFields();

            //String msgType = isoFields[0].GetValue(); // If the Message Type is supported from the packager definition then do the below else Log an Error and return a 0 byte array

            //ISOFieldBitmap manBitmap = m_isoMsgTypePackager.GetMandatoryBitmap(msgType); // get the Bitmap of message type that indicates mandatory fields

            m_fieldPackagerList[0].Pack(isoFields[0], packedBytes, ref i); // pack the message type to the byteArray for transmission

            m_fieldPackagerList[1].Pack(isoFields[1], packedBytes, ref i); // pack the Bitmap to the byteArray for transmission

            int totFields = ((ISOFieldBitmap)isoFields[1]).GetLengthInBits(); // get max number of fields that this message can have            

            for (int fieldNumber = 2; fieldNumber <= totFields; fieldNumber++)
            {
                if (fieldNumber != 65 && fieldNumber != 129) // special bit fields indicating existance of second and tird bitmap (VISA BASE I Specifications)
                {
                    // check if current field number is present on message bitmap

                    //if (manBitmap.BitIsSet(fieldNumber))
                    //{
                    // it is a mandatory field

                    if (((ISOFieldBitmap)isoFields[1]).BitIsSet(fieldNumber))
                    {
                        // the mandatory field is present so package it

                        m_fieldPackagerList[fieldNumber].Pack(isoFields[fieldNumber], packedBytes, ref i);
                        
                        //((ISOField)(isoFields[fieldNumber])).Pack(packedBytes, ref i);

                    }
                    //else
                    //{
                    // the madnatory field is not present in the iso message we have a problem

                    //    allMandatoryExist = false;
                    //}
                    //}
                    //else
                    //{
                    // if is not a mandatory field is an Optional or Conditional so package it if is set in the bitmap

                    //if (((ISOFieldBitmap)isoFields[1]).BitIsSet(fieldNumber))
                    //{
                    //    m_fieldPackagerList[fieldNumber].Pack(isoFields[fieldNumber], packedBytes, ref i);
                    //}
                    //}
                }
            }

            if (!allMandatoryExist)
            {
                //if (Logger.IsEnabled(LogLevel.Critical)) Logger.LogCritical("Mandatory Field is missing! Should I pack the message?");
            }
        }

        public override void UnPack(ISOComponent isoField, byte[] packedBytes, ref int index)
        {
            bool allMandatoryExist = true;

            ISOComponent[] isoFields = ((ISOMessageFields)(isoField)).GetFields();

            // Unpack the message type from the byteArray for transmission

            isoFields[0] = new ISOField(Logger, m_fieldPackagerList[0], m_fieldPackagerList[0].GetFieldNumber());

            m_fieldPackagerList[0].UnPack(isoFields[0], packedBytes, ref index);

            String msgType = isoFields[0].GetValue();

            // If the Message Type is supported from the packager definition then do the below else Log an Error and return a 0 byte array

            // Unpack the Bitmap from the byteArray for transmission

            isoFields[1] = new ISOFieldBitmap(Logger, m_fieldPackagerList[1], m_fieldPackagerList[1].GetFieldNumber());

            m_fieldPackagerList[1].UnPack(isoFields[1], packedBytes, ref index);

            // once this is caleed then start traversing all m_msgFieldPackager items and if isoMessage.BitIsSet() then call their Pack()
            
            int totFields = ((ISOFieldBitmap)isoFields[1]).GetLengthInBits();

            //ISOFieldBitmap manBitmap = m_isoMsgTypePackager.GetMandatoryBitmap(msgType);

            for (int fieldNumber = 2; fieldNumber <= totFields; fieldNumber++)
            {
                // special bit fields indicating existance of third and fourth bitmap should not try to pack them

                if (fieldNumber != 65 && fieldNumber != 129)
                {
                    // check if current field number is present on message bitmap

                    //if (manBitmap.BitIsSet(fieldNumber)) 
                    //{
                        // it is a mandatory field

                        if (((ISOFieldBitmap)isoFields[1]).BitIsSet(fieldNumber)) 
                        {
                            // the mandatory field is present so package it, check if is ISOMessageFields Field
                            if (m_fieldPackagerList[fieldNumber].IsComposite())
                            {
                                //if (m_fieldPackagerList[fieldNumber].GetStorageClass() == "ISO8583Net.ISOMessageSubFields")
                                //{
                                    isoFields[fieldNumber] = new ISOFieldBitmapSubFields(Logger, (ISOFieldBitmapSubFieldsPackager)m_fieldPackagerList[fieldNumber], m_fieldPackagerList[fieldNumber].GetFieldNumber());
                                //}
                            }
                            else
                            {
                                isoFields[fieldNumber] = new ISOField(Logger, m_fieldPackagerList[fieldNumber], m_fieldPackagerList[fieldNumber].GetFieldNumber());
                            }

                            m_fieldPackagerList[fieldNumber].UnPack(isoFields[fieldNumber], packedBytes, ref index);
                        }
                        //else
                        //{
                            // the madnatory field is not present in the iso message we have a problem

                        //    allMandatoryExist = false;
                        //}
                    //}
                    //else
                    //{
                    //    // if is not a mandatory field is an Optional or Conditional so package it

                    //    if (((ISOFieldBitmap)isoFields[1]).BitIsSet(fieldNumber)) 
                    //    {
                    //        if (m_fieldPackagerList[fieldNumber].GetStorageClass() == "ISO8583Net.ISOMessageSubFields")
                    //        {
                    //            isoFields[fieldNumber] = new ISOMessageSubFields(Logger, (ISOMessageSubFieldsPackager)m_fieldPackagerList[fieldNumber], m_fieldPackagerList[fieldNumber].GetFieldNumber());
                    //        }
                    //        else
                    //        {
                    //            isoFields[fieldNumber] = new ISOField(Logger, m_fieldPackagerList[fieldNumber], m_fieldPackagerList[fieldNumber].GetFieldNumber());
                    //        }

                    //        m_fieldPackagerList[fieldNumber].UnPack(isoFields[fieldNumber], packedBytes, ref index);
                    //    }
                    //}

                }
            }

            if (!allMandatoryExist)
            {
                //if (Logger.IsEnabled(LogLevel.Critical)) Logger.LogCritical("Mandatory Field is missing! Should we discard the Unpacked message?");
            }
        }

        public override String ToString()
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

        public ISOPackager GetFieldPackager(int fieldNumber)
        {
            return m_fieldPackagerList[fieldNumber];
        }

        public int GetTotalFields()
        {
            return m_totalFields;
        }

    }
}

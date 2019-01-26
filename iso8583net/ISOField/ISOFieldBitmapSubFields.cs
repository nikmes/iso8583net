using ISO8583Net.Packager;
using Microsoft.Extensions.Logging;
using System;
using System.Text;

namespace ISO8583Net.Field
{
    public class ISOFieldBitmapSubFields : ISOComponent
    {
        protected ISOComponent[] m_isoFields;

        protected ISOFieldBitmapSubFieldsPackager m_packager;

        public ISOFieldBitmapSubFields(ILogger logger, ISOFieldBitmapSubFieldsPackager packager, int fieldNumber) : base(logger, fieldNumber)
        {
            m_packager = packager;

            m_isoFields = new ISOComponent[m_packager.totalFields];

            m_isoFields[0] = new ISOFieldBitmap(Logger, (ISOFieldPackager)packager.GetFieldPackager(0), 0);
        }

        public override void SetValue(string value)
        {
            throw new NotImplementedException();
        }
        
        /// <summary>
        /// Assigns value to field <paramref name="fieldNumber"/>
        /// </summary>
        /// <param name="fieldNumber">The numeric value of the iso field</param>
        /// <param name="fieldValue">The value (as string) to be assigned to the iso field</param>
        /// <example>SetFieldValue(2,"4000XXXXXXXX4000")</example>
        public override void SetValue(int fieldNumber, String fieldValue)
        {
            if (m_isoFields[fieldNumber] != null)
            {
                m_isoFields[fieldNumber].SetValue(fieldValue);
            }
            else
            {
                if (SetFieldPackager(fieldNumber))
                {
                    m_isoFields[fieldNumber].SetValue(fieldValue);

                    //if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("Trying to set SubField [" + fieldNumber.ToString() + "] of Field [" + m_number + "]");

                    ((ISOFieldBitmap)m_isoFields[0]).SetBit(fieldNumber); 
                }
                else
                {
                   if (Logger.IsEnabled(LogLevel.Error)) Logger.LogError("Trying to set SubField [" + fieldNumber + "] of Field [" + m_number + "] that dose not exist in packager definition file");
                }
            }
        }

        public ISOComponent GetField(int fieldNumber)
        {
            return m_isoFields[fieldNumber];
        }

        public ISOComponent[] GetFields()
        {
            return m_isoFields;
        }

        public override String GetValue()
        {
            StringBuilder strBuilder = new StringBuilder();

            for (int i = 0; i < m_packager.totalFields; i++)
            {
                if (m_isoFields[i] != null)
                {
                    String str = m_isoFields[i].GetValue();

                    strBuilder.Append(str);
                }
            }

            return strBuilder.ToString();
        }

        public override String GetFieldValue(int fieldNumber)
        {
            return m_isoFields[fieldNumber].GetValue();
        }

        public override String GetFieldValue(int fieldNumber, int subField)
        {
            throw new NotImplementedException();
        }

        public bool SetFieldPackager(int fieldNumber)
        {
            ISOPackager fieldPackager = m_packager.GetFieldPackager(fieldNumber);

            if (m_isoFields[fieldNumber] == null && fieldPackager!=null) // field is not initialized and packager was intialzied from xml for this field
            {
                if (fieldPackager.IsComposite())
                {
                    m_isoFields[fieldNumber] = new ISOFieldBitmapSubFields(Logger, (ISOFieldBitmapSubFieldsPackager)fieldPackager, fieldNumber);
             
                    return true;
                }
                else
                {
                    m_isoFields[fieldNumber] = new ISOField(Logger, fieldPackager, fieldNumber);

                    return true;
                }
            }
            else
            {
                Logger.LogError("Field Packager was not initialized from XML Packager definition file");
                return false;
            }
        }

        public override String ToString()
        {
            StringBuilder msgFieldValues = new StringBuilder();

            msgFieldValues.Append("Field [" + m_number.ToString().PadLeft(3, '0') + "]".PadRight(5, ' ') + "[" + this.GetValue() + "]\n");

            for (int i = 0; i < m_packager.totalFields; i++)
            {
                if (m_isoFields[i] != null && (((ISOFieldBitmap)m_isoFields[0]).BitIsSet(i) || i==0))
                {
                    msgFieldValues.Append("      [" + m_number.ToString().PadLeft(3, '0') + "." + i.ToString().PadLeft(2, '0') + "]".PadRight(2, ' ') + "[" + m_isoFields[i].GetValue() + "]\n");

                    if (i == 0)
                    {
                        msgFieldValues.Append(((ISOFieldBitmap)m_isoFields[i]).ToHumanReadable("               ") + "\n");
                    }
                }
            }
            return msgFieldValues.ToString();
        }

        public override void Trace()
        {
            throw new NotImplementedException();
        }

    }
}

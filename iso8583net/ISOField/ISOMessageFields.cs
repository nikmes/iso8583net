using ISO8583Net.Packager;
using Microsoft.Extensions.Logging;
using System;
using System.Text;

namespace ISO8583Net.Field
{
    public class ISOMessageFields : ISOComponent
    {
        protected ISOComponent[] m_isoFields;

        protected ISOMessageFieldsPackager m_packager;

        public ISOMessageFields(ILogger logger, ISOMessageFieldsPackager packager, int fieldNumber) : base(logger, fieldNumber)
        {
            m_packager = packager;

            m_isoFields = new ISOComponent[m_packager.GetTotalFields()];

            m_isoFields[1] = new ISOFieldBitmap(Logger, (ISOFieldPackager)packager.GetFieldPackager(1), 1);
        }

        public override void SetValue(string value)
        {
            throw new NotImplementedException();
        }

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

                    if (fieldNumber > 0) 
                    {
                        ((ISOFieldBitmap)m_isoFields[1]).SetBit(fieldNumber); 
                    }
                }
                else
                {
                    Logger.LogError("Trying to set Field [" + fieldNumber + "] that dose not exist in packager definition file");
                }
            }
        }

        public void SetValue(int fieldNumber, int subFieldNumber, String fieldValue)
        {
            if (m_isoFields[fieldNumber] == null)
            {
                // field is not initialized yet in the dictionary so initialize it and set th

                if (SetFieldPackager(fieldNumber))
                {
                    ((ISOFieldBitmap)m_isoFields[1]).SetBit(fieldNumber);                    

                    m_isoFields[fieldNumber].SetValue(subFieldNumber, fieldValue);
                }
                else
                {
                    Logger.LogError("Trying to set SubField [" + subFieldNumber.ToString() + "] of Field [" + fieldNumber + "] that dose not exist in packager definition file");
                }
            }
            else
            {
                m_isoFields[fieldNumber].SetValue(subFieldNumber, fieldValue);
            }
        }

        public bool SetFieldPackager(int fieldNumber)
        {
            ISOPackager fieldPackager = m_packager.GetFieldPackager(fieldNumber);

            if (m_isoFields[fieldNumber] == null && fieldPackager != null) // field is not initialized and packager was intialzied from xml for this field
            {
                if (fieldPackager.IsComposite())
                {
                    // Logger.LogTrace("Field [" + fieldNumber.ToString().PadLeft(3, '0') + "] is composite    , set ISOPackager = ISOMessageSubFields");

                    m_isoFields[fieldNumber] = new ISOFieldBitmapSubFields(Logger, (ISOFieldBitmapSubFieldsPackager)fieldPackager, fieldNumber);

                    return true;
                }
                else
                {
                    // Logger.LogTrace("Field [" + fieldNumber.ToString().PadLeft(3, '0') + "] is NOT composite, set ISOPackager = ISOField");

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

            int totalFields = m_packager.GetTotalFields();

            for (int i = 0; i < totalFields; i++)
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

        public override String ToString()
        {
            StringBuilder msgFieldValues = new StringBuilder();

            for (int i = 0; i < m_packager.GetTotalFields(); i++)
            {
                if (m_isoFields[i] != null && (((ISOFieldBitmap)m_isoFields[1]).BitIsSet(i) || i==0 || i==1))
                {
                    msgFieldValues.Append(m_isoFields[i].ToString());
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

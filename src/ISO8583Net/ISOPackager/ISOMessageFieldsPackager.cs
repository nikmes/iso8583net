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

        private DialectValidationMode m_fieldParticipationValidationMode = DialectValidationMode.Off;

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
        /// Enables (true) or disables (false) outbound field-participation validation.
        /// Equivalent to <see cref="SetFieldParticipationValidationMode"/> with
        /// <see cref="DialectValidationMode.On"/> / <see cref="DialectValidationMode.Off"/>.
        /// Kept for backward compatibility.
        /// </summary>
        /// <param name="enabled"></param>
        public void EnableFieldParticipationValidations(bool enabled)
        {
            m_fieldParticipationValidationMode =
                enabled ? DialectValidationMode.On : DialectValidationMode.Off;
        }

        /// <summary>
        /// Sets the outbound validation mode. <see cref="DialectValidationMode.Off"/> disables
        /// validation, <see cref="DialectValidationMode.Warn"/> logs a warning on violation without
        /// throwing, and <see cref="DialectValidationMode.On"/> throws on the first violation.
        /// </summary>
        public void SetFieldParticipationValidationMode(DialectValidationMode mode)
        {
            m_fieldParticipationValidationMode = mode;
        }

        /// <summary>
        /// The current outbound validation mode. Defaults to <see cref="DialectValidationMode.Off"/>.
        /// </summary>
        public DialectValidationMode FieldParticipationValidationMode => m_fieldParticipationValidationMode;

        /// <summary>
        /// True when outbound field-participation validation is enabled in either
        /// <see cref="DialectValidationMode.Warn"/> or <see cref="DialectValidationMode.On"/> mode.
        /// Defaults to false, so existing callers are unaffected until they opt in.
        /// </summary>
        public bool FieldParticipationValidations => m_fieldParticipationValidationMode != DialectValidationMode.Off;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoMessageFields"></param>
        /// <param name="packedBytes"></param>
        /// <param name="i"></param>
        public override void Pack(ISOComponent isoMessageFields, byte[] packedBytes, ref int i)
        {
            ISOComponent[] isoFields = ((ISOMessageFields)(isoMessageFields)).GetFields();

            // Outbound field-participation validation (mode-controlled). When the mode is
            // Warn or On, validate MTI membership + field participation before any bytes are
            // written. Warn logs the violation and continues; On throws to fail fast.
            if (m_fieldParticipationValidationMode != DialectValidationMode.Off)
            {
                string mti = isoFields[0].value;
                var validationBitmap = isoFields[1] as ISOFieldBitmap;
                var result = DialectValidator.Validate(this, mti, validationBitmap);
                if (!result.IsValid)
                {
                    if (m_fieldParticipationValidationMode == DialectValidationMode.Warn)
                    {
                        if (Logger.IsEnabled(LogLevel.Warning))
                            Logger.LogWarning(
                                "Dialect validation warning [{Mti}]: {Message} Missing=[{Missing}] Disallowed=[{Disallowed}]",
                                mti, result.Message,
                                string.Join(",", result.MissingMandatoryFields),
                                string.Join(",", result.DisallowedFields));
                    }
                    else
                    {
                        throw new DialectValidationException(result);
                    }
                }
            }

            m_fieldPackagerList[0].Pack(isoFields[0], packedBytes, ref i);

            // Bitmap (field 1) is variable-length based on which bits are set —
            // write only the bytes actually needed (8/16/24) rather than the
            // dialect's declared fixed length.
            var bitmap = isoFields[1] as ISOFieldBitmap;
            if (bitmap != null)
            {
                byte[] bitmapBytes = bitmap.GetByteArray();
                Buffer.BlockCopy(bitmapBytes, 0, packedBytes, i, bitmapBytes.Length);
                i += bitmapBytes.Length;
            }
            else
            {
                m_fieldPackagerList[1].Pack(isoFields[1], packedBytes, ref i);
            }

            if (bitmap != null)
            {
                Span<int> setFields = stackalloc int[193];
                int count = bitmap.GetSetFields(setFields);

                for (int k = 0; k < count; k++)
                {
                    int fieldNumber = setFields[k];
                    // Skip bitmap indicator bits (fields 65 and 129)
                    if (fieldNumber >= 2 && fieldNumber != BitmapBoundaries.SecondaryBitmapFlag && fieldNumber != BitmapBoundaries.TertiaryBitmapFlag)
                    {
                        m_fieldPackagerList[fieldNumber].Pack(isoFields[fieldNumber], packedBytes, ref i);
                    }
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

            // Guard against bitmap-less / truncated inbound messages. A valid ISO 8583
            // message always carries at least a primary bitmap (8 bytes) after the MTI.
            // Error signals (e.g. a D8 header with FieldInError set and only an MTI) may
            // carry no bitmap; attempting to read it would throw IndexOutOfRangeException.
            int bytesAfterMti = packedBytes.Length - index;
            if (bytesAfterMti < ISOFieldBitmap.MinimumLengthBytes)
            {
                if (Logger.IsEnabled(LogLevel.Warning))
                    Logger.LogWarning("Inbound message has MTI [{MessageType}] but only {BytesAfterMti} byte(s) after the MTI — no bitmap present. Bitmap left empty.",
                        msgType, bytesAfterMti);
                return;
            }

            // Unpack the Bitmap from the byteArray for transmission

            isoFields[1] = new ISOFieldBitmap(Logger, m_fieldPackagerList[1], m_fieldPackagerList[1].GetFieldNumber());

            m_fieldPackagerList[1].UnPack(isoFields[1], packedBytes, ref index);

            var bitmap = isoFields[1] as ISOFieldBitmap;
            Span<int> setFields = stackalloc int[193];
            int count = bitmap.GetSetFields(setFields);

            for (int k = 0; k < count; k++)
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
                        var existing = isoFields[fieldNumber];
                        if (existing != null)
                            existing.Reset();
                        else
                            isoFields[fieldNumber] = new ISOFieldBitmapSubFields(Logger, (ISOFieldBitmapSubFieldsPackager)m_fieldPackagerList[fieldNumber], m_fieldPackagerList[fieldNumber].GetFieldNumber());
                    }
                    else
                    {
                        var existing = isoFields[fieldNumber];
                        if (existing != null)
                            existing.Reset();
                        else
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
        /// Returns the message types packager — the entry point for enumerating the supported
        /// message types and for validating an MTI/bitmap against the dialect (via
        /// <see cref="DialectValidator"/>).
        /// </summary>
        public ISOMessageTypesPackager GetMessageTypesPackager()
        {
            return m_isoMsgTypePackager;
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

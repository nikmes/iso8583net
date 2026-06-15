using ISO8583Net.Field;
using ISO8583Net.Header;
using ISO8583Net.Packager;
using ISO8583Net.Types;
using ISO8583Net.Utilities;
using Microsoft.Extensions.Logging;
using System;
using System.Text;

namespace ISO8583Net.Message
{
    /// <summary>
    /// Represents a complete ISO 8583 financial message.
    /// Provides methods to set/get fields (including sub-fields), pack to bytes, and unpack from bytes.
    /// </summary>
    public class ISOMessage : ISOComponent
    {
        /// <summary>The message packager that defines field layout and encoding.</summary>
        protected ISOMessagePackager m_isoMesssagePackager;
        /// <summary>The collection of ISO fields in this message.</summary>
        protected ISOMessageFields m_isoMessageFields;
        /// <summary>The header packager, or null if no header is configured.</summary>
        protected ISOHeaderPackager m_isoHeaderPackager = null;
        /// <summary>The header instance, or null if no header is configured.</summary>
        protected ISOHeader m_isoHeader = null;
        /// <summary>Total number of fields defined in the dialect.</summary>
        protected int m_totalFields;

        /// <summary>
        /// Creates a new ISO message with a specific header packager.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="messagePackager">The message packager loaded from an XML dialect.</param>
        /// <param name="isoHeaderPackager">The header packager to use, or null for no header.</param>
        public ISOMessage(ILogger logger, ISOMessagePackager messagePackager, ISOHeaderPackager isoHeaderPackager) : base(logger, 0)
        {
            m_isoMesssagePackager = messagePackager;
            m_isoHeaderPackager = isoHeaderPackager;

            m_isoMessageFields = new ISOMessageFields(Logger, m_isoMesssagePackager.GetISOMessageFieldsPackager(), 0);

            // Create the appropriate header instance based on the header packager type
            if (m_isoHeaderPackager != null)
            {
                m_isoHeader = CreateHeaderForPackager(Logger, m_isoHeaderPackager);
            }

            m_totalFields = ((ISOMessagePackager)m_isoMesssagePackager).GetTotalFields();
        }

        /// <summary>
        /// Creates a new ISO message using the header configuration from the XML dialect.
        /// If the dialect specifies a header packager (e.g. "ISOHeaderVisaPackager"), it is used automatically.
        /// If no header is configured, the message is created without a header.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="isoMessagePackager">The message packager loaded from a JSON dialect definition.</param>
        public ISOMessage(ILogger logger, ISOMessagePackager isoMessagePackager) : base(logger, 0)
        {
            m_isoMesssagePackager = isoMessagePackager;

            m_isoMessageFields = new ISOMessageFields(Logger, m_isoMesssagePackager.GetISOMessageFieldsPackager(), 0);

            // Resolve the header packager from the XML dialect configuration
            string headerPackagerName = m_isoMesssagePackager.HeaderPackagerName;
            if (!string.IsNullOrEmpty(headerPackagerName))
            {
                m_isoHeaderPackager = CreateHeaderPackagerByName(Logger, headerPackagerName);
                if (m_isoHeaderPackager != null)
                {
                    m_isoHeader = CreateHeaderForPackager(Logger, m_isoHeaderPackager);
                }
            }

            m_totalFields = ((ISOMessagePackager)m_isoMesssagePackager).GetTotalFields();
        }

        /// <summary>
        /// Creates a header packager instance by its class name.
        /// </summary>
        private static ISOHeaderPackager CreateHeaderPackagerByName(ILogger logger, string packagerName)
        {
            Type packagerType = Type.GetType("ISO8583Net.Packager." + packagerName, throwOnError: false);
            if (packagerType == null)
            {
                packagerType = Type.GetType("ISO8583Net." + packagerName, throwOnError: false);
            }
            if (packagerType != null && typeof(ISOHeaderPackager).IsAssignableFrom(packagerType))
            {
                return (ISOHeaderPackager)Activator.CreateInstance(packagerType, logger);
            }
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Header packager type '" + packagerName + "' not found or not an ISOHeaderPackager. Message will have no header.");
            return null;
        }

        /// <summary>
        /// Creates the appropriate ISOHeader instance for the given header packager type.
        /// </summary>
        private static ISOHeader CreateHeaderForPackager(ILogger logger, ISOHeaderPackager packager)
        {
            if (packager is ISOHeaderVisaPackager)
                return new ISOHeaderVisa(logger, packager);
            if (packager is ISOHeaderD8Packager)
                return new ISOHeaderD8(logger, packager);
            // Default: create a generic Visa header (most common)
            return new ISOHeaderVisa(logger, packager);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fieldNumber"></param>
        /// <param name="fieldValue"></param>
        public override void Set(int fieldNumber, string fieldValue)
        {
            if (fieldNumber >= 0 && fieldNumber <= m_totalFields) 
            {
                m_isoMessageFields.Set(fieldNumber, fieldValue);
            }
            else
            {
                Logger.LogError("Attempt to set value for an out of range field[", fieldNumber.ToString().PadLeft(3, ' ') + "]"); 
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fieldNumber"></param>
        /// <param name="subFieldNumber"></param>
        /// <param name="fieldValue"></param>
        public void Set(int fieldNumber, int subFieldNumber, string fieldValue)
        {
            if (fieldNumber >= 0 && fieldNumber <= m_totalFields
                && fieldNumber != BitmapBoundaries.SecondaryBitmapFlag
                && fieldNumber != BitmapBoundaries.TertiaryBitmapFlag)
            {
                m_isoMessageFields.SetValue(fieldNumber, subFieldNumber, fieldValue);                
            }
            else
            {
                Logger.LogError("Attempt to set value for an out of range field[", fieldNumber.ToString().PadLeft(3, ' ') + "]");
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fieldNumber"></param>
        /// <returns></returns>
        public override string GetFieldValue(int fieldNumber)
        {
            return m_isoMessageFields.GetFieldValue(fieldNumber);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fieldNumber"></param>
        /// <returns></returns>
        public ISOComponent GetField(int fieldNumber)
        {
            return m_isoMessageFields.GetField(fieldNumber);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fieldNumber"></param>
        /// <param name="subField"></param>
        /// <returns></returns>
        public override string GetFieldValue(int fieldNumber, int subField)
        {
            return m_isoMessageFields.GetFieldValue(subField);
        }
        
        /// <summary>
        /// Pack message using temporaty packedBytes, returns new byte array of final result
        /// </summary>
        /// <param name="packedBytes"></param>
        /// <returns></returns>
        protected byte[] Pack(byte[] packedBytes)
        {
            int index = 0;
            // if there is ISOMessage Header try pack it
            if (m_isoHeaderPackager != null && m_isoHeader != null)
            {
                //start packing the message after the header position
                index = m_isoHeader.Length();
            }
            // pack the isoMessage - is without message header
            m_isoMesssagePackager.Pack(m_isoMessageFields, packedBytes, ref index);

            // if there is ISOMessage Header try pack it
            if (m_isoHeaderPackager != null && m_isoHeader != null)
            {
                // set total message legnth in header
                m_isoHeader.SetMessageLength(index);

                // pack the isoHeader of the isoMessage
                int headerIndex = 0;
                m_isoHeaderPackager.Pack(m_isoHeader, packedBytes, ref headerIndex);                
            }
           
            return packedBytes.AsSpan(0, index).ToArray();

        }
        /// <summary>
        /// Pack message using an ArrayPool
        /// </summary>
        /// <returns></returns>
        public byte[] PackPooled()
        {
            byte[] tmpBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(2048);
            try
            {

                return Pack(tmpBuffer);
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(tmpBuffer);
            }

        }
        /// <summary>
        /// Pack Message
        /// </summary>
        /// <returns></returns>
        public byte[] Pack()
        {
            byte[] packedBytes = new byte[2048];

            return Pack(packedBytes);

        }

        /// <summary>
        /// Unpack Message
        /// </summary>
        /// <param name="packedBytes"></param>
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
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder msgFieldValues = new StringBuilder();

            msgFieldValues.Append("ISO Message Content: \n");

            msgFieldValues.Append(m_isoMessageFields.ToString());

            return msgFieldValues.ToString();
        }
        /// <summary>
        /// 
        /// </summary>
        public override void Trace()
        {
            m_isoMessageFields.Trace();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ISOMessage ToCommonISO()
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoMessage"></param>
        public void FromCommonISO(ISOMessage isoMessage)
        {
            throw new NotImplementedException();
        }
    }
}

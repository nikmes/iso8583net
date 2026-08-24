using ISO8583Net.Field;
using ISO8583Net.Types;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Text;

namespace ISO8583Net.Packager
{
    /// <summary>
    /// 
    /// </summary>
    public class ISOMsgTypePackager
    {
        private readonly ILogger _logger;

        internal ILogger Logger { get { return _logger; } }


        private int m_totalFields;
        /// <summary>
        /// 
        /// </summary>

        public string messageTypeIdentifier;
        /// <summary>
        /// 
        /// </summary>

        public string messageTypeName;
        /// <summary>
        /// 
        /// </summary>

        public string messageTypeDescription;

        /// <summary>
        /// 
        /// </summary>
        public ISOFieldBitmap m_manBitmap;
        /// <summary>
        /// 
        /// </summary>

        public ISOFieldBitmap m_conBitmap;
        /// <summary>
        /// 
        /// </summary>
        public ISOFieldBitmap m_optBitmap;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="totalFields"></param>
        public ISOMsgTypePackager(ILogger logger, int totalFields)
        {
            _logger = logger;

            m_totalFields = totalFields;

            m_manBitmap = new ISOFieldBitmap(Logger);

            m_conBitmap = new ISOFieldBitmap(Logger);

            m_optBitmap = new ISOFieldBitmap(Logger);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder strBuilder = new StringBuilder("");

            // Format ISO Message Type Definition
            strBuilder.Append("\nMessage Type Definition:\n");
            strBuilder.Append("         MIT : [" + messageTypeIdentifier + "]\n");
            strBuilder.Append("        Name : [" + messageTypeName + "]\n");
            strBuilder.Append(" Description : [" + messageTypeDescription + "]\n");

            // Format Field Participation


            return strBuilder.ToString();
        }
        /// <summary>
        /// 
        /// </summary>
        public void Trace()
        {
            if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("\nMessage Type Definition:");
            if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("         MIT : [" + messageTypeIdentifier + "]");
            if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("        Name : [" + messageTypeName + "]");
            if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation(" Description : [" + messageTypeDescription + "]");
        }
        /// <summary>
        /// Compares the supplied message bitmap against this message type's mandatory,
        /// conditional, and optional field participation bitmaps, returning the missing
        /// mandatory fields and any disallowed fields present.
        /// </summary>
        /// <param name="isoMsgBitmap">The bitmap from the message being validated.</param>
        /// <returns>A structured validation result.</returns>
        public DialectValidationResult ValidateBitmap(ISOFieldBitmap isoMsgBitmap)
        {
            var missingMandatory = new List<int>();
            var disallowed = new List<int>();

            // Fields 0 (MTI) and 1 (the bitmap itself) are not data fields, and fields
            // 65/129 are bitmap continuation flags rather than data fields — skip them all.
            // A null bitmap is treated as an empty bitmap: no data fields are present.
            for (int fn = 2; fn < m_totalFields; fn++)
            {
                if (fn == BitmapBoundaries.SecondaryBitmapFlag || fn == BitmapBoundaries.TertiaryBitmapFlag)
                    continue;

                bool inMessage = isoMsgBitmap != null && isoMsgBitmap.BitIsSet(fn);
                bool mandatory = m_manBitmap.BitIsSet(fn);
                bool participates = mandatory || m_optBitmap.BitIsSet(fn) || m_conBitmap.BitIsSet(fn);

                if (mandatory && !inMessage)
                    missingMandatory.Add(fn);

                if (inMessage && !participates)
                    disallowed.Add(fn);
            }

            string message;
            if (missingMandatory.Count == 0 && disallowed.Count == 0)
            {
                message = "Message is valid.";
            }
            else
            {
                var sb = new StringBuilder();
                sb.Append("Message Type [").Append(messageTypeIdentifier).Append("] validation failed:");
                if (missingMandatory.Count > 0)
                    sb.Append(" missing mandatory fields ").AppendJoin(", ", missingMandatory).Append(';');
                if (disallowed.Count > 0)
                    sb.Append(" disallowed fields ").AppendJoin(", ", disallowed).Append(';');
                message = sb.ToString();
            }

            return new DialectValidationResult
            {
                IsMtiKnown = true,
                MissingMandatoryFields = missingMandatory,
                DisallowedFields = disallowed,
                Message = message
            };
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public byte[] GetMandatoryByteArray()
        {
            return m_manBitmap.GetByteArray();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public byte[] GetOptionalByteArray()
        {
            return m_optBitmap.GetByteArray();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public byte[] GetConditionalByteArray()
        {
            return m_conBitmap.GetByteArray();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ISOFieldBitmap GetMandatoryBitmap()
        {
            return m_manBitmap;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ISOFieldBitmap GetOptionalBitmap()
        {
            return m_optBitmap;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ISOFieldBitmap GetConditionalBitmap()
        {
            return m_conBitmap;
        }

    }
}

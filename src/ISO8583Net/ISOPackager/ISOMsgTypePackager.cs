using ISO8583Net.Field;
using Microsoft.Extensions.Logging;
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
        /// 
        /// </summary>
        /// <param name="isoMsgBitmap"></param>
        /// <returns></returns>
        public bool ValidateBitmap(ISOFieldBitmap isoMsgBitmap)
        {
            // copmare iso message bitmap with packagerSupportedFields bitmap and log the findings
            return false;
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

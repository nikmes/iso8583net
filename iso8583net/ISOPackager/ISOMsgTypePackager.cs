using ISO8583Net.Field;
using Microsoft.Extensions.Logging;
using System.Text;

namespace ISO8583Net.Packager
{
    public class ISOMsgTypePackager
    {
        private readonly ILogger _logger;

        internal ILogger Logger { get { return _logger; } }


        private int m_totalFields;

        public string messageTypeIdentifier;

        public string messageTypeName;

        public string messageTypeDescription;


        public ISOFieldBitmap m_manBitmap;

        public ISOFieldBitmap m_conBitmap;

        public ISOFieldBitmap m_optBitmap;


        public ISOMsgTypePackager(ILogger logger, int totalFields)
        {
            _logger = logger;

            m_totalFields = totalFields;

            m_manBitmap = new ISOFieldBitmap(Logger);

            m_conBitmap = new ISOFieldBitmap(Logger);

            m_optBitmap = new ISOFieldBitmap(Logger);
        }

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

        public void Trace()
        {
            if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("\nMessage Type Definition:");
            if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("         MIT : [" + messageTypeIdentifier + "]");
            if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("        Name : [" + messageTypeName + "]");
            if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation(" Description : [" + messageTypeDescription + "]");
        }

        public bool ValidateBitmap(ISOFieldBitmap isoMsgBitmap)
        {
            // copmare iso message bitmap with packagerSupportedFields bitmap and log the findings
            return false;
        }

        public byte[] GetMandatoryByteArray()
        {
            return m_manBitmap.GetByteArray();
        }

        public byte[] GetOptionalByteArray()
        {
            return m_optBitmap.GetByteArray();
        }

        public byte[] GetConditionalByteArray()
        {
            return m_conBitmap.GetByteArray();
        }
        public ISOFieldBitmap GetMandatoryBitmap()
        {
            return m_manBitmap;
        }

        public ISOFieldBitmap GetOptionalBitmap()
        {
            return m_optBitmap;
        }

        public ISOFieldBitmap GetConditionalBitmap()
        {
            return m_conBitmap;
        }

    }
}

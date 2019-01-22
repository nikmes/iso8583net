using Microsoft.Extensions.Logging;

namespace ISO8583Net.Header
{
    // Header Implementation

    public abstract class ISOHeader
    {
        protected ISOHeader m_isoHeader;

        private readonly ILogger _logger;

        internal ILogger Logger { get { return _logger; } }

        public ISOHeader(ILogger logger)
        {
            _logger = logger;
        }

        public abstract void Pack(byte[] packedBytes, ref int index);

        public abstract void UnPack(byte[] packedBytes, ref int index);

        public abstract int Length();

        public abstract void SetValue(byte[] bytes);

        public abstract void SetMessageLength(int length);

    }
}

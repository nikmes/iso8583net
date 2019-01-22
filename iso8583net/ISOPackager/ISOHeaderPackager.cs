using ISO8583Net.Header;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Packager
{
    public abstract class ISOHeaderPackager
    {
        private readonly ILogger _logger;

        internal ILogger Logger { get { return _logger; } }

        public ISOHeaderPackager(ILogger logger)
        {
            _logger = logger;
        }

        protected string m_storeageClass;

        public abstract void Pack(ISOHeader isoHeader, byte[] packedBytes, ref int index);

        public abstract void UnPack(ISOHeader isoHeader, byte[] packedBytes, ref int index);

        public abstract void Set(byte[] bytes);

        public abstract void Trace();
   }
}

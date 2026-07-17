using ISO8583Net.Header;
using Microsoft.Extensions.Logging;

namespace ISO8583Net.Packager
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class ISOHeaderPackager
    {
        private readonly ILogger _logger;

        internal ILogger Logger { get { return _logger; } }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        public ISOHeaderPackager(ILogger logger)
        {
            _logger = logger;
        }
        /// <summary>
        /// 
        /// </summary>
        protected string m_storeageClass;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoHeader"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        public abstract void Pack(ISOHeader isoHeader, byte[] packedBytes, ref int index);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoHeader"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        public abstract void UnPack(ISOHeader isoHeader, byte[] packedBytes, ref int index);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        public abstract void Set(byte[] bytes);
        /// <summary>
        /// 
        /// </summary>
        public abstract void Trace();
   }
}

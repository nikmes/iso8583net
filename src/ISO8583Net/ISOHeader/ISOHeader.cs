using Microsoft.Extensions.Logging;

namespace ISO8583Net.Header
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class ISOHeader
    {
        protected ISOHeader m_isoHeader;

        private readonly ILogger _logger;

        internal ILogger Logger { get { return _logger; } }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        public ISOHeader(ILogger logger)
        {
            _logger = logger;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        public abstract void Pack(byte[] packedBytes, ref int index);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        public abstract void UnPack(byte[] packedBytes, ref int index);
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public abstract int Length();
        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        public abstract void SetValue(byte[] bytes);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="length"></param>
        public abstract void SetMessageLength(int length);

    }
}

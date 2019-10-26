using Microsoft.Extensions.Logging;
using System;

namespace ISO8583Net.Field
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class ISOComponent
    {
        private readonly ILogger _logger;

        internal ILogger Logger { get { return _logger; } }

        protected int m_number;

        private string m_value;

        public virtual string value
        {
            get { return m_value; }
            set { m_value = value; }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="number"></param>
        public ISOComponent(ILogger logger, int number)
        {
            _logger = logger;

            m_number = number;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="number"></param>
        /// <param name="value"></param>
        public ISOComponent(ILogger logger, int number, string value)
        {
            _logger = logger;

            m_value = value;

            m_number = number;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public abstract override string ToString();
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fieldNumber"></param>
        /// <param name="fieldValue"></param>
        public abstract void Set(int fieldNumber, string fieldValue);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fieldNumber"></param>
        /// <returns></returns>
        public abstract string GetFieldValue(int fieldNumber);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fieldNumber"></param>
        /// <param name="subField"></param>
        /// <returns></returns>
        public abstract string GetFieldValue(int fieldNumber, int subField);
        /// <summary>
        /// 
        /// </summary>
        public abstract void Trace();
       
    }
}


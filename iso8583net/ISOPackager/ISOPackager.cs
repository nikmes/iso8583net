using ISO8583Net.Field;
using ISO8583Net.Interpreter;
using Microsoft.Extensions.Logging;
using System;

namespace ISO8583Net.Packager
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class ISOPackager
    {
        private readonly ILogger _logger;

        internal ILogger Logger { get { return _logger; } }

        private ISOInterpreter m_isoInterpreter;
        /// <summary>
        /// 
        /// </summary>

        protected bool m_composite = false;
        /// <summary>
        /// 
        /// </summary>

        protected int m_number;
        /// <summary>
        /// 
        /// </summary>

        protected string m_storeClass;
        /// <summary>
        /// 
        /// </summary>

        public ISOFieldDefinition m_isoFieldDefinition;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        public ISOPackager(ILogger logger)
        {
            _logger = logger;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="isoFieldDefinition"></param>
        public ISOPackager(ILogger logger, ISOFieldDefinition isoFieldDefinition)
        {
            _logger = logger;

            m_isoFieldDefinition = isoFieldDefinition;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoInterpreter"></param>
        public void SetISOInterpreter(ISOInterpreter isoInterpreter)
        {
            m_isoInterpreter = isoInterpreter;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="storageClass"></param>
        public void SetStorageClass(Type storageClass)
        {
            m_storeClass = storageClass.ToString();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="compositeIndicator"></param>
        public void SetComposite(bool compositeIndicator)
        {
            m_composite = compositeIndicator;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool IsComposite()
        {
            return m_composite;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int GetFieldNumber()
        {
            return m_number;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ISOFieldDefinition GetISOFieldDefinition()
        {
            return m_isoFieldDefinition;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoFieldDefinition"></param>
        public void SetISOFieldDefinition(ISOFieldDefinition isoFieldDefinition)
        {
            m_isoFieldDefinition = isoFieldDefinition;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fieldValue"></param>
        /// <returns></returns>
        public string InterpretField(string fieldValue)
        {
            if (m_isoInterpreter != null)
            {
                return m_isoInterpreter.ToString(fieldValue);
            }
            else
            {
                return string.Empty;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoField"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        public abstract void Pack(ISOComponent isoField, byte[] packedBytes, ref int index);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="isoField"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        public abstract void UnPack(ISOComponent isoField, byte[] packedBytes, ref int index);
        /// <summary>
        /// 
        /// </summary>
        public abstract void Trace();
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public abstract override string ToString();
    }
}

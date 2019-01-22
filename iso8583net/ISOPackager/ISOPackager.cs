using ISO8583Net.Field;
using ISO8583Net.Interpreter;
using Microsoft.Extensions.Logging;
using System;

namespace ISO8583Net.Packager
{
    public abstract class ISOPackager
    {
        private readonly ILogger _logger;

        internal ILogger Logger { get { return _logger; } }

        private ISOInterpreter m_isoInterpreter;

        protected bool m_composite = false;

        protected int m_number;

        protected string m_storeClass;

        public ISOFieldDefinition m_isoFieldDefinition;


        public ISOPackager(ILogger logger)
        {
            _logger = logger;
        }

        public ISOPackager(ILogger logger, ISOFieldDefinition isoFieldDefinition)
        {
            _logger = logger;

            m_isoFieldDefinition = isoFieldDefinition;
        }

        public void SetISOInterpreter(ISOInterpreter isoInterpreter)
        {
            m_isoInterpreter = isoInterpreter;
        }

        public void SetStorageClass(Type storageClass)
        {
            m_storeClass = storageClass.ToString();
        }

        public string GetStorageClass()
        {
            return m_storeClass;
        }

        public void SetComposite(bool compositeIndicator)
        {
            m_composite = compositeIndicator;
        }

        public bool IsComposite()
        {
            return m_composite;
        }

        public int GetFieldNumber()
        {
            return m_number;
        }

        public string GetContentCoding()
        {
            return m_isoFieldDefinition.contentCoding.ToString();
        }

        public string GetContentFormat()
        {
            return m_isoFieldDefinition.content.ToString();
        }
        
        public ISOFieldDefinition GetISOFieldDefinition()
        {
            return m_isoFieldDefinition;
        }

        public void SetISOFieldDefinition(ISOFieldDefinition isoFieldDefinition)
        {
            m_isoFieldDefinition = isoFieldDefinition;
        }

        public string GetFieldName()
        {
            return m_isoFieldDefinition.name;
        }

        public string InterpretField(String fieldValue)
        {
            if (m_isoInterpreter != null)
            {
                return m_isoInterpreter.ToString(fieldValue);
            }
            else
            {
                return String.Empty;
            }
        }

        public abstract void Pack(ISOComponent isoField, byte[] packedBytes, ref int index);

        public abstract void UnPack(ISOComponent isoField, byte[] packedBytes, ref int index);

        public abstract void Trace();

        public abstract override String ToString();
    }
}

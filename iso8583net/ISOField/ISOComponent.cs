﻿using Microsoft.Extensions.Logging;
using System;

namespace ISO8583Net.Field
{
    public abstract class ISOComponent
    {
        private readonly ILogger _logger;

        internal ILogger Logger { get { return _logger; } }

        protected int m_number;

        private String m_value;

        public virtual String value
        {
            get { return m_value; }
            set { m_value = value; }
        }

        public ISOComponent(ILogger logger, int number)
        {
            _logger = logger;

            m_number = number;
        }

        public ISOComponent(ILogger logger, int number, String value)
        {
            _logger = logger;

            m_value = value;

            m_number = number;
        }

        public abstract override String ToString();

        public abstract void SetValue(int fieldNumber, String fieldValue);

        public abstract String GetFieldValue(int fieldNumber);

        public abstract string GetFieldValue(int fieldNumber, int subField);

        public abstract void Trace();
       
    }
}


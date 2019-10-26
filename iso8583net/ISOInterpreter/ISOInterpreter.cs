using Microsoft.Extensions.Logging;
using System;

namespace ISO8583Net.Interpreter
{
    /// <summary>
    /// ISO Interpreter
    /// </summary>
    public abstract class ISOInterpreter
    {
        private readonly ILogger _logger;

        internal ILogger Logger { get { return _logger; } }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        public ISOInterpreter(ILogger logger)
        {
            _logger = logger;
        }

        public abstract string ToString(string value);
    }
}

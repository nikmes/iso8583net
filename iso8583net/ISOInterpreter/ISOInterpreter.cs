using Microsoft.Extensions.Logging;
using System;

namespace ISO8583Net.Interpreter
{
    public abstract class ISOInterpreter
    {
        private readonly ILogger _logger;

        internal ILogger Logger { get { return _logger; } }

        public ISOInterpreter(ILogger logger)
        {
            _logger = logger;
        }

        public abstract String ToString(string value);
    }
}

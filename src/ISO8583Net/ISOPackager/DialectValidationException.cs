using System;

namespace ISO8583Net.Packager
{
    /// <summary>
    /// Thrown when an outbound ISO 8583 message fails dialect validation, so the caller
    /// can fail fast before any bytes are produced.
    /// </summary>
    public class DialectValidationException : Exception
    {
        /// <summary>
        /// The structured validation result that describes the failure.
        /// </summary>
        public DialectValidationResult Result { get; }

        /// <summary>
        /// Creates an exception that carries the given validation result.
        /// </summary>
        public DialectValidationException(DialectValidationResult result)
            : base(result?.Message)
        {
            Result = result;
        }
    }
}

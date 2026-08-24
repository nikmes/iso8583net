using System;
using System.Collections.Generic;

namespace ISO8583Net.Packager
{
    /// <summary>
    /// The outcome of validating an ISO 8583 message against its dialect definition.
    /// </summary>
    public record DialectValidationResult
    {
        /// <summary>
        /// True when the message type identifier (MTI) is defined in the dialect.
        /// </summary>
        public bool IsMtiKnown { get; init; }

        /// <summary>
        /// Mandatory fields (by field number) that the dialect requires but that are
        /// absent from the message bitmap.
        /// </summary>
        public IReadOnlyList<int> MissingMandatoryFields { get; init; } = Array.Empty<int>();

        /// <summary>
        /// Fields (by field number) present in the message bitmap but that do not
        /// participate in this message type according to the dialect.
        /// </summary>
        public IReadOnlyList<int> DisallowedFields { get; init; } = Array.Empty<int>();

        /// <summary>
        /// Human-readable summary of the validation outcome.
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// True when the MTI is known, no mandatory fields are missing, and no disallowed
        /// fields are present.
        /// </summary>
        public bool IsValid => IsMtiKnown
            && (MissingMandatoryFields?.Count ?? 0) == 0
            && (DisallowedFields?.Count ?? 0) == 0;

        /// <summary>
        /// Creates a result for a known, well-formed message.
        /// </summary>
        public static DialectValidationResult Valid()
        {
            return new DialectValidationResult
            {
                IsMtiKnown = true,
                Message = "Message is valid."
            };
        }

        /// <summary>
        /// Creates a result for an MTI that is not defined in the dialect.
        /// </summary>
        public static DialectValidationResult MtiUnknown(string mti)
        {
            return new DialectValidationResult
            {
                IsMtiKnown = false,
                Message = "Message Type [" + mti + "] is not defined in the dialect."
            };
        }
    }
}

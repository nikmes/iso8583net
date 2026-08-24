using System;

namespace ISO8583Net.Packager
{
    /// <summary>
    /// Controls how outbound ISO 8583 dialect validation behaves. The mode is read from
    /// <see cref="ISOMessageFieldsPackager"/> at pack time and can be changed at runtime,
    /// so operators can tighten or loosen enforcement without redeploying.
    /// </summary>
    public enum DialectValidationMode
    {
        /// <summary>
        /// No validation. The packager stays fully permissive (pre-existing behavior).
        /// This is the default for the core library.
        /// </summary>
        Off = 0,

        /// <summary>
        /// Validate every outbound message and log a warning on failure, but never throw.
        /// Useful in production to surface dialect violations without breaking any flow.
        /// </summary>
        Warn = 1,

        /// <summary>
        /// Validate every outbound message and throw <see cref="DialectValidationException"/>
        /// on the first violation, so no invalid bytes are produced.
        /// </summary>
        On = 2
    }

    /// <summary>
    /// Parses <see cref="DialectValidationMode"/> from configuration/API strings.
    /// Accepts "Off", "Warn", "On" (case-insensitive) and the numeric values 0/1/2.
    /// Anything unrecognized — including null/empty — falls back to <see cref="DialectValidationMode.Off"/>.
    /// </summary>
    public static class DialectValidationModeParser
    {
        /// <summary>
        /// Parses a string into a <see cref="DialectValidationMode"/>.
        /// </summary>
        public static DialectValidationMode Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DialectValidationMode.Off;

            if (Enum.TryParse<DialectValidationMode>(value, ignoreCase: true, out var mode))
                return mode;

            return DialectValidationMode.Off;
        }
    }
}

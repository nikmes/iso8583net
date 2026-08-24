using System;
using System.Collections.Generic;
using System.Linq;
using ISO8583Net.Message;
using ISO8583Net.Packager;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ISO8583Tests
{
    /// <summary>
    /// Verifies the tri-state outbound dialect validation mode (Off/Warn/On).
    /// Off leaves the packer permissive (pre-existing behavior), Warn logs a warning on
    /// violation without throwing, and On throws before invalid bytes are produced.
    /// </summary>
    public class DialectValidationModeTests
    {
        private static ISOMessagePackager CreateD8Packager(ILogger logger)
            => new ISOMessagePackager(logger, BuiltInDialect.D8);

        private static ISOMessage Build1804MissingF28(ISOMessagePackager packager)
        {
            var message = new ISOMessage(NullLogger<DialectValidationModeTests>.Instance, packager);
            message.Set(0, "1804");
            message.Set(7, "0817111922"); // MMDDhhmmss (10 digits)
            message.Set(11, "000001");    // STAN (6 digits)
            message.Set(24, "831");       // Function Code = Echo test
            // F28 (mandatory for 1804) intentionally omitted.
            return message;
        }

        [Fact]
        public void Default_Off_IsPermissive()
        {
            var packager = CreateD8Packager(NullLogger<DialectValidationModeTests>.Instance);

            // Unknown MTI is allowed when validation is off.
            var message = new ISOMessage(NullLogger<DialectValidationModeTests>.Instance, packager);
            message.Set(0, "1800"); // not defined in D8 dialect — should NOT throw
            Assert.Equal("1800", message.GetFieldValue(0));

            // Missing mandatory field is allowed when validation is off.
            var incomplete = Build1804MissingF28(packager);
            var packed = incomplete.Pack();
            Assert.NotNull(packed);
            Assert.NotEmpty(packed);
        }

        [Fact]
        public void Warn_DoesNotThrow_ButLogsWarning()
        {
            var logger = new CapturingLogger();
            var packager = CreateD8Packager(logger);
            packager.GetISOMessageFieldsPackager().SetFieldParticipationValidationMode(DialectValidationMode.Warn);

            var message = Build1804MissingF28(packager);

            // Pack must succeed (no throw) despite the missing mandatory F28.
            var packed = message.Pack();
            Assert.NotNull(packed);
            Assert.NotEmpty(packed);

            // A warning must have been logged describing the violation.
            Assert.Contains(logger.Entries, e =>
                e.Level == LogLevel.Warning &&
                e.Message.Contains("Dialect validation warning", StringComparison.Ordinal));
        }

        [Fact]
        public void On_ThrowsOnUnknownMti()
        {
            var packager = CreateD8Packager(NullLogger<DialectValidationModeTests>.Instance);
            packager.GetISOMessageFieldsPackager().SetFieldParticipationValidationMode(DialectValidationMode.On);

            var message = new ISOMessage(NullLogger<DialectValidationModeTests>.Instance, packager);

            // "1800" is not defined in the D8 dialect (only 1804/1814 are).
            Assert.Throws<DialectValidationException>(() => message.Set(0, "1800"));
        }

        [Fact]
        public void On_ThrowsOnMissingMandatory()
        {
            var packager = CreateD8Packager(NullLogger<DialectValidationModeTests>.Instance);
            packager.GetISOMessageFieldsPackager().SetFieldParticipationValidationMode(DialectValidationMode.On);

            var message = Build1804MissingF28(packager);

            Assert.Throws<DialectValidationException>(() => message.Pack());
        }

        [Fact]
        public void Parser_ParsesAllModes()
        {
            Assert.Equal(DialectValidationMode.Off, DialectValidationModeParser.Parse(null));
            Assert.Equal(DialectValidationMode.Off, DialectValidationModeParser.Parse(""));
            Assert.Equal(DialectValidationMode.Off, DialectValidationModeParser.Parse("   "));
            Assert.Equal(DialectValidationMode.Off, DialectValidationModeParser.Parse("bogus"));

            Assert.Equal(DialectValidationMode.Off, DialectValidationModeParser.Parse("off"));
            Assert.Equal(DialectValidationMode.Off, DialectValidationModeParser.Parse("OFF"));
            Assert.Equal(DialectValidationMode.Warn, DialectValidationModeParser.Parse("warn"));
            Assert.Equal(DialectValidationMode.Warn, DialectValidationModeParser.Parse("WARN"));
            Assert.Equal(DialectValidationMode.On, DialectValidationModeParser.Parse("on"));
            Assert.Equal(DialectValidationMode.On, DialectValidationModeParser.Parse("ON"));

            // Numeric values accepted via Enum.TryParse.
            Assert.Equal(DialectValidationMode.Off, DialectValidationModeParser.Parse("0"));
            Assert.Equal(DialectValidationMode.Warn, DialectValidationModeParser.Parse("1"));
            Assert.Equal(DialectValidationMode.On, DialectValidationModeParser.Parse("2"));
        }

        /// <summary>
        /// Minimal ILogger that records every log entry so tests can assert on warnings.
        /// </summary>
        private sealed class CapturingLogger : ILogger
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Entries.Add((logLevel, formatter(state, exception)));
            }
        }
    }
}

using ISO8583Net.Message;
using ISO8583Net.Packager;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ISO8583Tests
{
    /// <summary>
    /// Verifies that outbound dialect enforcement (unknown MTI rejection, mandatory-field and
    /// disallowed-field checks at pack time) is gated behind the opt-in flag and behaves
    /// correctly when enabled. Default behavior (flag off) leaves the packer permissive.
    /// </summary>
    public class DialectEnforcementTests
    {
        private static ISOMessagePackager CreateD8PackagerWithValidation()
        {
            var packager = new ISOMessagePackager(
                NullLogger<DialectEnforcementTests>.Instance, BuiltInDialect.D8);
            packager.GetISOMessageFieldsPackager().EnableFieldParticipationValidations(true);
            return packager;
        }

        [Fact]
        public void Set_UnknownMti_Throws_WhenValidationEnabled()
        {
            var packager = CreateD8PackagerWithValidation();
            var message = new ISOMessage(NullLogger<DialectEnforcementTests>.Instance, packager);

            // "1800" is not defined in the D8 dialect (only 1804/1814 are).
            Assert.Throws<DialectValidationException>(() => message.Set(0, "1800"));
        }

        [Fact]
        public void Pack_Valid1804_Succeeds_WhenValidationEnabled()
        {
            var packager = CreateD8PackagerWithValidation();
            var message = new ISOMessage(NullLogger<DialectEnforcementTests>.Instance, packager);

            message.Set(0, "1804");
            message.Set(7, "0817111922"); // MMDDhhmmss (10 digits)
            message.Set(11, "000001");    // STAN (6 digits)
            message.Set(24, "831");       // Function Code = Echo test
            message.Set(28, "240824");    // YYMMDD (6 digits)

            var packed = message.Pack();

            Assert.NotNull(packed);
            Assert.NotEmpty(packed);
        }

        [Fact]
        public void Pack_1804_MissingF28_Throws()
        {
            var packager = CreateD8PackagerWithValidation();
            var message = new ISOMessage(NullLogger<DialectEnforcementTests>.Instance, packager);

            message.Set(0, "1804");
            message.Set(7, "0817111922");
            message.Set(11, "000001");
            message.Set(24, "831");
            // F28 (mandatory for 1804) intentionally omitted.

            Assert.Throws<DialectValidationException>(() => message.Pack());
        }

        [Fact]
        public void Pack_1804_DisallowedF2_Throws()
        {
            var packager = CreateD8PackagerWithValidation();
            var message = new ISOMessage(NullLogger<DialectEnforcementTests>.Instance, packager);

            message.Set(0, "1804");
            message.Set(7, "0817111922");
            message.Set(11, "000001");
            message.Set(24, "831");
            message.Set(28, "240824");
            message.Set(2, "4111111111111111"); // PAN does not participate in 1804.

            Assert.Throws<DialectValidationException>(() => message.Pack());
        }
    }
}

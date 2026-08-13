using ISO8583Net.Packager;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ISO8583Tests
{
    public class DialectLoaderTests
    {
        [Fact]
        public void EmbeddedD8Dialect_LoadsWith193Fields()
        {
            var packager = new ISOMessagePackager(
                NullLogger<DialectLoaderTests>.Instance, BuiltInDialect.D8);

            Assert.Equal("ISOHeaderD8Packager", packager.HeaderPackagerName);
            Assert.Equal(193, packager.GetTotalFields());
        }

        [Fact]
        public void EmbeddedVisaDialect_LoadsWithDefaultConstructor()
        {
            var packager = new ISOMessagePackager(
                NullLogger<DialectLoaderTests>.Instance);

            Assert.Equal("ISOHeaderVisaPackager", packager.HeaderPackagerName);
        }
    }
}

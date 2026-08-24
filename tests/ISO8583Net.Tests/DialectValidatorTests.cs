using ISO8583Net.Field;
using ISO8583Net.Message;
using ISO8583Net.Packager;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ISO8583Tests
{
    public class DialectValidatorTests
    {
        private static ISOMessageFieldsPackager CreateD8FieldsPackager()
        {
            var packager = new ISOMessagePackager(
                NullLogger<DialectValidatorTests>.Instance, BuiltInDialect.D8);
            return packager.GetISOMessageFieldsPackager();
        }

        private static ISOFieldBitmap CreateBitmap(params int[] fields)
        {
            var bitmap = new ISOFieldBitmap(NullLogger<DialectValidatorTests>.Instance);
            foreach (var field in fields)
            {
                bitmap.SetBit(field);
            }
            return bitmap;
        }

        [Fact]
        public void MessageTypesPackager_Contains_ReportsKnownAndUnknownMtis()
        {
            var msgTypes = CreateD8FieldsPackager().GetMessageTypesPackager();

            Assert.True(msgTypes.Contains("1804"));
            Assert.True(msgTypes.Contains("1100"));
            Assert.False(msgTypes.Contains("1800"));
            Assert.True(msgTypes.Contains("9800"));
        }

        [Fact]
        public void MessageTypesPackager_TryGet_ReturnsPackagerForKnownMti()
        {
            var msgTypes = CreateD8FieldsPackager().GetMessageTypesPackager();

            Assert.True(msgTypes.TryGet("1804", out var mt));
            Assert.NotNull(mt);
            Assert.Equal("1804", mt.messageTypeIdentifier);

            Assert.False(msgTypes.TryGet("9999", out _));
        }

        [Fact]
        public void Validate_KnownMti_AllMandatorySet_IsValid()
        {
            var fields = CreateD8FieldsPackager();
            var bitmap = CreateBitmap(7, 11, 24, 28);

            var result = DialectValidator.Validate(fields, "1804", bitmap);

            Assert.True(result.IsMtiKnown);
            Assert.True(result.IsValid);
            Assert.Empty(result.MissingMandatoryFields);
            Assert.Empty(result.DisallowedFields);
        }

        [Fact]
        public void Validate_UnknownMti_IsNotMtiKnown()
        {
            var fields = CreateD8FieldsPackager();

            var result = DialectValidator.Validate(fields, "1800", CreateBitmap());

            Assert.False(result.IsMtiKnown);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_KnownMti_MissingMandatoryFields_AreReported()
        {
            var fields = CreateD8FieldsPackager();

            var result = DialectValidator.Validate(fields, "1804", CreateBitmap());

            Assert.True(result.IsMtiKnown);
            Assert.False(result.IsValid);
            Assert.Equal(new[] { 7, 11, 24, 28 }, result.MissingMandatoryFields);
        }

        [Fact]
        public void Validate_KnownMti_DisallowedField_IsReported()
        {
            var fields = CreateD8FieldsPackager();
            // Field 2 (PAN) does not participate in MTI 1804.
            var bitmap = CreateBitmap(7, 11, 24, 28, 2);

            var result = DialectValidator.Validate(fields, "1804", bitmap);

            Assert.True(result.IsMtiKnown);
            Assert.False(result.IsValid);
            Assert.Empty(result.MissingMandatoryFields);
            Assert.Equal(new[] { 2 }, result.DisallowedFields);
        }

        [Fact]
        public void Validate_NullBitmap_TreatedAsEmpty_ReportsAllMandatoryMissing()
        {
            var fields = CreateD8FieldsPackager();

            var result = DialectValidator.Validate(fields, "1804", null);

            Assert.True(result.IsMtiKnown);
            Assert.False(result.IsValid);
            Assert.Equal(new[] { 7, 11, 24, 28 }, result.MissingMandatoryFields);
            Assert.Empty(result.DisallowedFields);
        }

        [Fact]
        public void Validate_ISOMessageOverload_ExtractsMtiAndBitmap()
        {
            var packager = new ISOMessagePackager(
                NullLogger<DialectValidatorTests>.Instance, BuiltInDialect.D8);
            var fields = packager.GetISOMessageFieldsPackager();
            var message = new ISOMessage(NullLogger<DialectValidatorTests>.Instance, packager);
            message.Set(0, "1804");
            var bitmap = (ISOFieldBitmap)message.GetField(1);
            bitmap.SetBit(7);
            bitmap.SetBit(11);
            bitmap.SetBit(24);
            bitmap.SetBit(28);

            var result = DialectValidator.Validate(fields, message);

            Assert.True(result.IsValid);
        }
    }
}

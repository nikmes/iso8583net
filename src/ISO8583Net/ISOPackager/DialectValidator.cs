using ISO8583Net.Field;
using ISO8583Net.Message;
using System;

namespace ISO8583Net.Packager
{
    /// <summary>
    /// Validates ISO 8583 messages against a dialect definition without mutating them.
    /// Returns a structured <see cref="DialectValidationResult"/> rather than throwing.
    /// </summary>
    public static class DialectValidator
    {
        /// <summary>
        /// Validates a message type identifier and its bitmap against a dialect.
        /// </summary>
        /// <param name="fieldsPackager">The message fields packager loaded from the dialect.</param>
        /// <param name="mti">The message type identifier (e.g. "1804").</param>
        /// <param name="bitmap">The bitmap from the message being validated.</param>
        public static DialectValidationResult Validate(
            ISOMessageFieldsPackager fieldsPackager,
            string mti,
            ISOFieldBitmap bitmap)
        {
            if (fieldsPackager == null)
                throw new ArgumentNullException(nameof(fieldsPackager));

            var msgTypes = fieldsPackager.GetMessageTypesPackager();
            if (!msgTypes.TryGet(mti, out var msgTypePackager))
                return DialectValidationResult.MtiUnknown(mti);

            return msgTypePackager.ValidateBitmap(bitmap);
        }

        /// <summary>
        /// Validates an <see cref="ISOMessage"/> (its MTI and bitmap) against a dialect.
        /// </summary>
        /// <param name="fieldsPackager">The message fields packager loaded from the dialect.</param>
        /// <param name="message">The message to validate.</param>
        public static DialectValidationResult Validate(
            ISOMessageFieldsPackager fieldsPackager,
            ISOMessage message)
        {
            if (fieldsPackager == null)
                throw new ArgumentNullException(nameof(fieldsPackager));
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            string mti = message.GetFieldValue(0);
            var bitmap = message.GetField(1) as ISOFieldBitmap;
            if (bitmap == null)
                return DialectValidationResult.MtiUnknown(mti);

            return Validate(fieldsPackager, mti, bitmap);
        }
    }
}

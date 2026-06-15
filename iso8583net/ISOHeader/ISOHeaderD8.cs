using ISO8583Net.Packager;
using Microsoft.Extensions.Logging;
using System;
using System.Text;

namespace ISO8583Net.Header
{
    /// <summary>
    /// D8 (ISO 8583:1993) ASCII text header — 22 bytes of printable ASCII.
    /// Commonly found in legacy ISO 8583 implementations as a protocol identifier.
    ///
    /// Example: "ISO8583-1993001000000"
    ///
    /// Byte layout:
    ///   Bytes  1-12   Standard identifier  ("ISO8583-1993")
    ///   Bytes 13-22   Format/version info  ("0010000000")
    /// </summary>
    public class ISOHeaderD8 : ISOHeader
    {
        /// <summary>Fixed header length in bytes.</summary>
        public const int HeaderLength = 21;

        private byte[] _headerData = new byte[HeaderLength];

        /// <summary>
        /// Gets or sets the raw header bytes.
        /// </summary>
        public byte[] HeaderData
        {
            get => _headerData;
            set
            {
                if (value.Length != HeaderLength)
                    throw new ArgumentException(
                        $"D8 header must be exactly {HeaderLength} bytes, got {value.Length}.");
                _headerData = value;
            }
        }

        /// <summary>
        /// Gets or sets the header as an ASCII string.
        /// </summary>
        public string HeaderText
        {
            get => Encoding.ASCII.GetString(_headerData);
            set
            {
                if (value.Length != HeaderLength)
                    throw new ArgumentException(
                        $"D8 header must be exactly {HeaderLength} characters, got {value.Length}.");
                Encoding.ASCII.GetBytes(value, 0, HeaderLength, _headerData, 0);
            }
        }

        /// <summary>
        /// Creates a D8 header with a default identifier.
        /// </summary>
        public ISOHeaderD8(ILogger logger) : base(logger)
        {
            HeaderText = "ISO8583-1993001000000";
        }

        /// <summary>
        /// Creates a D8 header with a custom ASCII text.
        /// </summary>
        public ISOHeaderD8(ILogger logger, string headerText) : base(logger)
        {
            HeaderText = headerText;
        }

        /// <summary>
        /// Creates a D8 header associated with a packager.
        /// </summary>
        public ISOHeaderD8(ILogger logger, ISOHeaderPackager packager) : base(logger)
        {
            HeaderText = "ISO8583-1993001000000";
        }

        /// <inheritdoc/>
        public override int Length() => HeaderLength;

        /// <inheritdoc/>
        public override void Pack(byte[] packedBytes, ref int index)
        {
            Array.Copy(_headerData, 0, packedBytes, index, HeaderLength);
            index += HeaderLength;
        }

        /// <inheritdoc/>
        public override void UnPack(byte[] packedBytes, ref int index)
        {
            Array.Copy(packedBytes, index, _headerData, 0, HeaderLength);
            index += HeaderLength;
        }

        /// <inheritdoc/>
        public override void SetValue(byte[] bytes)
        {
            HeaderData = bytes;
        }

        /// <inheritdoc/>
        public override void SetMessageLength(int length)
        {
            // D8 header doesn't carry message length — no-op
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"ISOHeaderD8: \"{HeaderText}\"";
        }
    }
}

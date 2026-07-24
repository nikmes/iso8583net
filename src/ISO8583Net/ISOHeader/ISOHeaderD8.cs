using ISO8583Net.Packager;
using Microsoft.Extensions.Logging;
using System;
using System.Text;

namespace ISO8583Net.Header
{
    /// <summary>
    /// D8 (ISO 8583:1993) ASCII text header — 21 bytes of printable ASCII.
    ///
    /// Byte layout:
    ///   Pos   1-12   Protocol Version Identifier  ("ISO8583-1993")
    ///   Pos  13-14   Message Source               (2 ASCII digits)
    ///   Pos  15-16   Version Number               (2 ASCII digits)
    ///   Pos  17-19   Field in Error               (3 ASCII digits, "000" = no error)
    ///   Pos  20-21   Not Used                     (2 ASCII, reserved)
    /// </summary>
    public class ISOHeaderD8 : ISOHeader
    {
        /// <summary>Fixed header length in bytes.</summary>
        public const int HeaderLength = 21;

        private const string ProtocolId = "ISO8583-1993";

        private byte[] _headerData = new byte[HeaderLength];

        /// <summary>Protocol Version Identifier (positions 1-12).</summary>
        public string ProtocolVersionIdentifier
        {
            get => Encoding.ASCII.GetString(_headerData, 0, 12);
            set
            {
                var padded = value.PadRight(12)[..12];
                Encoding.ASCII.GetBytes(padded, 0, 12, _headerData, 0);
            }
        }

        /// <summary>Message Source (positions 13-14). 2 ASCII digits.</summary>
        public string MessageSource
        {
            get => Encoding.ASCII.GetString(_headerData, 12, 2);
            set
            {
                var padded = value.PadLeft(2, '0')[..2];
                Encoding.ASCII.GetBytes(padded, 0, 2, _headerData, 12);
            }
        }

        /// <summary>Version Number (positions 15-16). 2 ASCII digits.</summary>
        public string VersionNumber
        {
            get => Encoding.ASCII.GetString(_headerData, 14, 2);
            set
            {
                var padded = value.PadLeft(2, '0')[..2];
                Encoding.ASCII.GetBytes(padded, 0, 2, _headerData, 14);
            }
        }

        /// <summary>Field in Error (positions 17-19). 3 ASCII digits. "000" = no error.</summary>
        public string FieldInError
        {
            get => Encoding.ASCII.GetString(_headerData, 16, 3);
            set
            {
                var padded = value.PadLeft(3, '0')[..3];
                Encoding.ASCII.GetBytes(padded, 0, 3, _headerData, 16);
            }
        }

        /// <summary>Not Used / Reserved (positions 20-21). 2 ASCII.</summary>
        public string NotUsed
        {
            get => Encoding.ASCII.GetString(_headerData, 19, 2);
            set
            {
                var padded = value.PadRight(2)[..2];
                Encoding.ASCII.GetBytes(padded, 0, 2, _headerData, 19);
            }
        }

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
        /// Gets or sets the full header as an ASCII string.
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
        /// Creates a D8 header with default values.
        /// </summary>
        public ISOHeaderD8(ILogger logger) : base(logger)
        {
            SetDefaults();
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
            SetDefaults();
        }

        private void SetDefaults()
        {
            ProtocolVersionIdentifier = ProtocolId;
            MessageSource = "00";
            VersionNumber = "10";
            FieldInError = "000";
            NotUsed = "00";
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
        public override string ToString()
        {
            return $"  Proto : {ProtocolVersionIdentifier}\n" +
                   $"  Src   : {MessageSource}\n" +
                   $"  Ver   : {VersionNumber}\n" +
                   $"  Error : {FieldInError}\n" +
                   $"  Rsvd  : {NotUsed}";
        }

        /// <inheritdoc/>
        public override void SetMessageLength(int length)
        {
            // D8 header doesn't carry message length — no-op
        }
    }
}

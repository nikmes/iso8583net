using ISO8583Net.Header;
using ISO8583Net.Utilities;
using Microsoft.Extensions.Logging;
using System;

namespace ISO8583Net.Packager
{
    /// <summary>
    /// Packager for <see cref="ISOHeaderD8"/> — 22 bytes of ASCII text.
    /// Packs/unpacks the raw ASCII bytes verbatim.
    /// </summary>
    public class ISOHeaderD8Packager : ISOHeaderPackager
    {
        /// <summary>
        /// Creates a D8 header packager.
        /// </summary>
        public ISOHeaderD8Packager(ILogger logger) : base(logger) { }

        /// <inheritdoc/>
        public override void Pack(ISOHeader isoHeader, byte[] packedBytes, ref int index)
        {
            var d8 = (ISOHeaderD8)isoHeader;
            Array.Copy(d8.HeaderData, 0, packedBytes, index, ISOHeaderD8.HeaderLength);
            index += ISOHeaderD8.HeaderLength;
        }

        /// <inheritdoc/>
        public override void UnPack(ISOHeader isoHeader, byte[] packedBytes, ref int index)
        {
            var d8 = (ISOHeaderD8)isoHeader;
            Array.Copy(packedBytes, index, d8.HeaderData, 0, ISOHeaderD8.HeaderLength);
            index += ISOHeaderD8.HeaderLength;
        }

        /// <inheritdoc/>
        public override void Set(byte[] bytes)
        {
            // Operation not applicable at packager level — handled by ISOHeaderD8.SetValue
        }

        /// <inheritdoc/>
        public override void Trace()
        {
            Logger.LogInformation("ISOHeaderD8Packager: {Len} bytes ASCII text",
                ISOHeaderD8.HeaderLength);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"ISOHeaderD8Packager ({ISOHeaderD8.HeaderLength} bytes ASCII)";
        }
    }
}

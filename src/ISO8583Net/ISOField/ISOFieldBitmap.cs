using ISO8583Net.Packager;
using ISO8583Net.Types;
using ISO8583Net.Utilities;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Linq;
using System.Text;

namespace ISO8583Net.Field
{
    /// <summary>
    /// 
    /// </summary>
    public class ISOFieldBitmap : ISOField
    {
        /// <summary>
        /// Minimum number of bytes required to read a primary ISO 8583 bitmap.
        /// A message without at least this many bytes after the MTI has no bitmap.
        /// </summary>
        public const int MinimumLengthBytes = 8;

        private readonly ISOFieldPackager m_packager;

        private readonly byte[] m_bitmap;

        private int m_length;
        /// <summary>
        /// 
        /// </summary>
        public override string value
        {
            get
            {
                return ISOUtils.Bytes2Hex(m_bitmap, this.GetLengthInBytes()); //.Substring(0, this.GetLengthInBytes() * 2);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public bool secondaryBitmapIsSet { get; set; }  = false;
        /// <summary>
        /// 
        /// </summary>
        public bool thirdBitmapIsSet { get; set; }  = false;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        public ISOFieldBitmap(ILogger logger) : base(logger, null, 0)
        {
            m_bitmap = new byte[25];

            m_length = 25;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="packager"></param>
        /// <param name="number"></param>
        public ISOFieldBitmap(ILogger logger, ISOPackager packager, int number) : base(logger, packager, number)
        {
            //!!! Problem here, what if the content coding is not BIN ? !!! //

            m_packager = (ISOFieldPackager)packager;

            m_length = m_packager.GetFieldLength() / 2; // Divide by 2 since we have unit of measurment the hexadecimal digits and we need 2 for each byte

            m_bitmap = new byte[25];
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        public void Set(byte[] packedBytes, ref int index)
        {
            secondaryBitmapIsSet = false;
            thirdBitmapIsSet = false;

            if (m_packager.m_isoFieldDefinition.contentCoding != ISOFieldCoding.BIN)
            {
                return;
            }

            int remaining = packedBytes.Length - index;
            if (remaining <= 0)
            {
                return;
            }

            if (m_length < 9)
            {
                // m_length is already in bytes (hex digits / 2 from constructor)

                int smallBytesToRead = Math.Min(m_length, remaining);

                Array.Copy(packedBytes, index, m_bitmap, 0, smallBytesToRead);

                index += smallBytesToRead;

                return;
            }

            // Check bit 1 to determine whether a secondary bitmap is declared, and bit 65
            // (first bit of the secondary bitmap) for a tertiary bitmap. Guard each read so a
            // truncated frame — e.g. a 9xxx error response that echoes only the primary bitmap —
            // degrades gracefully instead of throwing IndexOutOfRangeException.

            bool bitmap2nd = (packedBytes[index] & 0x80) != 0;

            bool bitmap3rd = false;

            int bytesToRead;
            if (bitmap2nd && remaining >= 16)
            {
                bitmap3rd = (packedBytes[index + 8] & 0x80) != 0;

                if (bitmap3rd)
                {
                    bytesToRead = Math.Min(24, remaining);
                    thirdBitmapIsSet = true;
                }
                else
                {
                    bytesToRead = Math.Min(16, remaining);
                    secondaryBitmapIsSet = true;
                }
            }
            else if (bitmap2nd)
            {
                // Bit 1 claims a secondary bitmap but fewer than 16 bytes remain. Read only
                // the primary bitmap and leave the secondary flag unset.
                bytesToRead = Math.Min(8, remaining);
            }
            else
            {
                bytesToRead = Math.Min(8, remaining);
            }

            Array.Copy(packedBytes, index, m_bitmap, 0, bytesToRead);

            index += bytesToRead;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int GetLengthInBytes()
        {
            if (m_length < 9)
            {
                return m_length;
            }
            else if (!this.secondaryBitmapIsSet)
            {
                return 8;

            }
            else if (this.thirdBitmapIsSet)
            {
                return 24;
            }
            else
            {
                return 16;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int GetLengthInBits()
        {
            if (m_length < 9)
            {
                return m_length * 8;
            }
            else if (!this.secondaryBitmapIsSet)
            {
                return 64;
            }
            else if (this.thirdBitmapIsSet)
            {
                return 192;
            }
            else
            {
                return 128;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        public void SetBit(int index)
        {
            if (index == 1)
            {
                index = 0;
            }
            else
            {
                index -= 1;
            }

            int byteIndex = index / 8;
            int bitIndex = index % 8;

            if (!secondaryBitmapIsSet)
            {
                if (byteIndex >= 8)
                {
                    this.SetBit(1);
                    secondaryBitmapIsSet = true;
                }
            }

            if (!thirdBitmapIsSet)
            {
                if (byteIndex >= 16)
                {
                    this.SetBit(65);
                    thirdBitmapIsSet = true;
                }
            }

            m_bitmap[byteIndex] = (byte)(true ? (m_bitmap[byteIndex] | ((byte)(128 >> bitIndex))) : (m_bitmap[byteIndex] & ~((byte)(128 >> bitIndex))));
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        public void ToggleBit(int index)
        {
            if (index == 1)
            {
                index = 0;
            }
            else
            {
                index -= 1;
            }

            byte mask = (byte)(128 >> (index % 8));

            m_bitmap[(index / 8)] ^= mask;
        }
        /// <summary>
        /// Check if bit is set
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public bool BitIsSet(int index)
        {
            if (index <= 1)
            {
                index = 0;
            }
            else
            {
                index -= 1;
            }

            byte mask = (byte)(128 >> (index % 8));

            return (m_bitmap[(index / 8)] & mask) != 0;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string ToHexString()
        {
            return ISOUtils.Bytes2Hex(m_bitmap, this.GetLengthInBytes());//.Substring(0, this.GetLengthInBytes() * 2);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string ToDashedHexString()
        {
            return BitConverter.ToString(m_bitmap, 0, this.GetLengthInBytes());
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string ToBinaryString()
        {
            return string.Join(string.Empty, ToHexString().Select(c => Convert.ToString(Convert.ToInt32(c.ToString(), 16), 2).PadLeft(4, '0')));
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="padString"></param>
        /// <returns></returns>
        public string ToHumanReadable(string padString)
        {
            StringBuilder humanReadableSring = new StringBuilder(1024);

            humanReadableSring.Append(padString);
            int length = GetLengthInBits();
            for (int i = 1; i <= length; i++)
            {
                string pos = (i).ToString("000");

                if (BitIsSet(i))
                {
                    humanReadableSring.Append("[" + pos.PadRight(3, '0') + "][X] ");
                }
                else
                {
                    humanReadableSring.Append("[" + pos.PadRight(3, '0') + "][ ] ");
                }

                if ((i) % 8 == 0 && i != length)
                {
                    humanReadableSring.Append("\n" + padString);
                }
            }

            return humanReadableSring.ToString();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string retStr = "F[" + m_number.ToString().PadLeft(3, '0') + "]".PadRight(2, ' ') + "[" + ToHexString() + "]\n" + ToHumanReadable("       ") + '\n';
 
            return retStr;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public byte[] GetByteArray()
        {
            if (this.BitIsSet(65))
            {
                return m_bitmap.AsSpan(0, 24).ToArray();
            }
            else if (this.BitIsSet(1))
            {
                return m_bitmap.AsSpan(0, 16).ToArray();
            }
            else if (!this.BitIsSet(1))
            {
                return m_bitmap.AsSpan(0, 8).ToArray();
            }
            else
            {
                return m_bitmap.AsSpan(0, m_length).ToArray();
            }          
        }

        /// <summary>
        /// Fills caller-provided span with the numbers of all set fields. Field 0 is always first.
        /// Returns the count of fields written.
        /// </summary>
        public int GetSetFields(Span<int> destination)
        {
            int currentIndex = 0;
            destination[currentIndex++] = 0;

            int length = GetLengthInBytes();
            for (int i = 0; i < length; i++)
            {
                int multiplier = i * 8;
                byte b = m_bitmap[i];
                if ((128 & b) > 0) destination[currentIndex++] = 1 + multiplier;
                if ((64  & b) > 0) destination[currentIndex++] = 2 + multiplier;
                if ((32  & b) > 0) destination[currentIndex++] = 3 + multiplier;
                if ((16  & b) > 0) destination[currentIndex++] = 4 + multiplier;
                if ((8   & b) > 0) destination[currentIndex++] = 5 + multiplier;
                if ((4   & b) > 0) destination[currentIndex++] = 6 + multiplier;
                if ((2   & b) > 0) destination[currentIndex++] = 7 + multiplier;
                if ((1   & b) > 0) destination[currentIndex++] = 8 + multiplier;
            }
            return currentIndex;
        }

        public int[] GetSetFields()
        {
            int length = GetLengthInBytes();
            var result = new int[(length * 8) + 1];
            int count = GetSetFields(result);
            return result.AsSpan(0, count).ToArray();
        }
        /// <summary>
        /// 
        /// </summary>
        public override void Trace()
        {
            Logger.LogInformation("F[" + m_number.ToString().PadLeft(3, '0') + "]".PadRight(2, ' ') + "[" + ToHexString() + "]\n" + ToHumanReadable("               "));
        }
    }
}

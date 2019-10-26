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
            bool bitmap3rd = false;

            bool bitmap2nd = false;

            if ((m_packager.m_isoFieldDefinition.contentCoding) == ISOFieldCoding.BIN && m_length<9)
            {
                // length is bytes is number of hexadecimal digits divided by 2

                Array.Copy(packedBytes, index, m_bitmap, 0, m_length/2);

                index += 4;
            }
            else if (m_packager.m_isoFieldDefinition.contentCoding == ISOFieldCoding.BIN)
            {
                // check first bit and bit 64 to determine how many bytes to read

                byte mask = (byte)(128 >> (0 % 8));

                bitmap2nd = (packedBytes[index] & mask) != 0;

                if (bitmap2nd)
                {
                    // there is a 2nd bitmap so check if there is third as well

                    mask = (byte)(128 >> (64 % 8));

                    bitmap3rd = (packedBytes[index + 8] & mask) != 0;

                    secondaryBitmapIsSet = true;
                }


                if (bitmap3rd)
                {
                    // copy 8x3=24 bytes to initialize m_bitmap

                    Array.Copy(packedBytes, index, m_bitmap, 0, 24);

                    index += 24;

                    thirdBitmapIsSet = true;

                }
                else if (bitmap2nd)
                {
                    // copy 8x2=16 bytes to initialize m_bitmap

                    Array.Copy(packedBytes, index, m_bitmap, 0, 16);

                    index += 16;

                }
                else
                {
                    // copy 8 bytes to initialize m_bitmap

                    Array.Copy(packedBytes, index, m_bitmap, 0, 8);

                    index += 8;

                }
            }
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

        public int[] GetSetFields()
        {
            //allocate bitmap length * 8 plus one for the zero field;
            int[] result;
            int currentIndex = 0;
            try
            {
                int length = GetLengthInBytes();
                result = ArrayPool<int>.Shared.Rent((length * 8) + 1);

                result[currentIndex] = 0;
                currentIndex++;
                for (int i = 0; i < length; i++)
                {
                    int multiplier = i * 8;
                    if ((128 & m_bitmap[i]) > 0)
                    {
                        result[currentIndex] = 1 + multiplier;
                        currentIndex++;
                    }
                    if ((64 & m_bitmap[i]) > 0)
                    {
                        result[currentIndex] = 2 + multiplier;
                        currentIndex++;
                    }
                    if ((32 & m_bitmap[i]) > 0)
                    {
                        result[currentIndex] = 3 + multiplier;
                        currentIndex++;
                    }
                    if ((16 & m_bitmap[i]) > 0)
                    {
                        result[currentIndex] = 4 + multiplier;
                        currentIndex++;
                    }
                    if ((8 & m_bitmap[i]) > 0)
                    {
                        result[currentIndex] = 5 + multiplier;
                        currentIndex++;
                    }
                    if ((4 & m_bitmap[i]) > 0)
                    {
                        result[currentIndex] = 6 + multiplier;
                        currentIndex++;
                    }
                    if ((2 & m_bitmap[i]) > 0)
                    {
                        result[currentIndex] = 7 + multiplier;
                        currentIndex++;
                    }
                    if ((1 & m_bitmap[i]) > 0)
                    {
                        result[currentIndex] = 8 + multiplier;
                        currentIndex++;
                    }

                }
            }
            finally
            {


            }

            if (result != null)
            {
                var finalResult = result.AsSpan<int>(0, currentIndex).ToArray();
                ArrayPool<int>.Shared.Return(result);
                return finalResult;
            }
            else
            {
                return null;
            }

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

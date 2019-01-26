using ISO8583Net.Packager;
using ISO8583Net.Types;
using ISO8583Net.Utilities;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text;

namespace ISO8583Net.Field
{
    public class ISOFieldBitmap : ISOField
    {
        private ISOFieldPackager m_packager;

        private byte[] m_bitmap;

        private int m_length;

        public bool secondaryBitmapIsSet { get; set; }  = false;

        public bool thirdBitmapIsSet { get; set; }  = false;

        public string m_value
        {
            get
            {
                return ISOUtils.bytes2hex(m_bitmap).Substring(0, this.GetLengthInBytes() * 2);
            }
            set
            {

            }

        }

        public ISOFieldBitmap(ILogger logger) : base(logger, null, 0)
        {
            m_bitmap = new byte[25];

            m_length = 25;
        }

        public ISOFieldBitmap(ILogger logger, ISOPackager packager, int number) : base(logger, packager, number)
        {
            //!!! Problem here, what if the content coding is not BIN ? !!! //

            m_packager = (ISOFieldPackager)packager;

            m_length = m_packager.GetFieldLength() / 2; // Divide by 2 since we have unit of measurment the hexadecimal digits and we need 2 for each byte

            m_bitmap = new byte[25];
        }

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

        public bool BitIsSet(int index)
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

            return (m_bitmap[(index / 8)] & mask) != 0;
        }

        public string ToHexString()
        {
            return ISOUtils.bytes2hex(m_bitmap).Substring(0, this.GetLengthInBytes() * 2);
        }

        public string ToDashedHexString()
        {
            return BitConverter.ToString(m_bitmap, 0, this.GetLengthInBytes());
        }

        public string ToBinaryString()
        {
            return String.Join(String.Empty, ToHexString().Select(c => Convert.ToString(Convert.ToInt32(c.ToString(), 16), 2).PadLeft(4, '0')));
        }

        public string ToHumanReadable(String padString)
        {
            StringBuilder humanReadableSring = new StringBuilder(1024);

            humanReadableSring.Append(padString);

            for (int i = 1; i <= this.GetLengthInBits(); i++)
            {
                string pos = (i).ToString("000");

                if (this.BitIsSet(i))
                {
                    humanReadableSring.Append("[" + pos.PadRight(3, '0') + "][X] ");
                }
                else
                {
                    humanReadableSring.Append("[" + pos.PadRight(3, '0') + "][ ] ");
                }

                if ((i) % 8 == 0 && i != this.GetLengthInBits())
                {
                    humanReadableSring.Append("\n" + padString);
                }
            }

            return humanReadableSring.ToString();
        }

        public override string ToString()
        {
            string retStr = "Field [" + m_number.ToString().PadLeft(3, '0') + "]".PadRight(5, ' ') + "[" + ToHexString() + "]\n" + ToHumanReadable("               ") + '\n';
 
            return retStr;
        }

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

        public override String GetValue()
        {
             return ISOUtils.bytes2hex(m_bitmap).Substring(0,this.GetLengthInBytes()*2);
        }

        public override void Trace()
        {
            if (Logger.IsEnabled(LogLevel.Information))
            {
                Logger.LogInformation("Field [" + m_number.ToString().PadLeft(3, '0') + "]".PadRight(5, ' ') + "[" + ToHexString() + "]\n" + ToHumanReadable("               "));
            }
        }

    }

}

using ISO8583Net.Types;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;


namespace ISO8583Net.Utilities
{
    /// <summary>
    /// Bitmap Extensions to provide functions such as bit enumerators
    /// </summary>
    public static class BitMapExtensions 
    {
        /// <summary>
        /// Get an boolean enumeration for each field of the bitmap         
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        public static IEnumerable<bool> GetBitEnumerator(this byte[] bitmap)
        {
            //return true for field 0
            yield return true;
            for (int i = 0; i < bitmap.Length; i++)
            {
                yield return (128 & bitmap[i]) > 0;
                yield return (64 & bitmap[i]) > 0;
                yield return (32 & bitmap[i]) > 0;
                yield return (16 & bitmap[i]) > 0;
                yield return (8 & bitmap[i]) > 0;
                yield return (4 & bitmap[i]) > 0;
                yield return (2 & bitmap[i]) > 0;
                yield return (1 & bitmap[i]) > 0;
            }
        }

        /// <summary>
        /// Get all the field ids that are set    
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        public static IEnumerable<int> GetFieldIdEnumerator(this byte[] bitmap)
        {
            //return set for field 0
            yield return 0;
            for (int i = 0; i < bitmap.Length; i++)
            {
                int multiplier = i * 8;
                if ((128 & bitmap[i]) > 0)
                    yield return 1 + multiplier;
                if ((64 & bitmap[i]) > 0)
                    yield return 2 + multiplier;
                if ((32 & bitmap[i]) > 0)
                    yield return 3 + multiplier;
                if ((16 & bitmap[i]) > 0)
                    yield return 4 + multiplier;
                if ((8 & bitmap[i]) > 0)
                    yield return 5 + multiplier;
                if ((4 & bitmap[i]) > 0)
                    yield return 6 + multiplier;
                if ((2 & bitmap[i]) > 0)
                    yield return 7 + multiplier;
                if ((1 & bitmap[i]) > 0)
                    yield return 8 + multiplier;
            }
        }

        
        
    }
    /// <summary>
    /// The ISO8583Utilities static class implements methods for performing fast conversion between different data types
    /// </summary>
    /// <remarks>
    /// This class can perform the below convertsions:
    /// </remarks>
    public static class ISOUtils
    {
        /// <summary>Static array for fast lookup to convert from ebcdic to ascii</summary>
        private static readonly int[] _os_toascii = new int[256] 
        {
	    /*00*/ 0x00, 0x01, 0x02, 0x03, 0x85, 0x09, 0x86, 0x7f,
               0x87, 0x8d, 0x8e, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f, /*................*/
	    /*10*/ 0x10, 0x11, 0x12, 0x13, 0x8f, 0x0a, 0x08, 0x97,
               0x18, 0x19, 0x9c, 0x9d, 0x1c, 0x1d, 0x1e, 0x1f, /*................*/
	    /*20*/ 0x80, 0x81, 0x82, 0x83, 0x84, 0x92, 0x17, 0x1b,
               0x88, 0x89, 0x8a, 0x8b, 0x8c, 0x05, 0x06, 0x07, /*................*/
	    /*30*/ 0x90, 0x91, 0x16, 0x93, 0x94, 0x95, 0x96, 0x04,
               0x98, 0x99, 0x9a, 0x9b, 0x14, 0x15, 0x9e, 0x1a, /*................*/
	    /*40*/ 0x20, 0xa0, 0xe2, 0xe4, 0xe0, 0xe1, 0xe3, 0xe5,
               0xe7, 0xf1, 0x60, 0x2e, 0x3c, 0x28, 0x2b, 0x7c, /* .........`.<(+|*/
	    /*50*/ 0x26, 0xe9, 0xea, 0xeb, 0xe8, 0xed, 0xee, 0xef,
               0xec, 0xdf, 0x21, 0x24, 0x2a, 0x29, 0x3b, 0x9f, /*&.........!$*);.*/
	    /*60*/ 0x2d, 0x2f, 0xc2, 0xc4, 0xc0, 0xc1, 0xc3, 0xc5,
               0xc7, 0xd1, 0x5e, 0x2c, 0x25, 0x5f, 0x3e, 0x3f, /*-/........^,%_>?*/
	    /*70*/ 0xf8, 0xc9, 0xca, 0xcb, 0xc8, 0xcd, 0xce, 0xcf,
               0xcc, 0xa8, 0x3a, 0x23, 0x40, 0x27, 0x3d, 0x22, /*..........:#@'="*/
	    /*80*/ 0xd8, 0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67,
               0x68, 0x69, 0xab, 0xbb, 0xf0, 0xfd, 0xfe, 0xb1, /*.abcdefghi......*/
	    /*90*/ 0xb0, 0x6a, 0x6b, 0x6c, 0x6d, 0x6e, 0x6f, 0x70,
               0x71, 0x72, 0xaa, 0xba, 0xe6, 0xb8, 0xc6, 0xa4, /*.jklmnopqr......*/
	    /*a0*/ 0xb5, 0xaf, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78,
               0x79, 0x7a, 0xa1, 0xbf, 0xd0, 0xdd, 0xde, 0xae, /*..stuvwxyz......*/
	    /*b0*/ 0xa2, 0xa3, 0xa5, 0xb7, 0xa9, 0xa7, 0xb6, 0xbc,
               0xbd, 0xbe, 0xac, 0x5b, 0x5c, 0x5d, 0xb4, 0xd7, /*...........[\]..*/
	    /*c0*/ 0xf9, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47,
               0x48, 0x49, 0xad, 0xf4, 0xf6, 0xf2, 0xf3, 0xf5, /*.ABCDEFGHI......*/
	    /*d0*/ 0xa6, 0x4a, 0x4b, 0x4c, 0x4d, 0x4e, 0x4f, 0x50,
               0x51, 0x52, 0xb9, 0xfb, 0xfc, 0xdb, 0xfa, 0xff, /*.JKLMNOPQR......*/
	    /*e0*/ 0xd9, 0xf7, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
               0x59, 0x5a, 0xb2, 0xd4, 0xd6, 0xd2, 0xd3, 0xd5, /*..STUVWXYZ......*/
	    /*f0*/ 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37,
               0x38, 0x39, 0xb3, 0x7b, 0xdc, 0x7d, 0xda, 0x7e  /*0123456789.{.}.~*/};

        /// <summary>Static array for fast lookup to convert from ascii to ebcdic</summary>
        private static readonly int[] _os_toebcdic = new int[256] 
        {
	    /*00*/ 0x00, 0x01, 0x02, 0x03, 0x37, 0x2d, 0x2e, 0x2f,
               0x16, 0x05, 0x15, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f,  /*................*/
	    /*10*/ 0x10, 0x11, 0x12, 0x13, 0x3c, 0x3d, 0x32, 0x26,
               0x18, 0x19, 0x3f, 0x27, 0x1c, 0x1d, 0x1e, 0x1f,  /*................*/
	    /*20*/ 0x40, 0x5a, 0x7f, 0x7b, 0x5b, 0x6c, 0x50, 0x7d,
               0x4d, 0x5d, 0x5c, 0x4e, 0x6b, 0x60, 0x4b, 0x61,  /* !"#$%&'()*+,-./ */
	    /*30*/ 0xf0, 0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7,
               0xf8, 0xf9, 0x7a, 0x5e, 0x4c, 0x7e, 0x6e, 0x6f,  /*0123456789:;<=>?*/
	    /*40*/ 0x7c, 0xc1, 0xc2, 0xc3, 0xc4, 0xc5, 0xc6, 0xc7,
               0xc8, 0xc9, 0xd1, 0xd2, 0xd3, 0xd4, 0xd5, 0xd6,  /*@ABCDEFGHIJKLMNO*/
	    /*50*/ 0xd7, 0xd8, 0xd9, 0xe2, 0xe3, 0xe4, 0xe5, 0xe6,
               0xe7, 0xe8, 0xe9, 0xbb, 0xbc, 0xbd, 0x6a, 0x6d,  /*PQRSTUVWXYZ[\]^_*/
	    /*60*/ 0x4a, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87,
               0x88, 0x89, 0x91, 0x92, 0x93, 0x94, 0x95, 0x96,  /*`abcdefghijklmno*/
	    /*70*/ 0x97, 0x98, 0x99, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6,
               0xa7, 0xa8, 0xa9, 0xfb, 0x4f, 0xfd, 0xff, 0x07,  /*pqrstuvwxyz{|}~.*/
	    /*80*/ 0x20, 0x21, 0x22, 0x23, 0x24, 0x04, 0x06, 0x08,
               0x28, 0x29, 0x2a, 0x2b, 0x2c, 0x09, 0x0a, 0x14,  /*................*/
	    /*90*/ 0x30, 0x31, 0x25, 0x33, 0x34, 0x35, 0x36, 0x17,
               0x38, 0x39, 0x3a, 0x3b, 0x1a, 0x1b, 0x3e, 0x5f,  /*................*/
	    /*a0*/ 0x41, 0xaa, 0xb0, 0xb1, 0x9f, 0xb2, 0xd0, 0xb5,
               0x79, 0xb4, 0x9a, 0x8a, 0xba, 0xca, 0xaf, 0xa1,  /*................*/
	    /*b0*/ 0x90, 0x8f, 0xea, 0xfa, 0xbe, 0xa0, 0xb6, 0xb3,
               0x9d, 0xda, 0x9b, 0x8b, 0xb7, 0xb8, 0xb9, 0xab,  /*................*/
	    /*c0*/ 0x64, 0x65, 0x62, 0x66, 0x63, 0x67, 0x9e, 0x68,
               0x74, 0x71, 0x72, 0x73, 0x78, 0x75, 0x76, 0x77,  /*................*/
	    /*d0*/ 0xac, 0x69, 0xed, 0xee, 0xeb, 0xef, 0xec, 0xbf,
               0x80, 0xe0, 0xfe, 0xdd, 0xfc, 0xad, 0xae, 0x59,  /*................*/
	    /*e0*/ 0x44, 0x45, 0x42, 0x46, 0x43, 0x47, 0x9c, 0x48,
               0x54, 0x51, 0x52, 0x53, 0x58, 0x55, 0x56, 0x57,  /*................*/
	    /*f0*/ 0x8c, 0x49, 0xcd, 0xce, 0xcb, 0xcf, 0xcc, 0xe1,
               0x70, 0xc0, 0xde, 0xdb, 0xdc, 0x8d, 0x8e, 0xdf   /*................*/
        };

        /// <summary>Static array for fast lookup to convert from bytes to hex</summary>
        private static readonly uint[] _lookup32 = CreateLookup32();

        /// <summary>Static array for fast HexBibble lookup</summary>
        private static readonly byte[] _HexNibble = new byte[] {
                      0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7,
                      0x8, 0x9, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0,
                      0x0, 0xA, 0xB, 0xC, 0xD, 0xE, 0xF, 0x0,
                      0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0,
                      0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0,
                      0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0,
                      0x0, 0xA, 0xB, 0xC, 0xD, 0xE, 0xF
        };
               
        /// <summary>
        /// Extents array functionality by allowing to concatenate two arrays
        /// </summary>
        /// <param name="inData1">The first arrays to be concatanated with the next array</param>  
        /// <param name="inData2">The arrays to be concatanate to the previous array</param>  
        /// <returns>
        /// A new array, the resutl of the concatanation of the two arrays passed as parameters
        /// </returns>
        public static byte[] BufferConcat(byte[] inData1, byte[] inData2)
        {
            byte[] result = new byte[inData1.Length + inData2.Length];
            Array.Copy(inData1, 0, result, 0, inData1.Length);
            Array.Copy(inData2, 0, result, inData1.Length, inData2.Length);
            return result;
        }

        /// <summary>
        /// Initializes static array used for Byte to Hex convertions 
        /// </summary>
        /// <returns>
        /// The initialized unsigned int array
        /// </returns>
        private static uint[] CreateLookup32()
        {
            var result = new uint[256];

            for (int i = 0; i < 256; i++)
            {
                string s = i.ToString("X2");

                result[i] = ((uint)s[0]) + ((uint)s[1] << 16);
            }

            return result;
        }

        
        /// <summary>
        /// Hex to Byte array
        /// </summary>
        /// <param name="hexString"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] HexToByteArray(string hexString)
        {
            hexString = hexString.Replace(" ", "");

            int NumberChars = hexString.Length;

            byte[] bytes = new byte[NumberChars / 2];

            for (int i = 0; i < NumberChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
            }

            return bytes;
        }

        /// <summary>
        /// Integer to byte
        /// </summary>
        /// <param name="value"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        /// <param name="numHexDigits"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Int2Bytes(int value, byte[] packedBytes, ref int index, int numHexDigits)
        {
            // !! PROBLEM NEED TO CONVERT BASE ON LENGTH OF LENGTH !!! NOT ALWAYS 1 BYTE !!!!!!

            if (numHexDigits == 2)
            {
                packedBytes[index] = (byte)(value);
                index += 1;
            }
            else
            {
                packedBytes[index] = (byte)(value);
                packedBytes[index + 1] = (byte)(value >> 8);
                packedBytes[index + 2] = (byte)(value >> 16);
                packedBytes[index + 3] = (byte)(value >> 24);
                index = index + 4;
            }
        }
        /// <summary>
        /// Convert a byte array to int
        /// </summary>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        /// <param name="numHexDigits"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Bytes2Int(byte[] packedBytes, ref int index, int numHexDigits)
        {
            int pos = 8 * ((numHexDigits / 2) - 1);

            int len = numHexDigits / 2;

            int result = 0;

            for (int i = 0; i < len; i++)
            {
                result |= (int)(packedBytes[index + i] << pos);
                pos -= 8;
            }
            index += len;

            return result;
        }
        /// <summary>
        /// Integer to Bcd
        /// </summary>
        /// <param name="index"></param>
        /// <param name="packedBytes"></param>
        /// <param name="value"></param>
        /// <param name="numBytes"></param>
        public static void Int2Bcd(int value, byte[] packedBytes, ref int index, int numBytes)
        {

        }
        /// <summary>
        /// Integer to ASCII
        /// </summary>
        /// <param name="value"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        /// <param name="numBytes"></param>
        public static void Int2Ascii(int value, byte[] packedBytes, ref int index, int numBytes)
        {

        }
        /// <summary>
        /// Integer to Ebcdic
        /// </summary>
        /// <param name="numBytes"></param>
        /// <param name="index"></param>
        /// <param name="packedBytes"></param>
        /// <param name="value"></param>
        public static void Int2Ebcdic(int value, byte[] packedBytes, ref int index, int numBytes)
        {

        }
        /// <summary>
        /// Do not use
        /// </summary>
        /// <param name="value"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        /// <param name="padding"></param>
        public static void Ascii2BcdOld(string value, byte[] packedBytes, ref int index, ISOFieldPadding padding)
        {
            int valueLength = value.Length;

            if (valueLength % 2 == 0)
            {
                // no padding needed just convert to bcd

                for (int i = 0; i < valueLength; i += 2)
                {
                    packedBytes[index] = (byte)((value[i] - 0x30) * 0x10 + value[i + 1] - 0x30);
                    index++;
                }
            }
            else
            {
                // one for end of string and one for padding char

                string bcdString = string.Empty;

                if (padding == ISOFieldPadding.LEFT)
                {
                    bcdString = "0" + value;
                }
                else if (padding == ISOFieldPadding.RIGHT)
                {

                    bcdString = value + "0";
                }

                valueLength = bcdString.Length;

                for (int i = 0; i < valueLength; i += 2)
                {
                    packedBytes[index] = (byte)((bcdString[i] - 0x30) * 0x10 + bcdString[i + 1] - 0x30);
                    index++;
                }
            }
        }
        /// <summary>
        /// Ascii 2 Bcd
        /// </summary>
        /// <param name="value"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        /// <param name="padding"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Ascii2Bcd(string value, byte[] packedBytes, ref int index, ISOFieldPadding padding)
        {
            int valueLength = value.Length;
            int startIndex = 0;
            bool needPadding = valueLength % 2 != 0;
            if (needPadding && padding == ISOFieldPadding.LEFT)
            {
                //left nibble padding leave empty and make a byte from the first nibble of the data 
                packedBytes[index] = (byte)(value[0] - 0x30);
                index++;
                //ignore the first char of the value 
                startIndex++;                                
            }  
            else if (needPadding && padding == ISOFieldPadding.RIGHT)
            {
                //Stop before the last half nibble
                valueLength--;
            }
            
            // no padding needed just convert to bcd
            for (int i = startIndex;  i < valueLength; i += 2)
            {                    
                packedBytes[index] = (byte)((value[i] - 0x30) * 0x10 + value[i + 1] - 0x30);
                index++;
            }
            if (needPadding && padding == ISOFieldPadding.RIGHT)
            {
                //right nibble padding, take the last nibble and set it as the high part of the byte, leave the low/right side as 0 
                packedBytes[index] = (byte)((value[valueLength] - 0x30) * 0x10);
                index++;
            }

        }
        /// <summary>
        /// Bcd to ASCII
        /// </summary>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        /// <param name="padding"></param>
        /// <param name="valueLength"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Bcd2Ascii(byte[] packedBytes, ref int index, ISOFieldPadding padding, int valueLength)
        {
            var value = valueLength <= MaxStackAllocationSize
             ? stackalloc char[valueLength]
             : new char[valueLength];

            if (valueLength % 2 == 0)
            {
                // no padding needed just convert to bcd               
                for (int i = 0; i < valueLength; i += 2)
                {
                    value[i] = (char)((packedBytes[index] >> 4) + 0x30);
                    value[i + 1] = (char)((packedBytes[index] & 0x0F) + 0x30);
                    index++;
                }
            }
            else
            {
                if (padding == ISOFieldPadding.LEFT)
                {
                    // LEFT padding so ignore the first half byte/ just read the second half byte
                    value[0] = (char)((packedBytes[index] & 0x0F) + 0x30);
                    index++;
                    for (int i = 1; i < valueLength; i += 2)
                    {
                        value[i] = (char)((packedBytes[index] >> 4) + 0x30);
                        value[i + 1] = (char)((packedBytes[index] & 0x0F) + 0x30);
                        index++;
                    }
                }


                if (padding == ISOFieldPadding.RIGHT)
                {
                    // RIGHT padding so ignore the last half byte/ just read the first half of last byte
                    int i;

                    for (i = 0; i < valueLength - 1; i += 2)
                    {
                        value[i] = (char)((packedBytes[index] >> 4) + 0x30);
                        value[i + 1] = (char)((packedBytes[index] & 0x0F) + 0x30);
                        index++;
                    }

                    value[i] = (char)((packedBytes[index] >> 4) + 0x30);
                }

            }
            return value.ToString();
        }
        /// <summary>
        /// Ascii to byte array
        /// </summary>
        /// <param name="strASCIIString"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Ascii2Bytes(string strASCIIString, byte[] packedBytes, ref int index)
        {
            int len = strASCIIString.Length;

            for (int i = 0; i < len; i++)
            {
                packedBytes[index + i] = (byte)strASCIIString[i];
            }
            index += len;
        }
        /// <summary>
        /// Bytes to ASCII
        /// </summary>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        /// <param name="numBytes"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Bytes2Ascii(byte[] packedBytes, ref int index, int numBytes)
        {
            var result = numBytes <= MaxStackAllocationSize
              ? stackalloc char[numBytes]
              : new char[numBytes];

            for (int i = 0; i < numBytes; i++)
            {
                result[index + i] = (char)packedBytes[index];
            }

            index += numBytes;

            return result.ToString();
        }
        /// <summary>
        /// Hex to byte array
        /// </summary>
        /// <param name="value"></param>
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Hex2Bytes(string value, byte[] packedBytes, ref int index)
        {
            int binlength = value.Length / 2;

            int i = 0;

            char a, b;

            for (i = 0; i < binlength; i++)
            {
                a = value[2 * i + 0];

                b = value[2 * i + 1];

                packedBytes[index + i] = (byte)((((a) <= '9' ? (a) - '0' : (a) - 'A' + 10) << 4) | ((b) <= '9' ? (b) - '0' : (b) - 'A' + 10));
            }

            index += binlength;
        }
        /// <summary>
        /// Hex string to byte array
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static byte[] Hex2Bytes(string str)
        {
            var HexNibble = _HexNibble;

            int byteCount = str.Length >> 1;

            byte[] result = new byte[byteCount + (str.Length & 1)];

            for (int i = 0; i < byteCount; i++)
                result[i] = (byte)(HexNibble[str[i << 1] - 48] << 4 | HexNibble[str[(i << 1) + 1] - 48]);

            if ((str.Length & 1) != 0)
                result[byteCount] = (byte)HexNibble[str[str.Length - 1] - 48];

            return result;
        }
        /// <summary>
        ///  Convert a byte[] to a hex values string, starting from the index position with lenght numbytes
        /// </summary>
        /// <param name="index"></param>
        /// <param name="numBytes"></param>
        /// <param name="packedBytes"></param>  
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Bytes2Hex(byte[] packedBytes, ref int index, int numBytes)
        {
            var lookup32 = _lookup32;

            int charLength = numBytes * 2;
            var result = charLength <= MaxStackAllocationSize
               ? stackalloc char[charLength]
               : new char[charLength];

            for (int i = 0; i < charLength; i += 2)
            {
                var val = lookup32[packedBytes[i / 2]];

                result[i] = (char)val;

                result[i + 1] = (char)(val >> 16);
            }
                        
            index += numBytes;

            return result.ToString();
        }
        /// <summary>
        /// Convert a byte[] to a hex values string
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="length"></param>
        public static string Bytes2HexOld(byte[] bytes, int length)
        {
            var lookup32 = _lookup32;

            var result = new char[length * 2];

            for (int i = 0; i < length; i++)
            {
                var val = lookup32[bytes[i]];

                result[2 * i] = (char)val;

                result[2 * i + 1] = (char)(val >> 16);
            }

            return new string(result);
        }
        private const int MaxStackAllocationSize = 256;
        /// <summary>
        /// Convert a byte[] to a hex values string
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Bytes2Hex(byte[] bytes, int length)
        {
            var lookup32 = _lookup32;

            int charLength = length * 2;
            var result = charLength <= MaxStackAllocationSize
               ? stackalloc char[charLength]
               : new char[charLength];
            
            for (int i = 0; i < charLength; i+=2)
            {
                var val = lookup32[bytes[i/2]];

                result[i] = (char)val;

                result[i + 1] = (char)(val >> 16);
            }

            return result.ToString();
        }
        /// <summary>
        /// Convert a byte[] to a hex values string
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string Bytes2Hex(byte[] bytes)
        {
            return Bytes2Hex(bytes, bytes.Length);
        }
        /// <summary>
        /// Writes the value of src to packedBytes in ebcdic format
        /// </summary>
        /// <param name="index"></param>
        /// <param name="packedBytes"></param>
        /// <param name="src"></param>
        public static void Ascii2Ebcdic(string src, byte[] packedBytes, ref int index)
        {
            var os_toebcdic = _os_toebcdic;

            int srcLength = src.Length;

            for (int i = 0; i < srcLength; i++)
            {
                packedBytes[index + i] = (byte)os_toebcdic[src[i]];
            }

            index += srcLength;
        }
        /// <summary>
        /// Converts ebcdic to ascii
        /// </summary>        
        /// <param name="packedBytes"></param>
        /// <param name="index"></param>
        /// <param name="numBytes"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Ebcdic2Ascii(byte[] packedBytes, ref int index, int numBytes)
        {
            var os_toascii = _os_toascii;

            var result = numBytes <= MaxStackAllocationSize
              ? stackalloc char[numBytes]
              : new char[numBytes];
            
            for (int i = 0; i < numBytes; i++)
            {
                result[i] = (char)os_toascii[packedBytes[index + i]];
            }

            index += numBytes;

            return result.ToString();
        }
        /// <summary>
        /// Check if value is a digit
        /// </summary>
        /// <param name="value"></param>
        public static bool IsDigits(string value)
        {
            /* checks that all characters in a string correspond to a numerical digit
             *
             * --------------------------
             * |ASCII | DEC -'0' |  Res |
             * |------|----------|------|
             * |  '0' | 48  - 48 | = 0  |
             * |  '1' | 49  - 48 | = 1  |
             * |  '2' | 50  - 48 | = 2  |
             * |  '3' | 51  - 48 | = 3  |
             * |  '4' | 52  - 48 | = 4  |
             * |  '5' | 53  - 48 | = 5  |
             * |  '6' | 54  - 48 | = 6  |
             * |  '7' | 55  - 48 | = 7  |
             * |  '8' | 56  - 48 | = 8  |
             * |  '9' | 57  - 48 | = 9  |
             * --------------------------
             */

            bool isOnlyDigits = true;

            foreach (char c in value)
            {
                int decimalDigit = c - '0';

                if ((decimalDigit < 0) || (decimalDigit > 9))
                {
                    isOnlyDigits = false;
                }
            }

            return isOnlyDigits;
        }
        /// <summary>
        /// Adds two integers and returns the result.
        /// </summary>
        /// <param name="value">The integer to be convert to bytes</param>  
        public static bool IsHexDigits(string value)
        {
            /* checks that all characters in a string correspond to a numerical digit
             * 
             * --------------------------
             * |ASCII | DEC -'0' |  Res |
             * |------|----------|------|
             * |  '0' | 48  - 48 | = 0  |
             * |  '1' | 49  - 48 | = 1  |
             * |  '2' | 50  - 48 | = 2  |
             * |  '3' | 51  - 48 | = 3  |
             * |  '4' | 52  - 48 | = 4  |
             * |  '5' | 53  - 48 | = 5  |
             * |  '6' | 54  - 48 | = 6  |
             * |  '7' | 55  - 48 | = 7  |
             * |  '8' | 56  - 48 | = 8  |
             * |  '9' | 57  - 48 | = 9  |
             * |  'A' | 65  - 48 | = 17 |
             * |  'B' | 66  - 48 | = 18 |
             * |  'C' | 67  - 48 | = 19 |
             * |  'D' | 68  - 48 | = 20 |
             * |  'E' | 69  - 48 | = 21 |
             * |  'F' | 70  - 48 | = 22 |
             * --------------------------
             */

            bool isHexDigits = true;

            foreach (char c in value)
            {
                int decimalDigit = c - '0';

                if (decimalDigit < 0 || decimalDigit > 9)
                {
                    if (decimalDigit < 17 || decimalDigit > 22)
                    {
                        isHexDigits = false;
                    }
                }
            }

            return isHexDigits;
        }
        /// <summary>
        /// Adds two integers and returns the result.
        /// </summary>
        /// <param name="value">The integer to be convert to bytes</param>  
        public static bool IsAscii(string value)
        {
            // ASCII encoding replaces non-ascii with question marks, so we use UTF8 to see if multi-byte sequences are there
            return Encoding.UTF8.GetByteCount(value) == value.Length;
        }
        /// <summary>
        /// Prepares in a string a nice representation of binary data to hex data and ascii (whereever binary characters happen to be printable else is replacing them with a dot
        /// </summary>
        /// <param name="iNumBytes"></param>
        /// <param name="iPtr"></param>
        /// <returns>
        /// The string with the nicely formated hex representation of binary data
        /// </returns>
        public static string PrintHex(byte[] iPtr, int iNumBytes)
        {
            uint kDL_OUTPUT_HEX_COLS = 16;

            string hexBuffer = string.Empty;

            uint rowIdx, colIdx;

            for (rowIdx = 0; rowIdx < (iNumBytes + kDL_OUTPUT_HEX_COLS - 1) / kDL_OUTPUT_HEX_COLS; rowIdx++)
            {

                hexBuffer = hexBuffer + string.Format("{0:D8}  ", rowIdx * kDL_OUTPUT_HEX_COLS);

                /*
		         *  output hex characters
		         */

                for (colIdx = 0; colIdx < kDL_OUTPUT_HEX_COLS; colIdx++)
                {

                    uint offset = (rowIdx * kDL_OUTPUT_HEX_COLS) + colIdx;

                    if (offset >= iNumBytes)
                    {
                        hexBuffer = hexBuffer + "   ";
                    }
                    else
                    {
                        hexBuffer = hexBuffer + string.Format("{0:X2} ", iPtr[offset]);
                    }

                } /* end-for (colIdx) */

                hexBuffer = hexBuffer + "  ";

                /*
		         * output ascii characters (if printable)
		         */

                for (colIdx = 0; colIdx < kDL_OUTPUT_HEX_COLS; colIdx++)
                {

                    uint offset = (rowIdx * kDL_OUTPUT_HEX_COLS) + colIdx;

                    if (offset >= iNumBytes)
                    {
                        hexBuffer = hexBuffer + " ";
                    }
                    else if ((iPtr[offset] >= 33) && (iPtr[offset] <= 126))
                    {
                        hexBuffer = hexBuffer + (char)iPtr[offset];
                    }
                    else
                    {
                        hexBuffer = hexBuffer + ".";
                    }

                } /* end-for (colIdx) */

                hexBuffer = hexBuffer + "\n";

            } /* end-for (rowIdx) */

            return hexBuffer.ToString();
        }
    }
}

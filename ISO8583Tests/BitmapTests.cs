using ISO8583Net.Field;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections;
using System.Collections.Generic;
using Xunit;
using ISO8583Net.Utilities;
using System.Linq;
using ISO8583Net.Packager;
using Microsoft.Extensions.Logging;
using ISO8583Net.Message;

namespace ISO8583Tests
{
    public class BitmapTests
    {

        private ILogger logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<BitmapTests>();

        [Fact]
        public void HexBytes2Hex2Works()
        {
            string stringhex = "29001234567890123456193012121959";
            byte[] bytes = ISOUtils.Hex2Bytes(stringhex);
            
            string oldValue = ISOUtils.Bytes2HexOld(bytes, stringhex.Length/2);
            Assert.Equal(stringhex, oldValue);

            string newValue = ISOUtils.Bytes2Hex(bytes, stringhex.Length/2);
            Assert.Equal(stringhex, oldValue);
        }

        [Fact]
        public void BitmapIsSameAsField_1()
        {

            ISOFieldBitmap bitmapOnly = new ISOFieldBitmap(new Microsoft.Extensions.Logging.Abstractions.NullLogger<BitmapTests>());
            bitmapOnly.SetBit(0);
            bitmapOnly.SetBit(2);
            bitmapOnly.SetBit(3);
            bitmapOnly.SetBit(4);
            bitmapOnly.SetBit(7);
            bitmapOnly.SetBit(11);
            bitmapOnly.SetBit(12);
            bitmapOnly.SetBit(14);
            bitmapOnly.SetBit(18);
            bitmapOnly.SetBit(19);
            bitmapOnly.SetBit(22);
            bitmapOnly.SetBit(25);
            bitmapOnly.SetBit(37);

            var mPackager = new ISOMessagePackager(logger); // initialize from default visa packager that is embeded as a resource in the library
            var m = new ISOMessage(logger, mPackager);

            m.Set(0, "0100");
            m.Set(2, "40004000400040001");
            m.Set(3, "000000");
            m.Set(4, "000000002900");
            m.Set(7, "1231231233");
            m.Set(11, "123123");
            m.Set(12, "193012");
            m.Set(14, "1219");
            m.Set(18, "5999");
            m.Set(19, "196");
            m.Set(22, "9010");
            m.Set(25, "23");
            m.Set(37, "123123123123");

            var bitmap = m.GetField(1) as ISO8583Net.Field.ISOFieldBitmap;


            var bmOnlybytes = bitmapOnly.GetByteArray();
            var bmBytes = bitmap.GetByteArray();
            for (int i = 0; i < bmOnlybytes.Length; i++)
            {
                Assert.Equal(bmBytes[i], bmOnlybytes[i]);
            }
        }
        [Fact]
        public void GetSetFieldsWorks()
        {
            ISOFieldBitmap bitmap = new ISOFieldBitmap(new Microsoft.Extensions.Logging.Abstractions.NullLogger<BitmapTests>());
            bitmap.SetBit(0);
            bitmap.SetBit(2);
            bitmap.SetBit(3);
            bitmap.SetBit(4);
            bitmap.SetBit(7);
            bitmap.SetBit(11);
            bitmap.SetBit(12);
            bitmap.SetBit(14);
            bitmap.SetBit(18);
            bitmap.SetBit(19);
            bitmap.SetBit(22);
            bitmap.SetBit(25);
            bitmap.SetBit(37);

            //Collect status of all fields using BitIsSet
            bool[] fieldsOld = new bool[196];
            int length = bitmap.GetByteArray().Length * 8;
            for (int i = 0; i < length; i++)
            {
                if (i != 1 && i != 65)
                {
                    fieldsOld[i] = bitmap.BitIsSet(i);
                }
            }

            //Collect status of all fields using GetSetFields
            bool[] fields = new bool[196];
            var setFields = bitmap.GetSetFields();
            for (int i = 0; i < setFields.Length; i++)
            {
                fields[setFields[i]] = true;
            }

            //Check if same
            for (int i = 1; i < 196; i++)
            {
                Assert.Equal(fieldsOld[i], fields[i]);
            }
        }
        [Fact]
        public void FieldEnumeratorWorks()
        {
            ISOFieldBitmap bitmap = new ISOFieldBitmap(new Microsoft.Extensions.Logging.Abstractions.NullLogger<BitmapTests>());
            bitmap.SetBit(0);
            bitmap.SetBit(2);
            bitmap.SetBit(3);
            bitmap.SetBit(4);
            bitmap.SetBit(7);
            bitmap.SetBit(11);
            bitmap.SetBit(12);
            bitmap.SetBit(14);
            bitmap.SetBit(18);
            bitmap.SetBit(19);
            bitmap.SetBit(22);
            bitmap.SetBit(25);
            bitmap.SetBit(37);

            //Collect status of all fields using enumerator
            bool[] fields = new bool[196];            
            var enumerator = bitmap.GetByteArray().GetFieldIdEnumerator();
            foreach (var item in enumerator)
            {
                fields[item] = true;
            }

            //Collect status of all fields using BitIsSet
            bool[] fieldsOld = new bool[196];            
            int length = bitmap.GetByteArray().Length * 8;
            for (int i = 0; i < length; i++)
            {
                if (i != 1 && i != 65)
                {
                    fieldsOld[i] = bitmap.BitIsSet(i);
                }
            }
            //Check if same
            for (int i = 1; i < 196; i++)
            {
                Assert.Equal(fieldsOld[i], fields[i]);
            }

            
        }

        [Fact] 
        public void BitmapFieldEnumeratorWorks()
        {
            ISOFieldBitmap bitmap = new ISOFieldBitmap(new Microsoft.Extensions.Logging.Abstractions.NullLogger<BitmapTests>());
            bitmap.SetBit(0);
            bitmap.SetBit(1);
            bitmap.SetBit(2);
            bitmap.SetBit(8);
            bitmap.SetBit(12);
            bitmap.SetBit(34);
            bitmap.SetBit(62);

            var setFields = bitmap.GetByteArray().GetFieldIdEnumerator().ToList();
            int length = bitmap.GetByteArray().Length;
            for (int i = 0; i < length; i++)
            {
                if (i != 1 && i != 65)
                {
                    bool fieldExists = setFields.Exists(field => field == i);
                    Assert.Equal(bitmap.BitIsSet(i), fieldExists);
                }
            }
           
        }
        [Fact]
        public void BitIsSetWorks()
        {
            
            ISOFieldBitmap bitmap = new ISOFieldBitmap(new Microsoft.Extensions.Logging.Abstractions.NullLogger<BitmapTests>());
            bitmap.SetBit(0);
            bitmap.SetBit(1);
            bitmap.SetBit(2);
            bitmap.SetBit(8);
            bitmap.SetBit(12);
            bitmap.SetBit(34);
            bitmap.SetBit(62);

            for (int i = 0; i < 128; i++)
            {
                if (i == 0 || i == 1 || i == 2 || i == 8 || i == 12 || i ==34 || i == 62)
                {
                    Assert.True(bitmap.BitIsSet(i));
                }
                else
                {
                    Assert.False(bitmap.BitIsSet(i));
                } 
            }
            
        }
        [Fact]
        public void BitmapEnumeratorWorks()
        {

            ISOFieldBitmap bitmap = new ISOFieldBitmap(new Microsoft.Extensions.Logging.Abstractions.NullLogger<BitmapTests>());
            bitmap.SetBit(0);
            bitmap.SetBit(1);
            bitmap.SetBit(2);
            bitmap.SetBit(8);
            bitmap.SetBit(12);
            bitmap.SetBit(34);
            bitmap.SetBit(62);
                    
            int i = 0;
            foreach (var item in bitmap.GetByteArray().GetBitEnumerator())
            {
                if (i != 1 && i != 65)
                {
                    Assert.Equal(bitmap.BitIsSet(i), item);
                }
                i++;
            }

        }
    }
}

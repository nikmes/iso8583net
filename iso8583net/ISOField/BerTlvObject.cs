﻿using ISO8583Net.Utilities;
using System;
using System.Collections.Generic;
using System.Text;

namespace ISO8583Net.Field
{
    public class BerTLVObject
    {
        private BerTLVObject parent = null;

        public BerTLVObject Parent
        {
            get { return parent; }
            set { parent = value; }
        }

        private List<BerTLVObject> childList = new List<BerTLVObject>();

        public List<BerTLVObject> ChildList
        {
            get { return childList; }
            set { childList = value; }
        }

        public string TagStr
        {
            get
            {
                return tagStr;
            }
        }

        public string LengthStr
        {
            get
            {
                return ISOUtils.Bytes2Hex(lenBytes, lenBytes.Length);
            }
        }

        public string ValueStr
        {
            get
            {
                return ISOUtils.Bytes2Hex(mValue, mValue.Length);
            }
        }

        public byte[] Tag
        {
            get
            {
                return ISOUtils.HexToByteArray(tagStr);
            }
        }

        public byte[] Length
        {
            get
            {
                return lenBytes;
            }
        }

        public byte[] Value
        {
            get
            {
                return mValue;
            }
        }

        public int LengthInt
        {
            get
            {
                return mLen;
            }
        }

        private string tagStr;

        private int tagWidth;

        private int mLen;

        private int lenWidth;

        private byte[] lenBytes;

        private byte[] mValue;

        public BerTLVObject(string tag)
        {
            if ((tag.Length % 2) != 0)
                throw new Exception("Error in Tag (length)");

            tagWidth = tag.Length / 2;
            tagStr = tag;
            mLen = 0;
            mValue = new byte[0];
            SetLengthBytes();

            this.parent = null;
        }

        public BerTLVObject(string tag, byte[] value)
        {
            if ((tag.Length % 2) != 0)
                throw new Exception("Error in Tag (length)");

            tagWidth = tag.Length / 2;

            tagStr = tag;
            mLen = value.Length;
            mValue = value;
            SetLengthBytes();

            this.parent = null;
        }

        public BerTLVObject(string tag, string strVal)
        {
            if ((tag.Length % 2) != 0)
                throw new Exception("Error in Tag (length)");

            if ((strVal.Length % 2) != 0)
                throw new Exception("Error in Value (length)");

            tagWidth = tag.Length / 2;

            tagStr = tag;
            mLen = strVal.Length / 2;
            mValue = ISOUtils.HexToByteArray(strVal);

            SetLengthBytes();

            this.parent = null;
        }

        public void SetValue(byte[] value)
        {
            mLen = value.Length;
            mValue = new byte[mLen];
            Array.Copy(value, 0, mValue, 0, mLen);

            SetLengthBytes();
        }

        public void SetValue(string value)
        {
            mLen = value.Length / 2;
            mValue = ISOUtils.HexToByteArray(value);
            SetLengthBytes();
        }

        public void AddChildObject(BerTLVObject obj)
        {
            mValue = ISOUtils.BufferConcat(mValue, obj.ToByteArray());
            mLen = mValue.Length;
            SetLengthBytes();
            obj.parent = this;
            this.childList.Add(obj);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("TAG:").Append(tagStr).Append(" LEN:").Append(mLen.ToString()).Append(" DATA:").Append(ISOUtils.Bytes2Hex(mValue, mValue.Length));
            if (this.parent != null)
                sb.Append(" Parent:" + this.parent.TagStr);
            else
                sb.Append(" Parent: ROOT");
            sb.Append(" Children:");

            if (this.childList.Count == 0)
                sb.Append("N/A");

            foreach (BerTLVObject obj in this.childList)
                sb.Append(obj.tagStr + " ");

            return sb.ToString();
        }

        public byte[] ToByteArray()
        {

            byte[] Length = null;

            if (mLen < 128)
            {
                Length = new byte[1];
                Length[0] = (byte)this.mLen;
                lenWidth = 1;
            }
            else if (mLen < 256)
            {
                Length = new byte[2];
                Length[0] = 0x81;
                Length[1] = (byte)this.mLen;
                lenWidth = 2;
            }
            else
            {
                Length = new byte[3];
                Length[0] = 0x82;
                Length[1] = (byte)(mLen / 256);
                Length[2] = (byte)(mLen % 256);
                lenWidth = 3;
            }

            byte[] ret = new byte[tagWidth + mValue.Length + lenWidth];

            Array.Copy(ISOUtils.HexToByteArray(tagStr), 0, ret, 0, tagWidth);
            Array.Copy(Length, 0, ret, tagWidth, lenWidth);

            if (this.mValue != null)
                Array.Copy(mValue, 0, ret, tagWidth + lenWidth, mValue.Length);

            return ret;
        }

        private void SetLengthBytes()
        {
            if (mLen < 128)
            {
                lenBytes = new byte[1];
                lenBytes[0] = (byte)mLen;
                lenWidth = 1;
            }
            else if (mLen < 256)
            {
                lenBytes = new byte[2];
                lenBytes[0] = 0x81;
                lenBytes[0] = (byte)mLen;
                lenWidth = 2;
            }
            else
            {
                lenBytes = new byte[3];
                lenBytes[0] = 0x82;
                lenBytes[1] = (byte)(mLen / 256);
                lenBytes[2] = (byte)(mLen % 256);
                lenWidth = 3;
            }
        }
    }
}

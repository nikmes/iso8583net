using ISO8583Net.Field;
using ISO8583Net.Types;
using ISO8583Net.Utilities;
using Microsoft.Extensions.Logging;
using System.Text;

namespace ISO8583Net.Packager
{
    public class ISOFieldPackager : ISOPackager
    {
        public ISOFieldPackager(ILogger logger) : base(logger) { }

        public ISOFieldPackager(ILogger logger, ISOFieldDefinition isoFieldDefinition) : base(logger, isoFieldDefinition) { }

        public void SetFieldDefinition(ISOFieldDefinition isoFieldDefinition)
        {
            m_isoFieldDefinition = isoFieldDefinition;
        }

        public void SetFieldNumber(int number)
        {
            m_number = number;
        }

        public int GetFieldLength()
        {
           return m_isoFieldDefinition.length;      
        }

        public void Validate(ISOComponent isoField)
        {
            // Check field length limitation
            // Validate content based on Definition AN, N, ASCII, HEX (if BIN) etc
            // if FIXED check that user provided exact data
            // if VAR check that is not exceeding max definition

            this.Trace();

            //  Validate the packager
            //  ---------------------
            //
            // Notes on Field Encoding
            // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
            // 01. Field Encoding   BCD    implies field value format = N and should be padded if not ODD (LEFT or RIGHT)
            // 02. Field Encoding   ASCII  implies each char is [a..z][A..Z][0..9] and value length should always be ODD number
            // 03. Field Encoding   BIN    implies each char is [0..9][A..F]
            // 04. Field Encoding   EBCDIC implies each char is an EBCDIC char
            // 05. Field Encoding   Z      implies that field contains [0..9]0x0D[0..9]
            //
            // Notes on Field Format
            // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
            // 06. Field Format     A      implies field value contains only [A..Z] [a..z] 
            // 07. Field Format     AN     implies field value contains only [A..Z] [a..z] [0..9]
            // 08. Field Format     ANS    implies field value contains only [A..Z] [a..z] [0..9] [!"£$%^&*()_+-=[]{}#';~@:/.,?><\|`¬] 
            // 09. Field Format     AS     implies field value contains only [A..Z] [a..z] [!"£$%^&*()_+-=[]{}#';~@:/.,?><\|`¬]
            // 10. Field Format     N      implies field value contains only [0..9]    
            // 11. Field Format     NS     implies field value contains only [0..9] [!"£$%^&*()_+-=[]{}#';~@:/.,?><\|`¬]
            // 12. Field Format     Z      implies field value contains only [0..9] [0x0D]
            // 13. Field Format     HD     implies field value contains only [0..9] [A..F] Hexadecimal Digits
            //
            // Notes on Length Type                      
            // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
            // 13. Length Type      FIXED  implies that field length is always of length equals to field length definition
            // 14. Length Type      LVAR   implies that field length is using 1 byte to indicate the length of value to follow and has MAX value equal to what is in field length definition or no more than 255
            // 15. Length Type      LLVAR  implies that field length is using 2 bytes to indicate the length of value to follow and has MAX value equal to what is in field length definition or no more than 65535
            //
            // Notes on Length Encoding 
            // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
            // 16. Length Encoding  BCD    implies field value format = N and should be padded if not ODD (LEFT or RIGHT)
            // 17. Length Encoding  BIN    implies each char is [0..9][A..F] and value length should always be ODD number
            // 18. Length Encoding  ASCII  implies each char is [0..9] since is length
            // 19. Length Encoding  EBCDIC implies each char is a valid EBCDIC char and is [0..9]
            // 20. Length Encoding  Z      NOT POSSIBLE TO USE AS LENGTH ENCODING
            //
            // Notes on Lengths
            // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
            // 21. When Encoding is BCD    Length is expressed in nipples (1/2 bytes) For Example: field 002 card number len = 19 means 19 Nipple means field value can be up to 19 decimal digits 
            // 22. When Encoding is BIN    Length is expressed in hexadecimal digits  For Example: field 064 Mac len = 16 means 8 Bytes which means a valid hexadecimal sequence of length 16 0123456789ABCDEF
            // 23. When Encoding is ASCII  Length is expressed in characters          For Example: if len=16 it means 16 ASCII characters (8bytes)
            // 24. When Encoding is EBCDIC Length is expressed in characters          For Example: if len=16 it means 16 EBCDIC characters (8bytes)
            // 25. When Encoding is Z      Length is expressed in niples (1/2Bytes) plus 1 byte for HEXADECIMAL 0x0D those 0x0D is accounted for two nibbles 

            string fldValue = isoField.GetValue();

            string fldNumber = m_number.ToString().PadLeft(3, '0');

            // validate the content of field value based on field format

            switch (m_isoFieldDefinition.content)
            {
                case ISOFieldContent.A:
                    // check if is only [A..Z][a..z]
                    break;

                case ISOFieldContent.AN:
                    // check if is only [A..Z][a..z][0..1]
                    break;

                case ISOFieldContent.ANS:
                    // check if is only [A..Z] [a..z] [0..9] [!"£$%^&*()_+-=[]{}#';~@:/.,?><\|`¬] 
                    break;

                case ISOFieldContent.AS:
                    // check if is only [A..Z] [a..z] [!"£$%^&*()_+-=[]{}#';~@:/.,?><\|`¬] 
                    break;

                case ISOFieldContent.N:
                    // check if is only [0..9]
                    break;

                case ISOFieldContent.NS:
                    // check if is only [0..9] [!"£$%^&*()_+-=[]{}#';~@:/.,?><\|`¬] 
                    break;

                case ISOFieldContent.HD:
                    // check is only [0..9] [A..F] Hexadecimal Digits
                    break;
            }

            // validate the field value based on field content coding

            switch (m_isoFieldDefinition.contentCoding)
            {
                case ISOFieldCoding.EBCDIC:

                    if (ISOUtils.IsAscii(fldValue))
                    {
                        if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Field [" + fldNumber + "] contains only EBCDIC characters [OK]");
                    }
                    else
                    {
                        if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Field [" + fldNumber + "] contains non EBCDIC characters [NOT-OK]");
                    }

                    break;

                case ISOFieldCoding.ASCII:

                    if (ISOUtils.IsAscii(fldValue))
                    {
                        if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Field [" + fldNumber + "] contains only ASCII characters [OK]");
                    }
                    else
                    {
                        //if (Logger.IsEnabled(LogLevel.Critical)) Logger.LogCritical("Field [" + fldNumber + "] contains non ASCII characters [NOT-OK]");
                    }

                    break;

                case ISOFieldCoding.BCDU:
                case ISOFieldCoding.BCD:

                    if (ISOUtils.IsDigits(fldValue))
                    {
                       // if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Field [" + fldNumber + "] contains only decimal digits [OK]");
                    }
                    else
                    {
                        //if (Logger.IsEnabled(LogLevel.Critical)) Logger.LogCritical("Field [" + fldNumber + "] contains non decimal digits [NOT-OK]");
                    }

                    break;


                case ISOFieldCoding.BIN:
 
                    if (ISOUtils.IsHexDigits(fldValue))
                    {
                        //if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Field [" + fldNumber + "] contains only hexadecimal digits [OK]");
                    }
                    else
                    {
                       // if (Logger.IsEnabled(LogLevel.Critical)) Logger.LogCritical("Field [" + fldNumber + "] contains non hexadecimal digits [NOT-OK]");
                    }

                    break;

                case ISOFieldCoding.Z:
                    break;
            }

            // validate length of field value based on length field coding

            switch (m_isoFieldDefinition.lengthCoding)
            {
                case ISOFieldCoding.ASCII:
                    break;

                case ISOFieldCoding.BCD:
                    break;

                case ISOFieldCoding.BCDU:
                    break;

                case ISOFieldCoding.BIN:
                    break;

                case ISOFieldCoding.EBCDIC:
                    break;

                case ISOFieldCoding.Z:
                    break;
            }

            // validate length based on length format

            switch (m_isoFieldDefinition.lengthFormat)
            {
                // All checks are based on value length and definition length since length is always ecxpressed in units of coding used 
                // For example, ASCII=number of chars, BIN=number of hex digits, BCD=number of nibbles etc

                case ISOFieldLengthFormat.FIXED:

                    if ((fldValue.Length == m_isoFieldDefinition.length) && this.m_number != 1)
                    {
                        //if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Field [" + fldNumber + "] Value has Length[" + fldValue.Length + "] = Definition Length=[" + m_isoFieldDefinition.m_length +"] [OK]");
                    }
                    else if ( (fldValue.Length > m_isoFieldDefinition.length) && this.m_number != 1)
                    {
                        //if (Logger.IsEnabled(LogLevel.Critical)) Logger.LogCritical("Field [" + fldNumber + "] Value has Length[" + fldValue.Length + "] > Definition Length=[" + m_isoFieldDefinition.m_length + "] [NOT-OK] [TRIM & PACK POSSIBLE]");
                    }
                    else if ((fldValue.Length < m_isoFieldDefinition.length) && this.m_number != 1)
                    {
                        //if (Logger.IsEnabled(LogLevel.Critical)) Logger.LogCritical("Field [" + fldNumber + "] Value has Length[" + fldValue.Length + "] < Definition Length=[" + m_isoFieldDefinition.m_length + "] [NOT-OK] [PACK IMPOSSIBLE]");
                    }
                    break;

                case ISOFieldLengthFormat.VAR:

                    if ((fldValue.Length <= m_isoFieldDefinition.length) && this.m_number !=1 )
                    {
                        //if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Field [" + fldNumber + "] Value has Length[" + fldValue.Length + "] <= Definition Length=[" + m_isoFieldDefinition.m_length + "] [OK]");
                    }
                    else if ((fldValue.Length > m_isoFieldDefinition.length) && this.m_number != 1)
                    {
                        //if (Logger.IsEnabled(LogLevel.Critical)) Logger.LogCritical("Field [" + fldNumber + "] value length[" + fldValue.Length + "] > definition length[" + m_isoFieldDefinition.m_length + "] [NOT-OK] [TRIM & PACK POSSIBLE");
                    }
                    break;
            }
        }

        public override void Pack(ISOComponent isoField, byte[] packedBytes, ref int index)
        {
            //if (this.GetStorageClass() != "ISO8583Net.ISOMessageSubFields")
            //{
            //    //Validate(isoField);
            //}

            // handle length type, size and coding

            string isoFieldValue = isoField.GetValue(); // value that user assigned

            if (m_isoFieldDefinition.lengthFormat == ISOFieldLengthFormat.FIXED)
            {
                //if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("ISOField [" + m_number + "] is of FIXED length format");
                // Do nothing, there is no length indicator
            }
            else if (m_isoFieldDefinition.lengthFormat == ISOFieldLengthFormat.VAR)
            {
                // variable length format, check how many units of m_isoFieldDefinition.m_lengthCoding we should use
                switch (m_isoFieldDefinition.lengthCoding)
                {
                    case ISOFieldCoding.BIN:
                        // convert HexDigits to bytes - lengthlength = number of hexadecimal digits
                        //if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("ISOField [" + m_number + "] is of variable length and of BINARY format with lengthIndicator=[" + m_isoFieldDefinition.m_lengthLength + "] bytes");
                        ISOUtils.Int2Bytes(isoFieldValue.Length, packedBytes, ref index, m_isoFieldDefinition.lengthLength);
                        break;

                    case ISOFieldCoding.ASCII:
                        // convert ascii char to the corresponding byte value - lengthlength = number of ascii characters
                        //if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("ISOField [" + m_number + "] is of variable length and of ASCII format with lengthIndicator=[" + m_isoFieldDefinition.m_lengthLength + "] ascii characters");
                        break;

                    case ISOFieldCoding.EBCDIC:
                        // convert EBCDIC char to the corresponding byte value - lengthlength = number of ebdic characters
                        //if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("ISOField [" + m_number + "] is of variable length and of EBCDIC format with lengthIndicator=[" + m_isoFieldDefinition.m_lengthLength + "] ebdic charachters");
                        break;

                    case ISOFieldCoding.BCD:
                        //if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("ISOField [" + m_number + "] is of variable length and of BCD format with lengthIndicator=[" + m_isoFieldDefinition.m_lengthLength + "] BCD Nibles");
                        // convert bcd digits to byte values - lengthlength = number of bcd nipples (1 byte has 2 nibbles and padding should be used) 
                        break;

                    default:
                        //if (Logger.IsEnabled(LogLevel.Critical)) Logger.LogCritical("Error packaging field [" + m_number + "]. Length Coding Type is Invalid!");
                        break;
                }
            }
            else
            {
                //if (Logger.IsEnabled(LogLevel.Critical)) Logger.LogCritical("Error packaging field [" + m_number + "]. Field Length Format is Invalid!");
            }

            // handle content coding

            // if is of type ISOMessageFields then dont try pack the content as is invalid 

            if (!this.IsComposite()) //this.GetStorageClass() != "ISO8583Net.ISOMessageSubFields")
            {
                ////!!!! NOT EFFICIENT - WE KNOW THE TYPE GET VALUES IN BYTES WHERE YOU CAN !!!!!!/////
                
                switch (m_isoFieldDefinition.contentCoding)
                {
                    case ISOFieldCoding.BCD:
                        ISOUtils.ascii2bcd(isoFieldValue, packedBytes, ref index, m_isoFieldDefinition.contentPadding);
                        break;

                    case ISOFieldCoding.ASCII:
                        ISOUtils.ascii2bytes(isoField.GetValue(), packedBytes, ref index);
                        break;

                    case ISOFieldCoding.BIN:
                        ISOUtils.hex2bytes(isoField.GetValue(), packedBytes, ref index);
                        break;

                    case ISOFieldCoding.EBCDIC:
                        ISOUtils.ascii2ebcdic(isoField.GetValue(), packedBytes, ref index);
                        break;

                    //case ISOFieldCoding.Z:
                    //    break;

                    //case ISOFieldCoding.BCDU:
                    //    break;

                    default:
                       //if (Logger.IsEnabled(LogLevel.Critical)) Logger.LogCritical("Unknown content coding for Field [" + m_number.ToString().PadLeft(3, '0') + "]. Fallback to ASCII Packager");

                        ISOUtils.ascii2bytes(isoField.GetValue(), packedBytes, ref index);

                        break;
                }
            }
        }

        public override void UnPack(ISOComponent isoField, byte[] packedBytes, ref int index)
        {
            //if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Trying to Unpack Field [" + m_number.ToString().PadLeft(3, '0') + "]");

            int lengthToRead = m_isoFieldDefinition.length;   // in type units

            // handle length type, size and coding

            if (m_isoFieldDefinition.lengthFormat == ISOFieldLengthFormat.FIXED)
            {
                // Do nothing, there is no length indicator

                lengthToRead = m_isoFieldDefinition.length; // in type units

                //if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("ISOField [" + m_number.ToString().PadLeft(3, '0') + "] is of FIXED length format");
            }
            else if (m_isoFieldDefinition.lengthFormat == ISOFieldLengthFormat.VAR)
            {
                //if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("ISOField [" + m_number.ToString().PadLeft(3, '0') + "] is of VARIABLE length format");

                if (m_isoFieldDefinition.lengthCoding == ISOFieldCoding.BCD)
                {
                    // adjust legnthToRead

                }
                else if (m_isoFieldDefinition.lengthCoding == ISOFieldCoding.ASCII)
                {
                    // adjust legnthToRead

                }
                else if (m_isoFieldDefinition.lengthCoding == ISOFieldCoding.BIN)
                {
                    // adjust legnthToRead

                    lengthToRead = ISOUtils.Bytes2Int(packedBytes, ref index, m_isoFieldDefinition.lengthLength); // !!! hmmm this value is not bytes, it depends on content coding !! 

                    //if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Length of VARIABLE Field [" + m_number.ToString().PadLeft(3, '0') + "] is [" + lengthToRead + "]");

                }
                else if (m_isoFieldDefinition.lengthCoding == ISOFieldCoding.EBCDIC)
                {
                    // adjust legnthToRead

                }
                else
                {
                    //if (Logger.IsEnabled(LogLevel.Critical)) Logger.LogCritical("Unknown length coding for Field [" + m_number.ToString().PadLeft(3, '0') + "]");
                }
            }
            else
            {
                //if (Logger.IsEnabled(LogLevel.Critical)) Logger.LogCritical("Error Unpacking field [" + m_number + "]. Field Length Format is Invalid!");
            }

            // handle content coding

            if (!this.IsComposite()) //this.GetStorageClass() != "ISO8583Net.ISOMessageSubFields")
            {
                if (m_isoFieldDefinition.contentCoding == ISOFieldCoding.BCD)
                {
                    isoField.SetValue(ISOUtils.bcd2ascii(packedBytes, ref index, m_isoFieldDefinition.contentPadding, lengthToRead));

                    //if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("Field  [" + m_number + "] [" + isoField.GetValue() + "]");
                }
                else if (m_isoFieldDefinition.contentCoding == ISOFieldCoding.ASCII)
                {
                    isoField.SetValue(ISOUtils.bytes2ascii(packedBytes, ref index, lengthToRead));

                    //if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("Field  [" + m_number + "] [" + isoField.GetValue() + "]");
                }
                else if (m_isoFieldDefinition.contentCoding == ISOFieldCoding.BIN)
                {
                    // handle the ISOMessage Bitmap differently than other Bitmaps

                    if (m_isoFieldDefinition.lengthFormat == ISOFieldLengthFormat.FIXED && m_storeClass == "ISO8583Net.Field.ISOFieldBitmap")
                    {
                        //if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("ISO field [" + m_number + "] is ISOFieldBitmap and is FIXED Length but special handling needed based on 2nd and 3rd Bitmap Indicator.");

                        // no length to read, the Set method of Bitmap will identify if 2nd and 3rd bitmap is set so it will know how many byets to read

                        ((ISOFieldBitmap)isoField).Set(packedBytes, ref index);
                    }
                    else
                    {
                        isoField.SetValue(ISOUtils.bytes2hex(packedBytes, ref index, lengthToRead / 2)); // Number of hex digits so convert to number of bytes
                    }
                }
                else if (m_isoFieldDefinition.contentCoding == ISOFieldCoding.EBCDIC)
                {
                    isoField.SetValue(ISOUtils.ebcdic2ascii(packedBytes, ref index, lengthToRead));

                    //if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation("Field  [" + m_number + "] [" + isoField.GetValue() + "]");

                }
                else
                {
                    //if (Logger.IsEnabled(LogLevel.Critical)) Logger.LogCritical("Unknown content coding for Field [" + m_number.ToString().PadLeft(3, '0') + "]. Fallback to ASCII UnPack");
                }
            }
        }

        public override string ToString()
        {
            StringBuilder strBuilder = new StringBuilder("");

            strBuilder.Append("ISOFieldPackager Definition: \n");
            strBuilder.Append(        "   Field Number: " + m_number + "\n");
            strBuilder.Append(        "           Name: " + m_isoFieldDefinition.name + "\n");
            switch (m_isoFieldDefinition.contentCoding)
            {
                case ISOFieldCoding.ASCII:
                    strBuilder.Append("         Length: " + m_isoFieldDefinition.length + " ASCII characters");
                    strBuilder.Append("Length in bytes: " + m_isoFieldDefinition.length);
                    break;

                case ISOFieldCoding.BCD:
                    strBuilder.Append("         Length: " + m_isoFieldDefinition.length + " Nibbles (half bytes)");
                    strBuilder.Append("Length in bytes: " + (float)m_isoFieldDefinition.length / 2);
                    break;

                case ISOFieldCoding.BIN:
                    strBuilder.Append("         Length: " + m_isoFieldDefinition.length + " Hexadecimal Digits");
                    strBuilder.Append("Length in bytes: " + m_isoFieldDefinition.length / 2);
                    break;

                case ISOFieldCoding.BCDU:
                    strBuilder.Append("         Length: " + m_isoFieldDefinition.length);
                    strBuilder.Append("Length in bytes: " + m_isoFieldDefinition.length);
                    break;

                case ISOFieldCoding.EBCDIC:
                    strBuilder.Append("         Length: " + m_isoFieldDefinition.length + " EBCDIC characters");
                    strBuilder.Append("Length in bytes: " + m_isoFieldDefinition.length);
                    break;

                case ISOFieldCoding.Z:
                    strBuilder.Append("         Length: " + m_isoFieldDefinition.length + " Nibbles (plus 1 byte 0x0D seperator which accounts for two nibbles");
                    strBuilder.Append("Length in bytes: " + m_isoFieldDefinition.length);
                    break;
            }
            strBuilder.Append("  Length Length: " + m_isoFieldDefinition.lengthLength   + "\n");
            strBuilder.Append("  Length Format: " + m_isoFieldDefinition.lengthFormat   + "\n");
            strBuilder.Append("  Length Coding: " + m_isoFieldDefinition.lengthCoding   + "\n");
            strBuilder.Append(" Length Padding: " + m_isoFieldDefinition.lengthPadding  + "\n");
            strBuilder.Append(" Content Format: " + m_isoFieldDefinition.content        + "\n");
            strBuilder.Append(" Content Coding: " + m_isoFieldDefinition.contentCoding  + "\n");
            strBuilder.Append("Content Padding: " + m_isoFieldDefinition.contentPadding + "\n");
            strBuilder.Append("    Description: " + m_isoFieldDefinition.description    + "\n");

            return strBuilder.ToString();
        }

        public override void Trace()
        {
            if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("ISOFieldPackager Definition:");
            if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace(        "   Field Number: " + m_number);
            if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace(        "           Name: " + m_isoFieldDefinition.name);

            switch (m_isoFieldDefinition.contentCoding)
            {
                case ISOFieldCoding.ASCII:
                    if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("         Length: " + m_isoFieldDefinition.length + " ASCII characters");
                    if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("Length in bytes: " + m_isoFieldDefinition.length);
                    break;

                case ISOFieldCoding.BCD:
                    if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("         Length: " + m_isoFieldDefinition.length + " Nibbles (half bytes)");
                    if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("Length in bytes: " + (float) m_isoFieldDefinition.length/2);
                    break;

                case ISOFieldCoding.BIN:
                    if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("         Length: " + m_isoFieldDefinition.length + " Hexadecimal Digits");
                    if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("Length in bytes: " + m_isoFieldDefinition.length/2);
                    break;

                case ISOFieldCoding.BCDU:
                    if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("         Length: " + m_isoFieldDefinition.length);
                    if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("Length in bytes: " + m_isoFieldDefinition.length);
                    break;

                case ISOFieldCoding.EBCDIC:
                    if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("         Length: " + m_isoFieldDefinition.length + " EBCDIC characters");
                    if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("Length in bytes: " + m_isoFieldDefinition.length);
                    break;

                case ISOFieldCoding.Z:
                    if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("         Length: " + m_isoFieldDefinition.length + " Nibbles (plus 1 byte 0x0D seperator which accounts for two nibbles");
                    if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("Length in bytes: " + m_isoFieldDefinition.length);
                    break;
            }

            if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("  Length Length: " + m_isoFieldDefinition.lengthLength.ToString());
            if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("  Length Format: " + m_isoFieldDefinition.lengthFormat);
            if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("  Length Coding: " + m_isoFieldDefinition.lengthCoding);
            if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace(" Length Padding: " + m_isoFieldDefinition.lengthPadding);
            if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace(" Content Format: " + m_isoFieldDefinition.content);
            if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace(" Content Coding: " + m_isoFieldDefinition.contentCoding);
            if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("Content Padding: " + m_isoFieldDefinition.contentPadding);
            if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("    Description: " + m_isoFieldDefinition.description);
        }
    }
}

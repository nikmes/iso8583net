namespace ISO8583Net.Types
{
    /// <summary>
    /// ISO field participation in the an ISO message type
    /// </summary>
    public enum ISOFieldParticipation
    {
        /// <summary>ISO field is mandatory, must exist in the message type</summary>
        MAN,
        /// <summary>ISO field is optional, may exist in the message type</summary>
        OPT,
        /// <summary>ISO field is conditional, must exists in the message type if some conditions are met</summary>
        CON
    }

    /// <summary>
    /// Indicates the length format used for the iso field. ISO Fields can have a fixed length format or a variable length format. When
    /// a variable length format is used, it means that some length indicator which indicates the length of the iso field preceeds the 
    /// field data.
    /// </summary>
    public enum ISOFieldLengthFormat
    {
        /// <summary>ISO field has fixed length type. There is no field data length indicator</summary>
        FIXED,
        /// <summary>ISO field has varialble length type. Field data are preceeded by length indicator</summary>
        VAR,
    }

    /// <summary>Defines the encoding of the ISO field value</summary>
    public enum ISOFieldCoding
    {
        /// <summary>ISO field data are encoded in ASCII</summary>       
        ASCII,
        /// <summary>ISO field data are encoded in BCD</summary>        
        BCD,
        /// <summary>ISO field data are encoded in BCDU (BCD Unpacked)</summary>        
        BCDU,
        /// <summary>ISO field data are encoded in EBCDIC</summary>        
        EBCDIC,
        /// <summary>ISO field data are encoded in BIN (Binary)</summary>        
        BIN,
        /// <summary>ISO field data are encoded in Z (Track2 Encoding)</summary>       
        Z
    }

    /// <summary>ISO field data paddding method.</summary>
    public enum ISOFieldPadding
    {
        /// <summary>ISO field data are left padded</summary>
        LEFT,
        /// <summary>ISO field data are right padded</summary>
        RIGHT,
        /// <summary>ISO field data are not padded</summary>
        NONE
    }

    /// <summary>Defines the data content type</summary>
    public enum ISOFieldContent
    {
        /// <summary>Data consist only of alphabetic characters</summary>
        A,
        /// <summary>Data consist only of numeric characters (digits)</summary>
        N,
        /// <summary>Data consist only of alphabetic and numeric characters</summary>
        AN,
        /// <summary>Data consist only of alphabetic, numeric and special characters</summary>
        ANS,
        /// <summary>Numeric (amount) values, where the first byte is either 'C' to indicate a positive or Credit value, or 'D' to indicate a negative or Debit value, followed by the numeric value (using n digits)</summary>
        XN,
        /// <summary>Special Characters only</summary>
        S,
        /// <summary>Data consist only of numeric and special characters</summary>
        NS,
        /// <summary>Data consist only of alphabetic and special characters</summary>
        AS,
        /// <summary>Data consist only of hexadecimal valid characters and digits (ABCDEF0123456789) only</summary>
        HD,
        /// <summary>Tracks 2 and 3 code set as defined in ISO/IEC 7813 and ISO/IEC 4909 respectively</summary>
        Z
    }
}
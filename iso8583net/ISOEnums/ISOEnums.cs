namespace ISO8583Net
{
    /// <summary>
    /// Indicates an ISO field participation in a message type.
    /// </summary>
    public enum ISOFieldParticipation
    {
        /// <summary>Mandatory, the field mast exist in the message type</summary>
        MAN,
        /// <summary>Optional, the field may exist in the message type</summary>
        OPT,
        /// <summary>Conditional, the field mast exists in the message type if some conditions are met</summary>
        CON
    }

    /// <summary>
    /// Indicates the length format used for the iso field. ISO Fields can have a fixed length format or a variable length format. When
    /// a variable length format is used, it means that some length indicator which indicates the length of the iso field preceeds the 
    /// field data.
    /// </summary>
    public enum ISOFieldLengthFormat
    {
        /// <summary>The field is of fixed length type. There is no length indicator</summary>
        FIXED,
        /// <summary>The field is of varialble length type. There is a length indicator before the data</summary>
        VAR,
    }

    /// <summary>Defines the encoding of the ISO field value</summary>
    public enum ISOFieldCoding
    {
        /// <summary>ISO field value is encoded in ASCII</summary>       
        ASCII,
        /// <summary>ISO field value is encoded in BCD</summary>        
        BCD,
        /// <summary>ISO field value is encoded in BCDU (BCD Unpacked)</summary>        
        BCDU,
        /// <summary>ISO field value is encoded in EBCDIC</summary>        
        EBCDIC,
        /// <summary>ISO field value is encoded in BIN (Binary)</summary>        
        BIN,
        /// <summary>ISO field value is encoded in Z (Track2 Encoding)</summary>       
        Z
    }

    /// <summary>Defines the padding of the ISO field value</summary>
    public enum ISOFieldPadding
    {
        /// <summary>Field is using left padding</summary>
        LEFT,
        /// <summary>Field is using right padding</summary>
        RIGHT,
        /// <summary>Field is using no padding</summary>
        NONE
    }

    /// <summary>Defines the data content type</summary>
    public enum ISOFieldContent
    {
        /// <summary>Data consist only of alphabetic characters</summary>
        A,
        /// <summary>Data consist only of alphabetic and numeric characters</summary>
        AN,
        /// <summary>Data consist only of alphabetic characters</summary>
        ANP,
        /// <summary>Data consist only of alphabetic, numeric and special characters</summary>
        ANS,
        /// <summary>Data consist only of alphabetic and special characters</summary>
        AS,
        /// <summary>Data consist only of hexadecimal digits</summary>
        HD,
        /// <summary>Data consist only of numeric digits</summary>
        N,
        /// <summary>Data consist only of numeric digits and special characters</summary>
        NS,
        /// <summary>Data consist only of valid TRACK2 characters</summary>
        TRACK2
    }
}
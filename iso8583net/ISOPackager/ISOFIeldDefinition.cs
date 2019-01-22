using ISO8583Net.Types;
using System;

namespace ISO8583Net.Packager
{
    public struct ISOFieldDefinition
    {
        /// <value>The length format indicator</value>
        public ISOFieldLengthFormat lengthFormat { get; set; }

        /// <value>The length format indicator</value>
        public ISOFieldCoding lengthCoding { get; set; }

        /// <value>The length format indicator</value>
        public ISOFieldPadding lengthPadding { get; set; }

        /// <value>The length format indicator</value>
        public ISOFieldContent content { get; set; }

        /// <value>The field content format</value>
        public ISOFieldCoding contentCoding { get; set; }

        /// <value>The content padding alignment</value>
        public ISOFieldPadding contentPadding { get; set; }

        /// <value>The field description</value>
        public String description { get; set; }

        /// <value>The field name</value>
        public String name { get; set; }

        /// <value>The units used for length indicator</value>
        public int lengthLength { get; set; }

        /// <value>The max lngth of field data</value>
        public int length { get; set; }
    }
}

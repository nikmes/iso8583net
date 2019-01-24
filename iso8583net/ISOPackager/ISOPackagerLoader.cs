using ISO8583Net.Interpreter;
using ISO8583Net.Types;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace ISO8583Net.Packager
{
    public class ISOPackagerLoader
    {
        private int m_totalFields = 0;

        private readonly ILogger _logger;

        internal ILogger Logger { get { return _logger; } }

        public ISOPackagerLoader(ILogger logger, string fileName, ref ISOMessageFieldsPackager msgFieldPackager)
        {
            _logger = logger;

            XmlReader reader = null;

            ISOMessageTypesPackager isoMessageTypesPackager = null;

            if (File.Exists(fileName))
            {
                if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("Loading packager definition from [" + fileName + "]");

                reader = XmlReader.Create(fileName);
            }
            else
            {
                Logger.LogError(String.Format("Filename [{0}] dose not exist",fileName));

                throw new Exception(String.Format("Filename[{0}] dose not exist", fileName));
            }

            while (reader.Read())
            {
                if (reader.IsStartElement())
                {
                    switch (reader.Name)
                    {
                        case "isopackager":
                            string attribute = reader["totalfields"];
                            m_totalFields = Int32.Parse(attribute);
                            m_totalFields += 1;
                            break;

                        case "messages":
                            isoMessageTypesPackager = new ISOMessageTypesPackager(Logger, m_totalFields);
                            isoMessageTypesPackager = LoadMessageTypes(reader);
                            break;

                        case "isofields":
                            msgFieldPackager = LoadISOMessageFieldsPackager(reader,0); 
                            msgFieldPackager.SetMessageTypesPackager(isoMessageTypesPackager);                           
                            msgFieldPackager.SetStorageClass(Type.GetType("ISO8583Net.ISOMessageFields"));
                            break;
                    }
                }
            }
        }

        public ISOPackagerLoader(ILogger logger, ref ISOMessageFieldsPackager msgFieldPackager)
        {
            _logger = logger;

            XmlReader reader = null;

            ISOMessageTypesPackager isoMessageTypesPackager = null;

            if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("Loading packager definition from build-in resource");

            // load from embeded resource visa.xml

            Stream stream = typeof(ISOPackagerLoader).GetTypeInfo().Assembly.GetManifestResourceStream("iso8583net.Resources.visa.xml");

            reader = XmlReader.Create(stream);

            while (reader.Read())
            {
                if (reader.IsStartElement())
                {
                    switch (reader.Name)
                    {
                        case "isopackager":
                            string attribute = reader["totalfields"];
                            m_totalFields = Int32.Parse(attribute);
                            m_totalFields += 1;
                            break;

                        case "messages":
                            isoMessageTypesPackager = new ISOMessageTypesPackager(Logger, m_totalFields);
                            isoMessageTypesPackager = LoadMessageTypes(reader);
                            break;

                        case "isofields":
                            msgFieldPackager = LoadISOMessageFieldsPackager(reader, 0);
                            msgFieldPackager.SetMessageTypesPackager(isoMessageTypesPackager);
                            msgFieldPackager.SetStorageClass(Type.GetType("ISO8583Net.ISOMessageFields"));
                            break;
                    }
                }
            }
        }

        private ISOMessageTypesPackager LoadMessageTypes(XmlReader reader)
        {
            ISOMessageTypesPackager msgTypesPackager = new ISOMessageTypesPackager(Logger, m_totalFields);

            if (reader.ReadToDescendant("message"))
            {
                do
                {
                    ISOMsgTypePackager msgTypeDefinition = new ISOMsgTypePackager(Logger, m_totalFields);

                    // Search for the attribute name on this current node.

                    string attribute = reader["type"];

                    if (attribute != null)
                    {
                        msgTypeDefinition.messageTypeIdentifier = attribute;
                    }

                    attribute = reader["name"];

                    if (attribute != null)
                    {
                        msgTypeDefinition.messageTypeName = attribute;
                    }

                    attribute = reader["desc"];

                    if (attribute != null)
                    {
                        msgTypeDefinition.messageTypeDescription = attribute;
                    }

                    // read the rest of attributes from 0 to total_dialect fields

                    for (int field = 0; field <= m_totalFields; ++field)
                    {
                        string attributeName = "f" + field.ToString("D3");

                        attribute = reader[attributeName];

                        switch (attribute)
                        {
                            case "C":
                                msgTypeDefinition.m_conBitmap.SetBit(field);
                                break;

                            case "M":
                                msgTypeDefinition.m_manBitmap.SetBit(field);
                                break;

                            case "O":
                                msgTypeDefinition.m_optBitmap.SetBit(field); 
                                break;

                            default:
                                msgTypeDefinition.m_manBitmap.SetBit(field);

                                msgTypeDefinition.m_optBitmap.SetBit(field);
                                break;
                        }
                    }

                    msgTypesPackager.Add(msgTypeDefinition.messageTypeIdentifier, msgTypeDefinition);

                } while (reader.ReadToNextSibling("message"));
            }

            return msgTypesPackager;
        }

        private ISOFieldPackager LoadISOFieldPackager(XmlReader reader)
        {
            ISOFieldDefinition fieldDefinition = new ISOFieldDefinition();

            ISOFieldPackager fieldPackager = new ISOFieldPackager(Logger);

            // Search for the attribute name on this current node.

            String attribute = reader["number"];

            if (attribute != null)
            {
                fieldPackager.SetFieldNumber(Int32.Parse(attribute));
            }

            attribute = reader["name"];

            if (attribute != null)
            {
                fieldDefinition.name = attribute;
            }

            attribute = reader["length"];

            if (attribute != null)
            {
                fieldDefinition.length = Int32.Parse(attribute);
            }

            attribute = reader["lengthlength"];

            if (attribute != null)
            {
                fieldDefinition.lengthLength = Int32.Parse(attribute);
            }

            attribute = reader["lengthformat"];

            if (attribute != null)
            {
                switch (attribute)
                {
                    case "FIXED":
                        fieldDefinition.lengthFormat = ISOFieldLengthFormat.FIXED;
                        break;

                    case "VAR":
                        fieldDefinition.lengthFormat = ISOFieldLengthFormat.VAR;
                        break;
                }
            }

            attribute = reader["lengthcoding"];

            if (attribute != null)
            {
                switch (attribute)
                {
                    case "ASCII":
                        fieldDefinition.lengthCoding = ISOFieldCoding.ASCII;
                        break;

                    case "BCD":
                        fieldDefinition.lengthCoding = ISOFieldCoding.BCD;
                        break;

                    case "BCDU":
                        fieldDefinition.lengthCoding = ISOFieldCoding.BCDU;
                        break;

                    case "EBCDIC":
                        fieldDefinition.lengthCoding = ISOFieldCoding.EBCDIC;
                        break;

                    case "BIN":
                        fieldDefinition.lengthCoding = ISOFieldCoding.BIN;
                        break;
                }
            }

            attribute = reader["lengthpadding"];

            if (attribute != null)
            {
                switch (attribute)
                {
                    case "LEFT":
                        fieldDefinition.lengthPadding = ISOFieldPadding.LEFT;
                        break;

                    case "RIGHT":
                        fieldDefinition.lengthPadding = ISOFieldPadding.RIGHT;
                        break;

                    case "NONE":
                        fieldDefinition.lengthPadding = ISOFieldPadding.NONE;
                        break;
                }
            }

            attribute = reader["contentformat"];

            if (attribute != null)
            {
                switch (attribute)
                {
                    case "A":
                        fieldDefinition.content = ISOFieldContent.A;
                        break;

                    case "AN":
                        fieldDefinition.content = ISOFieldContent.AN;
                        break;

                    case "ANS":
                        fieldDefinition.content = ISOFieldContent.ANS;
                        break;

                    case "AS":
                        fieldDefinition.content = ISOFieldContent.AS;
                        break;

                    case "N":
                        fieldDefinition.content = ISOFieldContent.N;
                        break;

                    case "NS":
                        fieldDefinition.content = ISOFieldContent.NS;
                        break;

                    case "HD":
                        fieldDefinition.content = ISOFieldContent.HD;
                        break;

                    case "TRACK2":
                        fieldDefinition.content = ISOFieldContent.Z;
                        break;

                    case "Z":
                        fieldDefinition.content = ISOFieldContent.Z;
                        break;
                }
            }

            attribute = reader["contentcoding"];

            if (attribute != null)
            {
                switch (attribute)
                {
                    case "ASCII":
                        fieldDefinition.contentCoding = ISOFieldCoding.ASCII;
                        break;

                    case "BCD":
                        fieldDefinition.contentCoding = ISOFieldCoding.BCD;
                        // Always N the content since nothing else is possible
                        fieldDefinition.content = ISOFieldContent.N; 
                        break;

                    case "BCDU":
                        fieldDefinition.contentCoding = ISOFieldCoding.BCDU;
                        // Always N the content since nothing else is possible
                        fieldDefinition.content = ISOFieldContent.N;
                        break;

                    case "EBCDIC":
                        fieldDefinition.contentCoding = ISOFieldCoding.EBCDIC;
                        break;

                    case "BIN":
                        // Always HD the content since nothing else is possible
                        fieldDefinition.content = ISOFieldContent.HD;
                        fieldDefinition.contentCoding = ISOFieldCoding.BIN;
                        break;

                    case "Z":
                        fieldDefinition.contentCoding = ISOFieldCoding.Z;
                        break;

                }
            }

            attribute = reader["contentpadding"];

            if (attribute != null)
            {
                switch (attribute)
                {
                    case "LEFT":
                        fieldDefinition.contentPadding = ISOFieldPadding.LEFT;    
                        break;

                    case "RIGHT":
                        fieldDefinition.contentPadding = ISOFieldPadding.RIGHT;
                        break;

                    case "NONE":
                        fieldDefinition.contentPadding = ISOFieldPadding.NONE;
                        break;
                }
            }

            attribute = reader["desc"];

            if (attribute != null)
            {
                fieldDefinition.description = attribute;
            }

            fieldPackager.SetFieldDefinition(fieldDefinition);
            return fieldPackager;
        }

        private ISOMessageFieldsPackager LoadISOMessageFieldsPackager(XmlReader reader, int fieldNumber)
        {
            ISOMessageFieldsPackager msgFieldPackager = new ISOMessageFieldsPackager(Logger, fieldNumber, m_totalFields); 

            if (reader.ReadToDescendant("isofield"))
            {
                do
                {
                    int fldNumber = int.Parse(reader["number"]);

                    String packager       = reader["packager"];
                    String storageclass   = reader["storageclass"];
                    String iscomposite    = reader["composite"];
                    String isointerpreter = reader["interpreter"];

                    if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("Field Number: " + fldNumber.ToString().PadLeft(3, '0') + " Name: " + reader["name"] + " Description: " + reader["desc"]);
                                                                                                
                    switch (packager)
                    {
                        case "ISOMessageSubFieldsPackager":

                            int totalFields = Int32.Parse(reader["totalfields"]);

                            totalFields += 1;

                            if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("Field Number: " + fldNumber.ToString().PadLeft(3,'0') + " is of [[[<--ISOMessageFieldsPackager-->]]], SubFields follow:");

                            ISOFieldPackager fPackager = LoadISOFieldPackager(reader);

                            ISOMessageSubFieldsPackager newMsgFieldPackager = LoadISOMessageSubFieldsPackager(reader, fldNumber);

                            newMsgFieldPackager.SetISOFieldDefinition(fPackager.GetISOFieldDefinition());

                            newMsgFieldPackager.SetStorageClass(Type.GetType("ISO8583Net.ISOMessageSubFields"));

                            msgFieldPackager.Add(newMsgFieldPackager, newMsgFieldPackager.GetFieldNumber());

                            newMsgFieldPackager.totalFields=totalFields;

                            break;

                        default:

                            ISOFieldPackager fieldPackager = LoadISOFieldPackager(reader);

                            if (storageclass == null)
                            {
                                fieldPackager.SetStorageClass(Type.GetType("ISO8583Net.ISOField"));
                            }
                            else
                            {
                                fieldPackager.SetStorageClass(Type.GetType("ISO8583Net." + storageclass));
                            }

                            if (iscomposite == null || iscomposite=="N" || iscomposite=="n" || iscomposite=="No" || iscomposite=="no")
                            {
                                fieldPackager.SetComposite(false);
                            }
                            else
                            {
                                fieldPackager.SetComposite(true);
                            }

                            switch (isointerpreter)
                            {
                                //case "ISOEMVTagInterpreter":
                                //    fieldPackager.SetISOInterpreter(new ISOEMVTagInterpreter(Logger));
                                //    break;

                                case "ISOIndexedValueInterpreter":
                                    ISOIndexedValueInterpreter isoIndexedValueInterpreter = LoadISOIndexedValueInterpreter(reader);
                                    fieldPackager.SetISOInterpreter(isoIndexedValueInterpreter);
                                    break;

                            }

                            msgFieldPackager.Add(fieldPackager, fieldPackager.GetFieldNumber());

                            break;
                    }
                } while (reader.ReadToNextSibling("isofield"));
            }
            return msgFieldPackager;
        }

        private ISOMessageSubFieldsPackager LoadISOMessageSubFieldsPackager(XmlReader reader, int fieldNumber)
        {
            ISOMessageSubFieldsPackager msgFieldPackager = new ISOMessageSubFieldsPackager(Logger, fieldNumber, m_totalFields);

            if (reader.ReadToDescendant("isofield"))
            {
                do
                {
                    int fldNumber = int.Parse(reader["number"]);

                    String packager = reader["packager"];
                    String storageclass = reader["storageclass"];
                    String iscomposite = reader["composite"];
                    String isointerpreter = reader["interpreter"];

                    if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("Field Number: " + fldNumber.ToString().PadLeft(3, '0') + " Name: " + reader["name"] + " Description: " + reader["desc"]);

                    switch (packager)
                    {
                        case "ISOMessageSubFieldsPackager":

                            int totalFields = Int32.Parse(reader["totalfields"]);

                            totalFields += 1;

                            if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("Field Number: " + fldNumber.ToString().PadLeft(3, '0') + " is of [[[<--ISOMessageFieldsPackager-->]]], SubFields follow:");

                            ISOFieldPackager fPackager = LoadISOFieldPackager(reader);

                            ISOMessageSubFieldsPackager newMsgFieldPackager = LoadISOMessageSubFieldsPackager(reader, fldNumber);

                            newMsgFieldPackager.SetISOFieldDefinition(fPackager.GetISOFieldDefinition());

                            newMsgFieldPackager.SetStorageClass(Type.GetType("ISO8583Net.ISOMessageSubFields"));

                            msgFieldPackager.Add(newMsgFieldPackager, newMsgFieldPackager.GetFieldNumber());

                            newMsgFieldPackager.totalFields=totalFields;

                            break;

                        default:

                            ISOFieldPackager fieldPackager = LoadISOFieldPackager(reader);

                            if (storageclass == null)
                            {
                                fieldPackager.SetStorageClass(Type.GetType("ISO8583Net.ISOField"));
                            }
                            else
                            {
                                fieldPackager.SetStorageClass(Type.GetType("ISO8583Net." + storageclass));
                            }

                            if (iscomposite == null || iscomposite == "N" || iscomposite == "n" || iscomposite == "No" || iscomposite == "no")
                            {
                                fieldPackager.SetComposite(false);
                            }
                            else
                            {
                                fieldPackager.SetComposite(true);
                            }

                            switch (isointerpreter)
                            {
                                //case "ISOEMVTagInterpreter":
                                //    fieldPackager.SetISOInterpreter(new ISOEMVTagInterpreter(Logger));
                                //    break;

                                case "ISOIndexedValueInterpreter":
                                    ISOIndexedValueInterpreter isoIndexedValueInterpreter = LoadISOIndexedValueInterpreter(reader);
                                    fieldPackager.SetISOInterpreter(isoIndexedValueInterpreter);
                                    break;

                            }

                            msgFieldPackager.Add(fieldPackager, fieldPackager.GetFieldNumber());

                            break;
                    }
                } while (reader.ReadToNextSibling("isofield"));
            }
            return msgFieldPackager;
        }

        private ISOIndexedValueInterpreter LoadISOIndexedValueInterpreter(XmlReader reader)
        {
            ISOIndexedValueInterpreter isoIndexedValueInterpreter = new ISOIndexedValueInterpreter(Logger);

            if (reader.ReadToDescendant("interpreter"))
            {
                do
                {
                    String index = reader["index"];
                    String length = reader["length"];
                    String desc = reader["desc"];

                    if (reader.ReadToDescendant("value"))
                    {
                        isoIndexedValueInterpreter.AddIndexLength(Int32.Parse(index), Int32.Parse(length));

                        Dictionary<string, string> dic = new Dictionary<string, string>();

                        dic.Add("", desc);

                        do
                        {
                            String value = reader["value"];
                            desc         = reader["desc"];

                            dic.Add(value, desc);

                        } while (reader.ReadToNextSibling("value"));

                        isoIndexedValueInterpreter.AddIndexValueDescriptionDic(Int32.Parse(index),dic);
                    } 
                } while (reader.ReadToNextSibling("interpreter"));
            }

            return isoIndexedValueInterpreter;
        }
    }
}

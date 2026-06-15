using ISO8583Net.Field;
using ISO8583Net.Interpreter;
using ISO8583Net.Types;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ISO8583Net.Packager
{
    // ═══════════════════════════════════════════════════════════════════════
    //  JSON Dialect DTO Models (System.Text.Json polymorphic deserialization)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Root DTO for an ISO 8583 dialect definition.</summary>
    public class DialectDefinition
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public int TotalFields { get; set; }
        public string HeaderPackager { get; set; }

        [JsonPropertyName("messages")]
        public List<MessageTypeDto> Messages { get; set; } = new();

        [JsonPropertyName("fields")]
        public List<FieldDefinitionDto> Fields { get; set; } = new();
    }

    /// <summary>DTO for a message type (e.g. 0100 Authorization Request).</summary>
    public class MessageTypeDto
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// Field participation flags keyed by "fNNN" (e.g. "f002":"M").
        /// M = mandatory, O = optional, C = conditional.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, object> Participation { get; set; } = new();
    }

    /// <summary>
    /// Polymorphic base for field definitions.
    /// The "$type" discriminator selects the derived type at deserialization time.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(SimpleFieldDto), "simple")]
    [JsonDerivedType(typeof(BitmapFieldDto), "bitmap")]
    [JsonDerivedType(typeof(BitmapSubFieldsDto), "bitmapSubFields")]
    public abstract class FieldDefinitionDto
    {
        public int Number { get; set; }
        public string Name { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ISOFieldLengthFormat LengthFormat { get; set; } = ISOFieldLengthFormat.FIXED;

        public int LengthLength { get; set; }
        public int Length { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ISOFieldCoding LengthCoding { get; set; } = ISOFieldCoding.BIN;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ISOFieldPadding LengthPadding { get; set; } = ISOFieldPadding.LEFT;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ISOFieldContent ContentFormat { get; set; } = ISOFieldContent.N;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ISOFieldCoding ContentCoding { get; set; } = ISOFieldCoding.BCD;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ISOFieldPadding ContentPadding { get; set; } = ISOFieldPadding.LEFT;

        public string Description { get; set; }
        public string StorageClass { get; set; }
        public InterpreterDto Interpreter { get; set; }
    }

    /// <summary>A standard flat ISO field.</summary>
    public class SimpleFieldDto : FieldDefinitionDto { }

    /// <summary>A bitmap field (field 1).</summary>
    public class BitmapFieldDto : FieldDefinitionDto
    {
        public BitmapFieldDto() => StorageClass = "Field.ISOFieldBitmap";
    }

    /// <summary>
    /// A field whose payload is a bitmap followed by indexed sub-fields
    /// (e.g. VISA fields 062, 063, 126).
    /// </summary>
    public class BitmapSubFieldsDto : FieldDefinitionDto
    {
        public int TotalSubFields { get; set; }

        [JsonPropertyName("subFields")]
        public List<FieldDefinitionDto> SubFields { get; set; } = new();
    }

    /// <summary>Indexed-value interpreter configuration.</summary>
    public class InterpreterDto
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public InterpreterType Type { get; set; }

        [JsonPropertyName("indexes")]
        public List<InterpreterIndexDto> Indexes { get; set; } = new();
    }

    public enum InterpreterType { ISOIndexedValueInterpreter }

    public class InterpreterIndexDto
    {
        public int Index { get; set; }
        public int Length { get; set; }
        public string Description { get; set; }
        [JsonPropertyName("values")]
        public List<InterpreterValueDto> Values { get; set; } = new();
    }

    public class InterpreterValueDto
    {
        public string Value { get; set; }
        public string Description { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  DTO → Runtime Object Builder
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Converts deserialized <see cref="DialectDefinition"/> DTOs into runtime packager objects.
    /// </summary>
    public static class DialectBuilder
    {
        /// <summary>
        /// Builds the runtime <see cref="ISOMessageFieldsPackager"/> from a dialect DTO.
        /// </summary>
        public static ISOMessageFieldsPackager Build(
            ILogger logger,
            DialectDefinition dialect,
            out string headerPackagerName)
        {
            int totalFields = dialect.TotalFields + 1;
            headerPackagerName = dialect.HeaderPackager;

            // ── Build message types ──────────────────────────────────────
            var msgTypesPackager = new ISOMessageTypesPackager(logger, totalFields);
            foreach (var mt in dialect.Messages)
            {
                var def = new ISOMsgTypePackager(logger, totalFields)
                {
                    messageTypeIdentifier = mt.Type,
                    messageTypeName = mt.Name,
                    messageTypeDescription = mt.Description
                };

                foreach (var kvp in mt.Participation)
                {
                    if (kvp.Key.StartsWith("f") &&
                        int.TryParse(kvp.Key.AsSpan(1), out int fn) &&
                        fn <= totalFields)
                    {
                        switch (kvp.Value?.ToString()?.ToUpperInvariant())
                        {
                            case "M": def.m_manBitmap.SetBit(fn); break;
                            case "O": def.m_optBitmap.SetBit(fn); break;
                            case "C": def.m_conBitmap.SetBit(fn); break;
                            default:
                                def.m_manBitmap.SetBit(fn);
                                def.m_optBitmap.SetBit(fn);
                                break;
                        }
                    }
                }
                msgTypesPackager.Add(mt.Type, def);
            }

            // ── Build field packagers ────────────────────────────────────
            var fieldsPackager = new ISOMessageFieldsPackager(logger, 0, totalFields);
            fieldsPackager.SetMessageTypesPackager(msgTypesPackager);
            fieldsPackager.SetStorageClass(Type.GetType("ISO8583Net.Field.ISOMessageFields"));
            fieldsPackager.HeaderPackagerName = dialect.HeaderPackager;

            foreach (var fd in dialect.Fields)
                BuildField(logger, fd, fieldsPackager, totalFields);

            return fieldsPackager;
        }

        private static void BuildField(
            ILogger logger, FieldDefinitionDto dto,
            ISOMessageFieldsPackager parent, int totalFields)
        {
            if (dto is BitmapSubFieldsDto bmp)
                BuildBitmapSubFields(logger, bmp, parent);
            else
                BuildSimple(logger, dto, parent);
        }

        private static void BuildSimple(
            ILogger logger, FieldDefinitionDto dto,
            ISOMessageFieldsPackager parent)
        {
            var packager = new ISOFieldPackager(logger);
            packager.SetFieldNumber(dto.Number);
            packager.SetFieldDefinition(ToFieldDefinition(dto));
            packager.SetStorageClass(ResolveType(dto.StorageClass));
            packager.SetComposite(false);

            if (dto.Interpreter != null)
                packager.SetISOInterpreter(BuildInterpreter(logger, dto.Interpreter));

            parent.Add(packager, dto.Number);
        }

        private static void BuildBitmapSubFields(
            ILogger logger, BitmapSubFieldsDto dto,
            ISOMessageFieldsPackager parent)
        {
            int subTotal = dto.TotalSubFields + 1;
            var fd = ToFieldDefinition(dto);

            var composite = new ISOFieldBitmapSubFieldsPackager(logger, dto.Number, subTotal);
            composite.SetISOFieldDefinition(fd);
            composite.SetStorageClass(Type.GetType("ISO8583Net.Field.ISOFieldBitmapSubFields"));
            composite.totalFields = subTotal;

            foreach (var sub in dto.SubFields)
            {
                var subFd = ToFieldDefinition(sub);
                var sp = new ISOFieldPackager(logger);
                sp.SetFieldNumber(sub.Number);
                sp.SetFieldDefinition(subFd);
                sp.SetStorageClass(ResolveType(sub.StorageClass));
                sp.SetComposite(false);

                if (sub.Interpreter != null)
                    sp.SetISOInterpreter(BuildInterpreter(logger, sub.Interpreter));

                composite.Add(sp, sub.Number);
            }

            parent.Add(composite, dto.Number);
        }

        private static ISOFieldDefinition ToFieldDefinition(FieldDefinitionDto dto)
        {
            var fd = new ISOFieldDefinition
            {
                name = dto.Name ?? "",
                length = dto.Length,
                lengthLength = dto.LengthLength,
                lengthFormat = dto.LengthFormat,
                lengthCoding = dto.LengthCoding,
                lengthPadding = dto.LengthPadding,
                content = dto.ContentFormat,
                contentCoding = dto.ContentCoding,
                contentPadding = dto.ContentPadding,
                description = dto.Description ?? ""
            };

            // Encoding-specific overrides (mirrors XML parser behavior)
            switch (dto.ContentCoding)
            {
                case ISOFieldCoding.BCD:
                case ISOFieldCoding.BCDU:
                    fd.content = ISOFieldContent.N;
                    break;
                case ISOFieldCoding.BIN:
                    fd.content = ISOFieldContent.HD;
                    break;
            }

            return fd;
        }

        private static Type ResolveType(string storageClass)
        {
            if (string.IsNullOrEmpty(storageClass))
                return Type.GetType("ISO8583Net.Field.ISOField");
            return Type.GetType("ISO8583Net." + storageClass)
                ?? Type.GetType("ISO8583Net.Field.ISOField");
        }

        private static ISOInterpreter BuildInterpreter(ILogger logger, InterpreterDto dto)
        {
            var interp = new ISOIndexedValueInterpreter(logger);
            foreach (var idx in dto.Indexes)
            {
                interp.AddIndexLength(idx.Index, idx.Length);
                var dic = new Dictionary<string, string> { { "", idx.Description } };
                foreach (var v in idx.Values)
                    dic[v.Value] = v.Description;
                interp.AddIndexValueDescriptionDic(idx.Index, dic);
            }
            return interp;
        }
    }
}

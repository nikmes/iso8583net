using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace ISO8583Net.Packager
{
    /// <summary>
    /// Identifies a dialect that is embedded in the ISO8583Net assembly as a resource.
    /// </summary>
    public enum BuiltInDialect
    {
        /// <summary>VISA BASE I dialect (default).</summary>
        Visa,

        /// <summary>D8 G2B ISO 8583:1993 dialect.</summary>
        D8
    }

    /// <summary>
    /// Loads ISO 8583 dialect definitions from JSON files or embedded resources.
    /// Uses System.Text.Json polymorphic deserialization for one-line loading.
    /// </summary>
    public class ISOPackagerLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly ILogger _logger;
        internal ILogger Logger => _logger;

        /// <summary>Loads a dialect from a JSON file on disk.</summary>
        public ISOPackagerLoader(ILogger logger, string fileName,
            ref ISOMessageFieldsPackager msgFieldPackager)
        {
            _logger = logger;

            if (!File.Exists(fileName))
            {
                Logger.LogError("Dialect file [{FileName}] does not exist", fileName);
                throw new FileNotFoundException(
                    $"Dialect file [{fileName}] does not exist", fileName);
            }

            if (Logger.IsEnabled(LogLevel.Trace))
                Logger.LogTrace("Loading packager definition from [{FileName}]", fileName);

            string json = File.ReadAllText(fileName);
            var dialect = JsonSerializer.Deserialize<DialectDefinition>(json, JsonOptions);
            msgFieldPackager = DialectBuilder.Build(Logger, dialect, out _);
        }

        /// <summary>Loads the default VISA dialect from the embedded JSON resource.</summary>
        public ISOPackagerLoader(ILogger logger,
            ref ISOMessageFieldsPackager msgFieldPackager)
            : this(logger, BuiltInDialect.Visa, ref msgFieldPackager)
        {
        }

        /// <summary>Loads a built-in dialect from the embedded JSON resource.</summary>
        public ISOPackagerLoader(ILogger logger, BuiltInDialect dialect,
            ref ISOMessageFieldsPackager msgFieldPackager)
        {
            _logger = logger;

            string resourceName = GetResourceName(dialect);

            if (Logger.IsEnabled(LogLevel.Trace))
                Logger.LogTrace(
                    "Loading packager definition from built-in resource [{ResourceName}]",
                    resourceName);

            using Stream stream = typeof(ISOPackagerLoader).GetTypeInfo().Assembly
                .GetManifestResourceStream(resourceName);

            if (stream is null)
            {
                Logger.LogError(
                    "Embedded dialect resource [{ResourceName}] was not found", resourceName);
                throw new InvalidOperationException(
                    $"Embedded dialect resource [{resourceName}] was not found");
            }

            var dialectDefinition =
                JsonSerializer.Deserialize<DialectDefinition>(stream, JsonOptions);
            msgFieldPackager = DialectBuilder.Build(Logger, dialectDefinition, out _);
        }

        /// <summary>Resolves the manifest resource name for a built-in dialect.</summary>
        public static string GetResourceName(BuiltInDialect dialect)
        {
            return dialect switch
            {
                BuiltInDialect.Visa => "ISO8583Net.ISODialects.visa.json",
                BuiltInDialect.D8 => "ISO8583Net.ISODialects.d8-iso8583.json",
                _ => throw new ArgumentOutOfRangeException(nameof(dialect), dialect, null)
            };
        }
    }
}

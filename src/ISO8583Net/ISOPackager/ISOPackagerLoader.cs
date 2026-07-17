using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace ISO8583Net.Packager
{
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
        {
            _logger = logger;

            if (Logger.IsEnabled(LogLevel.Trace))
                Logger.LogTrace(
                    "Loading packager definition from built-in resource");

            using Stream stream = typeof(ISOPackagerLoader).GetTypeInfo().Assembly
                .GetManifestResourceStream("ISO8583Net.ISODialects.visa.json");

            var dialect = JsonSerializer.Deserialize<DialectDefinition>(stream, JsonOptions);
            msgFieldPackager = DialectBuilder.Build(Logger, dialect, out _);
        }
    }
}

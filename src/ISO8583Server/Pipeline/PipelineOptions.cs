namespace ISO8583Net.Server.Pipeline;

/// <summary>
/// Configuration for the pipeline architecture.
/// Bound from <c>Iso8583Pipeline</c> section in appsettings.json.
/// </summary>
public sealed class PipelineOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Iso8583Pipeline";

    /// <summary>Number of concurrent parser tasks per connection (default: 1).</summary>
    public int ParserConcurrency { get; set; } = 1;

    /// <summary>Capacity of the raw message channel (reader → parser).</summary>
    public int RawMessageCapacity { get; set; } = 256;

    /// <summary>Capacity of the parsed message channel (parser → dispatcher).</summary>
    public int ParsedMessageCapacity { get; set; } = 512;

    /// <summary>Capacity of the outbound message channel (handlers → writer).</summary>
    public int OutboundMessageCapacity { get; set; } = 256;

    /// <summary>Maximum time to wait for in-flight handlers during graceful shutdown.</summary>
    public int DrainTimeoutSeconds { get; set; } = 30;

    /// <summary>Maximum parser errors before the parser stage pauses (circuit breaker). 0 = disabled.</summary>
    public int MaxParseErrorsBeforePause { get; set; } = 0;

    /// <summary>Cooldown period in seconds after parser circuit breaker trips (default: 5).</summary>
    public int ParserCooldownSeconds { get; set; } = 5;
}

using Microsoft.Extensions.Logging;

namespace ISO8583Net.Simulator.Builders;

/// <summary>
/// Central registry of <see cref="IMessageBuilder"/> instances.
/// Populated from DI via <c>IEnumerable&lt;IMessageBuilder&gt;</c> at construction.
/// Provides MTI-based lookup and advice detection.
/// </summary>
public class MessageBuilderRegistry
{
    private readonly Dictionary<string, IMessageBuilder> _builders;
    private readonly HashSet<string> _adviceMtis;
    private readonly ILogger<MessageBuilderRegistry> _logger;

    public MessageBuilderRegistry(
        IEnumerable<IMessageBuilder> builders,
        ILogger<MessageBuilderRegistry> logger)
    {
        _logger = logger;
        _builders = new Dictionary<string, IMessageBuilder>();
        _adviceMtis = new HashSet<string>();

        foreach (var builder in builders)
        {
            foreach (var mti in builder.SupportedMTIs)
            {
                if (!_builders.TryAdd(mti, builder))
                {
                    _logger.LogWarning("Duplicate MTI registration: {Mti} — builder {Builder} ignored",
                        mti, builder.GetType().Name);
                    continue;
                }

                if (IsAdviceMti(mti))
                {
                    _adviceMtis.Add(mti);
                }

                _logger.LogDebug("Registered builder {Builder} for MTI {Mti}",
                    builder.GetType().Name, mti);
            }
        }
    }

    /// <summary>Get the builder for the given MTI, or null if not found.</summary>
    public IMessageBuilder? GetBuilder(string mti)
    {
        _builders.TryGetValue(mti, out var builder);
        return builder;
    }

    /// <summary>Returns true if the MTI is an advice (ends in "20" and is not 0820).</summary>
    public bool IsAdvice(string mti) => _adviceMtis.Contains(mti);

    /// <summary>All registered MTIs.</summary>
    public IReadOnlyCollection<string> RegisteredMTIs => _builders.Keys;

    private static bool IsAdviceMti(string mti) =>
        mti.EndsWith("20") && mti != "0820";
}

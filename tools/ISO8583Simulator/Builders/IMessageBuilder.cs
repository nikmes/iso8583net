using ISO8583Net.Message;

namespace ISO8583Net.Simulator.Builders;

/// <summary>
/// Contract for a message builder that populates an <see cref="ISOMessage"/>
/// with the correct field values for one or more MTIs.
/// </summary>
public interface IMessageBuilder
{
    /// <summary>Set of MTIs this builder can construct (e.g. "0100").</summary>
    IReadOnlySet<string> SupportedMTIs { get; }

    /// <summary>
    /// Populate <paramref name="message"/> with the correct field values for
    /// the MTI it was initialized with. Caller is responsible for setting F0
    /// (MTI) before calling this method, or the implementation may override it.
    /// </summary>
    void BuildRequest(ISOMessage message);
}

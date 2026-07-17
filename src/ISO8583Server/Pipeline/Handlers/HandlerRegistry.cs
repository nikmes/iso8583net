using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace ISO8583Net.Server.Pipeline.Handlers;

/// <summary>
/// Builds an MTI → handler(s) lookup map at startup from all
/// <see cref="IMessageHandler"/> instances registered in DI.
/// </summary>
public sealed class HandlerRegistry
{
    private readonly Dictionary<string, List<IMessageHandler>> _handlers = new();
    private readonly List<IMessageHandler> _catchAll = new();

    /// <summary>
    /// Build the registry from all registered handlers.
    /// </summary>
    public HandlerRegistry(IEnumerable<IMessageHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            foreach (var mti in handler.SupportedMTIs)
            {
                if (mti == "*")
                {
                    _catchAll.Add(handler);
                }
                else
                {
                    if (!_handlers.TryGetValue(mti, out var list))
                    {
                        list = new List<IMessageHandler>();
                        _handlers[mti] = list;
                    }
                    list.Add(handler);
                }
            }
        }
    }

    /// <summary>
    /// Get all handlers for a given MTI, plus any catch-all handlers.
    /// Returns an empty list if no handlers match.
    /// </summary>
    public IReadOnlyList<IMessageHandler> GetHandlers(string mti)
    {
        var result = new List<IMessageHandler>();

        if (_handlers.TryGetValue(mti, out var specific))
            result.AddRange(specific);

        result.AddRange(_catchAll);

        return result;
    }

    /// <summary>
    /// Total number of registered handlers (including catch-all).
    /// </summary>
    public int HandlerCount => _handlers.Values.Sum(l => l.Count) + _catchAll.Count;
}

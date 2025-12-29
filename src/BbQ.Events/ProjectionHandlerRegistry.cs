using System.Collections.Concurrent;

namespace BbQ.Events;

/// <summary>
/// Registry for tracking registered projection handlers.
/// Used by the projection engine to discover which event types have registered handlers.
/// </summary>
public static class ProjectionHandlerRegistry
{
    private static readonly ConcurrentDictionary<Type, List<Type>> _handlers = new();

    /// <summary>
    /// Registers a projection handler service type for a specific event type.
    /// </summary>
    public static void Register(Type eventType, Type handlerServiceType)
    {
        _handlers.AddOrUpdate(
            eventType,
            _ => new List<Type> { handlerServiceType },
            (_, list) =>
            {
                if (!list.Contains(handlerServiceType))
                {
                    list.Add(handlerServiceType);
                }
                return list;
            });
    }

    /// <summary>
    /// Gets all registered handler service types for a specific event type.
    /// </summary>
    public static List<Type> GetHandlers(Type eventType)
    {
        return _handlers.TryGetValue(eventType, out var handlers) 
            ? new List<Type>(handlers) 
            : new List<Type>();
    }

    /// <summary>
    /// Gets all event types that have registered handlers.
    /// </summary>
    public static IEnumerable<Type> GetEventTypes()
    {
        return _handlers.Keys;
    }

    /// <summary>
    /// Clears all registered handlers (primarily for testing).
    /// </summary>
    public static void Clear()
    {
        _handlers.Clear();
    }
}

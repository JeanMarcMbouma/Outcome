using System.Collections.Concurrent;

namespace BbQ.Events;

/// <summary>
/// Registry for tracking registered projection handlers.
/// Used by the projection engine to discover which event types have registered handlers.
/// </summary>
public static class ProjectionHandlerRegistry
{
    private static readonly ConcurrentDictionary<Type, List<HandlerRegistration>> _handlers = new();

    /// <summary>
    /// Registers a projection handler service type for a specific event type.
    /// </summary>
    public static void Register(Type eventType, Type handlerServiceType, Type concreteType)
    {
        _handlers.AddOrUpdate(
            eventType,
            _ => new List<HandlerRegistration> { new(handlerServiceType, concreteType) },
            (_, list) =>
            {
                if (!list.Any(r => r.HandlerServiceType == handlerServiceType))
                {
                    list.Add(new HandlerRegistration(handlerServiceType, concreteType));
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
            ? handlers.Select(h => h.HandlerServiceType).ToList()
            : new List<Type>();
    }

    /// <summary>
    /// Gets the handler registration (including concrete type) for a handler service type and event type.
    /// </summary>
    public static HandlerRegistration? GetHandlerRegistration(Type eventType, Type handlerServiceType)
    {
        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            return handlers.FirstOrDefault(h => h.HandlerServiceType == handlerServiceType);
        }
        return null;
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

    /// <summary>
    /// Represents a handler registration with both interface and concrete types.
    /// </summary>
    public record HandlerRegistration(Type HandlerServiceType, Type ConcreteType);
}

// Sample demonstrating pub/sub integration with BbQ.Cqrs
using BbQ.Cqrs;
using BbQ.Cqrs.DependencyInjection;
using BbQ.Events.Configuration;
using BbQ.Events.Events;
using BbQ.Outcome;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace BbQ.CQRS.Samples;

/// <summary>
/// Demonstrates the pub/sub functionality integrated with CQRS.
/// This example shows:
/// 1. Publishing events from command handlers
/// 2. Handling events one-by-one with IEventHandler
/// 3. Subscribing to event streams with IEventSubscriber
/// 4. Using streaming handlers to expose event streams
/// </summary>
public class PubSubIntegrationSample
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== BbQ.Cqrs Pub/Sub Integration Sample ===\n");

        // Setup dependency injection
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        // Register the event bus
        services.AddInMemoryEventBus();
        
        // Register mediator
        services.AddBbQMediator();
        
        // Register handlers (would normally be done by source generator)
        services.AddScoped<IRequestHandler<CreateUser, Outcome<PubSubUser>>, CreateUserHandler>();
        services.AddScoped<IEventHandler<UserCreated>, SendWelcomeEmailHandler>();
        services.AddScoped<IEventHandler<UserCreated>, UpdateAnalyticsHandler>();
        services.AddScoped<IEventSubscriber<UserCreated>, UserCreatedStreamSubscriber>();
        services.AddScoped<IStreamHandler<StreamUserEventsQuery, UserCreated>, StreamUserEventsHandler>();
        
        // Register in-memory event store for demo
        services.AddSingleton<IEventStore, InMemoryEventStore>();
        
        using var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();
        
        // Scenario 1: Publish event from command handler
        Console.WriteLine("Scenario 1: Publishing events from command handlers");
        Console.WriteLine("---------------------------------------------------");
        
        var createCommand = new CreateUser(Guid.NewGuid(), "Alice Smith", "alice@example.com");
        var result = await mediator.Send(createCommand);
        
        if (result.IsSuccess)
        {
            Console.WriteLine($"✓ User created: {result.Value.Name}");
            // Event handlers are executed before this line
        }

        Console.WriteLine();
        
        // Scenario 2: Create another user to see multiple event handlers
        Console.WriteLine("Scenario 2: Multiple event handlers processing same event");
        Console.WriteLine("----------------------------------------------------------");
        
        var createCommand2 = new CreateUser(Guid.NewGuid(), "Bob Johnson", "bob@example.com");
        var result2 = await mediator.Send(createCommand2);
        
        if (result2.IsSuccess)
        {
            Console.WriteLine($"✓ User created: {result2.Value.Name}");
            // Event handlers are executed before this line
        }

        Console.WriteLine();
        
        // Scenario 3: Streaming events using IStreamHandler
        Console.WriteLine("Scenario 3: Streaming events from the event store");
        Console.WriteLine("--------------------------------------------------");
        
        var streamQuery = new StreamUserEventsQuery();
        var eventCount = 0;
        
        await foreach (var evt in mediator.Stream(streamQuery))
        {
            eventCount++;
            Console.WriteLine($"  Event {eventCount}: User {evt.Name} created (ID: {evt.Id})");
        }
        
        Console.WriteLine($"✓ Streamed {eventCount} events from the event store");
        Console.WriteLine();
        
        Console.WriteLine("=== Sample Complete ===");
    }
}

// Domain Models
public record PubSubUser(Guid Id, string Name, string Email);

// Commands
public record CreateUser(Guid Id, string Name, string Email) : ICommand<Outcome<PubSubUser>>;

// Events
public record UserCreated(Guid Id, string Name, string Email);

// Queries
public record StreamUserEventsQuery : IStreamQuery<UserCreated>;

// Command Handler (publishes events)
public class CreateUserHandler : IRequestHandler<CreateUser, Outcome<PubSubUser>>
{
    private readonly IEventPublisher _eventPublisher;
    private readonly IEventStore _eventStore;

    public CreateUserHandler(IEventPublisher eventPublisher, IEventStore eventStore)
    {
        _eventPublisher = eventPublisher;
        _eventStore = eventStore;
    }

    public async Task<Outcome<PubSubUser>> Handle(CreateUser command, CancellationToken ct)
    {
        // Domain logic: Create user
        var user = new PubSubUser(command.Id, command.Name, command.Email);
        
        // Create event
        var userCreatedEvent = new UserCreated(user.Id, user.Name, user.Email);
        
        // Store event in event store
        await _eventStore.AppendAsync("users", userCreatedEvent, ct);
        
        // Publish event (triggers all IEventHandler<UserCreated> instances)
        await _eventPublisher.Publish(userCreatedEvent, ct);
        
        return Outcome<PubSubUser>.From(user);
    }
}

// Event Handler: Send welcome email
public class SendWelcomeEmailHandler : IEventHandler<UserCreated>
{
    public Task Handle(UserCreated evt, CancellationToken ct)
    {
        Console.WriteLine($"  📧 Sending welcome email to {evt.Name} ({evt.Email})");
        return Task.CompletedTask;
    }
}

// Event Handler: Update analytics
public class UpdateAnalyticsHandler : IEventHandler<UserCreated>
{
    public Task Handle(UserCreated evt, CancellationToken ct)
    {
        Console.WriteLine($"  📊 Updating analytics for user {evt.Name}");
        return Task.CompletedTask;
    }
}

// Event Subscriber: Forward events to stream
public class UserCreatedStreamSubscriber : IEventSubscriber<UserCreated>
{
    private readonly IEventStore _store;

    public UserCreatedStreamSubscriber(IEventStore store)
    {
        _store = store;
    }

    public IAsyncEnumerable<UserCreated> Subscribe(CancellationToken ct)
    {
        return _store.Subscribe<UserCreated>("users", ct);
    }
}

// Stream Handler: Expose events as stream
public class StreamUserEventsHandler : IStreamHandler<StreamUserEventsQuery, UserCreated>
{
    private readonly IEventStore _eventStore;

    public StreamUserEventsHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public IAsyncEnumerable<UserCreated> Handle(StreamUserEventsQuery request, CancellationToken ct)
    {
        return _eventStore.Subscribe<UserCreated>("users", ct);
    }
}

// Simple in-memory event store for demo purposes
public interface IEventStore
{
    Task AppendAsync<TEvent>(string stream, TEvent @event, CancellationToken ct);
    IAsyncEnumerable<TEvent> Subscribe<TEvent>(string stream, CancellationToken ct);
}

public class InMemoryEventStore : IEventStore
{
    private readonly Dictionary<string, List<object>> _events = new();
    private readonly object _lock = new();

    public Task AppendAsync<TEvent>(string stream, TEvent @event, CancellationToken ct)
    {
        lock (_lock)
        {
            if (!_events.ContainsKey(stream))
            {
                _events[stream] = new List<object>();
            }
            _events[stream].Add(@event!);
        }
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<TEvent> Subscribe<TEvent>(string stream, [EnumeratorCancellation] CancellationToken ct)
    {
        List<object> events;
        lock (_lock)
        {
            if (_events.TryGetValue(stream, out var streamEvents))
            {
                events = streamEvents.ToList();
            }
            else
            {
                events = new List<object>();
            }
        }
        
        foreach (var evt in events.OfType<TEvent>())
        {
            yield return evt;
        }
        
        // In a real implementation, this would continue to yield new events as they arrive
        await Task.CompletedTask;
    }
}

using BbQ.Cqrs;
using BbQ.Cqrs.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Concurrent;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests to verify that event handlers and subscribers work correctly with command handlers.
/// </summary>
[TestFixture]
public class EventIntegrationTests
{
    [Test]
    public async Task EventPublisher_FromCommandHandler_WorksCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddBbQMediator();
        
        // Manually register handlers (would normally be done by source generator)
        services.AddScoped<IEventHandler<UserCreatedEvent>, UserCreatedEventHandler>();
        services.AddScoped<IRequestHandler<CreateUserCommand, Unit>, CreateUserCommandHandler>();

        using var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        var command = new CreateUserCommand("Jane Doe", "jane@example.com");

        // Clear any previous events
        UserCreatedEventHandler.HandledEvents.Clear();

        // Act - send command which should publish an event
        await mediator.Send(command);
        
        // Give event handler time to execute
        await Task.Delay(200);

        // Assert - verify the event was published and handled
        Assert.That(UserCreatedEventHandler.HandledEvents.Count, Is.GreaterThan(0));
        var handledEvent = UserCreatedEventHandler.HandledEvents.First();
        Assert.That(handledEvent.UserName, Is.EqualTo("Jane Doe"));
    }

    [Test]
    public async Task EventSubscriber_ReceivesEventsFromCommandHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        services.AddBbQMediator();
        
        services.AddScoped<IRequestHandler<CreateUserCommand, Unit>, CreateUserCommandHandler>();
        services.AddScoped<IEventSubscriber<UserCreatedEvent>, UserCreatedEventSubscriber>();

        using var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();
        var subscriber = sp.GetRequiredService<IEventSubscriber<UserCreatedEvent>>();

        var receivedEvents = new List<UserCreatedEvent>();
        var cts = new CancellationTokenSource();

        // Start subscription
        var subscriptionTask = Task.Run(async () =>
        {
            await foreach (var evt in subscriber.Subscribe(cts.Token))
            {
                receivedEvents.Add(evt);
                if (receivedEvents.Count >= 2)
                {
                    cts.Cancel();
                    break;
                }
            }
        });

        // Give subscription time to start
        await Task.Delay(100);

        // Act - send commands which publish events
        await mediator.Send(new CreateUserCommand("Alice", "alice@example.com"));
        await mediator.Send(new CreateUserCommand("Bob", "bob@example.com"));

        // Wait for subscription to process
        await Task.WhenAny(subscriptionTask, Task.Delay(5000));

        // Assert
        Assert.That(receivedEvents.Count, Is.EqualTo(2));
        Assert.That(receivedEvents[0].UserName, Is.EqualTo("Alice"));
        Assert.That(receivedEvents[1].UserName, Is.EqualTo("Bob"));
    }
}

// Test event
public record UserCreatedEvent(string UserId, string UserName);

// Test event handler that will be discovered by source generator
public class UserCreatedEventHandler : IEventHandler<UserCreatedEvent>
{
    // Static collection to verify handler was called
    public static readonly ConcurrentBag<UserCreatedEvent> HandledEvents = new();

    public Task Handle(UserCreatedEvent @event, CancellationToken ct = default)
    {
        HandledEvents.Add(@event);
        return Task.CompletedTask;
    }
}

// Test event subscriber that will be discovered by source generator
public class UserCreatedEventSubscriber : IEventSubscriber<UserCreatedEvent>
{
    private readonly IEventBus _eventBus;

    public UserCreatedEventSubscriber(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public IAsyncEnumerable<UserCreatedEvent> Subscribe(CancellationToken ct = default)
    {
        return _eventBus.Subscribe<UserCreatedEvent>(ct);
    }
}

// Test command for integration scenario
public record CreateUserCommand(string Name, string Email) : ICommand<Unit>;

// Test command handler that publishes an event
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Unit>
{
    private readonly IEventPublisher _eventPublisher;

    public CreateUserCommandHandler(IEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher;
    }

    public async Task<Unit> Handle(CreateUserCommand request, CancellationToken ct)
    {
        // Simulate user creation
        var userId = Guid.NewGuid().ToString();
        
        // Publish event
        await _eventPublisher.Publish(new UserCreatedEvent(userId, request.Name), ct);
        
        return Unit.Value;
    }
}

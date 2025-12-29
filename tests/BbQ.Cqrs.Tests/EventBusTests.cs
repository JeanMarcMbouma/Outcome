using BbQ.Cqrs;
using BbQ.Events;
using BbQ.Events.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Collections.Concurrent;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for event bus functionality including publishing, handling, and subscribing.
/// </summary>
[TestFixture]
public class EventBusTests
{
    private ServiceProvider _serviceProvider = null!;
    private IEventBus _eventBus = null!;
    private IEventPublisher _eventPublisher = null!;

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        
        _serviceProvider = services.BuildServiceProvider();
        _eventBus = _serviceProvider.GetRequiredService<IEventBus>();
        _eventPublisher = _serviceProvider.GetRequiredService<IEventPublisher>();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }

    [Test]
    public async Task Publish_WithNoHandlers_DoesNotThrow()
    {
        // Arrange
        var evt = new TestEvent("Test");

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await _eventPublisher.Publish(evt));
    }

    [Test]
    public async Task Publish_WithSingleHandler_InvokesHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        
        var handledEvents = new ConcurrentBag<TestEvent>();
        services.AddSingleton<IEventHandler<TestEvent>>(sp => 
            new TestEventHandler(evt => handledEvents.Add(evt)));

        using var sp = services.BuildServiceProvider();
        var publisher = sp.GetRequiredService<IEventPublisher>();

        var testEvent = new TestEvent("Test Message");

        // Act
        await publisher.Publish(testEvent);

        // Assert
        Assert.That(handledEvents.Count, Is.EqualTo(1));
        Assert.That(handledEvents.First().Message, Is.EqualTo("Test Message"));
    }

    [Test]
    public async Task Publish_WithMultipleHandlers_InvokesAllHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        
        var handledEvents1 = new ConcurrentBag<TestEvent>();
        var handledEvents2 = new ConcurrentBag<TestEvent>();
        
        services.AddSingleton<IEventHandler<TestEvent>>(sp => 
            new TestEventHandler(evt => handledEvents1.Add(evt)));
        services.AddSingleton<IEventHandler<TestEvent>>(sp => 
            new TestEventHandler(evt => handledEvents2.Add(evt)));

        using var sp = services.BuildServiceProvider();
        var publisher = sp.GetRequiredService<IEventPublisher>();

        var testEvent = new TestEvent("Test Message");

        // Act
        await publisher.Publish(testEvent);

        // Assert
        Assert.That(handledEvents1.Count, Is.EqualTo(1));
        Assert.That(handledEvents2.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Subscribe_ReceivesPublishedEvents()
    {
        // Arrange
        var receivedEvents = new List<TestEvent>();
        using var cts = new CancellationTokenSource();
        
        // Start subscription in background
        var subscriptionTask = Task.Run(async () =>
        {
            await foreach (var evt in _eventBus.Subscribe<TestEvent>(cts.Token))
            {
                receivedEvents.Add(evt);
                if (receivedEvents.Count >= 3)
                {
                    cts.Cancel();
                    break;
                }
            }
        });

        // Give subscription time to start
        await Task.Delay(100);

        // Act - publish events
        await _eventPublisher.Publish(new TestEvent("Event 1"));
        await _eventPublisher.Publish(new TestEvent("Event 2"));
        await _eventPublisher.Publish(new TestEvent("Event 3"));

        // Wait for subscription to process
        await Task.WhenAny(subscriptionTask, Task.Delay(5000));

        // Assert
        Assert.That(receivedEvents.Count, Is.EqualTo(3));
        Assert.That(receivedEvents[0].Message, Is.EqualTo("Event 1"));
        Assert.That(receivedEvents[1].Message, Is.EqualTo("Event 2"));
        Assert.That(receivedEvents[2].Message, Is.EqualTo("Event 3"));
    }

    [Test]
    public async Task Subscribe_MultipleSubscribers_EachReceivesAllEvents()
    {
        // Arrange
        var receivedEvents1 = new List<TestEvent>();
        var receivedEvents2 = new List<TestEvent>();
        using var cts = new CancellationTokenSource();
        
        // Start two subscriptions in background
        var subscription1 = Task.Run(async () =>
        {
            try
            {
                await foreach (var evt in _eventBus.Subscribe<TestEvent>(cts.Token))
                {
                    receivedEvents1.Add(evt);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is triggered
            }
        });

        var subscription2 = Task.Run(async () =>
        {
            try
            {
                await foreach (var evt in _eventBus.Subscribe<TestEvent>(cts.Token))
                {
                    receivedEvents2.Add(evt);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is triggered
            }
        });

        // Give subscriptions time to start
        await Task.Delay(100);

        // Act - publish events
        await _eventPublisher.Publish(new TestEvent("Event 1"));
        await _eventPublisher.Publish(new TestEvent("Event 2"));

        // Wait for events to be processed
        await Task.Delay(200);
        cts.Cancel();

        // Wait for subscriptions to complete
        await Task.WhenAll(subscription1, subscription2);

        // Assert
        Assert.That(receivedEvents1.Count, Is.EqualTo(2));
        Assert.That(receivedEvents2.Count, Is.EqualTo(2));
        Assert.That(receivedEvents1[0].Message, Is.EqualTo("Event 1"));
        Assert.That(receivedEvents2[0].Message, Is.EqualTo("Event 1"));
    }

    [Test]
    public async Task Publish_WithHandlersAndSubscribers_BothReceiveEvents()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        
        var handledEvents = new ConcurrentBag<TestEvent>();
        services.AddSingleton<IEventHandler<TestEvent>>(sp => 
            new TestEventHandler(evt => handledEvents.Add(evt)));

        using var sp = services.BuildServiceProvider();
        var publisher = sp.GetRequiredService<IEventPublisher>();
        var eventBus = sp.GetRequiredService<IEventBus>();

        var subscribedEvents = new List<TestEvent>();
        using var cts = new CancellationTokenSource();
        
        // Start subscription
        var subscriptionTask = Task.Run(async () =>
        {
            await foreach (var evt in eventBus.Subscribe<TestEvent>(cts.Token))
            {
                subscribedEvents.Add(evt);
                if (subscribedEvents.Count >= 2)
                {
                    cts.Cancel();
                    break;
                }
            }
        });

        // Give subscription time to start
        await Task.Delay(100);

        // Act
        await publisher.Publish(new TestEvent("Event 1"));
        await publisher.Publish(new TestEvent("Event 2"));

        // Wait for processing
        await Task.WhenAny(subscriptionTask, Task.Delay(5000));

        // Assert
        Assert.That(handledEvents.Count, Is.EqualTo(2));
        Assert.That(subscribedEvents.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task EventBus_ImplementsIEventPublisher()
    {
        // Assert - verify that IEventBus can be used as IEventPublisher
        Assert.That(_eventBus, Is.InstanceOf<IEventPublisher>());
        Assert.That(_eventPublisher, Is.SameAs(_eventBus));
    }

    [Test]
    public async Task Publish_WithHandlerException_DoesNotStopOtherHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInMemoryEventBus();
        
        var successfulEvents = new ConcurrentBag<TestEvent>();
        
        // Handler that throws
        services.AddSingleton<IEventHandler<TestEvent>>(sp => 
            new TestEventHandler(_ => throw new InvalidOperationException("Handler error")));
        
        // Handler that succeeds
        services.AddSingleton<IEventHandler<TestEvent>>(sp => 
            new TestEventHandler(evt => successfulEvents.Add(evt)));

        using var sp = services.BuildServiceProvider();
        var publisher = sp.GetRequiredService<IEventPublisher>();

        var testEvent = new TestEvent("Test Message");

        // Act
        await publisher.Publish(testEvent);

        // Assert - the successful handler should still have executed
        Assert.That(successfulEvents.Count, Is.EqualTo(1));
    }

    // Test event types
    public record TestEvent(string Message);

    // Test event handler
    private class TestEventHandler : IEventHandler<TestEvent>
    {
        private readonly Action<TestEvent> _handler;

        public TestEventHandler(Action<TestEvent> handler)
        {
            _handler = handler;
        }

        public Task Handle(TestEvent @event, CancellationToken ct = default)
        {
            _handler(@event);
            return Task.CompletedTask;
        }
    }
}

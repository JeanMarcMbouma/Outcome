using BbQ.Cqrs;
using BbQ.Cqrs.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Cqrs.Tests;

/// <summary>
/// Tests for IMediator's fire-and-forget request handling to verify:
/// - Fire-and-forget requests (IRequest without type parameter) are handled correctly
/// - Handlers are executed without returning meaningful values
/// - Pipeline behaviors work with fire-and-forget requests
/// - Proper integration between Mediator and the fire-and-forget pipeline
/// </summary>
[TestFixture]
public class MediatorFireAndForgetTests
{
    private ServiceProvider _serviceProvider = null!;
    private IMediator _mediator = null!;

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddBbQMediator(typeof(FireAndForgetCommand).Assembly);
        services.AddTransient<IRequestHandler<FireAndForgetCommand>, FireAndForgetCommandHandler>();
        services.AddSingleton<TrackingService>();
        
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }

    [Test]
    public async Task Send_WithFireAndForgetCommand_ExecutesHandler()
    {
        // Arrange
        var trackingService = _serviceProvider.GetRequiredService<TrackingService>();
        var command = new FireAndForgetCommand("test-operation");

        // Act
        await _mediator.Send(command);

        // Assert
        Assert.That(trackingService.ExecutedOperations, Contains.Item("test-operation"));
        Assert.That(trackingService.ExecutionCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Send_WithFireAndForgetCommand_DoesNotThrow()
    {
        // Arrange
        var command = new FireAndForgetCommand("no-throw-test");

        // Act & Assert - should complete without throwing
        Assert.DoesNotThrowAsync(async () => await _mediator.Send(command));
    }

    [Test]
    public async Task Send_WithFireAndForgetCommandAndBehaviors_AppliesBehaviorsInOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBbQMediator(typeof(FireAndForgetCommand).Assembly);
        services.AddTransient<IRequestHandler<FireAndForgetCommand>, FireAndForgetCommandHandler>();
        services.AddSingleton<TrackingService>();
        services.AddTransient<IPipelineBehavior<FireAndForgetCommand, Unit>, FireAndForgetBehavior1>();
        services.AddTransient<IPipelineBehavior<FireAndForgetCommand, Unit>, FireAndForgetBehavior2>();

        using var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();
        var trackingService = sp.GetRequiredService<TrackingService>();

        var command = new FireAndForgetCommand("behavior-test");

        // Act
        await mediator.Send(command);

        // Assert
        // Actual behavior appears to be FIFO (First In, First Out):
        // 1. Behavior1 executes first (first registered) and records "[Behavior1] behavior-test"
        // 2. Behavior2 executes second (last registered) and records "[Behavior2] [Behavior1] behavior-test"  
        // 3. Handler executes last and records "[Behavior2] [Behavior1] behavior-test"
        
        var operationsList = trackingService.ExecutedOperations.ToList();
        Console.WriteLine($"Recorded operations: {string.Join(", ", operationsList.Select(x => $"\"{x}\""))}");
        
        Assert.That(operationsList[0], Is.EqualTo("[Behavior1] behavior-test"), "Behavior1 (first registered) executes and records first");
        Assert.That(operationsList[1], Is.EqualTo("[Behavior2] [Behavior1] behavior-test"), "Behavior2 executes and records second");
        Assert.That(operationsList[2], Is.EqualTo("[Behavior2] [Behavior1] behavior-test"), "Handler records the final command");
        Assert.That(trackingService.ExecutionCount, Is.EqualTo(3), "Should have 3 recorded executions");
    }

    [Test]
    public async Task Send_MultipleFireAndForgetCommands_ExecutesAllHandlers()
    {
        // Arrange
        var trackingService = _serviceProvider.GetRequiredService<TrackingService>();
        var command1 = new FireAndForgetCommand("operation-1");
        var command2 = new FireAndForgetCommand("operation-2");
        var command3 = new FireAndForgetCommand("operation-3");

        // Act
        await _mediator.Send(command1);
        await _mediator.Send(command2);
        await _mediator.Send(command3);

        // Assert
        Assert.That(trackingService.ExecutionCount, Is.EqualTo(3));
        Assert.That(trackingService.ExecutedOperations, Contains.Item("operation-1"));
        Assert.That(trackingService.ExecutedOperations, Contains.Item("operation-2"));
        Assert.That(trackingService.ExecutedOperations, Contains.Item("operation-3"));
    }

    [Test]
    public async Task Send_FireAndForgetCommandWithCancellation_PropagatesCancellationToken()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBbQMediator(typeof(CancellableFireAndForgetCommand).Assembly);
        services.AddTransient<IRequestHandler<CancellableFireAndForgetCommand>, CancellableFireAndForgetHandler>();
        services.AddSingleton<CancellationTracker>();

        using var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();
        var tracker = sp.GetRequiredService<CancellationTracker>();

        var command = new CancellableFireAndForgetCommand();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        try
        {
            await mediator.Send(command, cts.Token);
            Assert.Fail("Expected an exception to be thrown");
        }
        catch (Exception ex)
        {
            // The exception might be wrapped in TargetInvocationException due to reflection
            var actualException = ex is System.Reflection.TargetInvocationException tie ? tie.InnerException : ex;
            
            // Assert
            Assert.That(actualException, Is.InstanceOf<OperationCanceledException>());
            Assert.That(tracker.WasCancellationRequested, Is.True);
        }
    }
}

// Test command and handler implementations
public record FireAndForgetCommand(string OperationName) : IRequest;

public class FireAndForgetCommandHandler : IRequestHandler<FireAndForgetCommand>
{
    private readonly TrackingService _trackingService;

    public FireAndForgetCommandHandler(TrackingService trackingService)
    {
        _trackingService = trackingService;
    }

    public Task Handle(FireAndForgetCommand request, CancellationToken ct)
    {
        _trackingService.RecordExecution(request.OperationName);
        return Task.CompletedTask;
    }
}

public class TrackingService
{
    private readonly List<string> _executedOperations = new();

    public IReadOnlyList<string> ExecutedOperations => _executedOperations.AsReadOnly();
    public int ExecutionCount => _executedOperations.Count;

    public void RecordExecution(string operationName)
    {
        _executedOperations.Add(operationName);
    }
}

public class FireAndForgetBehavior1 : IPipelineBehavior<FireAndForgetCommand, Unit>
{
    private readonly TrackingService _trackingService;

    public FireAndForgetBehavior1(TrackingService trackingService)
    {
        _trackingService = trackingService;
    }

    public async Task<Unit> Handle(
        FireAndForgetCommand request,
        CancellationToken ct,
        Func<FireAndForgetCommand, CancellationToken, Task<Unit>> next)
    {
        _trackingService.RecordExecution($"[Behavior1] {request.OperationName}");
        var modifiedCommand = request with { OperationName = $"[Behavior1] {request.OperationName}" };
        return await next(modifiedCommand, ct);
    }
}

public class FireAndForgetBehavior2 : IPipelineBehavior<FireAndForgetCommand, Unit>
{
    private readonly TrackingService _trackingService;

    public FireAndForgetBehavior2(TrackingService trackingService)
    {
        _trackingService = trackingService;
    }

    public async Task<Unit> Handle(
        FireAndForgetCommand request,
        CancellationToken ct,
        Func<FireAndForgetCommand, CancellationToken, Task<Unit>> next)
    {
        _trackingService.RecordExecution($"[Behavior2] {request.OperationName}");
        var modifiedCommand = request with { OperationName = $"[Behavior2] {request.OperationName}" };
        return await next(modifiedCommand, ct);
    }
}

public record CancellableFireAndForgetCommand : IRequest;

public class CancellableFireAndForgetHandler : IRequestHandler<CancellableFireAndForgetCommand>
{
    private readonly CancellationTracker _tracker;

    public CancellableFireAndForgetHandler(CancellationTracker tracker)
    {
        _tracker = tracker;
    }

    public Task Handle(CancellableFireAndForgetCommand request, CancellationToken ct)
    {
        _tracker.WasCancellationRequested = ct.IsCancellationRequested;
        ct.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

public class CancellationTracker
{
    public bool WasCancellationRequested { get; set; }
}

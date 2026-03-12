using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BbQ.Events.Configuration;
using BbQ.Events.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.Channels;

namespace BbQ.Events.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 8)]
[SimpleJob(RuntimeMoniker.Net90, launchCount: 1, warmupCount: 3, iterationCount: 8)]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 3, iterationCount: 8)]
public class EventBusBenchmarks
{
    private IEventBus _busWithoutHandlers = null!;
    private IEventBus _busWithSingleHandler = null!;
    private IEventBus _busWithActiveSubscriber = null!;
    private IEventBus _busWithTwoActiveSubscribers = null!;

    private readonly Channel<int> _receivedValues = Channel.CreateUnbounded<int>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
    private readonly Channel<int> _receivedValuesSubscriberA = Channel.CreateUnbounded<int>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
    private readonly Channel<int> _receivedValuesSubscriberB = Channel.CreateUnbounded<int>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

    private CancellationTokenSource _subscriberCts = null!;
    private Task _subscriberPumpTask = null!;
    private CancellationTokenSource _twoSubscribersCts = null!;
    private Task _subscriberPumpTaskA = null!;
    private Task _subscriberPumpTaskB = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var noHandlerServices = new ServiceCollection();
        noHandlerServices.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        noHandlerServices.AddInMemoryEventBus();
        _busWithoutHandlers = noHandlerServices.BuildServiceProvider().GetRequiredService<IEventBus>();

        var singleHandlerServices = new ServiceCollection();
        singleHandlerServices.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        singleHandlerServices.AddSingleton<IEventHandler<TestEvent>, NoOpHandler>();
        singleHandlerServices.AddInMemoryEventBus();
        _busWithSingleHandler = singleHandlerServices.BuildServiceProvider().GetRequiredService<IEventBus>();

        var subscriberServices = new ServiceCollection();
        subscriberServices.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        subscriberServices.AddInMemoryEventBus();
        _busWithActiveSubscriber = subscriberServices.BuildServiceProvider().GetRequiredService<IEventBus>();

        var twoSubscribersServices = new ServiceCollection();
        twoSubscribersServices.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        twoSubscribersServices.AddInMemoryEventBus();
        _busWithTwoActiveSubscribers = twoSubscribersServices.BuildServiceProvider().GetRequiredService<IEventBus>();

        _subscriberCts = new CancellationTokenSource();
        _subscriberPumpTask = Task.Run(() => PumpSubscriberAsync(_busWithActiveSubscriber, _receivedValues, _subscriberCts.Token));

        _twoSubscribersCts = new CancellationTokenSource();
        _subscriberPumpTaskA = Task.Run(() => PumpSubscriberAsync(_busWithTwoActiveSubscribers, _receivedValuesSubscriberA, _twoSubscribersCts.Token));
        _subscriberPumpTaskB = Task.Run(() => PumpSubscriberAsync(_busWithTwoActiveSubscribers, _receivedValuesSubscriberB, _twoSubscribersCts.Token));

        await EnsureSubscriberReadyAsync();
        await EnsureTwoSubscribersReadyAsync();
    }

    [Benchmark]
    public Task PublishWithoutHandlers()
    {
        return _busWithoutHandlers.Publish(new TestEvent(Environment.TickCount));
    }

    [Benchmark]
    public Task PublishWithSingleHandler()
    {
        return _busWithSingleHandler.Publish(new TestEvent(Environment.TickCount));
    }

    [Benchmark]
    public async Task<int> PublishWithActiveSubscriber()
    {
        var value = Environment.TickCount;
        await _busWithActiveSubscriber.Publish(new TestEvent(value));
        return await _receivedValues.Reader.ReadAsync();
    }

    [Benchmark]
    public async Task<int> PublishWithTwoActiveSubscribers()
    {
        var value = Environment.TickCount;
        await _busWithTwoActiveSubscribers.Publish(new TestEvent(value));

        var valueA = await _receivedValuesSubscriberA.Reader.ReadAsync();
        var valueB = await _receivedValuesSubscriberB.Reader.ReadAsync();
        return valueA + valueB;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _subscriberCts.Cancel();

        try
        {
            await _subscriberPumpTask;
        }
        catch (OperationCanceledException)
        {
        }

        _subscriberCts.Dispose();

        _twoSubscribersCts.Cancel();

        try
        {
            await Task.WhenAll(_subscriberPumpTaskA, _subscriberPumpTaskB);
        }
        catch (OperationCanceledException)
        {
        }

        _twoSubscribersCts.Dispose();
    }

    private static async Task PumpSubscriberAsync(IEventBus eventBus, Channel<int> outputChannel, CancellationToken ct)
    {
        await foreach (var @event in eventBus.Subscribe<TestEvent>(ct))
        {
            await outputChannel.Writer.WriteAsync(@event.Value, ct);
        }
    }

    private async Task EnsureSubscriberReadyAsync()
    {
        var readinessValue = int.MinValue;

        for (var attempt = 0; attempt < 10; attempt++)
        {
            await _busWithActiveSubscriber.Publish(new TestEvent(readinessValue));

            try
            {
                var value = await _receivedValues.Reader.ReadAsync(_subscriberCts.Token).AsTask().WaitAsync(TimeSpan.FromMilliseconds(200));
                if (value == readinessValue)
                {
                    return;
                }
            }
            catch (TimeoutException)
            {
            }
        }

        throw new InvalidOperationException("Failed to initialize active subscriber benchmark within timeout.");
    }

    private async Task EnsureTwoSubscribersReadyAsync()
    {
        var readinessValue = int.MaxValue;

        for (var attempt = 0; attempt < 10; attempt++)
        {
            await _busWithTwoActiveSubscribers.Publish(new TestEvent(readinessValue));

            try
            {
                var valueA = await _receivedValuesSubscriberA.Reader.ReadAsync(_twoSubscribersCts.Token).AsTask().WaitAsync(TimeSpan.FromMilliseconds(200));
                var valueB = await _receivedValuesSubscriberB.Reader.ReadAsync(_twoSubscribersCts.Token).AsTask().WaitAsync(TimeSpan.FromMilliseconds(200));

                if (valueA == readinessValue && valueB == readinessValue)
                {
                    return;
                }
            }
            catch (TimeoutException)
            {
            }
        }

        throw new InvalidOperationException("Failed to initialize two-subscriber benchmark within timeout.");
    }

    private sealed record TestEvent(int Value);

    private sealed class NoOpHandler : IEventHandler<TestEvent>
    {
        public Task Handle(TestEvent @event, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BbQ.Cqrs;
using BbQ.Cqrs.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BbQ.Cqrs.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 8)]
[SimpleJob(RuntimeMoniker.Net90, launchCount: 1, warmupCount: 3, iterationCount: 8)]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 3, iterationCount: 8)]
public class CqrsDispatchBenchmarks
{
    private ICommandDispatcher _commandDispatcherNoBehavior = null!;
    private ICommandDispatcher _commandDispatcherOneBehavior = null!;
    private IQueryDispatcher _queryDispatcherNoBehavior = null!;
    private IQueryDispatcher _queryDispatcherOneBehavior = null!;
    private IMediator _mediatorNoBehavior = null!;
    private IMediator _mediatorOneBehavior = null!;

    private readonly PingCommand _command = new(42);
    private readonly LookupQuery _query = new(42);
    private static readonly Task<int> CachedResponseTask = Task.FromResult(42);

    [GlobalSetup]
    public void Setup()
    {
        _commandDispatcherNoBehavior = CreateProvider(withBehavior: false).GetRequiredService<ICommandDispatcher>();
        _commandDispatcherOneBehavior = CreateProvider(withBehavior: true).GetRequiredService<ICommandDispatcher>();

        _queryDispatcherNoBehavior = CreateProvider(withBehavior: false).GetRequiredService<IQueryDispatcher>();
        _queryDispatcherOneBehavior = CreateProvider(withBehavior: true).GetRequiredService<IQueryDispatcher>();

        _mediatorNoBehavior = CreateProvider(withBehavior: false).GetRequiredService<IMediator>();
        _mediatorOneBehavior = CreateProvider(withBehavior: true).GetRequiredService<IMediator>();
    }

    [Benchmark]
    public Task<int> CommandDispatch_NoBehavior()
    {
        return _commandDispatcherNoBehavior.Dispatch(_command);
    }

    [Benchmark]
    public Task<int> CommandDispatch_OneBehavior()
    {
        return _commandDispatcherOneBehavior.Dispatch(_command);
    }

    [Benchmark]
    public Task<int> QueryDispatch_NoBehavior()
    {
        return _queryDispatcherNoBehavior.Dispatch(_query);
    }

    [Benchmark]
    public Task<int> QueryDispatch_OneBehavior()
    {
        return _queryDispatcherOneBehavior.Dispatch(_query);
    }

    [Benchmark]
    public Task<int> MediatorSend_Command_NoBehavior()
    {
        return _mediatorNoBehavior.Send(_command);
    }

    [Benchmark]
    public Task<int> MediatorSend_Query_OneBehavior()
    {
        return _mediatorOneBehavior.Send(_query);
    }

    private static ServiceProvider CreateProvider(bool withBehavior)
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddBbQMediator(ServiceLifetime.Singleton, [typeof(CqrsDispatchBenchmarks).Assembly]);

        if (withBehavior)
        {
            services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(NoOpBehavior<,>));
        }

        return services.BuildServiceProvider();
    }

    private sealed record PingCommand(int Value) : ICommand<int>;

    private sealed record LookupQuery(int Value) : IQuery<int>;

    private sealed class PingCommandHandler : IRequestHandler<PingCommand, int>
    {
        public Task<int> Handle(PingCommand request, CancellationToken ct)
        {
            return CachedResponseTask;
        }
    }

    private sealed class LookupQueryHandler : IRequestHandler<LookupQuery, int>
    {
        public Task<int> Handle(LookupQuery request, CancellationToken ct)
        {
            return CachedResponseTask;
        }
    }

    private sealed class NoOpBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> Handle(
            TRequest request,
            CancellationToken ct,
            Func<TRequest, CancellationToken, Task<TResponse>> next)
        {
            return next(request, ct);
        }
    }
}

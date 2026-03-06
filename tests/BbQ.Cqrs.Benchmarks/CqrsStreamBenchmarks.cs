using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BbQ.Cqrs;
using BbQ.Cqrs.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.CompilerServices;

namespace BbQ.Cqrs.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 8)]
public class CqrsStreamBenchmarks
{
    [Params(100, 1000)]
    public int ItemCount { get; set; }

    private IQueryDispatcher _queryDispatcherNoBehavior = null!;
    private IQueryDispatcher _queryDispatcherOneBehavior = null!;

    [GlobalSetup]
    public void Setup()
    {
        _queryDispatcherNoBehavior = CreateProvider(withBehavior: false).GetRequiredService<IQueryDispatcher>();
        _queryDispatcherOneBehavior = CreateProvider(withBehavior: true).GetRequiredService<IQueryDispatcher>();
    }

    [Benchmark]
    public async Task<int> Stream_NoBehavior()
    {
        var total = 0;
        await foreach (var item in _queryDispatcherNoBehavior.Stream(new RangeStreamQuery(ItemCount)))
        {
            total += item;
        }

        return total;
    }

    [Benchmark]
    public async Task<int> Stream_OneBehavior()
    {
        var total = 0;
        await foreach (var item in _queryDispatcherOneBehavior.Stream(new RangeStreamQuery(ItemCount)))
        {
            total += item;
        }

        return total;
    }

    private static ServiceProvider CreateProvider(bool withBehavior)
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddBbQMediator(ServiceLifetime.Singleton, [typeof(CqrsStreamBenchmarks).Assembly]);
        services.AddSingleton<IStreamHandler<RangeStreamQuery, int>, RangeStreamQueryHandler>();

        if (withBehavior)
        {
            services.AddSingleton(typeof(IStreamPipelineBehavior<,>), typeof(NoOpStreamBehavior<,>));
        }

        return services.BuildServiceProvider();
    }

    private sealed record RangeStreamQuery(int Count) : IStreamQuery<int>;

    private sealed class RangeStreamQueryHandler : IStreamHandler<RangeStreamQuery, int>
    {
        public async IAsyncEnumerable<int> Handle(
            RangeStreamQuery request,
            [EnumeratorCancellation] CancellationToken ct)
        {
            for (var i = 0; i < request.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                yield return i;
            }
        }
    }

    private sealed class NoOpStreamBehavior<TRequest, TItem> : IStreamPipelineBehavior<TRequest, TItem>
        where TRequest : IStreamRequest<TItem>
    {
        public async IAsyncEnumerable<TItem> Handle(
            TRequest request,
            [EnumeratorCancellation] CancellationToken ct,
            Func<TRequest, CancellationToken, IAsyncEnumerable<TItem>> next)
        {
            await foreach (var item in next(request, ct).WithCancellation(ct))
            {
                yield return item;
            }
        }
    }
}

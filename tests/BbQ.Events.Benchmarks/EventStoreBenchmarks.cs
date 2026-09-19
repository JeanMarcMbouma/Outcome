using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BbQ.Events.Events;

namespace BbQ.Events.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 8)]
[SimpleJob(RuntimeMoniker.Net90, launchCount: 1, warmupCount: 3, iterationCount: 8)]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 3, iterationCount: 8)]
public class EventStoreBenchmarks
{
    private const int AppendBatchSize = 256;
    [Params(1_000, 10_000)]
    public int EventCount { get; set; }

    private InMemoryEventStore _store = null!;

    [GlobalSetup(Target = nameof(ReadAllFromStart))]
    public async Task SetupRead()
    {
        _store = new InMemoryEventStore();

        for (var i = 0; i < EventCount; i++)
        {
            await _store.AppendAsync("users", new TestEvent(i));
        }
    }

    // Iteration setup keeps retained events bounded. BenchmarkDotNet runs one
    // invocation per iteration when iteration setup is present.
    [IterationSetup(Target = nameof(AppendSingleEvent))]
    public void SetupAppend()
    {
        _store = new InMemoryEventStore();
    }

    [Benchmark(OperationsPerInvoke = AppendBatchSize)]
    public async Task<long> AppendSingleEvent()
    {
        long position = -1;
        for (var i = 0; i < AppendBatchSize; i++)
            position = await _store.AppendAsync("users", new TestEvent(i));
        return position;
    }

    [Benchmark]
    public async Task<int> ReadAllFromStart()
    {
        var count = 0;

        await foreach (var _ in _store.ReadAsync<TestEvent>("users", 0))
        {
            count++;
        }

        return count;
    }

    private sealed record TestEvent(int Id);
}

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BbQ.Events.Events;

namespace BbQ.Events.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 8)]
public class EventStoreBenchmarks
{
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

    [GlobalSetup(Target = nameof(AppendSingleEvent))]
    public void SetupAppend()
    {
        _store = new InMemoryEventStore();
    }

    [Benchmark]
    public Task<long> AppendSingleEvent()
    {
        return _store.AppendAsync("users", new TestEvent(Environment.TickCount));
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

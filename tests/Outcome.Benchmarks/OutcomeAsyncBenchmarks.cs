using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BbQ.Outcome;

namespace Outcome.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 8)]
public class OutcomeAsyncBenchmarks
{
    private static readonly Outcome<int> Success = 42;
    private static readonly Outcome<int> Failure = Outcome<int>.Validation("VAL", "invalid");

    [Benchmark]
    public Task<Outcome<int>> MapAsync_Success()
    {
        return Success.MapAsync(x => Task.FromResult(x + 1));
    }

    [Benchmark]
    public Task<Outcome<int>> MapAsync_Error()
    {
        return Failure.MapAsync(x => Task.FromResult(x + 1));
    }

    [Benchmark]
    public Task<Outcome<int>> BindAsync_Success()
    {
        return Success.BindAsync(x => Task.FromResult(Outcome<int>.From(x + 1)));
    }

    [Benchmark]
    public Task<Outcome<int>> BindAsync_Error()
    {
        return Failure.BindAsync(x => Task.FromResult(Outcome<int>.From(x + 1)));
    }

    [Benchmark]
    public Task<Outcome<IEnumerable<int>>> CombineAsync_AllSuccess()
    {
        Task<Outcome<int>>[] tasks =
        [
            Task.FromResult(Outcome<int>.From(1)),
            Task.FromResult(Outcome<int>.From(2)),
            Task.FromResult(Outcome<int>.From(3))
        ];

        return Outcome<int>.CombineAsync(tasks);
    }

    [Benchmark]
    public Task<Outcome<IEnumerable<int>>> CombineAsync_WithError()
    {
        Task<Outcome<int>>[] tasks =
        [
            Task.FromResult(Outcome<int>.From(1)),
            Task.FromResult(Outcome<int>.Validation("VAL", "bad"))
        ];

        return Outcome<int>.CombineAsync(tasks);
    }
}

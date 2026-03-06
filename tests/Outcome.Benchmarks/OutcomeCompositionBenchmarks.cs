using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BbQ.Outcome;

namespace Outcome.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 8)]
public class OutcomeCompositionBenchmarks
{
    private static readonly Outcome<int> Success = 42;
    private static readonly Outcome<int> Failure = Outcome<int>.Validation("VAL", "invalid");

    [Benchmark]
    public Outcome<int> Map_Success()
    {
        return Success.Map(x => x + 1);
    }

    [Benchmark]
    public Outcome<int> Map_Error()
    {
        return Failure.Map(x => x + 1);
    }

    [Benchmark]
    public Outcome<int> Bind_Success()
    {
        return Success.Bind(x => Outcome<int>.From(x + 1));
    }

    [Benchmark]
    public Outcome<int> Bind_Error()
    {
        return Failure.Bind(x => Outcome<int>.From(x + 1));
    }

    [Benchmark]
    public int Match_Success()
    {
        return Success.Match(v => v, _ => -1);
    }

    [Benchmark]
    public int Match_Error()
    {
        return Failure.Match(v => v, _ => -1);
    }

    [Benchmark]
    public bool HasErrors_Typed()
    {
        return Failure.HasErrors<string>();
    }

    [Benchmark]
    public Error<string>? GetError_Typed()
    {
        return Failure.GetError<string>();
    }

    [Benchmark]
    public Outcome<IEnumerable<int>> Combine_AllSuccess()
    {
        return Outcome<int>.Combine([
            Outcome<int>.From(1),
            Outcome<int>.From(2),
            Outcome<int>.From(3)
        ]);
    }

    [Benchmark]
    public Outcome<IEnumerable<int>> Combine_WithError()
    {
        return Outcome<int>.Combine([
            Outcome<int>.From(1),
            Outcome<int>.Validation("VAL", "bad")
        ]);
    }
}

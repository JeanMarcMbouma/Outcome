using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BbQ.Outcome;

namespace BbQ.Outcome.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 8)]
[SimpleJob(RuntimeMoniker.Net90, launchCount: 1, warmupCount: 3, iterationCount: 8)]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 3, iterationCount: 8)]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class OutcomeTypedVsUntypedBenchmarks
{
    // ---- Outcome<T> (untyped, errors as object?) ----
    private static readonly Outcome<int> UntypedSuccess = 42;
    private static readonly Outcome<int> UntypedFailure = Outcome<int>.Validation("VAL", "invalid");

    // ---- Outcome<T, TError> (typed, errors as Error<string>) ----
    private static readonly Outcome<int, Error<string>> TypedSuccess = Outcome<int, Error<string>>.From(42);
    private static readonly Outcome<int, Error<string>> TypedFailure =
        Outcome<int, Error<string>>.FromError(new Error<string>("VAL", "invalid"));

    // ===================== Creation =====================

    [BenchmarkCategory("Create_Success"), Benchmark(Baseline = true)]
    public Outcome<int> Untyped_Create_Success() => Outcome<int>.From(42);

    [BenchmarkCategory("Create_Success"), Benchmark]
    public Outcome<int, Error<string>> Typed_Create_Success() => Outcome<int, Error<string>>.From(42);

    [BenchmarkCategory("Create_Error"), Benchmark(Baseline = true)]
    public Outcome<int> Untyped_Create_Error() => Outcome<int>.Validation("VAL", "bad");

    [BenchmarkCategory("Create_Error"), Benchmark]
    public Outcome<int, Error<string>> Typed_Create_Error() =>
        Outcome<int, Error<string>>.FromError(new Error<string>("VAL", "bad"));

    // ===================== Map =====================

    [BenchmarkCategory("Map_Success"), Benchmark(Baseline = true)]
    public Outcome<int> Untyped_Map_Success() => UntypedSuccess.Map(x => x + 1);

    [BenchmarkCategory("Map_Success"), Benchmark]
    public Outcome<int, Error<string>> Typed_Map_Success() => TypedSuccess.Map(x => x + 1);

    [BenchmarkCategory("Map_Error"), Benchmark(Baseline = true)]
    public Outcome<int> Untyped_Map_Error() => UntypedFailure.Map(x => x + 1);

    [BenchmarkCategory("Map_Error"), Benchmark]
    public Outcome<int, Error<string>> Typed_Map_Error() => TypedFailure.Map(x => x + 1);

    // ===================== Bind =====================

    [BenchmarkCategory("Bind_Success"), Benchmark(Baseline = true)]
    public Outcome<int> Untyped_Bind_Success() =>
        UntypedSuccess.Bind(x => Outcome<int>.From(x + 1));

    [BenchmarkCategory("Bind_Success"), Benchmark]
    public Outcome<int, Error<string>> Typed_Bind_Success() =>
        TypedSuccess.Bind(x => Outcome<int, Error<string>>.From(x + 1));

    [BenchmarkCategory("Bind_Error"), Benchmark(Baseline = true)]
    public Outcome<int> Untyped_Bind_Error() =>
        UntypedFailure.Bind(x => Outcome<int>.From(x + 1));

    [BenchmarkCategory("Bind_Error"), Benchmark]
    public Outcome<int, Error<string>> Typed_Bind_Error() =>
        TypedFailure.Bind(x => Outcome<int, Error<string>>.From(x + 1));

    // ===================== Match =====================

    [BenchmarkCategory("Match_Success"), Benchmark(Baseline = true)]
    public int Untyped_Match_Success() => UntypedSuccess.Match(v => v, _ => -1);

    [BenchmarkCategory("Match_Success"), Benchmark]
    public int Typed_Match_Success() => TypedSuccess.Match(v => v, _ => -1);

    [BenchmarkCategory("Match_Error"), Benchmark(Baseline = true)]
    public int Untyped_Match_Error() => UntypedFailure.Match(v => v, _ => -1);

    [BenchmarkCategory("Match_Error"), Benchmark]
    public int Typed_Match_Error() => TypedFailure.Match(v => v, _ => -1);

    // ===================== Combine =====================

    [BenchmarkCategory("Combine_AllSuccess"), Benchmark(Baseline = true)]
    public Outcome<IEnumerable<int>> Untyped_Combine_AllSuccess() =>
        Outcome<int>.Combine([
            Outcome<int>.From(1),
            Outcome<int>.From(2),
            Outcome<int>.From(3)
        ]);

    [BenchmarkCategory("Combine_AllSuccess"), Benchmark]
    public Outcome<IEnumerable<int>, Error<string>> Typed_Combine_AllSuccess() =>
        OutcomeTypedExtensions.Combine<int, Error<string>>(
            Outcome<int, Error<string>>.From(1),
            Outcome<int, Error<string>>.From(2),
            Outcome<int, Error<string>>.From(3)
        );

    [BenchmarkCategory("Combine_WithError"), Benchmark(Baseline = true)]
    public Outcome<IEnumerable<int>> Untyped_Combine_WithError() =>
        Outcome<int>.Combine([
            Outcome<int>.From(1),
            Outcome<int>.Validation("VAL", "bad")
        ]);

    [BenchmarkCategory("Combine_WithError"), Benchmark]
    public Outcome<IEnumerable<int>, Error<string>> Typed_Combine_WithError() =>
        OutcomeTypedExtensions.Combine<int, Error<string>>(
            Outcome<int, Error<string>>.From(1),
            Outcome<int, Error<string>>.FromError(new Error<string>("VAL", "bad"))
        );

    // ===================== Chained pipeline =====================

    [BenchmarkCategory("Pipeline"), Benchmark(Baseline = true)]
    public Outcome<string> Untyped_Pipeline()
    {
        return UntypedSuccess
            .Map(x => x * 2)
            .Bind(x => x > 0 ? Outcome<string>.From(x.ToString()) : Outcome<string>.Validation("NEG", "negative"))
            .Map(s => s + "!");
    }

    [BenchmarkCategory("Pipeline"), Benchmark]
    public Outcome<string, Error<string>> Typed_Pipeline()
    {
        return TypedSuccess
            .Map(x => x * 2)
            .Bind(x => x > 0
                ? Outcome<string, Error<string>>.From(x.ToString())
                : Outcome<string, Error<string>>.FromError(new Error<string>("NEG", "negative")))
            .Map(s => s + "!");
    }
}

using BenchmarkDotNet.Attributes;
using BbQ.Outcome;

namespace BbQ.Outcome.Benchmarks;

[MemoryDiagnoser]
public class ExtensionPointsBenchmarks
{
    private Outcome<int, string> _outcome;
    private readonly string[] _errors = ["not_found"];

    [Params(true, false)]
    public bool Success { get; set; }

    [GlobalSetup]
    public void Setup() => _outcome = Success ? Outcome<int, string>.From(42) : Outcome<int, string>.FromErrors(_errors);

    [Benchmark(Baseline = true)]
    public Outcome<int, string> Map() => _outcome.Map(static value => value + 1);

    [Benchmark]
    public Outcome<int, int> MapError() => _outcome.MapError(static error => error.Length);

    [Benchmark]
    public Outcome<int, string> Recover() => _outcome.Recover(static _ => Outcome<int, string>.From(0));

    [Benchmark]
    public Outcome<int, string> Observe() => _outcome.Tap(static _ => { }).TapError(static _ => { });

    [Benchmark]
    public Outcome<int, string> SnapshotExternalErrors() => Outcome<int, string>.FromErrors(_errors);
}

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parity.Before;
using Parity.After;
using Before = Parity.Before;
using After = Parity.After;

namespace ThroughputBenchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ScalarParityBenchmarks
{
    private readonly string[] _errors = ["first", "second", "third"];
    private readonly Before.Outcome<int, string> _before = Before.Outcome<int, string>.FromError("failure");
    private readonly After.Outcome<int, string> _after = After.Outcome<int, string>.FromError("failure");

    [Benchmark(Baseline = true), BenchmarkCategory("CreateSuccess")]
    public Before.Outcome<int, string> BeforeSuccess() => Before.Outcome<int, string>.From(42);
    [Benchmark, BenchmarkCategory("CreateSuccess")]
    public After.Outcome<int, string> AfterSuccess() => After.Outcome<int, string>.From(42);

    [Benchmark(Baseline = true), BenchmarkCategory("CreateSingleError")]
    public Before.Outcome<int, string> BeforeSingle() => Before.Outcome<int, string>.FromError("failure");
    [Benchmark, BenchmarkCategory("CreateSingleError")]
    public After.Outcome<int, string> AfterSingle() => After.Outcome<int, string>.FromError("failure");

    [Benchmark(Baseline = true), BenchmarkCategory("SnapshotMultipleErrors")]
    public Before.Outcome<int, string> BeforeSnapshot() => Before.Outcome<int, string>.FromErrors(_errors);
    [Benchmark, BenchmarkCategory("SnapshotMultipleErrors")]
    public After.Outcome<int, string> AfterSnapshot() => After.Outcome<int, string>.FromErrors(_errors);

    [Benchmark(Baseline = true), BenchmarkCategory("PropagateError")]
    public Before.Outcome<int, string> BeforePropagate() => _before.Map(static value => value + 1);
    [Benchmark, BenchmarkCategory("PropagateError")]
    public After.Outcome<int, string> AfterPropagate() => _after.Map(static value => value + 1);

    [Benchmark(Baseline = true), BenchmarkCategory("ReuseErrorSnapshot")]
    public Before.Outcome<int, string> BeforeReuse() => Before.Outcome<int, string>.FromErrors(_before.Errors);
    [Benchmark, BenchmarkCategory("ReuseErrorSnapshot")]
    public After.Outcome<int, string> AfterReuse() => After.Outcome<int, string>.FromErrors(_after.Errors);
}

public enum InputShape { Success, FailureFirst, FailureLast, AllErrors }

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class CollectionParityBenchmarks
{
    private Before.Outcome<int, string>[] _before = null!;
    private After.Outcome<int, string>[] _after = null!;
    private int[] _indices = null!;
    private Func<int, Before.Outcome<int, string>> _beforeOperation = null!;
    private Func<int, After.Outcome<int, string>> _afterOperation = null!;
    private Func<int, CancellationToken, Task<Before.Outcome<int, string>>> _beforeAsync = null!;
    private Func<int, CancellationToken, Task<After.Outcome<int, string>>> _afterAsync = null!;

    [Params(8, 128)] public int Count { get; set; }
    [Params(InputShape.Success, InputShape.FailureFirst, InputShape.FailureLast, InputShape.AllErrors)]
    public InputShape Shape { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _indices = Enumerable.Range(0, Count).ToArray();
        _before = _indices.Select(i => IsFailure(i) ? Before.Outcome<int, string>.FromError("failure") : Before.Outcome<int, string>.From(i)).ToArray();
        _after = _indices.Select(i => IsFailure(i) ? After.Outcome<int, string>.FromError("failure") : After.Outcome<int, string>.From(i)).ToArray();
        var beforeTasks = _before.Select(Task.FromResult).ToArray();
        var afterTasks = _after.Select(Task.FromResult).ToArray();
        _beforeOperation = i => _before[i];
        _afterOperation = i => _after[i];
        _beforeAsync = (i, _) => beforeTasks[i];
        _afterAsync = (i, _) => afterTasks[i];
    }

    private bool IsFailure(int i) => Shape == InputShape.AllErrors
        || (Shape == InputShape.FailureFirst && i == 0)
        || (Shape == InputShape.FailureLast && i == Count - 1);

    [Benchmark(Baseline = true), BenchmarkCategory("Combine")]
    public Before.Outcome<IEnumerable<int>, string> BeforeCombine() => Before.Outcome<int, string>.Combine(_before);
    [Benchmark, BenchmarkCategory("Combine")]
    public After.Outcome<IEnumerable<int>, string> AfterCombine() => After.Outcome<int, string>.Combine(_after);

    [Benchmark(Baseline = true), BenchmarkCategory("Sequence")]
    public Before.Outcome<IReadOnlyList<int>, string> BeforeSequence() => _before.Sequence();
    [Benchmark, BenchmarkCategory("Sequence")]
    public After.Outcome<IReadOnlyList<int>, string> AfterSequence() => _after.Sequence();

    [Benchmark(Baseline = true), BenchmarkCategory("Traverse")]
    public Before.Outcome<IReadOnlyList<int>, string> BeforeTraverse() => _indices.Traverse(_beforeOperation);
    [Benchmark, BenchmarkCategory("Traverse")]
    public After.Outcome<IReadOnlyList<int>, string> AfterTraverse() => _indices.Traverse(_afterOperation);

    [Benchmark(Baseline = true), BenchmarkCategory("TraverseCompletedAsync")]
    public Task<Before.Outcome<IReadOnlyList<int>, string>> BeforeAsync() => _indices.TraverseAsync(_beforeAsync, maxConcurrency: 8);
    [Benchmark, BenchmarkCategory("TraverseCompletedAsync")]
    public Task<After.Outcome<IReadOnlyList<int>, string>> AfterAsync() => _indices.TraverseAsync(_afterAsync, maxConcurrency: 8);
}

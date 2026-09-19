using BenchmarkDotNet.Attributes;

namespace BbQ.Outcome.Benchmarks;

// Inputs are prepared outside the measurement so these cases isolate library allocations.
[MemoryDiagnoser]
public class OutcomeAllocationBenchmarks
{
    private readonly string[] _externalErrors = ["first", "second", "third"];
    private readonly Outcome<int, string> _failure = Outcome<int, string>.FromError("failure");

    [Benchmark]
    public Outcome<int, string> CreateSuccess() => Outcome<int, string>.From(42);

    [Benchmark]
    public Outcome<int, string> CreateSingleError() => Outcome<int, string>.FromError("failure");

    [Benchmark]
    public Outcome<int, string> SnapshotMultipleErrors() => Outcome<int, string>.FromErrors(_externalErrors);

    [Benchmark]
    public Outcome<int, string> PropagateError() => _failure.Map(static value => value + 1);

    [Benchmark]
    public Outcome<int, string> ReuseErrorSnapshot() => Outcome<int, string>.FromErrors(_failure.Errors);
}

public enum AllocationInputShape { Success, FailureFirst, FailureLast, AllErrors }

[MemoryDiagnoser]
public class OutcomeCollectionAllocationBenchmarks
{
    private Outcome<int, string>[] _outcomes = null!;
    private int[] _indices = null!;
    private Task<Outcome<int, string>>[] _completedTasks = null!;
    private Func<int, Outcome<int, string>> _operation = null!;
    private Func<int, CancellationToken, Task<Outcome<int, string>>> _asyncOperation = null!;

    [Params(8, 128)]
    public int Count { get; set; }

    [Params(AllocationInputShape.Success, AllocationInputShape.FailureFirst,
        AllocationInputShape.FailureLast, AllocationInputShape.AllErrors)]
    public AllocationInputShape Shape { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _indices = Enumerable.Range(0, Count).ToArray();
        _outcomes = _indices.Select(index => IsFailure(index)
            ? Outcome<int, string>.FromError("failure")
            : Outcome<int, string>.From(index)).ToArray();
        _completedTasks = _outcomes.Select(Task.FromResult).ToArray();
        _operation = index => _outcomes[index];
        _asyncOperation = (index, _) => _completedTasks[index];
    }

    private bool IsFailure(int index) => Shape switch
    {
        AllocationInputShape.FailureFirst => index == 0,
        AllocationInputShape.FailureLast => index == Count - 1,
        AllocationInputShape.AllErrors => true,
        _ => false
    };

    [Benchmark]
    public Outcome<IEnumerable<int>, string> Combine() => Outcome<int, string>.Combine(_outcomes);

    [Benchmark]
    public Outcome<IReadOnlyList<int>, string> Sequence() => _outcomes.Sequence();

    [Benchmark]
    public Outcome<IReadOnlyList<int>, string> Traverse() => _indices.Traverse(_operation);

    // Cached completed tasks exclude callback/task creation and isolate traversal overhead.
    [Benchmark]
    public Task<Outcome<IReadOnlyList<int>, string>> TraverseCompletedAsync()
        => _indices.TraverseAsync(_asyncOperation, maxConcurrency: 8);
}

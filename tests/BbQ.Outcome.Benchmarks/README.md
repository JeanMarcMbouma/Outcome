# BbQ.Outcome Benchmarks

BenchmarkDotNet suite for core `BbQ.Outcome` composition paths.

## Coverage

- `OutcomeCompositionBenchmarks`
  - `Map_*`, `Bind_*`, `Match_*`
  - typed error access (`HasErrors`, `GetError`)
  - `Combine` success/error paths
- `OutcomeAsyncBenchmarks`
  - `MapAsync_*`, `BindAsync_*`
  - `CombineAsync` success/error paths
- `OutcomeTypedVsUntypedBenchmarks`
  - Compares `Outcome<T>` (untyped errors) vs `Outcome<T, TError>` (typed errors)
  - `Create_*`, `Map_*`, `Bind_*`, `Match_*`, `Combine_*`, `Pipeline`

## Run

### Allocation baseline

`OutcomeAllocationBenchmarks` isolates success construction, single-error construction,
external error snapshots, and reuse/propagation of existing immutable errors.
`OutcomeCollectionAllocationBenchmarks` measures `Combine`, `Sequence`, `Traverse`,
and completed-task `TraverseAsync` with 8 and 128 inputs, covering all-success,
first/last failure, and all-error inputs.

Inputs, error strings, callbacks, and completed callback tasks are prepared outside
measurement. Collection results are returned to BenchmarkDotNet. `Combine` receives
an existing array, so its numbers exclude the caller's `params` array allocation.
Async cases include traversal's own tasks and buffers, but exclude creating callback
tasks; these cases do not model suspended I/O. Bytes/op means one complete collection
operation, not one input element. All cases use `MemoryDiagnoser` for allocated bytes
and Gen0 collections per 1,000 operations.

Run a focused .NET 10 baseline (these classes have no fixed runtime jobs):

```bash
dotnet run -c Release --framework net10.0 --project tests/BbQ.Outcome.Benchmarks -- --filter '*OutcomeAllocationBenchmarks*' '*OutcomeCollectionAllocationBenchmarks*' --job short --artifacts artifacts/allocation-baseline/core
```

Use the same command and environment before and after each optimization. For a
longer measurement, omit `--job short`; for runtime comparisons add
`--runtimes net8.0 net9.0 net10.0`. Treat short-run timings as diagnostic, not as
performance thresholds. Preserve zero-allocation success and error-propagation
paths, and compare bytes/op for the collection and construction cases.

The first measured results and limitations are recorded in
[the allocation baseline](../../docs/allocation-baseline.md).
The first production changes and before/after results are in
[the allocation optimization report](../../docs/allocation-optimization.md).
The initial [throughput verification](../../docs/throughput-parity.md) found
two regressions, now corrected in the [verified fixes](../../docs/throughput-fixes.md).
See the [paired benchmark harness](../BbQ.Outcome.ThroughputBenchmarks/README.md)
for reproducible checks.

### Full suite

From repository root:

```bash
dotnet run -c Release --framework net10.0 --project tests/BbQ.Outcome.Benchmarks/BbQ.Outcome.Benchmarks.csproj -- --filter * --join
```

## Latest findings by runtime (2026-03-12)

Source report: `BenchmarkDotNet.Artifacts/results/BenchmarkRun-joined-2026-03-12-16-12-47-report.csv`

| Runtime | Benchmarks | Avg Mean (ns) | Min (ns) | Max (ns) |
|---|---:|---:|---:|---:|
| .NET 8.0 | 16 | 57.29 | 1.44 | 311.79 |
| .NET 9.0 | 16 | 57.13 | 2.00 | 330.97 |
| .NET 10.0 | 16 | 44.11 | 2.28 | 210.75 |

- Fastest average runtime: **.NET 10.0**

## `Outcome<T>` vs `Outcome<T, TError>` parity (2026-04-16)

Source report: `BenchmarkDotNet.Artifacts/results/BbQ.Outcome.Benchmarks.OutcomeTypedVsUntypedBenchmarks-report-github.md`

`Outcome<T>` wraps `Outcome<T, object?>` internally. Before optimization, the double indirection plus throw-based validation in `Value`/`Errors` properties prevented JIT inlining on .NET 8, causing `Outcome<T>` Map/Bind to run **2–2.5x slower** than `Outcome<T, TError>`.

After adding internal unchecked accessors and `[AggressiveInlining]` on hot paths, both types now perform at parity:

| Category | .NET 8 | | .NET 9 | | .NET 10 | |
|---|---:|---:|---:|---:|---:|---:|
| | Untyped (ns) | Typed (ns) | Untyped (ns) | Typed (ns) | Untyped (ns) | Typed (ns) |
| Create_Success | 1.71 | 1.68 | 1.97 | 1.97 | 1.60 | 2.01 |
| Create_Error | 13.43 | 13.27 | 12.20 | 11.75 | 14.11 | 14.70 |
| Map_Success | 1.93 | 1.87 | 1.73 | 1.92 | 2.02 | 1.95 |
| Map_Error | 2.24 | 1.51 | 1.83 | 1.71 | 2.28 | 1.90 |
| Bind_Success | 7.17 | 2.00 | 2.48 | 2.24 | 2.29 | 2.26 |
| Bind_Error | 7.27 | 2.84 | 1.92 | 2.30 | 2.52 | 2.45 |
| Match_Success | 0.35 | 0.87 | 0.42 | 0.54 | 0.76 | 0.22 |
| Match_Error | 1.27 | 0.57 | 1.03 | 0.78 | 1.86 | 0.98 |
| Combine_AllSuccess | 42.28 | 59.10 | 39.46 | 44.13 | 41.14 | 51.15 |
| Combine_WithError | 70.63 | 89.02 | 67.45 | 88.00 | 61.02 | 77.86 |
| Pipeline | 16.19 | 15.47 | 15.55 | 14.88 | 12.97 | 14.77 |

**Key takeaway**: Map and Bind on .NET 9/10 are at full parity (~1.0 ratio). .NET 8 Bind still shows some overhead from the wrapper layer but Map is now equal. Combine carries inherent allocation cost that masks the indirection overhead.

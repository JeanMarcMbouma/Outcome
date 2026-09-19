# Allocation optimization — 2026-09-19

Production changes reduce single-error storage and temporary collection allocations.
Initial [throughput verification](throughput-parity.md) found two regressions.
Both are corrected in the [verified follow-up](throughput-fixes.md), which also
reduces successful Combine allocations further. Two earlier inconclusive cases
remain outside that targeted follow-up.
The public API, defensive snapshots of caller-owned errors, input/error ordering,
and bounded async traversal remain intact.

## Changes

- Store a single error directly in an immutable collection object, without an array.
- Share the only failure snapshot across collection composition.
- Pre-size successful collections when the source count is available without enumeration.
- Stop retaining successful values once a failure makes them unusable; still visit every input.
- Index error lists during merging, and transfer newly owned arrays without another snapshot.
- Traverse directly instead of constructing intermediate LINQ selectors.
- Aggregate each completed async batch rather than retaining every Outcome until the end.

## Measurement

Same 37-case .NET 10 ShortRun configuration as the [baseline](allocation-baseline.md).
Inputs and callbacks are prepared outside measurement. Async callbacks return cached
completed tasks; suspended I/O is not modeled. Bytes are per complete operation.
All measured cases allocate the same or fewer bytes than the baseline.
Short-run timing varies substantially; this comparison establishes allocation savings,
not throughput parity. Some timing means increased and warrant isolated longer runs
before making latency claims. Raw reports: `artifacts/allocation-optimized/core/results/`.

| Operation | Inputs | Shape | Before bytes | After bytes | Saved bytes | After Gen0/1,000 ops |
|---|---:|---|---:|---:|---:|---:|
| CreateSuccess | 1 |  | 0 | 0 | 0 | 0.0000 |
| CreateSingleError | 1 |  | 56 | 24 | 32 | 0.0038 |
| SnapshotMultipleErrors | 1 |  | 72 | 72 | 0 | 0.0115 |
| PropagateError | 1 |  | 0 | 0 | 0 | 0.0000 |
| ReuseErrorSnapshot | 1 |  | 0 | 0 | 0 | 0.0000 |
| Combine | 8 | Success | 88 | 88 | 0 | 0.0140 |
| Sequence | 8 | Success | 184 | 112 | 72 | 0.0179 |
| Traverse | 8 | Success | 208 | 112 | 96 | 0.0179 |
| TraverseCompletedAsync | 8 | Success | 1680 | 1456 | 224 | 0.2317 |
| Combine | 8 | FailureFirst | 232 | 0 | 232 | 0.0000 |
| Sequence | 8 | FailureFirst | 336 | 0 | 336 | 0.0000 |
| Traverse | 8 | FailureFirst | 360 | 0 | 360 | 0.0000 |
| TraverseCompletedAsync | 8 | FailureFirst | 1880 | 1344 | 536 | 0.2136 |
| Combine | 8 | FailureLast | 232 | 88 | 144 | 0.0140 |
| Sequence | 8 | FailureLast | 336 | 88 | 248 | 0.0140 |
| Traverse | 8 | FailureLast | 360 | 88 | 272 | 0.0140 |
| TraverseCompletedAsync | 8 | FailureLast | 1880 | 1432 | 448 | 0.2279 |
| Combine | 8 | AllErrors | 288 | 288 | 0 | 0.0459 |
| Sequence | 8 | AllErrors | 608 | 288 | 320 | 0.0459 |
| Traverse | 8 | AllErrors | 632 | 288 | 344 | 0.0459 |
| TraverseCompletedAsync | 8 | AllErrors | 2152 | 1632 | 520 | 0.2594 |
| Combine | 128 | Success | 568 | 568 | 0 | 0.0904 |
| Sequence | 128 | Success | 1240 | 592 | 648 | 0.0942 |
| Traverse | 128 | Success | 1264 | 592 | 672 | 0.0942 |
| TraverseCompletedAsync | 128 | Success | 21192 | 16456 | 4736 | 2.6169 |
| Combine | 128 | FailureFirst | 712 | 0 | 712 | 0.0000 |
| Sequence | 128 | FailureFirst | 1392 | 0 | 1392 | 0.0000 |
| Traverse | 128 | FailureFirst | 1416 | 0 | 1416 | 0.0000 |
| TraverseCompletedAsync | 128 | FailureFirst | 21392 | 15864 | 5528 | 2.5253 |
| Combine | 128 | FailureLast | 712 | 568 | 144 | 0.0904 |
| Sequence | 128 | FailureLast | 1392 | 568 | 824 | 0.0904 |
| Traverse | 128 | FailureLast | 1416 | 568 | 848 | 0.0904 |
| TraverseCompletedAsync | 128 | FailureLast | 21392 | 16432 | 4960 | 2.6169 |
| Combine | 128 | AllErrors | 3264 | 3264 | 0 | 0.5202 |
| Sequence | 128 | AllErrors | 7424 | 3264 | 4160 | 0.5202 |
| Traverse | 128 | AllErrors | 7448 | 3264 | 4184 | 0.5198 |
| TraverseCompletedAsync | 128 | AllErrors | 27424 | 19128 | 8296 | 3.0441 |

## Validation

- Core tests: 163 passed on each of .NET 8, 9, and 10.
- Integration tests: 53 passed on each of .NET 8, 9, and 10.
- Total: 648 passing test executions; all 37 allocation benchmarks completed.
- Regression coverage includes caller mutation, immutable single-error storage,
  shared failure snapshots, default-outcome rejection, merged error ordering,
  traversal across batches, cancellation, and draining started tasks.

Validation commands:

```sh
dotnet test tests/BbQ.Outcome.Tests -c Release --no-restore
dotnet test tests/BbQ.Outcome.Integrations.Tests -c Release --no-restore
dotnet run -c Release --framework net10.0 --no-restore --project tests/BbQ.Outcome.Benchmarks -- --filter '*OutcomeAllocationBenchmarks*' '*OutcomeCollectionAllocationBenchmarks*' --job short --artifacts artifacts/allocation-optimized/core
```

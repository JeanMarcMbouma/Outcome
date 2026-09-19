# Throughput regression fixes — 2026-09-19

Both regressions from the [initial parity check](throughput-parity.md) are fixed.
All 13 targeted comparisons pass the same conservative 99.9% confidence-interval
check: 5% slowdown tolerance, with a 1 ns floor for scalar operations.

## Changes

- Combine writes successful values directly into an exactly sized result array.
  Error aggregation starts at the first failure, preserving snapshot reuse and ordering.
- Snapshot reuse and array copying are separate small inlinable methods. Array inputs
  avoid interface indexing while retaining null validation and defensive copying.

## Verification

Core tests: 166 passed on each of .NET 8, 9, and 10. Integration tests: 53 passed
on each runtime. Total: **657 passing test executions**. Regression checks include
null errors in every array position and successful Combine results remaining
independent of mutations to their input array.

Original sources: `d667dc99723922e29e852378e36fd598965a15fc`.
Final source snapshot: `artifacts/throughput-fix-sources/v3/After/` (hashes recorded).
BenchmarkDotNet 0.15.6, .NET 10.0.12, Release, CPU affinity 1, two launches,
30 warmup and 12 measured iterations per launch, 200 ms target iteration time.
Production code was tested separately from benchmarks to avoid measurement contention.

| Case | Inputs | Original ns | Fixed ns | Original bytes | Fixed bytes | Result |
|---|---|---:|---:|---:|---:|---|
| Combine | Count=8, Shape=Success | 18.642 | 6.241 | 88 | 56 | Pass |
| Combine | Count=8, Shape=FailureFirst | 33.654 | 5.114 | 232 | 0 | Pass |
| Combine | Count=8, Shape=FailureLast | 34.144 | 7.056 | 232 | 56 | Pass |
| Combine | Count=8, Shape=AllErrors | 59.083 | 42.843 | 288 | 288 | Pass |
| Combine | Count=128, Shape=Success | 134.807 | 70.099 | 568 | 536 | Pass |
| Combine | Count=128, Shape=FailureFirst | 178.848 | 52.479 | 712 | 0 | Pass |
| Combine | Count=128, Shape=FailureLast | 157.694 | 75.124 | 712 | 536 | Pass |
| Combine | Count=128, Shape=AllErrors | 636.695 | 421.965 | 3264 | 3264 | Pass |
| Single |  | 4.669 | 2.397 | 56 | 24 | Pass |
| Success |  | 0.981 | 1.011 | 0 | 0 | Pass |
| Propagate |  | 1.582 | 1.510 | 0 | 0 | Pass |
| Reuse |  | 1.233 | 1.484 | 0 | 0 | Pass |
| Snapshot |  | 7.705 | 6.612 | 72 | 72 | Pass |

This targeted follow-up covers all Combine input shapes at 8/128 items and all five
scalar cases. It resolves the previously inconclusive successful Combine case at
128 inputs as well as both confirmed regressions. The earlier inconclusive Sequence
all-error and completed-async all-success cases at 128 inputs were not remeasured;
this report does not claim universal throughput parity. Scalar propagation/reuse
passes use the explicitly stated 1 ns allowance, not strict percentage equivalence.

## Reproduce the preserved final snapshot

```powershell
$env:ParityAfterProject = Join-Path (Get-Location) 'artifacts/throughput-fix-sources/v3/After/After.csproj'
dotnet run -c Release --project tests/BbQ.Outcome.ThroughputBenchmarks -- --filter '*Scalar*' '*Combine*' --launchCount 2 --warmupCount 30 --iterationCount 12 --iterationTime 200 --affinity 1 --exporters json --artifacts artifacts/throughput-parity-fix-final
```

Raw results, including confidence bounds: `artifacts/throughput-parity-fix-final/results/`.
On a fresh checkout, the measurement script can snapshot the current implementation;
the environment override is only needed to remeasure the preserved v3 snapshot.

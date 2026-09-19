# Throughput parity — 2026-09-19

Historical results below. Both reported regressions were subsequently corrected;
see [the targeted fix verification](throughput-fixes.md).

Results: **32 pass, 2 regressions, 3 inconclusive** across 37 comparisons.

Full throughput parity is not established. Allocation savings alone are insufficient to approve the changes as performance-neutral.

## Method

- Before: production sources at `d667dc99723922e29e852378e36fd598965a15fc`.
- After: the optimized working-tree sources, snapshotted before measurement.
- Both snapshots compile with identical Release settings and separate assembly/namespace names.
- .NET 10.0.12, SDK 10.0.401, Windows 11, Intel Core Ultra 7 258V; BenchmarkDotNet 0.15.6.
- One CPU core (affinity mask 1), sequential runs, two process launches per benchmark.
- Each launch: 30 warmup iterations, 12 measured iterations, 200 ms target iteration time.
- Original and optimized cases run consecutively for each workload. Both are prepared by the same harness.
- An initial six-warmup run showed runtime compilation transitions during measurement and was discarded.

## Interpretation

Pass means the optimized upper 99.9% confidence bound is at most the original lower
bound plus a 5% slowdown tolerance. Scalar operations use a minimum 1 ns tolerance
because their durations approach measurement overhead. Regression means the optimized
lower bound exceeds the original upper bound plus that tolerance. Other cases are
inconclusive; similar averages alone do not establish parity.

The 1 ns floor is a practical allowance, not strict percentage equivalence.
Inspect the scalar means even when they pass: a sub-nanosecond slowdown can be
a large relative change. Cases with insufficiently narrow intervals remain
inconclusive rather than being reported as equivalent.

These are conservative comparisons of independent timing intervals, not paired-sample
equivalence tests or a guarantee of application throughput. Results apply to .NET 10,
the tested typed outcomes, and completed async callbacks. Other runtimes, heterogeneous
errors, suspended I/O, Zip, and arbitrary workloads are not covered by this throughput check.

Times and intervals below are nanoseconds per operation. Bytes are managed allocations.

| Operation | Inputs | Before mean [99.9% CI] | After mean [99.9% CI] | Bytes before → after | Result |
|---|---|---:|---:|---:|---|
| Combine | Count=8, Shape=Success | 16.448 [15.951, 16.945] | 21.280 [21.036, 21.523] | 88 → 88 | Regression |
| Combine | Count=8, Shape=FailureFirst | 31.688 [31.535, 31.842] | 6.199 [5.893, 6.505] | 232 → 0 | Pass |
| Combine | Count=8, Shape=FailureLast | 33.452 [32.614, 34.291] | 17.784 [17.480, 18.088] | 232 → 88 | Pass |
| Combine | Count=8, Shape=AllErrors | 56.229 [55.486, 56.972] | 39.595 [38.407, 40.784] | 288 → 288 | Pass |
| Combine | Count=128, Shape=Success | 124.928 [122.111, 127.744] | 130.589 [124.407, 136.771] | 568 → 568 | Inconclusive |
| Combine | Count=128, Shape=FailureFirst | 170.381 [163.685, 177.078] | 49.239 [48.853, 49.624] | 712 → 0 | Pass |
| Combine | Count=128, Shape=FailureLast | 150.099 [147.498, 152.700] | 131.184 [123.932, 138.436] | 712 → 568 | Pass |
| Combine | Count=128, Shape=AllErrors | 598.336 [582.415, 614.257] | 376.714 [367.019, 386.410] | 3264 → 3264 | Pass |
| Sequence | Count=8, Shape=Success | 41.597 [40.568, 42.626] | 22.320 [21.267, 23.372] | 184 → 112 | Pass |
| Sequence | Count=8, Shape=FailureFirst | 67.868 [65.252, 70.484] | 5.537 [5.384, 5.690] | 336 → 0 | Pass |
| Sequence | Count=8, Shape=FailureLast | 64.385 [62.727, 66.044] | 18.825 [18.365, 19.286] | 336 → 88 | Pass |
| Sequence | Count=8, Shape=AllErrors | 135.543 [133.827, 137.260] | 43.202 [42.221, 44.184] | 608 → 288 | Pass |
| Sequence | Count=128, Shape=Success | 326.116 [318.247, 333.985] | 127.349 [119.229, 135.468] | 1240 → 592 | Pass |
| Sequence | Count=128, Shape=FailureFirst | 392.316 [385.802, 398.830] | 84.946 [81.983, 87.908] | 1392 → 0 | Pass |
| Sequence | Count=128, Shape=FailureLast | 440.440 [424.345, 456.535] | 134.920 [128.196, 141.644] | 1392 → 568 | Pass |
| Sequence | Count=128, Shape=AllErrors | 1769.813 [1739.458, 1800.168] | 1756.879 [682.190, 2831.568] | 7424 → 3296 | Inconclusive |
| Traverse | Count=8, Shape=Success | 80.353 [78.214, 82.493] | 24.558 [23.824, 25.291] | 208 → 112 | Pass |
| Traverse | Count=8, Shape=FailureFirst | 99.112 [97.883, 100.340] | 9.448 [9.191, 9.705] | 360 → 0 | Pass |
| Traverse | Count=8, Shape=FailureLast | 97.760 [96.493, 99.027] | 21.862 [21.597, 22.126] | 360 → 88 | Pass |
| Traverse | Count=8, Shape=AllErrors | 178.807 [173.339, 184.276] | 51.573 [50.074, 53.071] | 632 → 288 | Pass |
| Traverse | Count=128, Shape=Success | 1129.814 [1087.283, 1172.345] | 221.832 [216.164, 227.499] | 1264 → 592 | Pass |
| Traverse | Count=128, Shape=FailureFirst | 1120.673 [1066.920, 1174.426] | 119.696 [116.602, 122.790] | 1416 → 0 | Pass |
| Traverse | Count=128, Shape=FailureLast | 1120.531 [1105.362, 1135.700] | 228.182 [224.574, 231.790] | 1416 → 568 | Pass |
| Traverse | Count=128, Shape=AllErrors | 2482.171 [2443.919, 2520.423] | 487.481 [477.810, 497.153] | 7448 → 3264 | Pass |
| TraverseCompletedAsync | Count=8, Shape=Success | 564.143 [544.430, 583.857] | 513.563 [496.318, 530.807] | 1680 → 1456 | Pass |
| TraverseCompletedAsync | Count=8, Shape=FailureFirst | 661.171 [642.576, 679.767] | 530.758 [522.129, 539.387] | 1880 → 1344 | Pass |
| TraverseCompletedAsync | Count=8, Shape=FailureLast | 646.123 [619.055, 673.192] | 534.417 [511.920, 556.914] | 1880 → 1432 | Pass |
| TraverseCompletedAsync | Count=8, Shape=AllErrors | 804.402 [791.713, 817.091] | 548.583 [543.969, 553.198] | 2152 → 1632 | Pass |
| TraverseCompletedAsync | Count=128, Shape=Success | 6233.108 [6144.322, 6321.893] | 6199.313 [5749.737, 6648.889] | 21192 → 16456 | Inconclusive |
| TraverseCompletedAsync | Count=128, Shape=FailureFirst | 7498.030 [7277.503, 7718.558] | 6069.023 [5909.305, 6228.742] | 21392 → 15864 | Pass |
| TraverseCompletedAsync | Count=128, Shape=FailureLast | 7413.097 [7231.036, 7595.158] | 5978.402 [5841.617, 6115.187] | 21392 → 16432 | Pass |
| TraverseCompletedAsync | Count=128, Shape=AllErrors | 9180.423 [8967.381, 9393.465] | 6443.084 [6313.895, 6572.274] | 27424 → 19128 | Pass |
| CreateSingleError |  | 4.555 [4.493, 4.618] | 2.148 [2.088, 2.207] | 56 → 24 | Pass |
| CreateSuccess |  | 0.948 [0.934, 0.963] | 0.956 [0.929, 0.984] | 0 → 0 | Pass |
| PropagateError |  | 1.282 [1.216, 1.348] | 1.891 [1.867, 1.915] | 0 → 0 | Pass |
| ReuseErrorSnapshot |  | 1.200 [1.160, 1.240] | 1.828 [1.805, 1.851] | 0 → 0 | Pass |
| SnapshotMultipleErrors |  | 6.985 [6.810, 7.161] | 16.281 [16.182, 16.380] | 72 → 72 | Regression |

## Reproduction

Run `scripts/Measure-AllocationThroughput.ps1` from a clean snapshot-artifact state.
It preserves source hashes in `artifacts/throughput-sources/source-hashes.json`.
Existing snapshots are intentionally not overwritten. To rerun those snapshots:

```powershell
dotnet run -c Release --project tests/BbQ.Outcome.ThroughputBenchmarks -- --filter '*' --launchCount 2 --warmupCount 30 --iterationCount 12 --iterationTime 200 --affinity 1 --exporters json --artifacts artifacts/throughput-parity-warmed
./scripts/Summarize-ThroughputParity.ps1
```

Raw JSON/CSV reports remain under `artifacts/throughput-parity-warmed/results/` (ignored by Git).

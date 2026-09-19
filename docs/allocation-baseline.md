# Allocation baseline — 2026-09-18

Initial measurement slice for the allocation-reduction work, against production
code at `d667dc9`. This slice changes benchmarks only; it does not reduce production
allocations yet.

## Scope and method

- Core Outcome: 37 cases on .NET 10.0.12, BenchmarkDotNet 0.15.6 ShortRun
  (one launch, three warmup iterations, three measured iterations).
- Event-store append validation: six cases across .NET 8.0.31, 9.0.20, and
  10.0.12 (one launch, one warmup iteration, three measured iterations).
- Host: Windows 11, Intel Core Ultra 7 258V, x64; .NET SDK 10.0.401.
- Core inputs and callbacks are prepared outside measurement. Error construction
  uses existing strings. Collection allocations are per complete operation;
  async callbacks return cached, completed tasks with concurrency set to eight.
- The event-store validation overlapped the start of the core run. These results
  establish managed allocation baselines, not reliable latency comparisons. Run
  benchmarks in isolation with longer jobs before making throughput claims.

Reproduction commands and workload details are in the
[Outcome benchmark README](../tests/BbQ.Outcome.Benchmarks/README.md) and
[Events benchmark README](../tests/BbQ.Events.Benchmarks/README.md).
Raw logs and BenchmarkDotNet reports are under `artifacts/allocation-baseline/`
(ignored by Git); the allocation results are preserved below.

## Scalar Outcome results

Gen0 is collections per 1,000 operations. Zero means BenchmarkDotNet reported no
managed allocation for that warmed path; it excludes setup and first-use costs.

| Operation | Allocated bytes/op | Gen0/1,000 ops |
|---|---:|---:|
| Success construction | 0 | 0 |
| Single-error construction | 56 | 0.0089 |
| Snapshot three external errors | 72 | 0.0115 |
| Map an existing failure | 0 | 0 |
| Reuse an immutable error snapshot | 0 | 0 |

## Collection results

Bytes/op and Gen0/1,000 operations, including the full collection operation.

| Operation | Inputs | Shape | Allocated | Gen0 |
|---|---:|---|---:|---:|
| Combine | 8 | Success | 88 B | 0.0140 |
| Sequence | 8 | Success | 184 B | 0.0293 |
| Traverse | 8 | Success | 208 B | 0.0331 |
| TraverseCompletedAsync | 8 | Success | 1680 B | 0.2670 |
| Combine | 8 | FailureFirst | 232 B | 0.0370 |
| Sequence | 8 | FailureFirst | 336 B | 0.0535 |
| Traverse | 8 | FailureFirst | 360 B | 0.0573 |
| TraverseCompletedAsync | 8 | FailureFirst | 1880 B | 0.2995 |
| Combine | 8 | FailureLast | 232 B | 0.0370 |
| Sequence | 8 | FailureLast | 336 B | 0.0535 |
| Traverse | 8 | FailureLast | 360 B | 0.0573 |
| TraverseCompletedAsync | 8 | FailureLast | 1880 B | 0.2995 |
| Combine | 8 | AllErrors | 288 B | 0.0459 |
| Sequence | 8 | AllErrors | 608 B | 0.0968 |
| Traverse | 8 | AllErrors | 632 B | 0.1006 |
| TraverseCompletedAsync | 8 | AllErrors | 2152 B | 0.3424 |
| Combine | 128 | Success | 568 B | 0.0904 |
| Sequence | 128 | Success | 1240 B | 0.1974 |
| Traverse | 128 | Success | 1264 B | 0.2003 |
| TraverseCompletedAsync | 128 | Success | 21192 B | 3.3722 |
| Combine | 128 | FailureFirst | 712 B | 0.1135 |
| Sequence | 128 | FailureFirst | 1392 B | 0.2217 |
| Traverse | 128 | FailureFirst | 1416 B | 0.2251 |
| TraverseCompletedAsync | 128 | FailureFirst | 21392 B | 3.4027 |
| Combine | 128 | FailureLast | 712 B | 0.1135 |
| Sequence | 128 | FailureLast | 1392 B | 0.2217 |
| Traverse | 128 | FailureLast | 1416 B | 0.2251 |
| TraverseCompletedAsync | 128 | FailureLast | 21392 B | 3.4027 |
| Combine | 128 | AllErrors | 3264 B | 0.5198 |
| Sequence | 128 | AllErrors | 7424 B | 1.1826 |
| Traverse | 128 | AllErrors | 7448 B | 1.1864 |
| TraverseCompletedAsync | 128 | AllErrors | 27424 B | 4.3640 |

The next implementation slice should reduce single-error storage and collection
intermediates while preserving the measured zero-allocation paths. The completed
async traversal cases expose substantial overhead even when callback tasks are
reused; avoid treating that overhead as the cost of real asynchronous I/O.

## Bounded event-store append

All six cases reported **129 bytes per append**. Each invocation appends 256
new event objects into a fresh store; BenchmarkDotNet normalizes allocations per
append. Store construction is outside measurement, while stream creation, list
growth, event objects, returned tasks, and amortized batch-method overhead are
inside it. The small batches did not produce a useful Gen0 rate. The two
`EventCount` parameter values are duplicate append workloads; that parameter
controls the read benchmark only.

## Follow-up measurements

This first slice focuses on the core Outcome candidates. Cold/warm CQRS dispatch,
subscriber publishing, serialization, suspended async callbacks, and large event
reads still need dedicated baselines before optimizing those paths. Collection
results here cover typed outcomes; heterogeneous errors and boxing require
separate comparisons.

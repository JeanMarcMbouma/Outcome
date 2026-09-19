# Outcome 1.2.0-preview.3

This preview reduces allocations in error snapshots, Combine, Sequence, Traverse,
Zip, and asynchronous collection operations without changing public signatures.
Error ordering, defensive copies, and full traversal are preserved.

- Single-error snapshots allocate 24 bytes instead of 56 bytes in the measured cases.
- Successful eight-item Combine allocates 56 bytes instead of 88 bytes and improved
  from 18.64 ns to 6.24 ns in the targeted benchmark.
- All 13 targeted throughput comparisons pass the documented tolerance. Two cases
  from the earlier full comparison remain inconclusive; universal throughput parity
  is not claimed.
- Core and integration suites passed 657 test executions across .NET 8, 9, and 10.

The release includes BbQ.Outcome, BbQ.Outcome.SourceGenerators,
BbQ.Outcome.AspNetCore, BbQ.Outcome.SystemTextJson, and BbQ.Outcome.Diagnostics.

See [allocation results](allocation-optimization.md) and
[throughput verification](throughput-fixes.md) for methodology and limitations.

# Before/after throughput verification

This opt-in harness compares all 37 typed Outcome allocation workloads against the
pre-optimization sources, using the same build settings for both versions.

From the repository root:

```powershell
./scripts/Measure-AllocationThroughput.ps1
./scripts/Summarize-ThroughputParity.ps1
```

The measurement script creates immutable source snapshots under
`artifacts/throughput-sources/` and records their hashes. It does not reset or edit
production files. Namespace names are changed in both copies so BenchmarkDotNet
can distinguish their return types. The baseline defaults to commit
`d667dc99723922e29e852378e36fd598965a15fc` and can be changed with `-BaselineRef`.

The project requires those generated snapshots, so it is intentionally separate
from the regular solution build. Re-running the script refuses to overwrite them.
To remeasure the existing snapshots:

```powershell
dotnet run -c Release --project tests/BbQ.Outcome.ThroughputBenchmarks -- --filter '*' --launchCount 2 --warmupCount 30 --iterationCount 12 --iterationTime 200 --affinity 1 --exporters json --artifacts artifacts/throughput-parity-warmed
```

Thirty warmup iterations are intentional: six iterations allowed tiered compilation
to change code during measurement on this machine. Inspect iteration stability
before interpreting results on a different host. Two launches and twelve measured
iterations provide 24 samples before outlier removal. CPU affinity pins each process
to one logical core; other heavy workloads should not run concurrently.

The summary script applies a 5% non-regression tolerance, with a 1 ns floor for
scalar cases, to the 99.9% timing confidence intervals. Overlapping or insufficiently
separated intervals are reported as inconclusive, not as proof of parity. Results
cover .NET 10 and cached completed async callbacks; they do not establish parity
for every runtime or workload.

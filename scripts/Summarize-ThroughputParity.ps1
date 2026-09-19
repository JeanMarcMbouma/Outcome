param(
    [string]$ReportPath = 'artifacts/throughput-parity-warmed/results',
    [string]$OutputPath = 'docs/throughput-parity.md'
)

$ErrorActionPreference = 'Stop'
$reports = Get-ChildItem $ReportPath -Filter '*-report-full-compressed.json'
$benchmarks = @($reports | ForEach-Object { (Get-Content $_.FullName -Raw | ConvertFrom-Json).Benchmarks })
if ($benchmarks.Count -ne 74 -or @($benchmarks | Where-Object { $null -eq $_.Statistics }).Count) {
    throw 'Expected 74 successfully measured benchmarks (37 before/after pairs).'
}

$rows = foreach ($before in $benchmarks | Where-Object { $_.Method.StartsWith('Before') }) {
    $operation = $before.Method.Substring(6)
    $after = @($benchmarks | Where-Object { $_.Type -eq $before.Type -and $_.Parameters -eq $before.Parameters -and $_.Method -eq "After$operation" })
    if ($after.Count -ne 1) { throw "Missing or duplicate counterpart for $operation" }
    $after = $after[0]
    $b = $before.Statistics
    $a = $after.Statistics
    $tolerance = 0.05 * $b.Mean
    if ($before.Type -eq 'ScalarParityBenchmarks') { $tolerance = [Math]::Max(1.0, $tolerance) }
    $status = if ($a.ConfidenceInterval.Upper -le ($b.ConfidenceInterval.Lower + $tolerance)) {
        'Pass'
    } elseif ($a.ConfidenceInterval.Lower -gt ($b.ConfidenceInterval.Upper + $tolerance)) {
        'Regression'
    } else { 'Inconclusive' }
    $operationName = switch ($operation) {
        'Single' { 'CreateSingleError' }
        'Success' { 'CreateSuccess' }
        'Snapshot' { 'SnapshotMultipleErrors' }
        'Propagate' { 'PropagateError' }
        'Reuse' { 'ReuseErrorSnapshot' }
        'Async' { 'TraverseCompletedAsync' }
        default { $operation }
    }
    [pscustomobject]@{
        Operation = $operationName
        Inputs = $before.Parameters.Replace('&', ', ')
        BeforeNs = $b.Mean
        AfterNs = $a.Mean
        BeforeLower = $b.ConfidenceInterval.Lower
        BeforeUpper = $b.ConfidenceInterval.Upper
        AfterLower = $a.ConfidenceInterval.Lower
        AfterUpper = $a.ConfidenceInterval.Upper
        BeforeBytes = $before.Memory.BytesAllocatedPerOperation
        AfterBytes = $after.Memory.BytesAllocatedPerOperation
        Status = $status
    }
}

$pass = @($rows | Where-Object Status -eq 'Pass').Count
$regressions = @($rows | Where-Object Status -eq 'Regression').Count
$inconclusive = @($rows | Where-Object Status -eq 'Inconclusive').Count
$verdict = if ($regressions -gt 0 -or $inconclusive -gt 0) {
    'Full throughput parity is not established. Allocation savings alone are insufficient to approve the changes as performance-neutral.'
} else {
    'All tested cases meet the stated non-regression tolerance. This is limited to the measured runtime and workloads.'
}
$lines = @(
    '# Throughput parity — 2026-09-19', '',
    "Results: **$pass pass, $regressions regressions, $inconclusive inconclusive** across 37 comparisons.", '',
    $verdict, '',
    '## Method', '',
    '- Before: production sources at `d667dc99723922e29e852378e36fd598965a15fc`.',
    '- After: the optimized working-tree sources, snapshotted before measurement.',
    '- Both snapshots compile with identical Release settings and separate assembly/namespace names.',
    '- .NET 10.0.12, SDK 10.0.401, Windows 11, Intel Core Ultra 7 258V; BenchmarkDotNet 0.15.6.',
    '- One CPU core (affinity mask 1), sequential runs, two process launches per benchmark.',
    '- Each launch: 30 warmup iterations, 12 measured iterations, 200 ms target iteration time.',
    '- Original and optimized cases run consecutively for each workload. Both are prepared by the same harness.',
    '- An initial six-warmup run showed runtime compilation transitions during measurement and was discarded.', '',
    '## Interpretation', '',
    'Pass means the optimized upper 99.9% confidence bound is at most the original lower',
    'bound plus a 5% slowdown tolerance. Scalar operations use a minimum 1 ns tolerance',
    'because their durations approach measurement overhead. Regression means the optimized',
    'lower bound exceeds the original upper bound plus that tolerance. Other cases are',
    'inconclusive; similar averages alone do not establish parity.', '',
    'The 1 ns floor is a practical allowance, not strict percentage equivalence.',
    'Inspect the scalar means even when they pass: a sub-nanosecond slowdown can be',
    'a large relative change. Cases with insufficiently narrow intervals remain',
    'inconclusive rather than being reported as equivalent.', '',
    'These are conservative comparisons of independent timing intervals, not paired-sample',
    'equivalence tests or a guarantee of application throughput. Results apply to .NET 10,',
    'the tested typed outcomes, and completed async callbacks. Other runtimes, heterogeneous',
    'errors, suspended I/O, Zip, and arbitrary workloads are not covered by this throughput check.', '',
    'Times and intervals below are nanoseconds per operation. Bytes are managed allocations.', '',
    '| Operation | Inputs | Before mean [99.9% CI] | After mean [99.9% CI] | Bytes before → after | Result |',
    '|---|---|---:|---:|---:|---|'
)
foreach ($row in $rows) {
    $lines += '| {0} | {1} | {2:F3} [{3:F3}, {4:F3}] | {5:F3} [{6:F3}, {7:F3}] | {8} → {9} | {10} |' -f $row.Operation, $row.Inputs, $row.BeforeNs, $row.BeforeLower, $row.BeforeUpper, $row.AfterNs, $row.AfterLower, $row.AfterUpper, $row.BeforeBytes, $row.AfterBytes, $row.Status
}
$lines += @('', '## Reproduction', '',
    'Run `scripts/Measure-AllocationThroughput.ps1` from a clean snapshot-artifact state.',
    'It preserves source hashes in `artifacts/throughput-sources/source-hashes.json`.',
    'Existing snapshots are intentionally not overwritten. To rerun those snapshots:', '',
    '```powershell',
    'dotnet run -c Release --project tests/BbQ.Outcome.ThroughputBenchmarks -- --filter ''*'' --launchCount 2 --warmupCount 30 --iterationCount 12 --iterationTime 200 --affinity 1 --exporters json --artifacts artifacts/throughput-parity-warmed',
    './scripts/Summarize-ThroughputParity.ps1',
    '```', '',
    'Raw JSON/CSV reports remain under `artifacts/throughput-parity-warmed/results/` (ignored by Git).')
Set-Content -LiteralPath $OutputPath -Value $lines -Encoding utf8
$rows | Where-Object Status -ne 'Pass' | Format-Table Operation, Inputs, BeforeNs, AfterNs, Status
Write-Output "$pass pass; $regressions regressions; $inconclusive inconclusive."

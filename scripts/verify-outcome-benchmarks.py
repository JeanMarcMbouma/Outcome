"""Fail CI when BenchmarkDotNet completes without measuring every expected case."""
import csv
import glob

paths = glob.glob("BenchmarkDotNet.Artifacts/results/*ExtensionPointsBenchmarks-report.csv")
if len(paths) != 1:
    raise SystemExit(f"Expected one extension benchmark CSV, found {len(paths)}")
with open(paths[0], newline="", encoding="utf-8-sig") as report:
    rows = list(csv.DictReader(report))
expected = {(method, success) for method in ("Map", "MapError", "Recover", "Observe", "SnapshotExternalErrors")
            for success in ("True", "False")}
actual = {(row.get("Method"), row.get("Success")) for row in rows}
if actual != expected:
    raise SystemExit(f"Benchmark cases differ: missing={expected-actual}, extra={actual-expected}")
for row in rows:
    mean = row.get("Mean", "").strip()
    if not mean or mean.upper() in {"NA", "NAN", "N/A", "?"}:
        raise SystemExit(f"Missing measurement: {row}")
print(f"BENCHMARK_PASS: {len(rows)} success/failure cases measured; no performance threshold is asserted.")

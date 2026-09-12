"""Summarize actual TRX counters, keeping skipped integration tests visible."""
from pathlib import Path
import os
import xml.etree.ElementTree as ET

ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
reports = sorted(Path("tests").glob("**/TestResults/*.trx"))
if not reports:
    raise SystemExit("No TRX test reports were produced.")
totals = {key: 0 for key in ("total", "passed", "failed", "notExecuted")}
lines = ["# Test results", "", "| Report | Passed | Failed | Not executed | Total |", "|---|---:|---:|---:|---:|"]
for report in reports:
    root = ET.parse(report).getroot()
    counters = root.find("t:ResultSummary/t:Counters", ns)
    if counters is None:
        raise SystemExit(f"Missing test counters in {report}")
    counts = {key: int(counters.attrib.get(key, "0")) for key in totals}
    for key in totals:
        totals[key] += counts[key]
    lines.append(f"| {report.as_posix()} | {counts['passed']} | {counts['failed']} | {counts['notExecuted']} | {counts['total']} |")
lines.extend(["", f"TEST_SUMMARY: reports={len(reports)}, passed={totals['passed']}, failed={totals['failed']}, notExecuted={totals['notExecuted']}, total={totals['total']}",
    "", "Counts include repeated executions on each target framework. Not-executed tests are not claimed as passing."])
summary = "\n".join(lines) + "\n"
Path("artifacts").mkdir(exist_ok=True)
Path("artifacts/test-summary.md").write_text(summary, encoding="utf-8")
print(summary)
if path := os.environ.get("GITHUB_STEP_SUMMARY"):
    with open(path, "a", encoding="utf-8") as output:
        output.write(summary)
if totals["failed"]:
    raise SystemExit("One or more tests failed.")

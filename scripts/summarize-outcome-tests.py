"""Summarize actual TRX outcomes, including NUnit skips omitted from notExecuted counters."""
from collections import Counter
from pathlib import Path
import os
import xml.etree.ElementTree as ET

ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
reports = sorted(Path("tests").glob("**/TestResults/*.trx"))
if not reports:
    raise SystemExit("No TRX test reports were produced.")
totals = {key: 0 for key in ("total", "passed", "failed", "notExecuted", "other")}
lines = ["# Test results", "", "| Report | Passed | Failed | Not executed | Other | Total |", "|---|---:|---:|---:|---:|---:|"]
for report in reports:
    root = ET.parse(report).getroot()
    counters = root.find("t:ResultSummary/t:Counters", ns)
    if counters is None:
        raise SystemExit(f"Missing test counters in {report}")
    total = int(counters.attrib.get("total", "0"))
    passed = int(counters.attrib.get("passed", "0"))
    failed = int(counters.attrib.get("failed", "0"))
    outcomes = Counter(node.attrib.get("outcome", "") for node in root.findall("t:Results/t:UnitTestResult", ns))
    # NUnit can report skipped UnitTestResults while leaving the TRX notExecuted counter at zero.
    # Cross-check both the result outcomes and total-minus-executed, not just that counter.
    executed = int(counters.attrib.get("executed", str(total)))
    skipped = max(int(counters.attrib.get("notExecuted", "0")), outcomes["NotExecuted"], total - executed)
    other = total - passed - failed - skipped
    if other < 0:
        raise SystemExit(f"Inconsistent test counters in {report}")
    counts = {"total": total, "passed": passed, "failed": failed, "notExecuted": skipped, "other": other}
    for key in totals:
        totals[key] += counts[key]
    lines.append(f"| {report.as_posix()} | {passed} | {failed} | {skipped} | {other} | {total} |")
lines.extend(["", f"TEST_SUMMARY: reports={len(reports)}, passed={totals['passed']}, failed={totals['failed']}, notExecuted={totals['notExecuted']}, other={totals['other']}, total={totals['total']}",
    "", "Counts include repeated executions on each target framework. Not-executed and other outcomes are not claimed as passing."])
summary = "\n".join(lines) + "\n"
Path("artifacts").mkdir(exist_ok=True)
Path("artifacts/test-summary.md").write_text(summary, encoding="utf-8")
print(summary)
if path := os.environ.get("GITHUB_STEP_SUMMARY"):
    with open(path, "a", encoding="utf-8") as output:
        output.write(summary)
if totals["failed"] or totals["other"]:
    raise SystemExit("One or more tests failed or produced an unclassified outcome.")

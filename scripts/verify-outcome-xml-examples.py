#!/usr/bin/env python3
from pathlib import Path
import re
import sys

FILES = [
    "src/BbQ.Outcome/OutcomeErrorExtensions.cs",
    "src/BbQ.Outcome/OutcomeAsyncCompositionExtensions.cs",
    "src/BbQ.Outcome/OutcomeCollectionExtensions.cs",
    "src/BbQ.Outcome/OutcomeTry.cs",
    "src/BbQ.Cqrs.Outcome/ValidationServiceCollectionExtensions.cs",
    "src/BbQ.Outcome.AspNetCore/OutcomeHttpExtensions.cs",
    "src/BbQ.Outcome.Diagnostics/OutcomeObservationExtensions.cs",
    "src/BbQ.Outcome.SystemTextJson/OutcomeJsonExtensions.cs",
    "src/BbQ.Outcome.SystemTextJson/OutcomeErrorTypeRegistry.cs",
]

METHOD = re.compile(r"^\s*public\s+(?:static\s+)?(?:async\s+)?[^=;{]+\(")

def has_example(lines: list[str], index: int) -> bool:
    # XML documentation must be immediately associated with the member. Looking back
    # twenty lines allows detailed summary/remarks/typeparam sections without accepting
    # examples from an unrelated earlier declaration.
    start = max(0, index - 20)
    block = "\n".join(lines[start:index])
    last_member = max(block.rfind("public "), block.rfind("internal "), block.rfind("private "))
    last_example = block.rfind("<example>")
    return last_example >= 0 and last_example > last_member

failures: list[str] = []
checked = 0
for relative in FILES:
    path = Path(relative)
    if not path.exists():
        failures.append(f"{relative}: file not found")
        continue
    lines = path.read_text(encoding="utf-8-sig").splitlines()
    for index, line in enumerate(lines):
        if METHOD.match(line) and " class " not in line and " interface " not in line:
            checked += 1
            if not has_example(lines, index):
                failures.append(f"{relative}:{index + 1}: public API has no nearby <example> block: {line.strip()}")

if failures:
    print("XML_EXAMPLES_FAIL")
    print("\n".join(failures))
    sys.exit(1)

print(f"XML_EXAMPLES_PASS: {checked} public extension/factory members have inline <example> documentation.")

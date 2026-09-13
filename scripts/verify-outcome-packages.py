"""Check optional package contents and dependency direction without publishing."""
from pathlib import Path
import xml.etree.ElementTree as ET
from zipfile import ZipFile

expected = {
    "BbQ.Cqrs.Outcome": {"BbQ.Cqrs", "BbQ.Outcome"},
    "BbQ.Cqrs.Outcome.FluentValidation": {"BbQ.Cqrs.Outcome", "FluentValidation"},
    "BbQ.Outcome.AspNetCore": {"BbQ.Outcome"},
    "BbQ.Outcome.SystemTextJson": {"BbQ.Outcome"},
    "BbQ.Outcome.Diagnostics": {"BbQ.Outcome", "Microsoft.Extensions.Logging.Abstractions"},
}
found = set()
for path in Path("artifacts/packages").glob("*.nupkg"):
    with ZipFile(path) as package:
        manifest = next(name for name in package.namelist() if name.endswith(".nuspec"))
        root = ET.fromstring(package.read(manifest))
        ns = {"n": root.tag.partition("}")[0].lstrip("{")}
        package_id = root.findtext("n:metadata/n:id", namespaces=ns)
        if package_id not in expected:
            continue
        dependencies = {node.attrib["id"] for node in root.findall(".//n:dependency", ns)}
        if dependencies != expected[package_id]:
            raise SystemExit(f"Unexpected dependencies for {package_id}: {dependencies}")
        names = set(package.namelist())
        for framework in ("net8.0", "net9.0", "net10.0"):
            if f"lib/{framework}/{package_id}.dll" not in names:
                raise SystemExit(f"Missing {framework} assembly in {package_id}")
        if "README.md" not in names:
            raise SystemExit(f"Missing README in {package_id}")
        found.add(package_id)
if found != set(expected):
    raise SystemExit(f"Missing optional packages: {set(expected)-found}")
print(f"PACKAGE_PASS: {len(found)} optional packages contain all three target assemblies, README, and expected direct dependencies.")

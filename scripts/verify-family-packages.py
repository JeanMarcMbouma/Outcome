"""Verify packed Outcome/CQRS/Events versions and internal dependency minimums."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET
from zipfile import ZipFile

versions = {}
for project in Path("src").glob("BbQ.*/*.csproj"):
    root = ET.parse(project).getroot()
    package_id = root.findtext(".//PackageId")
    version = root.findtext(".//Version")
    if package_id and version:
        versions[package_id] = version

expected = {name for name in versions if name.startswith(("BbQ.Outcome", "BbQ.Cqrs", "BbQ.Events"))}
found = set()
for path in Path("artifacts/packages").glob("*.nupkg"):
    with ZipFile(path) as package:
        manifest = next(name for name in package.namelist() if name.endswith(".nuspec"))
        root = ET.fromstring(package.read(manifest))
        ns = {"n": root.tag.partition("}")[0].lstrip("{")}
        package_id = root.findtext("n:metadata/n:id", namespaces=ns)
        if package_id not in expected:
            continue
        version = root.findtext("n:metadata/n:version", namespaces=ns)
        if version != versions[package_id]:
            raise SystemExit(f"{package_id}: packed {version}, expected {versions[package_id]}")
        for dependency in root.findall(".//n:dependency", ns):
            dependency_id = dependency.attrib["id"]
            if dependency_id not in versions:
                continue
            requirement = dependency.attrib["version"]
            minimum = re.split(r"[,\]]", requirement.lstrip("[("))[0].strip()
            if minimum != versions[dependency_id]:
                raise SystemExit(f"{package_id}: {dependency_id} minimum {minimum}, expected {versions[dependency_id]}")
            if "-" not in version and "-" in minimum:
                raise SystemExit(f"{package_id}: stable package depends on preview {dependency_id} {minimum}")
        found.add(package_id)

if found != expected:
    raise SystemExit(f"Family package set mismatch: missing {expected-found}, unexpected {found-expected}")
print(f"FAMILY_PASS: {len(found)} package versions and internal dependency minimums match their projects.")

"""Open a canonical firmware example in the real Desktop input-selection workflow."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path


REPO = Path(__file__).resolve().parents[1]
GOLDEN = REPO / "testdata" / "golden" / "canonical"
DEFAULT_DESKTOP = (
    REPO / "src" / "NvtFwCombiner.Desktop" / "bin" / "Debug" / "net10.0"
    / "NvtFwCombiner.Desktop.exe"
)
CROSS_CASE_EXAMPLES = {
    "nt51929-ab-ctrlram-candidate": (
        "NT51929", "nt51929-ab-t05-d06", "nt51929-fw200-single-auto-prj-594-20260717"),
    "nt51950-ab-ctrlram-unsupported": (
        "NT51950", "nt51950-ab-boe-d82t80", "nt51950-fw200-single-auto-prj-676-20260717"),
}


def cases() -> dict[str, dict]:
    found = {}
    for path in GOLDEN.glob("**/provenance/case.json"):
        case = json.loads(path.read_text(encoding="utf-8"))
        case_id = case["caseId"]
        if case_id in found:
            raise ValueError(f"Duplicate canonical case ID: {case_id}")
        found[case_id] = case
    return found


def artifact(case: dict, artifact_id: str) -> Path:
    matches = [entry for entry in case["artifacts"] if entry["artifactId"] == artifact_id]
    if len(matches) != 1:
        raise ValueError(f"{case['caseId']}: expected one {artifact_id} artifact")
    entry = matches[0]
    path = (GOLDEN / entry["path"]).resolve()
    if not path.is_relative_to(GOLDEN.resolve()):
        raise ValueError(f"{case['caseId']}: {artifact_id} escapes canonical root")
    if not path.is_file():
        raise FileNotFoundError(path)
    if path.stat().st_size != entry["size"]:
        raise ValueError(f"{case['caseId']}: {artifact_id} size differs from provenance")
    with path.open("rb") as stream:
        actual = hashlib.file_digest(stream, "sha256").hexdigest()
    if actual.lower() != entry["sha256"].lower():
        raise ValueError(f"{case['caseId']}: {artifact_id} SHA-256 differs from provenance")
    return path


def number(case: dict) -> str:
    count = case.get("icCount")
    if isinstance(count, int) and count > 0:
        return "single" if count == 1 else str(count)
    topology = case["topology"]
    if topology == "single":
        return "single"
    match = re.fullmatch(r"cascade-(\d+)", topology)
    if match:
        return match.group(1)
    if topology == "topology-unscoped" and case["workflow"] in {"standard-merge", "ab-merge"}:
        return "single"
    raise ValueError(f"{case['caseId']}: IC Number is not declared")


def launch_args(case: dict) -> list[str]:
    workflow = case["workflow"]
    args = ["--workflow", workflow, "--ic", case["ic"], "--ic-num", number(case)]
    if workflow == "standard-merge":
        args += ["--dp", str(artifact(case, "dp-input")), "--tp", str(artifact(case, "tp-input"))]
    elif workflow == "ab-merge":
        for option, artifact_id in (("--dp", "dp-ab-input"), ("--tp-a", "tp-a-input"), ("--tp-b", "tp-b-input")):
            args += [option, str(artifact(case, artifact_id))]
    elif workflow == "ctrlram-replace":
        entries = case["artifacts"]
        base_id = "reference-base" if any(item["artifactId"] == "reference-base" for item in entries) else "expected-output"
        args += ["--base", str(artifact(case, base_id))]
        replacements = [
            item for item in entries
            if item.get("slotId", "").startswith("replace-ctrlram-")
        ]
        if not replacements:
            names = ("nf", "normal", "mp", "vn")
            for name in names:
                matches = [item for item in entries if item["artifactId"] in {
                    f"postbuild-{name}-ctrlram", f"{name}-ctrlram-input"}]
                if len(matches) > 1:
                    raise ValueError(f"{case['caseId']}: ambiguous {name} replacement")
                if matches:
                    replacements.append({**matches[0], "slotId": f"replace-ctrlram-{name}"})
        if not replacements:
            raise ValueError(f"{case['caseId']}: no declared CtrlRAM example input")
        for item in replacements:
            args += ["--ctrlram", f"{item['slotId']}={artifact(case, item['artifactId'])}"]
    else:
        raise ValueError(f"{case['caseId']}: no Desktop preloading route for {workflow}")
    return args


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("case_id", nargs="?")
    parser.add_argument("--list", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--no-build", action="store_true", help="Use an already built Desktop executable")
    parser.add_argument("--desktop", type=Path, default=DEFAULT_DESKTOP)
    options = parser.parse_args()
    catalog = cases()
    if options.list:
        for case_id, case in sorted(catalog.items()):
            print(f"{case_id}\t{case['ic']}\t{case['workflow']}\t{case['topology']}\t{case['testDisposition']['kind']}")
        for example_id, (ic, _, _) in CROSS_CASE_EXAMPLES.items():
            print(f"{example_id}\t{ic}\tctrlram-replace\tsingle\tcross-case UI example")
        return 0
    if not options.case_id or options.case_id not in catalog and options.case_id not in CROSS_CASE_EXAMPLES:
        parser.error("Specify a canonical case ID; use --list to see available IDs")
    if options.case_id in CROSS_CASE_EXAMPLES:
        ic, ab_case_id, ctrl_case_id = CROSS_CASE_EXAMPLES[options.case_id]
        ab, ctrl = catalog[ab_case_id], catalog[ctrl_case_id]
        args = ["--workflow", "ctrlram-replace", "--ic", ic, "--ic-num", "single",
            "--base", str(artifact(ab, "expected-output")), "--ctrlram",
            f"replace-ctrlram-nf={artifact(ctrl, 'postbuild-nf-ctrlram')}"]
        evidence = ("Cross-case UI example only; NT51950 AB Base is unsupported by CtrlRAM Replace"
            if ic == "NT51950" else "Cross-case UI candidate; no approved AB CtrlRAM full-output Golden")
    else:
        case = catalog[options.case_id]
        args = launch_args(case)
        evidence = f"{case['testDisposition']['kind']} ({GOLDEN / case['ic']})"
    command = [str(options.desktop.resolve()), *args]
    print(subprocess.list2cmdline(command), flush=True)
    print(f"Evidence: {evidence}", flush=True)
    if not options.dry_run:
        if not options.no_build and options.desktop.resolve() == DEFAULT_DESKTOP.resolve():
            subprocess.run(["dotnet", "build", "src/NvtFwCombiner.Desktop/NvtFwCombiner.Desktop.csproj",
                "--no-restore", "--verbosity", "quiet"], cwd=REPO, check=True)
        if not options.desktop.is_file():
            raise FileNotFoundError(f"Desktop executable missing: {options.desktop}; build src/NvtFwCombiner.Desktop first")
        subprocess.Popen(command, cwd=REPO)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, KeyError) as error:
        print(f"error: {error}", file=sys.stderr)
        sys.exit(1)

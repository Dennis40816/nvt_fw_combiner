"""Verify the CtrlRAM Replace fixture handoff.

The public smoke path exercises the workbench CtrlRAM Preview/Build flow with
self-replacement inputs sliced from existing owner-approved Standard Merge
golden data. Private owner firmware fixtures remain outside Git; when present,
this script validates their manifest and payload hashes so the same folder can
be promoted to byte regression once owner golden outputs are supplied.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_MANIFEST = ROOT / "testdata" / "golden" / "ctrlram-replace" / "private" / "manifest.json"
PUBLIC_SMOKE_FILTER = "FullyQualifiedName~CtrlRamReplace"
EXPECTED_SCHEMA_VERSION = "0.1"
EXPECTED_PAYLOAD_CLASS = "private-owner-golden-firmware"
EXPECTED_RUNNER_STATUSES = {"ready-for-private-golden", "pending-golden-parity"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--manifest",
        type=Path,
        default=DEFAULT_MANIFEST,
        help=f"Private fixture manifest path. Default: {DEFAULT_MANIFEST}",
    )
    parser.add_argument(
        "--skip-public-smoke",
        action="store_true",
        help="Skip the public golden-backed CtrlRAM workbench smoke test.",
    )
    parser.add_argument(
        "--require-private",
        action="store_true",
        help="Fail when the private manifest is missing.",
    )
    parser.add_argument(
        "--configuration",
        help="Optional dotnet test configuration for the public smoke test.",
    )
    parser.add_argument(
        "--no-build",
        action="store_true",
        help="Pass --no-build to the public dotnet smoke test.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if not args.skip_public_smoke:
        run_public_smoke(args.configuration, args.no_build)

    manifest_path = args.manifest.resolve()
    if not manifest_path.exists():
        message = f"private CtrlRAM fixture manifest not found: {manifest_path}"
        if args.require_private:
            print(f"error: {message}", file=sys.stderr)
            return 2

        print(f"warning: {message}")
        print("Public CtrlRAM workbench preview/build smoke passed; private byte regression was not executed.")
        return 0

    verify_private_manifest(manifest_path)
    print("Private CtrlRAM fixture manifest and payload hashes are valid.")
    print("CtrlRAM workbench output runner is enabled; private golden byte parity still requires owner outputs/sign-off.")
    return 0


def run_public_smoke(configuration: str | None, no_build: bool) -> None:
    dotnet = resolve_dotnet()
    command = [
        dotnet,
        "test",
        str(ROOT / "tests" / "NvtFwCombiner.UiSmoke.Tests" / "NvtFwCombiner.UiSmoke.Tests.csproj"),
        "--filter",
        PUBLIC_SMOKE_FILTER,
        "-v",
        "minimal",
    ]
    if configuration is not None:
        command.extend(["-c", configuration])
    if no_build:
        command.append("--no-build")
    print(f"> {' '.join(command)}", flush=True)
    result = subprocess.run(
        command,
        cwd=ROOT,
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    print(result.stdout, end="")
    if "No test matches" in result.stdout or "沒有任何測試符合" in result.stdout:
        raise RuntimeError(f"public CtrlRAM smoke filter matched no tests: {PUBLIC_SMOKE_FILTER}")


def resolve_dotnet() -> str:
    executable_name = "dotnet.exe" if sys.platform == "win32" else "dotnet"
    repository_dotnet = ROOT / ".dotnet" / executable_name
    if repository_dotnet.is_file():
        return str(repository_dotnet)
    system_dotnet = shutil.which("dotnet")
    if system_dotnet is not None:
        return system_dotnet
    raise RuntimeError("dotnet was not found. Run scripts/install-dotnet.ps1 or scripts/install-dotnet.sh first.")


def verify_private_manifest(manifest_path: Path) -> None:
    document = load_json(manifest_path)
    root = manifest_path.parent
    require(document.get("schemaVersion") == EXPECTED_SCHEMA_VERSION, "manifest schemaVersion must be 0.1")
    require(
        document.get("payloadClass") == EXPECTED_PAYLOAD_CLASS,
        f"manifest payloadClass must be {EXPECTED_PAYLOAD_CLASS}",
    )
    require(document.get("binaryPayloadsIncluded") is True, "manifest must declare binaryPayloadsIncluded=true")
    require(
        document.get("runnerStatus") in EXPECTED_RUNNER_STATUSES,
        f"runnerStatus must be one of {sorted(EXPECTED_RUNNER_STATUSES)}",
    )

    cases = document.get("cases")
    require(isinstance(cases, list) and cases, "manifest cases must be a non-empty array")
    for index, item in enumerate(cases):
        require(isinstance(item, dict), f"case[{index}] must be an object")
        verify_case(root, item, index)


def verify_case(root: Path, item: dict[str, Any], index: int) -> None:
    label = f"case[{index}]"
    for key in ("id", "ic", "icNum", "mode", "sourceClassification", "ownerApproval"):
        require(isinstance(item.get(key), str) and item[key].strip(), f"{label}.{key} must be a non-empty string")
    require(item["mode"] == "CtrlRAM", f"{label}.mode must be CtrlRAM")

    verify_file_entry(root, item.get("base"), f"{label}.base")
    replacements = item.get("replacementInputs")
    require(isinstance(replacements, list) and replacements, f"{label}.replacementInputs must be non-empty")
    for replacement_index, replacement in enumerate(replacements):
        require(isinstance(replacement, dict), f"{label}.replacementInputs[{replacement_index}] must be an object")
        for key in ("slotId", "regionName"):
            require(
                isinstance(replacement.get(key), str) and replacement[key].strip(),
                f"{label}.replacementInputs[{replacement_index}].{key} must be a non-empty string",
            )
        verify_file_entry(root, replacement.get("file"), f"{label}.replacementInputs[{replacement_index}].file")

    expected = item.get("expectedOutput")
    if expected is not None:
        verify_file_entry(root, expected, f"{label}.expectedOutput")


def verify_file_entry(root: Path, entry: Any, label: str) -> None:
    require(isinstance(entry, dict), f"{label} must be an object")
    relative_path = entry.get("path")
    expected_size = entry.get("size")
    expected_sha256 = entry.get("sha256")
    require(isinstance(relative_path, str) and relative_path.strip(), f"{label}.path must be a non-empty string")
    require(isinstance(expected_size, int) and expected_size >= 0, f"{label}.size must be a non-negative integer")
    require(isinstance(expected_sha256, str) and len(expected_sha256) == 64, f"{label}.sha256 must be a SHA-256 hex string")

    candidate = (root / Path(relative_path)).resolve()
    try:
        candidate.relative_to(root.resolve())
    except ValueError as exc:
        raise ValueError(f"{label}.path escapes the private fixture root: {relative_path}") from exc
    require(candidate.is_file(), f"{label}.path does not exist: {candidate}")

    payload = candidate.read_bytes()
    actual_sha256 = hashlib.sha256(payload).hexdigest()
    require(len(payload) == expected_size, f"{label}.size drift: expected {expected_size}, got {len(payload)}")
    require(actual_sha256 == expected_sha256.lower(), f"{label}.sha256 drift: expected {expected_sha256}, got {actual_sha256}")


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        document = json.load(handle)
    require(isinstance(document, dict), "manifest root must be an object")
    return document


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


if __name__ == "__main__":
    raise SystemExit(main())

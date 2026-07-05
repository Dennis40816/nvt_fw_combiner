"""Verify the CtrlRAM Replace fixture handoff.

The public smoke path exercises the workbench CtrlRAM Preview/Build flow with
self-replacement inputs sliced from existing owner-approved Standard Merge
golden data. Owner-approved CtrlRAM firmware fixtures may be committed under
testdata/golden; this script validates their manifest and payload hashes so the
same folder can be promoted to byte regression once owner golden outputs are
supplied.
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
DEFAULT_MANIFEST = ROOT / "testdata" / "golden" / "ctrlram-replace" / "manifest.json"
PUBLIC_SMOKE_FILTER = "FullyQualifiedName~CtrlRamReplace"
EXPECTED_SCHEMA_VERSION = "0.2"
EXPECTED_PAYLOAD_CLASSES = {
    "owner-approved-golden-firmware",
    "private-owner-golden-firmware",
}
EXPECTED_RUNNER_STATUSES = {"ready-for-private-golden", "pending-golden-parity"}
FWCONFIG_STARTS = {
    "NT51917": 0x16000,
    "NT51919": 0x1F200,
    "NT51920": 0x22000,
    "NT51923": 0x22000,
    "NT51926": 0x22000,
    "NT51927": 0x16000,
    "NT51928": 0x16000,
    "NT51929": 0x1F200,
    "NT51930": 0x1F200,
    "NT51931": 0x16000,
    "NT51932": 0x1F200,
    "NT51950": 0x22200,
    "NT51951": 0x22200,
}
FW_VERSION_OFFSET = 0x000
FW_VERSION_BAR_OFFSET = 0x001
COMMON_FW_MAJOR_OFFSET = 0x01A
COMMON_FW_MINOR_OFFSET = 0x01B
COMMON_FW_ADDITIONAL_OFFSET = 0x01C
PROJECT_ID_OFFSET = 0x022
FWCONFIG_REQUIRED_LENGTH = PROJECT_ID_OFFSET + 2
DEFAULT_POSTBUILD_CATEGORIES = {
    "NT51917": "PostbuildSetup_51927_1.4.1",
    "NT51919": "PostbuildSetup_51932_2.0.0",
    "NT51920": "PostbuildSetup_51920_1.3.1",
    "NT51923": "PostbuildSetup_51923_1.4.1",
    "NT51927": "PostbuildSetup_51927_1.4.1",
    "NT51928": "PostbuildSetup_51927_1.4.1",
    "NT51929": "PostbuildSetup_51932_2.0.0",
    "NT51931": "PostbuildSetup_51931_1.3.0",
    "NT51932": "PostbuildSetup_51932_2.0.0",
    "NT51950": "PostbuildSetup_51950_2.0.0",
    "NT51951": "PostbuildSetup_51950_2.0.0",
}
VERSIONED_POSTBUILD_CATEGORIES = {
    ("NT51926", "1.4.1"): "PostbuildSetup_51926_1.4.1",
    ("NT51926", "2.0.0"): "PostbuildSetup_51926_2.0.0",
    ("NT51930", "2.0.0"): "PostbuildSetup_51930_2.0.0",
}
VERSIONED_POSTBUILD_ICS = {"NT51926", "NT51930"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--manifest",
        type=Path,
        default=DEFAULT_MANIFEST,
        help=f"CtrlRAM fixture manifest path. Default: {DEFAULT_MANIFEST}",
    )
    parser.add_argument(
        "--skip-public-smoke",
        action="store_true",
        help="Skip the public golden-backed CtrlRAM workbench smoke test.",
    )
    parser.add_argument(
        "--require-fixture",
        action="store_true",
        help="Fail when the CtrlRAM fixture manifest is missing.",
    )
    parser.add_argument(
        "--require-private",
        action="store_true",
        help="Deprecated alias for --require-fixture.",
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
        message = f"CtrlRAM fixture manifest not found: {manifest_path}"
        if args.require_fixture or args.require_private:
            print(f"error: {message}", file=sys.stderr)
            return 2

        print(f"warning: {message}")
        print(
            "Public CtrlRAM workbench preview/build smoke passed; committed/private byte regression was not executed."
        )
        return 0

    verify_fixture_manifest(manifest_path)
    print("CtrlRAM fixture manifest and payload hashes are valid.")
    print(
        "CtrlRAM workbench output runner is enabled; golden byte parity still requires owner outputs/sign-off."
    )
    return 0


def run_public_smoke(configuration: str | None, no_build: bool) -> None:
    dotnet = resolve_dotnet()
    command = [
        dotnet,
        "test",
        str(
            ROOT
            / "tests"
            / "NvtFwCombiner.UiSmoke.Tests"
            / "NvtFwCombiner.UiSmoke.Tests.csproj"
        ),
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
        raise RuntimeError(
            f"public CtrlRAM smoke filter matched no tests: {PUBLIC_SMOKE_FILTER}"
        )


def resolve_dotnet() -> str:
    executable_name = "dotnet.exe" if sys.platform == "win32" else "dotnet"
    repository_dotnet = ROOT / ".dotnet" / executable_name
    if repository_dotnet.is_file():
        return str(repository_dotnet)
    system_dotnet = shutil.which("dotnet")
    if system_dotnet is not None:
        return system_dotnet
    raise RuntimeError(
        "dotnet was not found. Run scripts/install-dotnet.ps1 or scripts/install-dotnet.sh first."
    )


def verify_fixture_manifest(manifest_path: Path) -> None:
    document = load_json(manifest_path)
    root = manifest_path.parent
    require(
        document.get("schemaVersion") == EXPECTED_SCHEMA_VERSION,
        f"manifest schemaVersion must be {EXPECTED_SCHEMA_VERSION}",
    )
    require(
        document.get("payloadClass") in EXPECTED_PAYLOAD_CLASSES,
        f"manifest payloadClass must be one of {sorted(EXPECTED_PAYLOAD_CLASSES)}",
    )
    require(
        document.get("binaryPayloadsIncluded") is True,
        "manifest must declare binaryPayloadsIncluded=true",
    )
    require(
        document.get("runnerStatus") in EXPECTED_RUNNER_STATUSES,
        f"runnerStatus must be one of {sorted(EXPECTED_RUNNER_STATUSES)}",
    )

    cases = require_non_empty_list(document.get("cases"), "manifest cases")
    for index, item in enumerate(cases):
        require(isinstance(item, dict), f"case[{index}] must be an object")
        verify_case(root, item, index)


def verify_case(root: Path, item: dict[str, Any], index: int) -> None:
    label = f"case[{index}]"
    ic_id = require_non_empty_string(item.get("ic"), f"{label}.ic")
    mode = require_non_empty_string(item.get("mode"), f"{label}.mode")
    common_fw_version = require_non_empty_string(
        item.get("commonFwVersion"), f"{label}.commonFwVersion"
    )
    postbuild_category = require_non_empty_string(
        item.get("postbuildCategory"), f"{label}.postbuildCategory"
    )
    for key in ("id", "icNum", "sourceClassification", "ownerApproval"):
        require_non_empty_string(item.get(key), f"{label}.{key}")
    require(mode == "CtrlRAM", f"{label}.mode must be CtrlRAM")
    require(
        is_common_fw_version(common_fw_version),
        f"{label}.commonFwVersion must use major.minor.additional",
    )
    verify_postbuild_category(ic_id, common_fw_version, postbuild_category, label)

    base_payload = verify_file_entry(root, item.get("base"), f"{label}.base")
    verify_base_common_fw_version(ic_id, common_fw_version, base_payload, label)
    replacements = require_non_empty_list(
        item.get("replacementInputs"), f"{label}.replacementInputs"
    )
    for replacement_index, replacement in enumerate(replacements):
        require(
            isinstance(replacement, dict),
            f"{label}.replacementInputs[{replacement_index}] must be an object",
        )
        for key in ("slotId", "regionName"):
            require_non_empty_string(
                replacement.get(key),
                f"{label}.replacementInputs[{replacement_index}].{key}",
            )
        verify_file_entry(
            root,
            replacement.get("file"),
            f"{label}.replacementInputs[{replacement_index}].file",
        )

    expected = item.get("expectedOutput")
    if expected is not None:
        verify_file_entry(root, expected, f"{label}.expectedOutput")


def is_common_fw_version(value: str) -> bool:
    parts = value.split(".")
    return len(parts) == 3 and all(part.isdigit() for part in parts)


def verify_postbuild_category(
    ic_id: str, common_fw_version: str, category: str, label: str
) -> None:
    expected = expected_postbuild_category(ic_id, common_fw_version)
    if expected is not None:
        require(
            category == expected,
            f"{label}.postbuildCategory must be {expected} for {ic_id} Common FW {common_fw_version}",
        )
        return

    require(
        ic_id not in VERSIONED_POSTBUILD_ICS,
        f"{label}.commonFwVersion {common_fw_version} has no approved postbuild category for {ic_id}",
    )
    default_category = DEFAULT_POSTBUILD_CATEGORIES.get(ic_id)
    require(
        default_category is not None,
        f"{label}.ic {ic_id} has no approved postbuild category",
    )
    require(
        category == default_category,
        f"{label}.postbuildCategory must be {default_category} for {ic_id}",
    )


def expected_postbuild_category(ic_id: str, common_fw_version: str) -> str | None:
    if ic_id == "NT51930" and common_fw_version.startswith("1."):
        return "PostbuildSetup_51930_1.4.0"
    return VERSIONED_POSTBUILD_CATEGORIES.get((ic_id, common_fw_version))


def verify_base_common_fw_version(
    ic_id: str, expected_common_fw_version: str, payload: bytes, label: str
) -> None:
    require(
        ic_id in FWCONFIG_STARTS,
        f"{label}.ic {ic_id} has no verifier FWConfig start",
    )
    start = FWCONFIG_STARTS[ic_id]
    require(
        start + FWCONFIG_REQUIRED_LENGTH <= len(payload),
        f"{label}.base is too short for FWConfig at 0x{start:X}",
    )
    firmware_version = payload[start + FW_VERSION_OFFSET]
    firmware_version_bar = payload[start + FW_VERSION_BAR_OFFSET]
    require(
        firmware_version_bar == (~firmware_version & 0xFF),
        f"{label}.base FWConfig FW/bar validation failed at 0x{start:X}",
    )
    actual_common_fw_version = (
        f"{payload[start + COMMON_FW_MAJOR_OFFSET]}."
        f"{payload[start + COMMON_FW_MINOR_OFFSET]}."
        f"{payload[start + COMMON_FW_ADDITIONAL_OFFSET]}"
    )
    require(
        actual_common_fw_version == expected_common_fw_version,
        f"{label}.commonFwVersion must match base FWConfig: base has {actual_common_fw_version}, manifest declares {expected_common_fw_version}",
    )


def verify_file_entry(root: Path, entry: Any, label: str) -> bytes:
    require(isinstance(entry, dict), f"{label} must be an object")
    relative_path = entry.get("path")
    expected_size = entry.get("size")
    expected_sha256 = entry.get("sha256")
    relative_path = require_non_empty_string(relative_path, f"{label}.path")
    require(
        isinstance(expected_size, int) and expected_size >= 0,
        f"{label}.size must be a non-negative integer",
    )
    expected_sha256 = require_non_empty_string(expected_sha256, f"{label}.sha256")
    require(len(expected_sha256) == 64, f"{label}.sha256 must be a SHA-256 hex string")

    candidate = (root / Path(relative_path)).resolve()
    try:
        candidate.relative_to(root.resolve())
    except ValueError as exc:
        raise ValueError(
            f"{label}.path escapes the private fixture root: {relative_path}"
        ) from exc
    require(candidate.is_file(), f"{label}.path does not exist: {candidate}")

    payload = candidate.read_bytes()
    actual_sha256 = hashlib.sha256(payload).hexdigest()
    require(
        len(payload) == expected_size,
        f"{label}.size drift: expected {expected_size}, got {len(payload)}",
    )
    require(
        actual_sha256 == expected_sha256.lower(),
        f"{label}.sha256 drift: expected {expected_sha256}, got {actual_sha256}",
    )
    return payload


def require_non_empty_string(value: Any, label: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{label} must be a non-empty string")
    return value


def require_non_empty_list(value: Any, label: str) -> list[Any]:
    if not isinstance(value, list) or not value:
        raise ValueError(f"{label} must be a non-empty array")
    return value


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

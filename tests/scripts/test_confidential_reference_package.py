"""The package reference selection excludes confidential source formats."""

from __future__ import annotations

import os
import hashlib
import json
import re
import shutil
import subprocess
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[2]
PACKAGE_SOURCE = (ROOT / "scripts" / "package.ps1").read_text(encoding="utf-8-sig")


def test_reference_copy_admits_only_public_markdown_and_json(tmp_path: Path) -> None:
    shell = shutil.which("pwsh") or shutil.which("powershell")
    if shell is None:
        pytest.skip("PowerShell is unavailable")

    invocation = re.search(
        r"Copy-PackageReferenceTree -RelativeRoot 'docs/references/ic-flashmap' "
        r"-AllowedExtensions @\(([^)]*)\)",
        PACKAGE_SOURCE,
    )
    assert invocation is not None
    allowed_extensions = re.findall(r"'([^']+)'", invocation.group(1))

    function_start = PACKAGE_SOURCE.index("function Copy-PackageReferenceTree {")
    function_end = PACKAGE_SOURCE.index("function Assert-SafeCanonicalGoldenPath {", function_start)
    copy_function = PACKAGE_SOURCE[function_start:function_end]

    source = tmp_path / "repo" / "docs" / "references" / "ic-flashmap"
    source.mkdir(parents=True)
    for name in ("README.md", "SOURCE_MANIFEST.json", "sheet.xlsx", "script.bat", "memory_mmap.h"):
        (source / name).write_text("synthetic", encoding="utf-8")

    script = "\n".join(
        [
            "$ErrorActionPreference = 'Stop'",
            "$RepoRoot = $env:NFC_PACKAGE_TEST_REPO",
            "$ReferenceDestination = $env:NFC_PACKAGE_TEST_OUTPUT",
            "function Copy-PackageFile {",
            "    param([string]$RelativePath, [string]$DestinationRoot)",
            "    $destination = Join-Path $DestinationRoot $RelativePath",
            "    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null",
            "    Copy-Item -LiteralPath (Join-Path $RepoRoot $RelativePath) -Destination $destination",
            "}",
            copy_function,
            "Copy-PackageReferenceTree -RelativeRoot 'docs/references/ic-flashmap' "
            + "-AllowedExtensions @(" + ", ".join(f"'{extension}'" for extension in allowed_extensions) + ")",
        ]
    )
    output = tmp_path / "package"
    env = os.environ.copy()
    env["NFC_PACKAGE_TEST_REPO"] = str(tmp_path / "repo")
    env["NFC_PACKAGE_TEST_OUTPUT"] = str(output)
    subprocess.run([shell, "-NoProfile", "-Command", script], env=env, check=True, capture_output=True, text=True)

    copied = {path.name for path in output.rglob("*") if path.is_file()}
    assert copied == {"README.md", "SOURCE_MANIFEST.json"}


def test_public_confidential_manifest_is_a_reference_package_entry() -> None:
    assert "'docs/references/confidential-references.json'" in PACKAGE_SOURCE
    reference_list = PACKAGE_SOURCE.split("$ReferenceFiles = @(", 1)[1].split("\n    )", 1)[0]
    assert ".xlsx" not in reference_list


@pytest.mark.parametrize("mode", ["unset", "matching", "drifted", "escape", "input"])
def test_private_provenance_package_verification(tmp_path: Path, mode: str) -> None:
    shell = shutil.which("pwsh")
    if shell is None:
        pytest.skip("PowerShell is unavailable")
    root = tmp_path / "private-assets"
    references = root / "nfc" / "references"
    references.mkdir(parents=True)
    payload = b"synthetic private provenance"
    digest = hashlib.sha256(payload).hexdigest()
    (references / "synthetic.dat").write_bytes(b"drifted" if mode == "drifted" else payload)
    relative = "../outside.dat" if mode == "escape" else "synthetic.dat"
    (references / "SHA256SUMS").write_text(f"{digest}  {relative}\n", encoding="utf-8")
    artifact = {
        "storage": "private-reference",
        "role": "input" if mode == "input" else "provenance",
        "size": len(payload),
        "sha256": digest,
    }
    start = PACKAGE_SOURCE.index("function Assert-SafeCanonicalGoldenPath {")
    end = PACKAGE_SOURCE.index("function Get-DeclaredCanonicalGoldenPaths {", start)
    script = "\n".join([
        "$ErrorActionPreference = 'Stop'",
        "Set-StrictMode -Version Latest",
        "function Get-LowerSha256 { param([string]$Path) (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }",
        PACKAGE_SOURCE[start:end],
        "$Artifact = $env:NFC_PRIVATE_TEST_ARTIFACT | ConvertFrom-Json",
        "Assert-PrivateCanonicalReference -Artifact $Artifact",
    ])
    env = os.environ.copy()
    env["NVT_PRIVATE_ASSETS"] = "" if mode == "unset" else str(root)
    env["NFC_PRIVATE_TEST_ARTIFACT"] = json.dumps(artifact)
    result = subprocess.run([shell, "-NoProfile", "-Command", script], env=env,
                            capture_output=True, text=True, encoding="utf-8",
                            errors="replace", check=False)
    if mode in {"unset", "matching"}:
        assert result.returncode == 0, result.stderr
    else:
        assert result.returncode != 0
        assert {"drifted": "bytes drifted", "escape": "Unsafe canonical", "input": "hash-pinned provenance"}[mode] in result.stderr

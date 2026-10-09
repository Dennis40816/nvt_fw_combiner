"""Regression coverage for confidential-reference admission and certified identity."""

from __future__ import annotations

import hashlib
import json
import os
import shutil
import subprocess
import zipfile
from pathlib import Path
from unittest.mock import patch

import pytest

from scripts import verify


ROOT = Path(__file__).resolve().parents[2]


def test_private_provenance_preserves_certified_case_manifest_identity() -> None:
    allowlist = json.loads((ROOT / "testdata/golden/release-canonical-v1.json").read_bytes())
    private_cases = [
        case for case in allowlist["cases"]
        if any(artifact.get("storage") == "private-reference" for artifact in case["artifacts"])
    ]
    assert len(private_cases) == 1
    case = private_cases[0]
    payload = (ROOT / "testdata/golden/canonical" / case["manifestPath"]).read_bytes()
    certified_sha256 = "04b38a6bbf20918b98d259e0b6486b7724036c69a0d30017a314406958ce8c24"
    assert case["manifestSha256"] == hashlib.sha256(payload).hexdigest() == certified_sha256
    assert len(payload) == 12502
    assert all("storage" not in artifact for artifact in json.loads(payload)["artifacts"])


@pytest.mark.parametrize("mutation", ["missing-manifest", "reference-sheet", "golden-script"])
def test_first_inventory_version_smoke_refuses_confidential_reference_inventory(tmp_path: Path, mutation: str) -> None:
    shell = shutil.which("pwsh")
    if shell is None:
        pytest.skip("PowerShell is unavailable")
    version = "1.2.2"  # The first version that ships the inventory; published packages up to 1.2.1 predate it.
    package_name = f"NvtFwCombiner-v{version}-win-x64"
    required_paths = [
        "NvtFwCombiner.exe", "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe",
        "README.txt", "LICENSE.txt", "THIRD-PARTY-NOTICES.txt",
        "docs/contracts/canonical-capability-policy-v1.json",
    ]
    public_manifest = "reference/docs/references/confidential-references.json"
    if mutation != "missing-manifest":
        required_paths.append(public_manifest)
        required_paths.append({
            "reference-sheet": "reference/docs/references/synthetic.xlsx",
            "golden-script": "reference/golden/synthetic/provenance/synthetic.bat",
        }[mutation])
    reviewed_manifest = (ROOT / "docs/references/confidential-references.json").read_bytes()
    payloads = {path: (reviewed_manifest if path == public_manifest else b"synthetic") for path in required_paths}
    files = [
        {"path": path, "role": "reference" if path.startswith("reference/") else "application",
         "size": len(payloads[path]), "sha256": hashlib.sha256(payloads[path]).hexdigest()}
        for path in required_paths
    ]
    package_path = tmp_path / f"{package_name}.zip"
    with zipfile.ZipFile(package_path, "w") as archive:
        for path in required_paths:
            archive.writestr(f"{package_name}/{path}", payloads[path])
        archive.writestr(f"{package_name}/RELEASE-MANIFEST.json", json.dumps({
            "version": version, "sourceTag": f"v{version}", "files": files,
        }))
        archive.writestr(f"{package_name}/SHA256SUMS.txt", "")
    result = subprocess.run(
        [shell, "-NoProfile", "-File", str(ROOT / "scripts/smoke-release.ps1"),
         "-PackagePath", str(package_path), "-SkipUiLaunch"],
        capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=50, check=False,
    )
    output = result.stdout + result.stderr
    assert result.returncode != 0, output
    expected = ("must include the public confidential-reference manifest" if mutation == "missing-manifest"
                else "contains confidential reference content")
    assert expected in output, output


@pytest.mark.parametrize("skip_worktree", [False, True])
def test_confidential_index_entry_is_rejected_until_staged_removal(tmp_path: Path, skip_worktree: bool) -> None:
    subprocess.run(["git", "init", "--quiet"], cwd=tmp_path, check=True)
    source = tmp_path / "docs/references/synthetic.xlsx"
    source.parent.mkdir(parents=True)
    source.write_bytes(b"synthetic")
    subprocess.run(["git", "add", "."], cwd=tmp_path, check=True)
    relative_path = source.relative_to(tmp_path).as_posix()
    if skip_worktree:
        subprocess.run(["git", "update-index", "--skip-worktree", "--", relative_path], cwd=tmp_path, check=True)
    source.unlink()
    with patch.object(verify, "ROOT", tmp_path):
        with pytest.raises(RuntimeError, match="held privately"):
            verify.verify_confidential_reference_paths()
        subprocess.run(["git", "rm", "--cached", "--force", "--sparse", "--quiet", "--", relative_path], cwd=tmp_path, check=True)
        verify.verify_confidential_reference_paths()


@pytest.mark.parametrize("version_kind", ["historical", "current", "prerelease"])
def test_producer_selects_public_manifest_for_the_current_contract(version_kind: str) -> None:
    shell = shutil.which("pwsh")
    if shell is None:
        pytest.skip("PowerShell is unavailable")
    current = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    version = {"historical": "1.2.0", "current": current, "prerelease": current + "-preview.1"}[version_kind]
    env = os.environ.copy()
    env["NFC_REFERENCE_SELECTION_SCRIPT"] = str(ROOT / "scripts/package.ps1")
    env["NFC_REFERENCE_SELECTION_VERSION"] = version
    command = r"""
$ErrorActionPreference = 'Stop'
$tokens = $null; $errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    $env:NFC_REFERENCE_SELECTION_SCRIPT, [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw ($errors | Out-String) }
$assignments = @($ast.FindAll({ param($node)
    $node -is [System.Management.Automation.Language.AssignmentStatementAst] -and
    $node.Left.Extent.Text -ceq '$ReferenceFiles'
}, $true))
if ($assignments.Count -ne 1) { throw 'Expected one reference selection owner.' }
$SemanticVersion = $env:NFC_REFERENCE_SELECTION_VERSION
. ([scriptblock]::Create($assignments[0].Extent.Text))
ConvertTo-Json -InputObject @($ReferenceFiles) -Compress
"""
    result = subprocess.run(
        [shell, "-NoProfile", "-Command", command], env=env,
        capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=50, check=False,
    )
    assert result.returncode == 0, result.stdout + result.stderr
    paths = json.loads(result.stdout)
    assert all(isinstance(path, str) for path in paths)
    assert len(paths) == len(set(paths))
    assert ("docs/references/confidential-references.json" in paths) == (version_kind != "historical")

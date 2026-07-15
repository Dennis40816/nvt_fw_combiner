"""Deterministic release-package allowlist regressions."""

from __future__ import annotations

import json
import os
import shutil
import subprocess
import tempfile
import unittest
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
PACKAGE_SCRIPT = ROOT / "scripts" / "package.ps1"
SMOKE_SCRIPT = ROOT / "scripts" / "smoke-release.ps1"
PROBE_RELATIVE_PATH = Path("external-tools/release-package-policy-probe.txt")
POWERSHELL = shutil.which("pwsh") or shutil.which("powershell")


class ReleasePackagePolicyTests(unittest.TestCase):
    """Exercises the packager and smoke policy without building release binaries."""

    def run_powershell(
        self, script: Path, *arguments: str
    ) -> subprocess.CompletedProcess[str]:
        with tempfile.TemporaryDirectory(
            prefix=".release-policy-powershell-", dir=ROOT
        ) as shell_temp:
            environment = os.environ.copy()
            environment["TEMP"] = shell_temp
            environment["TMP"] = shell_temp
            return subprocess.run(
                [
                    str(POWERSHELL),
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    str(script),
                    *arguments,
                ],
                cwd=ROOT,
                check=False,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                env=environment,
            )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_packager_dry_run_excludes_extra_external_tool_from_stage_and_manifest(
        self,
    ) -> None:
        probe_path = ROOT / PROBE_RELATIVE_PATH
        self.assertFalse(
            probe_path.exists(), f"test probe already exists: {probe_path}"
        )

        result = self.run_powershell(
            PACKAGE_SCRIPT,
            "-Version",
            "0.0.0",
            "-Commit",
            "0" * 40,
            "-ExternalToolPolicyDryRun",
        )

        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(
            "probe excluded from staging and manifest",
            result.stdout,
        )
        self.assertFalse(
            probe_path.exists(), "packager did not clean its source policy probe"
        )

    def test_release_workflows_smoke_package_before_distribution(self) -> None:
        for workflow_path in (
            ROOT / ".github/workflows/release.yml",
            ROOT / ".github/workflows/main-package.yml",
            ROOT / "docs/ci/workflow-templates/release.yml",
        ):
            workflow = workflow_path.read_text(encoding="utf-8")
            package_index = workflow.index("scripts/package.ps1")
            smoke_index = workflow.index("scripts/smoke-release.ps1")
            distribution_markers = (
                marker
                for marker in ("gh release create", "actions/upload-artifact@")
                if marker in workflow
            )
            distribution_index = min(
                workflow.index(marker) for marker in distribution_markers
            )
            self.assertLess(package_index, smoke_index, workflow_path)
            self.assertLess(smoke_index, distribution_index, workflow_path)

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_release_smoke_rejects_extra_external_tool_even_when_manifested(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory(
            prefix="nvt-release-policy-test-"
        ) as temporary_directory:
            temporary_root = Path(temporary_directory)
            package_name = "NvtFwCombiner-v0.0.0-win-x64"
            package_root = temporary_root / package_name
            package_root.mkdir()

            for required_file in (
                "NvtFwCombiner.exe",
                "Nfc.CrcWorker.exe",
                "SHA256SUMS.txt",
                "README.txt",
                "LICENSE.txt",
                "THIRD-PARTY-NOTICES.txt",
            ):
                (package_root / required_file).write_bytes(b"release-policy fixture\n")

            staged_probe = package_root / PROBE_RELATIVE_PATH
            staged_probe.parent.mkdir(parents=True)
            staged_probe.write_bytes(b"negative release-policy probe\n")
            manifest = {
                "files": [
                    {
                        "path": PROBE_RELATIVE_PATH.as_posix(),
                        "size": staged_probe.stat().st_size,
                        "sha256": "0" * 64,
                        "role": "externalTool",
                    }
                ]
            }
            (package_root / "RELEASE-MANIFEST.json").write_text(
                json.dumps(manifest),
                encoding="utf-8",
            )

            package_path = temporary_root / f"{package_name}.zip"
            with zipfile.ZipFile(
                package_path, "w", compression=zipfile.ZIP_DEFLATED
            ) as archive:
                for path in sorted(package_root.rglob("*")):
                    if path.is_file():
                        archive.write(path, path.relative_to(temporary_root))

            result = self.run_powershell(
                SMOKE_SCRIPT,
                "-PackagePath",
                str(package_path),
                "-SkipUiLaunch",
            )

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(
            "Release manifest external-tool files differ from the approved allowlist.",
            result.stdout + result.stderr,
        )


if __name__ == "__main__":
    unittest.main()

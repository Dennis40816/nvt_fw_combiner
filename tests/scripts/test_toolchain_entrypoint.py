"""Policy tests for the single Windows toolchain entry point."""

from __future__ import annotations

import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
ENTRYPOINT = ROOT / "scripts" / "nfc.ps1"
POWERSHELL_7 = shutil.which("pwsh")
WINDOWS_POWERSHELL = shutil.which("powershell")


class ToolchainEntrypointTests(unittest.TestCase):
    def test_entrypoint_reexecutes_under_powershell_7_and_uses_canonical_verifier(
        self,
    ) -> None:
        script = ENTRYPOINT.read_text(encoding="utf-8")

        self.assertIn("$PSVersionTable.PSVersion.Major -lt 7", script)
        self.assertIn("Get-Command pwsh", script)
        self.assertIn("-File $PSCommandPath -Task $Task", script)
        self.assertIn("scripts/verify.py', '--all'", script)
        self.assertIn("scripts/verify.py', '--structure-only'", script)
        self.assertIn("install-dotnet.ps1", script)
        self.assertNotIn("dotnet test", script)

    def test_entrypoint_has_actionable_failure_classes(self) -> None:
        script = ENTRYPOINT.read_text(encoding="utf-8")

        self.assertIn("toolchain:invocation", script)
        self.assertIn("toolchain:dependency", script)
        self.assertIn("toolchain:environment", script)
        self.assertIn("[string]$FailureClass = 'assertion'", script)
        self.assertIn("-FailureClass 'evidence'", script)
        self.assertIn("toolchain:$FailureClass", script)

    def test_verify_fails_closed_when_explicit_bootstrap_is_missing(self) -> None:
        script = ENTRYPOINT.read_text(encoding="utf-8")

        self.assertIn("Run scripts/nfc.ps1 -Task bootstrap, then retry verify.", script)
        self.assertNotIn("-Step 'verify-dependencies'", script)

    @unittest.skipUnless(POWERSHELL_7, "PowerShell 7 is required")
    def test_invalid_task_is_classified_without_running_the_toolchain(self) -> None:
        result = subprocess.run(
            [
                str(POWERSHELL_7),
                "-NoLogo",
                "-NoProfile",
                "-File",
                str(ENTRYPOINT),
                "-Task",
                "definitely-invalid",
            ],
            cwd=ROOT,
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
        )

        self.assertNotEqual(0, result.returncode)
        self.assertIn("[toolchain:invocation]", result.stdout + result.stderr)
        self.assertNotIn("install-dotnet.ps1", result.stdout + result.stderr)

    @unittest.skipUnless(
        POWERSHELL_7 and WINDOWS_POWERSHELL,
        "Windows PowerShell and PowerShell 7 are required for re-entry coverage",
    )
    def test_windows_powershell_reentry_uses_pwsh_7_even_from_a_spaced_path(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory(prefix="nfc toolchain host ") as temporary:
            copied_entrypoint = Path(temporary) / "nfc entrypoint.ps1"
            shutil.copyfile(ENTRYPOINT, copied_entrypoint)
            result = subprocess.run(
                [
                    str(WINDOWS_POWERSHELL),
                    "-NoLogo",
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    str(copied_entrypoint),
                    "-Task",
                    "host-diagnostic",
                ],
                check=False,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
            )

        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertRegex(
            result.stdout, r"toolchain-host=pwsh;version=(?:[7-9]|[1-9][0-9])\."
        )
        self.assertNotIn("Repository structure validation passed", result.stdout)


if __name__ == "__main__":
    unittest.main()

"""Contract tests for the final Distribution Launcher local E2E smoke."""

from __future__ import annotations

import hashlib
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "scripts"
sys.path.insert(0, str(SCRIPTS))

import create_launcher_process_smoke_source as smoke_source  # noqa: E402


SMOKE_SCRIPT = SCRIPTS / "smoke-distribution-launcher.ps1"


class CreateSingleLauncherSmokeSourceTests(unittest.TestCase):
    def _package(self, root: Path, version: str = "1.0.6") -> Path:
        files = smoke_source._package_files(b"MZ-app", b"MZ-launcher")
        manifest = smoke_source._manifest(version, files)
        package = root / f"published-{version}.zip"
        smoke_source._write_package(package, version, files, manifest)
        return package

    def test_create_single_preserves_exact_package_and_binds_catalog_and_registry(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            package = self._package(root)
            expected_package = package.read_bytes()
            output = root / "source"

            result = smoke_source.build_single_source(output, package, "1.0.6")

            copied = output / "packages" / "NvtFwCombiner-v1.0.6-win-x64.zip"
            self.assertEqual(expected_package, copied.read_bytes())
            catalog_bytes = (output / "update-catalog.v1.json").read_bytes()
            catalog = json.loads(catalog_bytes)
            entry = catalog["versions"][0]
            self.assertEqual("1.0.6", entry["version"])
            self.assertEqual(len(expected_package), entry["packageSize"])
            self.assertEqual(hashlib.sha256(expected_package).hexdigest(), entry["packageSha256"])
            self.assertEqual("packages/NvtFwCombiner-v1.0.6-win-x64.zip", entry["packagePath"])
            registry = json.loads((output / "update-source-registry.json").read_bytes())
            self.assertEqual(1, registry["registryRevision"])
            self.assertEqual("1.0.6", registry["catalogPublication"]["latestVersion"])
            self.assertEqual(
                hashlib.sha256(catalog_bytes).hexdigest(),
                registry["catalogPublication"]["catalogSha256"],
            )
            self.assertEqual(str((output / "update-catalog.v1.json").resolve()), result.catalog_path)
            self.assertEqual(str((output / "update-source-registry.json").resolve()), result.registry_path)

    def test_create_single_rejects_manifest_version_mismatch_without_source(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            package = self._package(root, "1.0.5")

            with self.assertRaisesRegex(ValueError, "root or manifest path"):
                smoke_source.build_single_source(root / "source", package, "1.0.6")

            self.assertFalse((root / "source").exists())

    def test_create_single_rejects_noncanonical_or_unsafe_archive_inventory(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            package = root / "unsafe.zip"
            import zipfile

            with zipfile.ZipFile(package, "w") as archive:
                archive.writestr("../RELEASE-MANIFEST.json", b"{}")

            with self.assertRaisesRegex(ValueError, "unexpected root or manifest path"):
                smoke_source.build_single_source(root / "source", package, "1.0.6")

    def test_create_single_rejects_version_path_injection_before_writing(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            package = self._package(root)

            with self.assertRaisesRegex(ValueError, "three-component"):
                smoke_source.build_single_source(root / "source", package, "../1.0.6")

            self.assertFalse((root / "source").exists())

    def test_create_single_cli_publishes_the_same_single_source(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            package = self._package(root)
            output = root / "source"

            completed = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPTS / "create_launcher_process_smoke_source.py"),
                    "create-single",
                    "--output",
                    str(output),
                    "--package",
                    str(package),
                    "--version",
                    "1.0.6",
                ],
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertEqual(0, completed.returncode, completed.stderr)
            catalog = json.loads((output / "update-catalog.v1.json").read_bytes())
            self.assertEqual(["1.0.6"], [entry["version"] for entry in catalog["versions"]])


class DistributionLauncherSmokeScriptContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.script = SMOKE_SCRIPT.read_text(encoding="utf-8")

    def test_script_uses_real_enabled_install_click_and_two_launcher_runs(self) -> None:
        self.assertIn("UIAutomationClient", self.script)
        self.assertIn("[System.Windows.Automation.InvokePattern]::Pattern", self.script)
        self.assertIn(".Current.IsEnabled", self.script)
        self.assertIn(".Invoke()", self.script)
        self.assertIn(
            "$Evidence.firstInstallExitCode = Start-DistributionLauncher", self.script
        )
        self.assertIn("$Evidence.offlineExitCode = Start-DistributionLauncher", self.script)
        self.assertIn("firstInstallExitCode", self.script)
        self.assertIn("offlineExitCode", self.script)

    def test_script_isolates_state_and_registry_and_takes_source_offline(self) -> None:
        self.assertIn("$env:LOCALAPPDATA = $LocalAppData", self.script)
        self.assertIn("$env:NFC_UPDATE_SOURCE_REGISTRY_PATH = $RegistryPath", self.script)
        self.assertIn("create-single", self.script)
        self.assertIn("Move-Item -LiteralPath $SourceRoot -Destination $OfflineSourceRoot", self.script)
        self.assertIn("Assert-ReadyInstallation", self.script)
        self.assertIn("Assert-InstalledPackage", self.script)
        self.assertIn("expectedReleaseManifestSha256", self.script)
        self.assertIn("OutcomeText", self.script)
        self.assertIn("OperationProgressText", self.script)
        self.assertIn("SourceStatusText", self.script)
        self.assertIn("Setup reported a terminal failure", self.script)
        self.assertIn("NvtFwCombiner.Bootstrap.exe", self.script)
        self.assertIn("NvtFwCombiner.Launcher.exe", self.script)
        self.assertIn("function Wait-ExactManagedProcessSetExit", self.script)
        self.assertEqual(
            2,
            self.script.count("Wait-ExactManagedProcessSetExit $ManagedProcessPaths"),
        )
        first_wait = self.script.index("Wait-ExactManagedProcessSetExit $ManagedProcessPaths")
        source_offline = self.script.index(
            "Move-Item -LiteralPath $SourceRoot -Destination $OfflineSourceRoot"
        )
        self.assertLess(first_wait, source_offline)

    def test_script_cleanup_is_limited_to_validated_guid_temp_root(self) -> None:
        self.assertIn("nvt-distribution-launcher-smoke-", self.script)
        self.assertIn("Assert-SmokeRoot", self.script)
        self.assertIn("function Remove-SmokeRoot", self.script)
        self.assertEqual(1, self.script.count("[System.IO.Directory]::Delete($exactRoot, $true)"))
        self.assertIn("$exactRoot = Assert-SmokeRoot $Root", self.script)
        self.assertIn("$attempt -le 40", self.script)
        self.assertNotIn("Remove-Item -LiteralPath $env:", self.script)
        self.assertNotIn("Stop-Process -Name", self.script)
        self.assertNotIn(".Kill($true)", self.script)

    def test_managed_process_wait_requires_stable_quiescence(self) -> None:
        powershell = shutil.which("pwsh") or shutil.which("powershell")
        if powershell is None:
            self.skipTest("PowerShell is unavailable")
        start = self.script.index("function Wait-ExactManagedProcessSetExit")
        end = self.script.index("function Assert-InstalledPackage", start)
        function_source = self.script[start:end]
        expected = str((ROOT / "fake-managed.exe").resolve()).replace("'", "''")
        command = function_source + rf"""
$script:calls = 0
$process = [pscustomobject]@{{ Path = '{expected}' }}
$process | Add-Member -MemberType ScriptMethod -Name Dispose -Value {{ }}
$probe = {{
    $script:calls++
    if ($script:calls -eq 2) {{ return @($process) }}
    return @()
}}
Wait-ExactManagedProcessSetExit @('{expected}') 2000 $probe
if ($script:calls -ne 4) {{
    throw "Expected four snapshots (empty, active, empty, empty); got $script:calls."
}}
"""

        completed = subprocess.run(
            [powershell, "-NoProfile", "-Command", command],
            check=False,
            capture_output=True,
            text=True,
        )

        self.assertEqual(0, completed.returncode, completed.stderr + completed.stdout)

    def test_script_records_click_and_terminal_failure_stage_before_cleanup(self) -> None:
        self.assertIn("$script:UiAutomationInstallInvoked = $true", self.script)
        self.assertIn("$Evidence.stateExists", self.script)
        self.assertIn("$Evidence.launcherStateExists", self.script)
        self.assertIn("$Evidence.transactionExists", self.script)
        self.assertIn("$Evidence.transactionPhase", self.script)
        self.assertIn(
            '"$ManagedRoot.managed-setup-transaction.v1.json"',
            self.script,
        )
        self.assertIn(
            '"$ManagedRoot.managed-setup-staging"',
            self.script,
        )
        self.assertNotIn('"$ManagedRoot.setup-transaction.v1.json"', self.script)
        self.assertNotIn('"$ManagedRoot.setup-staging"', self.script)

    def test_script_parses_with_the_available_powershell(self) -> None:
        powershell = shutil.which("pwsh") or shutil.which("powershell")
        if powershell is None:
            self.skipTest("PowerShell is unavailable")
        escaped = str(SMOKE_SCRIPT).replace("'", "''")
        command = (
            "$tokens=$null;$errors=$null;"
            f"[System.Management.Automation.Language.Parser]::ParseFile('{escaped}',"
            "[ref]$tokens,[ref]$errors)|Out-Null;"
            "if($errors.Count){$errors|% Message;exit 1}"
        )

        completed = subprocess.run(
            [powershell, "-NoProfile", "-Command", command],
            check=False,
            capture_output=True,
            text=True,
        )

        self.assertEqual(0, completed.returncode, completed.stderr + completed.stdout)


if __name__ == "__main__":
    unittest.main()

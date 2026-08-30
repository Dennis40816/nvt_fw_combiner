"""Deterministic release-package allowlist regressions."""

from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import threading
import time
import tomllib
import unittest
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "scripts"
sys.path.insert(0, str(SCRIPTS))

import validate_repository as repository_validation  # noqa: E402

PACKAGE_SCRIPT = ROOT / "scripts" / "package.ps1"
LAUNCHER_PACKAGE_SCRIPT = ROOT / "scripts" / "package-distribution-launcher.ps1"
SMOKE_SCRIPT = ROOT / "scripts" / "smoke-release.ps1"
UPDATE_SOURCE_REGISTRY_TEMPLATE = (
    ROOT / "docs" / "ci" / "update-source-registry.json.in"
)
PROBE_RELATIVE_PATH = Path("external-tools/release-package-policy-probe.txt")
CAPABILITY_POLICY_RELATIVE_PATH = Path(
    "docs/contracts/canonical-capability-policy-v1.json"
)
CAPABILITY_POLICY_ROLE = "capabilityPolicy"
CAPABILITY_POLICY_SHA256 = (
    "bf818a4c9aa4d539882e4bc4a0a662ef70ece67a44e78ae83356430365828f50"
)
RUNTIME_CAPABILITY_POLICY = (
    ROOT
    / "src"
    / "NvtFwCombiner.Infrastructure"
    / "Capabilities"
    / "BuiltInCanonicalCapabilityPolicy.cs"
)
APPROVED_EXTERNAL_TOOL_PATHS = (
    "external-tools/README.md",
    "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe",
    "external-tools/legacy-combiner/README.md",
    "external-tools/legacy-combiner/1.13.0/Combiner.exe",
    "external-tools/legacy-combiner/1.13.0/manifest.json",
)
RETIRED_PRODUCTION_IC_IDS = ("NT51920", "NT51925", "NT51930", "NT51931")
POWERSHELL = shutil.which("pwsh") or shutil.which("powershell")
PWSH = shutil.which("pwsh")
DOTNET = (
    str(ROOT / ".dotnet" / "dotnet.exe")
    if (ROOT / ".dotnet" / "dotnet.exe").is_file()
    else shutil.which("dotnet")
)
PERSONAL_OWNER_IDENTIFIER = "Dennis40816"
DISTRIBUTION_OWNER = "MSP/FW3"
SOURCE_IDENTITY = "urn:msp-fw3:nvt-fw-combiner:source"
LEGACY_PACKAGE_BYTES = 80_000_000
MAXIMUM_PACKAGE_BYTES = 134_217_728
MAXIMUM_APPLICATION_BYTES = 80_000_000
ANSI_ESCAPE_PATTERN = re.compile(r"\x1b\[[0-?]*[ -/]*[@-~]")


def normalize_console_output(output: str) -> str:
    """Remove terminal styling and line wrapping before message assertions."""

    unstyled_output = ANSI_ESCAPE_PATTERN.sub("", output)
    return " ".join(unstyled_output.replace("|", " ").split())


def initialize_minimal_package_repository(repository_root: Path) -> str:
    """Create the smallest committed source that reaches snapshot identity checks."""

    script_path = repository_root / "scripts" / "package.ps1"
    script_path.parent.mkdir(parents=True)
    shutil.copy2(PACKAGE_SCRIPT, script_path)
    (repository_root / "VERSION").write_text("1.0.0\n", encoding="utf-8")
    for arguments in (
        ("init", "-q"),
        ("config", "user.name", "Package Identity Test"),
        ("config", "user.email", "package-identity@example.invalid"),
        ("config", "commit.gpgsign", "false"),
        ("add", "."),
        ("commit", "-q", "-m", "baseline"),
    ):
        subprocess.run(
            ["git", *arguments],
            cwd=repository_root,
            check=True,
            capture_output=True,
            text=True,
        )
    return subprocess.run(
        ["git", "rev-parse", "HEAD"],
        cwd=repository_root,
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()


def initialize_minimal_launcher_package_repository(
    repository_root: Path,
    *,
    stopper_fails: bool = False,
) -> str:
    """Create source that reaches Launcher restore and snapshot cleanup."""

    script_path = repository_root / "scripts" / "package-distribution-launcher.ps1"
    script_path.parent.mkdir(parents=True)
    shutil.copy2(LAUNCHER_PACKAGE_SCRIPT, script_path)
    if stopper_fails:
        (repository_root / "scripts" / "stop-idle-build-workers.ps1").write_text(
            "throw 'Injected stopper failure.'\n",
            encoding="utf-8",
        )
    (repository_root / "VERSION").write_text("1.0.6\n", encoding="utf-8")
    for arguments in (
        ("init", "-q"),
        ("config", "user.name", "Launcher Cleanup Test"),
        ("config", "user.email", "launcher-cleanup@example.invalid"),
        ("config", "commit.gpgsign", "false"),
        ("add", "."),
        ("commit", "-q", "-m", "baseline"),
    ):
        subprocess.run(
            ["git", *arguments],
            cwd=repository_root,
            check=True,
            capture_output=True,
            text=True,
        )
    return subprocess.run(
        ["git", "rev-parse", "HEAD"],
        cwd=repository_root,
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()


def literal_run_blocks(workflow: str) -> tuple[str, ...]:
    """Extract literal workflow run bodies for injection-policy assertions."""

    lines = workflow.splitlines()
    blocks: list[str] = []
    index = 0
    while index < len(lines):
        line = lines[index]
        if line.lstrip() != "run: |":
            index += 1
            continue
        parent_indent = len(line) - len(line.lstrip())
        index += 1
        body: list[str] = []
        while index < len(lines):
            candidate = lines[index]
            if (
                candidate.strip()
                and len(candidate) - len(candidate.lstrip()) <= parent_indent
            ):
                break
            body.append(candidate)
            index += 1
        blocks.append("\n".join(body))
    return tuple(blocks)


class ReleasePackagePolicyTests(unittest.TestCase):
    """Exercises the packager and smoke policy without building release binaries."""

    def test_packager_validates_the_generated_manifest_against_canonical_schema(
        self,
    ) -> None:
        package_script = PACKAGE_SCRIPT.read_text(encoding="utf-8")

        schema_path_index = package_script.index(
            "docs/contracts/release-manifest-v1.schema.json"
        )
        manifest_write_index = package_script.index(
            "$Manifest | ConvertTo-Json -Depth 8 | Set-Content"
        )
        validation_literal = (
            "Assert-CanonicalJsonSchema -JsonPath $ManifestPath "
            "-SchemaPath $ReleaseManifestSchemaPath"
        )
        validation_indexes = [
            match.start()
            for match in re.finditer(re.escape(validation_literal), package_script)
        ]
        archive_index = package_script.index(
            "Compress-Archive -LiteralPath $PackageRoot"
        )

        self.assertLess(schema_path_index, manifest_write_index)
        self.assertEqual(2, len(validation_indexes))
        self.assertLess(manifest_write_index, validation_indexes[0])
        self.assertLess(validation_indexes[0], validation_indexes[1])
        self.assertLess(validation_indexes[1], archive_index)

    def test_packager_restores_then_cleans_and_smoke_requires_window(self) -> None:
        package_script = PACKAGE_SCRIPT.read_text(encoding="utf-8")
        smoke_script = SMOKE_SCRIPT.read_text(encoding="utf-8")

        restore_index = package_script.index(
            "& $DotNet restore $AppProject -r win-x64 -p:PublishReadyToRun=true"
        )
        clean_index = package_script.index(
            "& $DotNet clean $AppProject -c Release -r win-x64"
        )
        publish_index = package_script.index(
            "& $DotNet publish $AppProject -c Release -r win-x64"
        )
        self.assertLess(restore_index, clean_index)
        self.assertLess(clean_index, publish_index)
        self.assertIn(
            "Restore-SourcePackageLocks -Snapshots $SourcePackageLockSnapshots",
            package_script,
        )
        self.assertIn("finally {", package_script)
        self.assertIn(
            "& $DotNet clean $AppProject -c Release -r win-x64", package_script
        )
        self.assertIn("$application.MainWindowHandle -eq 0", smoke_script)
        self.assertIn("$application.Responding", smoke_script)
        self.assertIn("$application.Dispose()", smoke_script)

    def test_distribution_metadata_uses_non_personal_owner_identity(self) -> None:
        distribution_metadata_paths = (
            ROOT / "LICENSE",
            ROOT / "Directory.Build.props",
            PACKAGE_SCRIPT,
            ROOT / "docs/references/verification-report.md",
        )

        for metadata_path in distribution_metadata_paths:
            metadata = metadata_path.read_text(encoding="utf-8")
            self.assertNotIn(PERSONAL_OWNER_IDENTIFIER, metadata, metadata_path)

        self.assertIn(
            DISTRIBUTION_OWNER,
            (ROOT / "LICENSE").read_text(encoding="utf-8"),
        )
        build_metadata = (ROOT / "Directory.Build.props").read_text(encoding="utf-8")
        self.assertIn(f"<Authors>{DISTRIBUTION_OWNER}</Authors>", build_metadata)
        self.assertIn(
            f"<RepositoryUrl>{SOURCE_IDENTITY}</RepositoryUrl>", build_metadata
        )
        package_script = PACKAGE_SCRIPT.read_text(encoding="utf-8")
        self.assertIn(f"$DistributionOwner = '{DISTRIBUTION_OWNER}'", package_script)
        self.assertIn(f"$SourceIdentity = '{SOURCE_IDENTITY}'", package_script)

    def test_packager_compresses_the_composite_ready_to_run_single_file(
        self,
    ) -> None:
        package_script = PACKAGE_SCRIPT.read_text(encoding="utf-8")
        publish_script = package_script[
            package_script.index(
                "& $DotNet publish $AppProject -c Release -r win-x64"
            ) : package_script.index("$PublishExitCode = $LASTEXITCODE")
        ]

        expected_publish_properties = (
            "-p:PublishSingleFile=true",
            "-p:EnableCompressionInSingleFile=true",
            "-p:PublishReadyToRun=true",
            "-p:PublishReadyToRunComposite=true",
            "-p:PublishTrimmed=false",
            "-p:IncludeNativeLibrariesForSelfExtract=true",
        )
        for publish_property in expected_publish_properties:
            self.assertEqual(1, publish_script.count(publish_property))

        property_positions = tuple(
            publish_script.index(publish_property)
            for publish_property in expected_publish_properties
        )
        self.assertEqual(tuple(sorted(property_positions)), property_positions)

    def test_external_tool_catalog_matches_packager_and_smoke_allowlists(self) -> None:
        catalog = json.loads(
            (ROOT / "external-tools/catalog.json").read_text(encoding="utf-8")
        )
        catalog_paths = set(catalog["releasePackagePaths"])

        self.assertEqual(set(APPROVED_EXTERNAL_TOOL_PATHS), catalog_paths)
        for script_path in (PACKAGE_SCRIPT, SMOKE_SCRIPT):
            match = re.search(
                r"\$ApprovedExternalToolPackagePaths\s*=\s*@\((.*?)\)\s*\|\s*Sort-Object",
                script_path.read_text(encoding="utf-8"),
                flags=re.DOTALL,
            )
            self.assertIsNotNone(match, script_path)
            self.assertEqual(
                catalog_paths,
                set(re.findall(r"'([^']+)'", match.group(1))),
                script_path,
            )
        self.assertFalse(
            any(
                Path(path).suffix.lower() in {".exe", ".dll"}
                and len(Path(path).parts) < 3
                for path in catalog_paths
            )
        )

    def test_capability_policy_is_hash_pinned_in_package_and_smoke_allowlists(
        self,
    ) -> None:
        runtime_policy = RUNTIME_CAPABILITY_POLICY.read_text(encoding="utf-8")
        self.assertIn(CAPABILITY_POLICY_RELATIVE_PATH.as_posix(), runtime_policy)
        self.assertIn(CAPABILITY_POLICY_SHA256, runtime_policy)
        for script_path in (PACKAGE_SCRIPT, SMOKE_SCRIPT):
            script = script_path.read_text(encoding="utf-8")
            self.assertIn(CAPABILITY_POLICY_RELATIVE_PATH.as_posix(), script)
            self.assertIn(CAPABILITY_POLICY_ROLE, script)
            self.assertIn(CAPABILITY_POLICY_SHA256, script)

    def test_console_output_normalization_removes_powershell_formatting(self) -> None:
        output = "\x1b[31mowner-approved maximum\x1b[0m\n| 58076715 bytes"

        self.assertEqual(
            "owner-approved maximum 58076715 bytes",
            normalize_console_output(output),
        )

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
    def test_packager_dry_run_enforces_external_tool_and_profile_allowlists(
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
        self.assertIn(
            "manifest-pinned materialized files included, entry hashes closed, and unexpected file rejected",
            result.stdout,
        )
        self.assertIn(
            "Runtime catalog package policy dry-run passed: approved files included and unexpected file rejected",
            result.stdout,
        )
        self.assertIn(
            "Retired support publication policy package dry-run passed: no parallel publicationPolicy payload entered staging or manifest",
            result.stdout,
        )
        self.assertIn(
            "Canonical golden package policy dry-run passed: 25 direct Standard Merge BIN artifacts and 10 direct/alias cases selected; diagnostics and other workflows excluded",
            result.stdout,
        )
        self.assertIn(
            "Canonical golden package policy direct/alias drift and strict-type rejection passed",
            result.stdout,
        )
        self.assertIn(
            "Release hash-list policy dry-run passed: Unicode paths round-trip through UTF-8",
            result.stdout,
        )
        self.assertFalse(
            probe_path.exists(), "packager did not clean its source policy probe"
        )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_packager_policy_dry_run_is_parallel_safe(self) -> None:
        command = [
            str(POWERSHELL),
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            str(PACKAGE_SCRIPT),
            "-Version",
            "0.0.0",
            "-Commit",
            "0" * 40,
            "-ExternalToolPolicyDryRun",
        ]
        processes = [
            subprocess.Popen(
                command,
                cwd=ROOT,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                encoding="utf-8",
                errors="replace",
            )
            for _ in range(2)
        ]

        results = [process.communicate(timeout=120) for process in processes]
        for process, (stdout, stderr) in zip(processes, results, strict=True):
            self.assertEqual(0, process.returncode, stdout + stderr)
        self.assertEqual([], list((ROOT / "external-tools").glob("release-package-policy-probe-*.txt")))

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_packager_rejects_version_that_differs_from_repository_identity(
        self,
    ) -> None:
        repository_version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
        major, minor, patch = (int(value) for value in repository_version.split("."))
        mismatched_version = f"{major}.{minor}.{patch + 1}"
        repository_head = subprocess.run(
            ["git", "rev-parse", "HEAD"],
            cwd=ROOT,
            check=True,
            capture_output=True,
            text=True,
        ).stdout.strip()

        result = self.run_powershell(
            PACKAGE_SCRIPT,
            "-Version",
            mismatched_version,
            "-Commit",
            repository_head,
            "-AllowPrerelease",
            "-ExternalToolPolicyDryRun",
        )

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(
            f"Package version '{mismatched_version}' does not match repository "
            f"VERSION '{repository_version}'",
            normalize_console_output(result.stdout + result.stderr),
        )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_v_prefixed_zero_version_is_not_the_policy_dry_run_sentinel(self) -> None:
        result = self.run_powershell(
            PACKAGE_SCRIPT,
            "-Version",
            "v0.0.0",
            "-Commit",
            "0" * 40,
            "-ExternalToolPolicyDryRun",
        )

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertNotIn(
            "External-tool package policy dry-run passed",
            normalize_console_output(result.stdout + result.stderr),
        )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_packager_rejects_commit_that_differs_from_repository_head(self) -> None:
        repository_version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()

        result = self.run_powershell(
            PACKAGE_SCRIPT,
            "-Version",
            repository_version,
            "-Commit",
            "1" * 40,
            "-ExternalToolPolicyDryRun",
        )

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(
            "Package commit does not match repository HEAD",
            normalize_console_output(result.stdout + result.stderr),
        )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_packager_rejects_dirty_repository_before_policy_dry_run(self) -> None:
        for dirty_kind in ("staged", "unstaged", "untracked"):
            with (
                self.subTest(dirty_kind=dirty_kind),
                tempfile.TemporaryDirectory(
                    prefix="nvt-package-source-identity-"
                ) as temporary_directory,
            ):
                repository_root = Path(temporary_directory)
                script_path = repository_root / "scripts" / "package.ps1"
                script_path.parent.mkdir(parents=True)
                shutil.copy2(PACKAGE_SCRIPT, script_path)
                (repository_root / "VERSION").write_text("1.0.0\n", encoding="utf-8")
                tracked_path = repository_root / "tracked.txt"
                tracked_path.write_text("clean\n", encoding="utf-8")
                for arguments in (
                    ("init", "-q"),
                    ("config", "user.name", "Package Identity Test"),
                    ("config", "user.email", "package-identity@example.invalid"),
                    ("config", "commit.gpgsign", "false"),
                    ("add", "."),
                    ("commit", "-q", "-m", "baseline"),
                ):
                    subprocess.run(
                        ["git", *arguments],
                        cwd=repository_root,
                        check=True,
                        capture_output=True,
                        text=True,
                    )
                repository_head = subprocess.run(
                    ["git", "rev-parse", "HEAD"],
                    cwd=repository_root,
                    check=True,
                    capture_output=True,
                    text=True,
                ).stdout.strip()

                if dirty_kind == "staged":
                    (repository_root / "staged.cs").write_text(
                        "internal sealed class Staged {}\n", encoding="utf-8"
                    )
                    subprocess.run(
                        ["git", "add", "staged.cs"],
                        cwd=repository_root,
                        check=True,
                    )
                elif dirty_kind == "unstaged":
                    tracked_path.write_text("changed\n", encoding="utf-8")
                else:
                    (repository_root / "untracked.cs").write_text(
                        "internal sealed class Untracked {}\n", encoding="utf-8"
                    )

                result = self.run_powershell(
                    script_path,
                    "-Version",
                    "1.0.0",
                    "-Commit",
                    repository_head,
                    "-ExternalToolPolicyDryRun",
                )

                self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertIn(
                    "Release packaging requires a clean repository worktree and index",
                    normalize_console_output(result.stdout + result.stderr),
                )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_packager_fails_closed_when_repository_head_is_unavailable(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="nvt-package-missing-git-"
        ) as temporary_directory:
            repository_root = Path(temporary_directory)
            script_path = repository_root / "scripts" / "package.ps1"
            script_path.parent.mkdir(parents=True)
            shutil.copy2(PACKAGE_SCRIPT, script_path)
            (repository_root / "VERSION").write_text("1.0.0\n", encoding="utf-8")

            result = self.run_powershell(
                script_path,
                "-Version",
                "1.0.0",
                "-Commit",
                "1" * 40,
                "-ExternalToolPolicyDryRun",
            )

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(
            "Repository HEAD could not be resolved",
            normalize_console_output(result.stdout + result.stderr),
        )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_packager_identity_mismatch_preserves_existing_artifacts(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="nvt-package-preserve-artifacts-"
        ) as temporary_directory:
            repository_root = Path(temporary_directory)
            script_path = repository_root / "scripts" / "package.ps1"
            script_path.parent.mkdir(parents=True)
            shutil.copy2(PACKAGE_SCRIPT, script_path)
            (repository_root / "VERSION").write_text("1.0.0\n", encoding="utf-8")
            artifact_path = repository_root / "artifacts" / "release" / "existing.txt"
            artifact_path.parent.mkdir(parents=True)
            artifact_path.write_text("preserve\n", encoding="utf-8")

            result = self.run_powershell(
                script_path,
                "-Version",
                "1.0.1",
                "-Commit",
                "1" * 40,
                "-ExternalToolPolicyDryRun",
            )

            self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertEqual("preserve\n", artifact_path.read_text(encoding="utf-8"))

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_repository_version_mismatch_does_not_attach_snapshot_worktree(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory(
            prefix=".nfc-package-mismatch-", dir=ROOT.parent
        ) as temporary_directory:
            temporary_root = Path(temporary_directory)
            invocation_root = temporary_root / "invocation"
            invocation_root.mkdir()
            repository_head = initialize_minimal_package_repository(invocation_root)
            environment = os.environ.copy()
            environment["TEMP"] = str(temporary_root / "runtime-temp")
            environment["TMP"] = environment["TEMP"]
            Path(environment["TEMP"]).mkdir()

            result = subprocess.run(
                [
                    str(POWERSHELL),
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    str(invocation_root / "scripts" / "package.ps1"),
                    "-Version",
                    "1.0.1",
                    "-Commit",
                    repository_head,
                    "-ExternalToolPolicyDryRun",
                ],
                cwd=invocation_root,
                check=False,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                env=environment,
            )

            self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertIn(
                "does not match repository VERSION '1.0.0'",
                normalize_console_output(result.stdout + result.stderr),
            )
            self.assertEqual([], list(temporary_root.glob(".nfcps-*")))
            worktree_list = subprocess.run(
                ["git", "worktree", "list", "--porcelain"],
                cwd=invocation_root,
                check=True,
                capture_output=True,
                text=True,
            ).stdout
            self.assertNotIn(".nfcps-", worktree_list)

    @unittest.skipUnless(
        POWERSHELL and os.name == "nt",
        "Windows PowerShell and command-wrapper semantics are required",
    )
    def test_snapshot_remove_failure_fails_package_and_preserves_evidence(self) -> None:
        actual_git = shutil.which("git")
        self.assertIsNotNone(actual_git)
        with tempfile.TemporaryDirectory(
            prefix=".npr-", dir=ROOT.parent
        ) as temporary_directory:
            temporary_root = Path(temporary_directory)
            invocation_root = temporary_root / "i"
            subprocess.run(
                [
                    str(actual_git),
                    "-C",
                    str(ROOT),
                    "worktree",
                    "add",
                    "--detach",
                    str(invocation_root),
                    "HEAD",
                ],
                check=True,
                capture_output=True,
                text=True,
            )
            repository_head = subprocess.run(
                [str(actual_git), "rev-parse", "HEAD"],
                cwd=invocation_root,
                check=True,
                capture_output=True,
                text=True,
            ).stdout.strip()
            repository_version = (
                (invocation_root / "VERSION").read_text(encoding="utf-8").strip()
            )
            wrapper_root = temporary_root / "wrapper"
            wrapper_root.mkdir()
            git_wrapper = wrapper_root / "git.cmd"
            git_wrapper.write_text(
                "@echo off\r\n"
                'if /I "%~3"=="worktree" if /I "%~4"=="remove" exit /b 42\r\n'
                f'"{actual_git}" %*\r\n'
                "exit /b %ERRORLEVEL%\r\n",
                encoding="ascii",
            )
            runtime_temp = temporary_root / "runtime-temp"
            runtime_temp.mkdir()
            environment = os.environ.copy()
            environment["PATH"] = str(wrapper_root) + os.pathsep + environment["PATH"]
            environment["TEMP"] = str(runtime_temp)
            environment["TMP"] = str(runtime_temp)
            snapshot_roots: list[Path] = []
            try:
                result = subprocess.run(
                    [
                        str(POWERSHELL),
                        "-NoProfile",
                        "-ExecutionPolicy",
                        "Bypass",
                        "-File",
                        str(invocation_root / "scripts" / "package.ps1"),
                        "-Version",
                        repository_version,
                        "-Commit",
                        repository_head,
                        "-ExternalToolPolicyDryRun",
                    ],
                    cwd=invocation_root,
                    check=False,
                    capture_output=True,
                    text=True,
                    encoding="utf-8",
                    errors="replace",
                    env=environment,
                )

                self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
                normalized_output = normalize_console_output(
                    result.stdout + result.stderr
                )
                self.assertIn(
                    "External-tool package policy dry-run passed",
                    normalized_output,
                )
                self.assertIn("Exact source snapshot cleanup failed", normalized_output)
                self.assertIn("preserved for inspection at", normalized_output)
                snapshot_roots = list(temporary_root.glob(".nfcps-*"))
                self.assertEqual(1, len(snapshot_roots))
            finally:
                for snapshot_root in snapshot_roots:
                    subprocess.run(
                        [
                            str(actual_git),
                            "-C",
                            str(invocation_root),
                            "worktree",
                            "remove",
                            "--force",
                            str(snapshot_root),
                        ],
                        check=False,
                        capture_output=True,
                        text=True,
                    )
                subprocess.run(
                    [
                        str(actual_git),
                        "-C",
                        str(ROOT),
                        "worktree",
                        "remove",
                        "--force",
                        str(invocation_root),
                    ],
                    check=False,
                    capture_output=True,
                    text=True,
                )
                subprocess.run(
                    [str(actual_git), "-C", str(ROOT), "worktree", "prune"],
                    check=False,
                    capture_output=True,
                    text=True,
                )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_packager_uses_exact_snapshot_when_invocation_tree_changes_after_preflight(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory(
            prefix=".nfc-package-race-", dir=ROOT.parent
        ) as temporary_directory:
            temporary_root = Path(temporary_directory)
            invocation_root = temporary_root / "invocation"
            runtime_temp = temporary_root / "runtime-temp"
            runtime_temp.mkdir()
            subprocess.run(
                [
                    "git",
                    "-C",
                    str(ROOT),
                    "worktree",
                    "add",
                    "--detach",
                    str(invocation_root),
                    "HEAD",
                ],
                check=True,
                capture_output=True,
                text=True,
            )

            process: subprocess.Popen[str] | None = None
            source_policy = invocation_root / CAPABILITY_POLICY_RELATIVE_PATH
            source_policy_bytes = b""
            snapshot_root: Path | None = None
            try:
                repository_version = (
                    (invocation_root / "VERSION").read_text(encoding="utf-8").strip()
                )
                repository_head = subprocess.run(
                    ["git", "rev-parse", "HEAD"],
                    cwd=invocation_root,
                    check=True,
                    capture_output=True,
                    text=True,
                ).stdout.strip()
                environment = os.environ.copy()
                environment["TEMP"] = str(runtime_temp)
                environment["TMP"] = str(runtime_temp)
                process = subprocess.Popen(
                    [
                        str(POWERSHELL),
                        "-NoProfile",
                        "-ExecutionPolicy",
                        "Bypass",
                        "-File",
                        str(invocation_root / "scripts" / "package.ps1"),
                        "-Version",
                        repository_version,
                        "-Commit",
                        repository_head,
                        "-ExternalToolPolicyDryRun",
                    ],
                    cwd=invocation_root,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.PIPE,
                    text=True,
                    encoding="utf-8",
                    errors="replace",
                    env=environment,
                )

                deadline = time.monotonic() + 90
                while time.monotonic() < deadline and process.poll() is None:
                    candidates = sorted(temporary_root.glob(".nfcps-*"))
                    snapshot_root = next(
                        (
                            candidate
                            for candidate in candidates
                            if (candidate / "VERSION").is_file()
                        ),
                        None,
                    )
                    if snapshot_root is not None:
                        break
                    time.sleep(0.01)

                self.assertIsNotNone(
                    snapshot_root,
                    "packager did not materialize an exact source snapshot",
                )
                source_policy_bytes = source_policy.read_bytes()
                source_policy.write_text("{}\n", encoding="utf-8")

                stdout, stderr = process.communicate(timeout=180)
                self.assertEqual(0, process.returncode, stdout + stderr)
                self.assertIn(
                    "External-tool package policy dry-run passed",
                    normalize_console_output(stdout + stderr),
                )
                self.assertFalse(
                    snapshot_root.exists(),
                    "packager did not remove its exact source snapshot",
                )
            finally:
                if source_policy_bytes:
                    source_policy.write_bytes(source_policy_bytes)
                if process is not None and process.poll() is None:
                    process.kill()
                    process.communicate()
                subprocess.run(
                    [
                        "git",
                        "-C",
                        str(ROOT),
                        "worktree",
                        "remove",
                        "--force",
                        str(invocation_root),
                    ],
                    check=False,
                    capture_output=True,
                    text=True,
                )
                subprocess.run(
                    ["git", "-C", str(ROOT), "worktree", "prune"],
                    check=False,
                    capture_output=True,
                    text=True,
                )

    def test_release_workflows_smoke_package_before_distribution(self) -> None:
        for workflow_path in (
            ROOT / ".github/workflows/release.yml",
            ROOT / ".github/workflows/main-package.yml",
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

    def test_distribution_launcher_packager_uses_canonical_package_entrypoint(
        self,
    ) -> None:
        release_workflow_path = ROOT / ".github/workflows/release.yml"
        self.assertEqual(
            "6c36f140c5282878ec3cc1a7b3de78ddf5bf86e29d951d57662c5fed19df0492",
            hashlib.sha256(release_workflow_path.read_bytes()).hexdigest(),
            "the historical release workflow must remain byte-exact",
        )
        for workflow_path in (ROOT / ".github/workflows").glob("*.yml"):
            self.assertNotIn(
                "package-distribution-launcher.ps1",
                workflow_path.read_text(encoding="utf-8"),
                workflow_path,
            )

        package_script = PACKAGE_SCRIPT.read_text(encoding="utf-8")
        launcher_index = package_script.index(
            "$DistributionLauncherPackager = Join-Path"
        )
        cleanup_index = package_script.index(
            "worktree remove --force $SourceSnapshotRoot"
        )
        dry_run_index = package_script.index("if ($ExternalToolPolicyDryRun)")
        dry_run_return_index = package_script.index("    return", dry_run_index)
        self.assertLess(dry_run_index, dry_run_return_index)
        self.assertLess(dry_run_return_index, cleanup_index)
        self.assertLess(cleanup_index, launcher_index)
        self.assertEqual(
            1,
            package_script.count("scripts/package-distribution-launcher.ps1"),
        )
        launcher_block = package_script[cleanup_index:]
        self.assertIn(
            "if (-not $AllowPrerelease -and "
            "[version]$SemanticVersion -ge [version]'1.0.6')",
            launcher_block,
        )
        self.assertIn("$InvocationRepoRoot", launcher_block)
        self.assertIn("-Version $Version", launcher_block)
        self.assertIn("-Commit $Commit", launcher_block)
        self.assertIn("-ReleaseDisposition unsigned-owner-approved", launcher_block)
        self.assertLess(
            launcher_block.index("-ge [version]'1.0.6'"),
            launcher_block.index("& $DistributionLauncherPackager"),
        )

        script = LAUNCHER_PACKAGE_SCRIPT.read_text(encoding="utf-8")
        self.assertIn("artifacts/installer-work", script)
        self.assertIn("--extract-release-payload", script)
        self.assertIn("installer-release-manifest-v1.schema.json", script)
        self.assertIn("managed-setup-payload-admission-v1.schema.json", script)
        self.assertIn("$AssetNames = @(", script)
        self.assertIn("$LauncherName", script)
        self.assertIn("$ManifestName", script)
        self.assertIn("$SbomName", script)
        self.assertIn("$ProvenanceName", script)
        self.assertIn("$ChecksumName", script)
        self.assertNotIn("update-catalog", script.lower())
        self.assertNotIn(
            "Remove-Item -LiteralPath $ReleaseRoot -Recurse",
            script,
        )

    def test_managed_launcher_restore_uses_committed_rid_lock_graph(self) -> None:
        script = PACKAGE_SCRIPT.read_text(encoding="utf-8")

        restore_lines = [
            line.strip()
            for line in script.splitlines()
            if line.strip().startswith("& $DotNet restore $LauncherProject")
        ]
        self.assertEqual(
            [
                "& $DotNet restore $LauncherProject -r win-x64 "
                "--locked-mode --disable-parallel"
            ],
            restore_lines,
        )

    def test_distribution_launcher_restore_uses_committed_rid_lock_graph(self) -> None:
        script = LAUNCHER_PACKAGE_SCRIPT.read_text(encoding="utf-8")

        self.assertEqual(2, script.count("& $DotNet restore"))
        self.assertIn(
            "& $DotNet restore $BootstrapProject -r win-x64 --locked-mode --disable-parallel",
            script,
        )
        self.assertIn(
            "& $DotNet restore $LauncherProject -r win-x64 --locked-mode --disable-parallel",
            script,
        )
        self.assertIn("[IO.Directory]::Delete($CanonicalSnapshotRoot, $true)", script)
        self.assertIn("^\\.nfcl-[0-9a-f]{12}$", script)
        self.assertIn("-c core.quotePath=false", script)
        self.assertIn("stop-idle-build-workers.ps1", script)
        self.assertIn("$CleanupAttempt -le 5", script)
        self.assertIn("$ExtractionProcess = Start-Process", script)
        self.assertIn("-Wait `", script)
        self.assertIn("-WindowStyle Hidden", script)
        self.assertNotIn("& $LauncherPath '--extract-release-payload'", script)

    def test_distribution_launcher_committed_rid_locks_preserve_package_identities(
        self,
    ) -> None:
        lock_paths = (
            "src/NvtFwCombiner.Bootstrap/packages.lock.json",
            "src/NvtFwCombiner.DistributionLauncher/packages.lock.json",
            "src/NvtFwCombiner.Infrastructure/packages.lock.json",
            "src/NvtFwCombiner.LauncherBootstrap/packages.lock.json",
            "src/NvtFwCombiner.Platform/packages.lock.json",
            "src/NvtFwCombiner.VersionManagement.Infrastructure/packages.lock.json",
        )
        for relative_path in lock_paths:
            with self.subTest(path=relative_path):
                lock = json.loads((ROOT / relative_path).read_text(encoding="utf-8"))
                targets = lock["dependencies"]
                base = targets["net10.0"]
                rid = targets["net10.0/win-x64"]
                for package_name, rid_identity in rid.items():
                    self.assertIn(package_name, base)
                    self.assertEqual(
                        rid_identity.get("resolved"),
                        base[package_name].get("resolved"),
                    )
                    self.assertEqual(
                        rid_identity.get("contentHash"),
                        base[package_name].get("contentHash"),
                    )

    @unittest.skipUnless(DOTNET, ".NET SDK is required for RID restore evidence")
    def test_distribution_launcher_rid_restore_keeps_invocation_locks_immutable(
        self,
    ) -> None:
        lock_bytes = {
            path.relative_to(ROOT): path.read_bytes()
            for path in ROOT.glob("src/**/packages.lock.json")
        }
        self.assertTrue(lock_bytes)

        with tempfile.TemporaryDirectory(
            prefix=".nfcl-rid-restore-", dir=ROOT.parent
        ) as temporary_directory:
            snapshot_root = Path(temporary_directory) / "snapshot"
            subprocess.run(
                [
                    "git",
                    "-C",
                    str(ROOT),
                    "worktree",
                    "add",
                    "--detach",
                    str(snapshot_root),
                    "HEAD",
                ],
                check=True,
                capture_output=True,
                text=True,
            )
            try:
                snapshot_lock_bytes = {
                    path.relative_to(snapshot_root): path.read_bytes()
                    for path in snapshot_root.glob("src/**/packages.lock.json")
                }
                snapshot_lock_graph = {
                    path.relative_to(snapshot_root): json.loads(
                        path.read_text(encoding="utf-8")
                    )
                    for path in snapshot_root.glob("src/**/packages.lock.json")
                }
                self.assertEqual(
                    {
                        path: json.loads((ROOT / path).read_text(encoding="utf-8"))
                        for path in lock_bytes
                    },
                    snapshot_lock_graph,
                )
                for project in (
                    "src/NvtFwCombiner.LauncherBootstrap/NvtFwCombiner.LauncherBootstrap.csproj",
                    "src/NvtFwCombiner.DistributionLauncher/NvtFwCombiner.DistributionLauncher.csproj",
                ):
                    result = subprocess.run(
                        [
                            str(DOTNET),
                            "restore",
                            str(snapshot_root / project),
                            "-r",
                            "win-x64",
                            "--locked-mode",
                            "--disable-parallel",
                        ],
                        check=False,
                        capture_output=True,
                        text=True,
                        encoding="utf-8",
                        errors="replace",
                    )
                    self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                    self.assertEqual(
                        lock_bytes,
                        {
                            path: (ROOT / path).read_bytes()
                            for path in lock_bytes
                        },
                    )

                self.assertEqual(
                    snapshot_lock_bytes,
                    {
                        path.relative_to(snapshot_root): path.read_bytes()
                        for path in snapshot_root.glob("src/**/packages.lock.json")
                    },
                )
                self.assertEqual(
                    snapshot_lock_graph,
                    {
                        path.relative_to(snapshot_root): json.loads(
                            path.read_text(encoding="utf-8")
                        )
                        for path in snapshot_root.glob("src/**/packages.lock.json")
                    },
                )
            finally:
                subprocess.run(
                    [
                        "git",
                        "-C",
                        str(ROOT),
                        "worktree",
                        "remove",
                        "--force",
                        str(snapshot_root),
                    ],
                    check=True,
                    capture_output=True,
                    text=True,
                )
                subprocess.run(
                    ["git", "-C", str(ROOT), "worktree", "prune"],
                    check=True,
                    capture_output=True,
                    text=True,
                )

            self.assertFalse(snapshot_root.exists())
            self.assertEqual(
                lock_bytes,
                {path: (ROOT / path).read_bytes() for path in lock_bytes},
            )
            worktrees = subprocess.run(
                ["git", "-C", str(ROOT), "worktree", "list", "--porcelain"],
                check=True,
                capture_output=True,
                text=True,
            ).stdout
            self.assertNotIn(str(snapshot_root), worktrees)

    @unittest.skipUnless(
        PWSH and os.name == "nt",
        "PowerShell 7 and Windows command wrappers are required",
    )
    def test_distribution_launcher_cleanup_preserves_registered_snapshot_only(
        self,
    ) -> None:
        actual_git = shutil.which("git")
        self.assertIsNotNone(actual_git)

        for cleanup_mode in (
            "still-registered",
            "stopper-and-registered",
            "detached-residue",
            "transient-lock",
            "permanent-lock",
            "stopper-failure",
            "unicode-path",
        ):
            with (
                self.subTest(cleanup_mode=cleanup_mode),
                tempfile.TemporaryDirectory(
                    prefix=(
                        ".nfcl-路徑-policy-"
                        if cleanup_mode == "unicode-path"
                        else ".nfcl-cleanup-policy-"
                    ),
                    dir=ROOT.parent,
                ) as temporary_directory,
            ):
                temporary_root = Path(temporary_directory)
                invocation_root = temporary_root / "invocation"
                invocation_root.mkdir()
                repository_head = initialize_minimal_launcher_package_repository(
                    invocation_root,
                    stopper_fails=cleanup_mode
                    in ("stopper-failure", "stopper-and-registered"),
                )
                wrapper_root = temporary_root / "wrapper"
                wrapper_root.mkdir()
                unrelated_root = temporary_root / "unrelated"
                unrelated_root.mkdir()
                (unrelated_root / "sentinel.txt").write_text(
                    "preserve",
                    encoding="ascii",
                )
                (wrapper_root / "dotnet.cmd").write_text(
                    "@echo off\r\n"
                    'if /I "%~1"=="build-server" exit /b 0\r\n'
                    "exit /b 42\r\n",
                    encoding="ascii",
                )
                lock_release = threading.Event()
                lock_thread: threading.Thread | None = None
                lock_errors: list[BaseException] = []
                if cleanup_mode in ("transient-lock", "permanent-lock"):
                    lock_target = wrapper_root / f"{cleanup_mode}.target"
                    lock_ready = wrapper_root / f"{cleanup_mode}.ready"
                    lock_done = wrapper_root / f"{cleanup_mode}.done"

                    def hold_residual_without_delete_share() -> None:
                        import ctypes
                        from ctypes import wintypes

                        try:
                            deadline = time.monotonic() + 10
                            while not lock_target.exists():
                                if time.monotonic() >= deadline:
                                    raise TimeoutError("Lock target was not published.")
                                time.sleep(0.01)
                            target = lock_target.read_text(encoding="utf-8").strip()
                            kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
                            kernel32.CreateFileW.argtypes = (
                                wintypes.LPCWSTR,
                                wintypes.DWORD,
                                wintypes.DWORD,
                                wintypes.LPVOID,
                                wintypes.DWORD,
                                wintypes.DWORD,
                                wintypes.HANDLE,
                            )
                            kernel32.CreateFileW.restype = wintypes.HANDLE
                            kernel32.CloseHandle.argtypes = (wintypes.HANDLE,)
                            kernel32.CloseHandle.restype = wintypes.BOOL
                            handle = kernel32.CreateFileW(
                                target,
                                0x80000000 | 0x40000000,
                                0x1 | 0x2,
                                None,
                                3,
                                0x80,
                                None,
                            )
                            if handle == ctypes.c_void_p(-1).value:
                                raise ctypes.WinError(ctypes.get_last_error())
                            try:
                                lock_ready.write_text("ready", encoding="ascii")
                                lock_release.wait(
                                    0.45
                                    if cleanup_mode == "transient-lock"
                                    else 10
                                )
                            finally:
                                kernel32.CloseHandle(handle)
                                lock_done.write_text("done", encoding="ascii")
                        except BaseException as error:  # noqa: BLE001
                            lock_errors.append(error)
                            lock_ready.write_text("error", encoding="ascii")

                    lock_thread = threading.Thread(
                        target=hold_residual_without_delete_share,
                        daemon=True,
                    )
                    lock_thread.start()
                git_wrapper = wrapper_root / "git.cmd"
                if cleanup_mode in ("still-registered", "stopper-and-registered"):
                    remove_body = "exit /b 42\r\n"
                elif cleanup_mode == "detached-residue":
                    remove_body = (
                        f'"{actual_git}" %*\r\n'
                        "if errorlevel 1 exit /b 43\r\n"
                        'mkdir "%~6"\r\n'
                        '>"%~6\\residual.txt" echo residual\r\n'
                        "exit /b 42\r\n"
                    )
                elif cleanup_mode in ("transient-lock", "permanent-lock"):
                    remove_body = (
                        f'"{actual_git}" %*\r\n'
                        "if errorlevel 1 exit /b 43\r\n"
                        'mkdir "%~6"\r\n'
                        '>"%~6\\residual.txt" echo residual\r\n'
                        f'>"{lock_target}" echo %~6\\residual.txt\r\n'
                        f'"{PWSH}" -NoProfile -Command '
                        f"\"while (-not (Test-Path -LiteralPath '{lock_ready}')) "
                        '{ Start-Sleep -Milliseconds 10 }"\r\n'
                        "exit /b 42\r\n"
                    )
                else:
                    remove_body = (
                        f'"{actual_git}" %*\r\n'
                        "exit /b %ERRORLEVEL%\r\n"
                    )
                git_wrapper.write_text(
                    "@echo off\r\n"
                    'if /I "%~3"=="worktree" if /I "%~4"=="remove" (\r\n'
                    f"{remove_body}"
                    ")\r\n"
                    f'"{actual_git}" %*\r\n'
                    "exit /b %ERRORLEVEL%\r\n",
                    encoding="ascii",
                )
                environment = os.environ.copy()
                environment["PATH"] = (
                    str(wrapper_root) + os.pathsep + environment["PATH"]
                )
                snapshot_roots: list[Path] = []
                try:
                    result = subprocess.run(
                        [
                            str(PWSH),
                            "-NoProfile",
                            "-File",
                            str(
                                invocation_root
                                / "scripts"
                                / "package-distribution-launcher.ps1"
                            ),
                            "-Version",
                            "1.0.6",
                            "-Commit",
                            repository_head,
                            "-ReleaseDisposition",
                            "unsigned-owner-approved",
                        ],
                        cwd=invocation_root,
                        check=False,
                        capture_output=True,
                        text=True,
                        encoding="utf-8",
                        errors="replace",
                        env=environment,
                    )
                    output = normalize_console_output(result.stdout + result.stderr)
                    self.assertNotEqual(0, result.returncode, output)
                    snapshot_roots = list(temporary_root.glob(".nfcl-*"))
                    worktrees = subprocess.run(
                        [str(actual_git), "worktree", "list", "--porcelain"],
                        cwd=invocation_root,
                        check=True,
                        capture_output=True,
                        text=True,
                        encoding="utf-8",
                        errors="replace",
                    ).stdout
                    if lock_thread is not None:
                        self.assertEqual("ready", lock_ready.read_text(encoding="ascii"))
                        self.assertEqual([], lock_errors)

                    if cleanup_mode in ("still-registered", "stopper-and-registered"):
                        self.assertIn("remains registered", output)
                        if cleanup_mode == "stopper-and-registered":
                            self.assertIn("Injected stopper failure", output)
                            self.assertIn("Distribution Bootstrap restore failed", output)
                        self.assertEqual(1, len(snapshot_roots))
                        self.assertIn(
                            str(snapshot_roots[0]).replace("\\", "/"), worktrees
                        )
                    elif cleanup_mode in ("detached-residue", "transient-lock"):
                        self.assertIn("Distribution Bootstrap restore failed", output)
                        self.assertNotIn("remains registered", output)
                        self.assertEqual([], snapshot_roots)
                        self.assertEqual(1, worktrees.count("worktree "))
                        self.assertIn(
                            str(invocation_root).replace("\\", "/"), worktrees
                        )
                    elif cleanup_mode == "permanent-lock":
                        self.assertTrue(lock_ready.exists())
                        self.assertFalse(lock_done.exists())
                        self.assertIn("Distribution Bootstrap restore failed", output)
                        self.assertIn("multiple failures", output)
                        self.assertEqual(1, len(snapshot_roots))
                        self.assertNotIn(
                            str(snapshot_roots[0]).replace("\\", "/"), worktrees
                        )
                        self.assertEqual(1, worktrees.count("worktree "))
                    else:
                        if cleanup_mode == "stopper-failure":
                            self.assertIn("Injected stopper failure", output)
                            self.assertIn("Distribution Bootstrap restore failed", output)
                        else:
                            self.assertIn("Distribution Bootstrap restore failed", output)
                        self.assertEqual([], snapshot_roots)
                        self.assertEqual(1, worktrees.count("worktree "))
                        self.assertIn(
                            str(invocation_root).replace("\\", "/"), worktrees
                        )
                    self.assertEqual(
                        "preserve",
                        (unrelated_root / "sentinel.txt").read_text(encoding="ascii"),
                    )
                finally:
                    if lock_thread is not None:
                        lock_release.set()
                        lock_thread.join(timeout=5)
                        self.assertFalse(lock_thread.is_alive())
                        self.assertEqual([], lock_errors)
                    for snapshot_root in snapshot_roots:
                        subprocess.run(
                            [
                                str(actual_git),
                                "worktree",
                                "remove",
                                "--force",
                                str(snapshot_root),
                            ],
                            cwd=invocation_root,
                            check=False,
                            capture_output=True,
                            text=True,
                        )
                        if snapshot_root.exists():
                            shutil.rmtree(snapshot_root)
                    subprocess.run(
                        [str(actual_git), "worktree", "prune"],
                        cwd=invocation_root,
                        check=False,
                        capture_output=True,
                        text=True,
                    )

    def test_stable_release_is_ci_owned_and_main_preview_is_manual(self) -> None:
        release_workflow = (ROOT / ".github/workflows/release.yml").read_text(
            encoding="utf-8"
        )
        main_package_workflow = (ROOT / ".github/workflows/main-package.yml").read_text(
            encoding="utf-8"
        )

        self.assertIn("Exact reviewed release-branch head", release_workflow)
        self.assertIn("source_branch:", release_workflow)
        self.assertIn("- 0.9.17", release_workflow)
        self.assertIn("- 0.9.18", release_workflow)
        self.assertIn("- 0.9.19", release_workflow)
        self.assertIn(
            "NFC_RELEASE_SOURCE_BRANCH -notin @('main', '0.9.17', '0.9.18', '0.9.19')",
            release_workflow,
        )
        self.assertIn("'0.9.17' = '0.9.17'", release_workflow)
        self.assertIn("'0.9.18' = '0.9.18'", release_workflow)
        self.assertIn("'0.9.19' = '0.9.19'", release_workflow)
        self.assertIn(
            "$approvedMaintenanceVersions[$env:NFC_SOURCE_BRANCH] -ne $version",
            release_workflow,
        )
        self.assertIn(
            "permissions:\n  actions: read\n  contents: read",
            release_workflow,
        )
        self.assertIn(
            "candidate:\n"
            "    name: release / candidate\n"
            "    runs-on: windows-latest\n"
            "    timeout-minutes: 60\n"
            "    permissions:\n"
            "      contents: read\n"
            "      pull-requests: read\n"
            "      issues: read\n"
            "      checks: read\n"
            "      statuses: read",
            release_workflow,
        )
        self.assertIn("environment: release", release_workflow)
        self.assertIn("contents: write", release_workflow)
        self.assertIn("scripts/render_release_notes.py", release_workflow)
        self.assertIn(
            "python $env:NFC_RELEASE_POLICY validate-context", release_workflow
        )
        self.assertIn("owner_self_approval_exception:", release_workflow)
        self.assertIn(
            "NFC_REPOSITORY_OWNER: ${{ github.repository_owner }}", release_workflow
        )
        self.assertIn("NFC_WORKFLOW_ACTOR: ${{ github.actor }}", release_workflow)
        self.assertIn("--owner-self-approval-exception", release_workflow)
        self.assertIn("$codexReviewer = 'chatgpt-codex-connector'", release_workflow)
        self.assertIn("Get-NormalizedReviewer", release_workflow)
        self.assertIn("function Get-PaginatedGitHubArray", release_workflow)
        self.assertIn(
            "$pages = $pagesText | ConvertFrom-Json -NoEnumerate", release_workflow
        )
        self.assertIn("foreach ($page in $pages)", release_workflow)
        self.assertIn("foreach ($item in $page)", release_workflow)
        self.assertEqual(
            1, release_workflow.count("gh api --paginate --slurp $endpoint")
        )
        self.assertNotIn("--jq 'add'", release_workflow)
        self.assertIn("$requiredCheckNames = @(", release_workflow)
        for required_check in (
            "policy / polytail",
            "python-worker / verify",
            "dotnet / build-test",
        ):
            self.assertIn(required_check, release_workflow)
        self.assertIn(
            "commits/$($pr.headRefOid)/check-runs?per_page=100", release_workflow
        )
        self.assertIn("$_.app.slug -eq 'github-actions'", release_workflow)
        self.assertNotIn("gh pr checks", release_workflow)
        self.assertIn("pulls/$env:NFC_PULL_REQUEST/comments", release_workflow)
        self.assertIn("issues/$env:NFC_PULL_REQUEST/comments", release_workflow)
        self.assertIn("reviewedCommitPrefix", release_workflow)
        self.assertIn("$reviewedCommitPattern =", release_workflow)
        self.assertIn("[regex]::IsMatch(", release_workflow)
        self.assertNotIn("$reviewedCommitMarker =", release_workflow)
        self.assertEqual(1, release_workflow.count("git rev-parse 'HEAD^{tree}'"))
        self.assertNotIn("git rev-parse HEAD^{tree}", release_workflow)
        self.assertIn("codexReview = if ($codexReview.Count -eq 1)", release_workflow)
        self.assertIn(
            "NFC_RELEASE_POLICY: ./scripts/release_promotion_policy.py",
            release_workflow,
        )
        self.assertIn(
            "python $env:NFC_RELEASE_POLICY validate-promotion-source",
            release_workflow,
        )
        self.assertIn("$env:NFC_RELEASE_POLICY validate-tag", release_workflow)
        self.assertIn("$env:NFC_RELEASE_POLICY validate-release", release_workflow)
        self.assertIn("$env:NFC_RELEASE_POLICY create-manifest", release_workflow)
        self.assertIn("$env:NFC_RELEASE_POLICY verify-manifest", release_workflow)
        self.assertIn("$env:NFC_RELEASE_POLICY plan-recovery", release_workflow)
        self.assertIn("review-snapshot.json", release_workflow)
        self.assertIn("artifact-digest", release_workflow)
        self.assertIn("git/tags", release_workflow)
        self.assertIn("git/refs", release_workflow)
        self.assertIn("actions/download-artifact@", release_workflow)
        self.assertIn("gh release download", release_workflow)
        self.assertNotIn("--generate-notes", release_workflow)
        self.assertNotIn("pull_request_target", release_workflow)
        self.assertFalse(
            any(
                "${{ inputs." in block for block in literal_run_blocks(release_workflow)
            ),
            "dispatch inputs must enter PowerShell only through validated environment variables",
        )
        self.assertNotIn("branches: [main]", main_package_workflow)
        self.assertNotIn("gh release", main_package_workflow)
        first_policy_call = release_workflow.index("release_promotion_policy.py")
        self.assertLess(
            release_workflow.index("actions/setup-python@"),
            first_policy_call,
            "release-authoritative Python policy must use the pinned interpreter",
        )

    def test_stable_release_emits_separate_update_source_handoff(self) -> None:
        release = (ROOT / ".github/workflows/release.yml").read_text(encoding="utf-8")
        candidate_start = release.index("\n  candidate:")
        promote_start = release.index("\n  promote:")
        smoke_start = release.index("\n  published-smoke:")
        candidate = release[candidate_start:promote_start]
        promote = release[promote_start:smoke_start]

        self.assertIn("published_at:", release)
        self.assertIn("NFC_RELEASE_PUBLISHED_AT: ${{ inputs.published_at }}", release)
        self.assertIn("-cnotmatch", candidate)
        self.assertIn("published-at=$env:NFC_RELEASE_PUBLISHED_AT", candidate)

        setup_python = candidate.index("Setup pinned Python for release policy")
        helper_copy = candidate.index(
            "Copy-Item -LiteralPath ./scripts/create_update_catalog.py"
        )
        registry_policy_copy = candidate.index(
            "Copy-Item -LiteralPath ./scripts/update_source_registry_policy.py"
        )
        detach = candidate.index("git checkout --detach")
        package = candidate.index("Build closed-allowlist release package")
        smoke = candidate.index("Smoke candidate package")
        notes = candidate.index("Render complete release notes from CHANGELOG")
        handoff = candidate.index("Create single-version update-source handoff")
        manifest = candidate.index(
            "Create closed candidate manifest and outer checksums"
        )
        candidate_upload = candidate.index("Upload immutable candidate assets")
        handoff_upload = candidate.index("Upload update-source handoff")

        self.assertLess(setup_python, helper_copy)
        self.assertLess(helper_copy, registry_policy_copy)
        self.assertLess(registry_policy_copy, detach)
        self.assertLess(helper_copy, detach)
        self.assertLess(package, smoke)
        self.assertLess(smoke, notes)
        self.assertLess(notes, handoff)
        self.assertLess(handoff, manifest)
        self.assertLess(manifest, candidate_upload)
        self.assertLess(candidate_upload, handoff_upload)

        self.assertIn("NFC_UPDATE_CATALOG_TOOL", candidate)
        self.assertIn("NFC_UPDATE_SOURCE_REGISTRY_TEMPLATE", candidate)
        self.assertIn("artifacts/update-source-handoff", candidate)
        self.assertIn("'artifacts/update-source-handoff'", candidate)
        self.assertIn("'packages'", candidate)
        self.assertIn("NvtFwCombiner-$env:NFC_TAG-win-x64.zip", candidate)
        self.assertIn("'--source-root'", candidate)
        self.assertIn("'--published-at'", candidate)
        self.assertIn("'--release-notes-file'", candidate)
        self.assertIn("'--manifest-copy'", candidate)
        self.assertIn("'RELEASE-MANIFEST.json'", candidate)
        self.assertIn("update-catalog.v1.json", candidate)
        self.assertIn("update-source-registry.json", candidate)
        self.assertIn(
            "Copy-Item -LiteralPath ./docs/ci/update-source-registry.json.in",
            candidate,
        )
        self.assertIn("--registry-template", candidate)
        self.assertIn("--registry-revision", candidate)
        self.assertIn("$env:GITHUB_RUN_ID", candidate)

        self.assertEqual(2, candidate.count("actions/upload-artifact@"))
        self.assertEqual(6, release.count("actions/upload-artifact@"))
        self.assertIn("path: artifacts/release/*", candidate)
        self.assertIn("path: artifacts/update-source-handoff/*", candidate)
        self.assertNotIn("update-source-handoff", promote)
        self.assertNotIn("update-catalog.v1.json", promote)
        self.assertNotIn("update-source-registry.json", promote)
        self.assertIn(
            "$registryPath = Join-Path $handoffRoot 'update-source-registry.json'",
            candidate,
        )
        self.assertNotIn(
            "update-source-registry.json",
            candidate[manifest:],
        )
        self.assertIn(
            "$expectedNames = @($manifest.assets.name) + "
            "@($env:NFC_MANIFEST_NAME, $checksumName)",
            promote,
        )

    def test_update_source_registry_seed_is_the_owner_approved_default_root(
        self,
    ) -> None:
        registry = json.loads(
            UPDATE_SOURCE_REGISTRY_TEMPLATE.read_text(encoding="utf-8")
        )

        self.assertEqual(1, registry["schemaVersion"])
        self.assertEqual("nvt-fw-combiner-production", registry["registryId"])
        self.assertEqual(0, registry["registryRevision"])
        self.assertEqual(
            {
                "latestVersion": "__LATEST_VERSION__",
                "catalogSchemaVersion": 1,
                "catalogSha256": "__CATALOG_SHA256__",
            },
            registry["catalogPublication"],
        )
        self.assertEqual("__PUBLISHED_AT_UTC__", registry["publishedAtUtc"])
        self.assertEqual(
            [
                {
                    "status": "latest",
                    "catalogPath": (
                        "G:\\AUTO\\projects\\模組專案開發\\NVT_FW_Combiner\\"
                        "update-catalog.v1.json"
                    ),
                }
            ],
            registry["entries"],
        )

    def test_write_token_job_never_checks_out_or_executes_maintenance_code(
        self,
    ) -> None:
        release = (ROOT / ".github/workflows/release.yml").read_text(encoding="utf-8")
        promote_start = release.index("\n  promote:")
        smoke_start = release.index("\n  published-smoke:", promote_start)
        promote = release[promote_start:smoke_start]
        published_smoke = release[smoke_start:]

        self.assertNotIn("Checkout prepared source", promote)
        self.assertNotIn("smoke-release.ps1", promote)
        self.assertIn("ref: main", promote)
        self.assertIn(
            'gh api "repos/$env:NFC_REPOSITORY/git/commits/$env:NFC_SOURCE_SHA"',
            promote,
        )
        self.assertIn("contents: read", published_smoke)
        self.assertIn("Smoke published package without a GitHub token", published_smoke)
        self.assertIn(
            "(Test-Path Env:GH_TOKEN) -or (Test-Path Env:GITHUB_TOKEN)",
            published_smoke,
        )
        self.assertIn(
            '--pattern "NvtFwCombiner-$env:NFC_TAG-win-x64*"',
            published_smoke,
        )
        absent_index = promote.index("if ($state.Trim() -eq 'absent')")
        head_recheck_index = promote.index(
            "git/ref/heads/$env:NFC_SOURCE_BRANCH", absent_index
        )
        tag_create_index = promote.index(
            'gh api --method POST "repos/$env:NFC_REPOSITORY/git/tags"',
            absent_index,
        )
        tag_step = promote[
            promote.index("- name: Create or verify immutable annotated tag") :
        ]
        self.assertIn(
            "NFC_SOURCE_BRANCH: ${{ needs.candidate.outputs.source-branch }}",
            tag_step,
        )
        self.assertLess(head_recheck_index, tag_create_index)

    def test_stable_candidate_permits_only_recoverable_tag_and_release_states(
        self,
    ) -> None:
        release_workflow = (ROOT / ".github/workflows/release.yml").read_text(
            encoding="utf-8"
        )

        candidate_start = release_workflow.index(
            "- name: Require readable stable tag and Release state"
        )
        candidate_end = release_workflow.index(
            "- name: Install pinned repository .NET SDK", candidate_start
        )
        candidate = release_workflow[candidate_start:candidate_end]

        self.assertIn("@('absent', 'present')", candidate)
        self.assertNotIn("-ne 'absent'", candidate)
        self.assertIn("validate-tag", release_workflow)
        self.assertIn("validate-release", release_workflow)

    def test_101_release_source_is_strictly_version_only_from_v100(self) -> None:
        release = (ROOT / ".github/workflows/release.yml").read_text(encoding="utf-8")

        self.assertIn("if ($version -eq '1.0.1')", release)
        self.assertIn("validate-version-only-lineage", release)
        self.assertIn("--repository '${{ github.workspace }}'", release)
        self.assertIn("validate-version-only-package", release)
        self.assertIn("gh release download v1.0.0", release)
        self.assertIn("NvtFwCombiner-v1.0.0-win-x64.zip", release)
        self.assertIn("NvtFwCombiner-v1.0.1-win-x64.zip", release)
        self.assertIn("VersionOnlyBasePackage", release)
        self.assertIn("VersionOnlyBasePackageSha256", release)
        self.assertIn("matches[0].digest", release)
        self.assertIn("^sha256:[0-9a-f]{64}$", release)
        self.assertIn("--base-package-sha256", release)
        self.assertLess(
            release.index("Download published 1.0.0 base package"),
            release.index("Build closed-allowlist release package"),
        )
        self.assertNotIn("git diff --name-only --no-renames", release)
        self.assertNotIn("$changedPaths.Count -ne 1", release)
        self.assertNotIn("$changedPaths[0] -ne 'VERSION'", release)

    def test_stable_promotion_waits_for_terminal_parity_evidence(self) -> None:
        release = (ROOT / ".github/workflows/release.yml").read_text(encoding="utf-8")
        promote = release[
            release.index("  promote:") : release.index(
                "    steps:", release.index("  promote:")
            )
        ]

        self.assertIn("- candidate", promote)
        self.assertIn("- v0916-parity-finalize", promote)
        self.assertNotIn("- v0916-parity-attestation", promote)
        self.assertIn("needs.v0916-parity-finalize.result == 'skipped'", promote)

        steps = release[
            release.index("    steps:", release.index("  promote:")) :
            release.index("\n  published-smoke:")
        ]
        self.assertIn("Download terminal v0.9.16 parity evidence", steps)
        self.assertIn("validate-terminal --evidence", steps)
        self.assertEqual(
            3,
            steps.count("needs.candidate.outputs.version == '2.0.0'"),
        )
        self.assertEqual(
            0,
            steps.count("needs.candidate.outputs.version == '1.0.0'"),
        )
        self.assertNotIn("Reuse only committed v1.0.0 firmware evidence", release)
        self.assertNotIn("--require-committed-transfer", release)

    def test_review_ready_event_and_closed_release_candidate_are_explicit(self) -> None:
        ci = (ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8")
        release = (ROOT / ".github/workflows/release.yml").read_text(encoding="utf-8")

        self.assertIn("ready_for_review", ci)
        self.assertIn(
            "Final reviewed pull request merged as this release-branch commit", release
        )
        self.assertIn(
            "GitHub CLI cannot query `--required` after the final PR's head branch is closed.",
            release,
        )
        self.assertIn("check-runs?per_page=100", release)
        self.assertNotIn("gh pr checks", release)
        self.assertIn("reviewDecision", release)
        self.assertIn("headTree", release)
        self.assertIn("contents: read", release)
        self.assertEqual(1, release.count("contents: write"))
        self.assertIn("environment: release", release)

    def test_workflows_install_the_pinned_parity_yaml_dependency(self) -> None:
        ci = (ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8")
        release = (ROOT / ".github/workflows/release.yml").read_text(encoding="utf-8")
        main_package = (ROOT / ".github/workflows/main-package.yml").read_text(
            encoding="utf-8"
        )
        worker_project = tomllib.loads(
            (ROOT / "tools/crc-worker/pyproject.toml").read_text(encoding="utf-8")
        )
        install = (
            "python -m pip install --disable-pip-version-check "
            "--only-binary=:all: PyYAML==6.0.3"
        )
        structure = ci[ci.index("  structure:") : ci.index("  python-worker:")]
        python_worker = ci[ci.index("  python-worker:") : ci.index("  dotnet-build:")]
        dotnet_build = ci[ci.index("  dotnet-build:") : ci.index("  dotnet-test:")]
        dotnet_test = ci[ci.index("  dotnet-test:") : ci.index("  dotnet:")]
        dotnet_finalizer = ci[ci.index("  dotnet:") :]
        candidate = release[release.index("  candidate:") : release.index("  promote:")]
        promote = release[release.index("  promote:") : release.index("  published-smoke:")]
        published_smoke = release[
            release.index("  published-smoke:") : release.index("  v0916-parity-compare:")
        ]
        parity_compare = release[
            release.index("  v0916-parity-compare:") : release.index(
                "  v0916-parity-attestation:"
            )
        ]
        parity_attestation = release[
            release.index("  v0916-parity-attestation:") : release.index(
                "  v0916-parity-finalize:"
            )
        ]
        parity_finalize = release[release.index("  v0916-parity-finalize:") :]
        dev_dependencies = worker_project["project"]["optional-dependencies"]["dev"]
        package_dependencies = worker_project["project"]["optional-dependencies"]["package"]

        self.assertEqual([], worker_project["project"]["dependencies"])
        self.assertIn("PyYAML==6.0.3", dev_dependencies)
        self.assertNotIn("PyYAML==6.0.3", package_dependencies)
        self.assertIn("./tools/crc-worker[dev]", python_worker)
        self.assertIn("./tools/crc-worker[dev,package]", candidate)
        self.assertIn("./tools/crc-worker[dev,package]", main_package)
        self.assertIn(install, structure)
        self.assertIn(install, dotnet_build)
        self.assertNotIn(install, dotnet_test)
        self.assertNotIn(install, dotnet_finalizer)
        self.assertNotIn(install, published_smoke)
        for parity_job in (parity_compare, parity_attestation, parity_finalize):
            self.assertIn("Setup pinned Python for parity verification", parity_job)
            self.assertIn("python-version: '3.13'", parity_job)
            self.assertIn(install, parity_job)
            dependency_step = parity_job[
                parity_job.index("- name: Install parity verification dependency") :
                parity_job.index(
                    "\n      - name:",
                    parity_job.index("- name: Install parity verification dependency") + 1,
                )
            ]
            self.assertIn("shell: pwsh", dependency_step)
        install_step = promote[
            promote.index("- name: Install parity verification dependency") :
            promote.index("- name: Download terminal v0.9.16 parity evidence")
        ]
        self.assertIn(install, install_step)
        self.assertIn("shell: pwsh", install_step)
        self.assertIn(
            "if: ${{ needs.candidate.outputs.version == '2.0.0' }}",
            install_step,
        )

    def test_release_processor_allowlist_matches_packaged_runtime_scope(self) -> None:
        package_script = PACKAGE_SCRIPT.read_text(encoding="utf-8")
        match = re.search(
            r"\$ApprovedProcessorIds\s*=\s*@\((.*?)\)\s*\n",
            package_script,
            flags=re.DOTALL,
        )

        self.assertIsNotNone(match)
        self.assertNotIn("nfc.nt51920.ctrlram-postbuild-v1", match.group(1))
        self.assertNotIn("nfc.nt51930.ctrlram-postbuild-v1", match.group(1))
        self.assertNotIn("nfc.nt51930.ctrlram-postbuild-fw1.x", match.group(1))
        self.assertNotIn("nfc.nt51931.ctrlram-postbuild-v1", match.group(1))
        self.assertIn("nfc.nt51926.ctrlram-postbuild-fw1.4.1", match.group(1))

    def test_standard_merge_release_allowlist_excludes_retired_ic_ids(self) -> None:
        allowlist_path = ROOT / "testdata/golden/release-standard-merge-v1.json"
        allowlist = json.loads(allowlist_path.read_text(encoding="utf-8"))
        publication_text = json.dumps(allowlist, sort_keys=True).upper()

        for retired_ic_id in RETIRED_PRODUCTION_IC_IDS:
            with self.subTest(ic=retired_ic_id):
                self.assertNotIn(retired_ic_id, publication_text)

    def test_sbom_file_ids_encode_every_package_path_as_valid_spdx_characters(
        self,
    ) -> None:
        package_script = PACKAGE_SCRIPT.read_text(encoding="utf-8")

        self.assertIn(
            "[Convert]::ToHexString([Text.Encoding]::UTF8.GetBytes($_.path))",
            package_script,
        )
        self.assertIn('SPDXID = "SPDXRef-File-$SpdxPathId"', package_script)
        self.assertNotIn("$($_.path.Replace('.', '-'))", package_script)

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_release_smoke_allows_package_above_legacy_budget_until_archive_validation(self) -> None:
        for package_name in (
            "oversized.zip",
            "NvtFwCombiner-v1.0.2-win-x64.zip",
        ):
            with (
                self.subTest(package_name=package_name),
                tempfile.TemporaryDirectory(
                    prefix="nvt-release-size-policy-test-"
                ) as temporary_directory,
            ):
                package_path = Path(temporary_directory) / package_name
                with package_path.open("wb") as package:
                    package.truncate(LEGACY_PACKAGE_BYTES + 1)

                result = self.run_powershell(
                    SMOKE_SCRIPT,
                    "-PackagePath",
                    str(package_path),
                    "-SkipUiLaunch",
                )

            output = normalize_console_output(result.stdout + result.stderr)
            self.assertNotEqual(0, result.returncode, output)
            self.assertNotIn(
                "exceeds the owner-approved maximum",
                output,
            )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_release_smoke_applies_temporary_complete_package_budget(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="nvt-release-exact-size-policy-test-"
        ) as temporary_directory:
            package_path = Path(temporary_directory) / (
                "NvtFwCombiner-v1.0.2-win-x64.zip"
            )
            with package_path.open("wb") as package:
                package.truncate(MAXIMUM_PACKAGE_BYTES)

            exact_result = self.run_powershell(
                SMOKE_SCRIPT,
                "-PackagePath",
                str(package_path),
                "-SkipUiLaunch",
            )

        exact_output = normalize_console_output(
            exact_result.stdout + exact_result.stderr
        )
        self.assertNotEqual(0, exact_result.returncode, exact_output)
        self.assertNotIn("exceeds the owner-approved maximum", exact_output)

        for version in ("0.10.6", "1.0.2", "2.0.0"):
            with (
                self.subTest(version=version),
                tempfile.TemporaryDirectory(
                    prefix="nvt-release-managed-size-policy-test-"
                ) as temporary_directory,
            ):
                package_path = Path(temporary_directory) / (
                    f"NvtFwCombiner-v{version}-win-x64.zip"
                )
                with package_path.open("wb") as package:
                    package.truncate(MAXIMUM_PACKAGE_BYTES + 1)

                result = self.run_powershell(
                    SMOKE_SCRIPT,
                    "-PackagePath",
                    str(package_path),
                    "-SkipUiLaunch",
                )

            self.assertNotEqual(
                0,
                result.returncode,
                result.stdout + result.stderr,
            )
            self.assertIn(
                "exceeds the owner-approved maximum 134217728 bytes",
                normalize_console_output(result.stdout + result.stderr),
            )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_release_smoke_rejects_application_above_owner_approved_budget(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory(
            prefix="nvt-release-application-size-policy-test-"
        ) as temporary_directory:
            temporary_root = Path(temporary_directory)
            package_name = "NvtFwCombiner-v0.0.0-win-x64"
            package_root = temporary_root / package_name
            package_root.mkdir()

            for required_file in (
                "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe",
                CAPABILITY_POLICY_RELATIVE_PATH.as_posix(),
                "RELEASE-MANIFEST.json",
                "SHA256SUMS.txt",
                "README.txt",
                "LICENSE.txt",
                "THIRD-PARTY-NOTICES.txt",
            ):
                required_path = package_root / required_file
                required_path.parent.mkdir(parents=True, exist_ok=True)
                required_path.write_bytes(b"release-policy fixture\n")

            application_path = package_root / "NvtFwCombiner.exe"
            with application_path.open("wb") as application:
                application.truncate(MAXIMUM_APPLICATION_BYTES + 1)

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
            "application size 80000001 exceeds the owner-approved maximum 80000000 bytes",
            normalize_console_output(result.stdout + result.stderr),
        )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_release_smoke_rejects_extra_external_tool_even_when_manifested(
        self,
    ) -> None:
        result = self.run_smoke_with_manifested_external_tool(PROBE_RELATIVE_PATH)

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(
            "Release manifest external-tool files differ from the approved allowlist.",
            result.stdout + result.stderr,
        )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_release_smoke_rejects_root_crc_worker_even_when_manifested(self) -> None:
        result = self.run_smoke_with_manifested_external_tool(Path("CRCWorker.exe"))

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(
            "Release manifest external-tool paths and roles are inconsistent.",
            result.stdout + result.stderr,
        )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_release_smoke_rejects_retired_support_policy_payload(self) -> None:
        result = self.run_smoke_with_retired_support_policy()

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(
            "Release manifest contains the retired support publication policy payload.",
            result.stdout + result.stderr,
        )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_release_smoke_rejects_retired_support_policy_path_with_reference_role(
        self,
    ) -> None:
        for relative_path in (
            Path("docs/contracts/support-publication-policy-v1.0.0.json"),
            Path("docs/contracts/support-publication-policy-v1.json"),
        ):
            with self.subTest(relative_path=relative_path):
                result = self.run_smoke_with_retired_support_policy(
                    relative_path=relative_path,
                    role="reference",
                )

                self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertIn(
                    "Release manifest contains the retired support publication policy payload.",
                    result.stdout + result.stderr,
                )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_release_smoke_rejects_missing_capability_policy(self) -> None:
        result = self.run_smoke_with_capability_policy(include_policy=False)

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(
            "Release package is missing required file "
            f"'{CAPABILITY_POLICY_RELATIVE_PATH.as_posix()}'.",
            normalize_console_output(result.stdout + result.stderr),
        )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_release_smoke_rejects_repathed_capability_policy(self) -> None:
        result = self.run_smoke_with_capability_policy(
            relative_path=Path("canonical-capability-policy-v1.json")
        )

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(
            "Release package is missing required file "
            f"'{CAPABILITY_POLICY_RELATIVE_PATH.as_posix()}'.",
            normalize_console_output(result.stdout + result.stderr),
        )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_release_smoke_rejects_wrong_capability_policy_role(self) -> None:
        result = self.run_smoke_with_capability_policy(role="reference")

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(
            "Release manifest canonical capability policy identity is inconsistent.",
            result.stdout + result.stderr,
        )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_release_smoke_rejects_self_consistent_wrong_capability_policy_hash(
        self,
    ) -> None:
        payload = b'{"changed":"but self-consistent"}\n'
        result = self.run_smoke_with_capability_policy(
            payload=payload,
            manifest_sha256=hashlib.sha256(payload).hexdigest(),
        )

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(
            "Release manifest canonical capability policy identity is inconsistent.",
            result.stdout + result.stderr,
        )

    def run_smoke_with_manifested_external_tool(
        self, relative_path: Path
    ) -> subprocess.CompletedProcess[str]:
        with tempfile.TemporaryDirectory(
            prefix="nvt-release-policy-test-"
        ) as temporary_directory:
            temporary_root = Path(temporary_directory)
            package_name = "NvtFwCombiner-v0.0.0-win-x64"
            package_root = temporary_root / package_name
            package_root.mkdir()

            for required_file in (
                "NvtFwCombiner.exe",
                "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe",
                "SHA256SUMS.txt",
                "README.txt",
                "LICENSE.txt",
                "THIRD-PARTY-NOTICES.txt",
            ):
                required_path = package_root / required_file
                required_path.parent.mkdir(parents=True, exist_ok=True)
                required_path.write_bytes(b"release-policy fixture\n")

            staged_probe = package_root / relative_path
            staged_probe.parent.mkdir(parents=True, exist_ok=True)
            staged_probe.write_bytes(b"negative release-policy probe\n")
            manifest_entries: list[dict[str, object]] = []
            self.add_valid_capability_policy(package_root, manifest_entries)
            manifest_entries.append(
                {
                    "path": relative_path.as_posix(),
                    "size": staged_probe.stat().st_size,
                    "sha256": "0" * 64,
                    "role": "externalTool",
                }
            )
            manifest = {"files": manifest_entries}
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

            return self.run_powershell(
                SMOKE_SCRIPT,
                "-PackagePath",
                str(package_path),
                "-SkipUiLaunch",
            )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_release_smoke_rejects_package_without_built_in_profiles(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="nvt-release-profile-policy-test-"
        ) as temporary_directory:
            temporary_root = Path(temporary_directory)
            package_name = "NvtFwCombiner-v0.0.0-win-x64"
            package_root = temporary_root / package_name
            package_root.mkdir()

            for required_file in (
                "NvtFwCombiner.exe",
                "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe",
                "SHA256SUMS.txt",
                "README.txt",
                "LICENSE.txt",
                "THIRD-PARTY-NOTICES.txt",
            ):
                required_path = package_root / required_file
                required_path.parent.mkdir(parents=True, exist_ok=True)
                required_path.write_bytes(b"release-policy fixture\n")

            manifest_entries = []
            self.add_valid_capability_policy(package_root, manifest_entries)
            for relative_path in APPROVED_EXTERNAL_TOOL_PATHS:
                external_path = package_root / relative_path
                external_path.parent.mkdir(parents=True, exist_ok=True)
                external_path.write_bytes(b"external-tool policy fixture\n")
                manifest_entries.append(
                    {
                        "path": relative_path,
                        "size": external_path.stat().st_size,
                        "sha256": "0" * 64,
                        "role": "externalTool",
                    }
                )

            (package_root / "RELEASE-MANIFEST.json").write_text(
                json.dumps({"files": manifest_entries}),
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
            "Release manifest has no materialized built-in profile files.",
            result.stdout + result.stderr,
        )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_release_smoke_rejects_mutated_trust_index_with_updated_release_hash(
        self,
    ) -> None:
        trust_index = json.loads(
            (ROOT / "profiles/built-in/package-trust-index.json").read_text(
                encoding="utf-8"
            )
        )
        trust_index["executablePath"] = "forbidden.exe"
        payload = json.dumps(trust_index).encode("utf-8")

        result = self.run_smoke_with_trust_index(payload)

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(
            "Release package trust index does not match the exact reviewed identity.",
            result.stdout + result.stderr,
        )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_release_smoke_rejects_profile_entry_drift_with_updated_release_hash(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory(
            prefix="nvt-release-profile-closure-test-"
        ) as temporary_directory:
            temporary_root = Path(temporary_directory)
            package_name = "NvtFwCombiner-v0.0.0-win-x64"
            package_root = temporary_root / package_name
            package_root.mkdir()
            for required_file in (
                "NvtFwCombiner.exe",
                "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe",
                "SHA256SUMS.txt",
                "README.txt",
                "LICENSE.txt",
                "THIRD-PARTY-NOTICES.txt",
            ):
                required_path = package_root / required_file
                required_path.parent.mkdir(parents=True, exist_ok=True)
                required_path.write_bytes(b"release-policy fixture\n")

            manifest_entries: list[dict[str, object]] = []
            self.add_valid_capability_policy(package_root, manifest_entries)
            for relative_path in APPROVED_EXTERNAL_TOOL_PATHS:
                external_path = package_root / relative_path
                external_path.parent.mkdir(parents=True, exist_ok=True)
                external_path.write_bytes(b"external-tool policy fixture\n")
                manifest_entries.append(
                    self.manifest_entry(external_path, package_root, "externalTool")
                )

            profile_paths = self.stage_profile_bundles(package_root)
            drift_path = next(
                path for path in profile_paths if "/schemas/" in path.as_posix()
            )
            drift_path.write_bytes(drift_path.read_bytes() + b"\n")
            manifest_entries.extend(
                self.manifest_entry(path, package_root, "builtInProfile")
                for path in profile_paths
            )
            (package_root / "RELEASE-MANIFEST.json").write_text(
                json.dumps({"files": manifest_entries}),
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
            "Release built-in profile bundle file hash differs",
            result.stdout + result.stderr,
        )

    def stage_profile_bundles(self, package_root: Path) -> tuple[Path, ...]:
        source_index_path = ROOT / "profiles/built-in/package-trust-index.json"
        source_index = json.loads(source_index_path.read_text(encoding="utf-8"))
        index_path = package_root / "profiles/built-in/package-trust-index.json"
        index_path.parent.mkdir(parents=True, exist_ok=True)
        index_path.write_bytes(source_index_path.read_bytes())

        staged = [index_path]
        for trust_entry in source_index["bundles"]:
            bundle_directory = trust_entry["bundleDirectory"]
            source_bundle_root = ROOT / "profiles/built-in" / bundle_directory
            bundle_root = package_root / "profiles/built-in" / bundle_directory
            bundle_root.mkdir(parents=True)
            source_manifest_path = source_bundle_root / "profile-bundle.json"
            manifest = json.loads(source_manifest_path.read_text(encoding="utf-8"))
            manifest_path = bundle_root / "profile-bundle.json"
            manifest_path.write_bytes(source_manifest_path.read_bytes())
            staged.append(manifest_path)
            canonical = trust_entry["materialization"].get("canonicalFirmwareFamily")
            for entry in manifest["entries"]:
                relative_path = Path(entry["path"])
                if entry["path"] == "schemas/composition-profile-v2.schema.json":
                    source_path = (
                        ROOT
                        / "docs/contracts"
                        / trust_entry["materialization"]["compositionProfileSchemaFile"]
                    )
                elif entry["path"] == "schemas/firmware-family-v1.schema.json":
                    source_path = (
                        ROOT
                        / "docs/contracts"
                        / trust_entry["materialization"]["firmwareFamilySchemaFile"]
                    )
                elif canonical and entry["path"] == canonical["destination"]:
                    source_path = ROOT / "profiles/built-in" / canonical["source"]
                else:
                    source_path = source_bundle_root / relative_path
                destination = bundle_root / relative_path
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(source_path.read_bytes())
                staged.append(destination)
        return tuple(staged)

    def run_smoke_with_trust_index(
        self,
        payload: bytes,
    ) -> subprocess.CompletedProcess[str]:
        with tempfile.TemporaryDirectory(
            prefix="nvt-release-trust-index-test-"
        ) as temporary_directory:
            temporary_root = Path(temporary_directory)
            package_name = "NvtFwCombiner-v0.0.0-win-x64"
            package_root = temporary_root / package_name
            package_root.mkdir()
            for required_file in (
                "NvtFwCombiner.exe",
                "SHA256SUMS.txt",
                "README.txt",
                "LICENSE.txt",
                "THIRD-PARTY-NOTICES.txt",
            ):
                required_path = package_root / required_file
                required_path.parent.mkdir(parents=True, exist_ok=True)
                required_path.write_bytes(b"release-policy fixture\n")

            manifest_entries: list[dict[str, object]] = []
            self.add_valid_capability_policy(package_root, manifest_entries)
            for relative_path in APPROVED_EXTERNAL_TOOL_PATHS:
                external_path = package_root / relative_path
                external_path.parent.mkdir(parents=True, exist_ok=True)
                external_path.write_bytes(b"external-tool policy fixture\n")
                manifest_entries.append(
                    self.manifest_entry(external_path, package_root, "externalTool")
                )

            index_path = package_root / "profiles/built-in/package-trust-index.json"
            index_path.parent.mkdir(parents=True, exist_ok=True)
            index_path.write_bytes(payload)
            manifest_entries.append(
                self.manifest_entry(index_path, package_root, "builtInProfile")
            )
            (package_root / "RELEASE-MANIFEST.json").write_text(
                json.dumps({"files": manifest_entries}),
                encoding="utf-8",
            )
            package_path = temporary_root / f"{package_name}.zip"
            with zipfile.ZipFile(
                package_path,
                "w",
                compression=zipfile.ZIP_DEFLATED,
            ) as archive:
                for path in sorted(package_root.rglob("*")):
                    if path.is_file():
                        archive.write(path, path.relative_to(temporary_root))

            return self.run_powershell(
                SMOKE_SCRIPT,
                "-PackagePath",
                str(package_path),
                "-SkipUiLaunch",
            )

    @staticmethod
    def manifest_entry(path: Path, package_root: Path, role: str) -> dict[str, object]:
        payload = path.read_bytes()
        return {
            "path": path.relative_to(package_root).as_posix(),
            "size": len(payload),
            "sha256": hashlib.sha256(payload).hexdigest(),
            "role": role,
        }

    def run_smoke_with_retired_support_policy(
        self,
        *,
        relative_path: Path = Path("docs/contracts/support-publication-policy-v1.json"),
        role: str = "publicationPolicy",
    ) -> subprocess.CompletedProcess[str]:
        with tempfile.TemporaryDirectory(
            prefix="nvt-release-support-policy-test-"
        ) as temporary_directory:
            temporary_root = Path(temporary_directory)
            package_name = "NvtFwCombiner-v0.0.0-win-x64"
            package_root = temporary_root / package_name
            package_root.mkdir()

            for required_file in (
                "NvtFwCombiner.exe",
                "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe",
                "SHA256SUMS.txt",
                "README.txt",
                "LICENSE.txt",
                "THIRD-PARTY-NOTICES.txt",
            ):
                required_path = package_root / required_file
                required_path.parent.mkdir(parents=True, exist_ok=True)
                required_path.write_bytes(b"release-policy fixture\n")

            manifest_entries: list[dict[str, object]] = []
            self.add_valid_capability_policy(package_root, manifest_entries)
            payload = b'{"retired":true}\n'
            policy_path = package_root / relative_path
            policy_path.parent.mkdir(parents=True, exist_ok=True)
            policy_path.write_bytes(payload)
            manifest_entries.append(
                {
                    "path": relative_path.as_posix(),
                    "size": len(payload),
                    "sha256": hashlib.sha256(payload).hexdigest(),
                    "role": role,
                }
            )

            (package_root / "RELEASE-MANIFEST.json").write_text(
                json.dumps({"files": manifest_entries}),
                encoding="utf-8",
            )
            package_path = temporary_root / f"{package_name}.zip"
            with zipfile.ZipFile(
                package_path, "w", compression=zipfile.ZIP_DEFLATED
            ) as archive:
                for path in sorted(package_root.rglob("*")):
                    if path.is_file():
                        archive.write(path, path.relative_to(temporary_root))

            return self.run_powershell(
                SMOKE_SCRIPT,
                "-PackagePath",
                str(package_path),
                "-SkipUiLaunch",
            )

    def run_smoke_with_capability_policy(
        self,
        *,
        include_policy: bool = True,
        relative_path: Path = CAPABILITY_POLICY_RELATIVE_PATH,
        role: str = CAPABILITY_POLICY_ROLE,
        payload: bytes | None = None,
        manifest_sha256: str | None = None,
    ) -> subprocess.CompletedProcess[str]:
        with tempfile.TemporaryDirectory(
            prefix="nvt-release-capability-policy-test-"
        ) as temporary_directory:
            temporary_root = Path(temporary_directory)
            package_name = "NvtFwCombiner-v0.0.0-win-x64"
            package_root = temporary_root / package_name
            package_root.mkdir()

            for required_file in (
                "NvtFwCombiner.exe",
                "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe",
                "SHA256SUMS.txt",
                "README.txt",
                "LICENSE.txt",
                "THIRD-PARTY-NOTICES.txt",
            ):
                required_path = package_root / required_file
                required_path.parent.mkdir(parents=True, exist_ok=True)
                required_path.write_bytes(b"release-policy fixture\n")

            manifest_entries: list[dict[str, object]] = []
            if include_policy:
                policy_payload = (
                    (ROOT / CAPABILITY_POLICY_RELATIVE_PATH).read_bytes()
                    if payload is None
                    else payload
                )
                policy_path = package_root / relative_path
                policy_path.parent.mkdir(parents=True, exist_ok=True)
                policy_path.write_bytes(policy_payload)
                manifest_entries.append(
                    {
                        "path": relative_path.as_posix(),
                        "size": len(policy_payload),
                        "sha256": manifest_sha256
                        or hashlib.sha256(policy_payload).hexdigest(),
                        "role": role,
                    }
                )

            (package_root / "RELEASE-MANIFEST.json").write_text(
                json.dumps({"files": manifest_entries}),
                encoding="utf-8",
            )
            package_path = temporary_root / f"{package_name}.zip"
            with zipfile.ZipFile(
                package_path, "w", compression=zipfile.ZIP_DEFLATED
            ) as archive:
                for path in sorted(package_root.rglob("*")):
                    if path.is_file():
                        archive.write(path, path.relative_to(temporary_root))

            return self.run_powershell(
                SMOKE_SCRIPT,
                "-PackagePath",
                str(package_path),
                "-SkipUiLaunch",
            )

    def add_valid_capability_policy(
        self,
        package_root: Path,
        manifest_entries: list[dict[str, object]],
    ) -> None:
        policy_payload = (ROOT / CAPABILITY_POLICY_RELATIVE_PATH).read_bytes()
        self.assertEqual(
            CAPABILITY_POLICY_SHA256,
            hashlib.sha256(policy_payload).hexdigest(),
        )
        policy_path = package_root / CAPABILITY_POLICY_RELATIVE_PATH
        policy_path.parent.mkdir(parents=True, exist_ok=True)
        policy_path.write_bytes(policy_payload)
        manifest_entries.append(
            {
                "path": CAPABILITY_POLICY_RELATIVE_PATH.as_posix(),
                "size": len(policy_payload),
                "sha256": CAPABILITY_POLICY_SHA256,
                "role": CAPABILITY_POLICY_ROLE,
            }
        )


if __name__ == "__main__":
    unittest.main()

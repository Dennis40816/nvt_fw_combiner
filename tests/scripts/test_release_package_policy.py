"""Deterministic release-package allowlist regressions."""

from __future__ import annotations

import json
import os
import re
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
APPROVED_EXTERNAL_TOOL_PATHS = (
    "external-tools/README.md",
    "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe",
    "external-tools/legacy-combiner/README.md",
    "external-tools/legacy-combiner/1.13.0/Combiner.exe",
    "external-tools/legacy-combiner/1.13.0/manifest.json",
)
POWERSHELL = shutil.which("pwsh") or shutil.which("powershell")
PERSONAL_OWNER_IDENTIFIER = "Dennis40816"
DISTRIBUTION_OWNER = "MSP/FW3"
SOURCE_IDENTITY = "urn:msp-fw3:nvt-fw-combiner:source"
MAXIMUM_PACKAGE_BYTES = 80_000_000
MAXIMUM_APPLICATION_BYTES = 80_000_000
ANSI_ESCAPE_PATTERN = re.compile(r"\x1b\[[0-?]*[ -/]*[@-~]")


def normalize_console_output(output: str) -> str:
    """Remove terminal styling and line wrapping before message assertions."""

    unstyled_output = ANSI_ESCAPE_PATTERN.sub("", output)
    return " ".join(unstyled_output.replace("|", " ").split())


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
            "manifest-pinned materialized files included and unexpected file rejected",
            result.stdout,
        )
        self.assertIn(
            "Runtime catalog package policy dry-run passed: approved files included and unexpected file rejected",
            result.stdout,
        )
        self.assertIn(
            "Canonical golden package policy dry-run passed: 34 direct Standard Merge BIN artifacts and 13 direct/alias cases selected; diagnostics and other workflows excluded",
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

    def test_stable_release_is_ci_owned_and_main_preview_is_manual(self) -> None:
        release_workflow = (ROOT / ".github/workflows/release.yml").read_text(
            encoding="utf-8"
        )
        main_package_workflow = (ROOT / ".github/workflows/main-package.yml").read_text(
            encoding="utf-8"
        )

        self.assertIn("Exact reviewed main commit", release_workflow)
        self.assertIn("permissions:\n  contents: read", release_workflow)
        self.assertIn(
            "candidate:\n"
            "    name: release / candidate\n"
            "    runs-on: windows-latest\n"
            "    timeout-minutes: 60\n"
            "    permissions:\n"
            "      contents: read\n"
            "      pull-requests: read\n"
            "      checks: read\n"
            "      statuses: read",
            release_workflow,
        )
        self.assertIn("environment: release", release_workflow)
        self.assertIn("contents: write", release_workflow)
        self.assertIn("scripts/render_release_notes.py", release_workflow)
        self.assertIn("release_promotion_policy.py validate-context", release_workflow)
        self.assertIn(
            "release_promotion_policy.py validate-promotion-source", release_workflow
        )
        self.assertIn("release_promotion_policy.py validate-tag", release_workflow)
        self.assertIn("release_promotion_policy.py validate-release", release_workflow)
        self.assertIn("release_promotion_policy.py create-manifest", release_workflow)
        self.assertIn("release_promotion_policy.py verify-manifest", release_workflow)
        self.assertIn("release_promotion_policy.py plan-recovery", release_workflow)
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

    def test_review_ready_event_and_closed_release_candidate_are_explicit(self) -> None:
        ci = (ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8")
        release = (ROOT / ".github/workflows/release.yml").read_text(encoding="utf-8")

        self.assertIn("ready_for_review", ci)
        self.assertIn("Final reviewed pull request merged as this main commit", release)
        self.assertIn("--required --json name,state,bucket", release)
        self.assertIn("reviewDecision", release)
        self.assertIn("headTree", release)
        self.assertIn("contents: read", release)
        self.assertEqual(1, release.count("contents: write"))
        self.assertIn("environment: release", release)

    def test_release_processor_allowlist_matches_packaged_runtime_scope(self) -> None:
        package_script = PACKAGE_SCRIPT.read_text(encoding="utf-8")
        match = re.search(
            r"\$ApprovedProcessorIds\s*=\s*@\((.*?)\)\s*\n",
            package_script,
            flags=re.DOTALL,
        )

        self.assertIsNotNone(match)
        self.assertIn("nfc.nt51931.ctrlram-postbuild-v1", match.group(1))
        self.assertNotIn("nfc.nt51930.ctrlram-postbuild-v1", match.group(1))
        self.assertIn("nfc.nt51930.ctrlram-postbuild-fw1.x", match.group(1))
        self.assertIn("nfc.nt51926.ctrlram-postbuild-fw1.4.1", match.group(1))

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
    def test_release_smoke_rejects_package_above_owner_approved_budget(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="nvt-release-size-policy-test-"
        ) as temporary_directory:
            package_path = Path(temporary_directory) / "oversized.zip"
            with package_path.open("wb") as package:
                package.truncate(MAXIMUM_PACKAGE_BYTES + 1)

            result = self.run_powershell(
                SMOKE_SCRIPT,
                "-PackagePath",
                str(package_path),
                "-SkipUiLaunch",
            )

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(
            "exceeds the owner-approved maximum 80000000 bytes",
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
            manifest = {
                "files": [
                    {
                        "path": relative_path.as_posix(),
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


if __name__ == "__main__":
    unittest.main()

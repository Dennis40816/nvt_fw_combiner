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
import unittest
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "scripts"
sys.path.insert(0, str(SCRIPTS))

import validate_repository as repository_validation  # noqa: E402

PACKAGE_SCRIPT = ROOT / "scripts" / "package.ps1"
SMOKE_SCRIPT = ROOT / "scripts" / "smoke-release.ps1"
PROBE_RELATIVE_PATH = Path("external-tools/release-package-policy-probe.txt")
SUPPORT_POLICY_RELATIVE_PATH = Path(
    "docs/contracts/support-publication-policy-v1.json"
)
SUPPORT_POLICY_ROLE = "publicationPolicy"
SUPPORT_POLICY_SHA256 = (
    "b8d50829608c452124a010d78d8cd0df249f239fd272be35e87bdb8d7ea416ff"
)
SUPPORT_POLICY_HISTORY = (
    (
        Path("docs/contracts/support-publication-policy-v1.0.0.json"),
        "365a6ee92776bbd6b1aaa155919121dfbbbfc67046c3ab6a2fbfe7fa5d45c5c2",
    ),
    (SUPPORT_POLICY_RELATIVE_PATH, SUPPORT_POLICY_SHA256),
)
CAPABILITY_POLICY_RELATIVE_PATH = Path(
    "docs/contracts/canonical-capability-policy-v1.json"
)
CAPABILITY_POLICY_ROLE = "capabilityPolicy"
CAPABILITY_POLICY_SHA256 = (
    "1a837139da8c68dd72692d030db5b5e0094a5e2005a1e4fb0dd2e63a1993f034"
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

    def test_support_policy_package_contract_matches_runtime_and_release_scripts(
        self,
    ) -> None:
        errors: list[str] = []

        repository_validation.validate_support_publication_policy_package_contracts(
            ROOT, errors
        )

        self.assertEqual([], errors)

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

    def test_repository_validator_rejects_support_policy_contract_drift(self) -> None:
        mutations = {
            "empty": (
                PACKAGE_SCRIPT,
                lambda text: re.sub(
                    r"\$ApprovedSupportPublicationPolicyPackageContracts\s*=\s*@\("
                    r".*?\)\s*\n",
                    "$ApprovedSupportPublicationPolicyPackageContracts = @()\n",
                    text,
                    count=1,
                    flags=re.DOTALL,
                ),
            ),
            "repath": (
                PACKAGE_SCRIPT,
                lambda text: text.replace(
                    SUPPORT_POLICY_RELATIVE_PATH.as_posix(),
                    "docs/contracts/repathed-support-policy.json",
                    1,
                ),
            ),
            "wrong-role": (
                SMOKE_SCRIPT,
                lambda text: text.replace(
                    "role = 'publicationPolicy'",
                    "role = 'reference'",
                    1,
                ),
            ),
            "wrong-hash": (
                SMOKE_SCRIPT,
                lambda text: text.replace(
                    SUPPORT_POLICY_SHA256,
                    "0" * 64,
                    1,
                ),
            ),
        }

        for name, (script_path, mutate) in mutations.items():
            with self.subTest(name=name), tempfile.TemporaryDirectory(
                prefix="nvt-support-policy-contract-validator-"
            ) as temporary_directory:
                temporary_root = Path(temporary_directory)
                for source in (
                    PACKAGE_SCRIPT,
                    SMOKE_SCRIPT,
                    *(ROOT / path for path, _ in SUPPORT_POLICY_HISTORY),
                    ROOT
                    / "src/NvtFwCombiner.Infrastructure/Support/"
                    "BuiltInSupportPublicationPolicy.cs",
                ):
                    destination = temporary_root / source.relative_to(ROOT)
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copyfile(source, destination)

                copied_script = temporary_root / script_path.relative_to(ROOT)
                copied_script.write_text(
                    mutate(copied_script.read_text(encoding="utf-8")),
                    encoding="utf-8",
                )
                errors: list[str] = []

                repository_validation.validate_support_publication_policy_package_contracts(
                    temporary_root, errors
                )

                self.assertTrue(errors, name)

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
            "Support publication policy package dry-run passed: exact path, role, and SHA-256 pinned; empty contract and wrong published hash rejected",
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

        self.assertIn("Exact reviewed release-branch head", release_workflow)
        self.assertIn("source_branch:", release_workflow)
        self.assertIn("- 0.9.17", release_workflow)
        self.assertIn("- 0.9.18", release_workflow)
        self.assertIn(
            "NFC_RELEASE_SOURCE_BRANCH -notin @('main', '0.9.17', '0.9.18')",
            release_workflow,
        )
        self.assertIn("'0.9.17' = '0.9.17'", release_workflow)
        self.assertIn("'0.9.18' = '0.9.18'", release_workflow)
        self.assertIn(
            "$approvedMaintenanceVersions[$env:NFC_SOURCE_BRANCH] -ne $version",
            release_workflow,
        )
        self.assertIn("permissions:\n  contents: read", release_workflow)
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
        self.assertEqual(1, release_workflow.count("gh api --paginate --slurp $endpoint"))
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
        self.assertIn(
            "Smoke published package without a GitHub token", published_smoke
        )
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
        tag_step = promote[promote.index("- name: Create or verify immutable annotated tag") :]
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
                *(path.as_posix() for path, _ in SUPPORT_POLICY_HISTORY),
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
    def test_release_smoke_rejects_missing_support_policy(self) -> None:
        result = self.run_smoke_with_support_policy(include_policy=False)

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(
            "Release package is missing required file "
            f"'{SUPPORT_POLICY_RELATIVE_PATH.as_posix()}'.",
            normalize_console_output(result.stdout + result.stderr),
        )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_release_smoke_rejects_repathed_support_policy(self) -> None:
        result = self.run_smoke_with_support_policy(
            relative_path=Path("support-publication-policy-v1.json")
        )

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(
            "Release package is missing required file "
            f"'{SUPPORT_POLICY_RELATIVE_PATH.as_posix()}'.",
            normalize_console_output(result.stdout + result.stderr),
        )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_release_smoke_rejects_wrong_support_policy_role(self) -> None:
        result = self.run_smoke_with_support_policy(role="reference")

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(
            "Release manifest support publication policy identity is inconsistent.",
            result.stdout + result.stderr,
        )

    @unittest.skipUnless(
        POWERSHELL, "PowerShell is required for Windows release-policy tests"
    )
    def test_release_smoke_rejects_self_consistent_wrong_support_policy_hash(
        self,
    ) -> None:
        payload = b'{"changed":"but self-consistent"}\n'
        result = self.run_smoke_with_support_policy(
            payload=payload,
            manifest_sha256=hashlib.sha256(payload).hexdigest(),
        )

        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn(
            "Release manifest support publication policy identity is inconsistent.",
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
            self.add_valid_support_policy(package_root, manifest_entries)
            self.add_valid_capability_policy(package_root, manifest_entries)
            manifest_entries.append(
                {
                    "path": relative_path.as_posix(),
                    "size": staged_probe.stat().st_size,
                    "sha256": "0" * 64,
                    "role": "externalTool",
                }
            )
            manifest = {
                "files": manifest_entries
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
            self.add_valid_support_policy(package_root, manifest_entries)
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

    def run_smoke_with_support_policy(
        self,
        *,
        include_policy: bool = True,
        relative_path: Path = SUPPORT_POLICY_RELATIVE_PATH,
        role: str = SUPPORT_POLICY_ROLE,
        payload: bytes | None = None,
        manifest_sha256: str | None = None,
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
            for policy_relative_path, policy_sha256 in SUPPORT_POLICY_HISTORY:
                if policy_relative_path == SUPPORT_POLICY_RELATIVE_PATH:
                    continue
                policy_payload = (ROOT / policy_relative_path).read_bytes()
                self.assertEqual(
                    policy_sha256,
                    hashlib.sha256(policy_payload).hexdigest(),
                )
                policy_path = package_root / policy_relative_path
                policy_path.parent.mkdir(parents=True, exist_ok=True)
                policy_path.write_bytes(policy_payload)
                manifest_entries.append(
                    {
                        "path": policy_relative_path.as_posix(),
                        "size": len(policy_payload),
                        "sha256": policy_sha256,
                        "role": SUPPORT_POLICY_ROLE,
                    }
                )
            if include_policy:
                policy_payload = (
                    (ROOT / SUPPORT_POLICY_RELATIVE_PATH).read_bytes()
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
            self.add_valid_support_policy(package_root, manifest_entries)
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

    def add_valid_support_policy(
        self,
        package_root: Path,
        manifest_entries: list[dict[str, object]],
    ) -> None:
        for policy_relative_path, policy_sha256 in SUPPORT_POLICY_HISTORY:
            policy_payload = (ROOT / policy_relative_path).read_bytes()
            self.assertEqual(
                policy_sha256,
                hashlib.sha256(policy_payload).hexdigest(),
            )
            policy_path = package_root / policy_relative_path
            policy_path.parent.mkdir(parents=True, exist_ok=True)
            policy_path.write_bytes(policy_payload)
            manifest_entries.append(
                {
                    "path": policy_relative_path.as_posix(),
                    "size": len(policy_payload),
                    "sha256": policy_sha256,
                    "role": SUPPORT_POLICY_ROLE,
                }
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

"""CI source binding and Windows structure contracts."""
import json
import os
import shutil
import tempfile
import subprocess
import unittest
from pathlib import Path
import sys
from unittest.mock import patch
import yaml

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))
import validate_repository as repository_validator


class CiStructureContractTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)

    def test_repository_files_accepts_gitlink_without_reading_submodule_contents(self) -> None:
        def git(*arguments: str) -> None:
            subprocess.run(
                ["git", *arguments], cwd=self.root, check=True, capture_output=True
            )

        git("init", "--quiet")
        readme = self.root / "README.md"
        readme.write_text("# Test repository\n", encoding="utf-8")
        git("add", "README.md")
        # A gitlink records a foreign commit; its object need not exist locally.
        git("update-index", "--add", "--cacheinfo", "160000", "1" * 40,
            "third-party/example")
        submodule = self.root / "third-party/example"
        with patch.object(repository_validator, "ROOT", self.root):
            for state in ("absent", "empty", "populated"):
                with self.subTest(state=state):
                    if state == "empty":
                        submodule.mkdir(parents=True)
                    elif state == "populated":
                        (submodule / "invalid.py").write_text("invalid Python!", encoding="utf-8")
                    self.assertEqual([readme], repository_validator.repository_files())
                    tracked = repository_validator._git_tracked_paths()
                    self.assertEqual([readme], repository_validator.repository_files(tracked))

    def test_ci_structure_checkout_fetches_complete_history(self) -> None:
        workflow = (ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8")
        structure_job = workflow.split("  python-worker:", 1)[0]

        self.assertIn("fetch-depth: 0", structure_job)

    def test_ci_push_checks_main_and_integration_trunk(self) -> None:
        for relative in (
            ".github/workflows/ci.yml",
            "docs/ci/workflow-templates/ci.yml",
        ):
            with self.subTest(workflow=relative):
                workflow = yaml.load(
                    (ROOT / relative).read_text(encoding="utf-8"),
                    Loader=yaml.BaseLoader,
                )
                self.assertEqual(
                    ["main", "1.2.x"], workflow["on"]["push"]["branches"]
                )
                self.assertIn("pull_request", workflow["on"])

    def test_ci_jobs_bind_one_exact_event_source_and_current_base(self) -> None:
        expected_ref = "${{ github.event.pull_request.head.sha || github.sha }}"
        expected_base = "${{ github.event.pull_request.base.sha }}"
        expected_head = "${{ github.event.pull_request.head.sha }}"

        for relative in (
            ".github/workflows/ci.yml",
            "docs/ci/workflow-templates/ci.yml",
        ):
            with self.subTest(workflow=relative):
                workflow = yaml.safe_load((ROOT / relative).read_text(encoding="utf-8"))
                jobs = workflow["jobs"]
                checkout_steps = [
                    step
                    for job in jobs.values()
                    for step in job["steps"]
                    if str(step.get("uses", "")).startswith("actions/checkout@")
                ]

                self.assertEqual(6, len(checkout_steps))
                for step in checkout_steps:
                    checkout_with = step["with"]
                    self.assertEqual(expected_ref, checkout_with["ref"])
                    self.assertIs(False, checkout_with["persist-credentials"])
                    self.assertEqual(0, checkout_with["fetch-depth"])
                    self.assertNotIn("github.ref", checkout_with["ref"])
                    self.assertNotIn("refs/pull/", checkout_with["ref"])

                freshness = next(
                    step
                    for step in jobs["structure"]["steps"]
                    if step.get("name")
                    == "Require PR head to contain the exact reviewed base"
                )
                self.assertEqual("github.event_name == 'pull_request'", freshness["if"])
                self.assertEqual(expected_base, freshness["env"]["NFC_PR_BASE_SHA"])
                self.assertEqual(expected_head, freshness["env"]["NFC_PR_HEAD_SHA"])
                self.assertEqual("pwsh", freshness["shell"])
                script = freshness["run"]
                self.assertIn(
                    "$actualHead = & git rev-parse --verify HEAD",
                    script,
                )
                self.assertIn("$revParseStatus = $LASTEXITCODE", script)
                self.assertLess(
                    script.index("$revParseStatus = $LASTEXITCODE"),
                    script.index("if ($revParseStatus -ne 0)"),
                )
                self.assertIn("$actualHead = $actualHead.Trim()", script)
                self.assertIn("$actualHead -cne $env:NFC_PR_HEAD_SHA", script)
                self.assertIn(
                    "& git merge-base --is-ancestor $env:NFC_PR_BASE_SHA HEAD",
                    script,
                )
                self.assertIn("$ancestryStatus = $LASTEXITCODE", script)
                self.assertLess(
                    script.index(
                        "& git merge-base --is-ancestor $env:NFC_PR_BASE_SHA HEAD"
                    ),
                    script.index("$ancestryStatus = $LASTEXITCODE"),
                )
                self.assertLess(
                    script.index("$ancestryStatus = $LASTEXITCODE"),
                    script.index("if ($ancestryStatus -ne 0)"),
                )

    def test_code_health_extends_the_restored_build_job_without_changing_checks(self) -> None:
        expected_checks = {
            "structure": "policy / polytail",
            "python-worker": "python-worker / verify",
            "repository-scripts": "python / repository policy (${{ matrix.shard }})",
            "dotnet-build": "dotnet / build",
            "dotnet-test": "dotnet / test (${{ matrix.shard }})",
            "dotnet": "dotnet / build-test",
        }
        command = (
            "pwsh -NoProfile -File eng/core-health/repo-health.ps1 -Mode Verify "
            "-Repo nfc -Root . -Solution NvtFwCombiner.slnx"
        )
        paths = (ROOT / ".github/workflows/ci.yml",
                 ROOT / "docs/ci/workflow-templates/ci.yml")
        self.assertEqual(paths[0].read_bytes(), paths[1].read_bytes())
        for path in paths:
            with self.subTest(workflow=path):
                workflow = yaml.safe_load(path.read_text(encoding="utf-8"))
                jobs = workflow["jobs"]
                self.assertEqual(expected_checks, {key: job["name"] for key, job in jobs.items()})
                self.assertEqual({"contents": "read"}, workflow["permissions"])
                self.assertNotIn("pull_request_target", path.read_text(encoding="utf-8"))
                job = jobs["dotnet-build"]
                self.assertEqual(30, job["timeout-minutes"])
                steps = job["steps"]
                checks = [step for owner in jobs.values() for step in owner["steps"]
                          if "eng/core-health/repo-health.ps1" in step.get("run", "")]
                self.assertEqual(1, len(checks))
                health = checks[0]
                build = next(step for step in steps
                             if step.get("run") == "python scripts/verify.py --ci-dotnet-build")
                self.assertEqual(health, steps[steps.index(build) + 1])
                self.assertEqual("pwsh", health["shell"])
                self.assertNotIn("if", health)
                self.assertNotIn("continue-on-error", health)
                self.assertEqual({
                    "NFC_EVENT_NAME": "${{ github.event_name }}",
                    "NFC_PR_BASE_SHA": "${{ github.event.pull_request.base.sha }}",
                    "DOTNET_CLI_UI_LANGUAGE": "en",
                    "VSLANG": "1033",
                    "PreferredUILang": "en-US",
                    "MSBUILDDISABLENODEREUSE": "1",
                    "UseSharedCompilation": "false",
                    "DOTNET_CLI_USE_MSBUILD_SERVER": "0",
                }, health["env"])
                lines = [line.strip() for line in health["run"].splitlines()]
                self.assertIn(command + " -BaseRef $env:NFC_PR_BASE_SHA", lines)
                self.assertIn(command, lines)
                self.assertNotIn("${{", health["run"])
                self.assertEqual("exit $LASTEXITCODE", lines[-1])
                uploads = [step for step in steps
                           if step.get("with", {}).get("path") == "artifacts/code-health/"]
                self.assertEqual(1, len(uploads))
                artifact = uploads[0]
                log = next(step for step in steps if step.get("name") == "Upload dotnet build log")
                self.assertEqual("failure()", artifact["if"])
                self.assertIs(True, artifact["continue-on-error"])
                self.assertEqual(log["uses"], artifact["uses"])
                self.assertEqual("ignore", artifact["with"]["if-no-files-found"])
                self.assertEqual(log["with"]["retention-days"], artifact["with"]["retention-days"])

    def test_code_health_uses_pr_base_fails_closed_and_propagates_verify_status(self) -> None:
        shell = shutil.which("pwsh")
        if shell is None:
            self.skipTest("PowerShell is unavailable.")
        workflow = yaml.safe_load((ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8"))
        health = next(step for step in workflow["jobs"]["dotnet-build"]["steps"]
                      if step.get("name") == "Verify enrolled C# code health")
        checker = self.root / "eng/core-health/repo-health.ps1"
        checker.parent.mkdir(parents=True)
        checker.write_text(
            "param([string]$Mode, [string]$Repo, [string]$Root, "
            "[string]$Solution, [string]$BaseRef)\n"
            "@{mode=$Mode; repo=$Repo; root=$Root; solution=$Solution; "
            "base=$BaseRef; hasBase=$PSBoundParameters.ContainsKey('BaseRef')} "
            "| ConvertTo-Json -Compress\nexit ([int]$env:NFC_STUB_EXIT)\n",
            encoding="utf-8",
        )
        base = "a" * 40
        cases = (("pull_request", base, 0), ("push", "", 0), ("push", "unused", 0),
                 ("pull_request", "", 0), ("pull_request", " ", 0),
                 ("pull_request", base, 7), ("push", "", 7))
        for event, base_sha, exit_code in cases:
            with self.subTest(event=event, base=base_sha, exit=exit_code):
                environment = os.environ.copy()
                environment.update(health["env"])
                environment.update(NFC_EVENT_NAME=event, NFC_PR_BASE_SHA=base_sha,
                                   NFC_STUB_EXIT=str(exit_code))
                completed = subprocess.run(
                    [shell, "-NoProfile", "-NonInteractive", "-Command",
                     "[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)\n"
                     + health["run"]],
                    cwd=self.root, env=environment, capture_output=True,
                    text=True, encoding="utf-8", timeout=30, check=False,
                )
                if event == "pull_request" and not base_sha.strip():
                    self.assertNotEqual(0, completed.returncode)
                    self.assertEqual("", completed.stdout.strip())
                    continue
                self.assertEqual(exit_code, completed.returncode, completed.stderr)
                self.assertEqual({
                    "mode": "Verify", "repo": "nfc", "root": ".",
                    "solution": "NvtFwCombiner.slnx",
                    "base": base_sha if event == "pull_request" else "",
                    "hasBase": event == "pull_request",
                }, json.loads(completed.stdout))

    def test_ci_workflow_validator_rejects_every_non_windows_topology(self) -> None:
        expected_jobs = {
            "structure",
            "python-worker",
            "repository-scripts",
            "dotnet-build",
            "dotnet-test",
            "dotnet",
        }

        def workflow_text(overrides: dict[str, object] | None = None) -> str:
            jobs: dict[str, object] = {
                name: {"runs-on": "windows-latest", "steps": []}
                for name in expected_jobs
            }
            jobs.update(overrides or {})
            return yaml.safe_dump({"jobs": jobs}, sort_keys=False)

        workflow_path = self.root / "ci.yml"
        workflow_path.write_text(workflow_text(), encoding="utf-8")
        errors: list[str] = []
        repository_validator._validate_windows_only_ci_topology(  # noqa: SLF001
            workflow_path,
            errors,
        )
        self.assertEqual([], errors)

        invalid_cases: tuple[tuple[str, dict[str, object]], ...] = (
            ("script-runner", {"repository-scripts": {"runs-on": "ubuntu-latest", "steps": []}}),
            ("ubuntu", {"structure": {"runs-on": "ubuntu-latest", "steps": []}}),
            ("macos", {"structure": {"runs-on": "macos-latest", "steps": []}}),
            ("self-hosted", {"structure": {"runs-on": "self-hosted", "steps": []}}),
            ("expression", {"structure": {"runs-on": "${{ matrix.runner }}", "steps": []}}),
            ("sequence", {"structure": {"runs-on": ["self-hosted", "Windows"], "steps": []}}),
            ("missing-runner", {"structure": {"steps": []}}),
            ("extra-job", {"extra": {"runs-on": "windows-latest", "steps": []}}),
        )
        for name, override in invalid_cases:
            with self.subTest(case=name):
                workflow_path.write_text(workflow_text(override), encoding="utf-8")
                errors = []
                repository_validator._validate_windows_only_ci_topology(  # noqa: SLF001
                    workflow_path,
                    errors,
                )
                self.assertTrue(errors)

        missing_job_text = workflow_text()
        parsed = yaml.safe_load(missing_job_text)
        del parsed["jobs"]["structure"]
        workflow_path.write_text(
            yaml.safe_dump(parsed, sort_keys=False),
            encoding="utf-8",
        )
        errors = []
        repository_validator._validate_windows_only_ci_topology(  # noqa: SLF001
            workflow_path,
            errors,
        )
        self.assertTrue(errors)

        workflow_path.write_text("jobs: [", encoding="utf-8")
        errors = []
        repository_validator._validate_windows_only_ci_topology(  # noqa: SLF001
            workflow_path,
            errors,
        )
        self.assertTrue(errors)

"""Behavioral tests for the encrypted debug archive policy and its workflows."""

from __future__ import annotations

import hashlib
import importlib.util
import json
import re
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import yaml


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "release_promotion_policy.py"
SPEC = importlib.util.spec_from_file_location("release_promotion_policy_debug", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)

DEBUG_WORKFLOW = ROOT / ".github" / "workflows" / "debug-archive.yml"
RELEASE_WORKFLOW = ROOT / ".github" / "workflows" / "release.yml"
REPOSITORY = "owner/repository"
SOURCE_SHA = "a" * 40
VERSION = "1.3.0"
ARCHIVE_NAME = f"NvtFwCombiner-v{VERSION}-debug.7z"
ARCHIVE_BYTES = MODULE.DEBUG_ARCHIVE_SIGNATURE + b"encrypted payload"
PINNED_ACTION = re.compile(r"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[0-9a-f]{40}$")


def run_row(**changes: object) -> dict[str, object]:
    row: dict[str, object] = {
        "id": 700,
        "run_number": 7,
        "run_attempt": 1,
        "path": ".github/workflows/debug-archive.yml",
        "head_sha": SOURCE_SHA,
        "head_branch": "main",
        "event": "workflow_run",
        "status": "completed",
        "conclusion": "success",
        "repository": {"full_name": REPOSITORY},
    }
    row.update(changes)
    return row


def artifact_inventory(name: str, run_id: int = 700, **changes: object) -> dict[str, object]:
    row: dict[str, object] = {
        "name": name,
        "expired": False,
        "workflow_run": {"id": run_id, "head_sha": SOURCE_SHA},
    }
    row.update(changes)
    return {"total_count": 1, "artifacts": [row]}


class DebugArchiveNamingTests(unittest.TestCase):
    def test_archive_starts_with_version_1_2_2(self) -> None:
        self.assertFalse(MODULE.debug_archive_required("1.2.1"))
        self.assertFalse(MODULE.debug_archive_required("0.9.16"))
        self.assertTrue(MODULE.debug_archive_required("1.2.2"))
        self.assertTrue(MODULE.debug_archive_required("1.10.0"))
        self.assertTrue(MODULE.debug_archive_required("2.0.0"))
        for invalid in ("v1.2.2", "1.2", "1.2.2-g.1", ""):
            with self.subTest(version=invalid), self.assertRaises(ValueError):
                MODULE.debug_archive_required(invalid)

    def test_names_are_fixed_by_version_and_source(self) -> None:
        self.assertEqual(ARCHIVE_NAME, MODULE.debug_archive_name(VERSION))
        self.assertEqual(
            f"debug-archive-v{VERSION}-{SOURCE_SHA}",
            MODULE.debug_archive_artifact_name(VERSION, SOURCE_SHA),
        )
        with self.assertRaisesRegex(ValueError, "starts with version 1.2.2"):
            MODULE.debug_archive_name("1.2.1")
        with self.assertRaisesRegex(ValueError, "Git SHA"):
            MODULE.debug_archive_artifact_name(VERSION, "abc")


class DebugArchiveSelectionTests(unittest.TestCase):
    def select(self, rows: list[object]) -> dict[str, object]:
        return MODULE.select_debug_archive_run(
            rows, repository=REPOSITORY, source_sha=SOURCE_SHA
        )

    def test_newest_successful_automatic_run_of_the_exact_source_is_selected(self) -> None:
        older = run_row(id=1, run_number=3)
        newer = run_row(id=2, run_number=9)
        self.assertEqual(2, self.select([older, newer])["id"])
        self.assertEqual(2, self.select([newer, older])["id"])
        suffixed = run_row(id=3, run_number=10, path=".github/workflows/debug-archive.yml@refs/heads/main")
        self.assertEqual(3, self.select([older, suffixed])["id"])

    def test_every_provenance_mismatch_is_ignored(self) -> None:
        mismatches = (
            {"head_sha": "b" * 40},
            {"head_branch": "feature/x"},
            {"event": "workflow_dispatch"},
            {"event": "push"},
            {"status": "in_progress"},
            {"conclusion": "failure"},
            {"conclusion": "cancelled"},
            {"path": ".github/workflows/other.yml"},
            {"repository": {"full_name": "fork/repository"}},
            {"id": True},
        )
        for change in mismatches:
            with self.subTest(change=change), self.assertRaisesRegex(
                ValueError, "no successful debug archive run"
            ):
                self.select([run_row(**change)])
        with self.assertRaisesRegex(ValueError, "no successful debug archive run"):
            self.select([])
        with self.assertRaisesRegex(ValueError, "inventory is malformed"):
            MODULE.select_debug_archive_run(
                {}, repository=REPOSITORY, source_sha=SOURCE_SHA
            )

    def test_artifact_must_be_unique_live_and_owned_by_the_selected_run(self) -> None:
        name = MODULE.debug_archive_artifact_name(VERSION, SOURCE_SHA)
        row = MODULE.select_debug_archive_artifact(
            artifact_inventory(name), name=name, run_id=700, source_sha=SOURCE_SHA
        )
        self.assertEqual(name, row["name"])
        duplicate = artifact_inventory(name)
        duplicate["artifacts"].append(dict(duplicate["artifacts"][0]))
        cases = (
            (artifact_inventory("other"), "missing or ambiguous"),
            (duplicate, "missing or ambiguous"),
            (artifact_inventory(name, expired=True), "has expired"),
            (artifact_inventory(name, run_id=701), "another run or source"),
            (
                artifact_inventory(
                    name, workflow_run={"id": 700, "head_sha": "b" * 40}
                ),
                "another run or source",
            ),
            ({"artifacts": "no"}, "inventory is malformed"),
        )
        for inventory, message in cases:
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                MODULE.select_debug_archive_artifact(
                    inventory, name=name, run_id=700, source_sha=SOURCE_SHA
                )

    def test_download_must_be_exactly_one_7z_with_the_expected_name(self) -> None:
        with tempfile.TemporaryDirectory(prefix="debug-archive-download-") as temporary:
            root = Path(temporary)
            (root / ARCHIVE_NAME).write_bytes(ARCHIVE_BYTES)
            self.assertEqual(
                root / ARCHIVE_NAME, MODULE.verify_debug_archive_download(root, VERSION)
            )
            (root / "extra.txt").write_bytes(b"x")
            with self.assertRaisesRegex(ValueError, "exactly the expected single file"):
                MODULE.verify_debug_archive_download(root, VERSION)
            (root / "extra.txt").unlink()
            (root / ARCHIVE_NAME).write_bytes(b"PK\x03\x04 not a 7z file")
            with self.assertRaisesRegex(ValueError, "not a 7z file"):
                MODULE.verify_debug_archive_download(root, VERSION)
            (root / ARCHIVE_NAME).write_bytes(MODULE.DEBUG_ARCHIVE_SIGNATURE)
            with self.assertRaisesRegex(ValueError, "outside the release asset bounds"):
                MODULE.verify_debug_archive_download(root, VERSION)
            (root / ARCHIVE_NAME).unlink()
            (root / "NvtFwCombiner-v1.3.1-debug.7z").write_bytes(ARCHIVE_BYTES)
            with self.assertRaisesRegex(ValueError, "exactly the expected single file"):
                MODULE.verify_debug_archive_download(root, VERSION)


class DebugArchiveFetchTests(unittest.TestCase):
    def setUp(self) -> None:
        temporary = tempfile.TemporaryDirectory(prefix="debug-archive-fetch-")
        self.addCleanup(temporary.cleanup)
        self.destination = Path(temporary.name)
        self.name = MODULE.debug_archive_artifact_name(VERSION, SOURCE_SHA)
        self.reads: list[list[str]] = []

    def read(self, arguments: list[str], label: str) -> object:
        self.reads.append(arguments)
        endpoint = arguments[3]
        if endpoint.endswith("/actions/workflows/debug-archive.yml/runs"):
            return {"workflow_runs": [run_row()]}
        if endpoint.endswith("/actions/runs/700/artifacts"):
            return artifact_inventory(self.name)
        raise AssertionError(f"unexpected GitHub read: {endpoint}")

    def download(self, payload: bytes = ARCHIVE_BYTES, returncode: int = 0):
        def execute(arguments: list[str]) -> subprocess.CompletedProcess[str]:
            self.assertEqual(["run", "download", "700"], arguments[:3])
            self.assertEqual(["--repo", REPOSITORY, "--name", self.name], arguments[3:7])
            if returncode == 0:
                (Path(arguments[arguments.index("--dir") + 1]) / ARCHIVE_NAME).write_bytes(payload)
            return subprocess.CompletedProcess(arguments, returncode, "", "")

        return execute

    def fetch(self, **overrides: object) -> dict[str, object]:
        arguments = dict(
            repository=REPOSITORY, source_sha=SOURCE_SHA, version=VERSION,
            destination=self.destination,
        )
        arguments.update(overrides)
        return MODULE.fetch_debug_archive(**arguments)

    def test_fetch_copies_the_verified_archive_and_reports_identity(self) -> None:
        with mock.patch.object(MODULE, "_read_github_json", side_effect=self.read), \
                mock.patch.object(MODULE, "_execute_gh", side_effect=self.download()):
            result = self.fetch()
        copied = self.destination / ARCHIVE_NAME
        self.assertEqual(ARCHIVE_BYTES, copied.read_bytes())
        self.assertEqual(
            {
                "attached": True,
                "runId": 700,
                "name": ARCHIVE_NAME,
                "size": len(ARCHIVE_BYTES),
                "sha256": hashlib.sha256(ARCHIVE_BYTES).hexdigest(),
            },
            result,
        )
        self.assertEqual(2, len(self.reads))
        for arguments in self.reads:
            self.assertEqual(["api", "--method", "GET"], arguments[:3])
        self.assertIn(f"head_sha={SOURCE_SHA}", self.reads[0])
        self.assertIn("event=workflow_run", self.reads[0])

    def test_versions_before_1_2_2_attach_nothing_and_call_nothing(self) -> None:
        with mock.patch.object(MODULE, "_read_github_json") as read, \
                mock.patch.object(MODULE, "_execute_gh") as execute:
            self.assertEqual({"attached": False}, self.fetch(version="1.2.1"))
        read.assert_not_called()
        execute.assert_not_called()
        self.assertEqual([], list(self.destination.iterdir()))

    def test_failures_leave_the_release_directory_untouched(self) -> None:
        cases = (
            ("failed download", self.download(returncode=1), "could not be downloaded"),
            ("not a 7z", self.download(payload=b"PK\x03\x04" + b"x" * 20), "not a 7z file"),
        )
        for label, execute, message in cases:
            with self.subTest(label=label), \
                    mock.patch.object(MODULE, "_read_github_json", side_effect=self.read), \
                    mock.patch.object(MODULE, "_execute_gh", side_effect=execute):
                with self.assertRaisesRegex(ValueError, message):
                    self.fetch()
                self.assertEqual([], list(self.destination.iterdir()))
        with mock.patch.object(MODULE, "_read_github_json", return_value={"workflow_runs": []}):
            with self.assertRaisesRegex(ValueError, "no successful debug archive run"):
                self.fetch()
        with self.assertRaisesRegex(ValueError, "must already exist"):
            self.fetch(destination=self.destination / "missing")
        with self.assertRaisesRegex(ValueError, "repository is malformed"):
            self.fetch(repository="not a repository")
        (self.destination / ARCHIVE_NAME).write_bytes(b"present")
        with self.assertRaisesRegex(ValueError, "already holds the debug archive"):
            self.fetch()


class DebugArchiveReleaseSetTests(unittest.TestCase):
    def setUp(self) -> None:
        temporary = tempfile.TemporaryDirectory(prefix="debug-archive-release-set-")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.candidate = self.root / "candidate"
        self.published = self.root / "published"
        self.candidate.mkdir()
        self.published.mkdir()
        payload = b"package"
        self.package_name = MODULE._asset_names(VERSION)[0]
        (self.candidate / self.package_name).write_bytes(payload)
        self.manifest = self.candidate / f"NvtFwCombiner-v{VERSION}-candidate.json"
        self.manifest.write_text(
            json.dumps({
                "version": VERSION,
                "assets": [{
                    "name": self.package_name,
                    "size": len(payload),
                    "sha256": hashlib.sha256(payload).hexdigest(),
                }],
            }),
            encoding="utf-8",
        )
        (self.candidate / f"NvtFwCombiner-v{VERSION}-assets.sha256").write_bytes(b"outer")
        (self.candidate / ARCHIVE_NAME).write_bytes(ARCHIVE_BYTES)

    def test_archive_is_one_more_expected_asset_only_when_requested(self) -> None:
        plain = MODULE.expected_published_asset_metadata(self.manifest)
        self.assertNotIn(ARCHIVE_NAME, plain)
        with_archive = MODULE.expected_published_asset_metadata(
            self.manifest, with_debug_archive=True
        )
        self.assertEqual({*plain, ARCHIVE_NAME}, set(with_archive))
        self.assertEqual(
            {"size": len(ARCHIVE_BYTES), "sha256": hashlib.sha256(ARCHIVE_BYTES).hexdigest()},
            with_archive[ARCHIVE_NAME],
        )
        (self.candidate / ARCHIVE_NAME).unlink()
        with self.assertRaisesRegex(ValueError, "published asset is missing"):
            MODULE.expected_published_asset_metadata(self.manifest, with_debug_archive=True)

    def test_archive_before_1_2_2_is_refused(self) -> None:
        old = self.candidate / "NvtFwCombiner-v1.2.1-candidate.json"
        old.write_text(json.dumps({"version": "1.2.1", "assets": []}), encoding="utf-8")
        (self.candidate / "NvtFwCombiner-v1.2.1-assets.sha256").write_bytes(b"outer")
        with self.assertRaisesRegex(ValueError, "starts with version 1.2.2"):
            MODULE.expected_published_asset_metadata(old, with_debug_archive=True)

    def test_recovery_plans_the_missing_archive_and_rejects_conflicts(self) -> None:
        names = [self.package_name]
        (self.published / self.package_name).write_bytes(
            (self.candidate / self.package_name).read_bytes()
        )
        missing = MODULE.plan_release_asset_recovery(
            self.manifest, self.published, names, with_debug_archive=True
        )
        self.assertIn(ARCHIVE_NAME, missing)
        self.assertNotIn(ARCHIVE_NAME, MODULE.plan_release_asset_recovery(
            self.manifest, self.published, names))

        names.append(ARCHIVE_NAME)
        with self.assertRaisesRegex(ValueError, "unexpected assets"):
            MODULE.plan_release_asset_recovery(self.manifest, self.published, names)
        (self.published / ARCHIVE_NAME).write_bytes(ARCHIVE_BYTES)
        missing = MODULE.plan_release_asset_recovery(
            self.manifest, self.published, names, with_debug_archive=True
        )
        self.assertNotIn(ARCHIVE_NAME, missing)
        (self.published / ARCHIVE_NAME).write_bytes(ARCHIVE_BYTES + b"tampered")
        with self.assertRaisesRegex(ValueError, "digest conflicts"):
            MODULE.plan_release_asset_recovery(
                self.manifest, self.published, names, with_debug_archive=True
            )
        (self.candidate / ARCHIVE_NAME).unlink()
        with self.assertRaisesRegex(ValueError, "candidate debug archive is missing"):
            MODULE.plan_release_asset_recovery(
                self.manifest, self.published, names, with_debug_archive=True
            )

    def test_command_line_accepts_the_archive_flag_and_fetch_command(self) -> None:
        cases = (
            ["validate-release", "--release", "r.json", "--expected-tag", "v1.3.0",
             "--expected-body", "b.md", "--manifest", "m.json", "--with-debug-archive"],
            ["plan-recovery", "--manifest", "m.json", "--published-dir", "p",
             "--published-names", "n.json", "--with-debug-archive"],
            ["fetch-debug-archive", "--repository", REPOSITORY, "--source-sha", SOURCE_SHA,
             "--version", VERSION, "--destination", "d"],
        )
        for argv in cases:
            with self.subTest(command=argv[0]), \
                    mock.patch.object(sys, "argv", ["release_promotion_policy.py", *argv]):
                self.assertEqual(argv[0], MODULE.parse_args().command)
        with mock.patch.object(sys, "argv", ["release_promotion_policy.py", *cases[0][:-1]]):
            self.assertFalse(MODULE.parse_args().with_debug_archive)


class DebugArchiveWorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.text = DEBUG_WORKFLOW.read_text(encoding="utf-8")
        cls.workflow = yaml.safe_load(cls.text)
        cls.release = RELEASE_WORKFLOW.read_text(encoding="utf-8")
        cls.pack_steps = {
            step["name"]: step for step in cls.workflow["jobs"]["pack"]["steps"]
        }

    def test_triggers_are_the_main_ci_completion_and_a_manual_rehearsal(self) -> None:
        events = self.workflow.get("on", self.workflow.get(True))
        self.assertEqual(
            {"workflow_run": {"workflows": ["ci"], "types": ["completed"], "branches": ["main"]},
             "workflow_dispatch": None},
            events,
        )
        self.assertNotIn("pull_request", self.text.replace("pull-requests", ""))
        self.assertEqual({"contents": "read"}, self.workflow["permissions"])
        gate = self.workflow["jobs"]["gate"]
        for clause in ("github.ref == 'refs/heads/main'",
                       "github.event.workflow_run.conclusion == 'success'",
                       "github.event.workflow_run.event == 'push'",
                       "github.event.workflow_run.head_branch == 'main'",
                       "github.event.workflow_run.head_sha == github.sha",
                       "github.sha == github.workflow_sha"):
            self.assertIn(clause, gate["if"])

    def test_the_packing_job_is_gated_by_the_environment_and_has_read_only_rights(self) -> None:
        self.assertEqual({"gate", "pack"}, set(self.workflow["jobs"]))
        pack = self.workflow["jobs"]["pack"]
        self.assertEqual("debug-archive", pack["environment"])
        self.assertEqual({"contents": "read"}, pack["permissions"])
        self.assertEqual(["gate"], pack["needs"])
        self.assertEqual("${{ needs.gate.outputs.eligible == 'true' }}", pack["if"])
        self.assertNotIn("environment", self.workflow["jobs"]["gate"])
        self.assertNotIn("contents: write", self.text)
        self.assertEqual(1, self.text.count("environment:"))

    def test_every_action_is_pinned_and_checkouts_drop_credentials(self) -> None:
        for job in self.workflow["jobs"].values():
            for step in job["steps"]:
                if "uses" in step:
                    self.assertRegex(step["uses"], PINNED_ACTION)
                if str(step.get("uses", "")).startswith("actions/checkout@"):
                    self.assertIs(False, step["with"]["persist-credentials"])
                    self.assertEqual("${{ github.workflow_sha }}", step["with"]["ref"])

    def test_secrets_reach_only_the_packing_step_through_its_environment(self) -> None:
        for job in self.workflow["jobs"].values():
            for step in job["steps"]:
                self.assertNotIn("secrets.", step.get("run", ""))
        pack = self.pack_steps["Pack and self-check the encrypted archive"]
        self.assertEqual(
            {"NFC_DEBUG_ARCHIVE_PASSWORD", "TESTDATA_DEPLOY_KEY", "PRIVATE_ASSETS_DEPLOY_KEY"},
            {key for key, value in pack["env"].items() if "secrets." in str(value)},
        )
        self.assertEqual(3, self.text.count("secrets."))
        for other in ("ci.yml", "release.yml", "release-rehearsal.yml"):
            workflow = (ROOT / ".github" / "workflows" / other).read_text(encoding="utf-8")
            self.assertNotIn("NFC_DEBUG_ARCHIVE_PASSWORD", workflow)
            self.assertNotIn("PRIVATE_ASSETS_DEPLOY_KEY", workflow)

    def test_the_archive_is_encrypted_checked_and_only_counts_reach_the_log(self) -> None:
        run = self.pack_steps["Pack and self-check the encrypted archive"]["run"]
        for required in ("-t7z", "-mx=9", "-mhe=on", "-bso0", "-bse0", "-bsp0",
                         "'t'", "'x'", "'l'", "golden_pin.py verify",
                         "The debug archive header is not encrypted.",
                         "if ($failed) { exit 1 }"):
            self.assertIn(required, run)
        self.assertGreaterEqual(run.count("$password.Length -lt 24"), 1)
        # Deliberate failures use the safe prefix; any other error text is withheld.
        self.assertEqual(0, len(re.findall(r"throw '(?!debug-archive: )", run)))
        self.assertIn("details are withheld", run)
        for forbidden in ("Write-Output", "Write-Verbose", "Format-List", "Out-Host", "Tee-Object"):
            self.assertNotIn(forbidden, run)
        self.assertNotIn("Write-Host \"$", run.replace('Write-Host "::error::$text"', ""))

    def test_the_artifact_is_the_7z_alone_with_a_release_or_rehearsal_name(self) -> None:
        pack = self.pack_steps["Pack and self-check the encrypted archive"]["run"]
        self.assertIn('"debug-archive-rehearsal-$($env:GITHUB_RUN_ID)"', pack)
        self.assertIn('"debug-archive-v$($env:NFC_VERSION)-$($env:NFC_SOURCE_SHA)"', pack)
        upload = self.pack_steps["Upload encrypted debug archive"]
        self.assertEqual("${{ steps.pack.outputs.artifact-name }}", upload["with"]["name"])
        self.assertEqual("${{ steps.pack.outputs.archive-path }}", upload["with"]["path"])
        self.assertEqual("error", upload["with"]["if-no-files-found"])
        self.assertEqual(3, upload["with"]["retention-days"])
        self.assertTrue(upload["uses"].startswith("actions/upload-artifact@"))
        self.assertEqual("pack", self.pack_steps["Pack and self-check the encrypted archive"]["id"])

    def test_release_workflow_fetches_the_archive_after_the_closed_candidate_check(self) -> None:
        workflow = yaml.safe_load(self.release)
        steps = {step["name"]: step for step in workflow["jobs"]["promote"]["steps"]}
        verify = steps["Verify closed candidate identity and digests"]["run"]
        self.assertLess(verify.index("verify-manifest"), verify.index("fetch-debug-archive"))
        names = [step["name"] for step in workflow["jobs"]["promote"]["steps"]]
        self.assertLess(
            names.index("Verify closed candidate identity and digests"),
            names.index("Create or verify immutable annotated tag with fresh repository admission"),
        )
        publish = steps["Publish or validate GitHub Release"]["run"]
        verify_published = steps["Verify published assets and source downloads"]["run"]
        self.assertIn("--with-debug-archive", publish)
        self.assertIn("--with-debug-archive", verify_published)
        self.assertEqual(4, self.release.count("@debugArchiveArgument"))
        self.assertEqual(1, self.release.count("contents: write"))
        self.assertEqual(1, self.release.count("fetch-debug-archive"))
        self.assertIn(
            "actions: read", yaml.dump(workflow["jobs"]["promote"]["permissions"]),
        )


if __name__ == "__main__":
    unittest.main()

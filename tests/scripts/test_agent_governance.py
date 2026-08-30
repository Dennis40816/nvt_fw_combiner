"""Behavioral tests for tracked, diff-bound capability-reuse admission."""

from __future__ import annotations

import hashlib
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from typing import Any
from unittest import mock

import yaml

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))

from validate_repository import (  # noqa: E402
    _capability_path_state_digest,
    _is_capability_reuse_governed_path,
    _parse_git_name_status,
    validate_capability_reuse_governance,
)
import validate_repository as repository_validator  # noqa: E402


class AgentGovernanceTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)
        self._git("init", "-q")
        self._git("config", "user.name", "Governance Test")
        self._git("config", "user.email", "governance@example.invalid")
        self._git("config", "core.autocrlf", "false")
        self._write(".gitignore", "artifacts/\n")
        self._write("AGENTS.md", "# Baseline instructions\n")
        self._write("src/Product/Owner.cs", "internal sealed class Owner {}\n")
        self._write("src/Product/Other.cs", "internal sealed class Other {}\n")
        self._write("scratch/Source.cs", "internal sealed class Source {}\n")
        self._git("add", ".")
        self._git("commit", "-q", "-m", "baseline")
        self.integration_base = self._git("rev-parse", "HEAD").stdout.strip()
        self.trusted_initial_base = self.integration_base

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    def _git(self, *arguments: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            ["git", *arguments],
            cwd=self.root,
            check=True,
            capture_output=True,
            text=True,
        )

    def _write(self, relative: str, text: str) -> None:
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(text.encode("utf-8"))

    def _record(
        self,
        task_id: str = "TEST-01",
        paths: list[str] | None = None,
        **overrides: Any,
    ) -> dict[str, Any]:
        record: dict[str, Any] = {
            "schemaVersion": 2,
            "taskId": task_id,
            "capability": "One test behavior",
            "integrationBase": self.integration_base,
            "risk": "R2",
            "kind": "behavior",
            "state": "design-active",
            "mutablePaths": paths if paths is not None else ["src/Product/Owner.cs"],
            "implementationOwner": "implementer",
            "searchEvidence": ["rg -n 'Owner' src tests"],
            "semanticOwner": "Product.Owner",
            "terminalContract": "One typed terminal result",
            "disposition": "extend-owner",
            "designReview": {
                "reviewer": "architect",
                "outcome": "approved",
                "evidence": "Independent dependency and caller review.",
            },
            "implementationHead": None,
            "reviewedHead": None,
            "pathStateDigest": None,
            "finalReview": {
                "reviewer": None,
                "outcome": "pending",
                "evidence": "",
            },
        }
        record.update(overrides)
        return record

    def _write_record(
        self,
        record: dict[str, Any],
        relative: str | None = None,
        *,
        stage: bool = True,
    ) -> None:
        relative = relative or f"docs/governance/change-records/{record['taskId']}.json"
        self._write(relative, json.dumps(record, indent=2) + "\n")
        if stage:
            self._git("add", "--", relative)

    def _change(self, relative: str = "src/Product/Owner.cs") -> None:
        self._write(relative, "internal sealed class Owner { public int Value => 1; }\n")

    def validate(self) -> list[str]:
        errors: list[str] = []
        validate_capability_reuse_governance(
            self.root,
            errors,
            trusted_initial_base=self.trusted_initial_base,
        )
        return errors

    def _final_record(
        self,
        task_id: str = "TEST-01",
        paths: list[str] | None = None,
        **overrides: Any,
    ) -> dict[str, Any]:
        mutable_paths = paths if paths is not None else ["src/Product/Owner.cs"]
        reviewed_head = self._git("rev-parse", "HEAD").stdout.strip()
        digest, error = _capability_path_state_digest(
            self.root,
            reviewed_head,
            mutable_paths,
        )
        self.assertIsNone(error)
        record = self._record(
            task_id,
            mutable_paths,
            state="final-complete",
            implementationHead=reviewed_head,
            reviewedHead=reviewed_head,
            pathStateDigest=digest,
            finalReview={
                "reviewer": "final-reviewer",
                "outcome": "approved",
                "evidence": "Reviewed the frozen implementation head and focused evidence.",
            },
        )
        record.update(overrides)
        return record

    def _commit_implementation(self, relative: str = "src/Product/Owner.cs") -> str:
        self._change(relative)
        self._git("add", "--", relative)
        self._git("commit", "-q", "-m", "implement behavior")
        return self._git("rev-parse", "HEAD").stdout.strip()

    def _commit_candidate_with_active_record(self) -> str:
        self._change()
        self._write_record(self._record())
        self._git("add", "--", "src/Product/Owner.cs")
        self._git("commit", "-q", "-m", "implement admitted behavior")
        return self._git("rev-parse", "HEAD").stdout.strip()

    def _finalize_first_batch(self) -> str:
        self._commit_candidate_with_active_record()
        self._write_record(self._final_record())
        self._git("commit", "-q", "-m", "finalize capability evidence")
        return self._git("rev-parse", "HEAD").stdout.strip()

    def _validate_derived_checkpoint(self) -> list[str]:
        errors: list[str] = []
        validate_capability_reuse_governance(self.root, errors)
        return errors

    def _activate_trusted_checkpoint(
        self,
        *,
        mutate_manifest: Any | None = None,
        extra_activation_path: bool = False,
    ) -> tuple[str, dict[str, Any]]:
        reviewed_head = self._git("rev-parse", "HEAD").stdout.strip()
        reviewed_tree = self._git("rev-parse", f"{reviewed_head}^{{tree}}").stdout.strip()
        record_paths = sorted(
            value
            for value in self._git(
                "ls-tree",
                "-r",
                "--name-only",
                reviewed_head,
                "--",
                "docs/governance/change-records",
            ).stdout.splitlines()
            if Path(value).parent.as_posix() == "docs/governance/change-records"
            and Path(value).suffix == ".json"
        )
        legacy_records: list[dict[str, Any]] = []
        open_authorities: list[dict[str, Any]] = []
        for relative in record_paths:
            content = subprocess.run(
                ["git", "show", f"{reviewed_head}:{relative}"],
                cwd=self.root,
                check=True,
                capture_output=True,
            ).stdout
            value = json.loads(content.decode("utf-8"))
            legacy_records.append(
                {
                    "taskId": value["taskId"],
                    "path": relative,
                    "risk": value["risk"],
                    "state": value["state"],
                    "contentSha256": hashlib.sha256(content).hexdigest(),
                }
            )
            if value["risk"] == "R3":
                open_authorities.append(
                    {
                        "taskId": value["taskId"],
                        "authorityType": (
                            "firmware-owner"
                            if value["taskId"] == "FORMAL-SUPPORT-01"
                            else "release-owner"
                        ),
                        "status": "pending",
                    }
                )
        manifest: dict[str, Any] = {
            "schemaVersion": 1,
            "checkpointId": "CAPABILITY-REUSE-INITIAL-100",
            "reviewedHead": reviewed_head,
            "reviewedTree": reviewed_tree,
            "ownerDecisionRef": "test repository owner approval",
            "legacyRecords": legacy_records,
            "openR3Authorities": sorted(open_authorities, key=lambda item: item["taskId"]),
        }
        if mutate_manifest is not None:
            mutate_manifest(manifest)
        relative = "docs/governance/trusted-initial-capability-checkpoint.v1.json"
        self._write(relative, json.dumps(manifest, indent=2) + "\n")
        self._git("add", "--", relative)
        for record_path in record_paths:
            self._git("rm", "-q", "--", record_path)
        if extra_activation_path:
            self._write("scratch/activation-extra.txt", "not allowed\n")
            self._git("add", "--", "scratch/activation-extra.txt")
        self._git("commit", "-q", "-m", "activate trusted checkpoint")
        return self._git("rev-parse", "HEAD").stdout.strip(), manifest

    def _write_external_authority_batch(
        self,
        manifest: dict[str, Any],
    ) -> str:
        reviewed_head = self._git("rev-parse", "HEAD").stdout.strip()
        for authority in manifest["openR3Authorities"]:
            task_id = authority["taskId"]
            relative = f"docs/governance/external-authority-attestations/{task_id}.json"
            self._write(
                relative,
                json.dumps(
                    {
                        "schemaVersion": 1,
                        "taskId": task_id,
                        "authorityType": authority["authorityType"],
                        "reviewedHead": reviewed_head,
                        "decision": "approved",
                        "reviewer": f"test-{authority['authorityType']}",
                        "evidence": "Exact-head test authority evidence.",
                    },
                    indent=2,
                )
                + "\n",
            )
            self._git("add", "--", relative)
        self._git("commit", "-q", "-m", "record external authority evidence")
        return self._git("rev-parse", "HEAD").stdout.strip()

    def _merge_descendant_as_redundant_second_parent(
        self,
        descendant: str,
        ancestor: str,
        *,
        mutate_merge_tree: bool = False,
    ) -> str:
        branch = f"reviewed-{descendant[:12]}"
        self._git("branch", branch, descendant)
        self._git("checkout", "-q", "--detach", ancestor)
        merge_arguments = ["merge", "--no-ff", "-q", "-m", "redundant containment merge"]
        if mutate_merge_tree:
            merge_arguments.extend(["--no-commit"])
        merge_arguments.append(branch)
        self._git(*merge_arguments)
        if mutate_merge_tree:
            self._write("scratch/merge-only.txt", "merge-only tree mutation\n")
            self._git("add", "--", "scratch/merge-only.txt")
            self._git("commit", "-q", "-m", "distinct merge tree")
        return self._git("rev-parse", "HEAD").stdout.strip()

    def test_authority_classifier_covers_each_governed_surface_without_nearby_spill(self) -> None:
        cases = (
            ("AGENTS.md", "README.md"),
            ("tests/AGENTS.md", "tests/README.md"),
            (".agents/skills/implement/SKILL.md", ".agents/agents/implement.toml"),
            ("docs/governance/workflow.md", "docs/governance/change-records/TEST-01.json"),
            ("docs/policies/polytail.md", "docs/policy/polytail.md"),
            ("docs/adr/0051.md", "docs/adrs/0051.md"),
            ("docs/specs/version.md", "docs/spec/version.md"),
            ("docs/contracts/catalog.json", "docs/contract/catalog.json"),
            ("docs/ci/release-package.md", "docs/ci/package-notes.md"),
            (".github/workflows/release.yml", ".github/actions/release.yml"),
            ("profiles/nt51950.json", "profile/nt51950.json"),
            ("testdata/golden/manifest.json", "testdata/diagnostics/manifest.json"),
            ("eng/build.ps1", "engineering/build.ps1"),
            ("tools/crc-worker/worker.py", "tools/crc-worker/tests/test_worker.py"),
            ("src/Product/Owner.cs", "tests/Product/OwnerTests.cs"),
            ("scripts/verify.py", "tests/scripts/verify.py"),
            ("scripts/polytail_check.py", "tools/polytail_check.py"),
            ("scripts/canonical_golden_validation.py", "scripts/canonical_golden_validation.md"),
            ("scripts/external_tool_policy.py", "docs/external_tool_policy.py"),
            ("scripts/repository_contract_validation.py", "scripts/repository_contract_validation.json"),
            ("scripts/skill_metadata_validation.py", ".agents/skill_metadata_validation.py"),
            ("scripts/coverage_policy.py", "tests/scripts/coverage_policy.py"),
            ("scripts/package.ps1", "scripts/package.cmd"),
            ("scripts/publish-github.sh", "tools/publish-github.sh"),
        )
        for governed, nearby_negative in cases:
            with self.subTest(governed=governed):
                self.assertTrue(_is_capability_reuse_governed_path(governed))
                self.assertFalse(_is_capability_reuse_governed_path(nearby_negative))
        self.assertFalse(_is_capability_reuse_governed_path("tools/crc-worker/.pytest_cache/state"))
        self.assertFalse(_is_capability_reuse_governed_path("tools/crc-worker/cache/state"))
        self.assertFalse(_is_capability_reuse_governed_path("tools/crc-worker/.cache/state"))
        self.assertFalse(_is_capability_reuse_governed_path("scripts/README.md"))
        self.assertFalse(
            _is_capability_reuse_governed_path(
                "docs/governance/external-authority-attestations/TEST-01.json"
            )
        )
        self.assertTrue(
            _is_capability_reuse_governed_path(
                "docs/governance/external-authority-attestations/UNDECLARED.txt"
            )
        )
        self.assertTrue(
            _is_capability_reuse_governed_path(
                "docs/governance/external-authority-attestations/TEST-01.JSON"
            )
        )
        self.assertTrue(
            _is_capability_reuse_governed_path(
                "docs/governance/external-authority-attestations/nested/TEST-01.json"
            )
        )

    def test_name_status_parser_preserves_both_rename_and_copy_sides(self) -> None:
        payload = (
            b"A\0added\0M\0modified\0T\0typed\0D\0deleted\0"
            b"R100\0rename-old\0rename-new\0C090\0copy-old\0copy-new\0"
        )

        self.assertEqual(
            {
                "added",
                "modified",
                "typed",
                "deleted",
                "rename-old",
                "rename-new",
                "copy-old",
                "copy-new",
            },
            _parse_git_name_status(payload),
        )

    def test_missing_record_fails_closed(self) -> None:
        self._change()

        self.assertTrue(any("lacks a design-active" in error for error in self.validate()))

    def test_untracked_nonignored_record_cannot_open_gate(self) -> None:
        self._change()
        self._write_record(self._record(), stage=False)

        errors = self.validate()

        self.assertTrue(any("must be tracked in Git" in error for error in errors))

    def test_ignored_record_cannot_open_gate(self) -> None:
        self._change()
        self._write(
            ".gitignore",
            "artifacts/\ndocs/governance/change-records/TEST-01.json\n",
        )
        self._write_record(self._record(), stage=False)

        self.assertTrue(any("record is ignored" in error for error in self.validate()))

    def test_committed_nested_change_record_is_rejected_before_parsing(self) -> None:
        self._change()
        relative = "docs/governance/change-records/nested/TEST-01.json"
        self._write_record(self._record(), relative)
        self._git("add", "--", "src/Product/Owner.cs")
        self._git("commit", "-q", "-m", "commit invalid nested record")

        self.assertTrue(any("record parent must be exactly" in error for error in self.validate()))

    def test_committed_nested_change_record_cannot_be_reused_at_direct_parent(self) -> None:
        self._change()
        nested = "docs/governance/change-records/nested/TEST-01.json"
        direct = "docs/governance/change-records/TEST-01.json"
        self._write_record(self._record(), nested)
        self._git("add", "--", "src/Product/Owner.cs")
        self._git("commit", "-q", "-m", "commit invalid nested record")
        self._git("mv", nested, direct)

        self.assertTrue(any("record parent must be exactly" in error for error in self.validate()))

    def test_invalid_json_is_rejected(self) -> None:
        self._change()
        relative = "docs/governance/change-records/TEST-01.json"
        self._write(relative, "{ invalid\n")
        self._git("add", "--", relative)

        self.assertTrue(any("invalid capability-reuse JSON" in error for error in self.validate()))

    def test_old_schema_is_rejected(self) -> None:
        self._change()
        self._write_record(self._record(schemaVersion=0))

        self.assertTrue(any("requires schemaVersion 2" in error for error in self.validate()))

    def test_changed_governed_path_must_be_declared(self) -> None:
        self._change()
        self._change("src/Product/Other.cs")
        self._write_record(self._record())

        self.assertTrue(any(error.endswith("src/Product/Other.cs") for error in self.validate()))

    def test_changed_path_cannot_have_duplicate_coverage(self) -> None:
        self._change()
        self._write_record(self._record("TEST-01"))
        self._write_record(self._record("TEST-02"))

        self.assertTrue(any("duplicate capability-reuse coverage" in error for error in self.validate()))

    def test_filename_must_equal_valid_unique_task_id(self) -> None:
        self._change()
        record = self._record("bad id")
        self._write_record(record, "docs/governance/change-records/TEST-01.json")

        errors = self.validate()

        self.assertTrue(any("taskId has invalid format" in error for error in errors))
        self.assertTrue(any("filename must equal taskId" in error for error in errors))

    def test_duplicate_task_id_is_rejected(self) -> None:
        self._change()
        record = self._record("TEST-01")
        self._write_record(record)
        self._write_record(record, "docs/governance/change-records/TEST-02.json")

        with mock.patch.object(repository_validator, "_historical_final_records") as history_audit:
            errors = self.validate()

        history_audit.assert_not_called()
        self.assertTrue(any("taskId must be unique" in error for error in errors))

    def test_r2_design_reviewer_must_be_independent(self) -> None:
        self._change()
        self._write_record(self._record(designReview={
            "reviewer": "IMPLEMENTER",
            "outcome": "approved",
            "evidence": "Self review is not independent.",
        }))

        self.assertTrue(any("reviewer must be independent" in error for error in self.validate()))

    def test_blocked_record_requires_empty_mutable_paths(self) -> None:
        self._change()
        self._write_record(self._record(
            state="blocked",
            disposition="reject-duplicate",
            designReview={
                "reviewer": "architect",
                "outcome": "blocked",
                "evidence": "Decision remains open.",
            },
        ))

        self.assertTrue(any("blocked capability-reuse record requires empty" in error for error in self.validate()))

    def test_blocked_r2_record_still_requires_independent_reviewer(self) -> None:
        self._change()
        self._write_record(self._record(
            paths=[],
            state="blocked",
            disposition="reject-duplicate",
            designReview={
                "reviewer": "implementer",
                "outcome": "blocked",
                "evidence": "Self review is not independent.",
            },
        ))

        self.assertTrue(any("reviewer must be independent" in error for error in self.validate()))

    def test_r0_record_cannot_cover_agents_authority(self) -> None:
        self._write("AGENTS.md", "# Changed instructions\n")
        self._write_record(self._record(
            paths=["AGENTS.md"],
            risk="R0",
            designReview={
                "reviewer": None,
                "outcome": "not-required",
                "evidence": "R0 documentation review.",
            },
        ))

        self.assertTrue(any("risk is below path minimum R2" in error for error in self.validate()))

    def test_r1_record_cannot_cover_profile_authority(self) -> None:
        self._write("profiles/new.json", "{}\n")
        self._write_record(self._record(
            paths=["profiles/new.json"],
            risk="R1",
            designReview={"reviewer": None, "outcome": "not-required", "evidence": "R1."},
        ))

        self.assertTrue(any("risk is below path minimum R3" in error for error in self.validate()))

    def test_r1_record_cannot_cover_golden_authority(self) -> None:
        self._write("testdata/golden/new.json", "{}\n")
        self._write_record(self._record(
            paths=["testdata/golden/new.json"],
            risk="R1",
            designReview={"reviewer": None, "outcome": "not-required", "evidence": "R1."},
        ))

        self.assertTrue(any("risk is below path minimum R3" in error for error in self.validate()))

    def test_r1_record_cannot_cover_crc_worker_authority(self) -> None:
        self._write("tools/crc-worker/worker.py", "def run():\n    return 0\n")
        self._write_record(self._record(
            paths=["tools/crc-worker/worker.py"],
            risk="R1",
            designReview={"reviewer": None, "outcome": "not-required", "evidence": "R1."},
        ))

        self.assertTrue(any("risk is below path minimum R3" in error for error in self.validate()))

    def test_r1_record_cannot_cover_release_workflow(self) -> None:
        self._write(".github/workflows/release.yml", "name: release\n")
        self._write_record(self._record(
            paths=[".github/workflows/release.yml"],
            risk="R1",
            designReview={"reviewer": None, "outcome": "not-required", "evidence": "R1."},
        ))

        self.assertTrue(any("risk is below path minimum R3" in error for error in self.validate()))

    def test_r1_record_cannot_cover_governance_script(self) -> None:
        self._write("scripts/validate_repository.py", "def validate():\n    return []\n")
        self._write_record(self._record(
            paths=["scripts/validate_repository.py"],
            risk="R1",
            designReview={"reviewer": None, "outcome": "not-required", "evidence": "R1."},
        ))

        self.assertTrue(any("risk is below path minimum R2" in error for error in self.validate()))

    def test_r2_record_cannot_cover_external_tool_manifest_allowlist_owner(self) -> None:
        self._write("scripts/external_tool_policy.py", "APPROVED = ()\n")
        self._write_record(self._record(paths=["scripts/external_tool_policy.py"]))

        self.assertTrue(any("risk is below path minimum R3" in error for error in self.validate()))

    def test_r2_record_cannot_cover_canonical_golden_owner(self) -> None:
        self._write("scripts/canonical_golden_validation.py", "def validate():\n    return []\n")
        self._write_record(self._record(paths=["scripts/canonical_golden_validation.py"]))

        self.assertTrue(any("risk is below path minimum R3" in error for error in self.validate()))

    def test_r2_record_cannot_cover_live_registry_publisher(self) -> None:
        self._write(
            "scripts/edit_update_source_registry.py",
            "def publish():\n    return 0\n",
        )
        self._write_record(
            self._record(paths=["scripts/edit_update_source_registry.py"])
        )

        self.assertTrue(
            any("risk is below path minimum R3" in error for error in self.validate())
        )

    def test_r2_record_cannot_cover_same_family_golden_helpers(self) -> None:
        paths = [
            "scripts/ab_merge_fixture_validation.py",
            "scripts/create_candidate_ic_intake.py",
            "scripts/create_ctrlram_universal_sentinel.py",
            "scripts/diagnostic_golden_validation.py",
            "scripts/intake_ic_reference.py",
        ]
        for relative in paths:
            self._write(relative, "def validate():\n    return []\n")
        self._write_record(self._record(paths=paths))

        self.assertTrue(any("risk is below path minimum R3" in error for error in self.validate()))

    def test_r1_source_record_is_valid(self) -> None:
        self._change()
        self._write_record(self._record(
            risk="R1",
            designReview={"reviewer": None, "outcome": "not-required", "evidence": "R1."},
        ))

        self.assertEqual([], self.validate())

    def test_r2_governance_record_is_valid(self) -> None:
        self._write("AGENTS.md", "# Changed instructions\n")
        self._write_record(self._record(paths=["AGENTS.md"]))

        self.assertEqual([], self.validate())

    def test_r3_profile_record_is_valid(self) -> None:
        self._write("profiles/new.json", "{}\n")
        self._write_record(self._record(paths=["profiles/new.json"], risk="R3"))

        self.assertEqual([], self.validate())

    def test_r3_canonical_golden_script_record_is_valid(self) -> None:
        self._write("scripts/canonical_golden_validation.py", "def validate():\n    return []\n")
        self._write_record(self._record(
            paths=["scripts/canonical_golden_validation.py"],
            risk="R3",
        ))

        self.assertEqual([], self.validate())

    def test_mixed_mutable_paths_require_the_highest_minimum_risk(self) -> None:
        self._change()
        self._write("profiles/new.json", "{}\n")
        self._write_record(self._record(paths=[
            "src/Product/Owner.cs",
            "profiles/new.json",
        ]))

        self.assertTrue(any("risk is below path minimum R3" in error for error in self.validate()))

    def test_reject_duplicate_disposition_must_be_blocked(self) -> None:
        self._change()
        self._write_record(self._record(disposition="reject-duplicate"))

        self.assertTrue(any("reject-duplicate capability-reuse record must be blocked" in error for error in self.validate()))

    def test_v2_rejects_migration_dispositions_without_lifecycle_evidence(self) -> None:
        self._change()
        self._write_record(self._record(disposition="delete-then-replace"))

        self.assertTrue(any("disposition must be one of" in error for error in self.validate()))

    def test_design_complete_path_cannot_preauthorize_future_change(self) -> None:
        self._change()
        self._write_record(self._record(paths=[
            "src/Product/Owner.cs",
            "src/Product/Other.cs",
        ]))

        self.assertTrue(any("not in the current governed diff" in error for error in self.validate()))

    def test_unrelated_commit_cannot_be_used_as_integration_base(self) -> None:
        self._change()
        tree = self._git("rev-parse", "HEAD^{tree}").stdout.strip()
        unrelated = self._git("commit-tree", tree, "-m", "unrelated").stdout.strip()
        self._write_record(self._record(integrationBase=unrelated))

        self.assertTrue(any("latest evidence checkpoint" in error for error in self.validate()))

    def test_governed_to_ungoverned_rename_still_audits_old_path(self) -> None:
        self._git("mv", "src/Product/Owner.cs", "scratch/Renamed.cs")
        self._write_record(self._record(paths=["src/Product/Owner.cs"]))

        self.assertEqual([], self.validate())

    def test_ungoverned_to_governed_rename_audits_new_path(self) -> None:
        self._git("mv", "scratch/Source.cs", "src/Product/Promoted.cs")
        self._write_record(self._record(paths=["src/Product/Promoted.cs"]))

        self.assertEqual([], self.validate())

    def test_deleted_governed_path_remains_in_current_diff(self) -> None:
        self._git("rm", "src/Product/Owner.cs")
        self._write_record(self._record(paths=["src/Product/Owner.cs"]))

        self.assertEqual([], self.validate())

    def test_staged_record_binds_tracked_and_untracked_authority_changes(self) -> None:
        self._change()
        paths = [
            "src/Product/Owner.cs",
            "profiles/new-profile.json",
            "tools/crc-worker/worker.py",
            "scripts/create_update_catalog.py",
        ]
        self._write("profiles/new-profile.json", "{}\n")
        self._write("tools/crc-worker/worker.py", "def run():\n    return 0\n")
        self._write("scripts/create_update_catalog.py", "def main():\n    return 0\n")
        self._write_record(self._record(paths=paths, risk="R3"))

        self.assertEqual([], self.validate())

    def test_unchanged_committed_admission_can_transition_to_final(self) -> None:
        self._commit_candidate_with_active_record()
        self._write_record(self._final_record())

        self.assertEqual([], self.validate())

    def test_committed_admission_rejects_mutable_paths_change(self) -> None:
        self._commit_candidate_with_active_record()
        self._write_record(self._record(paths=["src/Product/Other.cs"]))

        self.assertTrue(any("immutable admitted fields" in error for error in self.validate()))

    def test_committed_admission_rejects_capability_change(self) -> None:
        self._commit_candidate_with_active_record()
        self._write_record(self._record(capability="Changed capability"))

        self.assertTrue(any("immutable admitted fields" in error for error in self.validate()))

    def test_committed_admission_rejects_risk_change(self) -> None:
        self._commit_candidate_with_active_record()
        self._write_record(self._record(risk="R3"))

        self.assertTrue(any("immutable admitted fields" in error for error in self.validate()))

    def test_committed_admission_rejects_integration_base_change(self) -> None:
        self._commit_candidate_with_active_record()
        self._write_record(self._record(integrationBase=self._git("rev-parse", "HEAD").stdout.strip()))

        self.assertTrue(any("immutable admitted fields" in error for error in self.validate()))

    def test_committed_admission_rejects_semantic_owner_change(self) -> None:
        self._commit_candidate_with_active_record()
        self._write_record(self._record(semanticOwner="Product.OtherOwner"))

        self.assertTrue(any("immutable admitted fields" in error for error in self.validate()))

    def test_committed_admission_rejects_design_review_change(self) -> None:
        self._commit_candidate_with_active_record()
        self._write_record(self._record(designReview={
            "reviewer": "different-architect",
            "outcome": "approved",
            "evidence": "Changed after admission.",
        }))

        self.assertTrue(any("immutable admitted fields" in error for error in self.validate()))

    def test_committed_admission_rejects_disposition_change(self) -> None:
        self._commit_candidate_with_active_record()
        self._write_record(self._record(disposition="reuse"))

        self.assertTrue(any("immutable admitted fields" in error for error in self.validate()))

    def test_committed_active_admission_change_is_rejected_after_restore(self) -> None:
        first_active_head = self._commit_candidate_with_active_record()
        relative = "docs/governance/change-records/TEST-01.json"
        original = self._git("show", f"{first_active_head}:{relative}").stdout
        changed = self._record(capability="Committed changed capability")
        self._write_record(changed)
        self._git("commit", "-q", "-m", "change active admission")
        self._write(relative, original)
        self._git("add", "--", relative)

        self.assertTrue(any("immutable admitted fields" in error for error in self.validate()))

    def test_final_record_rejects_a_digest_that_does_not_match_reviewed_bytes(self) -> None:
        self._commit_candidate_with_active_record()
        self._write_record(self._final_record(pathStateDigest="0" * 64))

        with mock.patch.object(
            repository_validator,
            "_historical_final_records",
            wraps=repository_validator._historical_final_records,
        ) as history_audit:
            errors = self.validate()

        history_audit.assert_called_once()
        self.assertTrue(any("pathStateDigest differs" in error for error in errors))

    def test_final_record_requires_a_committed_design_active_predecessor(self) -> None:
        self._commit_implementation()
        self._write_record(self._final_record())

        self.assertTrue(any("design-active predecessor" in error for error in self.validate()))

    def test_final_record_cannot_change_admitted_design_fields(self) -> None:
        self._commit_candidate_with_active_record()
        self._write_record(self._final_record(capability="Different capability"))

        self.assertTrue(any("changed admitted design fields" in error for error in self.validate()))

    def test_final_record_rejects_different_implementation_and_reviewed_heads(self) -> None:
        implementation_head = self._commit_candidate_with_active_record()
        self._write("scratch/Source.cs", "internal sealed class Source { public int Value => 2; }\n")
        self._git("add", "--", "scratch/Source.cs")
        self._git("commit", "-q", "-m", "later review candidate")
        self._write_record(self._final_record(implementationHead=implementation_head))

        self.assertTrue(any("implementationHead must equal reviewedHead" in error for error in self.validate()))

    def test_committed_design_active_record_cannot_be_reused(self) -> None:
        self._change()
        self._write_record(self._record())
        self._git("add", "--", "src/Product/Owner.cs")
        self._git("commit", "-q", "-m", "candidate left active")

        self.assertTrue(any("cannot remain committed or be reused" in error for error in self.validate()))

    def test_archived_final_record_is_excluded_from_a_later_batch(self) -> None:
        self._commit_candidate_with_active_record()
        self._write_record(self._final_record())
        self._git("commit", "-q", "-m", "finalize first record")
        self.integration_base = self._git("rev-parse", "HEAD").stdout.strip()
        self._change("src/Product/Other.cs")
        self._write_record(self._record("TEST-02", ["src/Product/Other.cs"]))

        self.assertEqual([], self.validate())

    def test_committed_final_record_cannot_be_tampered(self) -> None:
        self._commit_candidate_with_active_record()
        record = self._final_record()
        self._write_record(record)
        self._git("commit", "-q", "-m", "finalize record")
        record["capability"] = "Tampered capability"
        self._write_record(record)

        self.assertTrue(any("immutable after commit" in error for error in self.validate()))

    def test_final_record_remains_valid_after_redundant_containment_merge(self) -> None:
        evidence_commit = self._finalize_first_batch()
        reviewed_head = self._git("rev-parse", f"{evidence_commit}^").stdout.strip()
        self._merge_descendant_as_redundant_second_parent(evidence_commit, reviewed_head)

        self.assertEqual([], self.validate())

    def test_distinct_merge_tree_does_not_bypass_final_record_immutability(self) -> None:
        evidence_commit = self._finalize_first_batch()
        reviewed_head = self._git("rev-parse", f"{evidence_commit}^").stdout.strip()
        self._merge_descendant_as_redundant_second_parent(
            evidence_commit,
            reviewed_head,
            mutate_merge_tree=True,
        )

        self.assertTrue(any("changed in commit history" in error for error in self.validate()))

    def test_noncontained_parent_does_not_bypass_final_record_immutability(self) -> None:
        evidence_commit = self._finalize_first_batch()
        evidence_branch = f"reviewed-{evidence_commit[:12]}"
        self._git("branch", evidence_branch, evidence_commit)
        self._git("checkout", "-q", "--detach", self.integration_base)
        self._write("scratch/noncontained.txt", "independent side branch\n")
        self._git("add", "--", "scratch/noncontained.txt")
        self._git("commit", "-q", "-m", "independent side branch")
        self._git("merge", "--no-ff", "--no-commit", "-q", evidence_branch)
        self._git("read-tree", "--reset", "-u", evidence_branch)
        self._git("commit", "-q", "-m", "select reviewed tree from noncontained branch")

        self.assertTrue(any("changed in commit history" in error for error in self.validate()))

    def test_underlying_record_mutation_is_detected_through_redundant_merge(self) -> None:
        evidence_commit = self._finalize_first_batch()
        relative = "docs/governance/change-records/TEST-01.json"
        original = self._git("show", f"{evidence_commit}:{relative}").stdout
        record = json.loads(original)
        record["capability"] = "Temporary branch tamper"
        self._write_record(record)
        self._git("commit", "-q", "-m", "tamper on reviewed ancestry")
        self._write(relative, original)
        self._git("add", "--", relative)
        self._git("commit", "-q", "-m", "restore reviewed ancestry")
        descendant = self._git("rev-parse", "HEAD").stdout.strip()
        self._merge_descendant_as_redundant_second_parent(descendant, evidence_commit)

        self.assertTrue(any("changed in commit history" in error for error in self.validate()))

    def test_equal_parent_trees_do_not_hide_underlying_record_mutation(self) -> None:
        evidence_commit = self._finalize_first_batch()
        relative = "docs/governance/change-records/TEST-01.json"
        original = self._git("show", f"{evidence_commit}:{relative}").stdout
        self._git("checkout", "-q", "--detach", evidence_commit)
        record = json.loads(original)
        record["capability"] = "Temporary equal-tree branch tamper"
        self._write_record(record)
        self._git("commit", "-q", "-m", "tamper first equal-tree ancestry")
        self._write(relative, original)
        self._git("add", "--", relative)
        self._git("commit", "-q", "-m", "restore first equal-tree ancestry")
        first_parent = self._git("rev-parse", "HEAD").stdout.strip()
        self._git("checkout", "-q", "--detach", evidence_commit)
        self._git("commit", "--allow-empty", "-q", "-m", "second equal-tree ancestry")
        second_parent = self._git("rev-parse", "HEAD").stdout.strip()
        self._git("branch", f"equal-{second_parent[:12]}", second_parent)
        self._git("checkout", "-q", "--detach", first_parent)
        self._git("merge", "--no-ff", "-q", "-m", "equal parent tree merge", second_parent)

        self.assertTrue(any("changed in commit history" in error for error in self.validate()))

    def test_octopus_merge_does_not_bypass_final_record_immutability(self) -> None:
        evidence_commit = self._finalize_first_batch()
        reviewed_branch = f"reviewed-{evidence_commit[:12]}"
        self._git("branch", reviewed_branch, evidence_commit)
        side_heads: list[str] = []
        for number in (1, 2):
            self._git("checkout", "-q", "--detach", self.integration_base)
            self._write(f"scratch/side-{number}.txt", f"side {number}\n")
            self._git("add", "--", f"scratch/side-{number}.txt")
            self._git("commit", "-q", "-m", f"octopus side {number}")
            side_heads.append(self._git("rev-parse", "HEAD").stdout.strip())
        self._git("checkout", "-q", "--detach", self.integration_base)
        self._git(
            "merge",
            "--no-ff",
            "-q",
            "-m",
            "octopus merge",
            reviewed_branch,
            *side_heads,
        )

        self.assertTrue(any("changed in commit history" in error for error in self.validate()))

    def test_merge_topology_inspection_error_fails_closed(self) -> None:
        evidence_commit = self._finalize_first_batch()
        reviewed_head = self._git("rev-parse", f"{evidence_commit}^").stdout.strip()
        merge_head = self._merge_descendant_as_redundant_second_parent(
            evidence_commit,
            reviewed_head,
        )
        real_run = subprocess.run

        def fail_parent_lookup(arguments: list[str], **kwargs: Any) -> subprocess.CompletedProcess[Any]:
            if arguments == ["git", "rev-list", "--parents", "-n", "1", merge_head]:
                return subprocess.CompletedProcess(
                    arguments,
                    128,
                    stdout=b"",
                    stderr=b"synthetic topology read failure",
                )
            return real_run(arguments, **kwargs)

        with mock.patch.object(repository_validator.subprocess, "run", side_effect=fail_parent_lookup):
            errors = self.validate()

        self.assertTrue(any("history could not be audited" in error for error in errors))

    def test_committed_final_record_cannot_be_deleted(self) -> None:
        self._commit_candidate_with_active_record()
        self._write_record(self._final_record())
        self._git("commit", "-q", "-m", "finalize record")
        self._git("rm", "docs/governance/change-records/TEST-01.json")

        self.assertTrue(any("record was deleted" in error for error in self.validate()))

    def test_missing_trusted_initial_checkpoint_fails_closed(self) -> None:
        self._change()
        self._write_record(self._record())
        errors: list[str] = []

        validate_capability_reuse_governance(self.root, errors)

        self.assertTrue(any("trusted initial evidence checkpoint is pending" in error for error in errors))

    def test_owner_approved_activation_retires_exact_legacy_inventory(self) -> None:
        self._commit_candidate_with_active_record()

        self._activate_trusted_checkpoint()

        self.assertEqual([], self._validate_derived_checkpoint())

    def test_activation_rejects_incomplete_legacy_inventory(self) -> None:
        self._commit_candidate_with_active_record()

        self._activate_trusted_checkpoint(
            mutate_manifest=lambda value: value["legacyRecords"].clear(),
        )

        errors = self._validate_derived_checkpoint()
        self.assertTrue(any("non-empty legacyRecords" in error for error in errors))

    def test_activation_rejects_legacy_content_hash_drift(self) -> None:
        self._commit_candidate_with_active_record()

        self._activate_trusted_checkpoint(
            mutate_manifest=lambda value: value["legacyRecords"][0].update(
                {"contentSha256": "0" * 64}
            ),
        )

        self.assertTrue(
            any("legacy content SHA differs" in error for error in self._validate_derived_checkpoint())
        )

    def test_activation_binds_exact_reviewed_head_and_tree(self) -> None:
        self._commit_candidate_with_active_record()

        self._activate_trusted_checkpoint(
            mutate_manifest=lambda value: value.update({"reviewedTree": "0" * 40}),
        )

        self.assertTrue(
            any("reviewedTree differs" in error for error in self._validate_derived_checkpoint())
        )

    def test_activation_must_be_direct_child_of_reviewed_head(self) -> None:
        self._commit_candidate_with_active_record()

        self._activate_trusted_checkpoint(
            mutate_manifest=lambda value: value.update(
                {
                    "reviewedHead": self.integration_base,
                    "reviewedTree": self._git(
                        "rev-parse",
                        f"{self.integration_base}^{{tree}}",
                    ).stdout.strip(),
                }
            ),
        )

        self.assertTrue(
            any("must directly follow reviewedHead" in error for error in self._validate_derived_checkpoint())
        )

    def test_activation_rejects_any_extra_changed_path(self) -> None:
        self._commit_candidate_with_active_record()

        self._activate_trusted_checkpoint(extra_activation_path=True)

        self.assertTrue(
            any("outside its exact manifest" in error for error in self._validate_derived_checkpoint())
        )

    def test_retired_task_id_and_record_path_cannot_be_reused(self) -> None:
        self._commit_candidate_with_active_record()
        activation, _ = self._activate_trusted_checkpoint()
        self.integration_base = activation
        self._change("src/Product/Other.cs")
        self._write_record(self._record("TEST-01", ["src/Product/Other.cs"]))

        errors = self._validate_derived_checkpoint()

        self.assertTrue(any("retired legacy record was restored" in error for error in errors))
        self.assertTrue(any("retired capability-reuse taskId cannot be reused" in error for error in errors))

    def test_post_activation_batch_uses_ordinary_strict_lifecycle(self) -> None:
        self._commit_candidate_with_active_record()
        activation, _ = self._activate_trusted_checkpoint()
        self.integration_base = activation
        self._change("src/Product/Other.cs")
        self._write_record(self._record("TEST-02", ["src/Product/Other.cs"]))

        self.assertEqual([], self._validate_derived_checkpoint())

    def test_activation_manifest_is_immutable_after_commit(self) -> None:
        self._commit_candidate_with_active_record()
        self._activate_trusted_checkpoint()
        relative = "docs/governance/trusted-initial-capability-checkpoint.v1.json"
        value = json.loads((self.root / relative).read_text(encoding="utf-8"))
        value["ownerDecisionRef"] = "worktree tamper"
        self._write(relative, json.dumps(value, indent=2) + "\n")
        self._git("add", "--", relative)

        self.assertTrue(
            any("activation manifest is immutable" in error for error in self._validate_derived_checkpoint())
        )

    def test_initial_r3_authority_requires_one_exact_head_attestation_batch(self) -> None:
        self._change()
        self._write_record(self._record("FORMAL-SUPPORT-01", risk="R3"))
        self._git("add", "--", "src/Product/Owner.cs")
        self._git("commit", "-q", "-m", "legacy R3 candidate")
        _, manifest = self._activate_trusted_checkpoint()

        self.assertTrue(
            any("external R3 authority remains pending" in error for error in self._validate_derived_checkpoint())
        )

        self._write_external_authority_batch(manifest)

        self.assertEqual([], self._validate_derived_checkpoint())

    def test_external_authority_attestation_remains_bound_after_later_unrelated_commit(self) -> None:
        self._change()
        self._write_record(self._record("FORMAL-SUPPORT-01", risk="R3"))
        self._git("add", "--", "src/Product/Owner.cs")
        self._git("commit", "-q", "-m", "legacy R3 candidate")
        _, manifest = self._activate_trusted_checkpoint()
        self._write_external_authority_batch(manifest)
        self._write("scratch/later.txt", "later\n")
        self._git("add", "--", "scratch/later.txt")
        self._git("commit", "-q", "-m", "later commit")

        self.assertEqual([], self._validate_derived_checkpoint())

    def test_external_authority_attestation_remains_bound_after_redundant_containment_merge(
        self,
    ) -> None:
        self._change()
        self._write_record(self._record("FORMAL-SUPPORT-01", risk="R3"))
        self._git("add", "--", "src/Product/Owner.cs")
        self._git("commit", "-q", "-m", "legacy R3 candidate")
        _, manifest = self._activate_trusted_checkpoint()
        evidence_commit = self._write_external_authority_batch(manifest)
        reviewed_head = self._git("rev-parse", f"{evidence_commit}^").stdout.strip()
        self._merge_descendant_as_redundant_second_parent(evidence_commit, reviewed_head)

        self.assertEqual([], self._validate_derived_checkpoint())

    def test_external_authority_attestation_change_after_commit_remains_rejected(self) -> None:
        self._change()
        self._write_record(self._record("FORMAL-SUPPORT-01", risk="R3"))
        self._git("add", "--", "src/Product/Owner.cs")
        self._git("commit", "-q", "-m", "legacy R3 candidate")
        _, manifest = self._activate_trusted_checkpoint()
        self._write_external_authority_batch(manifest)
        relative = "docs/governance/external-authority-attestations/FORMAL-SUPPORT-01.json"
        value = json.loads((self.root / relative).read_text(encoding="utf-8"))
        value["evidence"] = "Tampered after approval."
        self._write(relative, json.dumps(value, indent=2) + "\n")
        self._git("add", "--", relative)
        self._git("commit", "-q", "-m", "tamper authority evidence")

        self.assertTrue(
            any("attestation is immutable after commit" in error for error in self._validate_derived_checkpoint())
        )

    def test_tracked_non_json_child_cannot_enter_attestation_directory_later(self) -> None:
        self._change()
        self._write_record(self._record("FORMAL-SUPPORT-01", risk="R3"))
        self._git("add", "--", "src/Product/Owner.cs")
        self._git("commit", "-q", "-m", "legacy R3 candidate")
        _, manifest = self._activate_trusted_checkpoint()
        self._write_external_authority_batch(manifest)
        relative = "docs/governance/external-authority-attestations/UNDECLARED.txt"
        self._write(relative, "not typed evidence\n")
        self._git("add", "--", relative)
        self._git("commit", "-q", "-m", "add undeclared evidence")

        self.assertTrue(
            any("must be a direct JSON child" in error for error in self._validate_derived_checkpoint())
        )

    def test_untracked_non_json_child_cannot_enter_attestation_directory(self) -> None:
        self._change()
        self._write_record(self._record("FORMAL-SUPPORT-01", risk="R3"))
        self._git("add", "--", "src/Product/Owner.cs")
        self._git("commit", "-q", "-m", "legacy R3 candidate")
        _, manifest = self._activate_trusted_checkpoint()
        self._write_external_authority_batch(manifest)
        self._write(
            "docs/governance/external-authority-attestations/UNDECLARED.txt",
            "untracked evidence\n",
        )

        self.assertTrue(
            any("must be a direct JSON child" in error for error in self._validate_derived_checkpoint())
        )

    def test_noncanonical_or_nested_json_attestation_paths_are_rejected(self) -> None:
        self._change()
        self._write_record(self._record("FORMAL-SUPPORT-01", risk="R3"))
        self._git("add", "--", "src/Product/Owner.cs")
        self._git("commit", "-q", "-m", "legacy R3 candidate")
        _, manifest = self._activate_trusted_checkpoint()
        self._write_external_authority_batch(manifest)
        for relative in (
            "docs/governance/external-authority-attestations/UNDECLARED.JSON",
            "docs/governance/external-authority-attestations/nested/UNDECLARED.json",
        ):
            self._write(relative, "{}\n")
            self._git("add", "--", relative)
        self._git("commit", "-q", "-m", "add noncanonical evidence")

        errors = self._validate_derived_checkpoint()
        self.assertEqual(
            2,
            sum("must be a direct JSON child" in error for error in errors),
        )

    def test_record_must_match_its_exact_index_blob(self) -> None:
        self._change()
        record = self._record()
        self._write_record(record)
        record["capability"] = "Worktree-only edit"
        self._write_record(record, stage=False)

        with mock.patch.object(repository_validator, "_historical_final_records") as history_audit:
            errors = self.validate()

        history_audit.assert_not_called()
        self.assertTrue(any("index/worktree content differs" in error for error in errors))

    def test_unstaged_final_transition_fails_before_history_audit(self) -> None:
        self._commit_candidate_with_active_record()
        self._write_record(self._final_record(), stage=False)

        with mock.patch.object(repository_validator, "_historical_final_records") as history_audit:
            errors = self.validate()

        history_audit.assert_not_called()
        self.assertTrue(any("index/worktree content differs" in error for error in errors))

    def test_non_governed_active_path_fails_before_history_audit(self) -> None:
        self._change()
        self._write_record(self._record(paths=["NvtFwCombiner.slnx"]))

        with mock.patch.object(repository_validator, "_historical_final_records") as history_audit:
            errors = self.validate()

        history_audit.assert_not_called()
        self.assertTrue(any("mutable path is not governed" in error for error in errors))

    def test_intent_to_add_record_cannot_open_gate(self) -> None:
        self._change()
        record = self._record()
        relative = "docs/governance/change-records/TEST-01.json"
        self._write_record(record, relative, stage=False)
        self._git("add", "--intent-to-add", "--", relative)

        self.assertTrue(any("intent-to-add" in error for error in self.validate()))

    def test_clean_committed_governed_change_after_checkpoint_requires_new_record(self) -> None:
        checkpoint = self._finalize_first_batch()
        self._change("src/Product/Other.cs")
        self._git("add", "--", "src/Product/Other.cs")
        self._git("commit", "-q", "-m", "unrecorded governed change")

        errors = self.validate()

        self.assertTrue(any("lacks a design-active/current-final" in error for error in errors))
        self.assertNotEqual(checkpoint, self._git("rev-parse", "HEAD").stdout.strip())

    def test_new_batch_must_bind_latest_final_evidence_checkpoint(self) -> None:
        self._finalize_first_batch()
        self._change("src/Product/Other.cs")
        self._write_record(self._record("TEST-02", ["src/Product/Other.cs"]))

        self.assertTrue(any("latest evidence checkpoint" in error for error in self.validate()))

    def test_committed_active_record_cannot_be_deleted_to_abandon_its_diff(self) -> None:
        checkpoint = self._finalize_first_batch()
        self.integration_base = checkpoint
        self._change("src/Product/Other.cs")
        self._write_record(self._record("TEST-02", ["src/Product/Other.cs"]))
        self._git("add", "--", "src/Product/Other.cs")
        self._git("commit", "-q", "-m", "second active candidate")
        self._git("rm", "docs/governance/change-records/TEST-02.json")

        self.assertTrue(any("cannot be deleted or blocked" in error for error in self.validate()))

    def test_committed_active_record_cannot_be_changed_to_blocked(self) -> None:
        checkpoint = self._finalize_first_batch()
        self.integration_base = checkpoint
        self._change("src/Product/Other.cs")
        active = self._record("TEST-02", ["src/Product/Other.cs"])
        self._write_record(active)
        self._git("add", "--", "src/Product/Other.cs")
        self._git("commit", "-q", "-m", "second active candidate")
        blocked = self._record(
            "TEST-02",
            [],
            state="blocked",
            disposition="reject-duplicate",
            designReview={
                "reviewer": "architect",
                "outcome": "blocked",
                "evidence": "Attempted to abandon an implemented diff.",
            },
        )
        self._write_record(blocked)

        self.assertTrue(any("cannot be deleted or blocked" in error for error in self.validate()))

    def test_first_final_evidence_commit_must_directly_follow_reviewed_head(self) -> None:
        reviewed_head = self._commit_candidate_with_active_record()
        self._write("scratch/Source.cs", "internal sealed class Source { public int Value => 2; }\n")
        self._git("add", "--", "scratch/Source.cs")
        self._git("commit", "-q", "-m", "intervening commit")
        record = self._final_record(
            implementationHead=reviewed_head,
            reviewedHead=reviewed_head,
        )
        digest, error = _capability_path_state_digest(
            self.root,
            reviewed_head,
            ["src/Product/Owner.cs"],
        )
        self.assertIsNone(error)
        record["pathStateDigest"] = digest
        self._write_record(record)
        self._git("commit", "-q", "-m", "late final evidence")

        self.assertTrue(any("direct child" in error for error in self.validate()))

    def test_final_evidence_commit_cannot_change_governed_paths(self) -> None:
        self._commit_candidate_with_active_record()
        self._write_record(self._final_record())
        self._change("src/Product/Other.cs")
        self._git("add", "--", "src/Product/Other.cs")
        self._git("commit", "-q", "-m", "final evidence with product mutation")

        self.assertTrue(any("changes governed paths" in error for error in self.validate()))

    def test_uncommitted_final_cannot_cover_governed_worktree_change_after_review(self) -> None:
        self._commit_candidate_with_active_record()
        self._write_record(self._final_record())
        self._change("src/Product/Other.cs")

        self.assertTrue(any("after reviewedHead" in error for error in self.validate()))

    def test_r3_final_record_cannot_replace_existing_external_owner_gate(self) -> None:
        self._change()
        self._write_record(self._record(risk="R3"))
        self._git("add", "--", "src/Product/Owner.cs")
        self._git("commit", "-q", "-m", "R3 active candidate")
        self._write_record(self._final_record(risk="R3"))

        self.assertTrue(any("external owner authority" in error for error in self.validate()))

    def test_r3_final_batch_accepts_only_exact_final_evidence_head_attestation(self) -> None:
        self._change()
        self._write_record(self._record(risk="R3"))
        self._git("add", "--", "src/Product/Owner.cs")
        self._git("commit", "-q", "-m", "R3 active candidate")
        self._write_record(self._final_record(risk="R3"))
        self._git("commit", "-q", "-m", "R3 final evidence")
        self._write_external_authority_batch(
            {
                "openR3Authorities": [
                    {
                        "taskId": "TEST-01",
                        "authorityType": "release-owner",
                        "status": "pending",
                    }
                ]
            }
        )

        self.assertEqual([], self.validate())

    def test_final_batch_must_exactly_cover_checkpoint_to_reviewed_head(self) -> None:
        self._change()
        self._change("src/Product/Other.cs")
        self._write_record(self._record())
        self._git("add", "--", "src/Product/Owner.cs", "src/Product/Other.cs")
        self._git("commit", "-q", "-m", "underdeclared candidate")
        self._write_record(self._final_record())
        self._git("commit", "-q", "-m", "underdeclared final evidence")

        self.assertTrue(any("does not exactly cover" in error for error in self.validate()))

    def test_later_record_change_is_rejected_even_when_exact_bytes_are_restored(self) -> None:
        evidence_commit = self._finalize_first_batch()
        relative = "docs/governance/change-records/TEST-01.json"
        original = self._git("show", f"{evidence_commit}:{relative}").stdout
        record = json.loads(original)
        record["capability"] = "Temporary tamper"
        self._write_record(record)
        self._git("commit", "-q", "-m", "tamper final evidence")
        self._write(relative, original)
        self._git("add", "--", relative)
        self._git("commit", "-q", "-m", "restore final evidence bytes")

        self.assertTrue(any("changed in commit history" in error for error in self.validate()))

    def test_path_state_digest_changes_with_blob_bytes(self) -> None:
        first_head = self._commit_implementation()
        first, first_error = _capability_path_state_digest(
            self.root, first_head, ["src/Product/Owner.cs"]
        )
        self._write("src/Product/Owner.cs", "internal sealed class Owner { public int Value => 2; }\n")
        self._git("add", "--", "src/Product/Owner.cs")
        self._git("commit", "-q", "-m", "change bytes")
        second, second_error = _capability_path_state_digest(
            self.root, "HEAD", ["src/Product/Owner.cs"]
        )

        self.assertIsNone(first_error)
        self.assertIsNone(second_error)
        self.assertNotEqual(first, second)

    def test_path_state_digest_changes_with_git_mode(self) -> None:
        first_head = self._commit_implementation()
        first, first_error = _capability_path_state_digest(
            self.root, first_head, ["src/Product/Owner.cs"]
        )
        self._git("update-index", "--chmod=+x", "src/Product/Owner.cs")
        self._git("commit", "-q", "-m", "change mode")
        second, second_error = _capability_path_state_digest(
            self.root, "HEAD", ["src/Product/Owner.cs"]
        )

        self.assertIsNone(first_error)
        self.assertIsNone(second_error)
        self.assertNotEqual(first, second)

    def test_path_state_digest_records_deletion_and_both_rename_sides(self) -> None:
        self._commit_implementation()
        before_delete, before_error = _capability_path_state_digest(
            self.root, "HEAD", ["src/Product/Owner.cs"]
        )
        self._git("rm", "src/Product/Owner.cs")
        self._git("commit", "-q", "-m", "delete path")
        after_delete, delete_error = _capability_path_state_digest(
            self.root, "HEAD", ["src/Product/Owner.cs"]
        )
        self._git("mv", "src/Product/Other.cs", "src/Product/Renamed.cs")
        self._git("commit", "-q", "-m", "rename path")
        rename_digest, rename_error = _capability_path_state_digest(
            self.root,
            "HEAD",
            ["src/Product/Other.cs", "src/Product/Renamed.cs"],
        )

        self.assertIsNone(before_error)
        self.assertIsNone(delete_error)
        self.assertIsNone(rename_error)
        self.assertNotEqual(before_delete, after_delete)
        self.assertRegex(str(rename_digest), r"^[0-9a-f]{64}$")

    def test_shallow_history_fails_closed(self) -> None:
        self._commit_implementation()
        clone = self.root / "shallow-clone"
        subprocess.run(
            ["git", "clone", "-q", "--depth", "1", self.root.as_uri(), str(clone)],
            check=True,
            capture_output=True,
        )
        errors: list[str] = []

        validate_capability_reuse_governance(clone, errors)

        self.assertTrue(any("shallow repositories fail closed" in error for error in errors))

    def test_ci_structure_checkout_fetches_complete_history(self) -> None:
        workflow = (ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8")
        structure_job = workflow.split("  python-worker:", 1)[0]

        self.assertIn("fetch-depth: 0", structure_job)

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

                self.assertEqual(5, len(checkout_steps))
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
                self.assertIn(
                    'test "$(git rev-parse HEAD)" = "$NFC_PR_HEAD_SHA"',
                    freshness["run"],
                )
                self.assertIn(
                    'git merge-base --is-ancestor "$NFC_PR_BASE_SHA" HEAD',
                    freshness["run"],
                )


if __name__ == "__main__":
    unittest.main()

"""Behavioral tests for stable release identity and immutable candidate policy."""

from __future__ import annotations

import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "release_promotion_policy.py"
SPEC = importlib.util.spec_from_file_location("release_promotion_policy", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)
SHA = "1" * 40
TREE = "2" * 40
TAG_OBJECT_SHA = "3" * 40
TAG_MESSAGE = "NVT FW Combiner v0.9.14\ncandidate-run: 99"


def valid_snapshot() -> dict[str, object]:
    return {
        "number": 214,
        "state": "MERGED",
        "mergedAt": "2026-07-22T01:00:00Z",
        "baseRefName": "main",
        "mergeCommitSha": SHA,
        "headSha": "4" * 40,
        "headTree": TREE,
        "reviewDecision": "APPROVED",
        "approvals": [
            {
                "reviewer": "independent-reviewer",
                "commitSha": "4" * 40,
                "submittedAt": "2026-07-22T00:55:00Z",
            }
        ],
        "ownerSelfApprovalException": False,
        "requiredChecks": [{"name": "dotnet / build-test", "bucket": "pass"}],
    }


class ReleasePromotionPolicyTests(unittest.TestCase):
    @staticmethod
    def candidate_arguments() -> dict[str, object]:
        return {
            "requested_sha": SHA,
            "workflow_sha": SHA,
            "workflow_ref": "refs/heads/main",
            "source_sha": SHA,
            "source_branch": "main",
            "source_version": "0.10.0",
            "main_sha": SHA,
            "source_tree": TREE,
            "repository_owner": "release-owner",
            "workflow_actor": "release-owner",
            "owner_self_approval_exception": False,
        }

    def test_accepts_only_exact_reviewed_main_identity(self) -> None:
        MODULE.validate_candidate_context(
            valid_snapshot(),
            **self.candidate_arguments(),
        )

    def test_accepts_exact_reviewed_0917_maintenance_identity(self) -> None:
        maintenance_sha = "5" * 40
        snapshot = {
            **valid_snapshot(),
            "baseRefName": "0.9.17",
            "mergeCommitSha": maintenance_sha,
        }

        MODULE.validate_candidate_context(
            snapshot,
            **{
                **self.candidate_arguments(),
                "requested_sha": maintenance_sha,
                "source_sha": maintenance_sha,
                "source_branch": "0.9.17",
                "source_version": "0.9.17",
            },
        )

    def test_accepts_exact_reviewed_0918_maintenance_identity(self) -> None:
        maintenance_sha = "6" * 40
        snapshot = {
            **valid_snapshot(),
            "baseRefName": "0.9.18",
            "mergeCommitSha": maintenance_sha,
        }

        MODULE.validate_candidate_context(
            snapshot,
            **{
                **self.candidate_arguments(),
                "requested_sha": maintenance_sha,
                "source_sha": maintenance_sha,
                "source_branch": "0.9.18",
                "source_version": "0.9.18",
            },
        )

    def test_accepts_exact_reviewed_0919_maintenance_identity(self) -> None:
        maintenance_sha = "7" * 40
        snapshot = {
            **valid_snapshot(),
            "baseRefName": "0.9.19",
            "mergeCommitSha": maintenance_sha,
        }

        MODULE.validate_candidate_context(
            snapshot,
            **{
                **self.candidate_arguments(),
                "requested_sha": maintenance_sha,
                "source_sha": maintenance_sha,
                "source_branch": "0.9.19",
                "source_version": "0.9.19",
            },
        )

    def test_rejects_unapproved_or_mismatched_maintenance_source(self) -> None:
        maintenance_sha = "5" * 40
        snapshot = {
            **valid_snapshot(),
            "baseRefName": "0.9.17",
            "mergeCommitSha": maintenance_sha,
        }
        common = {
            **self.candidate_arguments(),
            "requested_sha": maintenance_sha,
            "source_sha": maintenance_sha,
        }
        for source_branch, source_version in (
            ("0.9.16", "0.9.16"),
            ("0.9.17", "0.9.18"),
            ("0.9.18", "0.9.17"),
            ("0.9.19", "0.9.18"),
        ):
            with self.subTest(
                source_branch=source_branch, source_version=source_version
            ):
                with self.assertRaisesRegex(ValueError, "approved maintenance"):
                    MODULE.validate_candidate_context(
                        snapshot,
                        **{
                            **common,
                            "source_branch": source_branch,
                            "source_version": source_version,
                        },
                    )

    def test_rejects_release_source_sha_or_pr_base_drift(self) -> None:
        maintenance_sha = "5" * 40
        snapshot = {
            **valid_snapshot(),
            "baseRefName": "0.9.17",
            "mergeCommitSha": maintenance_sha,
        }
        arguments = {
            **self.candidate_arguments(),
            "requested_sha": maintenance_sha,
            "source_sha": maintenance_sha,
            "source_branch": "0.9.17",
            "source_version": "0.9.17",
        }

        with self.assertRaisesRegex(ValueError, "source SHAs must be identical"):
            MODULE.validate_candidate_context(
                snapshot,
                **{**arguments, "requested_sha": "6" * 40},
            )
        with self.assertRaisesRegex(ValueError, "selected release source branch"):
            MODULE.validate_candidate_context(
                {**snapshot, "baseRefName": "main"},
                **arguments,
            )
        with self.assertRaisesRegex(ValueError, "current protected main"):
            MODULE.validate_candidate_context(
                valid_snapshot(),
                **{
                    **self.candidate_arguments(),
                    "requested_sha": "6" * 40,
                    "source_sha": "6" * 40,
                },
            )

    def test_rejects_tree_review_and_required_check_drift(self) -> None:
        mutations = (
            ("headTree", "3" * 40, "tree differs"),
            ("reviewDecision", "CHANGES_REQUESTED", "not approved"),
            ("approvals", [], "no current-head approval"),
            (
                "approvals",
                [{"reviewer": "stale", "commitSha": "5" * 40}],
                "stale, malformed, or authored by Codex",
            ),
            ("requiredChecks", [{"name": "dotnet", "bucket": "fail"}], "not passing"),
        )
        for key, value, message in mutations:
            with self.subTest(key=key):
                snapshot = valid_snapshot()
                snapshot[key] = value
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_candidate_context(
                        snapshot,
                        **self.candidate_arguments(),
                    )

    def test_rejects_non_main_workflow_or_stale_sha(self) -> None:
        for workflow_ref, workflow_sha, message in (
            ("refs/heads/feature", SHA, "dispatched from main"),
            ("refs/heads/main", "3" * 40, "current protected main"),
        ):
            with self.subTest(workflow_ref=workflow_ref, workflow_sha=workflow_sha):
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_candidate_context(
                        valid_snapshot(),
                        **{
                            **self.candidate_arguments(),
                            "workflow_sha": workflow_sha,
                            "workflow_ref": workflow_ref,
                        },
                    )

    def test_owner_self_approval_exception_is_explicit_and_owner_bound(self) -> None:
        snapshot = valid_snapshot()
        snapshot["reviewDecision"] = ""
        snapshot["approvals"] = []
        snapshot["authorLogin"] = "release-owner"
        snapshot["repositoryOwner"] = "release-owner"
        snapshot["workflowActor"] = "release-owner"
        snapshot["ownerSelfApprovalException"] = True
        snapshot["codexReview"] = {
            "source": "pull-review",
            "reviewer": f"{MODULE.CODEX_REVIEWER}[bot]",
            "commitSha": "4" * 40,
            "state": "COMMENTED",
            "submittedAt": "2026-07-22T00:59:00Z",
        }
        arguments = {
            **self.candidate_arguments(),
            "owner_self_approval_exception": True,
        }

        MODULE.validate_candidate_context(snapshot, **arguments)

        snapshot["reviewDecision"] = "REVIEW_REQUIRED"
        MODULE.validate_candidate_context(snapshot, **arguments)
        snapshot["reviewDecision"] = ""

        for key, value, message in (
            ("owner_self_approval_exception", False, "no current-head approval"),
            ("workflow_actor", "other-user", "must be dispatched"),
        ):
            with self.subTest(key=key):
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_candidate_context(
                        snapshot, **{**arguments, key: value}
                    )

        for key, value, message in (
            ("authorLogin", "other-user", "author the PR"),
            ("workflowActor", "other-user", "identity differs"),
            ("ownerSelfApprovalException", False, "not recorded"),
            ("reviewDecision", "CHANGES_REQUESTED", "not approved"),
        ):
            with self.subTest(key=key):
                mutated = {**snapshot, key: value}
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_candidate_context(mutated, **arguments)

        for key, value, message in (
            ("codexReview", None, "no Codex review evidence"),
            (
                "codexReview",
                {**snapshot["codexReview"], "commitSha": "5" * 40},
                "Codex review is stale",
            ),
            (
                "codexReview",
                {**snapshot["codexReview"], "state": "PENDING"},
                "Codex review is incomplete",
            ),
            (
                "codexReview",
                {**snapshot["codexReview"], "source": "unknown"},
                "Codex review source is invalid",
            ),
        ):
            with self.subTest(key=key):
                mutated = {**snapshot, key: value}
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_candidate_context(mutated, **arguments)

        issue_comment = {
            **snapshot["codexReview"],
            "source": "issue-comment",
            "reviewedCommitPrefix": ("4" * 40)[:10],
        }
        MODULE.validate_candidate_context(
            {**snapshot, "codexReview": issue_comment}, **arguments
        )
        with self.assertRaisesRegex(ValueError, "issue comment is stale"):
            MODULE.validate_candidate_context(
                {
                    **snapshot,
                    "codexReview": {
                        **issue_comment,
                        "reviewedCommitPrefix": "5" * 10,
                    },
                },
                **arguments,
            )

    def test_rejects_codex_as_an_ordinary_pr_approval(self) -> None:
        snapshot = valid_snapshot()
        snapshot["approvals"] = [
            {
                "reviewer": f"{MODULE.CODEX_REVIEWER}[bot]",
                "commitSha": "4" * 40,
                "submittedAt": "2026-07-22T00:55:00Z",
            }
        ]

        with self.assertRaisesRegex(ValueError, "authored by Codex"):
            MODULE.validate_candidate_context(snapshot, **self.candidate_arguments())

    def test_rejects_owner_exception_on_an_approved_pr(self) -> None:
        with self.assertRaisesRegex(ValueError, "must be disabled"):
            MODULE.validate_candidate_context(
                {**valid_snapshot(), "ownerSelfApprovalException": True},
                **{
                    **self.candidate_arguments(),
                    "owner_self_approval_exception": True,
                },
            )

    def test_fresh_promotion_requires_current_main_but_recovery_allows_advance(
        self,
    ) -> None:
        common = {
            "source_sha": SHA,
            "source_tree": TREE,
            "checkout_sha": SHA,
            "checkout_tree": TREE,
            "source_branch": "main",
            "source_version": "0.10.0",
            "source_branch_sha": SHA,
            "workflow_sha": SHA,
        }
        MODULE.validate_promotion_source_state(
            **common,
            tag_state="absent",
            main_sha=SHA,
            source_is_branch_ancestor=True,
        )
        with self.assertRaisesRegex(ValueError, "workflow authority"):
            MODULE.validate_promotion_source_state(
                **common,
                tag_state="absent",
                main_sha="4" * 40,
                source_is_branch_ancestor=True,
            )
        MODULE.validate_promotion_source_state(
            **common,
            tag_state="present",
            main_sha=SHA,
            source_is_branch_ancestor=True,
        )
        with self.assertRaisesRegex(ValueError, "release branch"):
            MODULE.validate_promotion_source_state(
                **common,
                tag_state="present",
                main_sha=SHA,
                source_is_branch_ancestor=False,
            )

    def test_maintenance_promotion_requires_exact_current_branch_head(self) -> None:
        maintenance_sha = "5" * 40
        common = {
            "source_sha": maintenance_sha,
            "source_tree": TREE,
            "checkout_sha": maintenance_sha,
            "checkout_tree": TREE,
            "source_branch": "0.9.19",
            "source_version": "0.9.19",
            "source_branch_sha": maintenance_sha,
            "workflow_sha": SHA,
            "main_sha": SHA,
            "source_is_branch_ancestor": True,
        }
        MODULE.validate_promotion_source_state(**common, tag_state="absent")
        with self.assertRaisesRegex(ValueError, "current release branch head"):
            MODULE.validate_promotion_source_state(
                **{**common, "source_branch_sha": "6" * 40},
                tag_state="absent",
            )

    def test_existing_tag_must_be_annotated_exact_and_candidate_bound(self) -> None:
        tag_ref = {"object": {"type": "tag", "sha": TAG_OBJECT_SHA}}
        tag_object = {
            "sha": TAG_OBJECT_SHA,
            "tag": "v0.9.14",
            "object": {"type": "commit", "sha": SHA},
            "message": TAG_MESSAGE,
        }
        MODULE.validate_existing_tag(
            tag_ref,
            tag_object,
            expected_tag="v0.9.14",
            source_sha=SHA,
            expected_message=TAG_MESSAGE,
        )
        mutations = (
            ({"object": {"type": "commit", "sha": SHA}}, tag_object, "not annotated"),
            (tag_ref, {**tag_object, "sha": "4" * 40}, "different tag object"),
            (
                tag_ref,
                {**tag_object, "object": {"type": "commit", "sha": "4" * 40}},
                "target differs",
            ),
            (tag_ref, {**tag_object, "message": "conflict"}, "message differs"),
        )
        for ref, annotated, message in mutations:
            with self.subTest(message=message):
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_existing_tag(
                        ref,
                        annotated,
                        expected_tag="v0.9.14",
                        source_sha=SHA,
                        expected_message=TAG_MESSAGE,
                    )

    def test_existing_tag_accepts_crlf_transport_but_rejects_logical_drift(
        self,
    ) -> None:
        tag_ref = {"object": {"type": "tag", "sha": TAG_OBJECT_SHA}}
        tag_object = {
            "sha": TAG_OBJECT_SHA,
            "tag": "v0.9.14",
            "object": {"type": "commit", "sha": SHA},
            "message": TAG_MESSAGE.replace("\n", "\r\n"),
        }

        MODULE.validate_existing_tag(
            tag_ref,
            tag_object,
            expected_tag="v0.9.14",
            source_sha=SHA,
            expected_message=TAG_MESSAGE,
        )

        with self.assertRaisesRegex(ValueError, "message differs"):
            MODULE.validate_existing_tag(
                tag_ref,
                {
                    **tag_object,
                    "message": "NVT FW Combiner v0.9.14\r\ncandidate-run: 100",
                },
                expected_tag="v0.9.14",
                source_sha=SHA,
                expected_message=TAG_MESSAGE,
            )

    def test_existing_tag_rejects_every_non_transport_message_difference(self) -> None:
        tag_ref = {"object": {"type": "tag", "sha": TAG_OBJECT_SHA}}
        tag_object = {
            "sha": TAG_OBJECT_SHA,
            "tag": "v0.9.14",
            "object": {"type": "commit", "sha": SHA},
        }
        mutations = (
            " " + TAG_MESSAGE,
            TAG_MESSAGE + " ",
            TAG_MESSAGE + "\n",
            TAG_MESSAGE.replace("\n", "\r"),
        )

        for message in mutations:
            with self.subTest(message=repr(message)):
                with self.assertRaisesRegex(ValueError, "message differs"):
                    MODULE.validate_existing_tag(
                        tag_ref,
                        {**tag_object, "message": message},
                        expected_tag="v0.9.14",
                        source_sha=SHA,
                        expected_message=TAG_MESSAGE,
                    )

    def test_existing_release_metadata_returns_zero_one_or_all_names_as_arrays(
        self,
    ) -> None:
        names = ["one.zip", "two.json", "three.json", "manifest.json", "assets.sha256"]
        for count in (0, 1, len(names)):
            with self.subTest(count=count):
                release = json.loads(
                    json.dumps(
                        {
                            "tagName": "v0.9.14",
                            "isDraft": False,
                            "isPrerelease": False,
                            "body": "complete notes\n",
                            "assets": [{"name": name} for name in names[:count]],
                        }
                    )
                )
                actual = MODULE.validate_existing_release(
                    release,
                    expected_tag="v0.9.14",
                    expected_body="complete notes",
                )
                self.assertIsInstance(actual, list)
                self.assertEqual(names[:count], actual)

        for key, value, message in (
            ("tagName", "v0.9.15", "tag differs"),
            ("isDraft", True, "still a draft"),
            ("isPrerelease", True, "is a prerelease"),
            ("body", "conflicting notes", "body conflicts"),
        ):
            with self.subTest(key=key):
                release = {
                    "tagName": "v0.9.14",
                    "isDraft": False,
                    "isPrerelease": False,
                    "body": "complete notes",
                    "assets": [],
                    key: value,
                }
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.validate_existing_release(
                        release,
                        expected_tag="v0.9.14",
                        expected_body="complete notes",
                    )

        with self.assertRaisesRegex(ValueError, "JSON array"):
            MODULE.plan_release_asset_recovery(
                Path("unused-manifest"),
                Path("unused-published"),
                "one.zip",
            )

    def test_manifest_detects_digest_identity_and_extra_asset_drift(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="release-candidate-policy-"
        ) as temporary:
            temporary_root = Path(temporary)
            root = temporary_root / "assets"
            root.mkdir()
            version = "0.9.14"
            for name in MODULE._asset_names(version):
                (root / name).write_bytes(name.encode("utf-8"))
            notes = root / "RELEASE-NOTES.md"
            notes.write_text("release notes\n", encoding="utf-8")
            review = temporary_root / "review.json"
            review.write_text(json.dumps(valid_snapshot()), encoding="utf-8")
            manifest_path = MODULE.create_candidate_manifest(
                root,
                version=version,
                source_sha=SHA,
                source_tree=TREE,
                run_id="99",
                workflow_sha=SHA,
                workflow_ref="refs/heads/main",
                notes_path=notes,
                review_snapshot_path=review,
            )
            MODULE.verify_candidate_manifest(
                manifest_path,
                source_sha=SHA,
                source_tree=TREE,
                run_id="99",
                workflow_sha=SHA,
                workflow_ref="refs/heads/main",
            )

            (root / MODULE._asset_names(version)[0]).write_bytes(b"tampered")
            with self.assertRaisesRegex(ValueError, "size mismatch|digest mismatch"):
                MODULE.verify_candidate_manifest(
                    manifest_path,
                    source_sha=SHA,
                    source_tree=TREE,
                    run_id="99",
                    workflow_sha=SHA,
                    workflow_ref="refs/heads/main",
                )

    def test_manifest_rejects_wrong_run_and_unexpected_file(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="release-candidate-policy-"
        ) as temporary:
            temporary_root = Path(temporary)
            root = temporary_root / "assets"
            root.mkdir()
            version = "0.9.14"
            for name in MODULE._asset_names(version):
                (root / name).write_bytes(b"asset")
            notes = root / "RELEASE-NOTES.md"
            notes.write_bytes(b"notes")
            review = temporary_root / "review.json"
            review.write_text(json.dumps(valid_snapshot()), encoding="utf-8")
            manifest_path = MODULE.create_candidate_manifest(
                root,
                version=version,
                source_sha=SHA,
                source_tree=TREE,
                run_id="99",
                workflow_sha=SHA,
                workflow_ref="refs/heads/main",
                notes_path=notes,
                review_snapshot_path=review,
            )
            with self.assertRaisesRegex(ValueError, "candidateRunId mismatch"):
                MODULE.verify_candidate_manifest(
                    manifest_path,
                    source_sha=SHA,
                    source_tree=TREE,
                    run_id="100",
                    workflow_sha=SHA,
                    workflow_ref="refs/heads/main",
                )
            (root / "unexpected.exe").write_bytes(b"extra")
            with self.assertRaisesRegex(ValueError, "closed asset set"):
                MODULE.verify_candidate_manifest(
                    manifest_path,
                    source_sha=SHA,
                    source_tree=TREE,
                    run_id="99",
                    workflow_sha=SHA,
                    workflow_ref="refs/heads/main",
                )

    def test_recovery_allows_only_missing_assets_and_rejects_conflicts(self) -> None:
        with tempfile.TemporaryDirectory(
            prefix="release-recovery-policy-"
        ) as temporary:
            temporary_root = Path(temporary)
            candidate = temporary_root / "candidate"
            published = temporary_root / "published"
            candidate.mkdir()
            published.mkdir()
            version = "0.9.14"
            for name in MODULE._asset_names(version):
                (candidate / name).write_bytes(name.encode("utf-8"))
            notes = candidate / "RELEASE-NOTES.md"
            notes.write_bytes(b"notes")
            review = temporary_root / "review.json"
            review.write_text(json.dumps(valid_snapshot()), encoding="utf-8")
            manifest = MODULE.create_candidate_manifest(
                candidate,
                version=version,
                source_sha=SHA,
                source_tree=TREE,
                run_id="99",
                workflow_sha=SHA,
                workflow_ref="refs/heads/main",
                notes_path=notes,
                review_snapshot_path=review,
            )
            present_name = MODULE._asset_names(version)[0]
            (published / present_name).write_bytes(
                (candidate / present_name).read_bytes()
            )

            missing = MODULE.plan_release_asset_recovery(
                manifest, published, [present_name]
            )

            self.assertNotIn(present_name, missing)
            self.assertEqual(4, len(missing))
            (published / present_name).write_bytes(b"conflict")
            with self.assertRaisesRegex(ValueError, "digest conflicts"):
                MODULE.plan_release_asset_recovery(manifest, published, [present_name])
            with self.assertRaisesRegex(ValueError, "unexpected assets"):
                MODULE.plan_release_asset_recovery(
                    manifest, published, ["unexpected.zip"]
                )

    def test_github_probe_accepts_only_json_success_or_verified_404(self) -> None:
        self.assertEqual(
            "present", MODULE.classify_github_probe(0, '{"ref":"refs/tags/v0.9.14"}')
        )
        self.assertEqual(
            "absent", MODULE.classify_github_probe(1, "gh: Not Found (HTTP 404)")
        )
        for exit_code, output, message in (
            (0, "not-json", "malformed JSON"),
            (1, "gh: Forbidden (HTTP 403)", "without a verified 404"),
            (1, "network timeout", "without a verified 404"),
            (1, "gh: server error (HTTP 500)", "without a verified 404"),
        ):
            with self.subTest(exit_code=exit_code, output=output):
                with self.assertRaisesRegex(ValueError, message):
                    MODULE.classify_github_probe(exit_code, output)


if __name__ == "__main__":
    unittest.main()

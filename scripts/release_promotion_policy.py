"""Deterministic identity and artifact policy for stable release promotion."""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import os
import re
import subprocess
import tempfile
import zipfile
from pathlib import Path
from typing import Any


HEX_SHA = re.compile(r"^[0-9a-f]{40}$")
HEX_SHA256 = re.compile(r"^[0-9a-f]{64}$")
STABLE_VERSION = re.compile(r"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$")
REVIEW_PRIORITY = re.compile(
    r"(?:!\[\s*P([0-3])(?:\s+Badge)?\s*\]|\[\s*P([0-3])\s*\]|"
    r"(?<![A-Za-z0-9])P([0-3])\s*:)",
    re.IGNORECASE,
)
CODEX_REVIEWER = "chatgpt-codex-connector"
CODEX_REVIEW_SOURCES = frozenset({"pull-review", "inline-comment", "issue-comment"})
REQUIRED_RELEASE_CHECKS = (
    "policy / polytail",
    "python-worker / verify",
    "dotnet / build-test",
)
STRICT_RELEASE_POLICY_VERSION = (1, 1, 1)
MAINTENANCE_RELEASES = {
    "0.9.17": "0.9.17",
    "0.9.18": "0.9.18",
    "0.9.19": "0.9.19",
}
VERSION_ONLY_PACKAGE_PATHS = frozenset(
    {
        "NvtFwCombiner.exe",
        "launcher/NvtFwCombiner.Launcher.exe",
        "README.txt",
        "RELEASE-MANIFEST.json",
        "SHA256SUMS.txt",
    }
)
VERSION_ONLY_EXECUTABLE_PATHS = (
    "NvtFwCombiner.exe",
    "launcher/NvtFwCombiner.Launcher.exe",
)
VERSION_ONLY_STABLE_REUSE_PATHS = frozenset(
    {"external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe"}
)


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def _require_sha(value: object, label: str) -> None:
    _require(
        isinstance(value, str) and HEX_SHA.fullmatch(value) is not None,
        f"{label} must be a lowercase 40-character Git SHA",
    )


def _require_sha256(value: object, label: str) -> None:
    _require(
        isinstance(value, str) and HEX_SHA256.fullmatch(value) is not None,
        f"{label} must be a lowercase SHA-256",
    )


def _normalize_reviewer(value: object) -> str:
    """Normalize GitHub bot and non-bot logins before policy comparisons."""

    if not isinstance(value, str):
        return ""
    normalized = value.strip().lower()
    return normalized.removesuffix("[bot]").rstrip()


def _stable_version_parts(value: str, label: str) -> tuple[int, int, int]:
    _require(
        STABLE_VERSION.fullmatch(value) is not None,
        f"{label} must be stable SemVer",
    )
    major, minor, patch = value.split(".")
    return int(major), int(minor), int(patch)


def _requires_strict_release_policy(version: str) -> bool:
    return (
        _stable_version_parts(version, "release version")
        >= STRICT_RELEASE_POLICY_VERSION
    )


def _validate_remote_main(snapshot: dict[str, Any], main_sha: str) -> None:
    """Require one exact protected remote-main observation."""

    remote_main = snapshot.get("remoteMain")
    _require(isinstance(remote_main, dict), "remote main evidence is malformed")
    _require_sha(remote_main.get("sha"), "remote main SHA")
    _require(
        remote_main.get("sha") == main_sha, "remote main SHA differs from authority"
    )
    _require(
        remote_main.get("protected") is True,
        "remote main must be protected before stable release",
    )


def _validate_release_branch_position(
    *,
    tag_state: str,
    source_sha: str,
    source_branch_sha: str,
    source_is_branch_ancestor: bool,
) -> None:
    """Require exact new-tag source or forward-only existing-tag recovery."""

    _require(
        isinstance(tag_state, str) and tag_state in {"absent", "present"},
        "stable tag state is invalid",
    )
    _require(
        isinstance(source_is_branch_ancestor, bool),
        "release branch ancestry evidence is malformed",
    )
    if tag_state == "absent":
        _require(
            source_branch_sha == source_sha,
            "a new stable tag may be created only from the current release branch head",
        )
        return
    _require(
        source_is_branch_ancestor,
        "a recovery candidate must remain reachable from its release branch",
    )


def validate_live_branch_authority(
    snapshot: dict[str, Any], *, main_sha: str, source_sha: str
) -> None:
    """Bind every release mutation to fresh main and source-branch evidence."""

    _require(isinstance(snapshot, dict), "live branch authority must be an object")
    _require_sha(main_sha, "live branch authority main SHA")
    _require_sha(source_sha, "live branch authority source SHA")
    _validate_remote_main(snapshot, main_sha)
    remote_source = snapshot.get("remoteSource")
    _require(isinstance(remote_source, dict), "remote source evidence is malformed")
    _require_sha(remote_source.get("sha"), "remote source SHA")
    remote_source_sha = remote_source["sha"]
    tag_state = snapshot.get("tagState")
    source_is_branch_ancestor = remote_source_sha == source_sha
    if tag_state == "present" and not source_is_branch_ancestor:
        comparison = snapshot.get("sourceComparison")
        _require(
            isinstance(comparison, dict),
            "fresh recovery source ancestry evidence is missing",
        )
        source_is_branch_ancestor = (
            comparison.get("baseSha") == source_sha
            and comparison.get("headSha") == remote_source_sha
            and comparison.get("mergeBaseSha") == source_sha
            and comparison.get("status") == "ahead"
        )
    _validate_release_branch_position(
        tag_state=tag_state,
        source_sha=source_sha,
        source_branch_sha=remote_source_sha,
        source_is_branch_ancestor=source_is_branch_ancestor,
    )


def validate_repository_admission(
    snapshot: dict[str, Any],
    *,
    main_sha: str,
    review_head_sha: str,
    expected_tag: str,
) -> None:
    """Validate fresh GitHub repository policy at a stable-release boundary."""

    _require(isinstance(snapshot, dict), "repository admission must be an object")
    _require_sha(main_sha, "repository admission main SHA")
    _require_sha(review_head_sha, "repository admission review head SHA")
    _require(
        isinstance(expected_tag, str)
        and expected_tag.startswith("v")
        and STABLE_VERSION.fullmatch(expected_tag[1:]) is not None,
        "repository admission stable tag is invalid",
    )

    _validate_remote_main(snapshot, main_sha)

    _require(
        snapshot.get("mainRulesPaginationComplete") is True,
        "main rules pagination is incomplete",
    )
    main_rules = snapshot.get("mainRules")
    _require(isinstance(main_rules, list), "main rules evidence is malformed")
    required_contexts: list[str] = []
    for rule in main_rules:
        _require(isinstance(rule, dict), "main rules entry is malformed")
        if rule.get("type") != "required_status_checks":
            continue
        parameters = rule.get("parameters")
        _require(
            isinstance(parameters, dict),
            "required status checks rule parameters are malformed",
        )
        contexts = parameters.get("required_status_checks")
        _require(
            isinstance(contexts, list),
            "required status checks rule inventory is malformed",
        )
        for context in contexts:
            _require(
                isinstance(context, dict)
                and isinstance(context.get("context"), str)
                and bool(context["context"]),
                "required status checks rule entry is malformed",
            )
            required_contexts.append(context["context"])
    _require(
        len(required_contexts) == len(set(required_contexts))
        and set(required_contexts) == set(REQUIRED_RELEASE_CHECKS),
        "required status checks do not equal the closed release check set",
    )

    _require(
        snapshot.get("checkRunsPaginationComplete") is True,
        "required check runs pagination is incomplete",
    )
    check_runs = snapshot.get("checkRuns")
    _require(isinstance(check_runs, list), "required check runs evidence is malformed")
    _require(
        all(isinstance(item, dict) for item in check_runs),
        "required check runs entry is malformed",
    )
    for required_name in REQUIRED_RELEASE_CHECKS:
        matches = [item for item in check_runs if item.get("name") == required_name]
        _require(
            len(matches) == 1,
            f"required check runs must contain exactly one {required_name}",
        )
        match = matches[0]
        _require(
            match.get("headSha") == review_head_sha
            and match.get("appSlug") == "github-actions"
            and match.get("status") == "completed"
            and match.get("conclusion") == "success",
            f"required check runs are not exact and passing: {required_name}",
        )

    _require(
        snapshot.get("reviewThreadsPaginationComplete") is True,
        "review threads pagination is incomplete",
    )
    review_threads = snapshot.get("reviewThreads")
    _require(isinstance(review_threads, list), "review threads evidence is malformed")
    for thread in review_threads:
        _require(isinstance(thread, dict), "review thread entry is malformed")
        _require(
            isinstance(thread.get("isResolved"), bool)
            and isinstance(thread.get("isOutdated"), bool)
            and isinstance(thread.get("body"), str),
            "review thread entry is malformed",
        )
        if thread["isResolved"]:
            continue
        priorities = {
            int(group)
            for match in REVIEW_PRIORITY.finditer(thread["body"])
            for group in match.groups()
            if group is not None
        }
        _require(
            bool(priorities),
            "every unresolved review thread needs a classifiable priority",
        )
        _require(
            len(priorities) == 1,
            "every unresolved review thread must have exactly one priority",
        )
        priority = next(iter(priorities))
        _require(priority >= 2, f"unresolved P{priority} review thread blocks release")

    _require(
        snapshot.get("tagRulesetsPaginationComplete") is True,
        "stable tag rulesets pagination is incomplete",
    )
    tag_rulesets = snapshot.get("tagRulesets")
    _require(
        isinstance(tag_rulesets, list), "stable tag rulesets evidence is malformed"
    )
    expected_ref = f"refs/tags/{expected_tag}"
    immutable_ruleset_found = False
    ruleset_ids: set[int] = set()
    for ruleset in tag_rulesets:
        _require(isinstance(ruleset, dict), "stable tag ruleset entry is malformed")
        ruleset_id = ruleset.get("id")
        _require(
            isinstance(ruleset_id, int)
            and not isinstance(ruleset_id, bool)
            and ruleset_id > 0,
            "stable tag ruleset id is malformed",
        )
        _require(
            ruleset_id not in ruleset_ids,
            "stable tag ruleset inventory repeats an id",
        )
        ruleset_ids.add(ruleset_id)
        _require(
            ruleset.get("target") == "tag",
            "stable tag ruleset target is contradictory",
        )
        _require(
            isinstance(ruleset.get("enforcement"), str)
            and bool(ruleset["enforcement"]),
            "stable tag ruleset enforcement is malformed",
        )
        conditions = ruleset.get("conditions")
        _require(
            isinstance(conditions, dict), "stable tag ruleset conditions are malformed"
        )
        ref_name = conditions.get("ref_name")
        _require(
            isinstance(ref_name, dict), "stable tag ruleset ref condition is malformed"
        )
        include = ref_name.get("include")
        exclude = ref_name.get("exclude")
        _require(
            isinstance(include, list)
            and all(isinstance(item, str) and bool(item) for item in include),
            "stable tag ruleset include inventory is malformed",
        )
        _require(
            isinstance(exclude, list)
            and all(isinstance(item, str) and bool(item) for item in exclude),
            "stable tag ruleset exclude inventory is malformed",
        )
        rules = ruleset.get("rules")
        _require(
            isinstance(rules, list)
            and all(
                isinstance(rule, dict)
                and isinstance(rule.get("type"), str)
                and bool(rule["type"])
                for rule in rules
            ),
            "stable tag ruleset rule inventory is malformed",
        )
        if (
            ruleset["enforcement"] == "active"
            and ("refs/tags/v*" in include or "~ALL" in include)
            and exclude == []
        ):
            rule_types = {rule.get("type") for rule in rules}
            immutable_ruleset_found = immutable_ruleset_found or {
                "update",
                "deletion",
            }.issubset(rule_types)
    _require(
        immutable_ruleset_found,
        f"stable tag {expected_ref} has no active update/deletion ruleset",
    )


def _validate_release_source(source_branch: str, source_version: str) -> None:
    """Allow protected main or an explicitly owner-approved maintenance release."""

    _require(
        STABLE_VERSION.fullmatch(source_version) is not None,
        "release source VERSION must be stable SemVer",
    )
    _require(
        source_version != "1.1.0",
        "v1.1.0 is a manual-only operator release and cannot be rebuilt or "
        "recovered by CI; publish a new version",
    )
    if source_branch == "main":
        return
    _require(
        MAINTENANCE_RELEASES.get(source_branch) == source_version,
        "release source is not an approved maintenance branch/version",
    )


def _normalize_transport_line_endings(value: str) -> str:
    """Normalize only CRLF transport differences in external text payloads."""

    return value.replace("\r\n", "\n")


def validate_candidate_context(
    snapshot: dict[str, Any],
    *,
    requested_sha: str,
    workflow_sha: str,
    workflow_ref: str,
    source_sha: str,
    source_branch: str,
    source_version: str,
    main_sha: str,
    source_tree: str,
    repository_owner: str,
    workflow_actor: str,
    owner_self_approval_exception: bool,
) -> None:
    """Fail unless protected main authorizes an exact reviewed release source."""

    for label, value in (
        ("requested SHA", requested_sha),
        ("workflow SHA", workflow_sha),
        ("source SHA", source_sha),
        ("main SHA", main_sha),
        ("source tree", source_tree),
    ):
        _require_sha(value, label)
    _require(
        workflow_ref == "refs/heads/main",
        "release workflow must be dispatched from main",
    )
    _require(
        workflow_sha == main_sha,
        "workflow definition must be the current protected main SHA",
    )
    _require(
        requested_sha == source_sha,
        "requested and checkout source SHAs must be identical",
    )
    _validate_release_source(source_branch, source_version)
    if source_branch == "main":
        _require(
            source_sha == main_sha,
            "a main release source must be the current protected main SHA",
        )
    _require(
        isinstance(snapshot.get("number"), int) and snapshot["number"] > 0,
        "reviewed PR number is invalid",
    )
    _require(snapshot.get("state") == "MERGED", "reviewed PR must be merged")
    _require(bool(snapshot.get("mergedAt")), "reviewed PR has no merged timestamp")
    _require(
        snapshot.get("baseRefName") == source_branch,
        "reviewed PR must target the selected release source branch",
    )
    _require(
        snapshot.get("mergeCommitSha") == source_sha,
        "reviewed PR merge commit is not the candidate",
    )
    _require(
        snapshot.get("headTree") == source_tree,
        "reviewed PR tree differs from the candidate tree",
    )
    head_sha = snapshot.get("headSha")
    _require(isinstance(head_sha, str), "reviewed PR head SHA is missing")
    _require_sha(head_sha, "reviewed PR head SHA")
    approvals = snapshot.get("approvals")
    review_decision = snapshot.get("reviewDecision")
    if review_decision == "APPROVED":
        _require(
            not owner_self_approval_exception
            and snapshot.get("ownerSelfApprovalException") is False,
            "owner self-approval exception must be disabled for an approved PR",
        )
        _require(
            isinstance(approvals, list) and bool(approvals),
            "reviewed PR has no current-head approval",
        )
        _require(
            all(
                isinstance(item, dict)
                and item.get("commitSha") == head_sha
                and _normalize_reviewer(item.get("reviewer")) != CODEX_REVIEWER
                and isinstance(item.get("submittedAt"), str)
                and bool(item["submittedAt"])
                for item in approvals
            ),
            "reviewed PR approval is stale, malformed, or authored by Codex",
        )
    else:
        _require(
            review_decision in {None, "", "REVIEW_REQUIRED"},
            "reviewed PR is not approved",
        )
        _require(
            owner_self_approval_exception,
            "reviewed PR has no current-head approval",
        )
        _require(
            isinstance(repository_owner, str)
            and repository_owner != ""
            and workflow_actor == repository_owner,
            "owner self-approval exception must be dispatched by the repository owner",
        )
        _require(
            snapshot.get("repositoryOwner") == repository_owner
            and snapshot.get("workflowActor") == workflow_actor,
            "owner self-approval exception identity differs from release evidence",
        )
        _require(
            snapshot.get("authorLogin") == repository_owner,
            "owner self-approval exception requires the repository owner to author the PR",
        )
        _require(
            snapshot.get("ownerSelfApprovalException") is True,
            "owner self-approval exception was not recorded in review evidence",
        )
        source_version_parts = tuple(int(part) for part in source_version.split("."))
        if not (1, 0, 8) <= source_version_parts < (1, 2, 0):
            _require_exact_head_codex_review(snapshot, head_sha)
    checks = snapshot.get("requiredChecks")
    _require(
        isinstance(checks, list) and bool(checks), "reviewed PR has no required checks"
    )
    failed = [
        str(item.get("name", "<unnamed>"))
        for item in checks
        if not isinstance(item, dict) or item.get("bucket") != "pass"
    ]
    _require(
        not failed, f"reviewed PR required checks are not passing: {', '.join(failed)}"
    )
    if _requires_strict_release_policy(source_version):
        admission = snapshot.get("repositoryAdmission")
        _require(
            isinstance(admission, dict),
            "reviewed PR has no repository admission evidence",
        )
        validate_repository_admission(
            admission,
            main_sha=main_sha,
            review_head_sha=head_sha,
            expected_tag=f"v{source_version}",
        )


def _require_exact_head_codex_review(snapshot: dict[str, Any], head_sha: str) -> None:
    """Require a completed Codex response for the exact self-approved PR head."""

    review = snapshot.get("codexReview")
    _require(
        isinstance(review, dict),
        "owner self-approval exception has no Codex review evidence",
    )
    _require(
        _normalize_reviewer(review.get("reviewer")) == CODEX_REVIEWER,
        "owner self-approval exception Codex reviewer is invalid",
    )
    source = review.get("source")
    _require(
        source in CODEX_REVIEW_SOURCES,
        "owner self-approval exception Codex review source is invalid",
    )
    _require(
        review.get("commitSha") == head_sha,
        "owner self-approval exception Codex review is stale",
    )
    _require(
        review.get("state") in {"COMMENTED", "APPROVED"},
        "owner self-approval exception Codex review is incomplete",
    )
    _require(
        isinstance(review.get("submittedAt"), str) and bool(review["submittedAt"]),
        "owner self-approval exception Codex review has no submission time",
    )
    if source == "issue-comment":
        _require(
            review.get("reviewedCommitPrefix") == head_sha[:10],
            "owner self-approval exception Codex issue comment is stale",
        )


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _asset_names(version: str) -> tuple[str, str, str]:
    prefix = f"NvtFwCombiner-v{version}-win-x64"
    return (f"{prefix}.zip", f"{prefix}.spdx.json", f"{prefix}.provenance.json")


def _installer_asset_names(version: str) -> tuple[str, str, str, str, str]:
    prefix = f"NvtFwCombiner-Launcher-v{version}-win-x64"
    return (
        f"{prefix}.exe",
        f"{prefix}.manifest.json",
        f"{prefix}.spdx.json",
        f"{prefix}.intoto.jsonl",
        f"{prefix}.sha256",
    )


def _publishes_installer_assets(version: str) -> bool:
    parts = tuple(int(part) for part in version.split("."))
    return parts >= (1, 0, 6)


def _candidate_asset_names(version: str) -> tuple[str, ...]:
    return (
        (*_asset_names(version), *_installer_asset_names(version))
        if _publishes_installer_assets(version)
        else _asset_names(version)
    )


def create_candidate_manifest(
    asset_dir: Path,
    *,
    version: str,
    source_sha: str,
    source_tree: str,
    run_id: str,
    workflow_sha: str,
    workflow_ref: str,
    notes_path: Path,
    review_snapshot_path: Path,
) -> Path:
    """Create a closed candidate manifest and versioned outer checksum asset."""

    _require(
        STABLE_VERSION.fullmatch(version) is not None,
        "candidate version must be stable SemVer",
    )
    for label, value in (
        ("source SHA", source_sha),
        ("source tree", source_tree),
        ("workflow SHA", workflow_sha),
    ):
        _require_sha(value, label)
    _require(workflow_ref == "refs/heads/main", "candidate workflow ref must be main")
    _require(
        run_id.isdigit() and int(run_id) > 0,
        "candidate run id must be a positive integer",
    )
    _require(notes_path.is_file(), f"release notes are missing: {notes_path}")
    _require(
        review_snapshot_path.is_file(),
        f"review snapshot is missing: {review_snapshot_path}",
    )
    review_snapshot = json.loads(review_snapshot_path.read_text(encoding="utf-8"))
    _require(isinstance(review_snapshot, dict), "review snapshot must be an object")

    expected_names = _candidate_asset_names(version)
    assets: list[dict[str, Any]] = []
    for name in expected_names:
        path = asset_dir / name
        _require(path.is_file(), f"candidate asset is missing: {name}")
        assets.append(
            {"name": name, "size": path.stat().st_size, "sha256": _sha256(path)}
        )

    manifest_path = asset_dir / f"NvtFwCombiner-v{version}-candidate.json"
    checksum_path = asset_dir / f"NvtFwCombiner-v{version}-assets.sha256"
    unexpected = sorted(
        path.name
        for path in asset_dir.iterdir()
        if path.is_file()
        and path.name
        not in {
            *expected_names,
            notes_path.name,
            manifest_path.name,
            checksum_path.name,
        }
    )
    _require(not unexpected, f"unexpected candidate assets: {', '.join(unexpected)}")

    manifest = {
        "schemaVersion": "1.0",
        "version": version,
        "tag": f"v{version}",
        "sourceSha": source_sha,
        "sourceTree": source_tree,
        "workflowSha": workflow_sha,
        "workflowRef": workflow_ref,
        "candidateRunId": run_id,
        "reviewEvidence": review_snapshot,
        "reviewSnapshotSha256": _sha256(review_snapshot_path),
        "releaseNotes": {
            "name": notes_path.name,
            "size": notes_path.stat().st_size,
            "sha256": _sha256(notes_path),
        },
        "assets": assets,
    }
    manifest_path.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    checksum_entries = [
        *assets,
        {"name": manifest_path.name, "sha256": _sha256(manifest_path)},
    ]
    checksum_path.write_text(
        "".join(f"{entry['sha256']}  {entry['name']}\n" for entry in checksum_entries),
        encoding="utf-8",
        newline="\n",
    )
    return manifest_path


def classify_github_probe(exit_code: int, output: str) -> str:
    """Classify a gh API probe; only a verified HTTP 404 is absence."""

    if exit_code == 0:
        try:
            payload = json.loads(output)
        except json.JSONDecodeError as exc:
            raise ValueError("GitHub resource probe returned malformed JSON") from exc
        _require(
            isinstance(payload, dict),
            "GitHub resource probe returned an unexpected payload",
        )
        return "present"
    if re.search(r"\(HTTP 404\)\s*$", output.strip()):
        return "absent"
    raise ValueError(
        f"GitHub resource probe failed without a verified 404: {output.strip()}"
    )


def probe_github_resource(endpoint: str) -> str:
    _require(
        bool(endpoint) and not endpoint.startswith("-"),
        "GitHub API endpoint is invalid",
    )
    result = subprocess.run(
        ["gh", "api", endpoint],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    return classify_github_probe(result.returncode, result.stdout + result.stderr)


def validate_promotion_source_state(
    *,
    tag_state: str,
    source_sha: str,
    source_tree: str,
    checkout_sha: str,
    checkout_tree: str,
    source_branch: str,
    source_version: str,
    source_branch_sha: str,
    workflow_sha: str,
    main_sha: str,
    source_is_branch_ancestor: bool,
) -> None:
    """Require current release authority and an exact/reachable release source."""

    for label, value in (
        ("source SHA", source_sha),
        ("source tree", source_tree),
        ("checkout SHA", checkout_sha),
        ("checkout tree", checkout_tree),
        ("source branch SHA", source_branch_sha),
        ("workflow SHA", workflow_sha),
        ("main SHA", main_sha),
    ):
        _require_sha(value, label)
    _require(
        checkout_sha == source_sha, "prepared checkout SHA differs from the candidate"
    )
    _require(
        checkout_tree == source_tree,
        "prepared checkout tree differs from the candidate",
    )
    _require(
        workflow_sha == main_sha,
        "release workflow authority is no longer current protected main",
    )
    _validate_release_source(source_branch, source_version)
    if source_branch == "main":
        _require(
            source_branch_sha == main_sha,
            "main release branch identity differs from protected main",
        )
    _validate_release_branch_position(
        tag_state=tag_state,
        source_sha=source_sha,
        source_branch_sha=source_branch_sha,
        source_is_branch_ancestor=source_is_branch_ancestor,
    )


def validate_version_only_upgrade(snapshot: dict[str, Any]) -> None:
    """Require 1.0.1 to be the direct, content-only VERSION child of v1.0.0."""

    _require(isinstance(snapshot, dict), "version-only evidence must be an object")
    source_sha = snapshot.get("sourceSha")
    base_tag_commit = snapshot.get("baseTagCommit")
    _require_sha(source_sha, "version-only source SHA")
    _require_sha(base_tag_commit, "v1.0.0 peeled commit SHA")
    _require(
        snapshot.get("sourceVersion") == "1.0.1",
        "version-only source VERSION must be 1.0.1",
    )
    _require(
        snapshot.get("baseTag") == "v1.0.0",
        "version-only base tag must be v1.0.0",
    )
    _require(
        snapshot.get("baseVersion") == "1.0.0",
        "v1.0.0 tag must contain VERSION 1.0.0",
    )
    parent_shas = snapshot.get("parentShas")
    _require(
        isinstance(parent_shas, list)
        and len(parent_shas) == 1
        and parent_shas[0] == base_tag_commit,
        "1.0.1 must be the direct single-parent child of v1.0.0",
    )
    _require(
        snapshot.get("changedPaths") == ["VERSION"],
        "1.0.1 may change only the canonical VERSION file",
    )
    _require(
        snapshot.get("modeChanges") == [],
        "1.0.1 must not change file modes",
    )


def _git(repository: Path, *arguments: str) -> str:
    result = subprocess.run(
        ["git", "-C", str(repository), *arguments],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if result.returncode != 0:
        detail = (result.stderr or result.stdout).strip()
        raise ValueError(f"Git release evidence could not be read: {detail}")
    return result.stdout.strip()


def validate_version_only_lineage(repository: Path) -> None:
    """Read Git directly and prove that 1.0.1 changes VERSION content only."""

    repository = repository.resolve(strict=True)
    _require(repository.is_dir(), "version-only repository must be a directory")
    source_version = (repository / "VERSION").read_text(encoding="utf-8").strip()
    source_sha = _git(repository, "rev-parse", "--verify", "HEAD^{commit}")
    tag_type = _git(repository, "cat-file", "-t", "refs/tags/v1.0.0")
    _require(tag_type == "tag", "v1.0.0 must be an annotated immutable tag")
    base_tag_commit = _git(
        repository, "rev-parse", "--verify", "refs/tags/v1.0.0^{commit}"
    )
    commit_parts = _git(repository, "rev-list", "--parents", "-n", "1", "HEAD").split()
    base_version = _git(repository, "show", f"{base_tag_commit}:VERSION").strip()
    range_spec = f"{base_tag_commit}..{source_sha}"
    changed_paths = [
        line
        for line in _git(
            repository, "diff", "--name-only", "--no-renames", range_spec
        ).splitlines()
        if line
    ]
    mode_changes = [
        line
        for line in _git(repository, "diff", "--summary", range_spec).splitlines()
        if "mode change" in line
    ]
    validate_version_only_upgrade(
        {
            "sourceVersion": source_version,
            "sourceSha": source_sha,
            "baseTag": "v1.0.0",
            "baseTagCommit": base_tag_commit,
            "baseVersion": base_version,
            "parentShas": commit_parts[1:],
            "changedPaths": changed_paths,
            "modeChanges": mode_changes,
        }
    )


def _read_closed_zip(path: Path, captured: bytes | None = None) -> dict[str, bytes]:
    _require(
        captured is not None or path.is_file(), f"release package is missing: {path}"
    )
    entries: dict[str, bytes] = {}
    identities: set[str] = set()
    try:
        with zipfile.ZipFile(
            io.BytesIO(captured) if captured is not None else path
        ) as archive:
            for entry in archive.infolist():
                if entry.is_dir():
                    continue
                name = entry.filename.replace("\\", "/")
                _require(
                    name == entry.filename
                    and name != ""
                    and not name.startswith("/")
                    and all(part not in {"", ".", ".."} for part in name.split("/")),
                    f"release package contains an unsafe path: {entry.filename}",
                )
                identity = name.casefold()
                _require(
                    identity not in identities, f"release package repeats path: {name}"
                )
                identities.add(identity)
                entries[name] = archive.read(entry)
    except zipfile.BadZipFile as exc:
        raise ValueError(f"release package is not a valid ZIP: {path}") from exc
    _require(bool(entries), "release package contains no files")
    top_levels = {name.split("/", 1)[0] for name in entries}
    if len(top_levels) == 1 and all("/" in name for name in entries):
        prefix = f"{next(iter(top_levels))}/"
        entries = {
            name.removeprefix(prefix): content for name, content in entries.items()
        }
    return entries


def _manifest_from_package(entries: dict[str, bytes]) -> dict[str, Any]:
    raw = entries.get("RELEASE-MANIFEST.json")
    _require(raw is not None, "release package has no RELEASE-MANIFEST.json")
    try:
        document = json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ValueError("release package manifest is invalid") from exc
    _require(isinstance(document, dict), "release package manifest must be an object")
    return document


def _normalize_version_manifest(
    manifest: dict[str, Any],
    *,
    version: str,
    source_sha: str,
) -> dict[str, Any]:
    _require(manifest.get("version") == version, "package manifest version mismatch")
    _require(
        manifest.get("sourceTag") == f"v{version}", "package manifest tag mismatch"
    )
    _require(
        manifest.get("sourceCommit") == source_sha, "package manifest source mismatch"
    )
    normalized = json.loads(json.dumps(manifest))
    normalized["version"] = "<VERSION>"
    normalized["sourceTag"] = "v<VERSION>"
    normalized["sourceCommit"] = "<SOURCE-COMMIT>"
    for key, suffix in (
        ("provenanceAsset", ".provenance.json"),
        ("sbomAsset", ".spdx.json"),
    ):
        expected = f"NvtFwCombiner-v{version}-win-x64{suffix}"
        _require(manifest.get(key) == expected, f"package manifest {key} mismatch")
        normalized[key] = f"NvtFwCombiner-v<VERSION>-win-x64{suffix}"
    files = normalized.get("files")
    _require(isinstance(files, list), "package manifest files must be an array")
    file_paths: set[str] = set()
    for entry in files:
        _require(isinstance(entry, dict), "package manifest file entry is invalid")
        path = entry.get("path")
        _require(
            isinstance(path, str) and path not in file_paths,
            "package manifest repeats a path",
        )
        file_paths.add(path)
        if path in VERSION_ONLY_PACKAGE_PATHS:
            entry["size"] = "<VERSION-SIZE>"
            entry["sha256"] = "<VERSION-SHA256>"
    launcher = normalized.get("launcher")
    _require(isinstance(launcher, dict), "package manifest launcher is missing")
    _require(
        launcher.get("launcherVersion") == version,
        "package manifest launcher version mismatch",
    )
    launcher["launcherVersion"] = "<VERSION>"
    launcher["size"] = "<VERSION-SIZE>"
    launcher["sha256"] = "<VERSION-SHA256>"
    return normalized


def _normalized_hash_list(raw: bytes) -> dict[str, str]:
    try:
        lines = raw.decode("utf-8").splitlines()
    except UnicodeDecodeError as exc:
        raise ValueError("package SHA256SUMS.txt is not UTF-8") from exc
    result: dict[str, str] = {}
    for line in lines:
        match = re.fullmatch(r"([0-9a-f]{64})  (\S.*)", line)
        _require(match is not None, "package SHA256SUMS.txt is malformed")
        digest, path = match.groups()
        _require(path not in result, "package SHA256SUMS.txt repeats a path")
        result[path] = (
            "<VERSION-SHA256>" if path in VERSION_ONLY_PACKAGE_PATHS else digest
        )
    return result


def _read_windows_file_version(path: Path) -> tuple[str, str]:
    script = (
        "$v=[Diagnostics.FileVersionInfo]::GetVersionInfo($env:NFC_VERSION_FILE);"
        "[ordered]@{fileVersion=$v.FileVersion;productVersion=$v.ProductVersion}"
        "|ConvertTo-Json -Compress"
    )
    environment = dict(os.environ)
    environment["NFC_VERSION_FILE"] = str(path)
    result = subprocess.run(
        ["pwsh", "-NoProfile", "-NonInteractive", "-Command", script],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        env=environment,
    )
    _require(result.returncode == 0, "Windows executable version could not be read")
    try:
        value = json.loads(result.stdout)
    except json.JSONDecodeError as exc:
        raise ValueError("Windows executable version evidence is malformed") from exc
    _require(isinstance(value, dict), "Windows executable version evidence is invalid")
    return str(value.get("fileVersion", "")), str(value.get("productVersion", ""))


def _require_executable_version(path: Path, version: str) -> None:
    file_version, product_version = _read_windows_file_version(path)
    _require(
        file_version == f"{version}.0" and product_version == version,
        f"Windows executable version metadata does not equal {version}: {path.name}",
    )


def validate_version_only_packages(
    repository: Path,
    base_package: Path,
    candidate_package: Path,
    base_package_sha256: str,
) -> None:
    """Prove the 1.0.1 package differs only at declared version-bearing paths."""

    repository = repository.resolve(strict=True)
    validate_version_only_lineage(repository)
    base_sha = _git(repository, "rev-parse", "--verify", "refs/tags/v1.0.0^{commit}")
    candidate_sha = _git(repository, "rev-parse", "--verify", "HEAD^{commit}")
    resolved_base_package = base_package.resolve(strict=True)
    base_package_bytes = resolved_base_package.read_bytes()
    _require_sha256(base_package_sha256, "published 1.0.0 package SHA-256")
    _require(
        hashlib.sha256(base_package_bytes).hexdigest() == base_package_sha256,
        "published 1.0.0 package does not match its independently supplied asset digest",
    )
    base_entries = _read_closed_zip(resolved_base_package, base_package_bytes)
    candidate_entries = _read_closed_zip(candidate_package.resolve())
    _require(
        set(base_entries) == set(candidate_entries),
        "1.0.0 and 1.0.1 package inventories differ",
    )
    _require(
        VERSION_ONLY_PACKAGE_PATHS <= set(base_entries),
        "version-only package allowlist is incomplete",
    )
    changed = {
        path for path in base_entries if base_entries[path] != candidate_entries[path]
    }
    _require(
        changed == VERSION_ONLY_PACKAGE_PATHS,
        "package byte differences are not exactly the declared version-bearing paths",
    )
    base_manifest = _manifest_from_package(base_entries)
    candidate_manifest = _manifest_from_package(candidate_entries)
    _require(
        _normalize_version_manifest(base_manifest, version="1.0.0", source_sha=base_sha)
        == _normalize_version_manifest(
            candidate_manifest, version="1.0.1", source_sha=candidate_sha
        ),
        "package manifests differ beyond declared version identity",
    )
    base_readme = (
        base_entries["README.txt"].decode("utf-8").replace("1.0.0", "<VERSION>")
    )
    candidate_readme = (
        candidate_entries["README.txt"].decode("utf-8").replace("1.0.1", "<VERSION>")
    )
    _require(base_readme == candidate_readme, "package README differs beyond version")
    _require(
        _normalized_hash_list(base_entries["SHA256SUMS.txt"])
        == _normalized_hash_list(candidate_entries["SHA256SUMS.txt"]),
        "package hash lists differ beyond version-bearing hashes",
    )
    with tempfile.TemporaryDirectory(prefix="nfc-version-only-package-") as temporary:
        root = Path(temporary)
        for version, entries in (("1.0.0", base_entries), ("1.0.1", candidate_entries)):
            for relative_path in VERSION_ONLY_EXECUTABLE_PATHS:
                destination = root / version / relative_path
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(entries[relative_path])
                _require_executable_version(destination, version)


def extract_version_only_stable_payload(
    repository: Path,
    base_package: Path,
    destination: Path,
    relative_path: str,
    base_package_sha256: str,
) -> None:
    """Reuse one unchanged, manifest-bound payload from the published 1.0.0 ZIP."""

    _require(
        relative_path in VERSION_ONLY_STABLE_REUSE_PATHS,
        "requested package path is not approved for version-only reuse",
    )
    repository = repository.resolve(strict=True)
    validate_version_only_lineage(repository)
    base_sha = _git(repository, "rev-parse", "--verify", "refs/tags/v1.0.0^{commit}")
    resolved_base_package = base_package.resolve(strict=True)
    base_package_bytes = resolved_base_package.read_bytes()
    _require_sha256(base_package_sha256, "published 1.0.0 package SHA-256")
    _require(
        hashlib.sha256(base_package_bytes).hexdigest() == base_package_sha256,
        "published 1.0.0 package does not match its independently supplied asset digest",
    )
    entries = _read_closed_zip(resolved_base_package, base_package_bytes)
    _require(relative_path in entries, "published 1.0.0 package payload is missing")
    manifest = _manifest_from_package(entries)
    _normalize_version_manifest(manifest, version="1.0.0", source_sha=base_sha)
    manifest_files = manifest.get("files")
    _require(
        isinstance(manifest_files, list), "package manifest files must be an array"
    )
    matches = [
        entry
        for entry in manifest_files
        if isinstance(entry, dict) and entry.get("path") == relative_path
    ]
    _require(len(matches) == 1, "published 1.0.0 manifest payload binding is missing")
    payload = entries[relative_path]
    digest = hashlib.sha256(payload).hexdigest()
    _require(matches[0].get("size") == len(payload), "published payload size mismatch")
    _require(matches[0].get("sha256") == digest, "published payload hash mismatch")
    hashes = _normalized_hash_list(entries.get("SHA256SUMS.txt", b""))
    _require(
        hashes.get(relative_path) == digest, "published hash list payload mismatch"
    )
    _require(
        not destination.exists(), "version-only payload destination already exists"
    )
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(payload)


def validate_existing_tag(
    tag_ref: dict[str, Any],
    tag_object: dict[str, Any],
    *,
    expected_tag: str,
    source_sha: str,
    expected_message: str,
) -> None:
    """Reject lightweight, moved, or differently bound existing stable tags."""

    _require_sha(source_sha, "source SHA")
    reference_object = tag_ref.get("object")
    _require(isinstance(reference_object, dict), "existing stable tag ref is malformed")
    _require(
        reference_object.get("type") == "tag", "existing stable tag is not annotated"
    )
    tag_object_sha = tag_object.get("sha")
    _require_sha(tag_object_sha, "annotated tag object SHA")
    _require(
        reference_object.get("sha") == tag_object_sha,
        "stable tag ref points at a different tag object",
    )
    target = tag_object.get("object")
    _require(isinstance(target, dict), "annotated tag target is malformed")
    _require(
        tag_object.get("tag") == expected_tag,
        "annotated tag name differs from the candidate",
    )
    _require(target.get("type") == "commit", "annotated tag target is not a commit")
    _require(
        target.get("sha") == source_sha,
        "annotated tag target differs from the candidate",
    )
    message = tag_object.get("message")
    _require(isinstance(message, str), "annotated tag message is missing")
    _require(
        _normalize_transport_line_endings(message)
        == _normalize_transport_line_endings(expected_message),
        "annotated tag message differs from the candidate",
    )


def validate_existing_release(
    release: dict[str, Any],
    *,
    expected_tag: str,
    expected_body: str,
    expected_assets: dict[str, dict[str, Any]] | None = None,
) -> list[str]:
    """Validate Release metadata and return an always-array asset inventory."""

    _require(isinstance(release, dict), "existing Release metadata is malformed")
    _require(
        expected_tag.startswith("v")
        and STABLE_VERSION.fullmatch(expected_tag[1:]) is not None,
        "existing Release expected tag is invalid",
    )
    strict = _requires_strict_release_policy(expected_tag[1:])
    tag_name = release.get("tag_name", release.get("tagName"))
    is_draft = release.get("draft", release.get("isDraft"))
    is_prerelease = release.get("prerelease", release.get("isPrerelease"))
    _require(
        tag_name == expected_tag,
        "existing Release tag differs from the candidate",
    )
    _require(is_draft is False, "existing Release is still a draft")
    _require(is_prerelease is False, "existing Release is a prerelease")
    if strict:
        _require(
            release.get("immutable") is True,
            "existing Release must be REST immutable",
        )
        _require(
            isinstance(expected_assets, dict),
            "immutable Release validation requires candidate asset metadata",
        )
        assert expected_assets is not None
        for expected_name, expected_metadata in expected_assets.items():
            _require(
                isinstance(expected_name, str)
                and bool(expected_name)
                and Path(expected_name).name == expected_name
                and isinstance(expected_metadata, dict),
                "candidate asset metadata is malformed",
            )
    body = release.get("body")
    _require(isinstance(body, str), "existing Release body is missing")
    _require(
        body.strip() == expected_body.strip(),
        "existing Release body conflicts with the candidate",
    )
    assets = release.get("assets")
    _require(isinstance(assets, list), "existing Release asset inventory is malformed")
    names: list[str] = []
    for asset in assets:
        _require(isinstance(asset, dict), "existing Release asset entry is malformed")
        name = asset.get("name")
        _require(
            isinstance(name, str) and bool(name) and Path(name).name == name,
            "existing Release asset name is invalid",
        )
        names.append(name)
    _require(len(names) == len(set(names)), "existing Release repeats an asset name")
    if strict:
        assert expected_assets is not None
        _require(
            set(names) == set(expected_assets),
            "existing Release asset set differs from the candidate",
        )
        for asset in assets:
            name = asset["name"]
            expected = expected_assets[name]
            expected_size = expected.get("size")
            expected_sha256 = expected.get("sha256")
            _require(
                isinstance(expected_size, int)
                and not isinstance(expected_size, bool)
                and expected_size >= 0,
                f"candidate asset size is invalid: {name}",
            )
            _require_sha256(expected_sha256, f"candidate asset digest: {name}")
            _require(
                asset.get("state") == "uploaded",
                f"existing Release asset is not in uploaded state: {name}",
            )
            _require(
                isinstance(asset.get("size"), int)
                and not isinstance(asset.get("size"), bool)
                and asset.get("size") == expected_size,
                f"existing Release asset size differs from the candidate: {name}",
            )
            _require(
                asset.get("digest") == f"sha256:{expected_sha256}",
                f"existing Release asset digest differs from the candidate: {name}",
            )
    return names


def expected_published_asset_metadata(
    manifest_path: Path,
) -> dict[str, dict[str, Any]]:
    """Read exact candidate metadata for every published GitHub Release asset."""

    manifest_path = manifest_path.resolve(strict=True)
    _require(manifest_path.is_file(), "candidate manifest path must be a file")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    _require(isinstance(manifest, dict), "candidate manifest must be an object")
    version = manifest.get("version")
    _require(
        isinstance(version, str) and STABLE_VERSION.fullmatch(version) is not None,
        "candidate manifest version is invalid",
    )
    entries = manifest.get("assets")
    _require(isinstance(entries, list), "candidate manifest assets are invalid")
    result: dict[str, dict[str, Any]] = {}
    for entry in entries:
        _require(isinstance(entry, dict), "candidate asset entry is invalid")
        name = entry.get("name")
        _require(
            isinstance(name, str) and bool(name) and Path(name).name == name,
            "candidate asset name is unsafe",
        )
        _require(name not in result, "candidate manifest repeats an asset name")
        size = entry.get("size")
        digest = entry.get("sha256")
        _require(
            isinstance(size, int) and not isinstance(size, bool) and size >= 0,
            f"candidate asset size is invalid: {name}",
        )
        _require_sha256(digest, f"candidate asset digest: {name}")
        result[name] = {"size": size, "sha256": digest}

    checksum_path = manifest_path.parent / f"NvtFwCombiner-v{version}-assets.sha256"
    for path in (manifest_path, checksum_path):
        _require(path.is_file(), f"candidate published asset is missing: {path.name}")
        _require(path.name not in result, "candidate repeats a published asset name")
        result[path.name] = {"size": path.stat().st_size, "sha256": _sha256(path)}
    return result


def verify_candidate_manifest(
    manifest_path: Path,
    *,
    source_sha: str,
    source_tree: str,
    run_id: str,
    workflow_sha: str,
    workflow_ref: str,
) -> dict[str, Any]:
    """Verify candidate identity, exact asset set, notes, checksums, and payload hashes."""

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    root = manifest_path.parent
    _require(
        manifest.get("schemaVersion") == "1.0",
        "candidate manifest schema is unsupported",
    )
    version = manifest.get("version")
    _require(
        isinstance(version, str) and STABLE_VERSION.fullmatch(version) is not None,
        "candidate manifest version is invalid",
    )
    expected_identity = {
        "sourceSha": source_sha,
        "sourceTree": source_tree,
        "candidateRunId": run_id,
        "workflowSha": workflow_sha,
        "workflowRef": workflow_ref,
    }
    for key, expected in expected_identity.items():
        _require(manifest.get(key) == expected, f"candidate manifest {key} mismatch")
    _require(manifest.get("tag") == f"v{version}", "candidate tag/version mismatch")

    notes = manifest.get("releaseNotes")
    _require(isinstance(notes, dict), "candidate releaseNotes is invalid")
    entries = manifest.get("assets")
    expected_names = set(_candidate_asset_names(version))
    _require(
        isinstance(entries, list) and len(entries) == len(expected_names),
        "candidate manifest payload asset count is invalid for its version",
    )
    actual_names = {entry.get("name") for entry in entries if isinstance(entry, dict)}
    _require(
        actual_names == expected_names, "candidate payload asset names are invalid"
    )

    def verify_entry(entry: dict[str, Any], label: str) -> None:
        name = entry.get("name")
        _require(
            isinstance(name, str) and bool(name) and Path(name).name == name,
            f"{label} name is unsafe",
        )
        path = root / name
        _require(path.is_file(), f"{label} is missing: {name}")
        _require(
            entry.get("size") == path.stat().st_size, f"{label} size mismatch: {name}"
        )
        _require(
            entry.get("sha256") == _sha256(path), f"{label} digest mismatch: {name}"
        )

    for entry in entries:
        _require(isinstance(entry, dict), "candidate asset entry is invalid")
        verify_entry(entry, "candidate asset")
    verify_entry(notes, "release notes")

    checksum_path = root / f"NvtFwCombiner-v{version}-assets.sha256"
    _require(checksum_path.is_file(), "candidate outer checksum asset is missing")
    expected_checksum = "".join(
        f"{entry['sha256']}  {entry['name']}\n"
        for entry in [
            *entries,
            {"name": manifest_path.name, "sha256": _sha256(manifest_path)},
        ]
    )
    _require(
        checksum_path.read_text(encoding="utf-8") == expected_checksum,
        "candidate outer checksum asset is invalid",
    )

    expected_files = expected_names | {
        notes["name"],
        manifest_path.name,
        checksum_path.name,
    }
    actual_files = {path.name for path in root.iterdir() if path.is_file()}
    _require(
        actual_files == expected_files,
        "candidate directory differs from its closed asset set",
    )
    return manifest


def plan_release_asset_recovery(
    manifest_path: Path,
    published_dir: Path,
    published_names: list[str],
) -> list[str]:
    """Return only missing assets; reject unexpected names or conflicting bytes."""

    _require(
        isinstance(published_names, list),
        "published Release asset names must be a JSON array",
    )
    _require(
        all(
            isinstance(name, str) and bool(name) and Path(name).name == name
            for name in published_names
        ),
        "published Release asset names are invalid",
    )
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    version = manifest.get("version")
    _require(
        isinstance(version, str) and STABLE_VERSION.fullmatch(version) is not None,
        "candidate manifest version is invalid",
    )
    expected_names = {
        *(entry["name"] for entry in manifest["assets"]),
        manifest_path.name,
        f"NvtFwCombiner-v{version}-assets.sha256",
    }
    actual_names = set(published_names)
    _require(
        len(actual_names) == len(published_names),
        "published Release repeats an asset name",
    )
    unexpected = sorted(actual_names - expected_names)
    _require(
        not unexpected,
        f"published Release has unexpected assets: {', '.join(unexpected)}",
    )
    candidate_root = manifest_path.parent
    for name in sorted(actual_names):
        candidate = candidate_root / name
        published = published_dir / name
        _require(published.is_file(), f"published asset download is missing: {name}")
        _require(
            _sha256(published) == _sha256(candidate),
            f"published asset digest conflicts: {name}",
        )
    return sorted(expected_names - actual_names)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    context = subparsers.add_parser("validate-context")
    context.add_argument("--snapshot", type=Path, required=True)
    for name in (
        "requested-sha",
        "workflow-sha",
        "workflow-ref",
        "source-sha",
        "source-branch",
        "source-version",
        "main-sha",
        "source-tree",
    ):
        context.add_argument(f"--{name}", required=True)
    context.add_argument("--repository-owner", required=True)
    context.add_argument("--workflow-actor", required=True)
    context.add_argument(
        "--owner-self-approval-exception", choices=("true", "false"), required=True
    )
    repository_admission = subparsers.add_parser("validate-repository-admission")
    repository_admission.add_argument("--snapshot", type=Path, required=True)
    repository_admission.add_argument("--main-sha", required=True)
    repository_admission.add_argument("--review-head-sha", required=True)
    repository_admission.add_argument("--expected-tag", required=True)
    live_authority = subparsers.add_parser("validate-live-branch-authority")
    live_authority.add_argument("--snapshot", type=Path, required=True)
    live_authority.add_argument("--main-sha", required=True)
    live_authority.add_argument("--source-sha", required=True)

    for command in ("create-manifest", "verify-manifest"):
        manifest = subparsers.add_parser(command)
        manifest.add_argument("--asset-dir", type=Path, required=True)
        manifest.add_argument("--source-sha", required=True)
        manifest.add_argument("--source-tree", required=True)
        manifest.add_argument("--run-id", required=True)
        manifest.add_argument("--workflow-sha", required=True)
        manifest.add_argument("--workflow-ref", required=True)
        if command == "create-manifest":
            manifest.add_argument("--version", required=True)
            manifest.add_argument("--notes", type=Path, required=True)
            manifest.add_argument("--review-snapshot", type=Path, required=True)
        else:
            manifest.add_argument("--manifest", type=Path, required=True)
    probe = subparsers.add_parser("probe-resource")
    probe.add_argument("--endpoint", required=True)
    promotion = subparsers.add_parser("validate-promotion-source")
    promotion.add_argument("--tag-state", choices=("absent", "present"), required=True)
    promotion.add_argument("--source-sha", required=True)
    promotion.add_argument("--source-tree", required=True)
    promotion.add_argument("--checkout-sha", required=True)
    promotion.add_argument("--checkout-tree", required=True)
    promotion.add_argument("--source-branch", required=True)
    promotion.add_argument("--source-version", required=True)
    promotion.add_argument("--source-branch-sha", required=True)
    promotion.add_argument("--workflow-sha", required=True)
    promotion.add_argument("--main-sha", required=True)
    promotion.add_argument(
        "--source-is-branch-ancestor",
        choices=("true", "false"),
        required=True,
    )
    version_only = subparsers.add_parser("validate-version-only-lineage")
    version_only.add_argument("--repository", type=Path, required=True)
    version_package = subparsers.add_parser("validate-version-only-package")
    version_package.add_argument("--repository", type=Path, required=True)
    version_package.add_argument("--base-package", type=Path, required=True)
    version_package.add_argument("--base-package-sha256", required=True)
    version_package.add_argument("--candidate-package", type=Path, required=True)
    stable_payload = subparsers.add_parser("extract-version-only-stable-payload")
    stable_payload.add_argument("--repository", type=Path, required=True)
    stable_payload.add_argument("--base-package", type=Path, required=True)
    stable_payload.add_argument("--base-package-sha256", required=True)
    stable_payload.add_argument("--destination", type=Path, required=True)
    stable_payload.add_argument("--path", required=True)
    tag = subparsers.add_parser("validate-tag")
    tag.add_argument("--tag-ref", type=Path, required=True)
    tag.add_argument("--tag-object", type=Path, required=True)
    tag.add_argument("--expected-tag", required=True)
    tag.add_argument("--source-sha", required=True)
    tag.add_argument("--expected-message", type=Path, required=True)
    release = subparsers.add_parser("validate-release")
    release.add_argument("--release", type=Path, required=True)
    release.add_argument("--expected-tag", required=True)
    release.add_argument("--expected-body", type=Path, required=True)
    release.add_argument("--manifest", type=Path)
    recovery = subparsers.add_parser("plan-recovery")
    recovery.add_argument("--manifest", type=Path, required=True)
    recovery.add_argument("--published-dir", type=Path, required=True)
    recovery.add_argument("--published-names", type=Path, required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.command == "validate-context":
        validate_candidate_context(
            json.loads(args.snapshot.read_text(encoding="utf-8")),
            requested_sha=args.requested_sha,
            workflow_sha=args.workflow_sha,
            workflow_ref=args.workflow_ref,
            source_sha=args.source_sha,
            source_branch=args.source_branch,
            source_version=args.source_version,
            main_sha=args.main_sha,
            source_tree=args.source_tree,
            repository_owner=args.repository_owner,
            workflow_actor=args.workflow_actor,
            owner_self_approval_exception=args.owner_self_approval_exception == "true",
        )
    elif args.command == "validate-repository-admission":
        validate_repository_admission(
            json.loads(args.snapshot.read_text(encoding="utf-8")),
            main_sha=args.main_sha,
            review_head_sha=args.review_head_sha,
            expected_tag=args.expected_tag,
        )
    elif args.command == "validate-live-branch-authority":
        validate_live_branch_authority(
            json.loads(args.snapshot.read_text(encoding="utf-8")),
            main_sha=args.main_sha,
            source_sha=args.source_sha,
        )
    elif args.command == "create-manifest":
        manifest_path = create_candidate_manifest(
            args.asset_dir,
            version=args.version,
            source_sha=args.source_sha,
            source_tree=args.source_tree,
            run_id=args.run_id,
            workflow_sha=args.workflow_sha,
            workflow_ref=args.workflow_ref,
            notes_path=args.notes,
            review_snapshot_path=args.review_snapshot,
        )
        print(manifest_path)
    elif args.command == "verify-manifest":
        verify_candidate_manifest(
            args.manifest,
            source_sha=args.source_sha,
            source_tree=args.source_tree,
            run_id=args.run_id,
            workflow_sha=args.workflow_sha,
            workflow_ref=args.workflow_ref,
        )
    elif args.command == "probe-resource":
        print(probe_github_resource(args.endpoint))
    elif args.command == "validate-promotion-source":
        validate_promotion_source_state(
            tag_state=args.tag_state,
            source_sha=args.source_sha,
            source_tree=args.source_tree,
            checkout_sha=args.checkout_sha,
            checkout_tree=args.checkout_tree,
            source_branch=args.source_branch,
            source_version=args.source_version,
            source_branch_sha=args.source_branch_sha,
            workflow_sha=args.workflow_sha,
            main_sha=args.main_sha,
            source_is_branch_ancestor=args.source_is_branch_ancestor == "true",
        )
    elif args.command == "validate-version-only-lineage":
        validate_version_only_lineage(args.repository)
    elif args.command == "validate-version-only-package":
        validate_version_only_packages(
            args.repository,
            args.base_package,
            args.candidate_package,
            args.base_package_sha256,
        )
    elif args.command == "extract-version-only-stable-payload":
        extract_version_only_stable_payload(
            args.repository,
            args.base_package,
            args.destination,
            args.path,
            args.base_package_sha256,
        )
    elif args.command == "validate-tag":
        validate_existing_tag(
            json.loads(args.tag_ref.read_text(encoding="utf-8")),
            json.loads(args.tag_object.read_text(encoding="utf-8")),
            expected_tag=args.expected_tag,
            source_sha=args.source_sha,
            expected_message=args.expected_message.read_text(encoding="utf-8"),
        )
    elif args.command == "validate-release":
        names = validate_existing_release(
            json.loads(args.release.read_text(encoding="utf-8")),
            expected_tag=args.expected_tag,
            expected_body=args.expected_body.read_text(encoding="utf-8"),
            expected_assets=(
                expected_published_asset_metadata(args.manifest)
                if args.manifest is not None
                else None
            ),
        )
        print(json.dumps(names))
    else:
        missing = plan_release_asset_recovery(
            args.manifest,
            args.published_dir,
            json.loads(args.published_names.read_text(encoding="utf-8")),
        )
        print(json.dumps(missing))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

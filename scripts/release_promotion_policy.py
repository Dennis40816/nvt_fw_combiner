"""Deterministic identity and artifact policy for stable release promotion."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path
from typing import Any


HEX_SHA = re.compile(r"^[0-9a-f]{40}$")
STABLE_VERSION = re.compile(r"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$")
CODEX_REVIEWER = "chatgpt-codex-connector"
CODEX_REVIEW_SOURCES = frozenset({"pull-review", "inline-comment", "issue-comment"})


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def _require_sha(value: object, label: str) -> None:
    _require(
        isinstance(value, str) and HEX_SHA.fullmatch(value) is not None,
        f"{label} must be a lowercase 40-character Git SHA",
    )


def _normalize_reviewer(value: object) -> str:
    """Normalize GitHub bot and non-bot logins before policy comparisons."""

    if not isinstance(value, str):
        return ""
    normalized = value.strip().lower()
    return normalized.removesuffix("[bot]").rstrip()


def validate_candidate_context(
    snapshot: dict[str, Any],
    *,
    requested_sha: str,
    workflow_sha: str,
    workflow_ref: str,
    source_sha: str,
    main_sha: str,
    source_tree: str,
    repository_owner: str,
    workflow_actor: str,
    owner_self_approval_exception: bool,
) -> None:
    """Fail unless the candidate is the reviewed final PR merged at current main."""

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
        len({requested_sha, workflow_sha, source_sha, main_sha}) == 1,
        "requested, workflow, checkout, and current main SHAs must be identical",
    )
    _require(
        isinstance(snapshot.get("number"), int) and snapshot["number"] > 0,
        "reviewed PR number is invalid",
    )
    _require(snapshot.get("state") == "MERGED", "reviewed PR must be merged")
    _require(bool(snapshot.get("mergedAt")), "reviewed PR has no merged timestamp")
    _require(snapshot.get("baseRefName") == "main", "reviewed PR must target main")
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

    expected_names = _asset_names(version)
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
    main_sha: str,
    source_is_main_ancestor: bool,
) -> None:
    """Require current main for first publication and reachable source for recovery."""

    for label, value in (
        ("source SHA", source_sha),
        ("source tree", source_tree),
        ("checkout SHA", checkout_sha),
        ("checkout tree", checkout_tree),
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
    _require(tag_state in {"absent", "present"}, "stable tag state is invalid")
    if tag_state == "absent":
        _require(
            main_sha == source_sha,
            "a new stable tag may be created only from current protected main",
        )
        return
    _require(
        source_is_main_ancestor,
        "a recovery candidate must remain reachable from protected main",
    )


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
        message.replace("\r\n", "\n") == expected_message.replace("\r\n", "\n"),
        "annotated tag message differs from the candidate",
    )


def validate_existing_release(
    release: dict[str, Any],
    *,
    expected_tag: str,
    expected_body: str,
) -> list[str]:
    """Validate immutable Release metadata and return an always-array asset inventory."""

    _require(
        release.get("tagName") == expected_tag,
        "existing Release tag differs from the candidate",
    )
    _require(release.get("isDraft") is False, "existing Release is still a draft")
    _require(release.get("isPrerelease") is False, "existing Release is a prerelease")
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
            isinstance(name, str) and Path(name).name == name,
            "existing Release asset name is invalid",
        )
        names.append(name)
    _require(len(names) == len(set(names)), "existing Release repeats an asset name")
    return names


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
    _require(
        isinstance(entries, list) and len(entries) == 3,
        "candidate manifest must declare three payload assets",
    )
    expected_names = set(_asset_names(version))
    actual_names = {entry.get("name") for entry in entries if isinstance(entry, dict)}
    _require(
        actual_names == expected_names, "candidate payload asset names are invalid"
    )

    def verify_entry(entry: dict[str, Any], label: str) -> None:
        name = entry.get("name")
        _require(
            isinstance(name, str) and Path(name).name == name, f"{label} name is unsafe"
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
            isinstance(name, str) and Path(name).name == name
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
        "main-sha",
        "source-tree",
    ):
        context.add_argument(f"--{name}", required=True)
    context.add_argument("--repository-owner", required=True)
    context.add_argument("--workflow-actor", required=True)
    context.add_argument(
        "--owner-self-approval-exception", choices=("true", "false"), required=True
    )

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
    promotion.add_argument("--main-sha", required=True)
    promotion.add_argument(
        "--source-is-main-ancestor",
        choices=("true", "false"),
        required=True,
    )
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
            main_sha=args.main_sha,
            source_tree=args.source_tree,
            repository_owner=args.repository_owner,
            workflow_actor=args.workflow_actor,
            owner_self_approval_exception=args.owner_self_approval_exception == "true",
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
            main_sha=args.main_sha,
            source_is_main_ancestor=args.source_is_main_ancestor == "true",
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

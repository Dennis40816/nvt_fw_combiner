"""Collect deterministic, read-only evidence for a version-branch review handoff."""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Any, Sequence


ROOT = Path(__file__).resolve().parents[1]
SHA_PATTERN = re.compile(r"^[0-9a-f]{40}$")
TAG_PATTERN = re.compile(r"^v?[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$")
RECORD_KEY_PATTERN = re.compile(r"^[a-z][a-z0-9-]*$")
CI_STATES = frozenset({"pass", "pending", "fail", "unavailable", "not-collected"})
CHECK_STATES = frozenset({"pass", "warning", "fail", "not-run"})
GATE_STATES = frozenset({"open", "closed", "not-applicable", "not-collected"})
REQUIRED_GATES = frozenset(
    {"firmware-owner", "golden", "packaging", "release-owner", "codex-review"}
)


class ReviewHandoffError(ValueError):
    """Raised when review evidence would be incomplete or have unsafe lineage."""


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise ReviewHandoffError(message)


def _run_git(repo_root: Path, *arguments: str) -> bytes:
    result = subprocess.run(
        ["git", *arguments],
        cwd=repo_root,
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    if result.returncode != 0:
        detail = result.stderr.decode("utf-8", errors="replace").strip()
        raise ReviewHandoffError(
            f"git {' '.join(arguments)} failed" + (f": {detail}" if detail else "")
        )
    return result.stdout


def _git_text(repo_root: Path, *arguments: str) -> str:
    return _run_git(repo_root, *arguments).decode("utf-8", errors="strict").strip()


def _require_sha(value: str, label: str) -> None:
    _require(
        SHA_PATTERN.fullmatch(value) is not None,
        f"{label} must be a lowercase 40-character Git SHA",
    )


def _parse_records(
    values: Sequence[str],
    *,
    label: str,
    allowed_states: frozenset[str],
) -> dict[str, str]:
    records: dict[str, str] = {}
    for value in values:
        key, separator, state = value.partition("=")
        _require(
            separator == "=" and RECORD_KEY_PATTERN.fullmatch(key) is not None,
            f"{label} must use name=state",
        )
        _require(
            state in allowed_states, f"{label} '{key}' has invalid state '{state}'"
        )
        _require(key not in records, f"{label} repeats '{key}'")
        records[key] = state
    _require(bool(records), f"{label} must not be empty")
    return dict(sorted(records.items()))


def _require_nonempty(values: Sequence[str], label: str) -> list[str]:
    cleaned = [value.strip() for value in values]
    _require(
        bool(cleaned) and all(cleaned),
        f"{label} must contain at least one non-empty value",
    )
    return cleaned


def _require_clean_worktree(repo_root: Path) -> None:
    status = _run_git(repo_root, "status", "--porcelain=v1", "--untracked-files=all")
    _require(not status, "worktree is dirty; review handoff collection fails closed")


def _resolve_annotated_baseline(
    repo_root: Path, baseline_tag: str, expected_baseline_sha: str
) -> tuple[str, str]:
    _require(TAG_PATTERN.fullmatch(baseline_tag) is not None, "baseline tag is invalid")
    _require_sha(expected_baseline_sha, "expected baseline SHA")
    tag_ref = f"refs/tags/{baseline_tag}"
    tag_object = _git_text(repo_root, "rev-parse", "--verify", tag_ref)
    _require_sha(tag_object, "baseline tag object SHA")
    tag_type = _git_text(repo_root, "cat-file", "-t", tag_ref)
    _require(tag_type == "tag", "baseline tag must be annotated")
    peeled_sha = _git_text(repo_root, "rev-parse", f"{tag_ref}^{{}}")
    _require_sha(peeled_sha, "baseline peeled commit SHA")
    _require(
        peeled_sha == expected_baseline_sha,
        "baseline annotated tag does not peel to the expected commit",
    )
    return tag_object, peeled_sha


def _require_baseline_ancestor(repo_root: Path, baseline_sha: str) -> None:
    result = subprocess.run(
        ["git", "merge-base", "--is-ancestor", baseline_sha, "HEAD"],
        cwd=repo_root,
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    if result.returncode == 0:
        return
    if result.returncode == 1:
        raise ReviewHandoffError("baseline is not an ancestor of the current HEAD")
    detail = result.stderr.decode("utf-8", errors="replace").strip()
    raise ReviewHandoffError(
        "could not verify baseline ancestry" + (f": {detail}" if detail else "")
    )


def _require_clean_range_diff(repo_root: Path, baseline_sha: str) -> None:
    result = subprocess.run(
        ["git", "diff", "--check", f"{baseline_sha}..HEAD"],
        cwd=repo_root,
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    _require(
        result.returncode == 0,
        "baseline-to-HEAD diff has whitespace errors; review handoff collection fails closed",
    )


def _collect_commits(repo_root: Path, baseline_sha: str) -> list[dict[str, str]]:
    output = _git_text(
        repo_root, "log", "--reverse", "--format=%H%x09%s", f"{baseline_sha}..HEAD"
    )
    commits: list[dict[str, str]] = []
    for line in output.splitlines():
        sha, separator, subject = line.partition("\t")
        _require(separator == "\t", "git log returned an invalid review commit record")
        _require_sha(sha, "review commit SHA")
        commits.append({"sha": sha, "subject": subject})
    _require(bool(commits), "no commits exist beyond the baseline")
    return commits


def _collect_changed_files(repo_root: Path, baseline_sha: str) -> list[dict[str, str]]:
    raw = _run_git(repo_root, "diff", "--name-status", "-z", f"{baseline_sha}..HEAD")
    tokens = [
        token.decode("utf-8", errors="strict") for token in raw.split(b"\0") if token
    ]
    files: list[dict[str, str]] = []
    index = 0
    while index < len(tokens):
        status = tokens[index]
        index += 1
        _require(bool(status), "git diff returned an invalid file status")
        if status[0] in {"R", "C"}:
            _require(
                index + 1 < len(tokens),
                "git diff returned an incomplete rename/copy record",
            )
            old_path, new_path = tokens[index], tokens[index + 1]
            index += 2
            files.append({"status": status, "oldPath": old_path, "path": new_path})
            continue
        _require(index < len(tokens), "git diff returned an incomplete file record")
        files.append({"status": status, "path": tokens[index]})
        index += 1
    _require(bool(files), "no changed files exist beyond the baseline")
    return files


def collect_handoff(
    *,
    repo_root: Path,
    baseline_tag: str,
    expected_baseline_sha: str,
    verification: Sequence[str],
    ci_state: str,
    ci_url: str | None,
    impact: Sequence[str],
    unchanged_boundary: Sequence[str],
    gates: Sequence[str],
) -> dict[str, Any]:
    """Return a review-evidence payload after read-only lineage preflight."""

    resolved_root = repo_root.resolve()
    reported_root = Path(
        _git_text(resolved_root, "rev-parse", "--show-toplevel")
    ).resolve()
    _require(reported_root == resolved_root, "repo root must be the Git worktree root")
    _require_clean_worktree(resolved_root)
    tag_object, peeled_sha = _resolve_annotated_baseline(
        resolved_root, baseline_tag, expected_baseline_sha
    )
    branch = _git_text(resolved_root, "symbolic-ref", "--quiet", "--short", "HEAD")
    _require(
        branch not in {"", "main", "master"},
        "review handoff must run on a non-main branch",
    )
    _require_baseline_ancestor(resolved_root, peeled_sha)
    _require_clean_range_diff(resolved_root, peeled_sha)
    _require(ci_state in CI_STATES, "CI state is invalid")
    if ci_url is not None:
        _require(ci_url.startswith("https://"), "CI URL must use HTTPS")
    verification_records = _parse_records(
        verification, label="verification", allowed_states=CHECK_STATES
    )
    gate_records = _parse_records(gates, label="gate", allowed_states=GATE_STATES)
    _require(
        set(gate_records) == REQUIRED_GATES,
        "gate records must identify firmware-owner, golden, packaging, release-owner, and codex-review",
    )

    head_sha = _git_text(resolved_root, "rev-parse", "HEAD")
    head_tree = _git_text(resolved_root, "rev-parse", "HEAD^{tree}")
    _require_sha(head_sha, "HEAD SHA")
    _require_sha(head_tree, "HEAD tree SHA")
    return {
        "schemaVersion": "1.0",
        "collector": {
            "id": "review-handoff",
            "authorization": "evidence-only",
            "doesNot": ["build", "merge", "publish", "push", "tag"],
        },
        "baseline": {
            "tag": baseline_tag,
            "annotatedTagObjectSha": tag_object,
            "peeledCommitSha": peeled_sha,
        },
        "head": {"branch": branch, "commitSha": head_sha, "treeSha": head_tree},
        "worktree": {"state": "clean", "baselineIsAncestor": True, "diffCheck": "pass"},
        "changes": {
            "commits": _collect_commits(resolved_root, peeled_sha),
            "files": _collect_changed_files(resolved_root, peeled_sha),
        },
        "ci": {"state": ci_state, "url": ci_url},
        "verification": verification_records,
        "impact": _require_nonempty(impact, "impact"),
        "unchangedCandidateBoundaries": _require_nonempty(
            unchanged_boundary, "unchanged candidate boundary"
        ),
        "residualGates": gate_records,
    }


def serialize_handoff(payload: dict[str, Any]) -> str:
    """Use ASCII JSON so evidence remains readable on legacy Windows consoles."""

    return json.dumps(payload, ensure_ascii=True, indent=2, sort_keys=True) + "\n"


def write_handoff(payload: dict[str, Any], output_path: Path, repo_root: Path) -> None:
    """Atomically write only to an explicit path outside the clean worktree."""

    resolved_output = output_path.resolve()
    resolved_root = repo_root.resolve()
    _require(
        not resolved_output.is_relative_to(resolved_root),
        "handoff output must be outside the repository worktree",
    )
    resolved_output.parent.mkdir(parents=True, exist_ok=True)
    temporary_name: str | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="w",
            encoding="utf-8",
            newline="\n",
            dir=resolved_output.parent,
            prefix=f".{resolved_output.name}.",
            suffix=".tmp",
            delete=False,
        ) as temporary:
            temporary.write(serialize_handoff(payload))
            temporary_name = temporary.name
        os.replace(temporary_name, resolved_output)
        temporary_name = None
    finally:
        if temporary_name is not None:
            Path(temporary_name).unlink(missing_ok=True)


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=ROOT)
    parser.add_argument("--baseline-tag", required=True)
    parser.add_argument("--expected-baseline-sha", required=True)
    parser.add_argument(
        "--verification", action="append", required=True, metavar="NAME=STATE"
    )
    parser.add_argument(
        "--ci-state", choices=sorted(CI_STATES), default="not-collected"
    )
    parser.add_argument("--ci-url")
    parser.add_argument("--impact", action="append", required=True)
    parser.add_argument("--unchanged-boundary", action="append", required=True)
    parser.add_argument("--gate", action="append", required=True, metavar="NAME=STATE")
    parser.add_argument("--output", type=Path)
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        payload = collect_handoff(
            repo_root=args.repo_root,
            baseline_tag=args.baseline_tag,
            expected_baseline_sha=args.expected_baseline_sha,
            verification=args.verification,
            ci_state=args.ci_state,
            ci_url=args.ci_url,
            impact=args.impact,
            unchanged_boundary=args.unchanged_boundary,
            gates=args.gate,
        )
        if args.output is None:
            print(serialize_handoff(payload), end="")
        else:
            write_handoff(payload, args.output, args.repo_root)
            print(args.output.resolve())
        return 0
    except ReviewHandoffError as error:
        print(f"review handoff failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

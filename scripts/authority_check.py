"""The `governance / authority` pull request check (ADR 0080, G1-A).

It classifies the change a pull request brings against the authority policy of
its head and of its live base, then checks the description's authority block
(declared risk, roles and their evidence entries) and the review records on the
head. Evaluation is pure; the Git reader and the read-only GitHub adapter only
collect inputs. Every missing, malformed or failed input fails the check. The
check verifies presence and form; the owner judges content (ADR 0080, P5).
"""

from __future__ import annotations

import argparse
import http.client
import json
import os
import re
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request
from collections.abc import Callable, Mapping, Sequence
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

POLICY_PATH = "docs/governance/authority-policy.json"
SCHEMA_PATH = "docs/governance/authority-policy.schema.json"
CHECKER_PATH = "scripts/authority_check.py"
WORKFLOW_PATH = ".github/workflows/authority.yml"
# Repository files read to reach the verdict; the checker imports only the standard library.
CHECKER_DEPENDENCIES = (POLICY_PATH, SCHEMA_PATH)
# Files whose Git blob IDs the job summary records (pre-merge verification, step 1).
AUTHORITY_FILES = (WORKFLOW_PATH, CHECKER_PATH, SCHEMA_PATH, POLICY_PATH)

RISKS = ("R0", "R1", "R2", "R3")
ROLES = ("governance-owner", "firmware-owner", "release-owner")
UNCLASSIFIED_FLOOR = "R3"
UNCLASSIFIED_ROLE = "governance-owner"
ROLE_EVIDENCE = {
    "firmware-owner": ("golden", "writeRanges"),
    "release-owner": ("release",),
    "governance-owner": ("change",),
}
AUTHORITY_FENCE = "nfc-authority"
REVIEW_FENCE = "nfc-review-record"
AUTHORITY_FIELDS = {"risk", "roles", "implementationOwner", "ownedPaths", "evidence"}
RECORD_FIELDS = {"head", "reviewer", "mode", "verdict", "openP0P1", "state", "addedRoles"}
REVIEW_MODES = ("other-runtime", "same-runtime-fresh-session")
REVIEW_VERDICTS = ("accept", "accept-with-changes", "reject")
RECORD_STATES = ("complete", "incomplete")
RUNTIME_MODEL = re.compile(r"[a-z0-9][a-z0-9._-]*/[A-Za-z0-9][A-Za-z0-9._:-]*")
SHA = re.compile(r"[0-9a-f]{40}")
# Placeholder values an evidence field may not hold. "to" "do" is one string, split so that the
# repository's code-marker check (scripts/polytail_check.py) does not read this data value as a marker.
PLACEHOLDERS = {"", "-", "?", "n/a", "na", "none", "tbd", "to" "do"}
FENCE = re.compile(r" {0,3}(`{3,})[ \t]*([^`\s]*)[^`]*")
SCHEMA_KEYWORDS = {
    "$schema",
    "$id",
    "$defs",
    "$comment",
    "title",
    "description",
    "type",
    "const",
    "enum",
    "properties",
    "required",
    "additionalProperties",
    "items",
    "minItems",
    "uniqueItems",
    "minLength",
    "pattern",
    "minimum",
    "$ref",
}
PER_PAGE = 100
MAX_PAGES = 50
API_TIMEOUT_SECONDS = 30.0
GIT_TIMEOUT_SECONDS = 120
# `git diff -l`: 0 means no limit on the exhaustive rename and copy search.
COPY_DETECTION_LIMIT = 0


class AuthorityError(Exception):
    """An input the check cannot trust: the check fails closed."""


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise AuthorityError(message)


def _unique_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    value: dict[str, Any] = {}
    for key, item in pairs:
        _require(key not in value, f"duplicate JSON key {key!r}")
        value[key] = item
    return value


def _reject_constant(value: str) -> Any:
    raise AuthorityError(f"non-standard JSON constant {value}")


def strict_json(data: bytes | str, label: str) -> Any:
    try:
        text = data.decode("utf-8") if isinstance(data, bytes) else data
        return json.loads(text, object_pairs_hook=_unique_object, parse_constant=_reject_constant)
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise AuthorityError(f"{label} is not valid JSON: {error}") from error
    except AuthorityError as error:
        raise AuthorityError(f"{label} is not valid JSON: {error}") from error


def _risk_index(risk: str) -> int:
    return RISKS.index(risk)


def _text(value: object) -> bool:
    """A well-formed evidence or identity entry: non-empty and not a template placeholder."""

    if not isinstance(value, str):
        return False
    stripped = value.strip()
    return stripped.casefold() not in PLACEHOLDERS and re.fullmatch(r"<[^>]*>", stripped) is None


# --- JSON Schema subset -----------------------------------------------------------------


def _json_type(value: object, name: str) -> bool:
    checks = {
        "object": lambda: isinstance(value, dict),
        "array": lambda: isinstance(value, list),
        "string": lambda: isinstance(value, str),
        "integer": lambda: isinstance(value, int) and not isinstance(value, bool),
        "number": lambda: isinstance(value, (int, float)) and not isinstance(value, bool),
        "boolean": lambda: isinstance(value, bool),
        "null": lambda: value is None,
    }
    _require(name in checks, f"schema uses unsupported type {name!r}")
    return checks[name]()


def _canonical(value: object) -> str:
    return json.dumps(value, sort_keys=True, separators=(",", ":"))


def schema_errors(
    instance: object, schema: object, root: Mapping[str, Any], where: str = "$"
) -> list[str]:
    """Validate the JSON Schema 2020-12 subset the policy schema uses; other keywords fail."""

    _require(isinstance(schema, dict), f"schema node at {where} is not an object")
    unknown = set(schema) - SCHEMA_KEYWORDS
    _require(not unknown, f"schema uses unsupported keywords {sorted(unknown)}")
    if "$ref" in schema:
        _require(
            set(schema) <= {"$ref", "description", "$comment", "title"},
            f"schema $ref at {where} has siblings",
        )
        reference = schema["$ref"]
        _require(
            isinstance(reference, str) and reference.startswith("#/$defs/"),
            f"unsupported $ref {reference!r}",
        )
        target = root.get("$defs", {}).get(reference.removeprefix("#/$defs/"))
        _require(isinstance(target, dict), f"unresolved $ref {reference!r}")
        return schema_errors(instance, target, root, where)
    kind = schema.get("type")
    if kind is not None:
        kinds = kind if isinstance(kind, list) else [kind]
        if not any(_json_type(instance, name) for name in kinds):
            return [f"{where}: expected {kind}"]
    errors: list[str] = []
    if "const" in schema and _canonical(instance) != _canonical(schema["const"]):
        errors.append(f"{where}: must equal {schema['const']!r}")
    if "enum" in schema and _canonical(instance) not in {
        _canonical(item) for item in schema["enum"]
    }:
        errors.append(f"{where}: must be one of {schema['enum']!r}")
    if isinstance(instance, str):
        if len(instance) < schema.get("minLength", 0):
            errors.append(f"{where}: shorter than {schema['minLength']}")
        if "pattern" in schema and re.search(schema["pattern"], instance) is None:
            errors.append(f"{where}: does not match {schema['pattern']}")
    if _json_type(instance, "integer") and "minimum" in schema and instance < schema["minimum"]:
        errors.append(f"{where}: below {schema['minimum']}")
    if isinstance(instance, list):
        if len(instance) < schema.get("minItems", 0):
            errors.append(f"{where}: fewer than {schema['minItems']} items")
        if schema.get("uniqueItems") and len({_canonical(item) for item in instance}) != len(
            instance
        ):
            errors.append(f"{where}: items are not unique")
        if "items" in schema:
            for index, item in enumerate(instance):
                errors.extend(schema_errors(item, schema["items"], root, f"{where}[{index}]"))
    if isinstance(instance, dict):
        properties = schema.get("properties", {})
        errors.extend(
            f"{where}: missing {name!r}"
            for name in schema.get("required", [])
            if name not in instance
        )
        additional = schema.get("additionalProperties", True)
        _require(additional in (True, False), "schema additionalProperties must be a boolean")
        for name, item in instance.items():
            if name in properties:
                errors.extend(schema_errors(item, properties[name], root, f"{where}.{name}"))
            elif additional is False:
                errors.append(f"{where}: unexpected property {name!r}")
    return errors


# --- Policy -----------------------------------------------------------------------------


def compile_pattern(pattern: str) -> re.Pattern[str]:
    """Compile a repository path glob: `*` and `?` stay in one segment, `**` spans segments."""

    segments = pattern.split("/")
    parts: list[str] = []
    for index, segment in enumerate(segments):
        last = index == len(segments) - 1
        if segment == "**":
            parts.append("[^/]+(?:/[^/]+)*" if last else "(?:[^/]+/)*")
            continue
        _require(
            segment not in {"", ".", ".."} and "**" not in segment,
            f"invalid path pattern {pattern!r}",
        )
        body = "".join(
            "[^/]*" if char == "*" else "[^/]" if char == "?" else re.escape(char)
            for char in segment
        )
        parts.append(body if last else body + "/")
    # Case-insensitive: a path that differs only in case from a mapped path is over-classified.
    return re.compile("".join(parts), re.IGNORECASE)


@dataclass(frozen=True)
class Entry:
    id: str
    floor: str
    roles: frozenset[str]
    patterns: tuple[re.Pattern[str], ...]
    default: bool
    excludes: tuple[re.Pattern[str], ...]

    def matches(self, path: str, *, case_sensitive: bool = False) -> bool:
        def matches_pattern(pattern: re.Pattern[str]) -> bool:
            match = re.fullmatch(pattern.pattern, path) if case_sensitive else pattern.fullmatch(path)
            return match is not None

        if not any(matches_pattern(pattern) for pattern in self.patterns):
            return False
        return not (self.default and any(matches_pattern(pattern) for pattern in self.excludes))


@dataclass(frozen=True)
class Classification:
    floor: str
    roles: frozenset[str]
    unclassified: bool


@dataclass(frozen=True)
class Policy:
    entries: tuple[Entry, ...]
    principals: Mapping[str, frozenset[tuple[int, str]]]
    reviewers: frozenset[tuple[int, str]]

    def classify(self, path: str, *, case_sensitive: bool = False) -> Classification:
        matched = [
            entry for entry in self.entries if entry.matches(path, case_sensitive=case_sensitive)
        ]
        if not matched:
            return Classification(UNCLASSIFIED_FLOOR, frozenset({UNCLASSIFIED_ROLE}), True)
        floor = max((entry.floor for entry in matched), key=_risk_index)
        return Classification(floor, frozenset().union(*(entry.roles for entry in matched)), False)


def _principals(values: list[dict[str, Any]], label: str) -> frozenset[tuple[int, str]]:
    ids = [value["id"] for value in values]
    logins = [value["login"].casefold() for value in values]
    _require(
        len(set(ids)) == len(ids) and len(set(logins)) == len(logins),
        f"{label} repeats a principal",
    )
    return frozenset(zip(ids, logins, strict=True))


def load_policy(policy_bytes: bytes, schema_bytes: bytes, label: str) -> Policy:
    schema = strict_json(schema_bytes, f"the {label} policy schema")
    policy = strict_json(policy_bytes, f"the {label} policy")
    _require(isinstance(schema, dict), f"the {label} policy schema is not an object")
    violations = schema_errors(policy, schema, schema)
    _require(
        not violations, f"the {label} policy violates its schema: " + "; ".join(violations[:10])
    )
    entries: list[Entry] = []
    for value in policy["entries"]:
        default = value.get("default", False)
        _require(
            default or "excludes" not in value,
            f"the {label} policy entry {value['id']!r} excludes paths but is not a default entry",
        )
        entries.append(
            Entry(
                value["id"],
                value["floor"],
                frozenset(value["roles"]),
                tuple(compile_pattern(item) for item in value["patterns"]),
                default,
                tuple(compile_pattern(item) for item in value.get("excludes", [])),
            )
        )
    ids = [entry.id for entry in entries]
    _require(len(set(ids)) == len(ids), f"the {label} policy repeats an entry id")
    principals = {
        role: _principals(policy["roles"][role], f"the {label} policy role {role}")
        for role in ROLES
    }
    used = set().union(*(entry.roles for entry in entries), {UNCLASSIFIED_ROLE})
    _require(
        all(principals[role] for role in used),
        f"the {label} policy names a role without a principal",
    )
    return Policy(
        tuple(entries),
        principals,
        _principals(policy["reviewers"], f"the {label} policy reviewers"),
    )


# --- Description and review records -----------------------------------------------------


def fenced_blocks(text: str, info: str) -> list[str | None]:
    """Return every backtick fence body whose info string is `info` (None: unterminated)."""

    lines = text.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    blocks: list[str | None] = []
    index = 0
    while index < len(lines):
        opening = FENCE.fullmatch(lines[index])
        if opening is None:
            index += 1
            continue
        closing = re.compile(r" {0,3}`{" + str(len(opening.group(1))) + r",}[ \t]*")
        end = index + 1
        while end < len(lines) and closing.fullmatch(lines[end]) is None:
            end += 1
        if opening.group(2) == info:
            blocks.append("\n".join(lines[index + 1 : end]) if end < len(lines) else None)
        index = end + 1
    return blocks


def _single_block(text: str, info: str, label: str) -> Any:
    blocks = fenced_blocks(text, info)
    _require(blocks, f"{label} has no `{info}` block")
    _require(len(blocks) == 1, f"{label} has {len(blocks)} `{info}` blocks; exactly one is allowed")
    _require(blocks[0] is not None, f"{label} has an unterminated `{info}` block")
    return strict_json(blocks[0], f"the `{info}` block of {label}")


def _role_list(value: object, label: str) -> frozenset[str]:
    _require(
        isinstance(value, list) and all(item in ROLES for item in value),
        f"{label} must list roles from {ROLES}",
    )
    _require(len(set(value)) == len(value), f"{label} repeats a role")
    return frozenset(value)


@dataclass(frozen=True)
class AuthorityBlock:
    risk: str
    roles: frozenset[str]
    evidence: Mapping[str, Mapping[str, Any]]
    implementation_owner: str


def parse_authority_block(description: str) -> AuthorityBlock:
    block = _single_block(description, AUTHORITY_FENCE, "the pull request description")
    _require(
        isinstance(block, dict) and set(block) == AUTHORITY_FIELDS,
        f"the authority block must have exactly the fields {sorted(AUTHORITY_FIELDS)}",
    )
    _require(block["risk"] in RISKS, f"the authority block risk must be one of {RISKS}")
    roles = _role_list(block["roles"], "the authority block roles")
    _require(
        isinstance(block["implementationOwner"], str)
        and RUNTIME_MODEL.fullmatch(block["implementationOwner"]) is not None,
        "implementationOwner must be a runtime/model identifier",
    )
    owned = block["ownedPaths"]
    _require(
        isinstance(owned, list) and owned and all(_text(item) for item in owned),
        "the authority block needs ownedPaths",
    )
    evidence = block["evidence"]
    _require(
        isinstance(evidence, dict) and all(isinstance(value, dict) for value in evidence.values()),
        "the authority block evidence must map each role to an object",
    )
    undeclared = sorted(set(evidence) - roles)
    _require(not undeclared, f"the authority block has evidence for undeclared roles {undeclared}")
    return AuthorityBlock(block["risk"], roles, evidence, block["implementationOwner"])


@dataclass(frozen=True)
class Review:
    id: int
    login: str
    user_id: int
    state: str
    commit_id: str | None
    body: str
    submitted_at: str


@dataclass(frozen=True)
class ReviewRecord:
    head: str
    reviewer: str
    mode: str
    verdict: str
    open_p0_p1: int
    state: str
    added_roles: frozenset[str]


def parse_review_record(body: str) -> ReviewRecord:
    record = _single_block(body, REVIEW_FENCE, "the review")
    _require(
        isinstance(record, dict) and set(record) == RECORD_FIELDS,
        f"the review record must have exactly the fields {sorted(RECORD_FIELDS)}",
    )
    _require(
        isinstance(record["head"], str) and SHA.fullmatch(record["head"]) is not None,
        "the review record head is not a full lowercase SHA",
    )
    _require(
        isinstance(record["reviewer"], str)
        and RUNTIME_MODEL.fullmatch(record["reviewer"]) is not None,
        "the review record reviewer is not a runtime/model identifier",
    )
    _require(
        record["mode"] in REVIEW_MODES, f"the review record mode must be one of {REVIEW_MODES}"
    )
    _require(
        record["verdict"] in REVIEW_VERDICTS,
        f"the review record verdict must be one of {REVIEW_VERDICTS}",
    )
    count = record["openP0P1"]
    _require(
        isinstance(count, int) and not isinstance(count, bool) and count >= 0,
        "the review record openP0P1 must be a non-negative integer",
    )
    _require(
        record["state"] in RECORD_STATES, f"the review record state must be one of {RECORD_STATES}"
    )
    roles = _role_list(record["addedRoles"], "the review record addedRoles")
    return ReviewRecord(
        record["head"],
        record["reviewer"],
        record["mode"],
        record["verdict"],
        count,
        record["state"],
        roles,
    )


def _record_problem(review: Review, head: str) -> tuple[ReviewRecord | None, str | None]:
    """Parse a principal's review on the head: the record (if parseable), why it does not count."""

    try:
        record = parse_review_record(review.body)
    except AuthorityError as error:
        return None, str(error)
    if record.head != head:
        return None, "its block names another head"
    if review.state != "COMMENTED":
        return record, f"it is a {review.state} review, not a comment review"
    if record.verdict == "reject":
        return record, "its verdict rejects"
    if record.state != "complete":
        return record, "it is incomplete"
    if record.open_p0_p1:
        return record, f"it has {record.open_p0_p1} open P0/P1 findings"
    return record, None


# --- Evaluation -------------------------------------------------------------------------


@dataclass(frozen=True)
class Change:
    status: str
    paths: tuple[str, ...]


@dataclass(frozen=True)
class CheckInputs:
    head_sha: str
    changes: tuple[Change, ...]
    head_policy: bytes | None
    head_schema: bytes | None
    base_policy: bytes | None
    base_schema: bytes | None
    description: str
    reviews: tuple[Review, ...]


@dataclass
class Verdict:
    errors: list[str] = field(default_factory=list)
    floor: str | None = None
    required_roles: frozenset[str] = frozenset()
    declared_risk: str | None = None
    declared_roles: frozenset[str] = frozenset()
    unclassified: tuple[str, ...] = ()
    valid_records: tuple[str, ...] = ()
    paths: tuple[tuple[str, Classification], ...] = ()

    @property
    def passed(self) -> bool:
        return not self.errors


def _load_policies(inputs: CheckInputs, verdict: Verdict) -> list[Policy]:
    policies: list[Policy] = []
    added = {change.paths[-1] for change in inputs.changes if change.status in {"A", "C", "R"}}
    for label, policy, schema in (
        ("head", inputs.head_policy, inputs.head_schema),
        ("base", inputs.base_policy, inputs.base_schema),
    ):
        if policy is None:
            if label == "base" and POLICY_PATH in added:
                continue  # First introduction: the pull request adds the policy (bootstrap).
            verdict.errors.append(f"the {label} has no authority policy at {POLICY_PATH}")
            continue
        if schema is None:
            verdict.errors.append(f"the {label} has no policy schema at {SCHEMA_PATH}")
            continue
        try:
            policies.append(load_policy(policy, schema, label))
        except AuthorityError as error:
            verdict.errors.append(str(error))
    return policies if not verdict.errors else []


def _evidence_errors(block: AuthorityBlock, unclassified: Sequence[str]) -> list[str]:
    errors: list[str] = []
    for role in sorted(block.roles):
        entries = block.evidence.get(role)
        if not entries:
            errors.append(
                f"role {role} is declared without its evidence entries {list(ROLE_EVIDENCE[role])}"
            )
            continue
        allowed = set(ROLE_EVIDENCE[role]) | (
            {"classification"} if role == UNCLASSIFIED_ROLE else set()
        )
        extra = sorted(set(entries) - allowed)
        if extra:
            errors.append(f"role {role} has unknown evidence entries {extra}")
        missing = [name for name in ROLE_EVIDENCE[role] if not _text(entries.get(name))]
        if missing:
            errors.append(f"role {role} lacks well-formed evidence entries {missing}")
    classification = block.evidence.get(UNCLASSIFIED_ROLE, {}).get("classification", {})
    if not isinstance(classification, dict):
        return errors + [
            "the governance-owner classification must map each unclassified path to its proposal"
        ]
    for path in unclassified:
        proposal = classification.get(path)
        if not (
            isinstance(proposal, dict)
            and set(proposal) == {"floor", "roles"}
            and proposal["floor"] in RISKS
            and isinstance(proposal["roles"], list)
            and all(role in ROLES for role in proposal["roles"])
        ):
            errors.append(
                f"unclassified path {path} has no governance-owner classification {{floor, roles}}"
            )
    return errors


def evaluate(inputs: CheckInputs) -> Verdict:
    """Judge one pull request from collected inputs; every error fails the check."""

    verdict = Verdict()
    policies = _load_policies(inputs, verdict)
    if not policies:
        return verdict
    rows: list[tuple[str, Classification]] = []
    for path in sorted({path for change in inputs.changes for path in change.paths}):
        results = [policy.classify(path) for policy in policies]
        floor = max((result.floor for result in results), key=_risk_index)
        roles = frozenset().union(*(result.roles for result in results))
        rows.append(
            (path, Classification(floor, roles, any(result.unclassified for result in results)))
        )
    verdict.paths = tuple(rows)
    verdict.unclassified = tuple(path for path, result in rows if result.unclassified)

    block: AuthorityBlock | None = None
    try:
        block = parse_authority_block(inputs.description)
    except AuthorityError as error:
        verdict.errors.append(str(error))

    reviewers = frozenset.intersection(*(policy.reviewers for policy in policies))
    by_principal: dict[tuple[int, str], list[Review]] = {}
    for review in inputs.reviews:
        principal = (review.user_id, review.login.casefold())
        if (
            principal in reviewers
            and review.commit_id == inputs.head_sha
            and fenced_blocks(review.body, REVIEW_FENCE)
        ):
            by_principal.setdefault(principal, []).append(review)
    added_roles: set[str] = set()
    record_notes: list[str] = []
    valid: list[str] = []
    for _, reviews in sorted(by_principal.items()):
        for review in reviews:
            record, _ = _record_problem(review, inputs.head_sha)
            if record is not None:
                added_roles |= record.added_roles
        latest = max(reviews, key=lambda review: (review.submitted_at, review.id))
        record, problem = _record_problem(latest, inputs.head_sha)
        if problem is None and record is not None and block is not None:
            author_runtime = block.implementation_owner.split("/", 1)[0].casefold()
            reviewer_runtime = record.reviewer.split("/", 1)[0].casefold()
            if reviewer_runtime == author_runtime and record.mode != "same-runtime-fresh-session":
                problem = "same runtime requires same-runtime-fresh-session review mode"
            elif reviewer_runtime != author_runtime and record.mode != "other-runtime":
                problem = "different runtime requires other-runtime review mode"
        if problem is None and record is not None:
            valid.append(f"{latest.login} review {latest.id} ({record.reviewer}, {record.verdict})")
        else:
            record_notes.append(
                f"{latest.login}: latest record (review {latest.id}) does not count: {problem}"
            )
    verdict.valid_records = tuple(valid)

    proposal_roles: set[str] = set()
    if block is not None:
        proposals = block.evidence.get(UNCLASSIFIED_ROLE, {}).get("classification", {})
        if isinstance(proposals, dict):
            for path in verdict.unclassified:
                proposal = proposals.get(path)
                if isinstance(proposal, dict) and isinstance(proposal.get("roles"), list):
                    proposal_roles |= {role for role in proposal["roles"] if role in ROLES}
    required = frozenset().union(*(result.roles for _, result in rows), added_roles, proposal_roles)
    floor = max((result.floor for _, result in rows), key=_risk_index, default="R0")
    declared_roles = block.roles if block is not None else frozenset()
    if required or declared_roles:
        floor = "R3"  # A role is a named R3 approval (ADR 0080 items 4 and 7).
    verdict.floor, verdict.required_roles = floor, required

    if block is not None:
        verdict.declared_risk, verdict.declared_roles = block.risk, block.roles
        if _risk_index(block.risk) < _risk_index(floor):
            verdict.errors.append(f"the declared risk {block.risk} is below the floor {floor}")
        missing = sorted(required - block.roles)
        if missing:
            sources = [path for path, result in rows if result.roles & set(missing)]
            detail = (
                f" (paths: {', '.join(sources[:10])})"
                if sources
                else " (added by a review record or classification)"
            )
            verdict.errors.append(f"required roles are not declared: {missing}{detail}")
        verdict.errors.extend(_evidence_errors(block, verdict.unclassified))
        github_paths = {
            path
            for change in inputs.changes
            for path in (change.paths[-1:] if change.status in {"C", "R"} else change.paths)
        }

        def code_owned(path: str) -> bool:
            return any(
                entry.floor == "R3" and entry.matches(path, case_sensitive=True)
                for policy in policies
                for entry in policy.entries
            )

        owned_renames_to_unowned = [
            change
            for change in inputs.changes
            if change.status == "R" and code_owned(change.paths[0])
            and not code_owned(change.paths[-1])
        ]
        for change in owned_renames_to_unowned:
            verdict.errors.append(
                f"owned-path rename {change.paths[0]} -> {change.paths[-1]} has no "
                "code-owned destination; obtain owner approval or split the rename into "
                "deletion and addition so CODEOWNERS can require review, then rerun "
                "the authority check"
            )
        for path in sorted(github_paths):
            insensitive_owned = any(
                entry.floor == "R3" and entry.matches(path)
                for policy in policies
                for entry in policy.entries
            )
            sensitive_owned = any(
                entry.floor == "R3" and entry.matches(path, case_sensitive=True)
                for policy in policies
                for entry in policy.entries
            )
            if insensitive_owned and not sensitive_owned:
                verdict.errors.append(
                    f"path {path} matches an R3 policy pattern only without case sensitivity; "
                    "case-sensitive CODEOWNERS cannot request owner review"
                )
        if block.risk == "R3" and not any(map(code_owned, github_paths)):
            verdict.errors.append(
                "declared R3 risk has no code-owned path; update the policy "
                "and CODEOWNERS, obtain owner approval on the exact head, "
                "then rerun the authority check"
            )
    effective = max(floor, block.risk if block is not None else "R0", key=_risk_index)
    if not valid:
        detail = "; ".join(record_notes) or "no listed reviewer posted a record on this head"
        verdict.errors.append(
            f"an {effective} change needs a valid independent review record on head {inputs.head_sha}: {detail}"
        )
    return verdict


# --- Git --------------------------------------------------------------------------------


class Git:
    """Read-only Git access to one work tree."""

    def __init__(self, root: Path) -> None:
        self.root = root

    def run(self, *arguments: str) -> bytes:
        return self.run_with_warnings(*arguments)[0]

    def run_with_warnings(self, *arguments: str) -> tuple[bytes, str]:
        """Run a read-only Git command; return its output and what it wrote to stderr."""

        try:
            result = subprocess.run(
                ["git", "--literal-pathspecs", "-c", "core.quotepath=off", *arguments],
                cwd=self.root,
                capture_output=True,
                check=False,
                timeout=GIT_TIMEOUT_SECONDS,
            )
        except (OSError, subprocess.TimeoutExpired) as error:
            raise AuthorityError(f"git {arguments[0]} failed: {error}") from error
        if result.returncode != 0:
            detail = result.stderr.decode("utf-8", errors="replace").strip()
            raise AuthorityError(
                f"git {' '.join(arguments[:2])} failed: {detail or result.returncode}"
            )
        return result.stdout, result.stderr.decode("utf-8", errors="replace")

    def commit(self, revision: str) -> str:
        return (
            self.run("rev-parse", "--verify", "--end-of-options", f"{revision}^{{commit}}")
            .decode()
            .strip()
        )

    def merge_base(self, first: str, second: str) -> str:
        return self.run("merge-base", first, second).decode().strip()

    def changes(self, base: str, head: str) -> tuple[Change, ...]:
        # Copies from unchanged sources count too (ADR 0080 item 4: both sides of a copy), so
        # every base file is a copy candidate and the exhaustive search is never cut short.
        # A submodule's recorded commit is a changed path too: no "ignore" setting may hide it,
        # whether Git takes it from its configuration or from the checkout's .gitmodules.
        output, warnings = self.run_with_warnings(
            "diff",
            "--name-status",
            "-z",
            "--find-renames",
            "--find-copies-harder",
            f"-l{COPY_DETECTION_LIMIT}",
            "--no-ext-diff",
            "--ignore-submodules=none",
            base,
            head,
            "--",
        )
        _require(
            "too many files" not in warnings and "renamelimit" not in warnings.casefold(),
            f"git skipped part of the rename and copy detection: {warnings.strip()}",
        )
        values = [value.decode("utf-8") for value in output.split(b"\0") if value]
        changes: list[Change] = []
        index = 0
        while index < len(values):
            status = values[index][:1]
            _require(
                status in set("ACDMRT"),
                f"git reported an unsupported change status {values[index]!r}",
            )
            count = 2 if status in {"R", "C"} else 1
            _require(index + 1 + count <= len(values), "git reported a truncated change list")
            changes.append(Change(status, tuple(values[index + 1 : index + 1 + count])))
            index += 1 + count
        return tuple(changes)

    def blob(self, commit: str, path: str) -> tuple[str, bytes] | None:
        listing = self.run("ls-tree", "-z", "--full-tree", commit, "--", path)
        entries = [entry for entry in listing.split(b"\0") if entry]
        if not entries:
            return None
        meta, _, name = entries[0].partition(b"\t")
        mode, kind, object_id = meta.decode().split(" ")
        _require(
            len(entries) == 1 and name.decode("utf-8") == path and kind == "blob",
            f"{path} at {commit} is not one file",
        )
        return object_id, self.run("cat-file", "blob", object_id)


# --- GitHub -----------------------------------------------------------------------------


@dataclass(frozen=True)
class HttpResponse:
    status: int
    headers: Mapping[str, str]
    body: bytes


Transport = Callable[[str, Mapping[str, str], float], HttpResponse]


class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *arguments: Any, **keywords: Any) -> None:
        return None  # Never forward the token to another location.


def urllib_transport(url: str, headers: Mapping[str, str], timeout: float) -> HttpResponse:
    opener = urllib.request.build_opener(_NoRedirect)
    request = urllib.request.Request(url, headers=dict(headers), method="GET")
    try:
        with opener.open(request, timeout=timeout) as response:
            return HttpResponse(
                response.status,
                {key.lower(): value for key, value in response.headers.items()},
                response.read(),
            )
    except urllib.error.HTTPError as error:
        return HttpResponse(
            error.code, {key.lower(): value for key, value in error.headers.items()}, error.read()
        )
    except (urllib.error.URLError, OSError, http.client.HTTPException) as error:
        raise AuthorityError(f"GitHub API request failed or timed out: {url}: {error}") from error


class GitHubApi:
    """Read-only REST access with complete pagination."""

    def __init__(
        self,
        repository: str,
        *,
        token: str | None,
        api_url: str,
        transport: Transport = urllib_transport,
    ) -> None:
        _require(
            re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository) is not None,
            f"invalid repository {repository!r}",
        )
        self.repository = repository
        self.api_url = api_url.rstrip("/")
        self.transport = transport
        self.headers = {
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": "2022-11-28",
            "User-Agent": "nfc-authority-check",
        }
        if token:
            self.headers["Authorization"] = f"Bearer {token}"

    def _get(self, url: str, label: str) -> tuple[Any, Mapping[str, str]]:
        response = self.transport(url, self.headers, API_TIMEOUT_SECONDS)
        limited = response.status == 429 or (
            response.status == 403
            and (
                response.headers.get("x-ratelimit-remaining") == "0"
                or "retry-after" in response.headers
            )
        )
        _require(not limited, f"GitHub API rate limit reached while reading {label}")
        _require(
            response.status == 200,
            f"GitHub API returned HTTP {response.status} while reading {label}",
        )
        return strict_json(response.body, label), response.headers

    def _url(self, path: str) -> str:
        return f"{self.api_url}/repos/{self.repository}/{path}"

    def _paginated(self, path: str, label: str) -> list[Any]:
        url = f"{self._url(path)}?per_page={PER_PAGE}"
        items: list[Any] = []
        for _ in range(MAX_PAGES):
            page, headers = self._get(url, label)
            _require(isinstance(page, list), f"{label} page is not a list")
            items.extend(page)
            links = dict(
                (rel, target)
                for target, rel in re.findall(
                    r'<([^>]+)>\s*;\s*rel="([^"]+)"', headers.get("link", "")
                )
            )
            if "next" not in links:
                return items
            _require(
                len(page) == PER_PAGE, f"{label} returned an incomplete page before its last page"
            )
            _require(
                links["next"].startswith(self.api_url + "/"),
                f"{label} pagination left {self.api_url}",
            )
            url = links["next"]
        raise AuthorityError(f"{label} exceeded {MAX_PAGES} pages")

    def pull_request(self, number: int) -> dict[str, Any]:
        pull, _ = self._get(self._url(f"pulls/{number}"), "the pull request")
        _require(
            isinstance(pull, dict) and pull.get("number") == number,
            "the pull request response is malformed",
        )
        head, base = pull.get("head"), pull.get("base")
        _require(
            isinstance(head, dict)
            and isinstance(head.get("sha"), str)
            and SHA.fullmatch(head["sha"]) is not None,
            "the pull request head is malformed",
        )
        _require(
            isinstance(base, dict) and isinstance(base.get("ref"), str) and base["ref"],
            "the pull request base is malformed",
        )
        repo = base.get("repo")
        _require(
            isinstance(repo, dict)
            and str(repo.get("full_name", "")).casefold() == self.repository.casefold(),
            "the pull request base is in another repository",
        )
        _require(
            pull.get("body") is None or isinstance(pull["body"], str),
            "the pull request description is malformed",
        )
        return pull

    def branch_head(self, branch: str) -> str:
        ref, _ = self._get(
            self._url(f"git/ref/heads/{urllib.parse.quote(branch, safe='/')}"), f"branch {branch}"
        )
        target = ref.get("object") if isinstance(ref, dict) else None
        _require(
            isinstance(ref, dict) and ref.get("ref") == f"refs/heads/{branch}",
            f"branch {branch} response is malformed",
        )
        _require(
            isinstance(target, dict)
            and target.get("type") == "commit"
            and isinstance(target.get("sha"), str)
            and SHA.fullmatch(target["sha"]) is not None,
            f"branch {branch} does not name a commit",
        )
        return target["sha"]

    def reviews(self, number: int) -> tuple[Review, ...]:
        reviews: list[Review] = []
        for item in self._paginated(f"pulls/{number}/reviews", "the review list"):
            _require(
                isinstance(item, dict)
                and isinstance(item.get("id"), int)
                and isinstance(item.get("state"), str),
                "the review list is malformed",
            )
            user = item.get("user")
            if item["state"] == "PENDING" or not isinstance(user, dict):
                continue  # Unsubmitted, or by a deleted account: never a record.
            _require(
                isinstance(user.get("login"), str) and isinstance(user.get("id"), int),
                "a review author is malformed",
            )
            _require(
                isinstance(item.get("submitted_at"), str)
                and (item.get("commit_id") is None or isinstance(item["commit_id"], str)),
                "a review is malformed",
            )
            reviews.append(
                Review(
                    item["id"],
                    user["login"],
                    user["id"],
                    item["state"],
                    item.get("commit_id"),
                    item.get("body") or "",
                    item["submitted_at"],
                )
            )
        return tuple(reviews)


# --- Run --------------------------------------------------------------------------------


@dataclass(frozen=True)
class CheckerIdentity:
    """The checker file that actually ran, which may differ from the evaluated head's."""

    revision: str
    path: str
    blob: str
    state: str


def executed_checker(script: Path | None = None) -> CheckerIdentity:
    """Identify the running checker by its own Git work tree, not by the evaluated head."""

    script = (script or Path(__file__)).resolve()
    git = Git(script.parent)
    try:
        path = git.run("rev-parse", "--show-prefix").decode().strip() + script.name
        revision = git.commit("HEAD")
        blob = git.run("hash-object", "--", script.name).decode().strip()
        try:
            committed = git.run("rev-parse", "--verify", f"{revision}:{path}").decode().strip()
        except AuthorityError:
            committed = None
    except AuthorityError:
        return CheckerIdentity("unknown", script.name, "unknown", "not in a Git work tree")
    state = "matches that revision" if committed == blob else "differs from that revision"
    return CheckerIdentity(revision, path, blob, state)


@dataclass
class RunContext:
    number: int
    head: str | None = None
    base_ref: str | None = None
    base: str | None = None
    merge_base: str | None = None
    head_files: dict[str, str] = field(default_factory=dict)
    base_files: dict[str, str] = field(default_factory=dict)
    checker: CheckerIdentity | None = None


def collect(git: Git, api: GitHubApi, context: RunContext) -> CheckInputs:
    pull = api.pull_request(context.number)
    context.head, context.base_ref = pull["head"]["sha"], pull["base"]["ref"]
    checked_out = git.commit("HEAD")
    _require(
        checked_out == context.head,
        f"the checked-out head {checked_out} differs from the live head {context.head}; "
        "the push that moved it starts a new run",
    )
    context.base = api.branch_head(context.base_ref)
    try:
        git.commit(context.base)
    except AuthorityError as error:
        raise AuthorityError(
            f"the live base tip {context.base} is not in this clone "
            f"(the base moved after checkout; re-run): {error}"
        ) from error
    context.merge_base = git.merge_base(context.base, context.head)
    changes = git.changes(context.merge_base, context.head)
    files: dict[str, dict[str, tuple[str, bytes] | None]] = {"head": {}, "base": {}}
    for label, commit in (("head", context.head), ("base", context.base)):
        for path in AUTHORITY_FILES:
            files[label][path] = git.blob(commit, path)
    context.head_files = {path: value[0] for path, value in files["head"].items() if value}
    context.base_files = {path: value[0] for path, value in files["base"].items() if value}

    def content(label: str, path: str) -> bytes | None:
        value = files[label][path]
        return value[1] if value else None

    return CheckInputs(
        context.head,
        changes,
        content("head", POLICY_PATH),
        content("head", SCHEMA_PATH),
        content("base", POLICY_PATH),
        content("base", SCHEMA_PATH),
        pull.get("body") or "",
        api.reviews(context.number),
    )


def render_summary(context: RunContext, verdict: Verdict | None, errors: Sequence[str]) -> str:
    lines = ["## governance / authority", "", f"- Result: **{'PASS' if not errors else 'FAIL'}**"]
    lines.append(
        f"- Evaluated pull request #{context.number}: head `{context.head}`, "
        f"base `{context.base_ref}` at `{context.base}`, merge base `{context.merge_base}`"
    )
    checker = context.checker
    if checker is not None:
        own = checker.revision == context.head and checker.blob == context.head_files.get(
            CHECKER_PATH
        )
        origin = "the evaluated head's own checker" if own else "not the evaluated head's checker"
        lines.append(
            f"- Checker that ran: `{checker.path}` blob `{checker.blob}` from revision "
            f"`{checker.revision}` (the file {checker.state}; {origin})"
        )
    run = {
        name: os.environ.get(name)
        for name in (
            "GITHUB_RUN_ID",
            "GITHUB_RUN_ATTEMPT",
            "GITHUB_SHA",
            "GITHUB_WORKFLOW_REF",
            "GITHUB_WORKFLOW_SHA",
        )
    }
    if any(run.values()):
        lines.append(
            "- Workflow run that executed it: "
            + ", ".join(f"{name} `{value}`" for name, value in run.items() if value)
        )
    lines += [
        "",
        "| Authority file | Blob at evaluated head | Blob at base tip |",
        "| --- | --- | --- |",
    ]
    for path in AUTHORITY_FILES:
        head, base = context.head_files.get(path, "absent"), context.base_files.get(path, "absent")
        lines.append(f"| `{path}` | `{head}` | `{base}` |")
    if verdict is not None and verdict.floor is not None:
        lines += [
            "",
            f"- Floor: {verdict.floor}; required roles: {sorted(verdict.required_roles) or 'none'}",
            f"- Declared risk: {verdict.declared_risk}; "
            f"declared roles: {sorted(verdict.declared_roles) or 'none'}",
            f"- Valid review records on the head: {'; '.join(verdict.valid_records) or 'none'}",
            f"- Unclassified paths: {', '.join(verdict.unclassified) or 'none'}",
        ]
        counts = ", ".join(
            f"{risk}: {sum(1 for _, result in verdict.paths if result.floor == risk)}"
            for risk in RISKS
        )
        lines.append(f"- Changed paths: {len(verdict.paths)} ({counts})")
        roled = [(path, result) for path, result in verdict.paths if result.roles]
        if roled:
            lines += ["", "| Path with roles | Floor | Roles |", "| --- | --- | --- |"]
            lines += [
                f"| `{path}` | {result.floor} | {', '.join(sorted(result.roles))} |"
                for path, result in roled[:200]
            ]
    if errors:
        lines += ["", "### Failures", ""] + [f"- {error}" for error in errors]
    return "\n".join(lines) + "\n"


def run_check(
    git: Git, api: GitHubApi, number: int
) -> tuple[RunContext, Verdict | None, list[str]]:
    context = RunContext(number)
    try:
        verdict = evaluate(collect(git, api, context))
        return context, verdict, list(verdict.errors)
    except AuthorityError as error:
        return context, None, [str(error)]


def parse_args(argv: Sequence[str] | None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument(
        "--repository", required=True, help="owner/name of the pull request's repository"
    )
    parser.add_argument("--pull-request", type=int, required=True, help="pull request number")
    parser.add_argument(
        "--root",
        type=Path,
        default=Path.cwd(),
        help="checkout of the pull request head (default: current directory)",
    )
    parser.add_argument("--summary", type=Path, help="Markdown file to append the job summary to")
    parser.add_argument(
        "--api-url", default=os.environ.get("GITHUB_API_URL", "https://api.github.com")
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None, transport: Transport = urllib_transport) -> int:
    arguments = parse_args(argv)
    context, verdict, errors = RunContext(arguments.pull_request), None, ["the check did not run"]
    checker = executed_checker()
    try:
        root = Path(Git(arguments.root).run("rev-parse", "--show-toplevel").decode().strip())
        token = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
        api = GitHubApi(
            arguments.repository, token=token, api_url=arguments.api_url, transport=transport
        )
        context, verdict, errors = run_check(Git(root), api, arguments.pull_request)
    except AuthorityError as error:
        errors = [str(error)]
    except Exception as error:  # noqa: BLE001 - any unexpected failure fails the check closed.
        errors = [f"unexpected {type(error).__name__}: {error}"]
    context.checker = checker
    summary = render_summary(context, verdict, errors)
    print(summary)
    if verdict is not None:
        for path, result in verdict.paths:
            roles = ",".join(sorted(result.roles)) or "-"
            print(f"{result.floor} {roles} {'unclassified ' if result.unclassified else ''}{path}")
    if arguments.summary is not None:
        try:
            with arguments.summary.open("a", encoding="utf-8") as stream:
                stream.write(summary)
        except OSError as error:
            print(f"the job summary could not be written: {error}", file=sys.stderr)
            return 1
    return 0 if not errors else 1


if __name__ == "__main__":
    raise SystemExit(main())

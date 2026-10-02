"""Behavioral tests for the `governance / authority` check (ADR 0080, G1-A).

Every negative case of ADR 0080's G1-A acceptance list must fail the check. The
GitHub responses are recorded REST shapes; Git cases use a scratch repository.
"""

from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from typing import Any
from unittest import mock

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "authority_check", ROOT / "scripts" / "authority_check.py"
)
assert SPEC is not None and SPEC.loader is not None
check = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = check
SPEC.loader.exec_module(check)

POLICY = (ROOT / check.POLICY_PATH).read_bytes()
SCHEMA = (ROOT / check.SCHEMA_PATH).read_bytes()
HEAD = "a" * 40
OLDER = "b" * 40
BOT = ("nfc-agent-dennis40816[bot]", 334370883)
OWNER = ("Dennis40816", 146855708)
OUTSIDER = ("octo-outsider", 4242)
REPOSITORY = "Dennis40816/nvt_fw_combiner"
API = "https://api.github.test"
EVIDENCE = {
    "governance-owner": {"change": "Adds the authority check (ADR 0080 item 4)."},
    "release-owner": {"release": "New workflow reviewed; release-owner attestation follows."},
    "firmware-owner": {
        "golden": "Golden cases 1-12 pass byte-exact.",
        "writeRanges": "Write ranges audited: none changed.",
    },
}


def description(**fields: Any) -> str:
    roles = fields.get("roles", [])
    value = {
        "risk": "R1",
        "roles": roles,
        "implementationOwner": "claude-code/sonnet",
        "ownedPaths": ["src/NvtFwCombiner.Cli/"],
        "evidence": {role: EVIDENCE[role] for role in roles},
    }
    value.update(fields)
    return f"## Outcome\n\nText.\n\n```nfc-authority\n{json.dumps(value, indent=2)}\n```\n"


def record_body(**fields: Any) -> str:
    value = {
        "head": HEAD,
        "reviewer": "codex/gpt-6-astra",
        "mode": "other-runtime",
        "verdict": "accept",
        "openP0P1": 0,
        "state": "complete",
        "addedRoles": [],
    }
    value.update(fields)
    return f"Review.\n\n```nfc-review-record\n{json.dumps(value)}\n```\n"


def review(
    principal: tuple[str, int] = BOT,
    *,
    number: int = 1,
    state: str = "COMMENTED",
    commit: str | None = HEAD,
    body: str | None = None,
    at: str = "2026-09-27T10:00:00Z",
) -> Any:
    return check.Review(
        number,
        principal[0],
        principal[1],
        state,
        commit,
        record_body() if body is None else body,
        at,
    )


def change(status: str, *paths: str) -> Any:
    return check.Change(status, paths)


def inputs(
    changes: list[Any],
    text: str,
    reviews: tuple[Any, ...] = (),
    *,
    head_policy: bytes | None = POLICY,
    base_policy: bytes | None = POLICY,
    head_schema: bytes | None = SCHEMA,
    base_schema: bytes | None = SCHEMA,
) -> Any:
    return check.CheckInputs(
        HEAD,
        tuple(changes),
        head_policy,
        head_schema,
        base_policy,
        base_schema,
        text,
        reviews,
    )


def edited_policy(edit) -> bytes:
    policy = json.loads(POLICY)
    edit(policy)
    return json.dumps(policy).encode()


def entry(policy: dict[str, Any], entry_id: str) -> dict[str, Any]:
    return next(item for item in policy["entries"] if item["id"] == entry_id)


PROFILE = "".join(
    f'  "region{index}": {{"start": {index * 16}, "end": {index * 16 + 8}}},\n'
    for index in range(24)
).encode()
CODE = change("M", "src/NvtFwCombiner.Cli/Program.cs")
FIRMWARE = change("M", "src/NvtFwCombiner.Domain/Ranges/Range.cs")
WORKFLOW = change("A", ".github/workflows/authority.yml")
PROSE = change("M", "docs/handoff/1.1.13/WS-GOV.md")


class EvaluationTests(unittest.TestCase):
    def assert_fails(self, verdict: Any, fragment: str) -> None:
        self.assertFalse(verdict.passed, "the check passed")
        self.assertTrue(any(fragment in error for error in verdict.errors), verdict.errors)

    def assert_passes(self, verdict: Any) -> None:
        self.assertTrue(verdict.passed, verdict.errors)

    # Positive cases.

    def test_complete_r3_change_passes(self) -> None:
        text = description(risk="R3", roles=["governance-owner", "release-owner"])
        verdict = check.evaluate(inputs([WORKFLOW, CODE], text, (review(),)))
        self.assert_passes(verdict)
        self.assertEqual(verdict.floor, "R3")
        self.assertEqual(verdict.required_roles, {"governance-owner", "release-owner"})

    def test_prose_only_change_passes_as_r0_with_independent_review(self) -> None:
        verdict = check.evaluate(inputs([PROSE], description(risk="R0"), (review(OWNER),)))
        self.assert_passes(verdict)
        self.assertEqual(verdict.floor, "R0")

    def test_r0_change_without_review_record_fails(self) -> None:
        self.assert_fails(
            check.evaluate(inputs([PROSE], description(risk="R0"))),
            "needs a valid independent review record",
        )

    def test_r1_change_with_independent_review_passes(self) -> None:
        self.assert_passes(check.evaluate(inputs([CODE], description(), (review(OWNER),))))

    def test_r2_without_owned_path_passes_with_independent_record(self) -> None:
        r2_path = change("M", "SECURITY.md")
        verdict = check.evaluate(inputs([r2_path], description(risk="R2"), (review(OWNER),)))
        self.assert_passes(verdict)
        self.assertEqual(verdict.floor, "R2")

    def test_r1_path_with_r3_floor_fails(self) -> None:
        self.assert_fails(
            check.evaluate(inputs([FIRMWARE], description(risk="R1"), (review(OWNER),))),
            "declared risk R1 is below the floor R3",
        )

    def test_self_reported_r2_without_code_owned_path_passes(self) -> None:
        self.assert_passes(check.evaluate(inputs([CODE], description(risk="R2"), (review(),))))

    def test_r2_without_record_still_fails(self) -> None:
        self.assert_fails(
            check.evaluate(inputs([change("M", "SECURITY.md")], description(risk="R2"))),
            "needs a valid independent review record",
        )

    def test_r2_renames_and_case_only_matches_do_not_require_ownership(self) -> None:
        for item in (
            change("R", "SECURITY.md", "docs/security-notes.md"),
            change("R", "scripts/collect_review_handoff.py", "scripts/renamed_tool.py"),
            change("M", "src/Foo/Agents.md"),
            change("M", "Security.md"),
        ):
            with self.subTest(change=item):
                verdict = check.evaluate(inputs([item], description(risk="R2"), (review(),)))
                self.assert_passes(verdict)
                self.assertEqual(verdict.floor, "R2")

    def test_r2_to_r3_rename_escalates_and_requires_role_evidence(self) -> None:
        moved = change("R", "scripts/ordinary.py", "scripts/verify.py")
        verdict = check.evaluate(inputs([moved], description(risk="R2"), (review(),)))
        self.assert_fails(verdict, "below the floor R3")
        self.assert_fails(verdict, "required roles are not declared: ['governance-owner']")
        text = description(risk="R3", roles=["governance-owner"])
        self.assert_passes(check.evaluate(inputs([moved], text, (review(),))))

    def test_ci_owner_case_only_destination_fails_even_with_an_owned_change(self) -> None:
        moved = change("R", "scripts/ordinary.py", "scripts/Verify.py")
        text = description(risk="R3", roles=["governance-owner"])
        verdict = check.evaluate(inputs([moved, change("M", check.POLICY_PATH)], text, (review(),)))
        self.assert_fails(verdict, "case-sensitive CODEOWNERS")

    def test_r3_ci_owner_rename_to_ordinary_tool_stays_blocked(self) -> None:
        moved = change("R", "scripts/verify.py", "scripts/ordinary.py")
        text = description(risk="R3", roles=["governance-owner"])
        verdict = check.evaluate(inputs([moved, change("M", check.POLICY_PATH)], text, (review(),)))
        self.assert_fails(verdict, "owner approval or split the rename into deletion and addition")

    def test_case_only_matches_of_owned_paths_fail_closed(self) -> None:
        for path in (
            "Profiles/evil.json",
            "src/NvtFwCombiner.domain/Evil.cs",
            "scripts/Verify.py",
        ):
            with self.subTest(path=path):
                text = description(risk="R3", roles=["governance-owner", "firmware-owner"])
                verdict = check.evaluate(inputs([change("A", path)], text, (review(),)))
                self.assert_fails(verdict, "case-sensitive CODEOWNERS")

    def test_unchanged_copy_source_does_not_make_r1_destination_code_owned(self) -> None:
        copied = change("C", "profiles/built-in/p.json", "docs/x.json")
        text = description(risk="R3", roles=["firmware-owner"])
        verdict = check.evaluate(inputs([copied], text, (review(),)))
        self.assert_fails(verdict, "no code-owned path")

    def test_rename_from_owned_path_to_r1_path_needs_owner_approval_or_split(self) -> None:
        moved = change("R", "profiles/built-in/p.json", "docs/x.json")
        text = description(risk="R3", roles=["firmware-owner"])
        verdict = check.evaluate(inputs([moved], text, (review(),)))
        self.assert_fails(verdict, "owner approval or split the rename into deletion and addition")

    def test_owned_to_r1_rename_fails_even_with_another_owned_change(self) -> None:
        moved = change("R", "profiles/built-in/p.json", "docs/x.json")
        text = description(risk="R3", roles=["firmware-owner"])
        verdict = check.evaluate(inputs([moved, FIRMWARE], text, (review(),)))
        self.assert_fails(verdict, "owner approval or split the rename into deletion and addition")

    def test_case_exact_owned_destination_remains_code_owned(self) -> None:
        copied = change("C", "docs/README.md", "profiles/p.json")
        text = description(risk="R3", roles=["firmware-owner"])
        self.assert_passes(check.evaluate(inputs([copied], text, (review(),))))

    def test_same_runtime_without_fresh_session_mode_does_not_count(self) -> None:
        self.assert_fails(
            check.evaluate(
                inputs(
                    [CODE],
                    description(implementationOwner="codex/gpt-6-astra"),
                    (review(BOT),),
                )
            ),
            "same runtime requires same-runtime-fresh-session",
        )

    def test_same_runtime_fresh_session_can_review_same_model(self) -> None:
        text = description(implementationOwner="codex/gpt-6-astra")
        fresh = review(body=record_body(mode="same-runtime-fresh-session"))
        self.assert_passes(check.evaluate(inputs([CODE], text, (fresh,))))

    def test_same_runtime_different_model_requires_fresh_session_mode(self) -> None:
        text = description(implementationOwner="codex/gpt-6-sol")
        verdict = check.evaluate(inputs([CODE], text, (review(),)))
        self.assert_fails(verdict, "same runtime requires same-runtime-fresh-session")

    def test_other_runtime_mode_rejects_same_model(self) -> None:
        text = description(implementationOwner="codex/gpt-6-astra")
        verdict = check.evaluate(inputs([CODE], text, (review(),)))
        self.assert_fails(verdict, "same runtime requires same-runtime-fresh-session")

    def test_fresh_session_mode_rejects_different_runtime(self) -> None:
        fresh = review(body=record_body(mode="same-runtime-fresh-session"))
        verdict = check.evaluate(inputs([CODE], description(), (fresh,)))
        self.assert_fails(verdict, "different runtime requires other-runtime")

    def test_implementation_owner_needs_runtime_model(self) -> None:
        for owner in ("claude-code", "<agent runtime or person>", ""):
            with self.subTest(owner=owner):
                verdict = check.evaluate(
                    inputs([CODE], description(implementationOwner=owner), (review(),))
                )
                self.assert_fails(verdict, "implementationOwner must be a runtime/model identifier")

    def test_shared_app_principal_can_carry_independent_agent_review(self) -> None:
        self.assert_passes(check.evaluate(inputs([CODE], description(), (review(BOT),))))

    def test_head_policy_that_raises_its_paths_applies_at_once(self) -> None:
        raised = edited_policy(lambda policy: entry(policy, "prose").update(floor="R2"))
        verdict = check.evaluate(inputs([PROSE], description(risk="R0"), head_policy=raised))
        self.assert_fails(verdict, "below the floor R2")

    # Classification.

    def test_declared_risk_missing_fails(self) -> None:
        text = description().replace('"risk": "R1",', "")
        self.assert_fails(check.evaluate(inputs([CODE], text, (review(),))), "exactly the fields")

    def test_declared_risk_below_the_floor_fails(self) -> None:
        verdict = check.evaluate(
            inputs([change("M", "scripts/verify.py")], description(risk="R1"), (review(),))
        )
        self.assert_fails(verdict, "the declared risk R1 is below the floor R3")

    def test_unclassified_path_without_governance_role_and_classification_fails(self) -> None:
        verdict = check.evaluate(
            inputs([change("A", "newtop/tool.py")], description(risk="R3"), (review(),))
        )
        self.assert_fails(verdict, "required roles are not declared: ['governance-owner']")
        self.assert_fails(
            verdict, "unclassified path newtop/tool.py has no governance-owner classification"
        )

    def test_unclassified_path_without_code_owner_fails_even_with_classification(self) -> None:
        evidence = {
            "governance-owner": {
                "change": "New top-level folder.",
                "classification": {"newtop/tool.py": {"floor": "R2", "roles": []}},
            }
        }
        text = description(risk="R3", roles=["governance-owner"], evidence=evidence)
        self.assert_fails(
            check.evaluate(inputs([change("A", "newtop/tool.py")], text, (review(),))),
            "owner approval on the exact head",
        )

    def test_classification_that_adds_a_role_raises_the_requirement(self) -> None:
        evidence = {
            "governance-owner": {
                "change": "New top-level folder.",
                "classification": {"newtop/tool.py": {"floor": "R3", "roles": ["firmware-owner"]}},
            }
        }
        text = description(risk="R3", roles=["governance-owner"], evidence=evidence)
        verdict = check.evaluate(inputs([change("A", "newtop/tool.py")], text, (review(),)))
        self.assert_fails(verdict, "required roles are not declared: ['firmware-owner']")

    def test_cross_class_change_missing_one_of_its_roles_fails(self) -> None:
        text = description(risk="R3", roles=["release-owner"])
        verdict = check.evaluate(inputs([WORKFLOW, FIRMWARE], text, (review(),)))
        self.assert_fails(
            verdict, "required roles are not declared: ['firmware-owner', 'governance-owner']"
        )

    def test_rename_from_code_into_firmware_path_needs_the_firmware_role(self) -> None:
        moved = change("R", "src/NvtFwCombiner.Cli/Range.cs", "src/NvtFwCombiner.Domain/Range.cs")
        verdict = check.evaluate(inputs([moved], description(risk="R3"), (review(),)))
        self.assert_fails(verdict, "required roles are not declared: ['firmware-owner']")

    def test_deletion_of_a_firmware_file_needs_the_firmware_role(self) -> None:
        deleted = change("D", "profiles/built-in/example/profile.json")
        verdict = check.evaluate(inputs([deleted], description(risk="R3"), (review(),)))
        self.assert_fails(verdict, "required roles are not declared: ['firmware-owner']")

    def test_head_policy_lowering_its_own_paths_keeps_the_base_requirement(self) -> None:
        def lower(policy: dict[str, Any]) -> None:
            entry(policy, "firmware")["floor"] = "R1"
            entry(policy, "firmware")["roles"] = []

        verdict = check.evaluate(
            inputs(
                [FIRMWARE], description(risk="R1"), (review(),), head_policy=edited_policy(lower)
            )
        )
        self.assert_fails(verdict, "the declared risk R1 is below the floor R3")
        self.assert_fails(verdict, "required roles are not declared: ['firmware-owner']")

    def test_role_a_review_record_adds_must_be_declared(self) -> None:
        added = review(body=record_body(addedRoles=["firmware-owner"]))
        verdict = check.evaluate(inputs([CODE], description(risk="R3"), (added,)))
        self.assert_fails(verdict, "required roles are not declared: ['firmware-owner']")

    def test_declared_role_makes_the_change_r3(self) -> None:
        verdict = check.evaluate(
            inputs([CODE], description(risk="R1", roles=["firmware-owner"]), (review(),))
        )
        self.assert_fails(verdict, "the declared risk R1 is below the floor R3")

    # Evidence.

    def test_role_declared_without_its_evidence_entries_fails(self) -> None:
        text = description(
            risk="R3",
            roles=["governance-owner", "release-owner"],
            evidence={"governance-owner": EVIDENCE["governance-owner"]},
        )
        verdict = check.evaluate(inputs([WORKFLOW], text, (review(),)))
        self.assert_fails(verdict, "role release-owner is declared without its evidence entries")

    def test_firmware_role_without_golden_or_write_range_entry_fails(self) -> None:
        for missing in ("golden", "writeRanges"):
            with self.subTest(missing=missing):
                entries = {
                    name: value
                    for name, value in EVIDENCE["firmware-owner"].items()
                    if name != missing
                }
                text = description(
                    risk="R3", roles=["firmware-owner"], evidence={"firmware-owner": entries}
                )
                self.assert_fails(
                    check.evaluate(inputs([FIRMWARE], text, (review(),))),
                    f"lacks well-formed evidence entries ['{missing}']",
                )

    def test_placeholder_evidence_is_not_well_formed(self) -> None:
        evidence = {
            "firmware-owner": {"golden": "<byte and Golden evidence>", "writeRanges": "TBD"}
        }
        text = description(risk="R3", roles=["firmware-owner"], evidence=evidence)
        self.assert_fails(
            check.evaluate(inputs([FIRMWARE], text, (review(),))), "['golden', 'writeRanges']"
        )

    def test_governance_role_without_its_change_statement_fails(self) -> None:
        evidence = {
            "governance-owner": {"classification": {}},
            "release-owner": EVIDENCE["release-owner"],
        }
        text = description(
            risk="R3", roles=["governance-owner", "release-owner"], evidence=evidence
        )
        self.assert_fails(
            check.evaluate(inputs([WORKFLOW], text, (review(),))),
            "lacks well-formed evidence entries ['change']",
        )

    def test_evidence_for_an_undeclared_role_is_malformed(self) -> None:
        text = description(
            risk="R3", roles=[], evidence={"release-owner": EVIDENCE["release-owner"]}
        )
        self.assert_fails(
            check.evaluate(inputs([CODE], text, (review(),))), "evidence for undeclared roles"
        )

    # Description block form.

    def test_missing_duplicated_or_unterminated_authority_block_fails(self) -> None:
        cases = {
            "no `nfc-authority` block": "## Outcome\n\nNo block.\n",
            "exactly one is allowed": description() + description(),
            "unterminated": "```nfc-authority\n{}\n",
            "not valid JSON": '```nfc-authority\n{"risk": "R1", "risk": "R2"}\n```\n',
        }
        for fragment, text in cases.items():
            with self.subTest(fragment=fragment):
                self.assert_fails(check.evaluate(inputs([CODE], text, (review(),))), fragment)

    def test_block_inside_another_code_fence_does_not_count(self) -> None:
        text = "````markdown\n" + description() + "````\n"
        self.assert_fails(
            check.evaluate(inputs([CODE], text, (review(),))), "no `nfc-authority` block"
        )

    # Review records.

    def test_r1_change_without_a_review_record_fails(self) -> None:
        verdict = check.evaluate(inputs([CODE], description()))
        self.assert_fails(verdict, "needs a valid independent review record on head")

    def test_record_on_an_older_head_fails(self) -> None:
        cases = {
            "commit_id": review(commit=OLDER),
            "block SHA": review(body=record_body(head=OLDER)),
        }
        for name, stale in cases.items():
            with self.subTest(name=name):
                self.assert_fails(
                    check.evaluate(inputs([CODE], description(), (stale,))),
                    "needs a valid independent review record",
                )

    def test_record_from_a_principal_not_on_the_list_is_ignored(self) -> None:
        for principal in (OUTSIDER, ("nfc-agent-dennis40816[bot]", 1), ("impostor", BOT[1])):
            with self.subTest(principal=principal):
                verdict = check.evaluate(inputs([CODE], description(), (review(principal),)))
                self.assert_fails(verdict, "no listed reviewer posted a record")

    def test_record_with_open_p0_p1_fails(self) -> None:
        verdict = check.evaluate(
            inputs([CODE], description(), (review(body=record_body(openP0P1=1)),))
        )
        self.assert_fails(verdict, "1 open P0/P1 findings")

    def test_rejecting_incomplete_or_malformed_record_fails(self) -> None:
        cases = {
            "verdict rejects": record_body(verdict="reject"),
            "incomplete": record_body(state="incomplete"),
            "exactly the fields": record_body(extra=True),
            "runtime/model": record_body(reviewer="Codex"),
        }
        for fragment, body in cases.items():
            with self.subTest(fragment=fragment):
                self.assert_fails(
                    check.evaluate(inputs([CODE], description(), (review(body=body),))), fragment
                )

    def test_record_must_be_a_comment_review(self) -> None:
        verdict = check.evaluate(inputs([CODE], description(), (review(OWNER, state="APPROVED"),)))
        self.assert_fails(verdict, "APPROVED review, not a comment review")

    def test_latest_record_of_a_principal_decides(self) -> None:
        earlier = review(number=1, at="2026-09-27T10:00:00Z")
        later = review(number=2, body=record_body(verdict="reject"), at="2026-09-27T11:00:00Z")
        self.assert_fails(
            check.evaluate(inputs([CODE], description(), (earlier, later))), "verdict rejects"
        )
        self.assert_passes(
            check.evaluate(
                inputs([CODE], description(), (later, review(number=3, at="2026-09-27T12:00:00Z")))
            )
        )

    def test_owner_record_counts_beside_a_rejecting_bot_record(self) -> None:
        rejected = review(body=record_body(verdict="reject"))
        self.assert_passes(
            check.evaluate(inputs([CODE], description(), (rejected, review(OWNER, number=2))))
        )

    def test_reviewer_must_be_listed_in_base_and_head_policies(self) -> None:
        added = edited_policy(
            lambda policy: policy["reviewers"].append({"login": OUTSIDER[0], "id": OUTSIDER[1]})
        )
        verdict = check.evaluate(
            inputs([CODE], description(), (review(OUTSIDER),), head_policy=added)
        )
        self.assert_fails(verdict, "no listed reviewer posted a record")

    # Policy inputs.

    def test_policy_that_violates_its_schema_fails(self) -> None:
        broken = edited_policy(lambda policy: entry(policy, "prose").update(floor="R4"))
        self.assert_fails(
            check.evaluate(inputs([PROSE], description(risk="R0"), head_policy=broken)),
            "violates its schema",
        )

    def test_policy_naming_a_role_without_a_principal_fails(self) -> None:
        cases = {
            "empty principals": lambda policy: policy["roles"].update({"release-owner": []}),
            "unknown role": lambda policy: entry(policy, "release")["roles"].append(
                "security-owner"
            ),
        }
        for name, edit in cases.items():
            with self.subTest(name=name):
                verdict = check.evaluate(
                    inputs([PROSE], description(risk="R0"), head_policy=edited_policy(edit))
                )
                self.assert_fails(verdict, "violates its schema")

    def test_exclusions_on_a_non_default_entry_fail(self) -> None:
        broken = edited_policy(
            lambda policy: entry(policy, "prose").update(excludes=["docs/handoff/x.md"])
        )
        self.assert_fails(
            check.evaluate(inputs([PROSE], description(risk="R0"), head_policy=broken)),
            "not a default entry",
        )

    def test_schema_with_an_unsupported_keyword_fails(self) -> None:
        schema = json.loads(SCHEMA)
        schema["oneOf"] = []
        verdict = check.evaluate(
            inputs([PROSE], description(risk="R0"), head_schema=json.dumps(schema).encode())
        )
        self.assert_fails(verdict, "unsupported keywords")

    def test_missing_base_policy_fails_unless_the_pull_request_adds_it(self) -> None:
        verdict = check.evaluate(
            inputs([PROSE], description(risk="R0"), base_policy=None, base_schema=None)
        )
        self.assert_fails(verdict, "the base has no authority policy")
        bootstrap = [change("A", check.POLICY_PATH), change("A", check.SCHEMA_PATH), PROSE]
        text = description(risk="R3", roles=["governance-owner"])
        self.assert_passes(
            check.evaluate(inputs(bootstrap, text, (review(),), base_policy=None, base_schema=None))
        )

    def test_missing_head_policy_fails(self) -> None:
        verdict = check.evaluate(
            inputs([change("D", check.POLICY_PATH)], description(risk="R3"), head_policy=None)
        )
        self.assert_fails(verdict, "the head has no authority policy")


class Response:
    @staticmethod
    def ok(value: Any, link: str | None = None) -> Any:
        headers = {"content-type": "application/json"}
        if link:
            headers["link"] = link
        return check.HttpResponse(200, headers, json.dumps(value).encode())


def pull(head: str = HEAD, body: str | None = None) -> dict[str, Any]:
    return {
        "number": 7,
        "state": "open",
        "body": description() if body is None else body,
        "head": {"sha": head, "ref": "feature/x"},
        "base": {"ref": "1.1.x", "sha": OLDER, "repo": {"full_name": REPOSITORY}},
    }


def api_review(number: int, principal: tuple[str, int] = BOT) -> dict[str, Any]:
    return {
        "id": number,
        "user": {"login": principal[0], "id": principal[1], "type": "Bot"},
        "body": record_body(),
        "state": "COMMENTED",
        "commit_id": HEAD,
        "submitted_at": "2026-09-27T10:00:00Z",
    }


class FakeTransport:
    def __init__(self, routes: dict[str, Any]) -> None:
        self.routes = routes
        self.requests: list[tuple[str, dict[str, str]]] = []

    def __call__(self, url: str, headers: Any, timeout: float) -> Any:
        self.requests.append((url, dict(headers)))
        route = self.routes.get(url)
        if route is None:
            return check.HttpResponse(404, {}, b'{"message": "Not Found"}')
        if isinstance(route, Exception):
            raise route
        return route


def repo_url(path: str) -> str:
    return f"{API}/repos/{REPOSITORY}/{path}"


class GitHubAdapterTests(unittest.TestCase):
    def api(self, routes: dict[str, Any]) -> Any:
        return check.GitHubApi(REPOSITORY, token="t", api_url=API, transport=FakeTransport(routes))

    def test_reads_every_review_page(self) -> None:
        first = [api_review(number) for number in range(1, 101)]
        routes = {
            repo_url("pulls/7/reviews?per_page=100"): Response.ok(
                first, f'<{repo_url("pulls/7/reviews?per_page=100&page=2")}>; rel="next"'
            ),
            repo_url("pulls/7/reviews?per_page=100&page=2"): Response.ok([api_review(101)]),
        }
        self.assertEqual([item.id for item in self.api(routes).reviews(7)], list(range(1, 102)))

    def test_incomplete_page_before_the_last_fails(self) -> None:
        routes = {
            repo_url("pulls/7/reviews?per_page=100"): Response.ok(
                [api_review(1)], f'<{repo_url("pulls/7/reviews?page=2")}>; rel="next"'
            ),
        }
        with self.assertRaisesRegex(check.AuthorityError, "incomplete page"):
            self.api(routes).reviews(7)

    def test_pagination_that_leaves_the_api_fails(self) -> None:
        first = [api_review(number) for number in range(1, 101)]
        routes = {
            repo_url("pulls/7/reviews?per_page=100"): Response.ok(
                first, '<https://elsewhere.test/page2>; rel="next"'
            )
        }
        with self.assertRaisesRegex(check.AuthorityError, "pagination left"):
            self.api(routes).reviews(7)

    def test_api_error_rate_limit_and_malformed_responses_fail(self) -> None:
        url = repo_url("pulls/7")
        cases = {
            "HTTP 500": check.HttpResponse(500, {}, b"{}"),
            "rate limit": check.HttpResponse(403, {"x-ratelimit-remaining": "0"}, b"{}"),
            "rate limit ": check.HttpResponse(429, {"retry-after": "60"}, b"{}"),
            "not valid JSON": check.HttpResponse(200, {}, b"{not json"),
            "malformed": Response.ok({"number": 7, "head": {"sha": "short"}}),
            "another repository": Response.ok(
                {**pull(), "base": {"ref": "1.1.x", "repo": {"full_name": "fork/repo"}}}
            ),
        }
        for fragment, response in cases.items():
            with self.subTest(fragment=fragment):
                with self.assertRaisesRegex(check.AuthorityError, fragment.strip()):
                    self.api({url: response}).pull_request(7)

    def test_transport_timeout_fails(self) -> None:
        opener = mock.Mock()
        opener.open.side_effect = TimeoutError("timed out")
        with mock.patch.object(check.urllib.request, "build_opener", return_value=opener):
            with self.assertRaisesRegex(check.AuthorityError, "failed or timed out"):
                check.urllib_transport(repo_url("pulls/7"), {}, 1.0)

    def test_branch_head_must_name_a_commit(self) -> None:
        url = repo_url("git/ref/heads/1.1.x")
        good = {"ref": "refs/heads/1.1.x", "object": {"type": "commit", "sha": OLDER}}
        self.assertEqual(self.api({url: Response.ok(good)}).branch_head("1.1.x"), OLDER)
        with self.assertRaisesRegex(check.AuthorityError, "does not name a commit"):
            self.api(
                {url: Response.ok({**good, "object": {"type": "tag", "sha": OLDER}})}
            ).branch_head("1.1.x")

    def test_token_is_sent_only_to_the_api(self) -> None:
        transport = FakeTransport({repo_url("pulls/7"): Response.ok(pull())})
        check.GitHubApi(REPOSITORY, token="secret", api_url=API, transport=transport).pull_request(
            7
        )
        self.assertEqual(transport.requests[0][1]["Authorization"], "Bearer secret")
        self.assertTrue(transport.requests[0][0].startswith(API + "/"))


class GitRunTests(unittest.TestCase):
    """End-to-end runs over a scratch repository and recorded API responses."""

    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="authority-check-")
        self.repo = Path(self.temporary.name)
        self.git("init", "-q")
        self.git("checkout", "-q", "-b", "1.1.x")
        self.git("config", "user.name", "Test")
        self.git("config", "user.email", "test@example.invalid")
        self.write(check.POLICY_PATH, POLICY)
        self.write(check.SCHEMA_PATH, SCHEMA)
        self.write("src/NvtFwCombiner.Cli/Range.cs", b"class Range {}\n")
        self.write("profiles/built-in/p.json", PROFILE)
        self.base = self.commit("base")
        self.git("checkout", "-q", "-b", "feature/x")

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def git(self, *arguments: str) -> str:
        return subprocess.run(
            ["git", *arguments], cwd=self.repo, check=True, capture_output=True, text=True
        ).stdout.strip()

    def write(self, relative: str, content: bytes) -> None:
        path = self.repo / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content)

    def commit(self, message: str) -> str:
        self.git("add", "-A")
        self.git("commit", "-q", "-m", message)
        return self.git("rev-parse", "HEAD")

    def routes(self, head: str, text: str, reviews: list[dict[str, Any]]) -> dict[str, Any]:
        return {
            repo_url("pulls/7"): Response.ok({**pull(head, text), "head": {"sha": head}}),
            repo_url("git/ref/heads/1.1.x"): Response.ok(
                {"ref": "refs/heads/1.1.x", "object": {"type": "commit", "sha": self.base}}
            ),
            repo_url("pulls/7/reviews?per_page=100"): Response.ok(reviews),
        }

    def run_main(self, routes: dict[str, Any]) -> tuple[int, str]:
        summary = self.repo.parent / f"{self.repo.name}-summary.md"
        self.addCleanup(summary.unlink, missing_ok=True)
        arguments = [
            "--repository",
            REPOSITORY,
            "--pull-request",
            "7",
            "--root",
            str(self.repo),
            "--summary",
            str(summary),
            "--api-url",
            API,
        ]
        with mock.patch("sys.stdout"):
            code = check.main(arguments, transport=FakeTransport(routes))
        return code, summary.read_text(encoding="utf-8")

    def reviewed(self, head: str) -> list[dict[str, Any]]:
        value = api_review(1)
        value.update(commit_id=head, body=record_body(head=head))
        return [value]

    def test_rename_into_and_deletion_of_firmware_paths_are_both_checked(self) -> None:
        (self.repo / "src/NvtFwCombiner.Domain").mkdir(parents=True)
        self.git("mv", "src/NvtFwCombiner.Cli/Range.cs", "src/NvtFwCombiner.Domain/Range.cs")
        self.git("rm", "-q", "profiles/built-in/p.json")
        head = self.commit("move")
        changes = check.Git(self.repo).changes(self.base, head)
        paths = {path for item in changes for path in item.paths}
        self.assertTrue(
            {
                "src/NvtFwCombiner.Cli/Range.cs",
                "src/NvtFwCombiner.Domain/Range.cs",
                "profiles/built-in/p.json",
            }
            <= paths
        )
        code, summary = self.run_main(
            self.routes(head, description(risk="R3"), self.reviewed(head))
        )
        self.assertEqual(code, 1)
        self.assertIn("required roles are not declared: ['firmware-owner']", summary)

    def test_passing_run_records_the_authority_blob_ids(self) -> None:
        self.write("src/NvtFwCombiner.Cli/Other.cs", b"class Other {}\n")
        head = self.commit("code")
        code, summary = self.run_main(self.routes(head, description(), self.reviewed(head)))
        self.assertEqual(code, 0, summary)
        blob = self.git("rev-parse", f"{head}:{check.POLICY_PATH}")
        self.assertIn(f"| `{check.POLICY_PATH}` | `{blob}` | `{blob}` |", summary)
        self.assertIn(f"head `{head}`, base `1.1.x` at `{self.base}`", summary)

    def test_live_head_that_differs_from_the_checkout_fails(self) -> None:
        head = self.git("rev-parse", "HEAD")
        code, summary = self.run_main(self.routes(OLDER, description(), self.reviewed(OLDER)))
        self.assertEqual(code, 1)
        self.assertIn(f"the checked-out head {head} differs from the live head {OLDER}", summary)

    def test_git_failure_fails(self) -> None:
        head = self.git("rev-parse", "HEAD")
        routes = self.routes(head, description(), self.reviewed(head))
        routes[repo_url("git/ref/heads/1.1.x")] = Response.ok(
            {"ref": "refs/heads/1.1.x", "object": {"type": "commit", "sha": "c" * 40}}
        )
        code, summary = self.run_main(routes)
        self.assertEqual(code, 1)
        self.assertIn("is not in this clone", summary)

    def test_copy_from_an_unchanged_firmware_file_needs_the_firmware_role(self) -> None:
        # Fixed-head review F-1: the copy source is unchanged, so only an exhaustive copy search
        # that considers unmodified files reports it.
        self.write("src/NvtFwCombiner.Cli/regions.json", PROFILE)
        head = self.commit("copy")
        changes = check.Git(self.repo).changes(self.base, head)
        self.assertIn(
            ("profiles/built-in/p.json", "src/NvtFwCombiner.Cli/regions.json"),
            [item.paths for item in changes if item.status == "C"],
        )
        code, summary = self.run_main(self.routes(head, description(), self.reviewed(head)))
        self.assertEqual(code, 1)
        self.assertIn("required roles are not declared: ['firmware-owner']", summary)
        self.assertIn("paths: profiles/built-in/p.json", summary)

    def test_changed_submodule_commit_is_reported_whatever_the_submodule_settings(self) -> None:
        # A head that sets "ignore = all" in .gitmodules, or a runner with
        # diff.ignoreSubmodules=all, must not hide the submodule's new commit from the check.
        path = "third-party/vendored"
        self.git("update-index", "--add", "--cacheinfo", f"160000,{'1' * 40},{path}")
        self.git("commit", "-q", "-m", "submodule")
        base = self.git("rev-parse", "HEAD")
        self.write(
            ".gitmodules",
            f'[submodule "vendored"]\n\tpath = {path}\n\turl = ./vendored\n\tignore = all\n'.encode(),
        )
        self.git("add", ".gitmodules")
        self.git("update-index", "--cacheinfo", f"160000,{'2' * 40},{path}")
        self.git("commit", "-q", "-m", "move the submodule and ignore it")
        head = self.git("rev-parse", "HEAD")
        for setting in ("none", "all"):
            with self.subTest(setting=setting):
                self.git("config", "diff.ignoreSubmodules", setting)
                changes = check.Git(self.repo).changes(base, head)
                self.assertIn((path,), [item.paths for item in changes if item.status == "M"])

    def test_degraded_copy_detection_fails_closed(self) -> None:
        for index in range(3):
            self.write(f"src/NvtFwCombiner.Cli/New{index}.cs", f"class New{index} {{}}\n".encode())
        head = self.commit("several")
        with mock.patch.object(check, "COPY_DETECTION_LIMIT", 1):
            with self.assertRaisesRegex(check.AuthorityError, "skipped part of the rename"):
                check.Git(self.repo).changes(self.base, head)

    def test_summary_names_the_checker_that_ran_when_it_is_not_the_heads(self) -> None:
        # Fixed-head review F-3: pre-merge step 1 runs the base checker against another head.
        self.write(check.CHECKER_PATH, b"# a different checker at the evaluated head\n")
        head = self.commit("another checker")
        _, summary = self.run_main(self.routes(head, description(), self.reviewed(head)))
        ran = self.root_git("hash-object", "--", check.CHECKER_PATH)
        revision = self.root_git("rev-parse", "HEAD")
        evaluated = self.git("rev-parse", f"{head}:{check.CHECKER_PATH}")
        self.assertNotEqual(ran, evaluated)
        self.assertIn(
            f"- Checker that ran: `{check.CHECKER_PATH}` blob `{ran}` from revision `{revision}`",
            summary,
        )
        self.assertIn("not the evaluated head's checker", summary)
        self.assertIn(f"| `{check.CHECKER_PATH}` | `{evaluated}` | `absent` |", summary)
        self.assertNotIn("(used)", summary)

    def test_summary_names_the_heads_own_checker(self) -> None:
        self.write(check.CHECKER_PATH, (ROOT / check.CHECKER_PATH).read_bytes())
        head = self.commit("own checker")
        identity = check.executed_checker(self.repo / check.CHECKER_PATH)
        blob = self.git("rev-parse", f"{head}:{check.CHECKER_PATH}")
        expected = check.CheckerIdentity(head, check.CHECKER_PATH, blob, "matches that revision")
        self.assertEqual(identity, expected)
        context = check.RunContext(
            7, head=head, head_files={check.CHECKER_PATH: blob}, checker=identity
        )
        self.assertIn("the evaluated head's own checker", check.render_summary(context, None, []))
        self.write(check.CHECKER_PATH, b"# edited after the commit\n")
        edited = check.executed_checker(self.repo / check.CHECKER_PATH)
        self.assertEqual(edited.state, "differs from that revision")

    def root_git(self, *arguments: str) -> str:
        return subprocess.run(
            ["git", *arguments], cwd=ROOT, check=True, capture_output=True, text=True
        ).stdout.strip()

    def test_api_failure_fails_the_run(self) -> None:
        head = self.git("rev-parse", "HEAD")
        routes = self.routes(head, description(), self.reviewed(head))
        routes[repo_url("pulls/7/reviews?per_page=100")] = check.HttpResponse(502, {}, b"")
        code, summary = self.run_main(routes)
        self.assertEqual(code, 1)
        self.assertIn("HTTP 502", summary)


if __name__ == "__main__":
    unittest.main()

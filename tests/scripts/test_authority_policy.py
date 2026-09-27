"""Coverage, consistency and dependency tests for the authority policy (ADR 0080 item 4).

They bind the policy to the current tree: every tracked path is classified, no
path the record validator governs gets a lower floor while that validator
exists, every default exclusion is covered, the script table of item 4 holds,
the checker reads only governance-R3 files, CODEOWNERS gives every role path to
its principals, prose is read by no topic test, and the workflow keeps its
fail-closed shape.
"""

from __future__ import annotations

import ast
import importlib.util
import re
import subprocess
import sys
import unittest
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "authority_check", ROOT / "scripts" / "authority_check.py"
)
assert SPEC is not None and SPEC.loader is not None
check = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = check
SPEC.loader.exec_module(check)
sys.path.insert(0, str(ROOT / "scripts"))
import validate_repository as record_validator  # noqa: E402

POLICY = check.load_policy(
    (ROOT / check.POLICY_PATH).read_bytes(), (ROOT / check.SCHEMA_PATH).read_bytes(), "head"
)
TRACKED = sorted(
    value.decode("utf-8")
    for value in subprocess.run(
        ["git", "ls-files", "-z"], cwd=ROOT, check=True, capture_output=True
    ).stdout.split(b"\0")
    if value
)
# ADR 0080 item 4, the script table (the current R3 scripts and three raised from R2).
SCRIPT_ROLES = {
    **dict.fromkeys(
        [
            "ab_merge_fixture_validation.py",
            "create_candidate_ic_intake.py",
            "create_ctrlram_universal_sentinel.py",
            "diagnostic_golden_validation.py",
            "intake_ic_reference.py",
        ],
        {"firmware-owner"},
    ),
    **dict.fromkeys(
        [
            "canonical_golden_validation.py",
            "external_tool_policy.py",
            "v0916_parity_certification.py",
            "release_source_pins.py",
        ],
        {"firmware-owner", "release-owner"},
    ),
    **dict.fromkeys(
        [
            "create_update_catalog.py",
            "update_source_registry_policy.py",
            "edit_update_source_registry.py",
            "package.ps1",
            "package-distribution-launcher.ps1",
            "publish-github.ps1",
            "publish-github.sh",
            "render_release_notes.py",
            "sign-release.ps1",
            "sign-release.sh",
            "sign_release.py",
            "signing_policy.py",
            "smoke-release.ps1",
            "deploy-update-source.ps1",
        ],
        {"release-owner"},
    ),
    "release_promotion_policy.py": {"release-owner", "governance-owner"},
}
CHECKER_FILES = (
    check.CHECKER_PATH,
    check.WORKFLOW_PATH,
    "tests/scripts/test_authority_check.py",
    "tests/scripts/test_authority_policy.py",
    *check.CHECKER_DEPENDENCIES,
)
CONSUMER_ROOTS = ("tests/", "scripts/", "src/", "eng/", "tools/", ".github/")
CONSUMER_SUFFIXES = (
    ".py",
    ".cs",
    ".ps1",
    ".psm1",
    ".sh",
    ".yml",
    ".yaml",
    ".props",
    ".targets",
    ".csproj",
    ".toml",
)


def codeowners_rules() -> list[tuple[re.Pattern[str], set[str]]]:
    """Parse the GitHub CODEOWNERS subset this repository uses (the last match wins)."""

    rules: list[tuple[re.Pattern[str], set[str]]] = []
    for line in (ROOT / ".github/CODEOWNERS").read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        pattern, *owners = line.split()
        if re.search(r"[\[\]!\\]", pattern) or not owners:
            raise AssertionError(f"unsupported CODEOWNERS line: {line}")
        anchored = pattern.startswith("/") or "/" in pattern.rstrip("/")
        directory = pattern.endswith("/")
        body = (
            re.escape(pattern.strip("/"))
            .replace(r"\*\*", ".*")
            .replace(r"\*", "[^/]*")
            .replace(r"\?", "[^/]")
        )
        regex = ("" if anchored else "(?:.*/)?") + body + ("/.*" if directory else "(?:/.*)?")
        rules.append((re.compile(regex), {owner.casefold() for owner in owners}))
    return rules


class AuthorityPolicyTests(unittest.TestCase):
    def test_every_tracked_path_is_classified(self) -> None:
        unclassified = [path for path in TRACKED if POLICY.classify(path).unclassified]
        self.assertEqual(unclassified, [])

    def test_every_default_exclusion_is_matched_by_another_entry(self) -> None:
        for default in (entry for entry in POLICY.entries if entry.default):
            for path in TRACKED:
                if any(
                    pattern.fullmatch(path) for pattern in default.patterns
                ) and not default.matches(path):
                    others = [
                        entry.id
                        for entry in POLICY.entries
                        if entry is not default and entry.matches(path)
                    ]
                    self.assertTrue(
                        others,
                        f"{path} is excluded from {default.id} and matched by no other entry",
                    )

    def test_no_governed_path_gets_a_lower_floor_than_the_record_validator(self) -> None:
        # Binds the migration seam until G1-B deletes the record classifier.
        lowered = []
        for path in TRACKED:
            if record_validator._is_capability_reuse_governed_path(path):
                old = record_validator._capability_reuse_minimum_risk(path)
                new = POLICY.classify(path).floor
                if check.RISKS.index(new) < check.RISKS.index(old):
                    lowered.append(f"{path}: {old} -> {new}")
        self.assertEqual(lowered, [])

    def test_script_table_of_item_4(self) -> None:
        self.assertLessEqual(
            {Path(path).name for path in record_validator.CAPABILITY_REUSE_R3_SCRIPTS},
            set(SCRIPT_ROLES),
        )
        for name, roles in SCRIPT_ROLES.items():
            with self.subTest(script=name):
                result = POLICY.classify(f"scripts/{name}")
                self.assertEqual((result.floor, set(result.roles)), ("R3", roles))
        for path in TRACKED:
            if (
                path.startswith("scripts/")
                and Path(path).name not in SCRIPT_ROLES
                and path != check.CHECKER_PATH
            ):
                with self.subTest(script=path):
                    self.assertEqual(POLICY.classify(path).roles, frozenset())

    def test_checker_reads_and_imports_only_governance_r3_files(self) -> None:
        source = (ROOT / check.CHECKER_PATH).read_text(encoding="utf-8")
        tree = ast.parse(source)
        modules = {
            alias.name.split(".")[0]
            for node in ast.walk(tree)
            if isinstance(node, ast.Import)
            for alias in node.names
        }
        for node in ast.walk(tree):
            if isinstance(node, ast.ImportFrom):
                self.assertEqual(node.level, 0, "the checker must not use relative imports")
                modules.add(str(node.module).split(".")[0])
        self.assertEqual(
            {
                name
                for name in modules
                if name not in sys.stdlib_module_names and name != "__future__"
            },
            set(),
        )
        literals = {
            node.value
            for node in ast.walk(tree)
            if isinstance(node, ast.Constant) and isinstance(node.value, str)
        }
        self.assertLessEqual(literals & set(TRACKED), set(check.AUTHORITY_FILES))
        self.assertNotIn("read_text", source)
        self.assertNotIn("read_bytes", source)
        for path in CHECKER_FILES + check.AUTHORITY_FILES:
            with self.subTest(path=path):
                result = POLICY.classify(path)
                self.assertEqual(result.floor, "R3")
                self.assertIn("governance-owner", result.roles)

    def test_codeowners_gives_every_role_path_to_its_principals(self) -> None:
        rules = codeowners_rules()
        self.assertEqual(
            rules[0][0].pattern, "(?:.*/)?[^/]*(?:/.*)?", "the default line must come first"
        )
        logins = {
            role: {f"@{login}" for _, login in principals}
            for role, principals in POLICY.principals.items()
        }
        self.assertLessEqual(logins["governance-owner"], rules[0][1])
        missing = []
        for path in TRACKED:
            owners = next(owners for regex, owners in reversed(rules) if regex.fullmatch(path))
            required = set().union(*(logins[role] for role in POLICY.classify(path).roles))
            if not required <= owners:
                missing.append(f"{path}: {sorted(required - owners)}")
        self.assertEqual(missing, [])

    def test_prose_is_read_by_no_topic_test_or_verification(self) -> None:
        prose = [path for path in TRACKED if POLICY.classify(path).floor == "R0"]
        self.assertTrue(prose)
        consumers = [
            path
            for path in TRACKED
            if path.startswith(CONSUMER_ROOTS)
            and path.endswith(CONSUMER_SUFFIXES)
            and path not in CHECKER_FILES
        ]
        texts = {
            path: (ROOT / path).read_text(encoding="utf-8", errors="replace") for path in consumers
        }
        readers = []
        for document in prose:
            name = Path(document).name
            quoted = re.compile(rf"[\"']{re.escape(name)}[\"']")
            for path, text in texts.items():
                for line in text.splitlines():
                    if document in line or ("handoff" in line and quoted.search(line)):
                        readers.append(f"{path} reads {document}")
        self.assertEqual(readers, [])

    def test_workflow_keeps_its_fail_closed_shape(self) -> None:
        text = (ROOT / check.WORKFLOW_PATH).read_text(encoding="utf-8")
        workflow = yaml.safe_load(text)
        triggers = workflow.get("on", workflow.get(True))
        self.assertEqual(
            triggers,
            {
                "pull_request": {
                    "types": ["opened", "synchronize", "reopened", "ready_for_review", "edited"]
                }
            },
        )
        self.assertEqual(workflow["permissions"], {"contents": "read", "pull-requests": "read"})
        self.assertNotIn("concurrency", workflow)
        self.assertNotIn("secrets.", text)
        (job,) = workflow["jobs"].values()
        self.assertEqual(job["name"], "governance / authority")
        for forbidden in ("if", "continue-on-error", "strategy", "needs", "permissions"):
            self.assertNotIn(forbidden, job)
        for step in job["steps"]:
            self.assertNotIn("if", step)
            self.assertNotIn("continue-on-error", step)
        checkout = next(
            step
            for step in job["steps"]
            if str(step.get("uses", "")).startswith("actions/checkout@")
        )
        self.assertEqual(
            checkout["with"],
            {
                "ref": "${{ github.event.pull_request.head.sha }}",
                "persist-credentials": False,
                "fetch-depth": 0,
            },
        )
        run = next(step["run"] for step in job["steps"] if "run" in step)
        self.assertIn("python scripts/authority_check.py", run)
        self.assertIn('--summary "$GITHUB_STEP_SUMMARY"', run)


if __name__ == "__main__":
    unittest.main()

"""Coverage, consistency and dependency tests for the authority policy (ADR 0080 item 4).

They bind the policy to the current tree: every tracked path is classified, every default exclusion is covered, the script table of item 4 holds,
the checker reads only governance-R3 files, CODEOWNERS gives every role path to
its principals, prose is read by no topic test, and the workflow keeps its
fail-closed shape.
"""

from __future__ import annotations

import ast
import importlib.util
import json
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


def codeowner_pattern_errors(content: str, policy_bytes: bytes) -> list[str]:
    policy = json.loads(policy_bytes)
    expected = {
        pattern
        for entry in policy["entries"]
        if entry["floor"] in {"R2", "R3"}
        for pattern in entry["patterns"]
    }
    lines = [
        line.split()
        for line in content.splitlines()
        if line.strip() and not line.lstrip().startswith("#")
    ]
    actual = [parts[0].removeprefix("/") for parts in lines]
    errors = []
    if any(not parts[0].startswith("/") or re.search(r"[\[\]!\\]", parts[0]) for parts in lines):
        errors.append("CODEOWNERS contains an unanchored or unsupported pattern")
    if len(actual) != len(set(actual)):
        errors.append("duplicate CODEOWNERS patterns")
    if set(actual) != expected:
        errors.append(
            f"CODEOWNERS pattern mismatch: missing={sorted(expected - set(actual))}, "
            f"extra={sorted(set(actual) - expected)}"
        )
    if any(parts[1:] != ["@Dennis40816"] for parts in lines):
        errors.append("CODEOWNERS principals differ from @Dennis40816")
    return errors


class AuthorityPolicyTests(unittest.TestCase):
    def test_every_tracked_path_is_classified(self) -> None:
        unclassified = [path for path in TRACKED if POLICY.classify(path).unclassified]
        self.assertEqual(unclassified, [])

    def test_source_submodule_uses_repository_configuration_and_tooling(self) -> None:
        for path in (".gitmodules", "third-party/nvt_combiner"):
            with self.subTest(path=path):
                result = POLICY.classify(path, case_sensitive=True)
                self.assertEqual(result.floor, "R2")
                self.assertEqual(result.roles, frozenset())
                self.assertFalse(result.unclassified)

        # Source intake must not relax executable packages or unknown root files.
        executable = POLICY.classify("external-tools/nvt_combiner")
        self.assertEqual(executable.floor, "R3")
        self.assertEqual(executable.roles, {"firmware-owner", "release-owner"})
        unknown = POLICY.classify(".gitmodules-extra")
        self.assertEqual(unknown.floor, "R3")
        self.assertEqual(unknown.roles, {"governance-owner"})
        self.assertTrue(unknown.unclassified)

    def test_governance_paths_g1b_adds_are_classified_before_they_exist(self) -> None:
        # Fixed-head review F-2: ADR 0080 item 2's pin file and the three historical READMEs.
        planned = [
            "docs/governance/frozen-evidence-pins.json",
            "docs/governance/change-records/README.md",
            "docs/governance/external-authority-attestations/README.md",
            "docs/governance/waivers/README.md",
        ]
        for path in planned:
            with self.subTest(path=path):
                result = POLICY.classify(path)
                self.assertEqual((result.floor, result.unclassified), ("R3", False))
                self.assertIn("governance-owner", result.roles)

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

    def test_script_table_of_item_4(self) -> None:
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

    def test_codeowners_exactly_matches_r2_and_r3_patterns(self) -> None:
        content = (ROOT / ".github/CODEOWNERS").read_text(encoding="utf-8")
        policy_bytes = (ROOT / check.POLICY_PATH).read_bytes()
        self.assertEqual(codeowner_pattern_errors(content, policy_bytes), [])
        for path in (
            ".github/CODEOWNERS",
            check.CHECKER_PATH,
            check.POLICY_PATH,
            check.SCHEMA_PATH,
        ):
            self.assertGreaterEqual(check.RISKS.index(POLICY.classify(path).floor), 2)

    def test_nested_msbuild_configuration_is_owned_and_classified(self) -> None:
        for path in (
            "Directory.Build.props",
            "src/Directory.Build.props",
            "tests/Directory.Build.targets",
            "src/Directory.Packages.props",
            "src/NuGet.config",
        ):
            with self.subTest(path=path):
                result = POLICY.classify(path, case_sensitive=True)
                self.assertGreaterEqual(check.RISKS.index(result.floor), 2)
                self.assertFalse(result.unclassified)
                self.assertTrue(
                    any(
                        entry.floor in {"R2", "R3"}
                        and entry.matches(path, case_sensitive=True)
                        for entry in POLICY.entries
                    )
                )

    def test_codeowners_mismatch_with_policy_fails(self) -> None:
        content = (ROOT / ".github/CODEOWNERS").read_text(encoding="utf-8")
        policy_bytes = (ROOT / check.POLICY_PATH).read_bytes()
        self.assertTrue(
            codeowner_pattern_errors(content + "\n/README.md @Dennis40816\n", policy_bytes)
        )
        changed = json.loads(policy_bytes)
        changed["entries"][0]["patterns"].append("new-risk/**")
        self.assertTrue(codeowner_pattern_errors(content, json.dumps(changed).encode()))

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

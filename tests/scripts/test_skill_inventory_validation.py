"""Behavioral tests for repository skill inventory and Codex metadata."""

from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

SCRIPTS = Path(__file__).resolve().parents[2] / "scripts"
sys.path.insert(0, str(SCRIPTS))

import validate_repository as repository_validation  # noqa: E402


class SkillInventoryValidationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    def write_skill(
        self,
        name: str,
        *,
        extra_frontmatter: str = "",
        implicit_invocation: bool | None = None,
        include_metadata: bool = True,
        prompt_name: str | None = None,
        short_description: str | None = None,
    ) -> None:
        skill_root = self.root / ".agents" / "skills" / name
        skill_root.mkdir(parents=True)
        extra_line = f"{extra_frontmatter}\n" if extra_frontmatter else ""
        (skill_root / "SKILL.md").write_text(
            "---\n"
            f"name: {name}\n"
            f"description: Exercise the {name} repository workflow safely.\n"
            f"{extra_line}"
            "---\n\n"
            f"# {name}\n",
            encoding="utf-8",
        )
        if not include_metadata:
            return
        policy = ""
        if implicit_invocation is not None:
            value = "true" if implicit_invocation else "false"
            policy = f"policy:\n  allow_implicit_invocation: {value}\n"
        metadata_root = skill_root / "agents"
        metadata_root.mkdir()
        referenced_name = prompt_name or name
        description = short_description or f"Exercise the {name} workflow"
        (metadata_root / "openai.yaml").write_text(
            "interface:\n"
            f'  display_name: "{name}"\n'
            f'  short_description: "{description}"\n'
            f'  default_prompt: "Use ${referenced_name} for this workflow."\n'
            f"{policy}",
            encoding="utf-8",
        )

    def validate(
        self,
        *,
        expected: set[str] | None = None,
        user_invoked: set[str] | None = None,
    ) -> list[str]:
        expected_names = (
            {"explicit-skill", "implicit-skill"} if expected is None else expected
        )
        explicit_names = {"explicit-skill"} if user_invoked is None else user_invoked
        manifest_root = self.root / ".agents" / "skills"
        manifest_root.mkdir(parents=True, exist_ok=True)
        manifest = {
            "schemaVersion": 1,
            "skills": [
                {
                    "name": name,
                    "status": "active",
                    "scope": "repo",
                    "invocation": "explicit" if name in explicit_names else "implicit",
                    "authority": "test",
                    "owner": "repository",
                    "replaces": [],
                }
                for name in sorted(expected_names)
            ],
        }
        (manifest_root / "manifest.json").write_text(
            json.dumps(manifest),
            encoding="utf-8",
        )
        errors: list[str] = []
        with patch.object(repository_validation, "ROOT", self.root):
            repository_validation.validate_skills(errors)
        return errors

    def test_accepts_exact_inventory_and_invocation_metadata(self) -> None:
        self.write_skill("explicit-skill", implicit_invocation=False)
        self.write_skill("implicit-skill")

        self.assertEqual([], self.validate())

    def test_rejects_unsupported_frontmatter_key(self) -> None:
        self.write_skill(
            "explicit-skill",
            extra_frontmatter='argument-hint: "topic"',
            implicit_invocation=False,
        )

        errors = self.validate(
            expected={"explicit-skill"}, user_invoked={"explicit-skill"}
        )

        self.assertTrue(
            any("frontmatter keys must be exactly" in error for error in errors)
        )

    def test_rejects_missing_openai_metadata(self) -> None:
        self.write_skill("implicit-skill", include_metadata=False)

        errors = self.validate(expected={"implicit-skill"}, user_invoked=set())

        self.assertTrue(any("requires agents/openai.yaml" in error for error in errors))

    def test_rejects_invocation_policy_mismatch(self) -> None:
        self.write_skill("explicit-skill")
        self.write_skill("implicit-skill", implicit_invocation=False)

        errors = self.validate()

        self.assertTrue(any("explicit skill must set" in error for error in errors))
        self.assertTrue(
            any("implicit skill must not disable" in error for error in errors)
        )

    def test_rejects_default_prompt_for_another_skill(self) -> None:
        self.write_skill("implicit-skill", prompt_name="other-skill")

        errors = self.validate(expected={"implicit-skill"}, user_invoked=set())

        self.assertTrue(
            any(
                "default_prompt must reference $implicit-skill" in error
                for error in errors
            )
        )

    def test_rejects_default_prompt_matching_only_a_skill_prefix(self) -> None:
        self.write_skill("research", prompt_name="research-extra")

        errors = self.validate(expected={"research"}, user_invoked=set())

        self.assertTrue(
            any("default_prompt must reference $research" in error for error in errors)
        )

    def test_rejects_short_description_outside_codex_bounds(self) -> None:
        for description in ("x" * 24, "x" * 65):
            with self.subTest(length=len(description)):
                name = f"implicit-skill-{len(description)}"
                self.write_skill(name, short_description=description)

                errors = self.validate(expected={name}, user_invoked=set())

                self.assertTrue(
                    any(
                        "short_description must contain 25 to 64 characters" in error
                        for error in errors
                    )
                )

    def test_rejects_short_description_whose_raw_length_exceeds_bound(self) -> None:
        name = "implicit-skill-whitespace"
        self.write_skill(name, short_description=f" {'x' * 63} ")

        errors = self.validate(expected={name}, user_invoked=set())

        self.assertTrue(
            any(
                "short_description must contain 25 to 64 characters" in error
                for error in errors
            )
        )

    def test_rejects_short_description_whose_trimmed_length_is_below_bound(
        self,
    ) -> None:
        name = "implicit-skill-blank"
        self.write_skill(name, short_description=" " * 25)

        errors = self.validate(expected={name}, user_invoked=set())

        self.assertTrue(
            any(
                "short_description must contain 25 to 64 characters" in error
                for error in errors
            )
        )

    def test_rejects_malformed_openai_yaml_before_field_validation(self) -> None:
        self.write_skill("implicit-skill")
        metadata_path = (
            self.root
            / ".agents"
            / "skills"
            / "implicit-skill"
            / "agents"
            / "openai.yaml"
        )
        metadata_path.write_text(
            metadata_path.read_text(encoding="utf-8") + "malformed: [\n",
            encoding="utf-8",
        )

        errors = self.validate(expected={"implicit-skill"}, user_invoked=set())

        self.assertTrue(any("metadata is not valid YAML" in error for error in errors))

    def test_rejects_unknown_interface_metadata_field(self) -> None:
        self.write_skill("implicit-skill")
        metadata_path = (
            self.root
            / ".agents"
            / "skills"
            / "implicit-skill"
            / "agents"
            / "openai.yaml"
        )
        metadata_path.write_text(
            metadata_path.read_text(encoding="utf-8") + '  unexpected: "value"\n',
            encoding="utf-8",
        )

        errors = self.validate(expected={"implicit-skill"}, user_invoked=set())

        self.assertTrue(any("metadata is not valid YAML" in error for error in errors))

    def test_rejects_unknown_policy_metadata_field(self) -> None:
        self.write_skill("explicit-skill", implicit_invocation=False)
        metadata_path = (
            self.root
            / ".agents"
            / "skills"
            / "explicit-skill"
            / "agents"
            / "openai.yaml"
        )
        metadata_path.write_text(
            metadata_path.read_text(encoding="utf-8") + "  unexpected_policy: true\n",
            encoding="utf-8",
        )

        errors = self.validate(
            expected={"explicit-skill"}, user_invoked={"explicit-skill"}
        )

        self.assertTrue(any("metadata is not valid YAML" in error for error in errors))


class RepositorySkillRoutingContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.repository_root = Path(__file__).resolve().parents[2]
        cls.skills_root = cls.repository_root / ".agents" / "skills"
        cls.manifest = json.loads(
            (cls.skills_root / "manifest.json").read_text(encoding="utf-8")
        )
        cls.entries = {entry["name"]: entry for entry in cls.manifest["skills"]}

    def read_skill(self, name: str) -> str:
        return (self.skills_root / name / "SKILL.md").read_text(encoding="utf-8")

    def test_manifest_routes_exactly_twenty_active_skills(self) -> None:
        self.assertEqual(20, len(self.entries))
        self.assertTrue(
            all(entry["status"] == "active" for entry in self.entries.values())
        )

    def test_only_supervision_and_polling_are_explicit(self) -> None:
        explicit = {
            name
            for name, entry in self.entries.items()
            if entry["invocation"] == "explicit"
        }
        self.assertEqual(
            {
                "github-review-polling",
                "grilling",
                "supervised-branch-development",
            },
            explicit,
        )

    def test_to_spec_produces_draft_without_ready_label(self) -> None:
        text = self.read_skill("to-spec")
        self.assertIn("draft specification", text)
        self.assertIn("Never apply\n`ready-for-agent`", text)
        self.assertIn("Open\ndecisions", text)

    def test_to_tickets_accepts_headless_vertical_paths(self) -> None:
        text = self.read_skill("to-tickets")
        self.assertIn("headless path", text)
        self.assertIn("is vertical without UI", text)
        self.assertIn("owner explicitly approved", text)

    def test_implement_owns_red_green_refactor_loop(self) -> None:
        text = self.read_skill("implement")
        for phase in ("**Red:**", "**Green:**", "**Refactor:**", "**Repeat:**"):
            self.assertIn(phase, text)
        self.assertEqual(["tdd"], self.entries["implement"]["replaces"])

    def test_polytail_expands_only_touched_authority(self) -> None:
        text = self.read_skill("polytail")
        self.assertIn(
            "scope proportional to touched authority",
            (self.repository_root / "AGENTS.md").read_text(encoding="utf-8"),
        )
        self.assertIn("Expand the production-admission audit only when", text)

    def test_code_review_uses_three_lenses_without_forced_subagents(self) -> None:
        text = self.read_skill("code-review")
        self.assertIn("**Spec correctness**", text)
        self.assertIn("**Runtime, safety, and architecture**", text)
        self.assertIn("**Tests and evidence**", text)
        self.assertIn("Spawn read-only subagents only when", text)

    def test_supervision_is_not_the_default_workflow(self) -> None:
        text = self.read_skill("supervised-branch-development")
        self.assertIn("Ordinary work uses single-writer mode", text)
        self.assertIn("primary agent or owner is the default integrator", text)

    def test_removed_meta_skills_are_not_repository_routes(self) -> None:
        for name in (
            "ask-matt",
            "codebase-design",
            "domain-modeling",
            "handoff",
            "prototype",
            "research",
            "tdd",
        ):
            with self.subTest(name=name):
                self.assertNotIn(name, self.entries)
                self.assertFalse((self.skills_root / name / "SKILL.md").exists())

    def test_inventory_is_rendered_from_manifest(self) -> None:
        expected = repository_validation.render_skill_inventory(self.manifest["skills"])
        actual = (
            self.repository_root / "docs" / "governance" / "agent-skill-inventory.md"
        ).read_text(encoding="utf-8")
        self.assertEqual(expected, actual)


if __name__ == "__main__":
    unittest.main()

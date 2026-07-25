"""Behavioral tests for repository skill inventory and Codex metadata."""

from __future__ import annotations

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
        errors: list[str] = []
        with (
            patch.object(repository_validation, "ROOT", self.root),
            patch.object(
                repository_validation,
                "EXPECTED_SKILLS",
                (
                    {"explicit-skill", "implicit-skill"}
                    if expected is None
                    else expected
                ),
            ),
            patch.object(
                repository_validation,
                "EXPECTED_USER_INVOKED_SKILLS",
                {"explicit-skill"} if user_invoked is None else user_invoked,
            ),
        ):
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

        self.assertTrue(any("user-invoked skill must set" in error for error in errors))
        self.assertTrue(
            any("model-invoked skill must not disable" in error for error in errors)
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


if __name__ == "__main__":
    unittest.main()

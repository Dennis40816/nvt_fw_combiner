"""Behavioral tests for the closed AB golden evidence validator."""

from __future__ import annotations

import importlib.util
import json
from pathlib import Path, PurePosixPath
from types import ModuleType
from typing import Any
import unittest

ROOT = Path(__file__).resolve().parents[2]
VALIDATOR_PATH = ROOT / "scripts" / "ab_merge_fixture_validation.py"
CANONICAL_ROOT = ROOT / "testdata" / "golden" / "canonical"
MANIFEST_PATH = CANONICAL_ROOT / "manifest.json"


def load_validator_module() -> ModuleType:
    """Load the repository validator without changing the process import path."""
    spec = importlib.util.spec_from_file_location(
        "ab_merge_fixture_validation", VALIDATOR_PATH
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load {VALIDATOR_PATH}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


VALIDATOR = load_validator_module()


class AbMergeFixtureValidationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
        self.documents: dict[Path, dict[str, Any]] = {
            MANIFEST_PATH.resolve(): self.manifest
        }
        for entry in self.manifest["cases"]:
            if "/ab-merge/" not in entry["manifestPath"]:
                continue
            path = CANONICAL_ROOT / entry["manifestPath"]
            self.documents[path.resolve()] = json.loads(path.read_text(encoding="utf-8"))

    def validate(self) -> list[str]:
        errors: list[str] = []

        def load_json(path: Path, _: list[str]) -> dict[str, Any]:
            return self.documents[path.resolve()]

        def validate_entry(
            _: Path,
            entry: dict[str, Any],
            __: list[str],
            **___: Any,
        ) -> PurePosixPath:
            return PurePosixPath(entry["path"])

        VALIDATOR.validate_ab_merge_golden_fixtures(
            ROOT,
            load_json,
            validate_entry,
            errors,
        )
        return errors

    def test_accepts_the_closed_owner_approved_fixture_inventory(self) -> None:
        self.assertEqual([], self.validate())

    def test_rejects_nt51932_fact_scoped_alias_removal(self) -> None:
        direct_case = self.case("nt51929-ab-t05-d06")
        direct_case["evidenceApplicability"]["factScopedAliasMemberIds"].remove(
            "NT51932"
        )

        errors = self.validate()

        self.assertTrue(
            any("factScopedAliasMemberIds" in error for error in errors),
            errors,
        )

    def test_rejects_nt51951_fact_scoped_alias_removal(self) -> None:
        direct_case = self.case("nt51950-ab-boe-d82t80")
        direct_case["evidenceApplicability"]["factScopedAliasMemberIds"].clear()

        errors = self.validate()

        self.assertTrue(
            any("factScopedAliasMemberIds" in error for error in errors),
            errors,
        )

    def test_rejects_nt51932_canonical_alias_source_drift(self) -> None:
        alias_case = self.case("nt51932-ab-t05-d06-alias")
        alias_case["alias"]["sourceCaseId"] = "nt51950-ab-boe-d82t80"

        errors = self.validate()

        self.assertTrue(any("source drift" in error for error in errors), errors)

    def test_rejects_nt51951_workflow_alias_scope_promotion(self) -> None:
        alias_case = self.case("nt51951-ab-boe-d82t80-workflow-alias")
        alias_case["alias"]["factScope"][-1] = (
            "direct NT51951 product bytes and runtime support"
        )

        errors = self.validate()

        self.assertTrue(any("factScope drift" in error for error in errors), errors)

    def test_rejects_nt51929_first_half_promoted_to_single_golden(self) -> None:
        evidence = self.case("nt51929-ab-t05-d06")[
            "ctrlRamFirstHalfSelfReplacementEvidence"
        ]
        evidence["standaloneSingleGolden"] = True
        evidence["fullByteParity"] = True

        errors = self.validate()

        self.assertTrue(
            any("CtrlRAM first-half evidence drift" in error for error in errors),
            errors,
        )

    def test_rejects_nt51950_reference_configuration_drift(self) -> None:
        self.case("nt51950-ab-boe-d82t80")["referenceParity"][
            "configuration"
        ] = "51951"

        errors = self.validate()

        self.assertTrue(
            any("referenceParity drift" in error for error in errors), errors
        )

    def case(self, case_id: str) -> dict[str, Any]:
        entry = next(
            entry for entry in self.manifest["cases"] if entry["caseId"] == case_id
        )
        return self.documents[(CANONICAL_ROOT / entry["manifestPath"]).resolve()]


if __name__ == "__main__":
    unittest.main()

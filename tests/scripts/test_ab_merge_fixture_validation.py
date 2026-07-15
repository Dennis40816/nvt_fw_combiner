"""Behavioral tests for the closed AB golden evidence validator."""

from __future__ import annotations

import importlib.util
import json
from copy import deepcopy
from pathlib import Path, PurePosixPath
from types import ModuleType
from typing import Any
import unittest

ROOT = Path(__file__).resolve().parents[2]
VALIDATOR_PATH = ROOT / "scripts" / "ab_merge_fixture_validation.py"
MANIFEST_PATH = ROOT / "testdata" / "golden" / "ab-merge" / "manifest.json"


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

    def validate(self, manifest: dict[str, Any]) -> list[str]:
        errors: list[str] = []

        def load_json(_: Path, __: list[str]) -> dict[str, Any]:
            return manifest

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
        self.assertEqual([], self.validate(self.manifest))

    def test_rejects_nt51929_evidence_reused_as_nt51932_golden(self) -> None:
        manifest = deepcopy(self.manifest)
        manifest["cases"][0]["evidenceApplicability"]["notEstablishedMemberIds"] = []

        errors = self.validate(manifest)

        self.assertTrue(
            any("notEstablishedMemberIds" in error for error in errors),
            errors,
        )

    def test_rejects_nt51950_reference_configuration_drift(self) -> None:
        manifest = deepcopy(self.manifest)
        manifest["cases"][1]["referenceParity"]["configuration"] = "51951"

        errors = self.validate(manifest)

        self.assertTrue(
            any("referenceParity drift" in error for error in errors), errors
        )


if __name__ == "__main__":
    unittest.main()

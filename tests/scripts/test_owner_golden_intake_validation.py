"""Mutation tests for the exact 2026-07-18 owner golden intake contract."""

from __future__ import annotations

import copy
import json
import unittest
from pathlib import Path, PurePosixPath
from typing import Any, Callable

from scripts.owner_golden_intake_validation import (
    EXPECTED_20260718_CASES,
    verify_20260718_provenance,
    verify_20260718_phase_b_results,
    verify_exact_cases,
    verify_owner_golden_intake_20260718,
)


ROOT = Path(__file__).resolve().parents[2]
GOLDEN_ROOT = ROOT / "testdata/golden/ctrlram-replace"
MANIFEST_PATH = GOLDEN_ROOT / "manifest.20260718.json"


class OwnerGoldenIntakeValidationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.document = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
        self.declared_files = {
            PurePosixPath(entry["path"])
            for collection in ("payloads", "supportingFiles")
            for entry in self.document[collection]
        }

    def test_final_intake_is_valid(self) -> None:
        verify_owner_golden_intake_20260718(GOLDEN_ROOT)

    def test_exact_case_metadata_mutations_are_rejected(self) -> None:
        mutations: tuple[tuple[str, Callable[[dict[str, Any]], None]], ...] = (
            ("pid", lambda case: case.__setitem__("pid", "0xFFFF")),
            ("project", lambda case: case.__setitem__("project", "AUTO_PRJ-other")),
            ("topology", lambda case: case.__setitem__("topology", "cascade")),
            ("icCount", lambda case: case.__setitem__("icCount", 2)),
            (
                "baseKind",
                lambda case: case.__setitem__("baseKind", "direct-reference-flashcode"),
            ),
            (
                "profileId",
                lambda case: case["currentProfile"].__setitem__(
                    "profileId", "swapped-profile"
                ),
            ),
        )
        for label, mutate in mutations:
            with self.subTest(label=label):
                candidate = copy.deepcopy(self.document)
                mutate(candidate["cases"][0])
                with self.assertRaises(ValueError):
                    self.verify_cases(candidate)

    def test_payload_case_attribution_swap_is_rejected(self) -> None:
        candidate = copy.deepcopy(self.document)
        original_case_id = candidate["payloads"][0]["caseId"]
        candidate["payloads"][0]["caseId"] = next(
            case["caseId"]
            for case in candidate["cases"]
            if case["caseId"] != original_case_id
        )
        with self.assertRaisesRegex(ValueError, "attributed to a different caseId"):
            self.verify_cases(candidate)

    def test_engineering_gate_mutation_is_rejected(self) -> None:
        candidate = copy.deepcopy(self.document)
        candidate["cases"][0]["engineeringGateIds"] = ["unrelated-nonempty-gate"]
        with self.assertRaisesRegex(ValueError, "engineeringGateIds drifted"):
            self.verify_cases(candidate)

    def test_source_archive_identity_mutation_is_rejected(self) -> None:
        candidate = copy.deepcopy(self.document)
        candidate["sourceArchive"]["sha256"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "source archive provenance drifted"):
            verify_20260718_provenance(candidate)

    def test_external_tool_observation_mutation_is_rejected(self) -> None:
        candidate = copy.deepcopy(self.document)
        candidate["externalToolObservations"][2]["files"][0]["sha256"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "external tool observations drifted"):
            verify_20260718_provenance(candidate)

    def test_nt51926_phase_b_result_mutation_is_rejected(self) -> None:
        candidate = copy.deepcopy(self.document)
        candidate["cases"][0]["phaseBResult"]["differenceCounts"]["ownerToV2"] = 0
        with self.assertRaisesRegex(ValueError, "Phase B result drifted"):
            verify_20260718_phase_b_results(candidate)

    def verify_cases(self, document: dict[str, Any]) -> None:
        verify_exact_cases(
            document,
            self.declared_files,
            "20260718",
            EXPECTED_20260718_CASES,
        )


if __name__ == "__main__":
    unittest.main()

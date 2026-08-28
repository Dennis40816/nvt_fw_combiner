"""Contract tests for the canonicalized direct 2026-07-17 CtrlRAM cases."""

from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.canonical_golden_validation import validate_canonical_golden


ROOT = Path(__file__).resolve().parents[2]
CANONICAL_ROOT = ROOT / "testdata/golden/canonical"
LEGACY_MANIFEST = ROOT / "testdata/golden/ctrlram-replace/manifest.20260717.json"
DIRECT_CASES = {
    "nt51923-fw141-single-auto-prj-662-20260717": (
        "NT51923",
        "fw1.4.1",
        "single",
        "AUTO_PRJ-662",
        9,
        "a65ae33c9c11091f69d8935422ffc57db32262eb922590364d4bdd9c3af9916f",
    ),
    "nt51923-fw141-cascade3-auto-prj-734-20260717": (
        "NT51923",
        "fw1.4.1",
        "cascade-3",
        "AUTO_PRJ-734",
        9,
        "06dda13a592c151a767d47fff60da993f33d7bda37666794dd9ea5cf92094d18",
    ),
    "nt51926-fw141-single-auto-prj-747-20260717": (
        "NT51926",
        "fw1.4.1",
        "single",
        "AUTO_PRJ-747",
        8,
        "3a222cbc838192204e8a94b1677b3564fcabfa1983820eebc89e2ef10ec294cd",
    ),
    "nt51926-fw141-cascade2-auto-prj-597-20260717": (
        "NT51926",
        "fw1.4.1",
        "cascade-2",
        "AUTO_PRJ-597",
        9,
        "acdfb0a03c41d6b6e40d1e8f0f6ed72f7b82d5330aa3c13cc6269c930f7d016c",
    ),
    "nt51927-fw141-single-auto-prj-529-20260717": (
        "NT51927",
        "fw1.4.1",
        "single",
        "AUTO_PRJ-529",
        8,
        "fc4d2f9701c626b1c7cddd2b448970611d332295c64f86415af2855f1569c55a",
    ),
    "nt51929-fw200-single-auto-prj-594-20260717": (
        "NT51929",
        "fw2.0.0",
        "single",
        "AUTO_PRJ-594",
        7,
        "d3c958d2aac1e29bd1f88b8ac62dc74c36810ab11e707770199d4b34f5ce3910",
    ),
    "nt51950-fw200-single-auto-prj-676-20260717": (
        "NT51950",
        "fw2.0.0",
        "single",
        "AUTO_PRJ-676",
        7,
        "ccda75d0aa08540e293f9ab4a8058c43c4e39d2dd0238238848a2f13df68e38e",
    ),
}


class CtrlRamCanonical20260717Tests(unittest.TestCase):
    def setUp(self) -> None:
        self.root_manifest = json.loads(
            (CANONICAL_ROOT / "manifest.json").read_text(encoding="utf-8")
        )
        paths = {
            entry["caseId"]: entry["manifestPath"]
            for entry in self.root_manifest["cases"]
        }
        self.cases = {
            case_id: json.loads(
                (CANONICAL_ROOT / paths[case_id]).read_text(encoding="utf-8")
            )
            for case_id in DIRECT_CASES
        }

    def test_canonical_inventory_remains_closed(self) -> None:
        errors: list[str] = []
        validate_canonical_golden(ROOT, errors)
        self.assertEqual([], errors)

    def test_direct_case_facts_and_expected_hashes_are_exact(self) -> None:
        for case_id, expected in DIRECT_CASES.items():
            with self.subTest(case_id=case_id):
                ic, version, topology, project, artifact_count, expected_hash = expected
                case = self.cases[case_id]
                self.assertTrue(case["directGolden"])
                self.assertEqual("ctrlram-replace", case["workflow"])
                self.assertEqual(ic, case["ic"])
                self.assertEqual(version, case["variantOrVersion"])
                self.assertEqual(topology, case["topology"])
                self.assertEqual(project, case["project"])
                self.assertEqual(artifact_count, len(case["artifacts"]))
                expected_artifacts = [
                    artifact
                    for artifact in case["artifacts"]
                    if artifact["role"] == "expected"
                ]
                self.assertEqual(1, len(expected_artifacts))
                self.assertEqual(expected_hash, expected_artifacts[0]["sha256"])
                self.assertFalse(case["promotion"]["runtimeSupportPromotion"])
                self.assertFalse(case["promotion"]["releaseRedistributionApproved"])

    def test_physical_inventory_and_source_archive_are_exact(self) -> None:
        artifacts = [
            artifact for case in self.cases.values() for artifact in case["artifacts"]
        ]
        self.assertEqual(57, len(artifacts))
        self.assertEqual(51, sum(artifact["path"].endswith(".bin") for artifact in artifacts))
        self.assertEqual(13_871_204, sum(artifact["size"] for artifact in artifacts))
        collection = next(
            item
            for item in self.root_manifest["sourceCollections"]
            if item["legacyRoot"].endswith("fixtures/20260717")
        )
        self.assertEqual(
            "9027e9038f51c5421f922afdf73d1f16fc5f7dd582e23e25547727fda585205c",
            collection["source"]["sha256"],
        )

    def test_legacy_manifest_now_contains_only_remaining_diagnostics(self) -> None:
        legacy = json.loads(LEGACY_MANIFEST.read_text(encoding="utf-8"))
        self.assertEqual(set(DIRECT_CASES), set(legacy["canonicalDirectCases"]))
        self.assertEqual(31, len(legacy["payloads"]))
        self.assertEqual(2, len(legacy["supportingFiles"]))
        migrated_paths = {
            path
            for case in self.cases.values()
            for artifact in case["artifacts"]
            for path in artifact["legacyPaths"]
        }
        remaining_paths = {
            "testdata/golden/ctrlram-replace/" + item["path"]
            for item in [*legacy["payloads"], *legacy["supportingFiles"]]
        }
        self.assertTrue(migrated_paths.isdisjoint(remaining_paths))

    def test_nt51929_ab_expected_remains_a_separate_workflow_golden(self) -> None:
        ctrlram_case = self.cases["nt51929-fw200-single-auto-prj-594-20260717"]
        self.assertNotIn(
            "expected-final-output-ab",
            {artifact["sourceRole"] for artifact in ctrlram_case["artifacts"]},
        )
        ab_path = next(
            entry["manifestPath"]
            for entry in self.root_manifest["cases"]
            if entry["caseId"] == "nt51929-ab-t05-d06"
        )
        ab_case = json.loads((CANONICAL_ROOT / ab_path).read_text(encoding="utf-8"))
        ab_expected = [
            artifact for artifact in ab_case["artifacts"] if artifact["role"] == "expected"
        ]
        self.assertEqual(1, len(ab_expected))
        self.assertEqual(
            "c7e1e263ac8ca70f83a6f66fa268da4aa9be37c2c822a39d58fa9c153d66abe2",
            ab_expected[0]["sha256"],
        )

    def test_long_original_names_remain_provenance_not_physical_path_risk(self) -> None:
        case = self.cases["nt51923-fw141-cascade3-auto-prj-734-20260717"]
        shortened = {
            artifact["path"].rsplit("/", 1)[-1]: artifact["originalFileName"]
            for artifact in case["artifacts"]
        }
        self.assertIn("nt51923-expected-output.bin", shortened)
        self.assertIn("nt51923-dp-input.bin", shortened)
        self.assertIn("WhitePoint", shortened["nt51923-expected-output.bin"])
        self.assertIn("WhitePoint", shortened["nt51923-dp-input.bin"])


if __name__ == "__main__":
    unittest.main()

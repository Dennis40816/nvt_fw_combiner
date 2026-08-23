"""Contracts for 2026-07-05 CtrlRAM direct evidence and fact aliases."""

from __future__ import annotations

import json
import unittest
from pathlib import Path

from scripts.canonical_golden_validation import validate_canonical_golden


ROOT = Path(__file__).resolve().parents[2]
CANONICAL_ROOT = ROOT / "testdata/golden/canonical"
LEGACY_ROOT = ROOT / "testdata/golden/ctrlram-replace"
DIRECT_CASES = {
    "nt51927-2chip-self-20260705": ("fw1.3.2", "cascade-2", 7, "0x1615"),
    "nt51927-3chip-self-20260705": ("fw1.4.0", "cascade-3", 9, "0x570A"),
}
ALIASES = {
    "nt51917-fw141-single-nt51927-alias": (
        "NT51917",
        "nt51927-fw141-single-auto-prj-529-20260717",
    ),
    "nt51917-fw132-cascade2-nt51927-alias": (
        "NT51917",
        "nt51927-2chip-self-20260705",
    ),
    "nt51917-fw140-cascade3-nt51927-alias": (
        "NT51917",
        "nt51927-3chip-self-20260705",
    ),
    "nt51919-fw200-single-nt51929-alias": (
        "NT51919",
        "nt51929-fw200-single-auto-prj-594-20260717",
    ),
    "nt51928-fw132-non-nb-cascade2-nt51927-alias": (
        "NT51928",
        "nt51927-2chip-self-20260705",
    ),
}


class CtrlRamCanonical20260705Tests(unittest.TestCase):
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
            for case_id in {*DIRECT_CASES, *ALIASES}
        }

    def test_canonical_inventory_remains_closed(self) -> None:
        errors: list[str] = []
        validate_canonical_golden(ROOT, errors)
        self.assertEqual([], errors)

    def test_two_nt51927_cases_are_input_only_direct_evidence(self) -> None:
        artifacts = []
        for case_id, expected in DIRECT_CASES.items():
            with self.subTest(case_id=case_id):
                version, topology, count, pid = expected
                case = self.cases[case_id]
                self.assertFalse(case["directGolden"])
                self.assertTrue(case["directEvidence"])
                self.assertEqual("NT51927", case["ic"])
                self.assertEqual("ctrlram-replace", case["workflow"])
                self.assertEqual(version, case["variantOrVersion"])
                self.assertEqual(topology, case["topology"])
                self.assertEqual(pid, case["pid"])
                self.assertEqual(count, len(case["artifacts"]))
                self.assertEqual({"input"}, {item["role"] for item in case["artifacts"]})
                self.assertNotIn("legacyCaseFacts", case)
                self.assertEqual(
                    1,
                    sum(item["slotId"] == "replace-base" for item in case["artifacts"]),
                )
                self.assertFalse(case["promotion"]["runtimeSupportPromotion"])
                self.assertFalse(case["promotion"]["releaseRedistributionApproved"])
                artifacts.extend(case["artifacts"])
        self.assertEqual(16, len(artifacts))
        self.assertEqual(16, sum(item["path"].endswith(".bin") for item in artifacts))
        self.assertEqual(663_456, sum(item["size"] for item in artifacts))

    def test_aliases_are_same_workflow_fact_records_without_payloads(self) -> None:
        for case_id, expected in ALIASES.items():
            with self.subTest(case_id=case_id):
                ic, source_case_id = expected
                case = self.cases[case_id]
                self.assertFalse(case["directGolden"])
                self.assertNotIn("directEvidence", case)
                self.assertEqual(ic, case["ic"])
                self.assertEqual("ctrlram-replace", case["workflow"])
                self.assertEqual(source_case_id, case["alias"]["sourceCaseId"])
                self.assertNotIn("artifacts", case)
                self.assertTrue(case["alias"]["factScope"])
                self.assertTrue(case["alias"]["evidenceRefs"])
                self.assertFalse(case["promotion"]["runtimeSupportPromotion"])
                self.assertFalse(case["promotion"]["releaseRedistributionApproved"])

    def test_nt51928_alias_explicitly_excludes_byte_reuse_and_nb(self) -> None:
        case = self.cases["nt51928-fw132-non-nb-cascade2-nt51927-alias"]
        scope = " ".join(case["alias"]["factScope"])
        self.assertIn("no reference or output bytes are aliased", scope)
        self.assertIn("non-NB", scope)
        self.assertIn("512 KiB", scope)
        self.assertIn("NB is explicitly excluded", case["ownerApproval"])

    def test_active_legacy_fixture_authority_is_retired(self) -> None:
        self.assertFalse((LEGACY_ROOT / "manifest.json").exists())
        self.assertFalse((LEGACY_ROOT / "manifest.template.json").exists())
        self.assertFalse((LEGACY_ROOT / "fixtures/20260705").exists())
        self.assertFalse((LEGACY_ROOT / "fixtures/derived").exists())
        self.assertTrue((LEGACY_ROOT / "manifest.20260717.json").is_file())
        self.assertTrue((LEGACY_ROOT / "fixtures/20260717").is_dir())

    def test_source_archive_identity_is_pinned(self) -> None:
        collection = next(
            item
            for item in self.root_manifest["sourceCollections"]
            if item["legacyRoot"].endswith("fixtures/20260705")
        )
        self.assertEqual(
            "f4b9a72374e66fd09a03fd874f8c7e9e8bf9fa3710f67b9e0211c23b939f021e",
            collection["source"]["sha256"],
        )


if __name__ == "__main__":
    unittest.main()

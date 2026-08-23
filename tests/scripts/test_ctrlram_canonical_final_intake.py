"""Contract tests for the canonicalized final 2026-07-18 CtrlRAM intake."""

from __future__ import annotations

import hashlib
import json
import unittest
from pathlib import Path

from scripts.canonical_golden_validation import validate_canonical_golden


ROOT = Path(__file__).resolve().parents[2]
CANONICAL_ROOT = ROOT / "testdata/golden/canonical"
FINAL_CASES = {
    "nt51926-fw200-single-auto-prj-597-20260718": (
        "NT51926",
        "fw2.0.0",
        "single",
        "AUTO_PRJ-597",
        8,
        "bf4221635b58a33bff6875aacfb29636aa140354cb5ec5256bf2b0c09e9cc81c",
    ),
    "nt51926-fw200-cascade3-auto-prj-597-20260718": (
        "NT51926",
        "fw2.0.0",
        "cascade-3",
        "AUTO_PRJ-597",
        8,
        "2521192e6a846c8beeb49395e98977d243053efd292b094b31272fff70825825",
    ),
    "nt51932-fw200-cascade3-auto-prj-525-20260718": (
        "NT51932",
        "fw2.0.0",
        "cascade-3",
        "AUTO_PRJ-525",
        23,
        "3eb556e0a9323dd4fbe4c703be1eb33679df2b1ba839e79ddd7bbffa235008fd",
    ),
    "nt51951-fw200-single-auto-prj-695-20260718": (
        "NT51951",
        "fw2.0.0",
        "single",
        "AUTO_PRJ-695",
        6,
        "c1cd54d93af431727220adc37fec2488765909dc09cb917d1ff69f6087bb6b69",
    ),
}


class CtrlRamCanonicalFinalIntakeTests(unittest.TestCase):
    def setUp(self) -> None:
        root_manifest = json.loads(
            (CANONICAL_ROOT / "manifest.json").read_text(encoding="utf-8")
        )
        paths = {
            entry["caseId"]: entry["manifestPath"]
            for entry in root_manifest["cases"]
        }
        self.cases = {
            case_id: json.loads(
                (CANONICAL_ROOT / paths[case_id]).read_text(encoding="utf-8")
            )
            for case_id in FINAL_CASES
        }
        self.root_manifest = root_manifest

    def test_canonical_inventory_is_closed_and_hash_pinned(self) -> None:
        errors: list[str] = []
        validate_canonical_golden(ROOT, errors)
        self.assertEqual([], errors)

    def test_retirement_preserves_every_surviving_case_and_artifact_fact(self) -> None:
        retired_ics = {"NT51920", "NT51930", "NT51931"}
        cases = [
            json.loads((CANONICAL_ROOT / entry["manifestPath"]).read_text(encoding="utf-8"))
            for entry in self.root_manifest["cases"]
        ]
        self.assertTrue(retired_ics.isdisjoint(case["ic"] for case in cases))

        cases.sort(key=lambda case: case["caseId"])
        artifact_facts = sorted(
            (
                case["caseId"],
                artifact["artifactId"],
                artifact["role"],
                artifact["path"],
                artifact["size"],
                artifact["sha256"],
            )
            for case in cases
            for artifact in case.get("artifacts", [])
        )

        def normalized_sha256(value: object) -> str:
            payload = json.dumps(
                value,
                sort_keys=True,
                separators=(",", ":"),
                ensure_ascii=False,
            ).encode("utf-8")
            return hashlib.sha256(payload).hexdigest()

        self.assertEqual(39, len(cases))
        self.assertEqual(
            "d12ac7ea8c2ed5295ee5e2c7f52c819ce33e899887e5c54ecd130c547ac43735",
            normalized_sha256(cases),
        )
        self.assertEqual(174, len(artifact_facts))
        self.assertEqual(
            "e11c7817c85f844ef1112bd9c2bfaa4a1b157e68d506ccc30af992d140508252",
            normalized_sha256(artifact_facts),
        )

    def test_final_case_facts_and_expected_hashes_are_exact(self) -> None:
        for case_id, expected in FINAL_CASES.items():
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
                expected_artifact = expected_artifacts[0]
                self.assertEqual(expected_hash, expected_artifact["sha256"])
                self.assertEqual(
                    expected_hash,
                    case["phaseBResult"]["ownerExpectedSha256"],
                )
                self.assertTrue(
                    all(
                        legacy.startswith(
                            "testdata/golden/ctrlram-replace/fixtures/20260718/"
                        )
                        for artifact in case["artifacts"]
                        for legacy in artifact["legacyPaths"]
                    )
                )

    def test_final_archive_provenance_and_physical_inventory_are_exact(self) -> None:
        collection = next(
            item
            for item in self.root_manifest["sourceCollections"]
            if item["legacyRoot"].endswith("fixtures/20260718")
        )
        self.assertEqual(
            "da32ae0acebcd89a5c2b548cd4e0863620cfc774010751f6e826bc9cbc0f4351",
            collection["source"]["sha256"],
        )
        artifacts = [
            artifact for case in self.cases.values() for artifact in case["artifacts"]
        ]
        self.assertEqual(45, len(artifacts))
        self.assertEqual(7_727_759, sum(artifact["size"] for artifact in artifacts))

    def test_recovered_tools_remain_observations_not_payloads(self) -> None:
        observations = [
            observation
            for case in self.cases.values()
            for observation in case.get("externalToolObservations", [])
        ]
        self.assertEqual(1, len(observations))
        self.assertTrue(
            all(
                artifact["originalFileName"].lower()
                not in {"combiner.exe", "diffnfmerge.exe", "commandline.dll"}
                for case in self.cases.values()
                for artifact in case["artifacts"]
            )
        )


if __name__ == "__main__":
    unittest.main()

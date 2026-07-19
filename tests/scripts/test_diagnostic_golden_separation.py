"""Contracts for repository-only golden diagnostic separation."""

from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from scripts.canonical_golden_validation import (
    validate_diagnostic_golden_separation,
)


ROOT = Path(__file__).resolve().parents[2]


class DiagnosticGoldenSeparationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)
        self.diagnostics = self.root / "testdata/diagnostics/golden-evidence"
        self.legacy = self.root / "testdata/golden/ctrlram-replace"
        self.artifact_root = self.legacy / "fixtures/20260717"
        self.handoff = self.root / "testdata/golden/owner-handoff"

        self.payload = self.write_bytes(self.artifact_root / "payload.bin", b"diagnostic")
        self.note = self.write_bytes(self.artifact_root / "note.txt", b"note")
        self.write_bytes(self.diagnostics / "README.md", b"diagnostics\n")
        record = self.write_bytes(self.diagnostics / "records/control.json", b"{}\n")
        self.write_bytes(self.handoff / "CASE.md", b"handoff\n")

        source_sha = "a" * 64
        self.inventory = {
            "schemaVersion": "0.1",
            "payloadClass": "repository-only-diagnostic-evidence",
            "binaryPayloadsIncluded": True,
            "canonicalExpected": False,
            "runtimeSupportPromotion": False,
            "releaseRedistributionApproved": False,
            "sourceArchive": {"sha256": source_sha},
            "payloads": [self.item("fixtures/20260717/payload.bin", self.payload)],
            "supportingFiles": [
                self.item("fixtures/20260717/note.txt", self.note)
            ],
        }
        self.write_json(self.legacy / "manifest.20260717.json", self.inventory)
        self.manifest = {
            "schemaVersion": "1.0",
            "payloadClass": "repository-only-diagnostic-evidence",
            "canonicalExpected": False,
            "runtimeSupportPromotion": False,
            "releaseRedistributionApproved": False,
            "physicalPayloadMovement": "frozen-after-9e15bc0f",
            "records": [self.item("records/control.json", record)],
            "ctrlRamLegacyQuarantine": {
                "artifactRoot": "testdata/golden/ctrlram-replace/fixtures/20260717",
                "inventoryManifest": "testdata/golden/ctrlram-replace/manifest.20260717.json",
                "payloadCount": 1,
                "supportingFileCount": 1,
                "sourceArchiveSha256": source_sha,
            },
            "ownerHandoff": {
                "root": "testdata/golden/owner-handoff",
                "fileCount": 1,
                "treeSha256": self.tree_hash(self.handoff),
                "binaryPayloadsIncluded": False,
            },
            "exclusions": ["canonical expected"],
        }
        self.write_manifest()

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    @staticmethod
    def write_bytes(path: Path, payload: bytes) -> bytes:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(payload)
        return payload

    @staticmethod
    def write_json(path: Path, document: object) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")

    @staticmethod
    def item(path: str, payload: bytes) -> dict[str, object]:
        return {
            "path": path,
            "size": len(payload),
            "sha256": hashlib.sha256(payload).hexdigest(),
        }

    @staticmethod
    def tree_hash(root: Path) -> str:
        records = []
        for path in sorted(
            (item for item in root.rglob("*") if item.is_file()),
            key=lambda item: item.relative_to(root).as_posix(),
        ):
            payload = path.read_bytes()
            records.append(
                f"{path.relative_to(root).as_posix()}\0{len(payload)}\0"
                f"{hashlib.sha256(payload).hexdigest()}\n"
            )
        return hashlib.sha256("".join(records).encode("utf-8")).hexdigest()

    def write_manifest(self) -> None:
        self.write_json(self.diagnostics / "manifest.json", self.manifest)

    def validate(self) -> list[str]:
        errors: list[str] = []
        validate_diagnostic_golden_separation(self.root, errors)
        return errors

    def test_accepts_closed_repository_only_quarantine(self) -> None:
        self.assertEqual([], self.validate())

    def test_rejects_diagnostic_payload_hash_drift(self) -> None:
        self.payload = self.write_bytes(self.artifact_root / "payload.bin", b"changed")
        self.assertTrue(
            any("diagnostic artifact SHA-256 mismatch" in error for error in self.validate())
        )

    def test_rejects_unlisted_diagnostic_file(self) -> None:
        self.write_bytes(self.artifact_root / "extra.bin", b"extra")
        self.assertIn(
            "diagnostic CtrlRAM artifact root differs from its closed inventory",
            self.validate(),
        )

    def test_rejects_canonical_expected_promotion(self) -> None:
        self.manifest["canonicalExpected"] = True
        self.write_manifest()
        self.assertIn(
            "diagnostic golden manifest canonicalExpected must be False",
            self.validate(),
        )

    def test_rejects_frozen_authority_path_drift(self) -> None:
        cases = (
            (
                self.manifest["ctrlRamLegacyQuarantine"],
                "artifactRoot",
                "testdata/golden/ctrlram-replace/fixtures/other",
                "diagnostic CtrlRAM artifactRoot must remain frozen",
            ),
            (
                self.manifest["ctrlRamLegacyQuarantine"],
                "inventoryManifest",
                "testdata/golden/ctrlram-replace/other.json",
                "diagnostic CtrlRAM inventoryManifest must remain frozen",
            ),
            (
                self.manifest["ownerHandoff"],
                "root",
                "testdata/golden/other-handoff",
                "diagnostic ownerHandoff root must remain frozen",
            ),
        )
        for section, key, drifted_path, expected_error in cases:
            with self.subTest(key=key):
                original = section[key]
                section[key] = drifted_path
                self.write_manifest()
                self.assertTrue(
                    any(expected_error in error for error in self.validate())
                )
                section[key] = original

    def test_rejects_symlink_directory_in_quarantine(self) -> None:
        outside = self.root / "outside"
        outside.mkdir()
        link = self.artifact_root / "linked-directory"
        try:
            link.symlink_to(outside, target_is_directory=True)
        except OSError as error:
            self.skipTest(f"directory symlinks are unavailable: {error}")
        self.assertTrue(
            any(
                "artifact root cannot contain symlinks" in error
                for error in self.validate()
            )
        )

    def test_rejects_binary_owner_handoff(self) -> None:
        self.write_bytes(self.handoff / "firmware.bin", b"firmware")
        self.manifest["ownerHandoff"]["fileCount"] = 2
        self.manifest["ownerHandoff"]["treeSha256"] = self.tree_hash(self.handoff)
        self.write_manifest()
        self.assertTrue(
            any("ownerHandoff contains a binary" in error for error in self.validate())
        )

    def test_rejects_owner_handoff_content_drift(self) -> None:
        self.write_bytes(self.handoff / "CASE.md", b"changed\n")
        self.assertIn(
            "diagnostic ownerHandoff treeSha256 does not match its closed inventory",
            self.validate(),
        )

    def test_repository_diagnostics_are_closed(self) -> None:
        errors: list[str] = []
        validate_diagnostic_golden_separation(ROOT, errors)
        self.assertEqual([], errors)


if __name__ == "__main__":
    unittest.main()

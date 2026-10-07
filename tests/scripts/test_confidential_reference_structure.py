"""Confidential source files cannot re-enter the tracked public tree."""

import json
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from scripts import verify


class ConfidentialReferenceStructureTests(unittest.TestCase):
    def test_public_reference_documentation_is_allowed(self) -> None:
        for path in (
            "docs/references/ic-flashmap/README.md",
            "docs/references/confidential-references.json",
            "refcode/ab_code_combiner/clean.bat",
        ):
            with self.subTest(path=path):
                self.assertFalse(verify.is_confidential_reference_path(path))

    def test_each_confidential_pattern_is_blocked(self) -> None:
        for path in (
            "docs/references/sample.xlsx",
            "docs/references/nested/sample.bat",
            "include/sample_mmap.h",
            "src/Combiner.c",
            "src/ap_fwconfig.c",
            "provenance/ExamplePostbuildSetup.bat",
            "refcode/flashmap/README.md",
        ):
            with self.subTest(path=path):
                self.assertTrue(verify.is_confidential_reference_path(path))

    def test_root_and_case_variants_are_blocked(self) -> None:
        for path in ("Combiner.c", "AP_FWCONFIG.C", "MMAP.h", "docs/references/A.XLSX"):
            with self.subTest(path=path):
                self.assertTrue(verify.is_confidential_reference_path(path))

    def test_tracked_confidential_file_fails_until_removed_from_index(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            subprocess.run(["git", "init", "--quiet"], cwd=root, check=True)
            source = root / "docs/references/example.xlsx"
            source.parent.mkdir(parents=True)
            source.write_bytes(b"synthetic reference")
            subprocess.run(["git", "add", "."], cwd=root, check=True)
            with patch.object(verify, "ROOT", root):
                with self.assertRaisesRegex(RuntimeError, "held privately"):
                    verify.verify_confidential_reference_paths()
                source.unlink()
                # The structure check reads the Git index, so an unstaged deletion is still refused.
                with self.assertRaisesRegex(RuntimeError, "held privately"):
                    verify.verify_confidential_reference_paths()
                subprocess.run(["git", "rm", "--cached", "--quiet", "docs/references/example.xlsx"], cwd=root, check=True)
                verify.verify_confidential_reference_paths()

    def test_git_inventory_failure_is_not_a_pass(self) -> None:
        with patch.object(subprocess, "check_output", side_effect=subprocess.CalledProcessError(1, "git")):
            with self.assertRaises(subprocess.CalledProcessError):
                verify.verify_confidential_reference_paths()

    def test_public_manifest_is_valid(self) -> None:
        verify.verify_confidential_reference_manifest()

    def test_manifest_rejects_private_names_and_invalid_identity(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest_path = root / "docs/references/confidential-references.json"
            manifest_path.parent.mkdir(parents=True)
            schema_path = root / "docs/contracts/confidential-references-v1.schema.json"
            schema_path.parent.mkdir(parents=True)
            schema_path.write_bytes((verify.ROOT / "docs/contracts/confidential-references-v1.schema.json").read_bytes())
            entry = {"id": "synthetic-reference", "sizeBytes": 1, "sha256": "0" * 64, "kind": "flash-map"}
            invalid_entries = [
                [{**entry, "path": "private/example.xlsx"}],
                [{**entry, "id": "../example"}],
                [{**entry, "sha256": "invalid"}],
                [{**entry, "sizeBytes": True}],
                [{**entry, "kind": "unknown"}],
                [entry, entry],
            ]
            for entries in invalid_entries:
                with self.subTest(entries=entries):
                    manifest_path.write_text(json.dumps({"schemaVersion": 1, "entries": entries}), encoding="utf-8")
                    with patch.object(verify, "ROOT", root):
                        with self.assertRaisesRegex(RuntimeError, "confidential reference"):
                            verify.verify_confidential_reference_manifest()

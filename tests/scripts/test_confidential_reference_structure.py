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

    def test_every_intake_confidential_format_is_blocked_under_references(self) -> None:
        for path in (
            "docs/references/flashmap.xls",
            "docs/references/flashmap.xlsx",
            "docs/references/postbuild.cmd",
            "docs/references/nested/run.ps1",
            "docs/references/run.sh",
            "docs/references/header_mmap.h",
            "docs/references/ic-flashmap/mmap/sample.H",
        ):
            with self.subTest(path=path):
                self.assertTrue(verify.is_confidential_reference_path(path))

    def test_named_confidential_scripts_and_sheets_are_blocked_outside_references(self) -> None:
        for path in (
            "provenance/ExamplePostbuildSetup.cmd",
            "tools/postbuild-notes.ps1",
            "provenance/postbuild_run.sh",
            "data/ic-flashmap-layout.xls",
            "data/flash_map_export.xlsx",
            "data/tp-header.xls",
        ):
            with self.subTest(path=path):
                self.assertTrue(verify.is_confidential_reference_path(path))

    def test_ordinary_scripts_and_headers_are_allowed(self) -> None:
        for path in (
            "scripts/package.ps1",
            "scripts/run-tests.sh",
            "tools/helper.cmd",
            "src/common.h",
            "docs/references/README.md",
            "docs/references/confidential-references.json",
        ):
            with self.subTest(path=path):
                self.assertFalse(verify.is_confidential_reference_path(path))

    def test_no_currently_tracked_public_file_is_blocked(self) -> None:
        tracked = subprocess.check_output(["git", "ls-files", "--cached", "-z"], cwd=verify.ROOT).decode("utf-8")
        self.assertEqual(
            [],
            [path for path in tracked.split("\0") if path and verify.is_confidential_reference_path(path)],
        )

    def test_block_list_covers_every_intake_confidential_category(self) -> None:
        intake = verify._intake_reference_module()
        examples = {
            "postbuild-script": "provenance/ic51920_postbuild.cmd",
            "mmap-header": "provenance/ic51920_mmap.h",
            "flashmap-reference": "provenance/ic51920-flashmap.xls",
            "flash-header-reference": "provenance/tp_header.xls",
            "combiner-source-reference": "provenance/combiner-notes.sh",
        }
        self.assertEqual(set(intake.CONFIDENTIAL_KINDS), set(examples))
        for category, path in examples.items():
            with self.subTest(category=category):
                self.assertEqual(category, intake.classify(Path(path)))
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

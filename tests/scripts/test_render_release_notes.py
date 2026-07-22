"""Tests for complete stable release-note extraction."""

from __future__ import annotations

import importlib.util
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "render_release_notes.py"
SPEC = importlib.util.spec_from_file_location("render_release_notes", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def complete_section() -> str:
    return """## [0.9.14] - 2026-07-22

### Summary

This release admits one reviewed pilot and improves targeted interaction safety.

### Product changes

#### AB Code pilot

- Before → After: Hidden candidate → owner-approved pilot available.
- Affected: Merge / AB Code / NT51919, NT51929, NT51932 / Standard persona.
- Support status: Promoted only for the declared perfect family.
- Compatibility: No saved-data migration; NT51950 and NT51951 remain unavailable.
- Verification: Direct and fact-scoped golden evidence plus firmware-owner review.
- Limitations: Other AB families and global UI unification remain deferred.

### Security

Inputs remain immutable and execution stays inside declared half-open ranges.

### Known issues

Clean-machine signing remains an organizational gate.

### Upgrade and rollback

Replace the complete portable folder; rollback restores the prior untouched folder.

### Downloads and integrity

Download the Windows ZIP, SPDX SBOM, provenance, candidate manifest, and outer SHA-256 list.
"""


class RenderReleaseNotesTests(unittest.TestCase):
    def test_renders_only_requested_complete_stable_section(self) -> None:
        changelog = f"# Changelog\n\n## [Unreleased]\n\nLater work.\n\n{complete_section()}\n## [0.9.13]\n\nOld.\n"

        notes = MODULE.render_release_notes(changelog, "0.9.14")

        self.assertTrue(notes.startswith("# NVT FW Combiner v0.9.14\n"))
        self.assertIn("AB Code pilot", notes)
        self.assertNotIn("Later work.", notes)
        self.assertNotIn("Old.", notes)

    def test_rejects_every_missing_global_section(self) -> None:
        for heading in MODULE.REQUIRED_SECTIONS:
            with self.subTest(heading=heading):
                incomplete = complete_section().replace(
                    heading, f"### Removed {heading}", 1
                )
                with self.assertRaisesRegex(ValueError, "missing"):
                    MODULE.render_release_notes(incomplete, "0.9.14")

    def test_rejects_every_missing_or_empty_feature_field(self) -> None:
        for field in MODULE.REQUIRED_FEATURE_FIELDS:
            with self.subTest(field=field):
                incomplete = complete_section().replace(field, f"- Removed {field}", 1)
                with self.assertRaisesRegex(ValueError, "missing non-empty"):
                    MODULE.render_release_notes(incomplete, "0.9.14")

    def test_rejects_private_paths_and_secret_like_values(self) -> None:
        disclosures = (
            "testdata/golden/private.bin",
            "owner-handoff archive",
            "payload.7z",
            "password=example-not-a-secret",
        )
        for disclosure in disclosures:
            with self.subTest(disclosure=disclosure):
                unsafe = complete_section().replace(
                    "Clean-machine signing remains an organizational gate.", disclosure
                )
                with self.assertRaisesRegex(ValueError, "private|secret"):
                    MODULE.render_release_notes(unsafe, "0.9.14")

    def test_rejects_incomplete_markers_in_sections_and_feature_fields(self) -> None:
        for marker in ("TBD", "TODO", "FIXME", "PLACEHOLDER"):
            with self.subTest(marker=marker, location="section"):
                incomplete = complete_section().replace(
                    "Clean-machine signing remains an organizational gate.",
                    marker,
                )
                with self.assertRaisesRegex(ValueError, "incomplete release token"):
                    MODULE.render_release_notes(incomplete, "0.9.14")
            with self.subTest(marker=marker, location="feature"):
                incomplete = complete_section().replace(
                    "Direct and fact-scoped golden evidence plus firmware-owner review.",
                    marker,
                )
                with self.assertRaisesRegex(ValueError, "incomplete release token"):
                    MODULE.render_release_notes(incomplete, "0.9.14")

    def test_rejects_prerelease_and_duplicate_sections(self) -> None:
        changelog = complete_section() + "\n" + complete_section()

        with self.assertRaisesRegex(ValueError, "exactly one"):
            MODULE.render_release_notes(changelog, "0.9.14")
        with self.assertRaisesRegex(ValueError, "stable SemVer"):
            MODULE.render_release_notes(changelog, "v0.9.14")


if __name__ == "__main__":
    unittest.main()

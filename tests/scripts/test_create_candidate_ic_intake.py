"""Behavioral tests for the candidate-only IC intake command."""

from __future__ import annotations

import hashlib
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "create_candidate_ic_intake.py"
GENERATED_AT = "2026-07-15T12:00:00Z"


def sha256(content: bytes) -> str:
    return hashlib.sha256(content).hexdigest()


def manifest(artifact_content: bytes, *, status: str = "candidate") -> dict[str, Any]:
    return {
        "schemaVersion": "1.0",
        "manifestId": "nt51950-candidate-intake",
        "manifestVersion": "1.0.0",
        "status": status,
        "sourceArtifacts": [
            {
                "artifactId": "flashmap-workbook",
                "sourceKind": "workbook",
                "logicalName": "NT51950 Flash Map.xlsx",
                "contentHash": sha256(artifact_content),
                "sizeBytes": len(artifact_content),
            },
            {
                "artifactId": "owner-note",
                "sourceKind": "owner-record",
                "logicalName": "Owner note.txt",
                "contentHash": sha256(b"not-bound"),
                "sizeBytes": len(b"not-bound"),
            },
        ],
        "facts": [
            {
                "factId": "nt51950-map-observation",
                "subject": {
                    "familyId": "nt51950",
                    "memberId": "NT51950",
                    "modeId": "standard-merge",
                },
                "factKind": "range",
                "value": {
                    "kind": "statement",
                    "text": "Needs owner-reviewed normalization.",
                },
                "disposition": "unresolved",
                "promotionImpact": "blocks-execution",
                "citations": [
                    {"artifactId": "flashmap-workbook", "location": "DP Perspective!A1"}
                ],
            }
        ],
        "reviews": [],
    }


class CandidateIcIntakeTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)
        self.source_root = self.root / "source"
        self.source_root.mkdir()
        self.output = self.root / "output"
        self.output.mkdir()
        self.artifact_content = b"read-only workbook bytes"
        (self.source_root / "flashmap.xlsx").write_bytes(self.artifact_content)
        self.manifest_path = self.root / "evidence.json"
        self.write_manifest()

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    def write_manifest(self, *, status: str = "candidate") -> None:
        self.manifest_path.write_text(
            json.dumps(manifest(self.artifact_content, status=status), indent=2) + "\n",
            encoding="utf-8",
        )

    def run_command(
        self, *extra: str, artifact_binding: str = "flashmap-workbook=flashmap.xlsx"
    ) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                sys.executable,
                str(SCRIPT),
                "--evidence-manifest",
                str(self.manifest_path),
                "--source-root",
                str(self.source_root),
                "--artifact",
                artifact_binding,
                "--output",
                str(self.output),
                "--generated-at",
                GENERATED_AT,
                *extra,
            ],
            cwd=ROOT,
            text=True,
            capture_output=True,
            check=False,
        )

    def test_emits_deterministic_candidate_only_records_without_copying_artifacts(
        self,
    ) -> None:
        result = self.run_command()

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(
            {
                "candidate-bundle-rows.json",
                "candidate-evidence-manifest.json",
                "missing-evidence.json",
                "validation-report.json",
            },
            {path.name for path in self.output.iterdir()},
        )
        candidate_manifest = self.read_output("candidate-evidence-manifest.json")
        self.assertEqual(
            {
                "toolId": "candidate-ic-intake",
                "toolVersion": "1.0.0",
                "generatedAt": GENERATED_AT,
                "candidateOnly": True,
            },
            candidate_manifest["intakeProvenance"],
        )
        rows = self.read_output("candidate-bundle-rows.json")
        self.assertFalse(rows["runtimeRegistration"])
        self.assertFalse(rows["supportPromotion"])
        self.assertEqual("none", rows["rows"][0]["runtimeAuthority"])
        missing = self.read_output("missing-evidence.json")
        self.assertEqual(["owner-note"], missing["unboundArtifactIds"])
        self.assertEqual(
            ["nt51950-map-observation"], missing["blockingUnresolvedFactIds"]
        )
        report = self.read_output("validation-report.json")
        self.assertEqual("verified", report["artifactVerification"][0]["status"])
        self.assertEqual("not-bound", report["artifactVerification"][1]["status"])
        self.assertEqual(
            self.artifact_content, (self.source_root / "flashmap.xlsx").read_bytes()
        )

        replay_output = self.root / "replay-output"
        replay_output.mkdir()
        original_output = self.output
        self.output = replay_output
        replay = self.run_command()
        self.output = original_output
        self.assertEqual(0, replay.returncode, replay.stderr)
        for name in sorted(path.name for path in original_output.iterdir()):
            self.assertEqual(
                (original_output / name).read_bytes(),
                (replay_output / name).read_bytes(),
            )

    def test_rejects_hash_mismatch_without_writing_candidate_files(self) -> None:
        modified = bytearray(self.artifact_content)
        modified[-1] ^= 1
        (self.source_root / "flashmap.xlsx").write_bytes(modified)

        result = self.run_command()

        self.assertEqual(2, result.returncode)
        self.assertIn("SHA-256 does not match", result.stderr)
        self.assertEqual([], list(self.output.iterdir()))

    def test_rejects_approved_manifest_and_preserves_empty_output(self) -> None:
        self.write_manifest(status="approved")

        result = self.run_command()

        self.assertEqual(2, result.returncode)
        self.assertIn("status 'candidate'", result.stderr)
        self.assertEqual([], list(self.output.iterdir()))

    def test_rejects_unknown_evidence_fact_field_and_preserves_empty_output(
        self,
    ) -> None:
        invalid = manifest(self.artifact_content)
        invalid["facts"][0]["inferredRange"] = "forbidden"
        self.manifest_path.write_text(
            json.dumps(invalid, indent=2) + "\n", encoding="utf-8"
        )

        result = self.run_command()

        self.assertEqual(2, result.returncode)
        self.assertIn("unsupported fields", result.stderr)
        self.assertEqual([], list(self.output.iterdir()))

    def test_rejects_path_escape_office_lock_and_nonempty_output(self) -> None:
        escaped = self.run_command(
            artifact_binding="flashmap-workbook=../flashmap.xlsx"
        )
        self.assertEqual(2, escaped.returncode)
        self.assertIn("relative path without traversal", escaped.stderr)
        self.assertEqual([], list(self.output.iterdir()))

        locked = self.run_command(artifact_binding="flashmap-workbook=~$flashmap.xlsx")
        self.assertEqual(2, locked.returncode)
        self.assertIn("Office lock file", locked.stderr)
        self.assertEqual([], list(self.output.iterdir()))

        (self.output / "existing.txt").write_text("do not overwrite", encoding="utf-8")
        nonempty = self.run_command()
        self.assertEqual(2, nonempty.returncode)
        self.assertIn("must be empty", nonempty.stderr)
        self.assertEqual(
            "do not overwrite",
            (self.output / "existing.txt").read_text(encoding="utf-8"),
        )

    def test_rejects_reparse_artifact_path(self) -> None:
        link = self.source_root / "linked-flashmap.xlsx"
        try:
            os.symlink(self.source_root / "flashmap.xlsx", link)
        except OSError as exception:
            self.skipTest(f"symlink creation is unavailable: {exception}")

        result = self.run_command(
            artifact_binding="flashmap-workbook=linked-flashmap.xlsx"
        )

        self.assertEqual(2, result.returncode)
        self.assertIn("reparse point is not allowed", result.stderr)
        self.assertEqual([], list(self.output.iterdir()))

    def read_output(self, name: str) -> dict[str, Any]:
        return json.loads((self.output / name).read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()

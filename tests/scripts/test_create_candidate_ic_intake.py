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
from unittest import mock

from scripts import candidate_intake_output as output_boundary
from scripts import create_candidate_ic_intake as intake

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

    def test_rejects_a_substituted_regular_file_handle(self) -> None:
        substituted_path = self.root / "substituted.json"
        substituted_path.write_text("{}\n", encoding="utf-8")
        open_file = os.open

        def open_substituted_file(_path: Path, flags: int) -> int:
            return open_file(substituted_path, flags)

        with mock.patch.object(intake.os, "open", side_effect=open_substituted_file):
            with self.assertRaisesRegex(
                intake.IntakeError,
                "open handle does not match the validated path",
            ):
                intake.read_json(self.manifest_path)

    def test_rejects_a_path_replaced_after_initial_file_validation(self) -> None:
        original_path = self.root / "original-evidence.json"
        substituted_path = self.root / "substituted-evidence.json"
        substituted_path.write_text("{}\n", encoding="utf-8")
        open_file = os.open
        replaced = False

        def replace_then_open(path: Path, flags: int) -> int:
            nonlocal replaced
            if not replaced:
                self.manifest_path.rename(original_path)
                substituted_path.rename(self.manifest_path)
                replaced = True
            return open_file(path, flags)

        with mock.patch.object(intake.os, "open", side_effect=replace_then_open):
            with self.assertRaisesRegex(
                intake.IntakeError,
                "open handle does not match the validated path",
            ):
                intake.read_json(self.manifest_path)

    def test_interrupted_promotion_removes_every_partial_candidate_file(self) -> None:
        outputs = self.build_candidate_outputs()

        with intake.open_validated_output_directory(self.output) as output:
            publish = output.publish

            def interrupt_second_publish(
                temporary_name: str,
                final_name: str,
                descriptor: int,
                expected_content: bytes,
            ) -> None:
                if final_name == intake.OUTPUT_FILES[1]:
                    raise KeyboardInterrupt("simulated interruption")
                publish(temporary_name, final_name, descriptor, expected_content)

            with mock.patch.object(
                output,
                "publish",
                side_effect=interrupt_second_publish,
            ):
                with self.assertRaisesRegex(
                    KeyboardInterrupt, "simulated interruption"
                ):
                    intake.write_outputs(output, outputs)

            self.assertEqual({intake.OUTPUT_LOCK_FILE}, output.names())

        self.assertEqual([], list(self.output.iterdir()))

    def test_promotion_never_overwrites_a_competing_output_file(self) -> None:
        outputs = self.build_candidate_outputs()
        competing_name = intake.OUTPUT_FILES[0]
        competing_content = "preserve competing output\n"

        with self.assertRaises(FileExistsError):
            with intake.open_validated_output_directory(self.output) as output:
                publish = output.publish

                def inject_competing_file(
                    temporary_name: str,
                    final_name: str,
                    descriptor: int,
                    expected_content: bytes,
                ) -> None:
                    if final_name == competing_name:
                        (self.output / final_name).write_text(
                            competing_content,
                            encoding="utf-8",
                        )
                    publish(
                        temporary_name,
                        final_name,
                        descriptor,
                        expected_content,
                    )

                with mock.patch.object(
                    output,
                    "publish",
                    side_effect=inject_competing_file,
                ):
                    intake.write_outputs(output, outputs)

        self.assertEqual(
            competing_content,
            (self.output / competing_name).read_text(encoding="utf-8"),
        )
        self.assertEqual(
            [competing_name], [path.name for path in self.output.iterdir()]
        )

    def test_promotion_rejects_a_substituted_staged_file(self) -> None:
        outputs = self.build_candidate_outputs()
        competing_content = b"substituted staged bytes\n"

        with self.assertRaisesRegex(
            intake.IntakeError,
            "staged candidate output changed before cleanup and was preserved",
        ):
            with intake.open_validated_output_directory(self.output) as output:
                publish = output.publish

                def substitute_staged_file(
                    temporary_name: str,
                    final_name: str,
                    *publication_args: Any,
                ) -> None:
                    output.unlink(temporary_name)
                    (self.output / temporary_name).write_bytes(competing_content)
                    publish(temporary_name, final_name, *publication_args)

                with mock.patch.object(
                    output,
                    "publish",
                    side_effect=substitute_staged_file,
                ):
                    intake.write_outputs(output, outputs)

        self.assertEqual(1, len(list(self.output.iterdir())))
        self.assertEqual(competing_content, next(self.output.iterdir()).read_bytes())

    def test_promotion_rejects_in_place_staged_byte_mutation(self) -> None:
        outputs = self.build_candidate_outputs()
        mutated = False

        with self.assertRaisesRegex(
            intake.IntakeError,
            "staged candidate output changed before publication",
        ):
            with intake.open_validated_output_directory(self.output) as output:
                publish = output.publish

                def mutate_staged_bytes(
                    temporary_name: str,
                    final_name: str,
                    descriptor: int,
                    expected_content: bytes,
                ) -> None:
                    nonlocal mutated
                    if not mutated:
                        os.lseek(descriptor, 0, os.SEEK_SET)
                        os.write(descriptor, b"X" + expected_content[1:])
                        os.fsync(descriptor)
                        mutated = True
                    publish(
                        temporary_name,
                        final_name,
                        descriptor,
                        expected_content,
                    )

                with mock.patch.object(
                    output,
                    "publish",
                    side_effect=mutate_staged_bytes,
                ):
                    intake.write_outputs(output, outputs)

        self.assertEqual([], list(self.output.iterdir()))

    def test_rollback_preserves_replaced_output_and_removes_earlier_outputs(
        self,
    ) -> None:
        outputs = self.build_candidate_outputs()
        replaced_name = intake.OUTPUT_FILES[1]
        competing_content = "preserve replacement\n"

        with self.assertRaisesRegex(
            intake.IntakeError,
            "candidate output changed before cleanup and was preserved",
        ):
            with intake.open_validated_output_directory(self.output) as output:
                publish = output.publish

                def replace_then_interrupt(
                    temporary_name: str,
                    final_name: str,
                    *publication_args: Any,
                ) -> None:
                    if final_name == intake.OUTPUT_FILES[2]:
                        output.unlink(replaced_name)
                        (self.output / replaced_name).write_text(
                            competing_content,
                            encoding="utf-8",
                        )
                        raise KeyboardInterrupt("simulated interruption")
                    publish(temporary_name, final_name, *publication_args)

                with mock.patch.object(
                    output,
                    "publish",
                    side_effect=replace_then_interrupt,
                ):
                    intake.write_outputs(output, outputs)

        self.assertEqual([replaced_name], [path.name for path in self.output.iterdir()])
        self.assertEqual(
            competing_content,
            (self.output / replaced_name).read_text(encoding="utf-8"),
        )

    def test_rollback_preserves_replaced_temp_and_removes_other_staged_files(
        self,
    ) -> None:
        outputs = self.build_candidate_outputs()
        competing_content = b"preserve staged replacement\n"
        substituted_name: str | None = None

        with self.assertRaisesRegex(
            intake.IntakeError,
            "staged candidate output changed before cleanup and was preserved",
        ):
            with intake.open_validated_output_directory(self.output) as output:
                validate_identity = output.validate_identity

                def replace_then_interrupt() -> None:
                    nonlocal substituted_name
                    if substituted_name is None:
                        substituted_name = next(
                            name
                            for name in output.names()
                            if name != intake.OUTPUT_LOCK_FILE
                        )
                        output.unlink(substituted_name)
                        (self.output / substituted_name).write_bytes(competing_content)
                        raise KeyboardInterrupt("simulated interruption")
                    validate_identity()

                with mock.patch.object(
                    output,
                    "validate_identity",
                    side_effect=replace_then_interrupt,
                ):
                    intake.write_outputs(output, outputs)

        assert substituted_name is not None
        self.assertEqual(
            [substituted_name], [path.name for path in self.output.iterdir()]
        )
        self.assertEqual(
            competing_content,
            (self.output / substituted_name).read_bytes(),
        )

    @unittest.skipIf(os.name == "nt", "Unix hard-link rollback coverage")
    def test_rollback_removes_untracked_hardlinks_to_run_owned_outputs(self) -> None:
        outputs = self.build_candidate_outputs()
        hidden_link = "untracked-candidate-hardlink.json"

        with intake.open_validated_output_directory(self.output) as output:
            publish = output.publish

            def link_then_interrupt(
                temporary_name: str,
                final_name: str,
                descriptor: int,
                expected_content: bytes,
            ) -> None:
                if final_name == intake.OUTPUT_FILES[1]:
                    raise KeyboardInterrupt("simulated interruption")
                publish(
                    temporary_name,
                    final_name,
                    descriptor,
                    expected_content,
                )
                os.link(self.output / final_name, self.output / hidden_link)

            with mock.patch.object(
                output,
                "publish",
                side_effect=link_then_interrupt,
            ):
                with self.assertRaisesRegex(
                    KeyboardInterrupt,
                    "simulated interruption",
                ):
                    intake.write_outputs(output, outputs)

            self.assertEqual({intake.OUTPUT_LOCK_FILE}, output.names())

        self.assertEqual([], list(self.output.iterdir()))

    @unittest.skipIf(os.name == "nt", "Unix lock-substitution coverage")
    def test_unix_lock_cleanup_preserves_a_substituted_directory(self) -> None:
        outputs = self.build_candidate_outputs()
        replacement = self.output / intake.OUTPUT_LOCK_FILE

        with self.assertRaisesRegex(intake.IntakeError, "intake lock changed"):
            with intake.open_validated_output_directory(self.output) as output:
                intake.write_outputs(output, outputs)
                output.unlink(intake.OUTPUT_LOCK_FILE)
                replacement.mkdir()

        self.assertTrue(replacement.is_dir())
        self.assertEqual(
            {intake.OUTPUT_LOCK_FILE}, {path.name for path in self.output.iterdir()}
        )

    @unittest.skipIf(os.name == "nt", "Unix directory-fd substitution coverage")
    def test_unix_output_rejects_substitution_before_directory_open(self) -> None:
        original_output = self.root / "preopen-original-output"
        open_path = os.open
        replaced = False

        def replace_then_open(
            path: str | Path,
            flags: int,
            mode: int = 0o777,
            *,
            dir_fd: int | None = None,
        ) -> int:
            nonlocal replaced
            if not replaced and Path(path) == self.output and dir_fd is None:
                self.output.rename(original_output)
                self.output.mkdir()
                replaced = True
            return open_path(path, flags, mode, dir_fd=dir_fd)

        with mock.patch.object(
            output_boundary.os,
            "open",
            side_effect=replace_then_open,
        ):
            with self.assertRaisesRegex(
                intake.IntakeError,
                "output directory changed while validating",
            ):
                with intake.open_validated_output_directory(self.output):
                    self.fail("substituted output directory was accepted")

        self.assertEqual([], list(original_output.iterdir()))
        self.assertEqual([], list(self.output.iterdir()))

    @unittest.skipIf(os.name == "nt", "Unix directory-fd substitution coverage")
    def test_unix_output_writes_stay_bound_to_the_opened_directory(self) -> None:
        outputs = self.build_candidate_outputs()
        original_output = self.root / "original-output"

        with self.assertRaisesRegex(intake.IntakeError, "output directory changed"):
            with intake.open_validated_output_directory(self.output) as output:
                self.output.rename(original_output)
                self.output.mkdir()
                intake.write_outputs(output, outputs)

        self.assertEqual([], list(original_output.iterdir()))
        self.assertEqual([], list(self.output.iterdir()))

    @unittest.skipUnless(os.name == "nt", "Windows output-lock coverage")
    def test_windows_output_lock_blocks_directory_substitution(self) -> None:
        with intake.open_validated_output_directory(self.output) as output:
            with self.assertRaises(PermissionError):
                self.output.rename(self.root / "substituted-output")
            self.assertEqual({intake.OUTPUT_LOCK_FILE}, output.names())

        self.assertEqual([], list(self.output.iterdir()))

    def build_candidate_outputs(self) -> dict[str, dict[str, Any]]:
        candidate_manifest = manifest(self.artifact_content)
        artifacts, facts = intake.validate_manifest(candidate_manifest)
        return intake.build_outputs(
            candidate_manifest,
            facts,
            [
                {
                    "artifactId": artifact_id,
                    "status": (
                        "verified"
                        if artifact_id == "flashmap-workbook"
                        else "not-bound"
                    ),
                }
                for artifact_id in sorted(artifacts)
            ],
            GENERATED_AT,
        )

    def read_output(self, name: str) -> dict[str, Any]:
        return json.loads((self.output / name).read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()

"""Regression tests for manifest-driven candidate IC reference intake."""

from __future__ import annotations

import argparse
import contextlib
import hashlib
import io
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "intake_ic_reference.py"
sys.path.insert(0, str(ROOT / "scripts"))

import ic_reference_candidate_intake as candidate_intake
import intake_ic_reference as intake_cli


class CandidateIntakeCliTests(unittest.TestCase):
    def test_validator_constants_stay_aligned_with_contract_schemas(self) -> None:
        request_schema = json.loads(
            (ROOT / "docs" / "contracts" / "ic-reference-intake-request-v1.schema.json").read_text(
                encoding="utf-8"
            )
        )
        evidence_schema = json.loads(
            (ROOT / "docs" / "contracts" / "firmware-evidence-manifest-v1.schema.json").read_text(
                encoding="utf-8"
            )
        )

        self.assertEqual(set(request_schema["properties"]["workflow"]["enum"]), set(candidate_intake.WORKFLOWS))
        self.assertEqual(
            set(request_schema["$defs"]["sourceArtifactRequest"]["properties"]["sourceKind"]["enum"]),
            candidate_intake.SOURCE_KINDS,
        )
        self.assertEqual(
            set(evidence_schema["$defs"]["sourceArtifact"]["properties"]["sourceKind"]["enum"]),
            candidate_intake.SOURCE_KINDS,
        )
        self.assertEqual(
            set(evidence_schema["$defs"]["fact"]["properties"]["factKind"]["enum"]),
            candidate_intake.FACT_KINDS,
        )
        self.assertEqual(
            set(evidence_schema["$defs"]["fact"]["properties"]["disposition"]["enum"]),
            candidate_intake.FACT_DISPOSITIONS,
        )
        self.assertEqual(
            set(evidence_schema["$defs"]["fact"]["properties"]["promotionImpact"]["enum"]),
            candidate_intake.PROMOTION_IMPACTS,
        )
        self.assertEqual(
            set(evidence_schema["$defs"]["review"]["properties"]["decision"]["enum"]),
            candidate_intake.REVIEW_DECISIONS,
        )
        self.assertIn("sourcePath", request_schema["$defs"])
        self.assertIsNotNone(candidate_intake.SOURCE_PATH_PATTERN.fullmatch("evidence/flashmap.txt"))
        self.assertIsNone(candidate_intake.SOURCE_PATH_PATTERN.fullmatch("../flashmap.txt"))

    def test_stages_exact_declared_source_as_candidate_only(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            workspace = Path(temporary_directory)
            source_root = workspace / "source"
            source_root.mkdir()
            source = source_root / "flashmap.txt"
            source.write_bytes(b"candidate evidence\n")
            request = self.write_request(workspace, source, "flashmap.txt")
            request_document = json.loads(request.read_text(encoding="utf-8"))
            request_document["sourceRef"] = "owner-drop-7z"
            request.write_text(json.dumps(request_document), encoding="utf-8")
            output = workspace / "candidate"

            result = self.run_request(request, source_root, output)

            self.assertEqual(0, result.returncode, result.stderr)
            self.assertEqual(b"candidate evidence\n", source.read_bytes())
            self.assertEqual(b"candidate evidence\n", (output / "artifacts" / "flashmap-reference" / "flashmap.txt").read_bytes())
            evidence = json.loads((output / "evidence-manifest.json").read_text(encoding="utf-8"))
            report = json.loads((output / "intake-report.json").read_text(encoding="utf-8"))
            self.assertEqual("candidate", evidence["status"])
            self.assertTrue(evidence["intakeProvenance"]["candidateOnly"])
            self.assertNotIn("sourcePath", evidence["sourceArtifacts"][0])
            self.assertEqual("not-performed", report["runtimeRegistration"])
            self.assertEqual("not-performed", report["supportPromotion"])
            self.assertEqual(
                [
                    {
                        "factId": "nt51951-map-pending",
                        "factKind": "range",
                        "disposition": "unresolved",
                        "promotionImpact": "blocks-map-resolution",
                    }
                ],
                report["promotionBlockers"],
            )
            self.assertIn(
                "`nt51951-map-pending`",
                (output / "NEXT_STEPS.md").read_text(encoding="utf-8"),
            )
            serialized_output = "\n".join(
                [
                    (output / "evidence-manifest.json").read_text(encoding="utf-8"),
                    (output / "intake-report.json").read_text(encoding="utf-8"),
                    (output / "NEXT_STEPS.md").read_text(encoding="utf-8"),
                ]
            )
            self.assertNotIn(str(source_root), serialized_output)
            self.assertNotIn(str(output), serialized_output)
            self.assertNotIn("owner-drop-7z", serialized_output)
            self.assertNotIn(str(source_root), result.stdout)
            self.assertNotIn(str(output), result.stdout)
            self.assertNotIn("owner-drop-7z", result.stdout)

    def test_rejects_declared_path_escape_without_creating_output(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            workspace = Path(temporary_directory)
            source_root = workspace / "source"
            source_root.mkdir()
            outside = workspace / "outside.txt"
            outside.write_bytes(b"outside")
            request = self.write_request(workspace, outside, "../outside.txt")
            output = workspace / "candidate"

            result = self.run_request(request, source_root, output)

            self.assertNotEqual(0, result.returncode)
            self.assertIn("must use a relative POSIX path", result.stderr)
            self.assertFalse(output.exists())

    def test_rejects_hash_mismatch_without_partial_destination(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            workspace = Path(temporary_directory)
            source_root = workspace / "source"
            source_root.mkdir()
            source = source_root / "flashmap.txt"
            source.write_bytes(b"candidate evidence\n")
            request = self.write_request(workspace, source, "flashmap.txt", content_hash="0" * 64)
            output = workspace / "candidate"

            result = self.run_request(request, source_root, output)

            self.assertNotEqual(0, result.returncode)
            self.assertIn("does not match its declared size or SHA-256", result.stderr)
            self.assertFalse(output.exists())
            self.assertEqual([], list(workspace.glob(".candidate-*")))

    def test_rejects_existing_output_directory_even_when_empty(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            workspace = Path(temporary_directory)
            source_root = workspace / "source"
            source_root.mkdir()
            source = source_root / "flashmap.txt"
            source.write_bytes(b"candidate evidence\n")
            request = self.write_request(workspace, source, "flashmap.txt")
            output = workspace / "candidate"
            output.mkdir()

            result = self.run_request(request, source_root, output)

            self.assertNotEqual(0, result.returncode)
            self.assertIn("already exists", result.stderr)
            self.assertNotIn(str(output), result.stderr)
            self.assertEqual([], list(output.iterdir()))

    def test_rejects_legacy_folder_scan_options_for_manifest_request(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            workspace = Path(temporary_directory)
            source_root = workspace / "source"
            source_root.mkdir()
            source = source_root / "flashmap.txt"
            source.write_bytes(b"candidate evidence\n")
            request = self.write_request(workspace, source, "flashmap.txt")
            output = workspace / "candidate"

            result = self.run_request(
                request,
                source_root,
                output,
                extra_arguments=["--output-root", str(workspace / "legacy")],
            )

            self.assertNotEqual(0, result.returncode)
            self.assertIn("only accepts a manifest request", result.stderr)
            self.assertNotIn(str(workspace), result.stderr)
            self.assertFalse(output.exists())

    def test_rejects_office_lock_file(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            workspace = Path(temporary_directory)
            source_root = workspace / "source"
            source_root.mkdir()
            source = source_root / "~$flashmap.xlsx"
            source.write_bytes(b"lock")
            request = self.write_request(workspace, source, "~$flashmap.xlsx", logical_name="~$flashmap.xlsx")
            output = workspace / "candidate"

            result = self.run_request(request, source_root, output)

            self.assertNotEqual(0, result.returncode)
            self.assertIn("lock file", result.stderr)
            self.assertFalse(output.exists())

    def test_rejects_malformed_fact_enum_without_traceback(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            workspace = Path(temporary_directory)
            source_root = workspace / "source"
            source_root.mkdir()
            source = source_root / "flashmap.txt"
            source.write_bytes(b"candidate evidence\n")
            request_path = self.write_request(workspace, source, "flashmap.txt")
            request = json.loads(request_path.read_text(encoding="utf-8"))
            request["facts"][0]["disposition"] = []
            request_path.write_text(json.dumps(request), encoding="utf-8")
            output = workspace / "candidate"

            result = self.run_request(request_path, source_root, output)

            self.assertNotEqual(0, result.returncode)
            self.assertIn("disposition must be a non-empty string", result.stderr)
            self.assertNotIn("Traceback", result.stderr)
            self.assertFalse(output.exists())

    def test_rejects_local_path_in_output_fact_text(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            workspace = Path(temporary_directory)
            source_root = workspace / "source"
            source_root.mkdir()
            source = source_root / "flashmap.txt"
            source.write_bytes(b"candidate evidence\n")
            request_path = self.write_request(workspace, source, "flashmap.txt")
            request = json.loads(request_path.read_text(encoding="utf-8"))
            request["facts"][0]["citations"][0]["location"] = '"/home/dennis/private.xlsx"'
            request_path.write_text(json.dumps(request), encoding="utf-8")
            output = workspace / "candidate"

            result = self.run_request(request_path, source_root, output)

            self.assertNotEqual(0, result.returncode)
            self.assertIn("must not contain local paths", result.stderr)
            self.assertFalse(output.exists())

    def test_rejects_non_rfc3339_utc_timestamp(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            workspace = Path(temporary_directory)
            source_root = workspace / "source"
            source_root.mkdir()
            source = source_root / "flashmap.txt"
            source.write_bytes(b"candidate evidence\n")
            request_path = self.write_request(workspace, source, "flashmap.txt")
            request = json.loads(request_path.read_text(encoding="utf-8"))
            request["requestedAtUtc"] = "2026-07-14 00:00:00Z"
            request_path.write_text(json.dumps(request), encoding="utf-8")
            output = workspace / "candidate"

            result = self.run_request(request_path, source_root, output)

            self.assertNotEqual(0, result.returncode)
            self.assertIn("RFC 3339 UTC", result.stderr)
            self.assertFalse(output.exists())

    def test_redacts_request_path_from_candidate_errors(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            workspace = Path(temporary_directory)
            source_root = workspace / "source"
            source_root.mkdir()
            request = workspace / "private-request.json"
            output = workspace / "candidate"

            result = self.run_request(request, source_root, output)

            self.assertNotEqual(0, result.returncode)
            self.assertIn("cannot read intake request", result.stderr)
            self.assertNotIn(str(request), result.stderr)
            self.assertFalse(output.exists())

    def test_redacts_unknown_or_duplicate_request_keys(self) -> None:
        for private_key in (r"C:\private\firmware\map.xlsx", "/private/firmware/map.xlsx"):
            with self.subTest(kind="unknown", private_key=private_key):
                with tempfile.TemporaryDirectory() as temporary_directory:
                    workspace = Path(temporary_directory)
                    source_root = workspace / "source"
                    source_root.mkdir()
                    source = source_root / "flashmap.txt"
                    source.write_bytes(b"candidate evidence\n")
                    request_path = self.write_request(workspace, source, "flashmap.txt")
                    request = json.loads(request_path.read_text(encoding="utf-8"))
                    request[private_key] = None
                    request_path.write_text(json.dumps(request), encoding="utf-8")
                    output = workspace / "candidate"

                    result = self.run_request(request_path, source_root, output)

                    self.assertNotEqual(0, result.returncode)
                    self.assertIn("contains unknown field(s)", result.stderr)
                    self.assertNotIn(private_key, result.stderr)
                    self.assertFalse(output.exists())

            with self.subTest(kind="duplicate", private_key=private_key):
                with tempfile.TemporaryDirectory() as temporary_directory:
                    workspace = Path(temporary_directory)
                    source_root = workspace / "source"
                    source_root.mkdir()
                    source = source_root / "flashmap.txt"
                    source.write_bytes(b"candidate evidence\n")
                    request_path = self.write_request(workspace, source, "flashmap.txt")
                    request_text = request_path.read_text(encoding="utf-8")
                    encoded_key = json.dumps(private_key)
                    request_path.write_text(
                        f"{{{encoded_key}: null, {encoded_key}: null, {request_text[1:]}",
                        encoding="utf-8",
                    )
                    output = workspace / "candidate"

                    result = self.run_request(request_path, source_root, output)

                    self.assertNotEqual(0, result.returncode)
                    self.assertIn("duplicate JSON key", result.stderr)
                    self.assertNotIn(private_key, result.stderr)
                    self.assertFalse(output.exists())

    def test_rejects_accepted_textual_range_claim(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            workspace = Path(temporary_directory)
            source_root = workspace / "source"
            source_root.mkdir()
            source = source_root / "flashmap.txt"
            source.write_bytes(b"candidate evidence\n")
            request_path = self.write_request(workspace, source, "flashmap.txt")
            request = json.loads(request_path.read_text(encoding="utf-8"))
            request["facts"][0]["disposition"] = "accepted"
            request["facts"][0]["promotionImpact"] = "none"
            request_path.write_text(json.dumps(request), encoding="utf-8")
            output = workspace / "candidate"

            result = self.run_request(request_path, source_root, output)

            self.assertNotEqual(0, result.returncode)
            self.assertIn("requires a typed range", result.stderr)
            self.assertFalse(output.exists())

    def test_rejects_unresolved_scalar_or_reference_range_claim(self) -> None:
        for value in (
            {"kind": "scalar", "value": 123},
            {"kind": "reference", "targetId": "unresolved-range-source"},
        ):
            with self.subTest(value=value):
                with tempfile.TemporaryDirectory() as temporary_directory:
                    workspace = Path(temporary_directory)
                    source_root = workspace / "source"
                    source_root.mkdir()
                    source = source_root / "flashmap.txt"
                    source.write_bytes(b"candidate evidence\n")
                    request_path = self.write_request(workspace, source, "flashmap.txt")
                    request = json.loads(request_path.read_text(encoding="utf-8"))
                    request["facts"][0]["value"] = value
                    request_path.write_text(json.dumps(request), encoding="utf-8")
                    output = workspace / "candidate"

                    result = self.run_request(request_path, source_root, output)

                    self.assertNotEqual(0, result.returncode)
                    self.assertIn("requires a typed range", result.stderr)
                    self.assertFalse(output.exists())

    def test_accepts_typed_range_fact_without_promotion(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            workspace = Path(temporary_directory)
            source_root = workspace / "source"
            source_root.mkdir()
            source = source_root / "flashmap.txt"
            source.write_bytes(b"candidate evidence\n")
            request_path = self.write_request(workspace, source, "flashmap.txt")
            request = json.loads(request_path.read_text(encoding="utf-8"))
            request["facts"][0]["value"] = {
                "kind": "range",
                "addressSpaceId": "flash",
                "range": {"start": 0, "length": 1},
            }
            request["facts"][0]["disposition"] = "accepted"
            request["facts"][0]["promotionImpact"] = "none"
            request_path.write_text(json.dumps(request), encoding="utf-8")
            output = workspace / "candidate"

            result = self.run_request(request_path, source_root, output)

            self.assertEqual(0, result.returncode, result.stderr)
            self.assertEqual("candidate", json.loads((output / "evidence-manifest.json").read_text(encoding="utf-8"))["status"])
            report = json.loads((output / "intake-report.json").read_text(encoding="utf-8"))
            self.assertEqual([], report["promotionBlockers"])
            self.assertIn(
                "No promotion blocker was declared. Candidate status still grants no runtime authority.",
                (output / "NEXT_STEPS.md").read_text(encoding="utf-8"),
            )

    def test_rejects_output_directory_below_source_root(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            workspace = Path(temporary_directory)
            source_root = workspace / "source"
            source_root.mkdir()
            source = source_root / "flashmap.txt"
            source.write_bytes(b"candidate evidence\n")
            request = self.write_request(workspace, source, "flashmap.txt")
            output = source_root / "candidate"

            result = self.run_request(request, source_root, output)

            self.assertNotEqual(0, result.returncode)
            self.assertIn("must not be inside the source root", result.stderr)
            self.assertFalse(output.exists())

    def test_rejects_output_directory_containing_source_root(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            workspace = Path(temporary_directory)
            source_root = workspace / "source"
            source_root.mkdir()
            source = source_root / "flashmap.txt"
            source.write_bytes(b"candidate evidence\n")
            request = self.write_request(workspace, source, "flashmap.txt")

            result = self.run_request(request, source_root, workspace)

            self.assertNotEqual(0, result.returncode)
            self.assertIn("candidate output directory already exists", result.stderr)
            self.assertNotIn(str(workspace), result.stderr)
            self.assertEqual(b"candidate evidence\n", source.read_bytes())

    def test_rejects_source_replaced_by_symlink_after_initial_validation(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            workspace = Path(temporary_directory)
            source_root = workspace / "source"
            source_root.mkdir()
            source = source_root / "flashmap.txt"
            source.write_bytes(b"candidate evidence\n")
            request_path = self.write_request(workspace, source, "flashmap.txt")
            request = json.loads(request_path.read_text(encoding="utf-8"))
            _, artifacts = candidate_intake.validate_candidate_request(request, source_root)

            source.unlink()
            replacement = workspace / "external-copy.txt"
            replacement.write_bytes(b"candidate evidence\n")
            try:
                source.symlink_to(replacement)
            except OSError as exc:
                self.skipTest(f"symbolic links unavailable for reparse-point test: {exc}")

            staging_root = workspace / "staging"
            with self.assertRaisesRegex(ValueError, "escapes --source-root|symbolic link or reparse point"):
                candidate_intake.copy_declared_artifact(artifacts[0], staging_root)
            self.assertFalse(staging_root.exists())

    def test_reports_staging_cleanup_failure(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            workspace = Path(temporary_directory)
            source_root = workspace / "source"
            source_root.mkdir()
            source = source_root / "flashmap.txt"
            source.write_bytes(b"candidate evidence\n")
            request = self.write_request(workspace, source, "flashmap.txt")
            output = workspace / "candidate"
            args = argparse.Namespace(
                request=request,
                source_root=source_root,
                output_dir=output,
                dry_run=False,
                ic=None,
                mode=None,
                case=None,
                owner=None,
                source_ref=None,
                output_root=None,
                run_id=None,
            )

            with (
                mock.patch.object(candidate_intake, "write_json", side_effect=OSError("injected write failure")),
                mock.patch.object(candidate_intake.shutil, "rmtree", side_effect=OSError("injected cleanup failure")),
                self.assertRaisesRegex(
                    candidate_intake.CandidateIntakeError,
                    "cleanup failed; residual directory name is \\.candidate-",
                ),
            ):
                candidate_intake.stage_manifest_request(args)

            for residual in workspace.glob(".candidate-*"):
                shutil.rmtree(residual)

    def test_candidate_staging_boundary_redacts_filesystem_error(self) -> None:
        private_path = Path("C:/private/candidate-evidence.bin")
        args = argparse.Namespace()
        with mock.patch.object(
            candidate_intake,
            "stage_manifest_request_core",
            side_effect=OSError(str(private_path)),
        ):
            with self.assertRaises(candidate_intake.CandidateIntakeError) as raised:
                candidate_intake.stage_manifest_request(args)

        self.assertEqual("candidate evidence staging failed", str(raised.exception))
        self.assertNotIn(str(private_path), str(raised.exception))

    def test_candidate_cli_reports_controlled_cleanup_error_without_traceback(self) -> None:
        error = candidate_intake.CandidateIntakeError(
            "candidate staging cleanup failed; residual directory name is .candidate-private"
        )
        stderr = io.StringIO()
        with (
            mock.patch.object(
                sys,
                "argv",
                ["intake_ic_reference.py", "--request", "private-request.json"],
            ),
            mock.patch.object(intake_cli, "stage_manifest_request", side_effect=error),
            contextlib.redirect_stderr(stderr),
        ):
            exit_code = intake_cli.main()

        self.assertEqual(1, exit_code)
        self.assertIn("residual directory name is .candidate-private", stderr.getvalue())
        self.assertNotIn("Traceback", stderr.getvalue())
        self.assertNotIn("private-request.json", stderr.getvalue())

    def test_rejects_legacy_folder_scan_without_scanning(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            source_root = Path(temporary_directory) / "source"
            source_root.mkdir()
            source = source_root / "flashmap.txt"
            source.write_bytes(b"reference")

            result = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT),
                    "--source",
                    str(source_root),
                    "--ic",
                    "NT51951",
                    "--dry-run",
                ],
                cwd=ROOT,
                check=False,
                capture_output=True,
                text=True,
                encoding="utf-8",
            )

            self.assertNotEqual(0, result.returncode)
            self.assertEqual("", result.stdout)
            self.assertIn("only accepts a manifest request", result.stderr)
            self.assertNotIn(str(source_root), result.stderr)
            self.assertEqual(b"reference", source.read_bytes())

    def run_request(
        self,
        request: Path,
        source_root: Path,
        output: Path,
        *,
        extra_arguments: list[str] | None = None,
    ) -> subprocess.CompletedProcess[str]:
        command = [
            sys.executable,
            str(SCRIPT),
            "--request",
            str(request),
            "--source-root",
            str(source_root),
            "--output-dir",
            str(output),
        ]
        if extra_arguments:
            command.extend(extra_arguments)
        return subprocess.run(
            command,
            cwd=ROOT,
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
        )

    def write_request(
        self,
        workspace: Path,
        source: Path,
        source_path: str,
        *,
        content_hash: str | None = None,
        logical_name: str = "flashmap.txt",
    ) -> Path:
        payload = source.read_bytes()
        request = {
            "schemaVersion": "1.0",
            "requestId": "nt51951-reference-intake",
            "manifestId": "nt51951-evidence",
            "manifestVersion": "0.1.0",
            "requestedAtUtc": "2026-07-14T00:00:00Z",
            "owner": "firmware-owner",
            "workflow": "reference-only",
            "candidateScope": {
                "memberIds": ["NT51951"],
                "modeIds": ["ab-merge"],
                "capacityBytes": [524288],
                "topologyChoices": ["single"],
            },
            "sourceArtifacts": [
                {
                    "artifactId": "flashmap-reference",
                    "sourceKind": "document",
                    "logicalName": logical_name,
                    "sourcePath": source_path,
                    "contentHash": content_hash or hashlib.sha256(payload).hexdigest(),
                    "sizeBytes": len(payload),
                }
            ],
            "facts": [
                {
                    "factId": "nt51951-map-pending",
                    "subject": {"familyId": "nt51951", "memberId": "NT51951", "modeId": "ab-merge"},
                    "factKind": "range",
                    "value": {"kind": "statement", "text": "Map review is pending."},
                    "disposition": "unresolved",
                    "promotionImpact": "blocks-map-resolution",
                    "citations": [{"artifactId": "flashmap-reference", "location": "Sheet 1"}],
                }
            ],
            "reviews": [],
        }
        path = workspace / "request.json"
        path.write_text(json.dumps(request), encoding="utf-8")
        return path


if __name__ == "__main__":
    unittest.main()

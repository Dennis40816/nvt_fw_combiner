from __future__ import annotations

import hashlib
import importlib.util
import json
import os
import tempfile
import unittest
from pathlib import Path
from types import ModuleType

VALIDATOR_PATH = (
    Path(__file__).resolve().parents[2] / "scripts" / "canonical_golden_validation.py"
)


def load_validator_module() -> ModuleType:
    spec = importlib.util.spec_from_file_location(
        "canonical_golden_validation", VALIDATOR_PATH
    )
    if spec is None or spec.loader is None:
        raise RuntimeError("cannot load canonical golden validator")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


VALIDATOR = load_validator_module()


class CanonicalGoldenValidationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)
        self.canonical = self.root / "testdata/golden/canonical"
        self.case_directory = self.canonical / (
            "NT51927/standard-merge/gen-flash/topology-unscoped/"
            "nt51927-standard-merge-gen-flash"
        )
        input_path = self.case_directory / "inputs/dp.bin"
        expected_path = self.case_directory / "expected/flash.bin"
        input_path.parent.mkdir(parents=True)
        expected_path.parent.mkdir(parents=True)
        input_path.write_bytes(b"dp input")
        expected_path.write_bytes(b"expected output")
        provenance = self.case_directory / "provenance"
        provenance.mkdir(parents=True)
        case_id = "nt51927-standard-merge-gen-flash"
        case_manifest_path = (
            "NT51927/standard-merge/gen-flash/topology-unscoped/"
            f"{case_id}/provenance/case.json"
        )
        self.case_manifest = {
            "schemaVersion": "1.0",
            "caseId": case_id,
            "ic": "NT51927",
            "workflow": "standard-merge",
            "variantOrVersion": "gen-flash",
            "topology": "topology-unscoped",
            "directGolden": True,
            "sourceClassification": "owner-approved",
            "ownerApproval": "test fixture",
            "artifacts": [
                self.artifact(
                    "dp-input",
                    "input",
                    input_path,
                    "testdata/golden/standard-merge-gen-flash/inputs/51927/dp.bin",
                ),
                self.artifact(
                    "expected-output",
                    "expected",
                    expected_path,
                    "testdata/golden/standard-merge-gen-flash/expected/51927/flash.bin",
                ),
            ],
        }
        self.write_json(provenance / "case.json", self.case_manifest)
        self.root_manifest = {
            "schemaVersion": "1.0",
            "payloadClass": "owner-approved-golden",
            "binaryPayloadsIncluded": True,
            "diagnosticsRoot": "testdata/diagnostics/golden-evidence",
            "cases": [{"caseId": case_id, "manifestPath": case_manifest_path}],
        }
        self.write_json(self.canonical / "manifest.json", self.root_manifest)
        (self.canonical / "README.md").write_text(
            "canonical fixture\n", encoding="utf-8"
        )

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    def artifact(
        self, artifact_id: str, role: str, path: Path, legacy_path: str
    ) -> dict[str, object]:
        payload = path.read_bytes()
        return {
            "artifactId": artifact_id,
            "role": role,
            "path": path.relative_to(self.canonical).as_posix(),
            "size": len(payload),
            "sha256": hashlib.sha256(payload).hexdigest(),
            "legacyPaths": [legacy_path],
        }

    @staticmethod
    def write_json(path: Path, document: object) -> None:
        path.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")

    def validate(self) -> list[str]:
        errors: list[str] = []
        VALIDATOR.validate_canonical_golden(self.root, errors)
        return errors

    def validate_release_allowlist(self) -> list[str]:
        release_case = {
            "caseId": self.case_manifest["caseId"],
            "manifestPath": self.root_manifest["cases"][0]["manifestPath"],
            "directGolden": self.case_manifest["directGolden"],
            "artifacts": [
                {
                    "artifactId": artifact["artifactId"],
                    "path": artifact["path"],
                    "size": artifact["size"],
                    "sha256": artifact["sha256"],
                }
                for artifact in self.case_manifest["artifacts"]
            ],
        }
        self.release_allowlist = {
            "schemaVersion": "1.0",
            "policyId": "standard-merge-reference-v1",
            "workflow": "standard-merge",
            "releaseStatus": "human-gated-allowlist",
            "approvalBasis": "test fixture",
            "cases": [release_case],
        }
        path = self.root / "testdata/golden/release-standard-merge-v1.json"
        path.parent.mkdir(parents=True, exist_ok=True)
        self.write_json(path, self.release_allowlist)
        errors: list[str] = []
        VALIDATOR.validate_standard_merge_release_allowlist(self.root, errors)
        return errors

    def rewrite_case(self) -> None:
        self.write_json(
            self.case_directory / "provenance/case.json", self.case_manifest
        )

    def rewrite_root(self) -> None:
        self.write_json(self.canonical / "manifest.json", self.root_manifest)

    def convert_direct_golden_to_input_evidence(self) -> None:
        expected_path = self.case_directory / "expected/flash.bin"
        expected_path.unlink()
        expected_path.parent.rmdir()
        self.case_manifest["directGolden"] = False
        self.case_manifest["directEvidence"] = True
        self.case_manifest["artifacts"] = [self.case_manifest["artifacts"][0]]
        self.rewrite_case()

    def add_alias(
        self,
        source_case_id: str,
        workflow: str = "standard-merge",
    ) -> Path:
        alias_id = f"nt51917-{workflow}-gen-flash-alias"
        alias_directory = self.canonical / (
            f"NT51917/{workflow}/gen-flash/topology-unscoped/" + alias_id
        )
        alias_directory.mkdir(parents=True)
        alias_manifest_path = (
            f"NT51917/{workflow}/gen-flash/topology-unscoped/"
            f"{alias_id}/provenance/case.json"
        )
        alias_provenance = alias_directory / "provenance"
        alias_provenance.mkdir()
        self.write_json(
            alias_provenance / "case.json",
            {
                "schemaVersion": "1.0",
                "caseId": alias_id,
                "ic": "NT51917",
                "workflow": workflow,
                "variantOrVersion": "gen-flash",
                "topology": "topology-unscoped",
                "directGolden": False,
                "sourceClassification": "owner-approved-fact-alias",
                "ownerApproval": "test fixture",
                "alias": {
                    "sourceCaseId": source_case_id,
                    "factScope": ["standard-merge region set"],
                    "evidenceRefs": ["owner decision"],
                },
            },
        )
        self.root_manifest["cases"].append(
            {"caseId": alias_id, "manifestPath": alias_manifest_path}
        )
        self.rewrite_root()
        return alias_provenance / "case.json"

    def test_accepts_hash_pinned_direct_case(self) -> None:
        self.assertEqual([], self.validate())

    def test_accepts_hash_pinned_direct_input_evidence_without_expected(self) -> None:
        self.convert_direct_golden_to_input_evidence()

        self.assertEqual([], self.validate())

    def test_accepts_alias_to_direct_input_evidence(self) -> None:
        self.convert_direct_golden_to_input_evidence()
        self.add_alias("nt51927-standard-merge-gen-flash")

        self.assertEqual([], self.validate())

    def test_rejects_cross_workflow_alias_to_direct_input_evidence(self) -> None:
        self.convert_direct_golden_to_input_evidence()
        self.add_alias(
            "nt51927-standard-merge-gen-flash",
            workflow="ctrlram-replace",
        )

        errors = self.validate()

        self.assertTrue(any("workflow must match" in error for error in errors))

    def test_accepts_one_case_binding_one_payload_to_multiple_logical_roles(self) -> None:
        shared_input = dict(self.case_manifest["artifacts"][0])
        shared_input["artifactId"] = "tp-b-input"
        self.case_manifest["artifacts"].append(shared_input)
        self.rewrite_case()

        self.assertEqual([], self.validate())

    def test_rejects_a_second_case_reaching_into_the_first_case_payload(self) -> None:
        case_id = "nt51950-ab-cross-case-reference"
        manifest_path = (
            "NT51950/ab-merge/test/topology-unscoped/"
            f"{case_id}/provenance/case.json"
        )
        foreign_input = dict(self.case_manifest["artifacts"][0])
        foreign_expected = dict(foreign_input)
        foreign_expected["artifactId"] = "expected-output"
        foreign_expected["role"] = "expected"
        (self.canonical / manifest_path).parent.mkdir(parents=True)
        self.write_json(
            self.canonical / manifest_path,
            {
                "schemaVersion": "1.0",
                "caseId": case_id,
                "ic": "NT51950",
                "workflow": "ab-merge",
                "variantOrVersion": "test",
                "topology": "topology-unscoped",
                "directGolden": True,
                "sourceClassification": "owner-approved",
                "ownerApproval": "test fixture",
                "artifacts": [foreign_input, foreign_expected],
            },
        )
        self.root_manifest["cases"].append(
            {"caseId": case_id, "manifestPath": manifest_path}
        )
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(any("path must stay below" in error for error in errors))

    def test_accepts_explicit_standard_merge_release_artifact_facts(self) -> None:
        self.assertEqual([], self.validate_release_allowlist())

    def test_rejects_release_selection_of_direct_input_evidence(self) -> None:
        self.convert_direct_golden_to_input_evidence()

        errors = self.validate_release_allowlist()

        self.assertTrue(
            any("cannot select direct input evidence" in error for error in errors)
        )

    def test_rejects_release_artifact_hash_drift(self) -> None:
        self.validate_release_allowlist()
        self.release_allowlist["cases"][0]["artifacts"][0]["sha256"] = "0" * 64
        self.write_json(
            self.root / "testdata/golden/release-standard-merge-v1.json",
            self.release_allowlist,
        )

        errors: list[str] = []
        VALIDATOR.validate_standard_merge_release_allowlist(self.root, errors)

        self.assertTrue(any("sha256 differs" in error for error in errors))

    def test_rejects_release_case_manifest_path_drift(self) -> None:
        self.validate_release_allowlist()
        self.release_allowlist["cases"][0]["manifestPath"] = "wrong/case.json"
        self.write_json(
            self.root / "testdata/golden/release-standard-merge-v1.json",
            self.release_allowlist,
        )

        errors: list[str] = []
        VALIDATOR.validate_standard_merge_release_allowlist(self.root, errors)

        self.assertTrue(any("does not match" in error for error in errors))

    def test_rejects_non_boolean_release_direct_golden(self) -> None:
        for invalid_value in (1, "false", None):
            with self.subTest(direct_golden=invalid_value):
                self.validate_release_allowlist()
                if invalid_value is None:
                    del self.release_allowlist["cases"][0]["directGolden"]
                else:
                    self.release_allowlist["cases"][0]["directGolden"] = invalid_value
                self.write_json(
                    self.root / "testdata/golden/release-standard-merge-v1.json",
                    self.release_allowlist,
                )

                errors: list[str] = []
                VALIDATOR.validate_standard_merge_release_allowlist(self.root, errors)

                self.assertTrue(
                    any("directGolden must be a boolean" in error for error in errors)
                )

    def test_rejects_payload_hash_drift(self) -> None:
        (self.case_directory / "expected/flash.bin").write_bytes(b"changed")

        errors = self.validate()

        self.assertTrue(any("size mismatch" in error for error in errors))
        self.assertTrue(any("SHA-256 mismatch" in error for error in errors))

    def test_rejects_same_size_payload_hash_drift(self) -> None:
        path = self.case_directory / "expected/flash.bin"
        payload = bytearray(path.read_bytes())
        payload[0] ^= 0xFF
        path.write_bytes(payload)

        errors = self.validate()

        self.assertFalse(any("size mismatch" in error for error in errors))
        self.assertTrue(any("SHA-256 mismatch" in error for error in errors))

    def test_rejects_non_integer_size(self) -> None:
        artifact = self.case_manifest["artifacts"][0]
        for invalid_size in (True, float(artifact["size"])):
            with self.subTest(size=invalid_size):
                artifact["size"] = invalid_size
                self.rewrite_case()

                errors = self.validate()

                self.assertTrue(
                    any("non-negative integer" in error for error in errors)
                )

    def test_rejects_path_escape(self) -> None:
        self.case_manifest["artifacts"][0]["path"] = "../outside.bin"
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(
            any("not a normalized confined path" in error for error in errors)
        )

    def test_rejects_windows_path_escape(self) -> None:
        self.case_manifest["artifacts"][0]["path"] = "..\\outside.bin"
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(
            any("not a normalized confined path" in error for error in errors)
        )

    def test_rejects_windows_drive_path(self) -> None:
        self.case_manifest["artifacts"][0]["path"] = "C:/outside.bin"
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(
            any("not a normalized confined path" in error for error in errors)
        )

    def test_rejects_symlinked_artifact(self) -> None:
        path = self.case_directory / "expected/flash.bin"
        outside = self.root / "outside.bin"
        outside.write_bytes(path.read_bytes())
        path.unlink()
        try:
            os.symlink(outside, path)
        except OSError as error:
            self.skipTest(f"symlink creation is unavailable: {error}")

        errors = self.validate()

        self.assertTrue(any("cannot contain a symlink" in error for error in errors))

    def test_rejects_missing_declared_file(self) -> None:
        (self.case_directory / "expected/flash.bin").unlink()

        errors = self.validate()

        self.assertTrue(
            any("cannot resolve canonical artifact" in error for error in errors)
        )

    def test_rejects_direct_case_without_expected_role(self) -> None:
        self.case_manifest["artifacts"] = [self.case_manifest["artifacts"][0]]
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(any("requires input and expected" in error for error in errors))

    def test_rejects_direct_case_without_input_role(self) -> None:
        self.case_manifest["artifacts"] = [self.case_manifest["artifacts"][1]]
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(any("requires input and expected" in error for error in errors))

    def test_rejects_direct_input_evidence_with_expected_role(self) -> None:
        self.case_manifest["directGolden"] = False
        self.case_manifest["directEvidence"] = True
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(
            any("cannot declare an expected artifact" in error for error in errors)
        )

    def test_rejects_direct_input_evidence_without_input_role(self) -> None:
        self.case_manifest["directGolden"] = False
        self.case_manifest["directEvidence"] = True
        self.case_manifest["artifacts"] = [self.case_manifest["artifacts"][1]]
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(any("requires input artifacts" in error for error in errors))

    def test_rejects_case_marked_as_direct_golden_and_direct_evidence(self) -> None:
        self.case_manifest["directEvidence"] = True
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(any("cannot be both" in error for error in errors))

    def test_rejects_non_boolean_direct_evidence(self) -> None:
        self.case_manifest["directGolden"] = False
        self.case_manifest["directEvidence"] = "true"
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(any("must be a boolean" in error for error in errors))

    def test_rejects_diagnostic_path_as_canonical_artifact(self) -> None:
        self.case_manifest["artifacts"][0]["path"] = (
            "testdata/diagnostics/golden-evidence/payload.bin"
        )
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(any("path must stay below" in error for error in errors))

    def test_rejects_duplicate_artifact_declaration(self) -> None:
        duplicate = dict(self.case_manifest["artifacts"][0])
        self.case_manifest["artifacts"].append(duplicate)
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(any("duplicate artifactId" in error for error in errors))

    def test_rejects_malformed_sha(self) -> None:
        self.case_manifest["artifacts"][0]["sha256"] = "not-a-sha"
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(any("invalid sha256" in error for error in errors))

    def test_rejects_undeclared_file(self) -> None:
        (self.case_directory / "inputs/extra.bin").write_bytes(b"extra")

        errors = self.validate()

        self.assertTrue(any("contains undeclared files" in error for error in errors))

    def test_accepts_fact_scoped_alias_without_payload_copy(self) -> None:
        self.add_alias("nt51927-standard-merge-gen-flash")

        self.assertEqual([], self.validate())

    def test_rejects_alias_without_direct_source(self) -> None:
        self.add_alias("missing-direct-case")

        errors = self.validate()

        self.assertTrue(
            any(
                "must reference a direct canonical evidence case" in error
                for error in errors
            )
        )

    def test_rejects_alias_with_physical_artifacts(self) -> None:
        alias_manifest_path = self.add_alias("nt51927-standard-merge-gen-flash")
        alias_manifest = json.loads(alias_manifest_path.read_text(encoding="utf-8"))
        alias_manifest["artifacts"] = [self.case_manifest["artifacts"][0]]
        self.write_json(alias_manifest_path, alias_manifest)

        errors = self.validate()

        self.assertTrue(
            any(
                "alias case cannot contain physical artifacts" in error
                for error in errors
            )
        )

    def test_rejects_diagnostics_root_drift(self) -> None:
        self.root_manifest["diagnosticsRoot"] = "testdata/golden/canonical/diagnostics"
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(any("diagnosticsRoot must be" in error for error in errors))


if __name__ == "__main__":
    unittest.main()

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
        self.disposition_test = self.root / "tests/golden_runner.py"
        self.disposition_test.parent.mkdir(parents=True)
        self.disposition_test.write_text(
            "def direct_full_output():\n    pass\n", encoding="utf-8"
        )
        self.case_directory = self.canonical / (
            "NT51927/standard-merge/gen-flash/topology-unscoped/"
            "nt51927-standard-merge-gen-flash"
        )
        input_path = self.case_directory / (
            "inputs/nt51927-standard-merge-gen-flash-dp-input.bin"
        )
        expected_path = self.case_directory / (
            "expected/nt51927-standard-merge-gen-flash-expected-output.bin"
        )
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
            "testDisposition": {
                "kind": "direct-full-output",
                "evidenceRefs": ["tests/golden_runner.py#direct_full_output"],
            },
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
        self.route_id = "test-standard-merge-route"
        self.route_fingerprint = "1" * 64
        expected_payload = expected_path.read_bytes()
        self.root_manifest = {
            "schemaVersion": "1.1",
            "payloadClass": "owner-approved-golden",
            "binaryPayloadsIncluded": True,
            "diagnosticsRoot": "testdata/diagnostics/golden-evidence",
            "cases": [{"caseId": case_id, "manifestPath": case_manifest_path}],
            "routeEvidence": [
                {
                    "evidenceId": "test-direct-golden",
                    "kind": "direct-golden",
                    "routeId": self.route_id,
                    "capabilityFingerprint": self.route_fingerprint,
                    "caseId": case_id,
                    "testReference": ("tests/golden_runner.py#direct_full_output"),
                    "expectedView": {
                        "artifactId": "expected-output",
                        "start": 0,
                        "length": len(expected_payload),
                        "sha256": hashlib.sha256(expected_payload).hexdigest(),
                    },
                }
            ],
        }
        self.write_json(self.canonical / "manifest.json", self.root_manifest)
        self.policy_path = (
            self.root / "docs/contracts/canonical-capability-policy-v1.json"
        )
        self.policy_path.parent.mkdir(parents=True)
        self.rewrite_policy_from_route_evidence()
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
            "manifestSha256": hashlib.sha256(
                (
                    self.canonical
                    / self.root_manifest["cases"][0]["manifestPath"]
                ).read_bytes()
            ).hexdigest(),
            "workflow": self.case_manifest["workflow"],
            "testDispositionKind": "direct-full-output",
            "directGolden": self.case_manifest["directGolden"],
            "directEvidence": False,
            "artifacts": [
                {
                    "artifactId": artifact["artifactId"],
                    "role": artifact["role"],
                    "path": artifact["path"],
                    "size": artifact["size"],
                    "sha256": artifact["sha256"],
                }
                for artifact in self.case_manifest["artifacts"]
            ],
        }
        self.release_allowlist = {
            "schemaVersion": "1.0",
            "policyId": "canonical-reference-v1",
            "authorizedForVersion": "1.0.8",
            "releaseStatus": "human-gated-allowlist",
            "redistributionAuthorization": {
                "authorizedOn": "2026-09-01",
                "authorizedBy": "repository owner",
                "scope": "reference-payload-only",
                "supersedesHistoricalCaseRestrictions": True,
            },
            "authorityLimits": {
                "runtimeSupportPromotion": False,
                "fullByteParityClaim": False,
            },
            "canonicalReadmeSha256": hashlib.sha256(
                (self.canonical / "README.md").read_bytes()
            ).hexdigest(),
            "selectionSummary": {
                "caseCount": 1,
                "directGoldenCount": 1,
                "factScopedAliasCount": 0,
                "artifactDeclarationCount": 2,
                "uniqueArtifactPathCount": 2,
            },
            "cases": [release_case],
        }
        path = self.root / "testdata/golden/release-canonical-v1.json"
        path.parent.mkdir(parents=True, exist_ok=True)
        self.write_json(path, self.release_allowlist)
        errors: list[str] = []
        VALIDATOR.validate_canonical_release_allowlist(
            self.root,
            errors,
            expected_summary=self.release_allowlist["selectionSummary"],
        )
        return errors

    def add_release_alias(self) -> dict[str, object]:
        alias_case_id = "nt51917-standard-merge-gen-flash-alias"
        alias_manifest_path = (
            "NT51917/standard-merge/gen-flash/topology-unscoped/"
            f"{alias_case_id}/provenance/case.json"
        )
        alias_facts = {
            "sourceCaseId": self.case_manifest["caseId"],
            "factScope": ["command-shape"],
            "evidenceRefs": ["tests/golden_runner.py#direct_full_output"],
        }
        alias_manifest = {
            "schemaVersion": "1.0",
            "caseId": alias_case_id,
            "ic": "NT51917",
            "workflow": "standard-merge",
            "variantOrVersion": "gen-flash",
            "topology": "topology-unscoped",
            "directGolden": False,
            "testDisposition": {
                "kind": "fact-scoped-alias",
                "evidenceRefs": ["tests/golden_runner.py#direct_full_output"],
            },
            "sourceClassification": "owner-approved-alias",
            "ownerApproval": "test fixture",
            "alias": alias_facts,
        }
        self.root_manifest["cases"].append(
            {"caseId": alias_case_id, "manifestPath": alias_manifest_path}
        )
        self.write_json(self.canonical / "manifest.json", self.root_manifest)
        (self.canonical / alias_manifest_path).parent.mkdir(parents=True)
        self.write_json(self.canonical / alias_manifest_path, alias_manifest)
        release_alias = {
            "caseId": alias_case_id,
            "manifestPath": alias_manifest_path,
            "manifestSha256": hashlib.sha256(
                (self.canonical / alias_manifest_path).read_bytes()
            ).hexdigest(),
            "workflow": "standard-merge",
            "testDispositionKind": "fact-scoped-alias",
            "directGolden": False,
            "directEvidence": False,
            "alias": alias_facts,
            "artifacts": [],
        }
        self.release_allowlist["cases"].append(release_alias)
        self.release_allowlist["selectionSummary"].update(
            {"caseCount": 2, "factScopedAliasCount": 1}
        )
        self.write_json(
            self.root / "testdata/golden/release-canonical-v1.json",
            self.release_allowlist,
        )
        return release_alias

    def rewrite_case(self) -> None:
        self.write_json(
            self.case_directory / "provenance/case.json", self.case_manifest
        )

    def rewrite_root(self) -> None:
        self.write_json(self.canonical / "manifest.json", self.root_manifest)

    def rewrite_policy(self) -> None:
        self.write_json(self.policy_path, self.policy)

    def rewrite_policy_from_route_evidence(self) -> None:
        self.policy = {
            "schemaVersion": "1.0",
            "catalogId": "canonical-capability-policy",
            "catalogVersion": "test",
            "issuedOn": "2026-08-25",
            "routes": [
                {
                    "routeId": evidence["routeId"],
                    "capabilityFingerprint": evidence["capabilityFingerprint"],
                    "evidence": {
                        "decisionId": evidence["evidenceId"],
                        "routeId": evidence["routeId"],
                        "capabilityFingerprint": evidence["capabilityFingerprint"],
                        "value": evidence["kind"],
                        "sourceReference": "test fixture",
                    },
                }
                for evidence in self.root_manifest["routeEvidence"]
            ],
        }
        self.rewrite_policy()

    def convert_direct_golden_to_input_evidence(self) -> None:
        expected_path = self.case_directory / (
            "expected/nt51927-standard-merge-gen-flash-expected-output.bin"
        )
        expected_path.unlink()
        expected_path.parent.rmdir()
        self.case_manifest["directGolden"] = False
        self.case_manifest["directEvidence"] = True
        self.case_manifest["artifacts"] = [self.case_manifest["artifacts"][0]]
        self.case_manifest["testDisposition"] = {
            "kind": "input-only-evidence",
            "evidenceRefs": ["tests/golden_runner.py#direct_full_output"],
        }
        self.rewrite_case()
        self.root_manifest["routeEvidence"] = [
            {
                "evidenceId": "test-contract-only",
                "kind": "contract-only",
                "routeId": self.route_id,
                "capabilityFingerprint": self.route_fingerprint,
                "testReference": "tests/golden_runner.py#direct_full_output",
            }
        ]
        self.rewrite_root()
        self.rewrite_policy_from_route_evidence()

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
                "testDisposition": {
                    "kind": "fact-scoped-alias",
                    "evidenceRefs": ["tests/golden_runner.py#direct_full_output"],
                },
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

    def add_route_evidence_alias(
        self,
        *,
        source_route_id: str | None = None,
        source_fingerprint: str | None = None,
        fact_scope_ids: list[str] | None = None,
    ) -> dict[str, object]:
        alias_manifest = self.add_alias("nt51927-standard-merge-gen-flash")
        alias_case = json.loads(alias_manifest.read_text(encoding="utf-8"))
        evidence: dict[str, object] = {
            "evidenceId": "test-approved-alias",
            "kind": "approved-alias",
            "routeId": "test-standard-merge-alias-route",
            "capabilityFingerprint": "2" * 64,
            "sourceRouteId": source_route_id or self.route_id,
            "sourceCapabilityFingerprint": (
                source_fingerprint or self.route_fingerprint
            ),
            "caseId": alias_case["caseId"],
            "factScopeIds": fact_scope_ids or [f"{alias_case['caseId']}:fact-1"],
            "testReference": "tests/golden_runner.py#direct_full_output",
        }
        self.root_manifest["routeEvidence"].append(evidence)
        self.rewrite_root()
        self.rewrite_policy_from_route_evidence()
        return evidence

    def use_synthetic_route_evidence(self) -> None:
        oracle = self.root / "testdata/public-synthetic/oracle.json"
        oracle.parent.mkdir(parents=True)
        oracle.write_text('{"oracle":"test"}\n', encoding="utf-8")
        expected_sha256 = "3" * 64
        self.disposition_test.write_text(
            f"def direct_full_output():\n    expected_sha256 = '{expected_sha256}'\n",
            encoding="utf-8",
        )
        self.root_manifest["routeEvidence"] = [
            {
                "evidenceId": "test-synthetic-oracle",
                "kind": "synthetic-oracle",
                "routeId": self.route_id,
                "capabilityFingerprint": self.route_fingerprint,
                "oracleReference": "testdata/public-synthetic/oracle.json",
                "expectedSha256": expected_sha256,
                "testReference": "tests/golden_runner.py#direct_full_output",
            }
        ]
        self.rewrite_root()
        self.rewrite_policy_from_route_evidence()

    def test_accepts_hash_pinned_direct_case(self) -> None:
        self.assertEqual([], self.validate())

    def test_accepts_direct_route_evidence_without_an_expected_view(self) -> None:
        del self.root_manifest["routeEvidence"][0]["expectedView"]
        self.rewrite_root()

        self.assertEqual([], self.validate())

    def test_accepts_contract_only_route_evidence_with_contract_reference(
        self,
    ) -> None:
        contract = self.root / "docs/contracts/test-route-contract.md"
        contract.parent.mkdir(parents=True, exist_ok=True)
        contract.write_text("# Exact route contract\n", encoding="utf-8")
        self.root_manifest["routeEvidence"] = [
            {
                "evidenceId": "test-contract-only",
                "kind": "contract-only",
                "routeId": self.route_id,
                "capabilityFingerprint": self.route_fingerprint,
                "contractReference": (
                    "docs/contracts/test-route-contract.md#exact-route-contract"
                ),
            }
        ]
        self.rewrite_root()
        self.rewrite_policy_from_route_evidence()

        self.assertEqual([], self.validate())

    def test_accepts_synthetic_oracle_route_evidence(self) -> None:
        self.use_synthetic_route_evidence()

        self.assertEqual([], self.validate())

    def test_rejects_synthetic_hash_not_pinned_by_referenced_test(self) -> None:
        self.use_synthetic_route_evidence()
        self.root_manifest["routeEvidence"][0]["expectedSha256"] = "4" * 64
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(
            any("must appear as an exact literal" in error for error in errors)
        )

    def test_accepts_exact_route_approved_alias(self) -> None:
        self.add_route_evidence_alias()

        self.assertEqual([], self.validate())

    def test_rejects_stale_expected_view_hash(self) -> None:
        self.root_manifest["routeEvidence"][0]["expectedView"]["sha256"] = "0" * 64
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(
            any("expectedView SHA-256 mismatch" in error for error in errors)
        )

    def test_rejects_expected_view_length_outside_payload(self) -> None:
        self.root_manifest["routeEvidence"][0]["expectedView"]["length"] += 1
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(any("exceeds artifact size" in error for error in errors))

    def test_rejects_direct_route_evidence_with_unknown_case(self) -> None:
        self.root_manifest["routeEvidence"][0]["caseId"] = "missing-case"
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(
            any("does not identify a canonical case" in error for error in errors)
        )

    def test_rejects_expected_view_with_unknown_artifact(self) -> None:
        self.root_manifest["routeEvidence"][0]["expectedView"]["artifactId"] = (
            "missing-artifact"
        )
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(
            any("must identify exactly one artifact" in error for error in errors)
        )

    def test_rejects_alias_with_stale_source_fingerprint(self) -> None:
        self.add_route_evidence_alias(source_fingerprint="f" * 64)

        errors = self.validate()

        self.assertTrue(
            any(
                "source route evidence is missing or stale" in error for error in errors
            )
        )

    def test_rejects_alias_of_the_same_route_id(self) -> None:
        self.add_route_evidence_alias(source_route_id=self.route_id)
        alias = self.root_manifest["routeEvidence"][-1]
        alias["routeId"] = self.route_id
        alias["capabilityFingerprint"] = "2" * 64
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(any("same routeId" in error for error in errors))

    def test_rejects_alias_case_cycle_or_alias_chain(self) -> None:
        alias_manifest = self.add_alias("nt51927-standard-merge-gen-flash")
        alias_case = json.loads(alias_manifest.read_text(encoding="utf-8"))
        alias_case["alias"]["sourceCaseId"] = alias_case["caseId"]
        self.write_json(alias_manifest, alias_case)

        cycle_errors = self.validate()

        self.assertTrue(
            any("must reference a direct canonical evidence case" in error for error in cycle_errors)
        )

        second_id = "nt51926-standard-merge-gen-flash-alias"
        second_path = (
            self.canonical
            / "NT51926/standard-merge/gen-flash/topology-unscoped"
            / second_id
            / "provenance/case.json"
        )
        second_path.parent.mkdir(parents=True)
        second_case = json.loads(json.dumps(alias_case))
        second_case["caseId"] = second_id
        second_case["ic"] = "NT51926"
        second_case["alias"]["sourceCaseId"] = alias_case["caseId"]
        self.write_json(second_path, second_case)
        alias_case["alias"]["sourceCaseId"] = second_id
        self.write_json(alias_manifest, alias_case)
        self.root_manifest["cases"].append(
            {
                "caseId": second_id,
                "manifestPath": second_path.relative_to(self.canonical).as_posix(),
            }
        )
        self.rewrite_root()

        chain_errors = self.validate()

        self.assertGreaterEqual(
            sum(
                "must reference a direct canonical evidence case" in error
                for error in chain_errors
            ),
            2,
        )

    def test_rejects_route_evidence_case_substitution(self) -> None:
        alias = self.add_route_evidence_alias()
        alias["caseId"] = "nt51927-standard-merge-gen-flash"
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(
            any("caseId must identify a canonical alias case" in error for error in errors)
        )

    def test_rejects_duplicate_alias_fact_scope_ids(self) -> None:
        alias_fact_id = "nt51917-standard-merge-gen-flash-alias:fact-1"
        self.add_route_evidence_alias(fact_scope_ids=[alias_fact_id, alias_fact_id])

        errors = self.validate()

        self.assertTrue(
            any("factScopeIds cannot contain duplicates" in error for error in errors)
        )

    def test_rejects_unknown_alias_fact_scope_id(self) -> None:
        self.add_route_evidence_alias(
            fact_scope_ids=["nt51917-standard-merge-gen-flash-alias:fact-99"]
        )

        errors = self.validate()

        self.assertTrue(
            any("unknown or wrong-case fact id" in error for error in errors)
        )

    def test_rejects_wrong_case_alias_fact_scope_id(self) -> None:
        self.add_route_evidence_alias(fact_scope_ids=["another-alias-case:fact-1"])

        errors = self.validate()

        self.assertTrue(
            any("unknown or wrong-case fact id" in error for error in errors)
        )

    def test_rejects_missing_capability_policy_route_evidence(self) -> None:
        self.add_route_evidence_alias()
        self.root_manifest["routeEvidence"].pop()
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(
            any("missing capability-policy evidenceIds" in error for error in errors)
        )

    def test_rejects_extra_route_evidence_not_in_capability_policy(self) -> None:
        self.root_manifest["routeEvidence"].append(
            {
                "evidenceId": "extra-evidence",
                "kind": "contract-only",
                "routeId": "extra-route",
                "capabilityFingerprint": "4" * 64,
                "testReference": "tests/golden_runner.py#direct_full_output",
            }
        )
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(
            any("extra or has an unknown evidenceId" in error for error in errors)
        )

    def test_rejects_route_id_that_differs_from_capability_policy(self) -> None:
        self.root_manifest["routeEvidence"][0]["routeId"] = "wrong-route"
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(
            any("routeId does not match capability policy" in error for error in errors)
        )

    def test_rejects_stale_fingerprint_against_capability_policy(self) -> None:
        self.root_manifest["routeEvidence"][0]["capabilityFingerprint"] = "f" * 64
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(
            any(
                "capabilityFingerprint does not match capability policy" in error
                for error in errors
            )
        )

    def test_rejects_kind_that_differs_from_capability_policy(self) -> None:
        self.policy["routes"][0]["evidence"]["value"] = "contract-only"
        self.rewrite_policy()

        errors = self.validate()

        self.assertTrue(
            any("kind does not match capability policy" in error for error in errors)
        )

    def test_rejects_evidence_id_that_differs_from_capability_policy(self) -> None:
        self.root_manifest["routeEvidence"][0]["evidenceId"] = "wrong-evidence-id"
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(any("unknown evidenceId" in error for error in errors))

    def test_rejects_duplicate_route_evidence_ids(self) -> None:
        duplicate = {
            "evidenceId": "test-direct-golden",
            "kind": "contract-only",
            "routeId": "another-route",
            "capabilityFingerprint": "4" * 64,
            "testReference": "tests/golden_runner.py#direct_full_output",
        }
        self.root_manifest["routeEvidence"].append(duplicate)
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(
            any("duplicate canonical route evidenceId" in error for error in errors)
        )

    def test_rejects_duplicate_exact_route_evidence_identity(self) -> None:
        duplicate = {
            "evidenceId": "another-evidence-id",
            "kind": "contract-only",
            "routeId": self.route_id,
            "capabilityFingerprint": self.route_fingerprint,
            "testReference": "tests/golden_runner.py#direct_full_output",
        }
        self.root_manifest["routeEvidence"].append(duplicate)
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(
            any(
                "duplicate canonical route evidence identity" in error
                for error in errors
            )
        )

    def test_rejects_unknown_route_evidence_field(self) -> None:
        self.root_manifest["routeEvidence"][0]["supportStatus"] = "supported"
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(
            any("direct-golden keys must be exactly" in error for error in errors)
        )

    def test_rejects_unknown_route_evidence_kind(self) -> None:
        self.root_manifest["routeEvidence"][0]["kind"] = "observed-output"
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(any("unsupported kind" in error for error in errors))

    def test_rejects_malformed_capability_fingerprint(self) -> None:
        self.root_manifest["routeEvidence"][0]["capabilityFingerprint"] = "ABC123"
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(any("must be a lowercase SHA-256" in error for error in errors))

    def test_rejects_root_manifest_without_route_evidence(self) -> None:
        del self.root_manifest["routeEvidence"]
        self.rewrite_root()

        errors = self.validate()

        self.assertTrue(any("non-empty routeEvidence" in error for error in errors))

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

    def test_accepts_one_case_binding_one_payload_to_multiple_logical_roles(
        self,
    ) -> None:
        shared_input = dict(self.case_manifest["artifacts"][0])
        shared_input["artifactId"] = "tp-b-input"
        self.case_manifest["artifacts"].append(shared_input)
        self.rewrite_case()

        self.assertEqual([], self.validate())

    def test_rejects_a_second_case_reaching_into_the_first_case_payload(self) -> None:
        case_id = "nt51950-ab-cross-case-reference"
        manifest_path = (
            f"NT51950/ab-merge/test/topology-unscoped/{case_id}/provenance/case.json"
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

    def test_accepts_explicit_canonical_release_artifact_facts(self) -> None:
        self.assertEqual([], self.validate_release_allowlist())

    def test_rejects_release_authority_that_claims_support_promotion(self) -> None:
        self.validate_release_allowlist()
        self.release_allowlist["authorityLimits"]["runtimeSupportPromotion"] = True
        self.write_json(
            self.root / "testdata/golden/release-canonical-v1.json",
            self.release_allowlist,
        )

        errors: list[str] = []
        VALIDATOR.validate_canonical_release_allowlist(
            self.root,
            errors,
            expected_summary=self.release_allowlist["selectionSummary"],
        )

        self.assertTrue(any("runtime support promotion" in error for error in errors))

    def test_rejects_release_artifact_role_drift(self) -> None:
        self.validate_release_allowlist()
        self.release_allowlist["cases"][0]["artifacts"][0]["role"] = "expected"
        self.write_json(
            self.root / "testdata/golden/release-canonical-v1.json",
            self.release_allowlist,
        )

        errors: list[str] = []
        VALIDATOR.validate_canonical_release_allowlist(
            self.root,
            errors,
            expected_summary=self.release_allowlist["selectionSummary"],
        )

        self.assertTrue(any("role differs" in error for error in errors))

    def test_accepts_exact_fact_scoped_alias_facts(self) -> None:
        self.validate_release_allowlist()
        self.add_release_alias()

        errors: list[str] = []
        VALIDATOR.validate_canonical_release_allowlist(
            self.root,
            errors,
            expected_summary=self.release_allowlist["selectionSummary"],
        )

        self.assertEqual([], errors)

    def test_rejects_fact_scoped_alias_source_drift(self) -> None:
        self.validate_release_allowlist()
        release_alias = self.add_release_alias()
        release_alias["alias"]["sourceCaseId"] = "different-source"
        self.write_json(
            self.root / "testdata/golden/release-canonical-v1.json",
            self.release_allowlist,
        )

        errors: list[str] = []
        VALIDATOR.validate_canonical_release_allowlist(
            self.root,
            errors,
            expected_summary=self.release_allowlist["selectionSummary"],
        )

        self.assertTrue(any("alias differs" in error for error in errors))

    def test_rejects_alias_without_selected_same_workflow_direct_golden(self) -> None:
        self.validate_release_allowlist()
        self.add_release_alias()
        self.release_allowlist["cases"] = [self.release_allowlist["cases"][1]]
        self.release_allowlist["selectionSummary"].update(
            {
                "caseCount": 1,
                "directGoldenCount": 0,
                "factScopedAliasCount": 1,
                "artifactDeclarationCount": 0,
                "uniqueArtifactPathCount": 0,
            }
        )
        self.write_json(
            self.root / "testdata/golden/release-canonical-v1.json",
            self.release_allowlist,
        )

        errors: list[str] = []
        VALIDATOR.validate_canonical_release_allowlist(
            self.root,
            errors,
            expected_summary=self.release_allowlist["selectionSummary"],
        )

        self.assertTrue(
            any(
                "must select its exact same-workflow direct Golden source" in error
                for error in errors
            )
        )

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
            self.root / "testdata/golden/release-canonical-v1.json",
            self.release_allowlist,
        )

        errors: list[str] = []
        VALIDATOR.validate_canonical_release_allowlist(
            self.root,
            errors,
            expected_summary=self.release_allowlist["selectionSummary"],
        )

        self.assertTrue(any("sha256 differs" in error for error in errors))

    def test_rejects_release_case_manifest_path_drift(self) -> None:
        self.validate_release_allowlist()
        self.release_allowlist["cases"][0]["manifestPath"] = "wrong/case.json"
        self.write_json(
            self.root / "testdata/golden/release-canonical-v1.json",
            self.release_allowlist,
        )

        errors: list[str] = []
        VALIDATOR.validate_canonical_release_allowlist(
            self.root,
            errors,
            expected_summary=self.release_allowlist["selectionSummary"],
        )

        self.assertTrue(any("does not match" in error for error in errors))

    def test_rejects_release_case_manifest_exact_byte_drift(self) -> None:
        self.validate_release_allowlist()
        case_manifest_path = (
            self.canonical / self.release_allowlist["cases"][0]["manifestPath"]
        )
        changed_manifest = json.loads(case_manifest_path.read_text(encoding="utf-8"))
        changed_manifest["privateMetadata"] = {"classification": "unapproved"}
        self.write_json(case_manifest_path, changed_manifest)

        errors: list[str] = []
        VALIDATOR.validate_canonical_release_allowlist(
            self.root,
            errors,
            expected_summary=self.release_allowlist["selectionSummary"],
        )

        self.assertTrue(any("manifestSha256 differs" in error for error in errors))

    def test_rejects_release_canonical_readme_exact_byte_drift(self) -> None:
        self.validate_release_allowlist()
        (self.canonical / "README.md").write_text(
            "self-consistent replacement\n", encoding="utf-8"
        )

        errors: list[str] = []
        VALIDATOR.validate_canonical_release_allowlist(
            self.root,
            errors,
            expected_summary=self.release_allowlist["selectionSummary"],
        )

        self.assertTrue(
            any("canonicalReadmeSha256 differs" in error for error in errors)
        )

    def test_rejects_non_boolean_release_direct_golden(self) -> None:
        for invalid_value in (1, "false", None):
            with self.subTest(direct_golden=invalid_value):
                self.validate_release_allowlist()
                if invalid_value is None:
                    del self.release_allowlist["cases"][0]["directGolden"]
                else:
                    self.release_allowlist["cases"][0]["directGolden"] = invalid_value
                self.write_json(
                    self.root / "testdata/golden/release-canonical-v1.json",
                    self.release_allowlist,
                )

                errors: list[str] = []
                VALIDATOR.validate_canonical_release_allowlist(
                    self.root,
                    errors,
                    expected_summary=self.release_allowlist["selectionSummary"],
                )

                self.assertTrue(
                    any("directGolden must be a boolean" in error for error in errors)
                )

    def test_rejects_payload_hash_drift(self) -> None:
        (
            self.case_directory
            / ("expected/nt51927-standard-merge-gen-flash-expected-output.bin")
        ).write_bytes(b"changed")

        errors = self.validate()

        self.assertTrue(any("size mismatch" in error for error in errors))
        self.assertTrue(any("SHA-256 mismatch" in error for error in errors))

    def test_rejects_same_size_payload_hash_drift(self) -> None:
        path = self.case_directory / (
            "expected/nt51927-standard-merge-gen-flash-expected-output.bin"
        )
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
        path = self.case_directory / (
            "expected/nt51927-standard-merge-gen-flash-expected-output.bin"
        )
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
        (
            self.case_directory
            / ("expected/nt51927-standard-merge-gen-flash-expected-output.bin")
        ).unlink()

        errors = self.validate()

        self.assertTrue(
            any("cannot resolve canonical artifact" in error for error in errors)
        )

    def test_rejects_direct_case_without_expected_role(self) -> None:
        self.case_manifest["artifacts"] = [self.case_manifest["artifacts"][0]]
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(any("exactly one expected" in error for error in errors))

    def test_rejects_direct_case_without_input_role(self) -> None:
        self.case_manifest["artifacts"] = [self.case_manifest["artifacts"][1]]
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(any("requires input artifacts" in error for error in errors))

    def test_rejects_direct_case_with_multiple_expected_roles(self) -> None:
        duplicate_path = self.case_directory / "expected/nt51927-flash-copy.bin"
        duplicate_path.write_bytes(
            (
                self.case_directory
                / ("expected/nt51927-standard-merge-gen-flash-expected-output.bin")
            ).read_bytes()
        )
        duplicate = dict(self.case_manifest["artifacts"][1])
        duplicate["artifactId"] = "expected-output-copy"
        duplicate["path"] = duplicate_path.relative_to(self.canonical).as_posix()
        self.case_manifest["artifacts"].append(duplicate)
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(any("exactly one expected" in error for error in errors))

    def test_rejects_canonical_bin_filename_without_case_ic(self) -> None:
        artifact = self.case_manifest["artifacts"][0]
        original_path = self.canonical / artifact["path"]
        ambiguous_path = original_path.with_name("tp_bin.bin")
        original_path.rename(ambiguous_path)
        artifact["path"] = ambiguous_path.relative_to(self.canonical).as_posix()
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(
            any("filename must identify case IC NT51927" in error for error in errors)
        )

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

    def test_rejects_case_without_test_disposition(self) -> None:
        del self.case_manifest["testDisposition"]
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(
            any("must declare exactly one testDisposition" in error for error in errors)
        )

    def test_rejects_unknown_test_disposition_kind(self) -> None:
        self.case_manifest["testDisposition"]["kind"] = "runner-decides"
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(any("unsupported kind" in error for error in errors))

    def test_rejects_overlapping_allowed_difference_ranges(self) -> None:
        self.case_manifest["allowedByteDifferenceContract"] = {
            "addressSpaceId": "output-image",
            "allowedDifferenceRanges": [
                {"start": "0x0", "endExclusive": "0x2", "classification": "first"},
                {"start": "0x1", "endExclusive": "0x3", "classification": "overlap"},
            ],
        }
        self.case_manifest["testDisposition"] = {
            "kind": "allowed-byte-difference",
            "evidenceRefs": ["tests/golden_runner.py#direct_full_output"],
            "differenceContractProperty": "allowedByteDifferenceContract",
        }
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(any("sorted and non-overlapping" in error for error in errors))

    def test_rejects_allowed_difference_runner_that_does_not_consume_manifest_ranges(
        self,
    ) -> None:
        self.case_manifest["allowedByteDifferenceContract"] = {
            "addressSpaceId": "output-image",
            "allowedDifferenceRanges": [
                {"start": "0x0", "endExclusive": "0x1", "classification": "crc"},
            ],
        }
        self.case_manifest["testDisposition"] = {
            "kind": "allowed-byte-difference",
            "evidenceRefs": ["tests/golden_runner.py#direct_full_output"],
            "differenceContractProperty": "allowedByteDifferenceContract",
        }
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(
            any("must consume the case-local typed ranges" in error for error in errors)
        )

    def test_rejects_test_evidence_outside_tests_tree(self) -> None:
        self.case_manifest["testDisposition"]["evidenceRefs"] = [
            "scripts/golden_runner.py#direct_full_output"
        ]
        self.rewrite_case()

        errors = self.validate()

        self.assertTrue(any("below tests/" in error for error in errors))

    def test_rejects_retired_active_ctrlram_fixture_authority(self) -> None:
        retired_manifest = self.root / "testdata/golden/ctrlram-replace/manifest.json"
        retired_manifest.parent.mkdir(parents=True)
        retired_manifest.write_text("{}\n", encoding="utf-8")

        errors = self.validate()

        self.assertTrue(
            any(
                "retired active CtrlRAM fixture authority must stay absent" in error
                for error in errors
            )
        )

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

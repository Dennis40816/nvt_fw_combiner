"""Red tests for the fail-closed external firmware-owner approval boundary."""

from __future__ import annotations

import copy
import base64
import hashlib
import io
import json
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest.mock import patch

import yaml

from tests.scripts.v0916_parity_test_support import (
    MODULE,
    ROOT,
    RecordingProtectedApprovalReader,
    UnavailableProtectedApprovalReader,
    V0916ParityTestBase,
    parity_workflow_fixture_from_contract,
)


class UnavailableFirmwareOwnerVerifier:
    def verify(self, attestation: dict[str, object], record: bytes) -> object:
        raise OSError("external firmware-owner verifier is unavailable")


class RecordingFirmwareOwnerVerifier:
    def __init__(self, result: dict[str, object]) -> None:
        self.result = copy.deepcopy(result)
        self.calls: list[tuple[dict[str, object], bytes]] = []

    def verify(self, attestation: dict[str, object], record: bytes) -> dict[str, object]:
        self.calls.append((copy.deepcopy(attestation), bytes(record)))
        return copy.deepcopy(self.result)


class V0916ParityApprovalTests(V0916ParityTestBase):
    def test_workflow_semantic_contract_raw_identity_and_schema_without_production(self) -> None:
        plan = json.loads(self.plan_path.read_text(encoding="utf-8"))
        reference = plan["candidateAuthority"]["protectedBuild"][
            "workflowSemanticContract"
        ]
        contract_path = ROOT / reference["path"]
        contract_bytes = contract_path.read_bytes()
        self.assertEqual(reference["size"], len(contract_bytes))
        self.assertEqual(reference["sha256"], hashlib.sha256(contract_bytes).hexdigest())
        contract = json.loads(contract_bytes)
        schema = json.loads(
            (ROOT / "docs/contracts/v0916-parity-workflow-v1.schema.json").read_text(
                encoding="utf-8"
            )
        )
        self.assertEqual(
            "https://json-schema.org/draft/2020-12/schema", schema["$schema"]
        )
        self.assertEqual(set(schema["required"]), set(contract))
        self.assertEqual(
            {
                "v0916-parity-compare",
                "v0916-parity-attestation",
                "v0916-parity-finalize",
            },
            set(contract["jobs"]),
        )

    def test_release_workflow_yaml_has_exact_protected_job_graph(self) -> None:
        contract = json.loads(
            (ROOT / "docs/contracts/v0916-parity-workflow-v1.json").read_text(
                encoding="utf-8"
            )
        )
        workflow_path = ROOT / ".github/workflows/release.yml"
        MODULE.validate_protected_workflow_semantics(
            workflow_path.read_bytes(), contract
        )

    def test_workflow_semantics_reject_every_bypass_or_authority_drift(self) -> None:
        contract = json.loads(
            (ROOT / "docs/contracts/v0916-parity-workflow-v1.json").read_text(
                encoding="utf-8"
            )
        )
        valid = parity_workflow_fixture_from_contract(contract)
        MODULE.validate_protected_workflow_semantics(valid, contract)
        for mutation in (
            "job-swap",
            "top-trigger",
            "top-permissions",
            "unpinned-action",
            "action-map-swap",
            "command-argv",
            "artifact-name",
            "artifact-path",
            "needs-order",
            "condition-bypass",
            "write-permission",
            "timeout",
            "environment",
            "job-name",
            "runs-on",
            "checkout-ref",
            "checkout-option",
            "continue-on-error",
            "step-order",
            "promotion-needs",
            "promotion-condition",
            "promotion-step-condition",
            "promotion-step-before-gate",
            "extra-job",
            "top-defaults",
            "top-env",
        ):
            invalid = copy.deepcopy(valid)
            compare = invalid["jobs"]["v0916-parity-compare"]
            attestation = invalid["jobs"]["v0916-parity-attestation"]
            promotion = invalid["jobs"]["promote"]
            if mutation == "job-swap":
                invalid["jobs"]["v0916-parity-compare"], invalid["jobs"]["v0916-parity-attestation"] = (
                    attestation,
                    compare,
                )
            elif mutation == "top-trigger":
                invalid["on"] = {"push": {}}
            elif mutation == "top-permissions":
                invalid["permissions"]["contents"] = "write"
            elif mutation == "unpinned-action":
                compare["steps"][0]["uses"] = "actions/checkout@v7"
            elif mutation == "action-map-swap":
                compare["steps"][0]["uses"] = contract["actionPins"]["downloadArtifact"]
            elif mutation == "command-argv":
                compare["steps"][2]["run"] += " --allow-mismatch"
            elif mutation == "artifact-name":
                compare["steps"][3]["with"]["name"] = "other"
            elif mutation == "artifact-path":
                attestation["steps"][1]["with"]["path"] = "artifacts/other"
            elif mutation == "needs-order":
                attestation["needs"].reverse()
            elif mutation == "condition-bypass":
                compare["if"] = "${{ always() }}"
            elif mutation == "write-permission":
                attestation["permissions"]["contents"] = "write"
            elif mutation == "timeout":
                compare["timeout-minutes"] = 61
            elif mutation == "environment":
                attestation.pop("environment")
            elif mutation == "job-name":
                compare["name"] = "release / parity maybe"
            elif mutation == "runs-on":
                compare["runs-on"] = "ubuntu-latest"
            elif mutation == "checkout-ref":
                compare["steps"][0]["with"]["ref"] = "${{ github.sha }}"
            elif mutation == "checkout-option":
                compare["steps"][0]["with"]["persist-credentials"] = True
            elif mutation == "continue-on-error":
                compare["steps"][2]["continue-on-error"] = True
            elif mutation == "step-order":
                compare["steps"][1], compare["steps"][2] = (
                    compare["steps"][2],
                    compare["steps"][1],
                )
            elif mutation == "promotion-needs":
                promotion["needs"] = ["candidate"]
            elif mutation == "promotion-condition":
                promotion["if"] = "${{ always() }}"
            elif mutation == "promotion-step-condition":
                promotion["steps"][2]["if"] = "${{ always() }}"
            elif mutation == "promotion-step-before-gate":
                promotion["steps"].insert(
                    3,
                    {
                        "name": "Create release before terminal validation",
                        "shell": "pwsh",
                        "run": "gh release create v1.0.0",
                    },
                )
            elif mutation == "extra-job":
                invalid["jobs"]["v0916-parity-bypass"] = copy.deepcopy(compare)
            elif mutation == "top-defaults":
                invalid["defaults"] = {"run": {"shell": "malicious-wrapper"}}
            else:
                invalid["env"] = {"PATH": "malicious"}
            with self.subTest(workflow_mutation=mutation):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.validate_protected_workflow_semantics(invalid, contract)
                self.assertEqual("PARITY_WORKFLOW_MISMATCH", captured.exception.code)

    def test_deployment_status_creator_is_never_treated_as_firmware_owner(self) -> None:
        observed_github = {
            "run": {"id": 123, "head_sha": "1" * 40, "conclusion": "success"},
            "job": {"id": 500, "run_id": 123, "conclusion": "success"},
            "deployment": {"id": 600, "sha": "1" * 40, "environment": "firmware-parity"},
            "deploymentStatuses": [{
                "id": 601,
                "state": "success",
                "creator": {"login": "someone-who-is-not-provably-the-reviewer"},
                "created_at": "2026-08-26T00:02:00Z",
            }],
        }
        with self.assertRaises(MODULE.ParityError) as captured:
            MODULE.infer_firmware_owner_from_github(observed_github)
        self.assertEqual("PARITY_OWNER_APPROVAL_REQUIRED", captured.exception.code)

    def test_local_attestation_cannot_pass_without_external_verifier(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self._finalize_fixture(Path(temporary))
            for verifier in (None, UnavailableFirmwareOwnerVerifier()):
                with self.subTest(verifier=type(verifier).__name__ if verifier else "none"):
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.finalize_evidence(
                            fixture["finalizePath"],
                            github_reader=fixture["githubReader"],
                            firmware_owner_verifier=verifier,
                        )
                    self.assertEqual("PARITY_OWNER_APPROVAL_REQUIRED", captured.exception.code)

    def test_verified_external_result_must_bind_every_attestation_fact(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self._finalize_fixture(Path(temporary))
            expected = fixture["expectedVerification"]
            keys = {
                "attestation-id": "attestationId", "owner-id": "firmwareOwnerId",
                "attestation-sha": "attestationSha256", "record-sha": "verificationRecordSha256",
                "comparison-sha": "comparisonSha256",
                "comparison-artifact-id": "comparisonArtifactId",
                "comparison-artifact-digest": "comparisonArtifactDigest",
                "plan": "planSha256", "policy": "policySha256",
                "head": "implementationHead", "tree": "implementationTree",
                "package": "candidatePackageSha256", "routes": "routeEvidenceSha256",
                "manifest": "candidateManifestSha256",
                "candidate-artifact": "candidateArtifactDigest",
                "receipts": "receiptSetSha256", "operators": "authorizedOperators",
            }
            for mutation, key in keys.items():
                result = copy.deepcopy(expected)
                if key == "comparisonArtifactId":
                    result[key] = 999
                elif key == "authorizedOperators":
                    result[key] = ["other-operator"]
                elif key in {"comparisonArtifactDigest", "candidateArtifactDigest"}:
                    result[key] = "sha256:" + "0" * 64
                else:
                    result[key] = "0" * (40 if key in {"implementationHead", "implementationTree"} else 64)
                with self.subTest(verification_mutation=mutation):
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.finalize_evidence(
                            fixture["finalizePath"],
                            github_reader=fixture["githubReader"],
                            firmware_owner_verifier=RecordingFirmwareOwnerVerifier(result),
                        )
                    self.assertEqual("PARITY_OWNER_APPROVAL_REQUIRED", captured.exception.code)

    def test_complete_comparison_can_finalize_only_with_exact_external_owner_verification(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self._finalize_fixture(Path(temporary))
            verifier = RecordingFirmwareOwnerVerifier(
                fixture["expectedVerification"]
            )
            comparison = json.loads(Path(fixture["finalizePath"]).read_text(encoding="utf-8"))
            comparison_payload = json.loads(
                Path(comparison["comparison"]["path"]).read_text(encoding="utf-8")
            )
            self.assertNotEqual(
                fixture["protectedRun"]["workflowCommitSha"],
                comparison_payload["candidateAuthority"]["implementationHead"],
            )
            evidence = MODULE.finalize_evidence(
                fixture["finalizePath"],
                github_reader=fixture["githubReader"],
                firmware_owner_verifier=verifier,
            )
            self.assertEqual("pass", evidence["verdict"])
            self.assertEqual(64, len(evidence["routes"]))
            self.assertEqual(1, len(verifier.calls))
            for key, value in fixture["protectedRun"].items():
                if key.endswith("Artifact"):
                    for artifact_key, artifact_value in value.items():
                        self.assertEqual(
                            artifact_value,
                            evidence["protectedRun"][key][artifact_key],
                        )
                else:
                    self.assertEqual(value, evidence["protectedRun"][key])
            self.assertIn("member", evidence["protectedRun"]["comparisonArtifact"])
            self.assertEqual(
                fixture["expectedVerification"]["attestationId"],
                evidence["protectedRun"]["ownerVerification"]["attestationId"],
            )

            for mutation in (
                "owner-attestation-id",
                "owner-record-sha",
                "comparison-member",
                "comparison-canonical-bytes",
            ):
                forged = copy.deepcopy(evidence)
                if mutation == "owner-attestation-id":
                    forged["protectedRun"]["ownerVerification"][
                        "attestationId"
                    ] = "forged-attestation"
                elif mutation == "owner-record-sha":
                    forged["protectedRun"]["ownerVerification"][
                        "verificationRecordSha256"
                    ] = "0" * 64
                elif mutation == "comparison-member":
                    forged["protectedRun"]["comparisonArtifact"]["member"][
                        "sha256"
                    ] = "0" * 64
                else:
                    forged["comparison"]["sha256"] = "0" * 64
                    forged["protectedRun"]["comparisonArtifact"]["member"][
                        "sha256"
                    ] = "0" * 64
                with self.subTest(terminal_mutation=mutation):
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.validate_terminal_evidence(
                            forged
                        )
                    self.assertEqual(
                        "PARITY_EVIDENCE_INCOMPLETE", captured.exception.code
                    )

    def test_finalization_uses_each_verified_local_artifact_capture_once(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self._finalize_fixture(Path(temporary))
            original_reader = MODULE._read_artifact_reference
            originals: dict[Path, bytes] = {}
            captured_roles: set[str] = set()

            def capture_then_swap(
                reference: dict[str, object], role: str
            ) -> tuple[Path, bytes]:
                path, payload = original_reader(reference, role)
                if role in {
                    "comparison",
                    "firmware-owner-attestation",
                    "verification-record",
                }:
                    originals[path] = payload
                    captured_roles.add(role)
                    path.write_bytes(b"post-capture-finalization-swap")
                return path, payload

            try:
                with patch.object(
                    MODULE,
                    "_read_artifact_reference",
                    side_effect=capture_then_swap,
                ):
                    evidence = MODULE.finalize_evidence(
                        fixture["finalizePath"],
                        github_reader=fixture["githubReader"],
                        firmware_owner_verifier=RecordingFirmwareOwnerVerifier(
                            fixture["expectedVerification"]
                        ),
                    )
            finally:
                for path, payload in originals.items():
                    path.write_bytes(payload)
            self.assertEqual(
                {
                    "comparison",
                    "firmware-owner-attestation",
                    "verification-record",
                },
                captured_roles,
            )
            self.assertEqual("pass", evidence["verdict"])
            self.assertEqual(
                hashlib.sha256(originals[next(
                    path for path in originals if path.name == "comparison.json"
                )]).hexdigest(),
                evidence["comparison"]["sha256"],
            )

    def test_finalization_accepts_same_run_while_finalizer_job_is_still_running(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self._finalize_fixture(Path(temporary))
            reader = fixture["githubReader"]
            reader.run["status"] = "in_progress"
            reader.run["conclusion"] = None
            reader.run["updated_at"] = "2026-08-26T00:06:30Z"

            evidence = MODULE.finalize_evidence(
                fixture["finalizePath"],
                github_reader=reader,
                firmware_owner_verifier=RecordingFirmwareOwnerVerifier(
                    fixture["expectedVerification"]
                ),
            )

            self.assertEqual("pass", evidence["verdict"])

    def test_finalization_requires_independently_queried_same_run_job_and_artifact_owners(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self._finalize_fixture(Path(temporary))
            for mutation in (
                "workflow-blob", "workflow-bytes", "run-id", "run-head", "run-branch", "run-repository-id",
                "run-repository-name", "run-head-repository-id",
                "run-head-repository-name", "run-status", "run-conclusion", "job-id",
                "job-run", "job-attempt", "job-head", "job-branch",
                "job-status", "job-conclusion", "deployment-head", "deployment-ref",
                "deployment-environment", "status-state", "status-cross-job",
                "comparison-after-deployment", "deployment-after-job",
                "attestation-after-job", "status-before-job",
                "comparison-artifact-run", "comparison-artifact-head",
                "comparison-artifact-branch", "comparison-artifact-repository-id",
                "comparison-artifact-head-repository-id", "attestation-artifact-run",
                "attestation-artifact-head", "attestation-artifact-branch",
                "attestation-artifact-repository-id",
                "attestation-artifact-head-repository-id",
                "comparison-artifact-bytes", "attestation-artifact-bytes",
                "comparison-artifact-name", "attestation-artifact-name",
                "comparison-artifact-digest", "attestation-artifact-digest",
                "comparison-artifact-expired", "attestation-artifact-expired",
            ):
                reader = copy.deepcopy(fixture["githubReader"])
                if mutation == "workflow-blob":
                    reader.workflow_content["sha"] = "0" * 40
                elif mutation == "workflow-bytes":
                    reader.workflow_content["content"] = base64.b64encode(
                        b"name: substituted\n"
                    ).decode("ascii")
                elif mutation == "run-id":
                    reader.run["id"] = 999
                elif mutation == "run-head":
                    reader.run["head_sha"] = "0" * 40
                elif mutation == "run-branch":
                    reader.run["head_branch"] = "other"
                elif mutation == "run-repository-id":
                    reader.run["repository"]["id"] = 999
                elif mutation == "run-repository-name":
                    reader.run["repository"]["full_name"] = "other/repository"
                elif mutation == "run-head-repository-id":
                    reader.run["head_repository"]["id"] = 999
                elif mutation == "run-head-repository-name":
                    reader.run["head_repository"]["full_name"] = "other/repository"
                elif mutation == "run-status":
                    reader.run["status"] = "in_progress"
                elif mutation == "run-conclusion":
                    reader.run["conclusion"] = "failure"
                elif mutation == "job-id":
                    reader.job["id"] = 999
                elif mutation == "job-run":
                    reader.job["run_id"] = 999
                elif mutation == "job-attempt":
                    reader.job["run_attempt"] = 2
                elif mutation == "job-head":
                    reader.job["head_sha"] = "0" * 40
                elif mutation == "job-branch":
                    reader.job["head_branch"] = "other"
                elif mutation == "job-status":
                    reader.job["status"] = "in_progress"
                elif mutation == "job-conclusion":
                    reader.job["conclusion"] = "failure"
                elif mutation == "deployment-head":
                    reader.deployment["sha"] = "0" * 40
                elif mutation == "deployment-ref":
                    reader.deployment["ref"] = "other"
                elif mutation == "deployment-environment":
                    reader.deployment["environment"] = "release"
                elif mutation == "status-state":
                    reader.deployment_statuses[0]["state"] = "failure"
                elif mutation == "status-cross-job":
                    reader.deployment_statuses[0]["log_url"] = (
                        "https://github.com/Dennis40816/nvt_fw_combiner/actions/runs/123/job/999"
                    )
                elif mutation == "comparison-after-deployment":
                    reader.artifacts[700][0]["created_at"] = "2026-08-26T00:02:30Z"
                elif mutation == "deployment-after-job":
                    reader.deployment["created_at"] = "2026-08-26T00:03:30Z"
                elif mutation == "attestation-after-job":
                    reader.artifacts[701][0]["created_at"] = "2026-08-26T00:05:30Z"
                elif mutation == "status-before-job":
                    reader.deployment_statuses[0]["created_at"] = "2026-08-26T00:02:30Z"
                elif mutation in {"comparison-artifact-bytes", "attestation-artifact-bytes"}:
                    artifact_id = 700 if mutation.startswith("comparison") else 701
                    metadata, archive = reader.artifacts[artifact_id]
                    reader.artifacts[artifact_id] = (metadata, archive + b"drift")
                elif mutation.endswith("artifact-name"):
                    artifact_id = 700 if mutation.startswith("comparison") else 701
                    reader.artifacts[artifact_id][0]["name"] = "substituted"
                elif mutation.endswith("artifact-digest"):
                    artifact_id = 700 if mutation.startswith("comparison") else 701
                    reader.artifacts[artifact_id][0]["digest"] = "sha256:" + "0" * 64
                elif mutation.endswith("artifact-expired"):
                    artifact_id = 700 if mutation.startswith("comparison") else 701
                    reader.artifacts[artifact_id][0]["expired"] = True
                else:
                    artifact_id = 700 if mutation.startswith("comparison") else 701
                    owner = reader.artifacts[artifact_id][0]["workflow_run"]
                    field = mutation.rsplit("-", 1)[-1]
                    if field == "run":
                        owner["id"] = 999
                    elif field == "head":
                        owner["head_sha"] = "0" * 40
                    elif field == "branch":
                        owner["head_branch"] = "other"
                    elif mutation.endswith("repository-id") and not mutation.endswith("head-repository-id"):
                        owner["repository_id"] = 999
                    else:
                        owner["head_repository_id"] = 999
                with self.subTest(github_mutation=mutation):
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.finalize_evidence(
                            fixture["finalizePath"],
                            github_reader=reader,
                            firmware_owner_verifier=RecordingFirmwareOwnerVerifier(
                                fixture["expectedVerification"]
                            ),
                        )
                    self.assertEqual("PARITY_AUTHORITY_MISMATCH", captured.exception.code)

    def test_finalization_fails_closed_when_github_authority_is_unavailable(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            fixture = self._finalize_fixture(Path(temporary))
            with self.assertRaises(MODULE.ParityError) as captured:
                MODULE.finalize_evidence(
                    fixture["finalizePath"],
                    github_reader=UnavailableProtectedApprovalReader(),
                    firmware_owner_verifier=RecordingFirmwareOwnerVerifier(
                        fixture["expectedVerification"]
                    ),
                )
            self.assertEqual("PARITY_AUTHORITY_MISMATCH", captured.exception.code)

    def _finalize_fixture(self, root: Path) -> dict[str, object]:
        comparison = self._schema_complete_comparison_fixture()
        comparison_path = root / "comparison.json"
        comparison_path.write_text(json.dumps(comparison), encoding="utf-8")
        attestation = {
            "schemaVersion": "1.0",
            "authority": {
                "kind": "external-firmware-owner-attestation",
                "attestationId": "FW-OWNER-1.0.0-001", "firmwareOwnerId": "fw-owner",
                "issuedAtUtc": "2026-08-26T00:03:00Z", "verificationRequired": True,
            },
            "binding": {
                "comparisonSha256": hashlib.sha256(comparison_path.read_bytes()).hexdigest(),
                "comparisonArtifactId": 700, "comparisonArtifactDigest": "sha256:" + "8" * 64,
                "planSha256": comparison["planSha256"],
                "policySha256": comparison["policySha256"],
                "implementationHead": comparison["candidateAuthority"]["implementationHead"],
                "implementationTree": comparison["candidateAuthority"]["implementationTree"],
                "candidatePackageSha256": comparison["candidatePackage"]["sha256"],
                "candidateManifestSha256": comparison["candidateBuild"]["candidateManifest"]["sha256"],
                "candidateArtifactDigest": comparison["candidateBuild"]["artifactDigest"],
                "routeEvidenceSha256": comparison["routeEvidenceSha256"],
                "receiptSetSha256": comparison["receiptSetSha256"],
            },
            "authorizedOperators": ["dennis40816"],
            "issuedAtUtc": "2026-08-26T00:03:00Z", "verdict": "approved",
        }
        attestation_path = root / "owner-attestation.json"
        attestation_path.write_text(json.dumps(attestation), encoding="utf-8")
        verification_path = root / "external-verification.json"
        verification_path.write_text("{\"status\":\"externally-verified\"}", encoding="utf-8")
        workflow_contract = json.loads(
            (ROOT / "docs/contracts/v0916-parity-workflow-v1.json").read_text(
                encoding="utf-8"
            )
        )
        workflow_bytes = yaml.safe_dump(
            parity_workflow_fixture_from_contract(workflow_contract),
            sort_keys=False,
        ).encode("utf-8")
        workflow_blob_sha = hashlib.sha1(
            f"blob {len(workflow_bytes)}\0".encode("ascii") + workflow_bytes
        ).hexdigest()
        candidate_head = "1d1d1cfcad7f0963dd3ed1e3e920d9a3425d6220"
        workflow_head = "b" * 40
        self.assertNotEqual(candidate_head, workflow_head)
        comparison_archive = self._single_file_archive(
            "comparison.json", comparison_path.read_bytes()
        )
        attestation_archive = self._single_file_archive(
            "owner-attestation.json", attestation_path.read_bytes()
        )
        verification_archive = self._single_file_archive(
            "external-verification.json", verification_path.read_bytes()
        )
        artifact_owner = {
            "id": 123, "repository_id": 40816, "head_repository_id": 40816,
            "head_branch": "main", "head_sha": workflow_head,
        }
        comparison_digest = "sha256:" + hashlib.sha256(comparison_archive).hexdigest()
        attestation_digest = "sha256:" + hashlib.sha256(attestation_archive).hexdigest()
        verification_digest = "sha256:" + hashlib.sha256(verification_archive).hexdigest()
        protected_run = {
            "repository": "Dennis40816/nvt_fw_combiner",
            "repositoryId": 40816, "headRepositoryId": 40816,
            "workflowPath": ".github/workflows/release.yml",
            "workflowRef": "refs/heads/main", "workflowCommitSha": workflow_head,
            "workflowBlobSha": workflow_blob_sha,
            "workflowRawSha256": hashlib.sha256(workflow_bytes).hexdigest(),
            "workflowSemanticContractSha256": "9f0272d7299de222a662d39342b86efadd4f1625651c2c3785b71f77010bcb47",
            "workflowRun": {
                "id": 123, "runAttempt": 1, "headSha": workflow_head,
                "headBranch": "main", "event": "workflow_dispatch", "status": "completed",
                "conclusion": "success", "repositoryId": 40816,
                "headRepositoryId": 40816,
                "createdAtUtc": "2026-08-26T00:00:00Z",
                "updatedAtUtc": "2026-08-26T00:06:00Z",
            },
            "attestationJob": {
                "id": 500, "runId": 123, "runAttempt": 1, "headSha": workflow_head,
                "headBranch": "main",
                "name": "release / v0.9.16 parity attestation", "status": "completed",
                "conclusion": "success",
                "startedAtUtc": "2026-08-26T00:03:00Z",
                "completedAtUtc": "2026-08-26T00:05:00Z",
                "htmlUrl": "https://github.com/Dennis40816/nvt_fw_combiner/actions/runs/123/job/500",
            },
            "deployment": {
                "id": 600, "sha": workflow_head, "ref": "main",
                "environment": "firmware-parity",
                "createdAtUtc": "2026-08-26T00:02:00Z",
            },
            "deploymentStatus": {
                "id": 601, "state": "success",
                "createdAtUtc": "2026-08-26T00:05:00Z",
                "updatedAtUtc": "2026-08-26T00:05:00Z",
                "logUrl": "https://github.com/Dennis40816/nvt_fw_combiner/actions/runs/123/job/500",
            },
            "comparisonArtifact": {
                "id": 700, "name": "v0916-parity-comparison-123",
                "digest": comparison_digest, "memberName": "comparison.json",
                "createdAtUtc": "2026-08-26T00:01:30Z",
                "workflowRun": {
                    "id": 123, "repositoryId": 40816, "headRepositoryId": 40816,
                    "headBranch": "main", "headSha": workflow_head,
                },
            },
            "attestationArtifact": {
                "id": 701, "name": "v0916-parity-attestation-123",
                "digest": attestation_digest, "memberName": "owner-attestation.json",
                "createdAtUtc": "2026-08-26T00:04:30Z",
                "workflowRun": {
                    "id": 123, "repositoryId": 40816, "headRepositoryId": 40816,
                    "headBranch": "main", "headSha": workflow_head,
                },
            },
            "verificationArtifact": {
                "id": 702, "name": "v0916-parity-verification-123",
                "digest": verification_digest,
                "memberName": "external-verification.json",
                "createdAtUtc": "2026-08-26T00:04:31Z",
                "workflowRun": {
                    "id": 123, "repositoryId": 40816,
                    "headRepositoryId": 40816,
                    "headBranch": "main", "headSha": workflow_head,
                },
            },
        }
        attestation["binding"]["comparisonArtifactDigest"] = comparison_digest
        attestation["binding"]["comparisonArtifactId"] = 700
        attestation_path.write_text(json.dumps(attestation), encoding="utf-8")
        attestation_archive = self._single_file_archive(
            "owner-attestation.json", attestation_path.read_bytes()
        )
        attestation_digest = "sha256:" + hashlib.sha256(attestation_archive).hexdigest()
        protected_run["attestationArtifact"]["digest"] = attestation_digest
        github_reader = RecordingProtectedApprovalReader(
            workflow_content={
                "sha": workflow_blob_sha, "encoding": "base64",
                "content": base64.b64encode(workflow_bytes).decode("ascii"),
            },
            run={
                "id": 123, "run_attempt": 1, "head_sha": workflow_head,
                "head_branch": "main", "event": "workflow_dispatch",
                "status": "completed", "conclusion": "success",
                "repository": {"id": 40816, "full_name": "Dennis40816/nvt_fw_combiner"},
                "head_repository": {"id": 40816, "full_name": "Dennis40816/nvt_fw_combiner"},
                "created_at": "2026-08-26T00:00:00Z",
                "updated_at": "2026-08-26T00:06:00Z",
            },
            job={
                "id": 500, "run_id": 123, "run_attempt": 1, "head_sha": workflow_head,
                "head_branch": "main", "name": "release / v0.9.16 parity attestation",
                "status": "completed", "conclusion": "success",
                "started_at": "2026-08-26T00:03:00Z",
                "completed_at": "2026-08-26T00:05:00Z",
                "html_url": "https://github.com/Dennis40816/nvt_fw_combiner/actions/runs/123/job/500",
            },
            deployment={
                "id": 600, "sha": workflow_head, "ref": "main",
                "environment": "firmware-parity",
                "created_at": "2026-08-26T00:02:00Z",
            },
            deployment_statuses=[{
                "id": 601, "state": "success",
                "created_at": "2026-08-26T00:05:00Z",
                "updated_at": "2026-08-26T00:05:00Z",
                "log_url": "https://github.com/Dennis40816/nvt_fw_combiner/actions/runs/123/job/500",
                "creator": {"login": "github-actions[bot]"},
            }],
            artifacts={
                700: ({
                    "id": 700, "name": "v0916-parity-comparison-123",
                    "expired": False, "digest": comparison_digest,
                    "created_at": "2026-08-26T00:01:30Z",
                    "workflow_run": copy.deepcopy(artifact_owner),
                }, comparison_archive),
                701: ({
                    "id": 701, "name": "v0916-parity-attestation-123",
                    "expired": False, "digest": attestation_digest,
                    "created_at": "2026-08-26T00:04:30Z",
                    "workflow_run": copy.deepcopy(artifact_owner),
                }, attestation_archive),
                702: ({
                    "id": 702, "name": "v0916-parity-verification-123",
                    "expired": False, "digest": verification_digest,
                    "created_at": "2026-08-26T00:04:31Z",
                    "workflow_run": copy.deepcopy(artifact_owner),
                }, verification_archive),
            },
        )
        finalize = {
            "schemaVersion": "1.0", "comparison": self.artifact(comparison_path),
            "firmwareOwnerAttestation": self.artifact(attestation_path),
            "protectedRun": protected_run,
            "approvalAuthority": {
                "kind": "external-firmware-owner-verification",
                "verifierId": "firmware-owner-verifier-v1",
                "verificationRecord": self.artifact(verification_path),
            },
        }
        finalize_path = root / "finalize.json"
        finalize_path.write_text(json.dumps(finalize), encoding="utf-8")
        return {
            "finalizePath": finalize_path,
            "githubReader": github_reader,
            "protectedRun": protected_run,
            "expectedVerification": {
                "attestationId": "FW-OWNER-1.0.0-001", "firmwareOwnerId": "fw-owner",
                "attestationSha256": hashlib.sha256(attestation_path.read_bytes()).hexdigest(),
                "verificationRecordSha256": hashlib.sha256(verification_path.read_bytes()).hexdigest(),
                "comparisonSha256": hashlib.sha256(comparison_path.read_bytes()).hexdigest(),
                "comparisonArtifactId": 700,
                "comparisonArtifactDigest": comparison_digest,
                "planSha256": comparison["planSha256"],
                "policySha256": comparison["policySha256"],
                "implementationHead": comparison["candidateAuthority"]["implementationHead"],
                "implementationTree": comparison["candidateAuthority"]["implementationTree"],
                "candidatePackageSha256": comparison["candidatePackage"]["sha256"],
                "candidateManifestSha256": comparison["candidateBuild"]["candidateManifest"]["sha256"],
                "candidateArtifactDigest": comparison["candidateBuild"]["artifactDigest"],
                "routeEvidenceSha256": comparison["routeEvidenceSha256"],
                "receiptSetSha256": comparison["receiptSetSha256"],
                "verifiedAtUtc": "2026-08-26T00:04:00Z",
                "authorizedOperators": ["dennis40816"],
            },
        }

    @staticmethod
    def _single_file_archive(name: str, payload: bytes) -> bytes:
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, "w", zipfile.ZIP_STORED) as archive:
            archive.writestr(name, payload)
        return stream.getvalue()

    def _schema_complete_comparison_fixture(self) -> dict[str, object]:
        """Payload-free schema fixture only; it is not firmware parity evidence."""

        def digest(label: str) -> str:
            return hashlib.sha256(label.encode("utf-8")).hexdigest()

        plan = MODULE.load_and_validate_plan(self.plan_path, self.policy_path)
        candidate_executor = plan.raw["candidateAuthority"][
            "sourceExecutorContract"
        ]["sha256"]
        baseline_executor = plan.raw["baseline"]["executorContract"]["sha256"]
        candidate_contract = json.loads(
            (
                ROOT
                / plan.raw["candidateAuthority"]["sourceExecutorContract"]["path"]
            ).read_text(encoding="utf-8")
        )
        baseline_contract = json.loads(
            (ROOT / plan.raw["baseline"]["executorContract"]["path"]).read_text(
                encoding="utf-8"
            )
        )
        exact_routes = [
            route for route in plan.routes if route.proof_kind == "exact-output"
        ]
        exact_rows: list[dict[str, object]] = []
        for index, route in enumerate(exact_routes):
            row = copy.deepcopy(self.schema_exact_evidence_row())
            row["routeId"] = route.route_id
            row["capabilityFingerprint"] = route.capability_fingerprint
            row["scenario"].update(
                icId=route.ic_id,
                workflowId=route.workflow_id,
                icCountVariant=route.ic_count_variant,
                mapVariant=route.map_variant,
                selectionToken="fixture",
            )
            minimum_full_capacity = 1 + max(
                (
                    item.tp_length
                    for item in plan.routes
                    if item.full_route_id == route.route_id
                    and item.tp_length is not None
                ),
                default=0,
            )
            capacity = max(8, minimum_full_capacity)
            row["scenario"]["outputCapacity"] = capacity
            row["scenario"]["orderedInputs"][0]["size"] = capacity
            row["baselineOutput"]["size"] = capacity
            row["candidateOutput"]["size"] = capacity
            for receipt_index, receipt in enumerate(row["receipts"]):
                receipt["receiptSha256"] = digest(
                    f"receipt-exact-{index}-{receipt_index}"
                )
                receipt["invocationSha256"] = digest(
                    f"invocation-exact-{index}-{receipt_index}"
                )
                receipt["report"]["sha256"] = digest(
                    f"report-exact-{index}-{receipt_index}"
                )
            row["receipts"][0]["executorIdentitySha256"] = baseline_executor
            row["receipts"][1]["executorIdentitySha256"] = candidate_executor
            exact_rows.append(row)

        correction = json.loads(self.plan_path.read_text(encoding="utf-8"))[
            "approvedSemanticCorrections"
        ][0]
        correction_row = next(
            row for row in exact_rows if row["routeId"] == correction["routeId"]
        )
        correction_row["routeId"] = correction["routeId"]
        correction_row["capabilityFingerprint"] = correction[
            "capabilityFingerprint"
        ]
        correction_row["proofKind"] = correction["requiredProofKind"]
        correction_row["baselineOutput"] = copy.deepcopy(
            correction["baselineOutput"]
        )
        correction_row["candidateOutput"] = copy.deepcopy(
            correction["candidateOutput"]
        )
        correction_row["scenario"]["outputCapacity"] = correction["candidateOutput"][
            "size"
        ]
        correction_row["differenceValidation"] = {
            "kind": correction["kind"],
            "ownerDecision": correction["ownerDecision"],
            "differentByteCount": correction["differentByteCount"],
            "differentRanges": copy.deepcopy(correction["differentRanges"]),
        }
        correction_row["equal"] = False

        transitive_rows: list[dict[str, object]] = []
        transitive_routes = [
            route
            for route in plan.routes
            if route.proof_kind == "tp-prefix-transitive"
        ]
        exact_by_id = {row["routeId"]: row for row in exact_rows}
        for index, route in enumerate(transitive_routes):
            full = exact_by_id[route.full_route_id]
            row = copy.deepcopy(
                self.schema_transitive_evidence_row(
                    MODULE.canonical_route_row_sha256(full)
                )
            )
            row["routeId"] = route.route_id
            row["capabilityFingerprint"] = route.capability_fingerprint
            row["fullEvidence"]["routeId"] = full["routeId"]
            row["fullEvidence"]["capabilityFingerprint"] = full[
                "capabilityFingerprint"
            ]
            row["tpLength"] = route.tp_length
            row["tpScenario"].update(
                icId=route.ic_id,
                workflowId=route.workflow_id,
                icCountVariant=route.ic_count_variant,
                mapVariant=route.map_variant,
                selectionToken="fixture-tp",
                outputCapacity=route.tp_length,
            )
            row["candidateTpOutput"]["size"] = route.tp_length
            row["candidateFullInput"] = {
                key: full["scenario"]["orderedInputs"][0][key]
                for key in ("size", "sha256")
            }
            row["receipts"][0]["executorIdentitySha256"] = candidate_executor
            row["receipts"][0]["receiptSha256"] = digest(
                f"receipt-transitive-{index}"
            )
            row["receipts"][0]["invocationSha256"] = digest(
                f"invocation-transitive-{index}"
            )
            row["receipts"][0]["report"]["sha256"] = digest(
                f"report-transitive-{index}"
            )
            transitive_rows.append(row)

        routes = [*exact_rows, *transitive_rows]
        receipts = [
            {
                "routeId": route["routeId"],
                "role": receipt["role"],
                "receiptSha256": receipt["receiptSha256"],
            }
            for route in routes
            for receipt in route["receipts"]
        ]
        comparison = {
            "schemaVersion": "1.0",
            "planSha256": plan.identity_sha256,
            "policySha256": plan.raw["policyBinding"]["sha256"],
            "comparator": {
                "contractVersion": "1.0",
                "scriptSha256": hashlib.sha256(
                    (ROOT / "scripts/v0916_parity_certification.py").read_bytes()
                ).hexdigest(),
            },
            "candidateAuthority": {
                "implementationHead": candidate_contract["source"]["implementationHead"],
                "implementationTree": candidate_contract["source"]["implementationTree"],
                "authorityTrees": candidate_contract["source"]["authorityTrees"],
                "policySha256": plan.raw["policyBinding"]["sha256"],
                "sourceExecutorContract": {
                    "size": plan.raw["candidateAuthority"]["sourceExecutorContract"]["size"],
                    "sha256": candidate_executor,
                },
                "authorityTransfer": plan.raw["candidateAuthority"]["authorityTransfer"],
            },
            "baselineExecutor": {
                "kind": "exact-tag-source-built-cli",
                "tagObject": "578b2614632d6c2affdf2000324b134b5d1a16c1",
                "peeledCommit": "462590e8b993b8e42d088bc07377571a4bb9f25d",
                "sourceTree": "dc46c9aa9ecf00cb898ba3bc287e1b15acdab735",
                "resolvedSdkVersion": "10.0.303",
                "contract": {
                    "size": 3730,
                    "sha256": baseline_executor,
                },
                "cliAssembly": {
                    "size": baseline_contract["cliAssembly"]["size"],
                    "sha256": baseline_contract["cliAssembly"]["sha256"],
                },
            },
            "baselineReleaseReference": {
                "name": "NvtFwCombiner-v0.9.16-win-x64.zip",
                "size": 75556385,
                "sha256": "e55687f9d98ca3a2b02eac5789f4443697a249dcc60b261e3e6cfeae7dc03c84",
                "purpose": "release-provenance-reference-only",
            },
            "candidatePackage": {
                "name": "NvtFwCombiner-v1.0.0-win-x64.zip",
                "size": 100,
                "sha256": "5" * 64,
                "version": "1.0.0",
                "sourceCommit": "1d1d1cfcad7f0963dd3ed1e3e920d9a3425d6220",
            },
            "candidateBuild": {
                "repository": "Dennis40816/nvt_fw_combiner",
                "workflowPath": ".github/workflows/release.yml",
                "workflowRef": "refs/heads/main",
                "workflowCommitSha": "1d1d1cfcad7f0963dd3ed1e3e920d9a3425d6220",
                "workflowBlobSha": "e" * 40,
                "workflowRawSha256": "f" * 64,
                "workflowSemanticContractSha256": "9f0272d7299de222a662d39342b86efadd4f1625651c2c3785b71f77010bcb47",
                "runId": 123,
                "artifactId": 456,
                "artifactName": "stable-candidate-123-1d1d1cfcad7f0963dd3ed1e3e920d9a3425d6220",
                "artifactDigest": "sha256:" + "a" * 64,
                "artifactWorkflowRun": {
                    "id": 123,
                    "headSha": "1d1d1cfcad7f0963dd3ed1e3e920d9a3425d6220",
                    "headBranch": "main",
                    "repository": "Dennis40816/nvt_fw_combiner",
                    "repositoryId": 40816,
                    "headRepositoryId": 40816,
                },
                "candidateManifest": {"size": 10, "sha256": "9" * 64},
                "candidateSourceExecutorIdentitySha256": candidate_executor,
                "provenanceSubjectsSha256": "8" * 64,
                "candidateVerifierSha256": "7" * 64,
                "packageVerifierSha256": "6" * 64,
            },
            "routeEvidenceSha256": MODULE.canonical_route_evidence_sha256(routes),
            "receiptSetSha256": MODULE.canonical_receipt_set_sha256(receipts),
            "executedAtUtc": "2026-08-26T00:01:00Z",
            "routes": routes,
            "verdict": "provisional",
        }
        MODULE.validate_comparison_schema(comparison, plan=plan)
        return comparison

    def test_comparison_rejects_forged_rows_even_after_aggregate_rehash(self) -> None:
        for mutation in (
            "transitive-zero-length",
            "transitive-size-mismatch",
            "transitive-false-proof",
            "transitive-invalid-sha",
            "exact-invalid-compilation",
            "exact-invalid-receipt-report",
            "exact-invalid-output-sha",
            "exact-invalid-scenario-input",
            "exact-equal-but-different-output",
            "exact-output-capacity-mismatch",
            "forged-baseline-executor",
            "forged-candidate-executor",
            "transitive-full-input-unbound",
            "transitive-scenario-drift",
            "forged-route-inventory",
            "top-level-shape-forgery",
            "duplicate-input-slot",
            "transitive-references-correction",
            "unauthorized-operator",
        ):
            comparison = self._schema_complete_comparison_fixture()
            transitive = next(
                row
                for row in comparison["routes"]
                if row["proofKind"] == "tp-prefix-transitive"
            )
            exact = next(
                row
                for row in comparison["routes"]
                if row["proofKind"] == "exact-output"
            )
            if mutation == "transitive-zero-length":
                transitive["tpLength"] = 0
            elif mutation == "transitive-size-mismatch":
                transitive["candidateTpOutput"]["size"] += 1
            elif mutation == "transitive-false-proof":
                transitive["candidateFullTailImmutable"] = False
            elif mutation == "transitive-invalid-sha":
                transitive["candidateFullInput"]["sha256"] = "invalid"
            elif mutation == "exact-invalid-compilation":
                exact["compilationFingerprints"]["candidate"] = "invalid"
            elif mutation == "exact-invalid-receipt-report":
                exact["receipts"][0]["report"]["sha256"] = "invalid"
            elif mutation == "exact-invalid-output-sha":
                exact["candidateOutput"]["sha256"] = "invalid"
            elif mutation == "exact-invalid-scenario-input":
                exact["scenario"]["orderedInputs"][0]["size"] = 0
            elif mutation == "exact-equal-but-different-output":
                exact["candidateOutput"]["sha256"] = "a" * 64
                if exact["candidateOutput"] == exact["baselineOutput"]:
                    exact["candidateOutput"]["sha256"] = "b" * 64
            elif mutation == "exact-output-capacity-mismatch":
                exact["candidateOutput"]["size"] += 1
            elif mutation == "forged-baseline-executor":
                exact["receipts"][0]["executorIdentitySha256"] = "a" * 64
            elif mutation == "forged-candidate-executor":
                exact["receipts"][1]["executorIdentitySha256"] = "a" * 64
            elif mutation == "transitive-full-input-unbound":
                transitive["candidateFullInput"]["sha256"] = "a" * 64
            elif mutation == "transitive-scenario-drift":
                transitive["tpScenario"]["icId"] = "NT00000"
            elif mutation == "forged-route-inventory":
                exact["routeId"] = "route-forged-but-well-shaped"
            elif mutation == "top-level-shape-forgery":
                comparison["candidateAuthority"] = []
            elif mutation == "duplicate-input-slot":
                exact["scenario"]["orderedInputs"][1]["slotId"] = exact[
                    "scenario"
                ]["orderedInputs"][0]["slotId"]
            elif mutation == "transitive-references-correction":
                correction = next(
                    row
                    for row in comparison["routes"]
                    if row["proofKind"]
                    == "exact-output-with-approved-semantic-correction"
                )
                transitive["fullEvidence"] = {
                    "routeId": correction["routeId"],
                    "capabilityFingerprint": correction["capabilityFingerprint"],
                    "evidenceSha256": MODULE.canonical_route_row_sha256(correction),
                }
                transitive["candidateFullInput"] = {
                    key: correction["scenario"]["orderedInputs"][0][key]
                    for key in ("size", "sha256")
                }
            else:
                for route in comparison["routes"]:
                    for receipt in route["receipts"]:
                        receipt["operatorLogin"] = "unauthorized-user"
            comparison["routeEvidenceSha256"] = (
                MODULE.canonical_route_evidence_sha256(comparison["routes"])
            )
            receipts = [
                {
                    "routeId": route["routeId"],
                    "role": receipt["role"],
                    "receiptSha256": receipt["receiptSha256"],
                }
                for route in comparison["routes"]
                for receipt in route["receipts"]
            ]
            comparison["receiptSetSha256"] = MODULE.canonical_receipt_set_sha256(
                receipts
            )
            with self.subTest(mutation=mutation):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.validate_comparison_schema(
                        comparison, authorized_operators={"dennis40816"}
                    )
                self.assertEqual(
                    "PARITY_EVIDENCE_INCOMPLETE", captured.exception.code
                )


if __name__ == "__main__":
    unittest.main()

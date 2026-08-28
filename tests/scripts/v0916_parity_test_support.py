"""Behavioral tests for the exact-tag v0.9.16 source-baseline parity gate."""

from __future__ import annotations

import copy
import hashlib
import importlib.util
import io
import json
import subprocess
import unittest
from pathlib import Path

import yaml


ROOT = Path(__file__).resolve().parents[2]
MODULE_PATH = ROOT / "scripts" / "v0916_parity_certification.py"
SPEC = importlib.util.spec_from_file_location("v0916_parity_certification", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None


def parity_workflow_fixture_from_contract(
    contract: dict[str, object],
) -> dict[str, object]:
    """Materialize the one YAML-native graph declared by the workflow contract."""
    production_workflow = yaml.safe_load(
        (ROOT / ".github/workflows/release.yml").read_text(encoding="utf-8")
    )
    jobs: dict[str, object] = {
        job_id: copy.deepcopy(production_workflow["jobs"][job_id])
        for job_id in ("candidate", "promote", "published-smoke")
    }
    for job_id, declared in contract["jobs"].items():
        job = {
            "name": declared["name"],
            "needs": copy.deepcopy(declared["needs"]),
            "if": declared["if"],
            "runs-on": declared["runsOn"],
            "timeout-minutes": declared["timeoutMinutes"],
            "permissions": copy.deepcopy(declared["permissions"]),
            "steps": copy.deepcopy(declared["steps"]),
        }
        if declared["environment"] is not None:
            job["environment"] = declared["environment"]
        jobs[job_id] = job
    fixture = copy.deepcopy(production_workflow)
    fixture["jobs"] = jobs
    return fixture


class MissingProductionParityError(RuntimeError):
    """Red-test marker used only while the R3 production module is absent."""


class MissingProductionModule:
    ParityError = MissingProductionParityError

    def __getattr__(self, name: str):
        def missing(*args, **kwargs):
            raise MissingProductionParityError(
                f"production parity module is not admitted; missing {name}"
            )

        return missing


if MODULE_PATH.is_file():
    MODULE = importlib.util.module_from_spec(SPEC)
    SPEC.loader.exec_module(MODULE)
    PRODUCTION_AVAILABLE = True
else:
    MODULE = MissingProductionModule()
    PRODUCTION_AVAILABLE = False


def runtime_closure_facts(root: Path) -> tuple[str, int, int]:
    files = [
        (path.relative_to(root).as_posix(), path.read_bytes())
        for path in sorted(root.rglob("*"), key=lambda item: item.as_posix())
        if path.is_file()
    ]
    inventory = [
        {
            "path": relative,
            "size": len(payload),
            "sha256": hashlib.sha256(payload).hexdigest(),
        }
        for relative, payload in files
    ]
    return (
        MODULE.canonical_json_sha256(inventory),
        len(files),
        sum(len(payload) for _, payload in files),
    )
class RecordingProcessRunner:
    def __init__(self, results: list[subprocess.CompletedProcess[str]]) -> None:
        self.results = list(results)
        self.calls: list[tuple[list[str], Path]] = []

    def __call__(self, argv: list[str], cwd: Path) -> subprocess.CompletedProcess[str]:
        self.calls.append((argv, cwd))
        return self.results.pop(0)


class BoundedReadStream(io.BytesIO):
    def read(self, size: int = -1) -> bytes:
        if size < 0 or size > 65536:
            raise AssertionError("artifact readers must stream bounded chunks")
        return super().read(size)


class RecordingGithubReader:
    def __init__(
        self,
        workflow_content: dict[str, object],
        run: dict[str, object],
        artifact: dict[str, object],
        artifact_archive: bytes,
    ) -> None:
        self.workflow_content = copy.deepcopy(workflow_content)
        self.run = copy.deepcopy(run)
        self.artifact = copy.deepcopy(artifact)
        self.artifact_archive = artifact_archive
        self.calls: list[tuple[str, int | str]] = []

    def get_workflow_content(
        self, repository: str, path: str, ref: str
    ) -> dict[str, object]:
        self.calls.append((f"{repository}/contents/{path}", ref))
        return copy.deepcopy(self.workflow_content)

    def get_workflow_run(self, repository: str, run_id: int) -> dict[str, object]:
        self.calls.append((f"{repository}/run", run_id))
        return copy.deepcopy(self.run)

    def get_artifact(self, repository: str, artifact_id: int) -> dict[str, object]:
        self.calls.append((f"{repository}/artifact", artifact_id))
        return copy.deepcopy(self.artifact)

    def list_run_artifacts(
        self, repository: str, run_id: int
    ) -> list[dict[str, object]]:
        self.calls.append((f"{repository}/artifacts", run_id))
        return [copy.deepcopy(self.artifact)]

    def download_artifact(self, repository: str, artifact_id: int) -> io.BytesIO:
        self.calls.append((f"{repository}/artifact-download", artifact_id))
        return BoundedReadStream(self.artifact_archive)


class UnavailableGithubReader:
    def get_workflow_content(
        self, repository: str, path: str, ref: str
    ) -> dict[str, object]:
        raise OSError("GitHub unavailable")

    def get_workflow_run(self, repository: str, run_id: int) -> dict[str, object]:
        raise OSError("GitHub unavailable")

    def get_artifact(self, repository: str, artifact_id: int) -> dict[str, object]:
        raise OSError("GitHub unavailable")

    def download_artifact(self, repository: str, artifact_id: int) -> io.BytesIO:
        raise OSError("GitHub unavailable")


class RecordingBaselineExecutorHost:
    def __init__(
        self,
        *,
        head: str,
        tree: str,
        authority_trees: dict[str, str] | None = None,
        tag_object: str | None = None,
        sdk_version: str,
        dirty_paths: list[str] | None = None,
        ignored_build_paths: list[str] | None = None,
        process_results: list[subprocess.CompletedProcess[str]] | None = None,
    ) -> None:
        self.head = head
        self.tree = tree
        self.authority_trees = dict(authority_trees or {})
        self.tag_object = tag_object
        self.sdk_version = sdk_version
        self.dirty_paths = list(dirty_paths or [])
        self.ignored_build_paths = list(ignored_build_paths or [])
        self.process_results = list(
            process_results
            or [
                subprocess.CompletedProcess([], 0, "restore ok", ""),
                subprocess.CompletedProcess([], 0, "build ok", ""),
            ]
        )
        self.calls: list[tuple[str, object]] = []

    def git_head(self, root: Path) -> str:
        self.calls.append(("git-head", root))
        return self.head

    def git_tag_object(self, root: Path, tag: str) -> str:
        self.calls.append(("git-tag-object", (root, tag)))
        if self.tag_object is None:
            raise AssertionError("fixture must declare the annotated tag object")
        return self.tag_object

    def git_tree(self, root: Path) -> str:
        self.calls.append(("git-tree", root))
        return self.tree

    def git_tree_for_path(self, root: Path, relative_path: str) -> str:
        self.calls.append(("git-tree-path", (root, relative_path)))
        return self.authority_trees[relative_path]

    def git_dirty_paths(self, root: Path) -> list[str]:
        self.calls.append(("git-status", root))
        return list(self.dirty_paths)

    def git_ignored_build_paths(self, root: Path) -> list[str]:
        self.calls.append(("git-ignored-build-paths", root))
        return list(self.ignored_build_paths)

    def dotnet_sdk_version(self, root: Path) -> str:
        self.calls.append(("dotnet-version", root))
        return self.sdk_version

    def run(self, argv: list[str], cwd: Path) -> subprocess.CompletedProcess[str]:
        self.calls.append(("process", (argv, cwd)))
        return self.process_results.pop(0)


class RecordingProtectedApprovalReader:
    def __init__(
        self,
        workflow_content: dict[str, object],
        run: dict[str, object],
        job: dict[str, object],
        deployment: dict[str, object],
        deployment_statuses: list[dict[str, object]],
        artifacts: dict[int, tuple[dict[str, object], bytes]],
    ) -> None:
        self.workflow_content = copy.deepcopy(workflow_content)
        self.run = copy.deepcopy(run)
        self.job = copy.deepcopy(job)
        self.deployment = copy.deepcopy(deployment)
        self.deployment_statuses = copy.deepcopy(deployment_statuses)
        self.artifacts = copy.deepcopy(artifacts)
        self.calls: list[tuple[str, int | str]] = []

    def get_workflow_content(
        self, repository: str, path: str, ref: str
    ) -> dict[str, object]:
        self.calls.append((f"{repository}/contents/{path}", ref))
        return copy.deepcopy(self.workflow_content)

    def get_workflow_run(self, repository: str, run_id: int) -> dict[str, object]:
        self.calls.append((f"{repository}/run", run_id))
        return copy.deepcopy(self.run)

    def get_workflow_job(self, repository: str, job_id: int) -> dict[str, object]:
        self.calls.append((f"{repository}/job", job_id))
        return copy.deepcopy(self.job)

    def get_deployment(
        self,
        repository: str,
        deployment_id: int,
    ) -> dict[str, object]:
        self.calls.append((f"{repository}/deployment", deployment_id))
        return copy.deepcopy(self.deployment)

    def get_deployment_statuses(
        self, repository: str, deployment_id: int
    ) -> list[dict[str, object]]:
        self.calls.append((f"{repository}/deployment-statuses", deployment_id))
        return copy.deepcopy(self.deployment_statuses)

    def get_artifact(self, repository: str, artifact_id: int) -> dict[str, object]:
        self.calls.append((f"{repository}/artifact", artifact_id))
        return copy.deepcopy(self.artifacts[artifact_id][0])

    def download_artifact(self, repository: str, artifact_id: int) -> io.BytesIO:
        self.calls.append((f"{repository}/artifact-download", artifact_id))
        return BoundedReadStream(self.artifacts[artifact_id][1])


class UnavailableProtectedApprovalReader:
    def get_workflow_content(
        self, repository: str, path: str, ref: str
    ) -> dict[str, object]:
        raise OSError("GitHub protected environment unavailable")

    def get_workflow_run(self, repository: str, run_id: int) -> dict[str, object]:
        raise OSError("GitHub protected environment unavailable")

    def get_workflow_job(self, repository: str, job_id: int) -> dict[str, object]:
        raise OSError("GitHub protected environment unavailable")

    def get_deployment(self, repository: str, deployment_id: int) -> dict[str, object]:
        raise OSError("GitHub protected environment unavailable")

    def get_deployment_statuses(
        self, repository: str, deployment_id: int
    ) -> list[dict[str, object]]:
        raise OSError("GitHub protected environment unavailable")

    def get_artifact(self, repository: str, artifact_id: int) -> dict[str, object]:
        raise OSError("GitHub protected environment unavailable")

    def download_artifact(self, repository: str, artifact_id: int) -> io.BytesIO:
        raise OSError("GitHub protected environment unavailable")


class V0916ParityTestBase(unittest.TestCase):
    def setUp(self) -> None:
        self.plan_path = (
            ROOT / "docs" / "contracts" / "v0916-parity-certification-v1.json"
        )
        self.policy_path = (
            ROOT / "docs" / "contracts" / "canonical-capability-policy-v1.json"
        )

    @staticmethod
    def artifact(path: Path) -> dict[str, object]:
        return {
            "path": str(path),
            "size": path.stat().st_size,
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        }

    @staticmethod
    def serialized_application_report(
        *,
        route_id: str,
        ic_id: str,
        workflow_id: str,
        compilation_fingerprint: str,
        output: Path,
        inputs: list[dict[str, object]],
        processor: bool,
        started_at_utc: str = "2026-08-26T00:00:00Z",
        completed_at_utc: str = "2026-08-26T00:00:01Z",
    ) -> dict[str, object]:
        """Payload-free shape emitted by v0.9.16/current CompositionRunReportJson.

        The fixture deliberately retains the serializer's Pascal-case property
        names and full OperationRunSummary/MutationRunSummary shape. It is not a
        pre-normalized comparator projection.
        """
        output_size = output.stat().st_size
        output_sha = hashlib.sha256(output.read_bytes()).hexdigest()
        raw_started_at = (
            started_at_utc[:-1] + "+00:00"
            if started_at_utc.endswith("Z")
            else started_at_utc
        )
        raw_completed_at = (
            completed_at_utc[:-1] + "+00:00"
            if completed_at_utc.endswith("Z")
            else completed_at_utc
        )
        composition_kind = "Replace" if workflow_id == "ctrlram-replace" else "Merge"
        package_root = output.parent / "fixture-package"
        staging_root = output.parent / "fixture-staging"
        operation = {
            "OperationId": "op-1",
            "Sequence": 0,
            "Kind": "RunExternalProcessor" if processor else "CopyRange",
            "Status": "Succeeded",
            "SourceSpaceId": "source",
            "SourceRange": {"Start": 0, "Length": output_size, "EndExclusive": output_size},
            "TargetSpaceId": "output-image",
            "TargetRange": {"Start": 0, "Length": output_size, "EndExclusive": output_size},
            "OverlapPolicy": "Reject",
            "ProcessorId": "nfc.test" if processor else None,
            "ToolBindingId": "test-tool" if processor else None,
            "ProcessorAllowedReadRanges": (
                [{"Start": 0, "Length": output_size, "EndExclusive": output_size}]
                if processor
                else []
            ),
            "ProcessorAllowedWriteRanges": (
                [{"Start": 0, "Length": output_size, "EndExclusive": output_size}]
                if processor
                else []
            ),
            "ExecutedCommands": (
                [
                    {
                        "ExecutablePath": str(
                            package_root / "external-tools" / "nfc-test.exe"
                        ),
                        "WorkingDirectory": str(staging_root),
                        "Arguments": [
                            "--input",
                            str(staging_root / "input.bin"),
                            "--output",
                            str(staging_root / "output.bin"),
                            "--mode",
                            "test",
                        ],
                    }
                ]
                if processor
                else []
            ),
            "Reason": "payload-free parity plumbing fixture",
            "Provenance": {"Kind": "built-in-profile", "SourceId": None, "SourceVersion": None},
        }
        mutation = {
            "OperationId": "op-1",
            "Kind": operation["Kind"],
            "TargetSpaceId": "output-image",
            "TargetRange": {"Start": 0, "Length": output_size, "EndExclusive": output_size},
            "ChangedByteCount": output_size,
            "BeforeSha256": "0" * 64,
            "AfterSha256": output_sha,
            "Reason": "payload-free parity plumbing fixture",
        }
        return {
            "RunId": f"parity-{route_id}",
            "ProfileId": f"profile-{route_id}",
            "ProfileVersion": "1.0.0",
            "IcId": ic_id,
            "ModeId": workflow_id,
            "ExperienceId": workflow_id,
            "CompositionKind": composition_kind,
            "StartedAtUtc": raw_started_at,
            "CompletedAtUtc": raw_completed_at,
            "Inputs": [
                {
                    "AddressSpaceId": item["slotId"],
                    "ArtifactId": item["slotId"],
                    "Size": item["size"],
                    "Sha256": item["sha256"],
                    "OriginalFileName": None,
                }
                for item in inputs
            ],
            "Operations": [operation],
            "Mutations": [mutation],
            "Issues": [],
            "Output": {
                "FileName": output.name,
                "Size": output_size,
                "Sha256": output_sha,
                "Committed": True,
            },
            "OutputDifferences": [],
            "CompilationFingerprint": compilation_fingerprint,
            "Validations": [],
            "OutputNaming": None,
        }

    @staticmethod
    def normalized_operation_from_raw(
        raw: dict[str, object],
        *,
        package_root: Path,
        staging_root: Path,
    ) -> dict[str, object]:
        target = raw["TargetRange"]
        operation = {
            "operationId": raw["OperationId"],
            "sequence": raw["Sequence"],
            "kind": raw["Kind"],
            "status": str(raw["Status"]).lower(),
            "sourceSpaceId": raw["SourceSpaceId"],
            "sourceRange": (
                None
                if raw["SourceRange"] is None
                else {
                    "addressSpace": raw["SourceSpaceId"],
                    "start": raw["SourceRange"]["Start"],
                    "endExclusive": raw["SourceRange"]["EndExclusive"],
                }
            ),
            "targetSpaceId": raw["TargetSpaceId"],
            "targetRange": {
                "addressSpace": raw["TargetSpaceId"],
                "start": target["Start"],
                "endExclusive": target["EndExclusive"],
            },
            "overlapPolicy": raw["OverlapPolicy"],
            "processor": None,
            "executedCommands": [],
            "reason": raw["Reason"],
            "provenance": {
                "kind": raw["Provenance"]["Kind"],
                "sourceId": raw["Provenance"]["SourceId"],
                "sourceVersion": raw["Provenance"]["SourceVersion"],
            },
        }
        if raw["ProcessorId"] is not None:
            operation["processor"] = {
                "processorId": raw["ProcessorId"],
                "toolBindingId": raw["ToolBindingId"],
                "allowedReadRanges": [
                    {
                        "addressSpace": raw["TargetSpaceId"],
                        "start": item["Start"],
                        "endExclusive": item["EndExclusive"],
                    }
                    for item in raw["ProcessorAllowedReadRanges"]
                ],
                "allowedWriteRanges": [
                    {
                        "addressSpace": raw["TargetSpaceId"],
                        "start": item["Start"],
                        "endExclusive": item["EndExclusive"],
                    }
                    for item in raw["ProcessorAllowedWriteRanges"]
                ],
            }
            for sequence, command in enumerate(raw["ExecutedCommands"]):
                executable = Path(command["ExecutablePath"])
                executable_relative = executable.relative_to(package_root).as_posix()
                if Path(command["WorkingDirectory"]) != staging_root:
                    raise AssertionError("fixture command must use the declared staging root")
                tokenized_arguments = []
                for argument in command["Arguments"]:
                    normalized = str(argument).replace("\\", "/")
                    normalized = normalized.replace(
                        str(package_root).replace("\\", "/"), "{package}"
                    )
                    normalized = normalized.replace(
                        str(staging_root).replace("\\", "/"), "{staging}"
                    )
                    tokenized_arguments.append(normalized)
                argument_bytes = json.dumps(
                    tokenized_arguments,
                    ensure_ascii=False,
                    separators=(",", ":"),
                ).encode("utf-8")
                operation["executedCommands"].append(
                    {
                        "sequence": sequence,
                        "executablePackagePath": executable_relative,
                        "workingDirectoryKind": "host-created-staging",
                        "argumentCount": len(tokenized_arguments),
                        "canonicalArgumentsSha256": hashlib.sha256(
                            argument_bytes
                        ).hexdigest(),
                    }
                )
        return operation

    @staticmethod
    def normalized_mutation_from_raw(raw: dict[str, object]) -> dict[str, object]:
        target = raw["TargetRange"]
        return {
            "operationId": raw["OperationId"],
            "kind": raw["Kind"],
            "targetSpaceId": raw["TargetSpaceId"],
            "targetRange": {
                "addressSpace": raw["TargetSpaceId"],
                "start": target["Start"],
                "endExclusive": target["EndExclusive"],
            },
            "changedByteCount": raw["ChangedByteCount"],
            "beforeSha256": raw["BeforeSha256"],
            "afterSha256": raw["AfterSha256"],
            "reason": raw["Reason"],
        }

    @staticmethod
    def normalized_application_context_from_raw(
        raw: dict[str, object],
    ) -> dict[str, object]:
        def canonical_utc(value: str) -> str:
            if value.endswith("+00:00"):
                return value[:-6] + "Z"
            return value

        return {
            "icId": raw["IcId"],
            "modeId": raw["ModeId"],
            "experienceId": raw["ExperienceId"],
            "mapId": raw.get("MapId"),
            "compositionKind": raw["CompositionKind"],
            "startedAtUtc": canonical_utc(raw["StartedAtUtc"]),
            "completedAtUtc": canonical_utc(raw["CompletedAtUtc"]),
            "orderedInputs": [
                {
                    "addressSpaceId": item["AddressSpaceId"],
                    "artifactId": item["ArtifactId"],
                    "size": item["Size"],
                    "sha256": item["Sha256"],
                }
                for item in raw["Inputs"]
            ],
            "outputCommitted": raw["Output"]["Committed"],
            "issueCount": len(raw["Issues"]),
        }

    @staticmethod
    def schema_exact_evidence_row() -> dict[str, object]:
        return {
            "routeId": "route-full",
            "capabilityFingerprint": "a" * 64,
            "proofKind": "exact-output",
            "scenario": {
                "icId": "NT51927",
                "workflowId": "ctrlram-replace",
                "icCountVariant": "3-ic",
                "mapVariant": "fw141",
                "selectionToken": "full-base",
                "outputCapacity": 8,
                "orderedInputs": [
                    {
                        "slotId": "base",
                        "role": "base",
                        "size": 8,
                        "sha256": "1" * 64,
                    },
                    {
                        "slotId": "ctrlram",
                        "role": "replacement",
                        "size": 4,
                        "sha256": "2" * 64,
                    },
                ],
            },
            "compilationFingerprints": {
                "baseline": "0" * 64,
                "candidate": "a" * 64,
            },
            "reportValidation": {
                "kind": "independent-executor-typed-authority",
                "baseline": {
                    "rawReportSha256": "c" * 64,
                    "projectionSha256": "d" * 64,
                    "compiledAuthoritySha256": "d" * 64,
                    "passed": True,
                },
                "candidate": {
                    "rawReportSha256": "e" * 64,
                    "projectionSha256": "f" * 64,
                    "compiledAuthoritySha256": "f" * 64,
                    "passed": True,
                },
                "crossVersionOperationComparison": "not-applied-executor-specific",
                "passed": True,
            },
            "receipts": [
                {
                    "role": "baseline-exact",
                    "operatorLogin": "dennis40816",
                    "executorIdentitySha256": "92e400212b5cdbb5e164b4d1401d59cdd1adbb0aef9a490be4777554d5b1e659",
                    "receiptSha256": "3" * 64,
                    "invocationSha256": "4" * 64,
                    "report": {"size": 10, "sha256": "5" * 64},
                },
                {
                    "role": "candidate-exact",
                    "operatorLogin": "dennis40816",
                    "executorIdentitySha256": "1" * 64,
                    "receiptSha256": "6" * 64,
                    "invocationSha256": "7" * 64,
                    "report": {"size": 10, "sha256": "8" * 64},
                },
            ],
            "baselineOutput": {"size": 8, "sha256": "9" * 64},
            "candidateOutput": {"size": 8, "sha256": "9" * 64},
            "equal": True,
            "passed": True,
        }

    @staticmethod
    def schema_transitive_evidence_row(exact_digest: str) -> dict[str, object]:
        return {
            "routeId": "route-tp",
            "capabilityFingerprint": "b" * 64,
            "proofKind": "tp-prefix-transitive",
            "fullEvidence": {
                "routeId": "route-full",
                "capabilityFingerprint": "a" * 64,
                "evidenceSha256": exact_digest,
            },
            "tpLength": 4,
            "tpScenario": {
                "icId": "NT51927",
                "workflowId": "ctrlram-replace",
                "icCountVariant": "3-ic",
                "mapVariant": "fw141",
                "selectionToken": "tp-base",
                "outputCapacity": 4,
                "orderedInputs": [
                    {
                        "slotId": "base",
                        "role": "base",
                        "size": 4,
                        "sha256": "a" * 64,
                    },
                    {
                        "slotId": "ctrlram",
                        "role": "replacement",
                        "size": 4,
                        "sha256": "2" * 64,
                    },
                ],
            },
            "candidateCompilationFingerprint": "b" * 64,
            "receipts": [
                {
                    "role": "candidate-tp",
                    "operatorLogin": "dennis40816",
                    "executorIdentitySha256": "1" * 64,
                    "receiptSha256": "b" * 64,
                    "invocationSha256": "c" * 64,
                    "report": {"size": 10, "sha256": "d" * 64},
                }
            ],
            "candidateTpOutput": {"size": 4, "sha256": "e" * 64},
            "candidateFullInput": {"size": 8, "sha256": "f" * 64},
            "candidateTpEqualsCandidateFullPrefix": True,
            "candidateTpEqualsBaselineFullPrefix": True,
            "candidateFullTailImmutable": True,
            "passed": True,
        }

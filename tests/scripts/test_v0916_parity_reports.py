"""Behavioral red tests for one v0.9.16 parity concern."""

import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from tests.scripts.v0916_parity_test_support import (
    MODULE,
    MODULE_PATH,
    V0916ParityTestBase,
)


class V0916ParityReportTests(V0916ParityTestBase):
    def test_operation_and_mutation_order_are_semantic_and_fail_closed(self) -> None:
        operations = [
            {"operationId": "op-0", "sequence": 0},
            {"operationId": "op-1", "sequence": 1},
        ]
        mutations = [
            {"operationId": "op-0", "sequence": 0},
            {"operationId": "op-1", "sequence": 1},
        ]
        MODULE.validate_report_sequence(
            authority_operations=operations,
            observed_operations=copy.deepcopy(operations),
            observed_mutations=copy.deepcopy(mutations),
        )
        for mutation, observed_operations, observed_mutations in (
            ("operation-reorder", list(reversed(operations)), mutations),
            ("mutation-reorder", operations, list(reversed(mutations))),
        ):
            with self.subTest(mutation=mutation):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.validate_report_sequence(
                        authority_operations=operations,
                        observed_operations=observed_operations,
                        observed_mutations=observed_mutations,
                    )
                self.assertEqual("PARITY_PROVENANCE_INVALID", captured.exception.code)

    def test_nt51927_fw141_reports_are_validated_against_each_executor_not_each_other(self) -> None:
        plan = MODULE.load_and_validate_plan(self.plan_path, self.policy_path)
        route_id = (
            "route-7-nt51927-15-ctrlram-replace-4-1-ic-39-"
            "nt51927-ctrlram-fw141-single-full-flash"
        )
        capability = (
            "72c1d750aca975c65567800d2e27621c2793596c59cc3ebebd2de06b6d363e90"
        )
        writes = [
            {
                "addressSpace": "output-image",
                "start": start,
                "endExclusive": end,
            }
            for start, end in (
                (572, 576),
                (588, 592),
                (620, 624),
                (636, 640),
                (92160, 96208),
                (96208, 108496),
                (108496, 117712),
                (117712, 123440),
                (123440, 123840),
                (208320, 209440),
                (212992, 215040),
            )
        ]

        def operation(end_exclusive: int) -> dict[str, object]:
            return {
                "operationId": "postbuild-singlechip",
                "sequence": 2147483647,
                "kind": "RunExternalProcessor",
                "status": "succeeded",
                "sourceSpaceId": None,
                "sourceRange": None,
                "targetSpaceId": "output-image",
                "targetRange": {
                    "addressSpace": "output-image",
                    "start": 0,
                    "endExclusive": end_exclusive,
                },
                "overlapPolicy": "Reject",
                "processor": {
                    "processorId": "nfc.nt51927.ctrlram-postbuild-v1",
                    "toolBindingId": "nfc.nt51927.ctrlram-postbuild-v1",
                    "allowedReadRanges": [
                        {
                            "addressSpace": "output-image",
                            "start": 0,
                            "endExclusive": end_exclusive,
                        }
                    ],
                    "allowedWriteRanges": copy.deepcopy(writes),
                },
                "executedCommands": [
                    {
                        "sequence": index,
                        "executablePackagePath": "external-tools/crc-worker.exe",
                        "workingDirectoryKind": "host-created-staging",
                        "argumentCount": 7,
                        "canonicalArgumentsSha256": hashlib.sha256(
                            f"command:{index}".encode()
                        ).hexdigest(),
                    }
                    for index in range(7)
                ],
                "reason": "Run the declared NT51927 CtrlRAM postbuild plan.",
                "provenance": {
                    "kind": "built-in-profile",
                    "sourceId": None,
                    "sourceVersion": None,
                },
            }

        def mutation(
            end_exclusive: int, before_sha: str, after_sha: str
        ) -> dict[str, object]:
            return {
                "operationId": "postbuild-singlechip",
                "kind": "RunExternalProcessor",
                "targetSpaceId": "output-image",
                "targetRange": {
                    "addressSpace": "output-image",
                    "start": 0,
                    "endExclusive": end_exclusive,
                },
                "changedByteCount": 24,
                "beforeSha256": before_sha,
                "afterSha256": after_sha,
                "reason": "Run the declared NT51927 CtrlRAM postbuild plan.",
            }

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            baseline_output = root / "baseline.bin"
            candidate_output = root / "candidate.bin"
            baseline_output.write_bytes(bytes(262144))
            candidate_output.write_bytes(baseline_output.read_bytes())
            full_sha = hashlib.sha256(baseline_output.read_bytes()).hexdigest()
            prefix_sha = hashlib.sha256(
                candidate_output.read_bytes()[:217088]
            ).hexdigest()
            baseline_operations = [operation(262144)]
            candidate_operations = [operation(217088)]
            baseline_mutations = [mutation(262144, "1" * 64, full_sha)]
            candidate_mutations = [mutation(217088, "2" * 64, prefix_sha)]

            result = MODULE.build_independent_report_validation(
                route_id=route_id,
                capability_fingerprint=capability,
                baseline_projection={"compiledOperations": baseline_operations, "compiledMutations": baseline_mutations},
                baseline_compiled_authority={"compiledOperations": copy.deepcopy(baseline_operations), "compiledMutations": copy.deepcopy(baseline_mutations)},
                candidate_projection={"compiledOperations": candidate_operations, "compiledMutations": candidate_mutations},
                candidate_compiled_authority={"compiledOperations": copy.deepcopy(candidate_operations), "compiledMutations": copy.deepcopy(candidate_mutations)},
                baseline_raw_report_sha256="3" * 64,
                candidate_raw_report_sha256="4" * 64,
            )
            self.assertEqual("independent-executor-typed-authority", result["kind"])
            self.assertNotEqual(
                result["baseline"]["projectionSha256"],
                result["candidate"]["projectionSha256"],
            )
            self.assertEqual("not-applied-executor-specific", result["crossVersionOperationComparison"])

            for mutation_name in (
                "third-baseline-range",
                "third-candidate-range",
                "processor-id",
                "write-range",
                "command",
                "changed-count",
                "prefix-hash",
            ):
                invalid_baseline_operations = copy.deepcopy(baseline_operations)
                invalid_candidate_operations = copy.deepcopy(candidate_operations)
                invalid_baseline_mutations = copy.deepcopy(baseline_mutations)
                invalid_candidate_mutations = copy.deepcopy(candidate_mutations)
                if mutation_name == "third-baseline-range":
                    invalid_baseline_operations[0]["targetRange"][
                        "endExclusive"
                    ] = 217088
                elif mutation_name == "third-candidate-range":
                    invalid_candidate_operations[0]["targetRange"][
                        "endExclusive"
                    ] = 200000
                elif mutation_name == "processor-id":
                    invalid_candidate_operations[0]["processor"][
                        "processorId"
                    ] = "nfc.other"
                elif mutation_name == "write-range":
                    invalid_candidate_operations[0]["processor"][
                        "allowedWriteRanges"
                    ].pop()
                elif mutation_name == "command":
                    invalid_candidate_operations[0]["executedCommands"].pop()
                elif mutation_name == "changed-count":
                    invalid_candidate_mutations[0]["changedByteCount"] = 25
                elif mutation_name == "prefix-hash":
                    invalid_candidate_mutations[0]["afterSha256"] = "f" * 64
                with self.subTest(mutation=mutation_name):
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.build_independent_report_validation(
                            route_id=route_id,
                            capability_fingerprint=capability,
                            baseline_projection={"compiledOperations": invalid_baseline_operations, "compiledMutations": invalid_baseline_mutations},
                            baseline_compiled_authority={"compiledOperations": baseline_operations, "compiledMutations": baseline_mutations},
                            candidate_projection={"compiledOperations": invalid_candidate_operations, "compiledMutations": invalid_candidate_mutations},
                            candidate_compiled_authority={"compiledOperations": candidate_operations, "compiledMutations": candidate_mutations},
                            baseline_raw_report_sha256="3" * 64,
                            candidate_raw_report_sha256="4" * 64,
                        )
                    self.assertEqual("PARITY_PROVENANCE_INVALID", captured.exception.code)

    def test_nt51928_ld_to_ldc_is_a_closed_input_identity_alias_not_operation_normalization(self) -> None:
        plan = MODULE.load_and_validate_plan(self.plan_path, self.policy_path)
        route_id = (
            "route-7-nt51928-14-standard-merge-13-selector-free-31-"
            "nt51928-dual-capacity-256k-512k"
        )
        capability = "3b21585c95eb934e2cd2465489b64ac6ede33c5745c049f3f1d6e114fccc0104"

        def operation(operation_id: str, source_space: str) -> dict[str, object]:
            return {
                "operationId": operation_id,
                "sequence": 300,
                "kind": "CopyRange",
                "status": "succeeded",
                "sourceSpaceId": source_space,
                "sourceRange": {"addressSpace": source_space, "start": 262144, "endExclusive": 401408},
                "targetSpaceId": "output-image",
                "targetRange": {"addressSpace": "output-image", "start": 262144, "endExclusive": 401408},
                "overlapPolicy": "Reject",
                "processor": None,
                "executedCommands": [],
                "reason": "Copy the reviewed NT51928 LDC range.",
                "provenance": {"kind": "built-in-profile", "sourceId": None, "sourceVersion": None},
            }

        baseline = [operation("copy-ld", "ld-input")]
        candidate = [operation("copy-ldc", "ldc-input")]
        logical_inputs = MODULE.validate_same_scenario_inputs(
            plan,
            route_id=route_id,
            capability_fingerprint=capability,
            baseline_invocation_arguments=["--ld", "<INPUT>"],
            candidate_invocation_arguments=["--ldc", "<INPUT>"],
            baseline_inputs=[
                {"slotId": "dp-input", "size": 1, "sha256": "1" * 64},
                {"slotId": "tp-input", "size": 2, "sha256": "2" * 64},
                {"slotId": "ld-input", "size": 3, "sha256": "3" * 64},
            ],
            candidate_inputs=[
                {"slotId": "dp-input", "size": 1, "sha256": "1" * 64},
                {"slotId": "tp-input", "size": 2, "sha256": "2" * 64},
                {"slotId": "ldc-input", "size": 3, "sha256": "3" * 64},
            ],
        )
        self.assertEqual("ldc", logical_inputs[-1]["logicalInputId"])
        report_validation = MODULE.build_independent_report_validation(
            route_id=route_id,
            capability_fingerprint=capability,
            baseline_projection={"compiledOperations": baseline, "compiledMutations": []},
            baseline_compiled_authority={"compiledOperations": copy.deepcopy(baseline), "compiledMutations": []},
            candidate_projection={"compiledOperations": candidate, "compiledMutations": []},
            candidate_compiled_authority={"compiledOperations": copy.deepcopy(candidate), "compiledMutations": []},
            baseline_raw_report_sha256="4" * 64,
            candidate_raw_report_sha256="5" * 64,
        )
        self.assertNotEqual(
            report_validation["baseline"]["projectionSha256"],
            report_validation["candidate"]["projectionSha256"],
        )

        for mutation in (
            "other-route",
            "third-option",
            "third-slot",
            "third-operation",
            "sequence",
            "source-range",
            "target-range",
            "reason",
        ):
            baseline_ops = copy.deepcopy(baseline)
            candidate_ops = copy.deepcopy(candidate)
            baseline_args = ["--ld", "<INPUT>"]
            candidate_args = ["--ldc", "<INPUT>"]
            baseline_inputs = [
                {"slotId": "dp-input", "size": 1, "sha256": "1" * 64},
                {"slotId": "tp-input", "size": 2, "sha256": "2" * 64},
                {"slotId": "ld-input", "size": 3, "sha256": "3" * 64},
            ]
            candidate_inputs = [
                {"slotId": "dp-input", "size": 1, "sha256": "1" * 64},
                {"slotId": "tp-input", "size": 2, "sha256": "2" * 64},
                {"slotId": "ldc-input", "size": 3, "sha256": "3" * 64},
            ]
            invalid_route = route_id
            if mutation == "other-route":
                invalid_route = "route-other"
            elif mutation == "third-option":
                candidate_args[0] = "--initial-code"
            elif mutation == "third-slot":
                candidate_inputs[-1]["slotId"] = "initial-code-input"
            elif mutation == "third-operation":
                candidate_ops[0]["operationId"] = "copy-initial-code"
            elif mutation == "sequence":
                candidate_ops[0]["sequence"] = 301
            elif mutation == "source-range":
                candidate_ops[0]["sourceRange"]["endExclusive"] -= 1
            elif mutation == "target-range":
                candidate_ops[0]["targetRange"]["endExclusive"] -= 1
            else:
                candidate_ops[0]["reason"] = "different executable meaning"
            with self.subTest(mutation=mutation):
                with self.assertRaises(MODULE.ParityError) as captured:
                    if mutation in {"third-operation", "sequence", "source-range", "target-range", "reason"}:
                        MODULE.build_independent_report_validation(
                            route_id=invalid_route,
                            capability_fingerprint=capability,
                            baseline_projection={"compiledOperations": baseline_ops, "compiledMutations": []},
                            baseline_compiled_authority={"compiledOperations": baseline, "compiledMutations": []},
                            candidate_projection={"compiledOperations": candidate_ops, "compiledMutations": []},
                            candidate_compiled_authority={"compiledOperations": candidate, "compiledMutations": []},
                            baseline_raw_report_sha256="4" * 64,
                            candidate_raw_report_sha256="5" * 64,
                        )
                    else:
                        MODULE.validate_same_scenario_inputs(
                            plan,
                            route_id=invalid_route,
                            capability_fingerprint=capability,
                            baseline_invocation_arguments=baseline_args,
                            candidate_invocation_arguments=candidate_args,
                            baseline_inputs=baseline_inputs,
                            candidate_inputs=candidate_inputs,
                        )
                self.assertEqual("PARITY_PROVENANCE_INVALID", captured.exception.code)

    def test_nt51929_reason_text_may_differ_only_when_each_raw_report_matches_its_authority(self) -> None:
        route_id = "route-7-nt51929-8-ab-merge-13-selector-free-21-nt51929-ab-merge-512k"
        capability = "94d31e3e032cce5cc53e62ff57c41c2aa50ac3d8c32c538d67027add98477f09"
        operation_ids = ["relocate-tpb-ilm", "relocate-tpb-dlm", "relocate-tpb-diff"]

        def projections(reason_suffix: str) -> tuple[list[dict[str, object]], list[dict[str, object]]]:
            operations: list[dict[str, object]] = []
            mutations: list[dict[str, object]] = []
            for index, operation_id in enumerate(operation_ids):
                start = 29028 + index * 4
                noun = ("ILM", "DLM", "difference")[index]
                reason = f"Relocate the TPB {noun} address {reason_suffix}"
                operations.append({
                    "operationId": operation_id,
                    "sequence": 300 + index * 100,
                    "kind": "TransformScalar",
                    "status": "succeeded",
                    "sourceSpaceId": "tp-b-work",
                    "sourceRange": {"addressSpace": "tp-b-work", "start": start, "endExclusive": start + 4},
                    "targetSpaceId": "tp-b-work",
                    "targetRange": {"addressSpace": "tp-b-work", "start": start, "endExclusive": start + 4},
                    "overlapPolicy": "Reject",
                    "processor": None,
                    "executedCommands": [],
                    "reason": reason,
                    "provenance": {"kind": "built-in-profile", "sourceId": None, "sourceVersion": None},
                })
                mutations.append({
                    "operationId": operation_id,
                    "kind": "TransformScalar",
                    "targetSpaceId": "tp-b-work",
                    "targetRange": {"addressSpace": "tp-b-work", "start": start, "endExclusive": start + 4},
                    "changedByteCount": 1,
                    "beforeSha256": hashlib.sha256(f"before:{index}".encode()).hexdigest(),
                    "afterSha256": hashlib.sha256(f"after:{index}".encode()).hexdigest(),
                    "reason": reason,
                })
            return operations, mutations

        baseline_ops, baseline_mutations = projections("by the fixed B-bank offset.")
        candidate_ops, candidate_mutations = projections("by the resolved A-to-B bank instance delta.")
        result = MODULE.build_independent_report_validation(
            route_id=route_id,
            capability_fingerprint=capability,
            baseline_projection={"compiledOperations": baseline_ops, "compiledMutations": baseline_mutations},
            baseline_compiled_authority={"compiledOperations": copy.deepcopy(baseline_ops), "compiledMutations": copy.deepcopy(baseline_mutations)},
            candidate_projection={"compiledOperations": candidate_ops, "compiledMutations": candidate_mutations},
            candidate_compiled_authority={"compiledOperations": copy.deepcopy(candidate_ops), "compiledMutations": copy.deepcopy(candidate_mutations)},
            baseline_raw_report_sha256="6" * 64,
            candidate_raw_report_sha256="7" * 64,
        )
        self.assertEqual("independent-executor-typed-authority", result["kind"])
        self.assertNotEqual(result["baseline"]["projectionSha256"], result["candidate"]["projectionSha256"])

        for mutation in ("other-route", "fourth-id", "kind", "range", "hash", "changed-count", "third-reason"):
            candidate_ops_drift = copy.deepcopy(candidate_ops)
            candidate_mutations_drift = copy.deepcopy(candidate_mutations)
            if mutation == "other-route":
                candidate_ops_drift[0]["operationId"] = "other-route-operation"
            elif mutation == "fourth-id":
                candidate_ops_drift[0]["operationId"] = "relocate-other"
            elif mutation == "kind":
                candidate_ops_drift[0]["kind"] = "CopyRange"
            elif mutation == "range":
                candidate_ops_drift[0]["targetRange"]["endExclusive"] += 1
            elif mutation == "hash":
                candidate_mutations_drift[0]["afterSha256"] = "f" * 64
            elif mutation == "changed-count":
                candidate_mutations_drift[0]["changedByteCount"] = 2
            else:
                candidate_ops_drift[0]["reason"] = "different executable meaning"
                candidate_mutations_drift[0]["reason"] = "different executable meaning"
            with self.subTest(mutation=mutation):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.build_independent_report_validation(
                        route_id=route_id,
                        capability_fingerprint=capability,
                        baseline_projection={"compiledOperations": baseline_ops, "compiledMutations": baseline_mutations},
                        baseline_compiled_authority={"compiledOperations": baseline_ops, "compiledMutations": baseline_mutations},
                        candidate_projection={"compiledOperations": candidate_ops_drift, "compiledMutations": candidate_mutations_drift},
                        candidate_compiled_authority={"compiledOperations": candidate_ops, "compiledMutations": candidate_mutations},
                        baseline_raw_report_sha256="6" * 64,
                        candidate_raw_report_sha256="7" * 64,
                    )
                self.assertEqual("PARITY_PROVENANCE_INVALID", captured.exception.code)

    def test_report_content_must_bind_output_terminal_and_processor_outcome(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            report = root / "report.json"
            application_authority_report = root / "application-authority-report.json"
            application_report = root / "application-report.json"
            source = root / "source.bin"
            second_source = root / "second-source.bin"
            output = root / "output.bin"
            source.write_bytes(b"source")
            second_source.write_bytes(b"second-source")
            output.write_bytes(b"output")

            def artifact(path: Path) -> dict[str, object]:
                return {
                    "path": str(path),
                    "size": path.stat().st_size,
                    "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                }

            receipt = {
                "schemaVersion": "1.0",
                "role": "baseline-exact",
                "executionArtifactSha256": "e889668cf092fab4877aeea84642d3b48a9969a6184f1c128500673fa136f9db",
                "executorIdentitySha256": "861fa0fae7bf5904cac88a4bcb6ed6e0aef1a54518e0903914f2121fbc411bfb",
                "routeId": "route-test",
                "capabilityFingerprint": "b" * 64,
                "scenario": {
                    "icId": "NT51927",
                    "workflowId": "standard-merge",
                    "icCountVariant": "selector-free",
                    "mapVariant": "map",
                    "selectionToken": "default",
                    "expectedProfileId": "profile-route-test",
                    "outputCapacity": 6,
                    "compilationFingerprint": "c" * 64,
                },
                "captureAdapter": {
                    "contractVersion": "1.0",
                    "scriptSha256": (
                        hashlib.sha256(MODULE_PATH.read_bytes()).hexdigest()
                        if MODULE_PATH.is_file()
                        else "a" * 64
                    ),
                },
                "authorityInvocation": {
                    "interface": "source-baseline-cli",
                    "operatorLogin": "dennis40816",
                    "operation": "preview",
                    "argumentsSha256": "0" * 64,
                    "startedAtUtc": "2026-08-25T23:58:58Z",
                    "completedAtUtc": "2026-08-25T23:58:59Z",
                    "result": "success",
                },
                "invocation": {
                    "interface": "source-baseline-cli",
                    "operatorLogin": "dennis40816",
                    "operation": "build",
                    "argumentsSha256": "0" * 64,
                    "startedAtUtc": "2026-08-25T23:59:00Z",
                    "completedAtUtc": "2026-08-26T00:00:00Z",
                    "result": "success",
                },
                "reportContractVersion": "1.0",
                "inputs": [
                    {
                        "slotId": "source",
                        "role": "source",
                        **artifact(source),
                    },
                    {
                        "slotId": "second-source",
                        "role": "source",
                        **artifact(second_source),
                    }
                ],
                "output": artifact(output),
            }
            receipt["invocation"]["argumentsSha256"] = MODULE.canonical_arguments_sha256(
                receipt
            )
            receipt["authorityInvocation"][
                "argumentsSha256"
            ] = MODULE.canonical_authority_arguments_sha256(receipt)
            raw_report = self.serialized_application_report(
                route_id=receipt["routeId"],
                ic_id=receipt["scenario"]["icId"],
                workflow_id=receipt["scenario"]["workflowId"],
                compilation_fingerprint=receipt["scenario"]["compilationFingerprint"],
                output=output,
                inputs=receipt["inputs"],
                processor=True,
                started_at_utc=receipt["invocation"]["startedAtUtc"],
                completed_at_utc=receipt["invocation"]["completedAtUtc"],
            )
            self.assertEqual(
                "2026-08-25T23:59:00+00:00", raw_report["StartedAtUtc"]
            )
            self.assertEqual(
                receipt["invocation"]["startedAtUtc"],
                self.normalized_application_context_from_raw(raw_report)[
                    "startedAtUtc"
                ],
            )
            typed_operations = [
                self.normalized_operation_from_raw(
                    raw_report["Operations"][0],
                    package_root=output.parent / "fixture-package",
                    staging_root=output.parent / "fixture-staging",
                )
            ]
            typed_mutations = [
                self.normalized_mutation_from_raw(raw_report["Mutations"][0])
            ]
            application_report.write_text(
                json.dumps(raw_report),
                encoding="utf-8",
            )
            authority_report = copy.deepcopy(raw_report)
            authority_report["Mutations"] = []
            authority_report["StartedAtUtc"] = receipt["authorityInvocation"][
                "startedAtUtc"
            ]
            authority_report["CompletedAtUtc"] = receipt["authorityInvocation"][
                "completedAtUtc"
            ]
            authority_report["Output"]["Committed"] = False
            application_authority_report.write_text(
                json.dumps(authority_report), encoding="utf-8"
            )
            report_payload = {
                "schemaVersion": "1.0",
                "executionArtifactSha256": receipt["executionArtifactSha256"],
                "routeId": receipt["routeId"],
                "capabilityFingerprint": receipt["capabilityFingerprint"],
                "scenarioSha256": MODULE.canonical_json_sha256(receipt["scenario"]),
                "authorityArgumentsSha256": receipt["authorityInvocation"][
                    "argumentsSha256"
                ],
                "argumentsSha256": receipt["invocation"]["argumentsSha256"],
                "orderedInputsSha256": MODULE.canonical_ordered_inputs_sha256(
                    receipt["inputs"]
                ),
                "applicationAuthorityKind": "v0.9.16-typed-preview",
                "applicationAuthorityReport": artifact(application_authority_report),
                "applicationReportKind": "v0.9.16-typed-application",
                "applicationReport": artifact(application_report),
                "applicationContext": self.normalized_application_context_from_raw(
                    raw_report
                ),
                "compilationFingerprint": receipt["scenario"][
                    "compilationFingerprint"
                ],
                "compiledOperations": typed_operations,
                "compiledMutations": typed_mutations,
                "output": receipt["output"],
                "terminal": {
                    "result": "success",
                    "completedAtUtc": "2026-08-26T00:00:00Z",
                },
            }
            projected_operations_json = json.dumps(
                report_payload["compiledOperations"], ensure_ascii=False
            )
            self.assertNotIn(str(output.parent / "fixture-package"), projected_operations_json)
            self.assertNotIn(str(output.parent / "fixture-staging"), projected_operations_json)
            report.write_text(json.dumps(report_payload), encoding="utf-8")
            receipt["report"] = artifact(report)
            MODULE.validate_receipt(
                receipt,
                expected_execution_artifact_sha256=receipt["executionArtifactSha256"],
                expected_executor_identity_sha256=receipt["executorIdentitySha256"],
                authorized_operators={"dennis40816"},
            )

            for mutation in (
                "output",
                "terminal",
                "processor-empty",
                "processor-missing",
                "processor-extra",
                "processor-duplicate",
                "processor-id",
                "processor-range",
                "processor-failed",
                "raw-processor-id",
                "raw-processor-range",
                "raw-ic",
                "raw-mode",
                "raw-experience",
                "raw-composition-kind",
                "raw-started-at",
                "raw-completed-at",
                "raw-input-order",
                "raw-input-size",
                "raw-input-hash",
                "scenario-capacity",
                "raw-output-size",
                "raw-output-committed",
                "raw-issue",
                "raw-compilation-fingerprint",
                "raw-kind",
                "raw-source-space",
                "raw-source-range",
                "raw-overlap",
                "raw-operation-reason",
                "raw-provenance",
                "raw-command-empty",
                "raw-command-extra",
                "raw-command-duplicate",
                "projection-command-sequence",
                "raw-command-executable",
                "raw-command-working-directory",
                "raw-command-argument",
                "raw-mutation-range",
                "raw-mutation-reason",
                "raw-mutation-hash",
                "raw-and-projection-vs-authority",
                "authority-report-missing",
                "authority-report-drift",
                "authority-arguments",
                "authority-interface",
                "authority-time-order",
                "raw-normalized-substitute",
            ):
                application_authority_report.write_text(
                    json.dumps(authority_report), encoding="utf-8"
                )
                invalid_report = copy.deepcopy(report_payload)
                invalid_raw_report = copy.deepcopy(raw_report)
                if mutation == "output":
                    invalid_report["output"]["sha256"] = "e" * 64
                elif mutation == "terminal":
                    invalid_report["terminal"]["result"] = "failed"
                elif mutation == "processor-empty":
                    invalid_report["compiledOperations"] = []
                elif mutation == "processor-missing":
                    invalid_report["compiledOperations"][0].pop("processor")
                elif mutation == "processor-extra":
                    extra = copy.deepcopy(invalid_report["compiledOperations"][0])
                    extra["operationId"] = "op-2"
                    extra["sequence"] = 1
                    invalid_report["compiledOperations"].append(extra)
                elif mutation == "processor-duplicate":
                    invalid_report["compiledOperations"].append(
                        copy.deepcopy(invalid_report["compiledOperations"][0])
                    )
                elif mutation == "processor-id":
                    invalid_report["compiledOperations"][0]["processor"][
                        "processorId"
                    ] = "nfc.other"
                elif mutation == "processor-range":
                    invalid_report["compiledOperations"][0]["processor"][
                        "allowedWriteRanges"
                    ][0]["endExclusive"] = 0
                elif mutation == "processor-failed":
                    invalid_report["compiledOperations"][0]["status"] = "failed"
                elif mutation == "raw-processor-id":
                    invalid_raw_report["Operations"][0]["ProcessorId"] = "nfc.other"
                elif mutation == "raw-processor-range":
                    invalid_raw_report["Operations"][0]["ProcessorAllowedWriteRanges"][0][
                        "EndExclusive"
                    ] -= 1
                elif mutation == "raw-ic":
                    invalid_raw_report["IcId"] = "NT51928"
                elif mutation == "raw-mode":
                    invalid_raw_report["ModeId"] = "ab-merge"
                elif mutation == "raw-experience":
                    invalid_raw_report["ExperienceId"] = "ab-merge"
                elif mutation == "raw-composition-kind":
                    invalid_raw_report["CompositionKind"] = "Replace"
                elif mutation == "raw-started-at":
                    invalid_raw_report["StartedAtUtc"] = "2026-08-25T23:58:59Z"
                elif mutation == "raw-completed-at":
                    invalid_raw_report["CompletedAtUtc"] = "2026-08-26T00:00:01Z"
                elif mutation == "raw-input-order":
                    invalid_raw_report["Inputs"].reverse()
                elif mutation == "raw-input-size":
                    invalid_raw_report["Inputs"][0]["Size"] += 1
                elif mutation == "raw-input-hash":
                    invalid_raw_report["Inputs"][0]["Sha256"] = "f" * 64
                elif mutation == "scenario-capacity":
                    invalid_raw_report = copy.deepcopy(raw_report)
                elif mutation == "raw-output-size":
                    invalid_raw_report["Output"]["Size"] += 1
                elif mutation == "raw-output-committed":
                    invalid_raw_report["Output"]["Committed"] = False
                elif mutation == "raw-issue":
                    invalid_raw_report["Issues"] = [
                        {"Code": "TEST", "Severity": "Error", "Message": "failed"}
                    ]
                elif mutation == "raw-compilation-fingerprint":
                    invalid_raw_report["CompilationFingerprint"] = "f" * 64
                elif mutation == "raw-kind":
                    invalid_raw_report["Operations"][0]["Kind"] = "FillRange"
                elif mutation == "raw-source-space":
                    invalid_raw_report["Operations"][0]["SourceSpaceId"] = "other-source"
                elif mutation == "raw-source-range":
                    invalid_raw_report["Operations"][0]["SourceRange"]["Start"] = 1
                elif mutation == "raw-overlap":
                    invalid_raw_report["Operations"][0]["OverlapPolicy"] = "Allow"
                elif mutation == "raw-operation-reason":
                    invalid_raw_report["Operations"][0]["Reason"] = "other reason"
                elif mutation == "raw-provenance":
                    invalid_raw_report["Operations"][0]["Provenance"][
                        "Kind"
                    ] = "runtime-general-mapping"
                elif mutation == "raw-command-empty":
                    invalid_raw_report["Operations"][0]["ExecutedCommands"] = []
                elif mutation == "raw-command-extra":
                    extra_command = copy.deepcopy(
                        invalid_raw_report["Operations"][0]["ExecutedCommands"][0]
                    )
                    extra_command["Arguments"][-1] = "extra"
                    invalid_raw_report["Operations"][0]["ExecutedCommands"].append(
                        extra_command
                    )
                elif mutation == "raw-command-duplicate":
                    invalid_raw_report["Operations"][0]["ExecutedCommands"].append(
                        copy.deepcopy(
                            invalid_raw_report["Operations"][0]["ExecutedCommands"][0]
                        )
                    )
                elif mutation == "projection-command-sequence":
                    invalid_report["compiledOperations"][0]["executedCommands"][0][
                        "sequence"
                    ] = 1
                elif mutation == "raw-command-executable":
                    invalid_raw_report["Operations"][0]["ExecutedCommands"][0][
                        "ExecutablePath"
                    ] = str(output.parent / "outside.exe")
                elif mutation == "raw-command-working-directory":
                    invalid_raw_report["Operations"][0]["ExecutedCommands"][0][
                        "WorkingDirectory"
                    ] = str(output.parent / "not-staging")
                elif mutation == "raw-command-argument":
                    invalid_raw_report["Operations"][0]["ExecutedCommands"][0][
                        "Arguments"
                    ][-1] = "other"
                elif mutation == "raw-mutation-range":
                    invalid_raw_report["Mutations"][0]["TargetRange"]["Start"] = 1
                elif mutation == "raw-mutation-reason":
                    invalid_raw_report["Mutations"][0]["Reason"] = "other reason"
                elif mutation == "raw-mutation-hash":
                    invalid_raw_report["Mutations"][0]["AfterSha256"] = "e" * 64
                elif mutation == "raw-and-projection-vs-authority":
                    invalid_raw_report["Operations"][0]["Reason"] = "self-consistent but unauthorized"
                    invalid_report["compiledOperations"][0]["reason"] = "self-consistent but unauthorized"
                elif mutation == "authority-report-missing":
                    invalid_report["applicationAuthorityReport"]["path"] = str(
                        root / "missing-authority-report.json"
                    )
                elif mutation == "authority-report-drift":
                    drifted_authority = copy.deepcopy(authority_report)
                    drifted_authority["Operations"][0]["Reason"] = "drifted authority"
                    application_authority_report.write_text(
                        json.dumps(drifted_authority), encoding="utf-8"
                    )
                else:
                    invalid_raw_report = {
                        "CompilationFingerprint": receipt["scenario"][
                            "compilationFingerprint"
                        ],
                        "Operations": typed_operations,
                        "Mutations": typed_mutations,
                        "Output": receipt["output"],
                    }
                application_report.write_text(
                    json.dumps(invalid_raw_report), encoding="utf-8"
                )
                invalid_report["applicationReport"] = artifact(application_report)
                report.write_text(json.dumps(invalid_report), encoding="utf-8")
                invalid_receipt = copy.deepcopy(receipt)
                invalid_receipt["report"] = artifact(report)
                if mutation == "scenario-capacity":
                    invalid_receipt["scenario"]["outputCapacity"] += 1
                    invalid_receipt["invocation"][
                        "argumentsSha256"
                    ] = MODULE.canonical_arguments_sha256(invalid_receipt)
                    invalid_receipt["authorityInvocation"][
                        "argumentsSha256"
                    ] = MODULE.canonical_authority_arguments_sha256(invalid_receipt)
                    invalid_report["scenarioSha256"] = MODULE.canonical_json_sha256(
                        invalid_receipt["scenario"]
                    )
                    invalid_report["authorityArgumentsSha256"] = invalid_receipt[
                        "authorityInvocation"
                    ]["argumentsSha256"]
                    invalid_report["argumentsSha256"] = invalid_receipt[
                        "invocation"
                    ]["argumentsSha256"]
                    report.write_text(json.dumps(invalid_report), encoding="utf-8")
                    invalid_receipt["report"] = artifact(report)
                elif mutation == "authority-arguments":
                    invalid_receipt["authorityInvocation"]["argumentsSha256"] = "f" * 64
                elif mutation == "authority-interface":
                    invalid_receipt["authorityInvocation"][
                        "interface"
                    ] = "candidate-source-cli"
                elif mutation == "authority-time-order":
                    invalid_receipt["authorityInvocation"][
                        "completedAtUtc"
                    ] = invalid_receipt["invocation"]["completedAtUtc"]

                with self.subTest(mutation=mutation):
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.validate_receipt(
                            invalid_receipt,
                            expected_execution_artifact_sha256=receipt["executionArtifactSha256"],
                            expected_executor_identity_sha256=receipt["executorIdentitySha256"],
                            authorized_operators={"dennis40816"},
                        )
                    self.assertEqual(
                        (
                            "PARITY_ARTIFACT_MISSING"
                            if mutation == "authority-report-missing"
                            else "PARITY_PROVENANCE_INVALID"
                        ),
                        captured.exception.code,
                    )

    def test_forged_arguments_or_unpinned_capture_adapter_fails_provenance(self) -> None:
        receipt = {
            "executionArtifactSha256": "a" * 64,
            "executorIdentitySha256": "1" * 64,
            "routeId": "route-test",
            "capabilityFingerprint": "b" * 64,
            "scenario": {
                "icId": "NT51927",
                "workflowId": "standard-merge",
                "icCountVariant": "selector-free",
                "mapVariant": "map",
                "selectionToken": "default",
                "expectedProfileId": "profile-route-test",
                "outputCapacity": 6,
                "compilationFingerprint": "c" * 64,
            },
            "inputs": [
                {"slotId": "source", "role": "source", "size": 6, "sha256": "d" * 64}
            ],
            "captureAdapter": {"contractVersion": "1.0", "scriptSha256": "1" * 64},
            "invocation": {
                "operatorLogin": "dennis40816",
                "argumentsSha256": "0" * 64,
            },
        }
        receipt["invocation"]["argumentsSha256"] = MODULE.canonical_arguments_sha256(
            receipt
        )
        for mutation in ("arguments", "adapter", "operator", "executor", "artifact"):
            invalid = copy.deepcopy(receipt)
            if mutation == "arguments":
                invalid["invocation"]["argumentsSha256"] = "f" * 64
            elif mutation == "adapter":
                invalid["captureAdapter"]["scriptSha256"] = "e" * 64
            elif mutation == "operator":
                invalid["invocation"]["operatorLogin"] = "unknown-operator"
            elif mutation == "executor":
                invalid["executorIdentitySha256"] = "e" * 64
            else:
                invalid["executionArtifactSha256"] = "e" * 64
            with self.subTest(mutation=mutation):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.validate_invocation_authority(
                        invalid,
                        comparator_sha256="1" * 64,
                        expected_execution_artifact_sha256="a" * 64,
                        expected_executor_identity_sha256="1" * 64,
                        authorized_operators={"dennis40816"},
                    )
                self.assertEqual("PARITY_PROVENANCE_INVALID", captured.exception.code)

    def test_each_typed_report_projection_matches_its_own_compiled_authority(self) -> None:
        operation = {
            "operationId": "op-1",
            "sequence": 0,
            "kind": "RunExternalProcessor",
            "status": "succeeded",
            "sourceSpaceId": "source",
            "sourceRange": {
                "addressSpace": "source",
                "start": 0,
                "endExclusive": 8,
            },
            "targetSpaceId": "output-image",
            "targetRange": {
                "addressSpace": "output-image",
                "start": 0,
                "endExclusive": 8,
            },
            "overlapPolicy": "Reject",
            "processor": {
                "processorId": "nfc.test",
                "toolBindingId": "test-tool",
                "allowedReadRanges": [
                    {
                        "addressSpace": "output-image",
                        "start": 0,
                        "endExclusive": 8,
                    }
                ],
                "allowedWriteRanges": [
                    {
                        "addressSpace": "output-image",
                        "start": 2,
                        "endExclusive": 6,
                    }
                ],
            },
            "executedCommands": [
                {
                    "sequence": 0,
                    "executablePackagePath": "external-tools/nfc-test.exe",
                    "workingDirectoryKind": "host-created-staging",
                    "argumentCount": 6,
                    "canonicalArgumentsSha256": "a" * 64,
                }
            ],
            "reason": "typed authority",
            "provenance": {
                "kind": "built-in-profile",
                "sourceId": None,
                "sourceVersion": None,
            },
        }
        MODULE.validate_report_projection_against_compiled_authority(
            {"compiledOperations": [operation], "compiledMutations": []},
            {"compiledOperations": [copy.deepcopy(operation)], "compiledMutations": []},
        )

        no_processor = copy.deepcopy(operation)
        no_processor["kind"] = "CopyRange"
        no_processor["processor"] = None
        no_processor["executedCommands"] = []
        MODULE.validate_report_projection_against_compiled_authority(
            {"compiledOperations": [no_processor], "compiledMutations": []},
            {"compiledOperations": [copy.deepcopy(no_processor)], "compiledMutations": []},
        )

        mutation_outcome = {
            "operationId": "op-1",
            "kind": "RunExternalProcessor",
            "targetSpaceId": "output-image",
            "targetRange": {
                "addressSpace": "output-image",
                "start": 2,
                "endExclusive": 6,
            },
            "changedByteCount": 4,
            "beforeSha256": "1" * 64,
            "afterSha256": "2" * 64,
            "reason": "typed mutation",
        }
        baseline_projection = {
            "compilationFingerprint": "0" * 64,
            "compiledOperations": [operation],
            "compiledMutations": [mutation_outcome],
        }
        current_projection = copy.deepcopy(baseline_projection)
        current_projection["compilationFingerprint"] = "1" * 64
        MODULE.validate_report_projection_against_compiled_authority(
            baseline_projection,
            {
                "compilationFingerprint": "0" * 64,
                "compiledOperations": copy.deepcopy(baseline_projection["compiledOperations"]),
                "compiledMutations": [],
            },
        )
        MODULE.validate_report_projection_against_compiled_authority(
            current_projection,
            {
                "compilationFingerprint": "1" * 64,
                "compiledOperations": copy.deepcopy(current_projection["compiledOperations"]),
                "compiledMutations": [],
            },
        )

        for mutation in (
            "missing",
            "extra",
            "duplicate",
            "id",
            "range",
            "hash-shape",
            "target-space",
            "changed-count",
            "empty-reason",
        ):
            current_projection = copy.deepcopy(baseline_projection)
            current_projection["compilationFingerprint"] = "1" * 64
            current_mutations = current_projection["compiledMutations"]
            if mutation == "missing":
                current_mutations.clear()
            elif mutation == "extra":
                extra = copy.deepcopy(mutation_outcome)
                extra["operationId"] = "op-2"
                current_mutations.append(extra)
            elif mutation == "duplicate":
                current_mutations.append(copy.deepcopy(mutation_outcome))
            elif mutation == "id":
                current_mutations[0]["operationId"] = "op-other"
            elif mutation == "range":
                current_mutations[0]["targetRange"]["endExclusive"] = 7
            elif mutation == "hash-shape":
                current_mutations[0]["afterSha256"] = "not-a-sha256"
            elif mutation == "target-space":
                current_mutations[0]["targetSpaceId"] = "other-output"
            elif mutation == "changed-count":
                current_mutations[0]["changedByteCount"] = 5
            else:
                current_mutations[0]["reason"] = ""
            with self.subTest(mutation_projection=mutation):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.validate_report_projection_against_compiled_authority(
                        current_projection,
                        {
                            "compilationFingerprint": "1" * 64,
                            "compiledOperations": baseline_projection["compiledOperations"],
                            "compiledMutations": [],
                        },
                    )
                self.assertEqual("PARITY_PROVENANCE_INVALID", captured.exception.code)

        for mutation in (
            "missing",
            "extra",
            "duplicate",
            "id",
            "range",
            "runtime-command",
            "reason",
            "failed",
        ):
            current = [copy.deepcopy(operation)]
            if mutation == "missing":
                current[0].pop("processor")
            elif mutation == "extra":
                extra = copy.deepcopy(operation)
                extra["operationId"] = "op-2"
                extra["sequence"] = 1
                current.append(extra)
            elif mutation == "duplicate":
                current.append(copy.deepcopy(operation))
            elif mutation == "id":
                current[0]["processor"]["processorId"] = "nfc.other"
            elif mutation == "range":
                current[0]["processor"]["allowedWriteRanges"][0]["endExclusive"] = 7
            elif mutation == "runtime-command":
                current[0]["executedCommands"][0]["canonicalArgumentsSha256"] = "b" * 64
            elif mutation == "reason":
                current[0]["reason"] = "other authority"
            else:
                current[0]["status"] = "failed"
            with self.subTest(mutation=mutation):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.validate_report_projection_against_compiled_authority(
                        {"compiledOperations": current, "compiledMutations": []},
                        {"compiledOperations": [operation], "compiledMutations": []},
                    )
                self.assertEqual("PARITY_PROVENANCE_INVALID", captured.exception.code)

    def test_report_ranges_are_half_open_contained_counted_and_processor_safe(self) -> None:
        operation = {
            "operationId": "processor-op",
            "sequence": 0,
            "kind": "RunExternalProcessor",
            "status": "succeeded",
            "sourceSpaceId": "source",
            "sourceRange": {"addressSpace": "source", "start": 0, "endExclusive": 8},
            "targetSpaceId": "output-image",
            "targetRange": {
                "addressSpace": "output-image",
                "start": 0,
                "endExclusive": 8,
            },
            "overlapPolicy": "Reject",
            "processor": {
                "processorId": "nfc.test",
                "toolBindingId": "test-tool",
                "allowedReadRanges": [
                    {"addressSpace": "output-image", "start": 0, "endExclusive": 8}
                ],
                "allowedWriteRanges": [
                    {"addressSpace": "output-image", "start": 2, "endExclusive": 6}
                ],
            },
            "executedCommands": [{
                "sequence": 0,
                "executablePackagePath": "external-tools/nfc-test.exe",
                "workingDirectoryKind": "host-created-staging",
                "argumentCount": 6,
                "canonicalArgumentsSha256": "a" * 64,
            }],
            "reason": "typed authority",
            "provenance": {"kind": "built-in-profile", "sourceId": None, "sourceVersion": None},
        }
        mutation = {
            "operationId": "processor-op",
            "kind": "RunExternalProcessor",
            "targetSpaceId": "output-image",
            "targetRange": {
                "addressSpace": "output-image",
                "start": 2,
                "endExclusive": 6,
            },
            "changedByteCount": 4,
            "beforeSha256": "1" * 64,
            "afterSha256": "2" * 64,
            "reason": "typed mutation",
        }
        projection = {
            "compiledOperations": [operation],
            "compiledMutations": [mutation],
        }
        capacities = {
            "source": 16,
            "output-image": 16,
            "other-source": 16,
            "other-output": 16,
        }
        MODULE.validate_semantic_report_ranges(projection, capacities)

        for mutation_kind in (
            "negative-start",
            "empty-range",
            "source-integer-overflow",
            "target-integer-overflow",
            "mutation-integer-overflow",
            "processor-read-integer-overflow",
            "processor-write-integer-overflow",
            "source-outside-capacity",
            "target-outside-capacity",
            "source-address-space-mismatch",
            "target-address-space-mismatch",
            "mutation-address-space-mismatch",
            "copy-length-mismatch",
            "mutation-outside-operation",
            "changed-count-exceeds-range",
            "processor-read-outside-operation",
            "processor-read-overlap",
            "processor-read-address-space-mismatch",
            "processor-write-outside-operation",
            "processor-write-overlap",
            "processor-write-address-space-mismatch",
            "mutation-outside-allowed-write",
            "operation-overlap-rejected",
        ):
            invalid = copy.deepcopy(projection)
            op = invalid["compiledOperations"][0]
            outcome = invalid["compiledMutations"][0]
            if mutation_kind == "negative-start":
                op["sourceRange"]["start"] = -1
            elif mutation_kind == "empty-range":
                op["targetRange"]["start"] = op["targetRange"]["endExclusive"]
            elif mutation_kind == "source-integer-overflow":
                op["sourceRange"].update(start=2**63 - 1, endExclusive=2**63)
            elif mutation_kind == "target-integer-overflow":
                op["targetRange"].update(start=2**63 - 1, endExclusive=2**63)
            elif mutation_kind == "mutation-integer-overflow":
                outcome["targetRange"].update(start=2**63 - 1, endExclusive=2**63)
            elif mutation_kind == "processor-read-integer-overflow":
                op["processor"]["allowedReadRanges"][0].update(
                    start=2**63 - 1, endExclusive=2**63
                )
            elif mutation_kind == "processor-write-integer-overflow":
                op["processor"]["allowedWriteRanges"][0].update(
                    start=2**63 - 1, endExclusive=2**63
                )
            elif mutation_kind == "source-outside-capacity":
                op["sourceRange"]["endExclusive"] = 17
            elif mutation_kind == "target-outside-capacity":
                op["targetRange"]["endExclusive"] = 17
            elif mutation_kind == "source-address-space-mismatch":
                op["sourceRange"]["addressSpace"] = "other-source"
            elif mutation_kind == "target-address-space-mismatch":
                op["targetRange"]["addressSpace"] = "other-output"
            elif mutation_kind == "mutation-address-space-mismatch":
                outcome["targetRange"]["addressSpace"] = "other-output"
            elif mutation_kind == "copy-length-mismatch":
                op["kind"] = "CopyRange"
                op["processor"] = None
                op["executedCommands"] = []
                op["sourceRange"]["endExclusive"] = 7
            elif mutation_kind == "mutation-outside-operation":
                op["kind"] = "CopyRange"
                op["processor"] = None
                op["executedCommands"] = []
                outcome["kind"] = "CopyRange"
                outcome["targetRange"].update(start=7, endExclusive=9)
                outcome["changedByteCount"] = 2
            elif mutation_kind == "changed-count-exceeds-range":
                outcome["changedByteCount"] = 5
            elif mutation_kind == "processor-read-outside-operation":
                op["processor"]["allowedReadRanges"][0].update(
                    start=7, endExclusive=9
                )
            elif mutation_kind == "processor-read-overlap":
                op["processor"]["allowedReadRanges"].append(
                    {"addressSpace": "output-image", "start": 3, "endExclusive": 5}
                )
            elif mutation_kind == "processor-read-address-space-mismatch":
                op["processor"]["allowedReadRanges"][0]["addressSpace"] = "other-output"
            elif mutation_kind == "processor-write-outside-operation":
                op["processor"]["allowedWriteRanges"][0].update(
                    start=7, endExclusive=9
                )
            elif mutation_kind == "processor-write-overlap":
                op["processor"]["allowedWriteRanges"].append(
                    {"addressSpace": "output-image", "start": 3, "endExclusive": 5}
                )
            elif mutation_kind == "processor-write-address-space-mismatch":
                op["processor"]["allowedWriteRanges"][0]["addressSpace"] = "other-output"
            elif mutation_kind == "mutation-outside-allowed-write":
                outcome["targetRange"].update(start=0, endExclusive=1)
                outcome["changedByteCount"] = 1
            else:
                second = copy.deepcopy(op)
                second["operationId"] = "overlapping-op"
                second["sequence"] = 1
                second["targetRange"].update(start=4, endExclusive=7)
                invalid["compiledOperations"].append(second)
            with self.subTest(range_mutation=mutation_kind):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.validate_semantic_report_ranges(invalid, capacities)
                self.assertEqual("PARITY_REPORT_RANGE_INVALID", captured.exception.code)

if __name__ == "__main__":
    unittest.main()

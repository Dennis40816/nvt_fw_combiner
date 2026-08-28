"""Behavioral red tests for comparator-owned CLI execution plumbing."""

from __future__ import annotations

import copy
from dataclasses import dataclass
import hashlib
import json
import stat
import subprocess
import tempfile
import unittest
from unittest.mock import patch
from pathlib import Path

from tests.scripts.v0916_parity_test_support import MODULE, ROOT, V0916ParityTestBase


def reload_with_post_capture_swap(
    receipt: dict[str, object], target_roles: set[str]
) -> tuple[dict[str, object], set[str]]:
    """Replace paths after their final hash capture and prove no semantic reopen occurs."""
    original_reader = MODULE._read_artifact_reference
    originals: dict[Path, bytes] = {}
    captured_roles: set[str] = set()

    def capture_then_swap(reference: dict[str, object], role: str) -> tuple[Path, bytes]:
        path, payload = original_reader(reference, role)
        if role in target_roles and path not in originals:
            originals[path] = payload
            captured_roles.add(role)
            path.write_bytes(b"post-capture-second-read-swap")
        return path, payload

    try:
        with patch.object(
            MODULE, "_read_artifact_reference", side_effect=capture_then_swap
        ):
            reloaded = MODULE._reload_receipt_for_evidence(receipt)
    finally:
        for path, payload in originals.items():
            path.write_bytes(payload)
    return reloaded, captured_roles


def build_with_post_capture_swap(
    *, target_roles: set[str], **kwargs: object
) -> tuple[dict[str, object], set[str]]:
    """Swap source paths after capture; receipt construction must use captured bytes."""

    original_reader = MODULE._read_artifact_reference
    originals: dict[Path, bytes] = {}
    captured_roles: set[str] = set()

    def capture_then_swap(reference: dict[str, object], role: str) -> tuple[Path, bytes]:
        path, payload = original_reader(reference, role)
        if role in target_roles and path not in originals:
            originals[path] = payload
            captured_roles.add(role)
            path.write_bytes(b"post-capture-receipt-construction-swap")
        return path, payload

    try:
        with patch.object(
            MODULE, "_read_artifact_reference", side_effect=capture_then_swap
        ):
            receipt = MODULE.build_process_receipt(**kwargs)
    finally:
        for path, payload in originals.items():
            path.write_bytes(payload)
    return receipt, captured_roles


class RecordingCliRunner:
    def __init__(
        self,
        *,
        expected_cwd: Path,
        sources: list[tuple[str, Path]],
        profile_id: str = "nt51927-standard-merge-gen-flash",
        ic_id: str = "NT51927",
        workflow_id: str = "standard-merge",
        build_profile_id: str | None = None,
        standard_merge_profile_id: str | None = None,
        map_id: str = "nt51927-standard-merge-256k",
        standard_merge_map_id: str = "standard-merge-precursor-map",
        admitted_original_to_mutate_after_preview: Path | None = None,
    ) -> None:
        self.expected_cwd = expected_cwd
        self.sources = sources
        self.profile_id = profile_id
        self.ic_id = ic_id
        self.workflow_id = workflow_id
        self.build_profile_id = build_profile_id
        self.standard_merge_profile_id = standard_merge_profile_id
        self.map_id = map_id
        self.standard_merge_map_id = standard_merge_map_id
        self.admitted_original_to_mutate_after_preview = (
            admitted_original_to_mutate_after_preview
        )
        self.calls: list[tuple[list[str], Path]] = []
        self.input_observations: list[dict[str, tuple[Path, str, bool]]] = []

    def run(self, argv: list[str], cwd: Path) -> subprocess.CompletedProcess[str]:
        self.calls.append((list(argv), cwd))
        if cwd != self.expected_cwd:
            raise AssertionError("CLI must run from its verified source root")
        action = argv[3]
        workflow_id = argv[2]
        sources = self._sources_from_argv(argv, workflow_id)
        self.input_observations.append({
            {"dp-input": "--dp", "tp-input": "--tp", "dp-ab-input": "--dp-ab", "tp-a-input": "--tp-a", "tp-b-input": "--tp-b"}.get(slot, slot): (
                path,
                hashlib.sha256(path.read_bytes()).hexdigest(),
                (path.stat().st_mode & stat.S_IWRITE) == 0,
            )
            for slot, path in sources
        })
        report = Path(argv[argv.index("--report") + 1])
        output = Path(argv[argv.index("--output") + 1])
        payload = sources[0][1].read_bytes()
        output.parent.mkdir(parents=True, exist_ok=True)
        if action == "build":
            output.write_bytes(payload)
        profile_id = (
            self.standard_merge_profile_id or self.profile_id
            if workflow_id == "standard-merge"
            else self.profile_id
        )
        raw = self._report(
            output,
            payload,
            sources,
            profile_id=profile_id,
            workflow_id=workflow_id,
            map_id=(
                self.standard_merge_map_id
                if workflow_id == "standard-merge" and self.workflow_id != "standard-merge"
                else self.map_id
            ),
            committed=action == "build",
        )
        if action == "build" and self.build_profile_id is not None:
            raw["ProfileId"] = self.build_profile_id
        if action == "preview":
            raw["StartedAtUtc"] = "2026-08-25T23:59:58+00:00"
            raw["CompletedAtUtc"] = "2026-08-25T23:59:59+00:00"
        report.parent.mkdir(parents=True, exist_ok=True)
        report.write_text(json.dumps(raw), encoding="utf-8")
        if self.admitted_original_to_mutate_after_preview is not None and action == "preview":
            original = self.admitted_original_to_mutate_after_preview
            original.write_bytes(original.read_bytes() + b"malicious-original-mutation")
        return subprocess.CompletedProcess(argv, 0, "ok", "")

    def _sources_from_argv(
        self, argv: list[str], workflow_id: str
    ) -> list[tuple[str, Path]]:
        if workflow_id == "ctrlram-replace":
            sources = [("replace-base", Path(argv[argv.index("--base") + 1]))]
            for index, value in enumerate(argv):
                if value == "--ctrlram":
                    slot, path = argv[index + 1].split("=", 1)
                    sources.append((slot, Path(path)))
            return sources
        options = (
            (("dp-ab-input", "--dp-ab"), ("tp-a-input", "--tp-a"), ("tp-b-input", "--tp-b"))
            if workflow_id == "ab-merge"
            else (("dp-input", "--dp"), ("tp-input", "--tp"))
        )
        return [(slot, Path(argv[argv.index(option) + 1])) for slot, option in options]

    def _report(
        self,
        output: Path,
        payload: bytes,
        sources: list[tuple[str, Path]],
        *,
        profile_id: str,
        workflow_id: str,
        map_id: str,
        committed: bool,
    ) -> dict[str, object]:
        digest = hashlib.sha256(payload).hexdigest()
        byte_range = {"Start": 0, "Length": len(payload), "EndExclusive": len(payload)}
        operation = {
            "OperationId": "copy-dp", "Sequence": 0, "Kind": "CopyRange",
            "Status": "Succeeded", "SourceSpaceId": sources[0][0],
            "SourceRange": byte_range, "TargetSpaceId": "output-image",
            "TargetRange": byte_range, "OverlapPolicy": "Reject",
            "ProcessorId": None, "ToolBindingId": None,
            "ProcessorAllowedReadRanges": [], "ProcessorAllowedWriteRanges": [],
            "ExecutedCommands": [], "Reason": "typed test operation",
            "Provenance": {"Kind": "built-in-profile", "SourceId": None, "SourceVersion": None},
        }
        mutations = [] if not committed else [{
            "OperationId": "copy-dp", "Kind": "CopyRange",
            "TargetSpaceId": "output-image", "TargetRange": byte_range,
            "ChangedByteCount": len(payload), "BeforeSha256": "0" * 64,
            "AfterSha256": digest, "Reason": "typed test mutation",
        }]
        return {
            "RunId": "test-run", "ProfileId": profile_id, "MapId": map_id,
            "ProfileVersion": "1.0.0", "IcId": self.ic_id,
            "ModeId": workflow_id, "ExperienceId": workflow_id,
            "CompositionKind": "Replace" if workflow_id == "ctrlram-replace" else "Merge", "StartedAtUtc": "2026-08-26T00:00:00+00:00",
            "CompletedAtUtc": "2026-08-26T00:00:01+00:00",
            "Inputs": [
                {
                    "AddressSpaceId": slot, "ArtifactId": slot,
                    "Size": path.stat().st_size,
                    "Sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                    "OriginalFileName": None,
                }
                for slot, path in sources
            ],
            "Operations": [operation], "Mutations": mutations, "Issues": [],
            "Output": {"FileName": output.name, "Size": len(payload), "Sha256": digest, "Committed": committed},
            "OutputDifferences": [], "CompilationFingerprint": "a" * 64,
            "Validations": [], "OutputNaming": None,
        }


@dataclass(frozen=True)
class SyntheticTypedInputAuthority:
    """Test-only typed value that cannot be constructed from manifest bytes."""

    route_id: str
    execution_role: str
    capability_fingerprint: str
    source: str = "temporary-synthetic-input-port"


class SyntheticTypedInputPort:
    """Explicit orchestration fake; real canonical admission stays fail-closed."""

    def __init__(self, fingerprints: dict[str, str]) -> None:
        self.fingerprints = dict(fingerprints)
        self.calls: list[tuple[str, str]] = []

    def resolve(self, execution: object) -> SyntheticTypedInputAuthority:
        pair = (execution.role, execution.route_id)
        if pair in self.calls:
            raise AssertionError(f"duplicate synthetic input request: {pair}")
        self.calls.append(pair)
        return SyntheticTypedInputAuthority(
            route_id=execution.route_id,
            execution_role=execution.role,
            capability_fingerprint=self.fingerprints[execution.route_id],
        )


class V0916ParityPipelineTests(V0916ParityTestBase):
    def test_ctrlram_base_kind_is_declared_independently_of_map_name(self) -> None:
        plan = MODULE.load_and_validate_plan(self.plan_path, self.policy_path)
        route_id = (
            "route-7-nt51926-15-ctrlram-replace-4-1-ic-34-"
            "nt51926-ctrlram-fw141-tp-work-240k"
        )
        route = next(row for row in plan.routes if row.route_id == route_id)
        manifest_path = ROOT / "testdata/golden/canonical/manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        manifest["__manifestPath"] = str(manifest_path)
        evidence = next(
            row for row in manifest["routeEvidence"] if row["routeId"] == route_id
        )
        case = MODULE._resolve_case(manifest, evidence["caseId"])

        bindings, recipe = MODULE._ctrlram_artifact_bindings(
            plan,
            route._replace(map_variant="renamed-without-base-kind-signal"),
            case,
        )

        self.assertIsNone(recipe)
        self.assertEqual("replace-base", bindings[0][1])

    def test_required_execution_matrix_is_exactly_53_baseline_and_64_candidate_pairs(self) -> None:
        plan = MODULE.load_and_validate_plan(self.plan_path, self.policy_path)
        matrix = MODULE.build_required_execution_matrix(plan)
        pairs = [(item.role, item.route_id) for item in matrix]
        self.assertEqual(117, len(matrix))
        self.assertEqual(117, len(set(pairs)))
        self.assertEqual(53, sum(role == "baseline-exact" for role, _ in pairs))
        self.assertEqual(53, sum(role == "candidate-exact" for role, _ in pairs))
        self.assertEqual(11, sum(role == "candidate-tp" for role, _ in pairs))
        self.assertEqual(
            {route.route_id for route in plan.routes},
            {route_id for role, route_id in pairs if role.startswith("candidate-")},
        )
        self.assertEqual(
            {route.route_id for route in plan.routes if route.proof_kind == "exact-output"},
            {route_id for role, route_id in pairs if role == "baseline-exact"},
        )

    def test_capture_orchestrator_executes_each_required_pair_once(self) -> None:
        plan = MODULE.load_and_validate_plan(self.plan_path, self.policy_path)
        input_port = SyntheticTypedInputPort(
            {route.route_id: route.capability_fingerprint for route in plan.routes}
        )
        captured: list[tuple[str, str]] = []

        def capture(
            item: object, verified_inputs: SyntheticTypedInputAuthority
        ) -> dict[str, str]:
            pair = (item.role, item.route_id)
            self.assertNotIn(pair, captured)
            self.assertEqual(item.route_id, verified_inputs.route_id)
            self.assertEqual(item.role, verified_inputs.execution_role)
            self.assertEqual(
                "temporary-synthetic-input-port", verified_inputs.source
            )
            captured.append(pair)
            return {"role": item.role, "routeId": item.route_id, "processAuthority": "captured"}

        receipts = MODULE.capture_required_execution_matrix(
            plan,
            canonical_input_port=input_port,
            capture=capture,
        )
        self.assertEqual(117, len(receipts))
        self.assertEqual(117, len(captured))
        self.assertEqual(117, len(set(captured)))
        self.assertEqual(captured, input_port.calls)
        self.assertEqual(53, sum(role == "baseline-exact" for role, _ in captured))
        self.assertEqual(64, sum(role.startswith("candidate-") for role, _ in captured))
        self.assertEqual(captured, [(row["role"], row["routeId"]) for row in receipts])

    def test_run_admission_requires_all_64_governed_case_bindings_before_117_executions(self) -> None:
        plan = MODULE.load_and_validate_plan(self.plan_path, self.policy_path)
        with self.assertRaises(MODULE.ParityError) as missing:
            MODULE.build_required_execution_matrix(
                plan,
                canonical_inputs=MODULE.resolve_all_canonical_route_inputs(
                    plan,
                    ROOT / "testdata/golden/canonical/manifest.json",
                ),
            )
        self.assertEqual("PARITY_FIXTURE_MISSING", missing.exception.code)
        self.assertEqual(
            json.loads(self.plan_path.read_text(encoding="utf-8"))[
                "canonicalInputAuthority"
            ]["currentlyMissingRouteIds"],
            missing.exception.details["routeIds"],
        )

    def test_comparator_drives_existing_cli_preview_then_build_with_exact_paths(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source_root = root / "verified-source"
            source_root.mkdir()
            cli = source_root / "src/NvtFwCombiner.Cli/bin/Release/net10.0/NvtFwCombiner.Cli.dll"
            cli.parent.mkdir(parents=True)
            cli.write_bytes(b"cli")
            plan = MODULE.load_and_validate_plan(self.plan_path, self.policy_path)
            verified_inputs = MODULE.resolve_canonical_route_input(
                plan,
                ROOT / "testdata/golden/canonical/manifest.json",
                admitted_input_root=root / "admitted-inputs",
                route_id=(
                    "route-7-nt51927-14-standard-merge-13-selector-free-27-"
                    "nt51927-standard-merge-256k"
                ),
                execution_role="candidate-exact",
            )
            request = verified_inputs.request
            input_path, tp_path = [
                Path(row["path"]) for row in request["orderedInputs"]
            ]
            self.assertTrue(input_path.is_relative_to(root / "admitted-inputs"))
            self.assertTrue(tp_path.is_relative_to(root / "admitted-inputs"))
            self.assertFalse(input_path.is_relative_to(ROOT / "testdata/golden/canonical"))
            self.assertFalse(tp_path.is_relative_to(ROOT / "testdata/golden/canonical"))
            output_root = root / "outputs"
            output_root.mkdir()
            runner = RecordingCliRunner(
                expected_cwd=source_root,
                sources=[("dp-input", input_path), ("tp-input", tp_path)],
            )
            input_hashes_before = {
                path: hashlib.sha256(path.read_bytes()).hexdigest()
                for path in (input_path, tp_path)
            }
            verified_executor = MODULE.VerifiedSourceExecutor(
                kind="candidate-source-built-cli",
                source_root=source_root,
                source_head="1" * 40,
                source_tree="2" * 40,
                contract_identity_sha256="c" * 64,
                cli_path=cli,
                cli_size=cli.stat().st_size,
                cli_sha256=hashlib.sha256(cli.read_bytes()).hexdigest(),
                argv_prefix=("dotnet", str(cli)),
                fresh_build=True,
            )
            forged_tiny_request = copy.deepcopy(request)
            forged_tiny_request["orderedInputs"][0].update(size=1, sha256="0" * 64)
            with self.assertRaises((TypeError, MODULE.ParityError)) as forged:
                MODULE.execute_cli_capture(
                    forged_tiny_request,
                    verified_executor=verified_executor,
                    output_root=output_root / "forged-tiny",
                    process_runner=runner,
                )
            if isinstance(forged.exception, MODULE.ParityError):
                self.assertEqual("PARITY_PROVENANCE_INVALID", forged.exception.code)
            self.assertEqual([], runner.calls)

            receipt = MODULE.execute_cli_capture(
                verified_inputs, verified_executor=verified_executor,
                output_root=output_root, process_runner=runner,
            )
            self.assertEqual("c" * 64, receipt["executorIdentitySha256"])
            self.assertEqual(2, len(runner.calls))
            preview, build = runner.calls
            self.assertEqual(["dotnet", str(cli), "standard-merge", "preview"], preview[0][:4])
            self.assertEqual(["dotnet", str(cli), "standard-merge", "build"], build[0][:4])
            for call_index, (argv, cwd) in enumerate(runner.calls):
                self.assertEqual(source_root, cwd)
                self.assertEqual("NT51927", argv[argv.index("--profile") + 1])
                staged_dp, staged_dp_hash, staged_dp_read_only = runner.input_observations[call_index]["--dp"]
                staged_tp, staged_tp_hash, staged_tp_read_only = runner.input_observations[call_index]["--tp"]
                self.assertNotEqual(input_path, staged_dp)
                self.assertNotEqual(tp_path, staged_tp)
                capture_root = output_root / verified_inputs.route_id
                self.assertTrue(staged_dp.is_relative_to(capture_root))
                self.assertTrue(staged_tp.is_relative_to(capture_root))
                self.assertEqual(input_hashes_before[input_path], staged_dp_hash)
                self.assertEqual(input_hashes_before[tp_path], staged_tp_hash)
                self.assertTrue(staged_dp_read_only)
                self.assertTrue(staged_tp_read_only)
                self.assertLess(argv.index("--dp"), argv.index("--tp"))
                self.assertTrue(Path(argv[argv.index("--output") + 1]).is_relative_to(output_root))
                self.assertTrue(Path(argv[argv.index("--report") + 1]).is_relative_to(output_root))
            self.assertNotEqual(
                runner.input_observations[0]["--dp"][0],
                runner.input_observations[1]["--dp"][0],
            )
            self.assertNotEqual(
                runner.input_observations[0]["--tp"][0],
                runner.input_observations[1]["--tp"][0],
            )
            self.assertNotEqual(preview[0], build[0])
            self.assertEqual("success", receipt["invocation"]["result"])
            self.assertEqual("success", receipt["authorityInvocation"]["result"])
            self.assertEqual(
                input_hashes_before,
                {
                    path: hashlib.sha256(path.read_bytes()).hexdigest()
                    for path in (input_path, tp_path)
                },
            )

            swapped_projection, captured_roles = build_with_post_capture_swap(
                target_roles={
                    "application-authority-report",
                    "application-report",
                    "output",
                },
                capture=receipt,
                verified_inputs=verified_inputs,
                verified_executor=verified_executor,
                operator_login="dennis40816",
                receipt_root=root / "receipts-post-capture-swap",
                comparator_path=ROOT / "scripts/v0916_parity_certification.py",
            )
            self.assertEqual(
                {
                    "application-authority-report",
                    "application-report",
                    "output",
                },
                captured_roles,
            )
            self.assertEqual(receipt["output"]["sha256"], swapped_projection["output"]["sha256"])

            projected = MODULE.build_process_receipt(
                capture=receipt,
                verified_inputs=verified_inputs,
                verified_executor=verified_executor,
                operator_login="dennis40816",
                receipt_root=root / "receipts",
                comparator_path=ROOT / "scripts/v0916_parity_certification.py",
            )
            self.assertEqual("candidate-exact", projected["role"])
            self.assertEqual("preview", projected["authorityInvocation"]["operation"])
            self.assertEqual("build", projected["invocation"]["operation"])
            self.assertTrue(Path(projected["report"]["path"]).is_file())
            self.assertTrue(Path(projected["__receiptArtifact"]["path"]).is_file())
            for role, reference in (
                ("output", projected["output"]),
                ("report", projected["report"]),
                ("input", projected["inputs"][0]),
            ):
                artifact_path = Path(reference["path"])
                original_payload = artifact_path.read_bytes()
                artifact_path.write_bytes(original_payload + b"post-receipt-tamper")
                with self.subTest(post_receipt_tamper=role):
                    with self.assertRaises(MODULE.ParityError) as tampered:
                        MODULE._reload_receipt_for_evidence(projected)
                    self.assertEqual(
                        "PARITY_PROVENANCE_INVALID", tampered.exception.code
                    )
                artifact_path.write_bytes(original_payload)
            swapped = copy.deepcopy(projected)
            swapped["output"]["path"] = projected["inputs"][0]["path"]
            with self.assertRaises(MODULE.ParityError) as swapped_output:
                MODULE._reload_receipt_for_evidence(swapped)
            self.assertEqual(
                "PARITY_PROVENANCE_INVALID", swapped_output.exception.code
            )
            target_roles = {
                "receipt",
                "input",
                "output",
                "report",
                "application-report",
                "application-authority-report",
            }
            reloaded, captured_roles = reload_with_post_capture_swap(
                projected, target_roles
            )
            self.assertEqual(target_roles, captured_roles)
            self.assertEqual(
                projected["output"]["sha256"],
                hashlib.sha256(reloaded["__outputBytes"]).hexdigest(),
            )

            baseline_inputs = MODULE.resolve_canonical_route_input(
                plan,
                ROOT / "testdata/golden/canonical/manifest.json",
                admitted_input_root=root / "baseline-admitted-inputs",
                route_id=verified_inputs.route_id,
                execution_role="baseline-exact",
            )
            baseline_paths = [
                Path(row["path"]) for row in baseline_inputs.request["orderedInputs"]
            ]
            baseline_runner = RecordingCliRunner(
                expected_cwd=source_root,
                sources=[("dp-input", baseline_paths[0]), ("tp-input", baseline_paths[1])],
            )
            baseline_executor = verified_executor.with_changes(
                kind="exact-tag-source-built-cli",
                contract_identity_sha256=(
                    "861fa0fae7bf5904cac88a4bcb6ed6e0aef1a54518e0903914f2121fbc411bfb"
                ),
            )
            baseline_capture = MODULE.execute_cli_capture(
                baseline_inputs,
                verified_executor=baseline_executor,
                output_root=root / "baseline-outputs",
                process_runner=baseline_runner,
            )
            baseline_projected = MODULE.build_process_receipt(
                capture=baseline_capture,
                verified_inputs=baseline_inputs,
                verified_executor=baseline_executor,
                operator_login="dennis40816",
                receipt_root=root / "receipts",
                comparator_path=ROOT / "scripts/v0916_parity_certification.py",
            )
            route = next(row for row in plan.routes if row.route_id == verified_inputs.route_id)
            evidence = MODULE.build_exact_route_evidence(
                plan=plan,
                route=route,
                baseline_receipt=baseline_projected,
                candidate_receipt=projected,
            )
            self.assertTrue(evidence["equal"])
            self.assertTrue(evidence["passed"])
            self.assertNotIn("path", evidence["scenario"]["orderedInputs"][0])

            with self.assertRaises((TypeError, MODULE.ParityError)) as unverified:
                MODULE.execute_cli_capture(
                    verified_inputs,
                    verified_executor={
                        "cli_path": cli,
                        "cli_sha256": hashlib.sha256(cli.read_bytes()).hexdigest(),
                        "contract_identity_sha256": "c" * 64,
                    },
                    output_root=output_root / "unverified",
                    process_runner=runner,
                )
            if isinstance(unverified.exception, MODULE.ParityError):
                self.assertEqual(
                    "PARITY_AUTHORITY_MISMATCH", unverified.exception.code
                )

            for mutation in ("dll-path", "dll-size", "dll-hash", "argv", "not-fresh"):
                invalid = verified_executor.with_changes(
                    cli_path=(source_root / "other.dll") if mutation == "dll-path" else verified_executor.cli_path,
                    cli_size=999 if mutation == "dll-size" else verified_executor.cli_size,
                    cli_sha256="0" * 64 if mutation == "dll-hash" else verified_executor.cli_sha256,
                    argv_prefix=("dotnet", "other.dll") if mutation == "argv" else verified_executor.argv_prefix,
                    fresh_build=False if mutation == "not-fresh" else verified_executor.fresh_build,
                )
                with self.subTest(executor_mutation=mutation):
                    with self.assertRaises(MODULE.ParityError) as executor_error:
                        MODULE.validate_verified_source_executor(invalid)
                    self.assertEqual("PARITY_AUTHORITY_MISMATCH", executor_error.exception.code)

            malicious_runner = RecordingCliRunner(
                expected_cwd=source_root,
                sources=[("dp-input", input_path), ("tp-input", tp_path)],
                admitted_original_to_mutate_after_preview=input_path,
            )
            admitted_hash = request["orderedInputs"][0]["sha256"]
            with self.assertRaises(MODULE.ParityError) as mutated:
                MODULE.execute_cli_capture(
                    verified_inputs,
                    verified_executor=verified_executor,
                    output_root=output_root / "malicious",
                    process_runner=malicious_runner,
                )
            self.assertEqual("PARITY_INPUT_MUTATED", mutated.exception.code)
            self.assertEqual(1, len(malicious_runner.calls))
            self.assertFalse(
                (output_root / "malicious" / verified_inputs.route_id).exists()
            )
            self.assertNotEqual(
                admitted_hash,
                hashlib.sha256(input_path.read_bytes()).hexdigest(),
            )
            self.assertEqual(1, len(malicious_runner.input_observations))

    def test_comparator_invokes_ab_and_ctrlram_with_typed_cli_arguments_and_profile_binding(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source_root = root / "verified-source"
            cli = source_root / "src/NvtFwCombiner.Cli/bin/Release/net10.0/NvtFwCombiner.Cli.dll"
            cli.parent.mkdir(parents=True)
            cli.write_bytes(b"cli")
            executor = MODULE.VerifiedSourceExecutor(
                kind="candidate-source-built-cli",
                source_root=source_root,
                source_head="1" * 40,
                source_tree="2" * 40,
                contract_identity_sha256="c" * 64,
                cli_path=cli,
                cli_size=cli.stat().st_size,
                cli_sha256=hashlib.sha256(cli.read_bytes()).hexdigest(),
                argv_prefix=("dotnet", str(cli)),
                fresh_build=True,
            )
            plan = MODULE.load_and_validate_plan(self.plan_path, self.policy_path)
            cases = [
                (
                    "route-7-nt51950-8-ab-merge-4-1-ic-21-nt51950-ab-merge-512k",
                    "candidate-exact",
                    "ab-merge",
                    "NT51950",
                    "nt51950-ab-merge",
                    None,
                    ["--dp-ab", "--tp-a", "--tp-b", "--ab-topology", "single"],
                ),
                (
                    "route-7-nt51917-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash",
                    "candidate-exact",
                    "ctrlram-replace",
                    "NT51917",
                    "nt51917-ctrlram-replace-fw141-single",
                    "nt51917-standard-merge-gen-flash-alias",
                    ["--ic-num", "single", "--base", "--ctrlram"],
                ),
                (
                    "route-7-nt51919-8-ab-merge-13-selector-free-21-nt51919-ab-merge-512k",
                    "candidate-exact",
                    "ab-merge",
                    "NT51919",
                    "nt51919-ab-merge-alias",
                    None,
                    ["--dp-ab", "--tp-a", "--tp-b"],
                ),
                (
                    "route-7-nt51926-15-ctrlram-replace-4-1-ic-37-nt51926-ctrlram-fw141-full-flash-256k",
                    "candidate-exact",
                    "ctrlram-replace",
                    "NT51926",
                    "nt51926-ctrlram-replace-fw141-runtime-single",
                    "nt51926-standard-merge-gen-flash",
                    ["--ic-num", "single", "--base", "--ctrlram"],
                ),
                (
                    "route-7-nt51926-15-ctrlram-replace-4-1-ic-34-nt51926-ctrlram-fw141-tp-work-240k",
                    "candidate-exact",
                    "ctrlram-replace",
                    "NT51926",
                    "nt51926-ctrlram-replace-fw141-runtime-single",
                    None,
                    ["--ic-num", "single", "--base", "--ctrlram"],
                ),
                (
                    "route-7-nt51917-15-ctrlram-replace-4-1-ic-39-nt51927-ctrlram-fw141-single-full-flash",
                    "baseline-exact",
                    "ctrlram-replace",
                    "NT51917",
                    "nt51917-ctrlram-replace-fw141-single",
                    "nt51917-standard-merge-gen-flash-alias",
                    ["--ic-num", "single", "--base", "--ctrlram"],
                ),
                (
                    "route-7-nt51917-15-ctrlram-replace-4-1-ic-41-nt51927-ctrlram-fw141-single-tp-work-212k",
                    "candidate-tp",
                    "ctrlram-replace",
                    "NT51917",
                    "nt51917-ctrlram-replace-fw141-single",
                    None,
                    ["--ic-num", "single", "--base", "--ctrlram"],
                ),
            ]
            projected_by_execution: dict[
                tuple[str, str], dict[str, object]
            ] = {}
            for index, (
                route_id,
                execution_role,
                workflow,
                ic_id,
                profile_id,
                standard_profile_id,
                required,
            ) in enumerate(cases):
                verified = MODULE.resolve_canonical_route_input(
                    plan,
                    ROOT / "testdata/golden/canonical/manifest.json",
                    admitted_input_root=root / f"admitted-{index}",
                    route_id=route_id,
                    execution_role=execution_role,
                )
                runner = RecordingCliRunner(
                    expected_cwd=source_root,
                    sources=[
                        (row["slotId"], Path(row["path"]))
                        for row in verified.request["orderedInputs"]
                    ],
                    profile_id=profile_id,
                    standard_merge_profile_id=standard_profile_id,
                    ic_id=ic_id,
                    workflow_id=workflow,
                    map_id=next(
                        route.map_variant
                        for route in plan.routes
                        if route.route_id == route_id
                    ),
                    standard_merge_map_id=(
                        verified.request["baseRecipe"]["mapVariant"]
                        if "baseRecipe" in verified.request
                        else "standard-merge-precursor-map"
                    ),
                )
                route_executor = (
                    executor.with_changes(
                        kind="exact-tag-source-built-cli",
                        contract_identity_sha256=(
                            "861fa0fae7bf5904cac88a4bcb6ed6e0aef1a54518e0903914f2121fbc411bfb"
                        ),
                    )
                    if execution_role == "baseline-exact"
                    else executor
                )
                capture = MODULE.execute_cli_capture(
                    verified,
                    verified_executor=route_executor,
                    output_root=root / f"outputs-{index}",
                    process_runner=runner,
                )
                expected_call_count = 4 if "baseRecipe" in verified.request else 2
                self.assertEqual(expected_call_count, len(runner.calls))
                workflow_calls = [
                    (argv, cwd)
                    for argv, cwd in runner.calls
                    if argv[2] == workflow
                ]
                self.assertEqual(2, len(workflow_calls))
                for argv, _ in workflow_calls:
                    self.assertEqual(workflow, argv[2])
                    for value in required:
                        self.assertIn(value, argv)
                projected = MODULE.build_process_receipt(
                    capture=capture,
                    verified_inputs=verified,
                    verified_executor=route_executor,
                    operator_login="dennis40816",
                    receipt_root=root / f"receipts-{index}",
                    comparator_path=ROOT / "scripts/v0916_parity_certification.py",
                )
                self.assertEqual(profile_id, projected["scenario"]["resolvedProfileId"])
                projected_by_execution[(route_id, execution_role)] = projected
                self.assertNotIn("expectedProfileId", verified.request)
                self.assertEqual(
                    standard_profile_id is not None,
                    "basePrecursor" in projected,
                )
                if "basePrecursor" in projected:
                    proof_path = Path(projected["basePrecursor"]["path"])
                    proof = json.loads(proof_path.read_text(encoding="utf-8"))
                    tamper_paths = [
                        proof_path,
                        Path(proof["sourceInputs"][0]["path"]),
                        Path(proof["sourceInputs"][1]["path"]),
                        Path(proof["applicationReport"]["path"]),
                        Path(proof["output"]["path"]),
                    ]
                    for tamper_index, tamper_path in enumerate(tamper_paths):
                        original_payload = tamper_path.read_bytes()
                        tamper_path.write_bytes(
                            original_payload + b"post-receipt-precursor-tamper"
                        )
                        with self.subTest(base_precursor_tamper=tamper_index):
                            with self.assertRaises(MODULE.ParityError) as tampered:
                                MODULE._reload_receipt_for_evidence(projected)
                            self.assertEqual(
                                "PARITY_PROVENANCE_INVALID",
                                tampered.exception.code,
                            )
                        tamper_path.write_bytes(original_payload)
                    if index == 1:
                        swapped_projection, build_captured_roles = (
                            build_with_post_capture_swap(
                                target_roles={
                                    "application-authority-report",
                                    "application-report",
                                    "output",
                                    "base-precursor-output",
                                    "base-precursor-authority-report",
                                    "base-precursor-application-report",
                                },
                                capture=capture,
                                verified_inputs=verified,
                                verified_executor=route_executor,
                                operator_login="dennis40816",
                                receipt_root=root
                                / "receipts-precursor-post-capture-swap",
                                comparator_path=ROOT
                                / "scripts/v0916_parity_certification.py",
                            )
                        )
                        self.assertEqual(
                            {
                                "application-authority-report",
                                "application-report",
                                "output",
                                "base-precursor-output",
                                "base-precursor-authority-report",
                                "base-precursor-application-report",
                            },
                            build_captured_roles,
                        )
                        self.assertEqual(
                            projected["output"]["sha256"],
                            swapped_projection["output"]["sha256"],
                        )
                        precursor_roles = {
                            "base-precursor-proof",
                            "base-precursor-source",
                            "base-precursor-output",
                            "base-precursor-authority-report",
                            "base-precursor-application-report",
                        }
                        reloaded, captured_roles = reload_with_post_capture_swap(
                            projected, precursor_roles
                        )
                        self.assertEqual(precursor_roles, captured_roles)
                        self.assertEqual(
                            projected["output"]["sha256"],
                            hashlib.sha256(reloaded["__outputBytes"]).hexdigest(),
                        )

            full_route_id = cases[1][0]
            tp_route_id = cases[6][0]
            baseline_full = projected_by_execution[
                (full_route_id, "baseline-exact")
            ]
            candidate_full = projected_by_execution[
                (full_route_id, "candidate-exact")
            ]
            candidate_tp = projected_by_execution[(tp_route_id, "candidate-tp")]
            full_route = next(
                route for route in plan.routes if route.route_id == full_route_id
            )
            tp_route = next(
                route for route in plan.routes if route.route_id == tp_route_id
            )
            full_evidence = MODULE.build_exact_route_evidence(
                plan=plan,
                route=full_route,
                baseline_receipt=baseline_full,
                candidate_receipt=candidate_full,
            )
            full_base_path = Path(candidate_full["inputs"][0]["path"])
            tp_base_path = Path(candidate_tp["inputs"][0]["path"])
            baseline_capture = MODULE._reload_receipt_for_evidence(baseline_full)
            candidate_capture = MODULE._reload_receipt_for_evidence(candidate_full)
            tp_capture = MODULE._reload_receipt_for_evidence(candidate_tp)
            full_payload = b"F" * (tp_route.tp_length + 32)
            tp_payload = full_payload[: tp_route.tp_length]
            baseline_capture["__outputBytes"] = full_payload
            candidate_capture["__outputBytes"] = full_payload
            candidate_capture["__inputBytes"][0] = full_payload
            tp_capture["__outputBytes"] = tp_payload
            tp_capture["__inputBytes"][0] = tp_payload
            captures = {
                id(baseline_full): baseline_capture,
                id(candidate_full): candidate_capture,
                id(candidate_tp): tp_capture,
            }
            originals = {
                full_base_path: full_base_path.read_bytes(),
                tp_base_path: tp_base_path.read_bytes(),
            }
            for path in originals:
                path.write_bytes(b"post-capture-transitive-input-swap")

            try:
                with patch.object(
                    MODULE,
                    "_reload_receipt_for_evidence",
                    side_effect=lambda receipt: copy.deepcopy(captures[id(receipt)]),
                ):
                    transitive = MODULE.build_transitive_route_evidence(
                        route=tp_route,
                        full_route=full_route,
                        full_evidence=full_evidence,
                        baseline_full_receipt=baseline_full,
                        candidate_full_receipt=candidate_full,
                        candidate_tp_receipt=candidate_tp,
                    )
            finally:
                for path, payload in originals.items():
                    path.write_bytes(payload)
            self.assertTrue(transitive["passed"])

            precursor_route_id = cases[1][0]
            precursor_verified = MODULE.resolve_canonical_route_input(
                plan,
                ROOT / "testdata/golden/canonical/manifest.json",
                admitted_input_root=root / "admitted-precursor-map-mismatch",
                route_id=precursor_route_id,
                execution_role="candidate-exact",
            )
            precursor_mismatch_runner = RecordingCliRunner(
                expected_cwd=source_root,
                sources=[
                    (row["slotId"], Path(row["path"]))
                    for row in precursor_verified.request["orderedInputs"]
                ],
                profile_id="nt51917-ctrlram-replace-fw141-single",
                standard_merge_profile_id=(
                    "nt51917-standard-merge-gen-flash-alias"
                ),
                ic_id="NT51917",
                workflow_id="ctrlram-replace",
                map_id=next(
                    route.map_variant
                    for route in plan.routes
                    if route.route_id == precursor_route_id
                ),
                standard_merge_map_id="wrong-standard-merge-map",
            )
            precursor_mismatch_capture = MODULE.execute_cli_capture(
                precursor_verified,
                verified_executor=executor,
                output_root=root / "outputs-precursor-map-mismatch",
                process_runner=precursor_mismatch_runner,
            )
            with self.assertRaises(MODULE.ParityError) as precursor_mismatch:
                MODULE.build_process_receipt(
                    capture=precursor_mismatch_capture,
                    verified_inputs=precursor_verified,
                    verified_executor=executor,
                    operator_login="dennis40816",
                    receipt_root=root / "receipts-precursor-map-mismatch",
                    comparator_path=ROOT / "scripts/v0916_parity_certification.py",
                )
            self.assertEqual(
                "PARITY_PROVENANCE_INVALID", precursor_mismatch.exception.code
            )

            verified = MODULE.resolve_canonical_route_input(
                plan,
                ROOT / "testdata/golden/canonical/manifest.json",
                admitted_input_root=root / "admitted-profile-mismatch",
                route_id=cases[0][0],
                execution_role="candidate-exact",
            )
            mismatch_runner = RecordingCliRunner(
                expected_cwd=source_root,
                sources=[
                    (row["slotId"], Path(row["path"]))
                    for row in verified.request["orderedInputs"]
                ],
                profile_id="nt51950-ab-merge",
                build_profile_id="wrong-profile",
                ic_id="NT51950",
                workflow_id="ab-merge",
                map_id=next(
                    route.map_variant
                    for route in plan.routes
                    if route.route_id == cases[0][0]
                ),
            )
            mismatch_capture = MODULE.execute_cli_capture(
                verified,
                verified_executor=executor,
                output_root=root / "outputs-profile-mismatch",
                process_runner=mismatch_runner,
            )
            with self.assertRaises(MODULE.ParityError) as mismatch:
                MODULE.build_process_receipt(
                    capture=mismatch_capture,
                    verified_inputs=verified,
                    verified_executor=executor,
                    operator_login="dennis40816",
                    receipt_root=root / "receipts-profile-mismatch",
                    comparator_path=ROOT / "scripts/v0916_parity_certification.py",
                )
            self.assertEqual("PARITY_PROVENANCE_INVALID", mismatch.exception.code)

            map_mismatch_runner = RecordingCliRunner(
                expected_cwd=source_root,
                sources=[
                    (row["slotId"], Path(row["path"]))
                    for row in verified.request["orderedInputs"]
                ],
                profile_id="nt51950-ab-merge",
                ic_id="NT51950",
                workflow_id="ab-merge",
                map_id="wrong-map",
            )
            map_mismatch_capture = MODULE.execute_cli_capture(
                verified,
                verified_executor=executor,
                output_root=root / "outputs-map-mismatch",
                process_runner=map_mismatch_runner,
            )
            with self.assertRaises(MODULE.ParityError) as map_mismatch:
                MODULE.build_process_receipt(
                    capture=map_mismatch_capture,
                    verified_inputs=verified,
                    verified_executor=executor,
                    operator_login="dennis40816",
                    receipt_root=root / "receipts-map-mismatch",
                    comparator_path=ROOT / "scripts/v0916_parity_certification.py",
                )
            self.assertEqual(
                "PARITY_PROVENANCE_INVALID", map_mismatch.exception.code
            )

    def test_receipt_without_comparator_process_authority_cannot_certify(self) -> None:
        with self.assertRaises(MODULE.ParityError) as captured:
            MODULE.validate_process_driven_receipt({
                "role": "candidate-exact", "routeId": "route-test",
                "executorIdentitySha256": "a" * 64,
            })
        self.assertEqual("PARITY_PROVENANCE_INVALID", captured.exception.code)


if __name__ == "__main__":
    unittest.main()

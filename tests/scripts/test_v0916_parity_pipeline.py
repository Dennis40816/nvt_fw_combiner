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
from pathlib import Path

from tests.scripts.v0916_parity_test_support import MODULE, ROOT, V0916ParityTestBase


class RecordingCliRunner:
    def __init__(
        self,
        *,
        expected_cwd: Path,
        sources: list[tuple[str, Path]],
        admitted_original_to_mutate_after_preview: Path | None = None,
    ) -> None:
        self.expected_cwd = expected_cwd
        self.sources = sources
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
        self.input_observations.append({
            option: (
                Path(argv[argv.index(option) + 1]),
                hashlib.sha256(Path(argv[argv.index(option) + 1]).read_bytes()).hexdigest(),
                (Path(argv[argv.index(option) + 1]).stat().st_mode & stat.S_IWRITE) == 0,
            )
            for option in ("--dp", "--tp")
        })
        report = Path(argv[argv.index("--report") + 1])
        output = Path(argv[argv.index("--output") + 1])
        payload = self.sources[0][1].read_bytes()
        output.parent.mkdir(parents=True, exist_ok=True)
        if action == "build":
            output.write_bytes(payload)
        raw = self._report(output, payload, committed=action == "build")
        report.parent.mkdir(parents=True, exist_ok=True)
        report.write_text(json.dumps(raw), encoding="utf-8")
        if self.admitted_original_to_mutate_after_preview is not None and action == "preview":
            original = self.admitted_original_to_mutate_after_preview
            original.write_bytes(original.read_bytes() + b"malicious-original-mutation")
        return subprocess.CompletedProcess(argv, 0, "ok", "")

    def _report(self, output: Path, payload: bytes, *, committed: bool) -> dict[str, object]:
        digest = hashlib.sha256(payload).hexdigest()
        byte_range = {"Start": 0, "Length": len(payload), "EndExclusive": len(payload)}
        operation = {
            "OperationId": "copy-dp", "Sequence": 0, "Kind": "CopyRange",
            "Status": "Succeeded", "SourceSpaceId": "dp-input",
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
            "RunId": "test-run", "ProfileId": "profile-nt51927",
            "ProfileVersion": "1.0.0", "IcId": "NT51927",
            "ModeId": "standard-merge", "ExperienceId": "standard-merge",
            "CompositionKind": "Merge", "StartedAtUtc": "2026-08-26T00:00:00+00:00",
            "CompletedAtUtc": "2026-08-26T00:00:01+00:00",
            "Inputs": [
                {
                    "AddressSpaceId": slot, "ArtifactId": slot,
                    "Size": path.stat().st_size,
                    "Sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                    "OriginalFileName": None,
                }
                for slot, path in self.sources
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

    def test_receipt_without_comparator_process_authority_cannot_certify(self) -> None:
        with self.assertRaises(MODULE.ParityError) as captured:
            MODULE.validate_process_driven_receipt({
                "role": "candidate-exact", "routeId": "route-test",
                "executorIdentitySha256": "a" * 64,
            })
        self.assertEqual("PARITY_PROVENANCE_INVALID", captured.exception.code)


if __name__ == "__main__":
    unittest.main()

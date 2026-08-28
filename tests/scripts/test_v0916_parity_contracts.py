"""Behavioral red tests for one v0.9.16 parity concern."""

import copy
import contextlib
import hashlib
import io
import json
import subprocess
import tempfile
import unittest
import zipfile
from unittest import mock
from pathlib import Path

from tests.scripts.v0916_parity_test_support import (
    MODULE,
    MODULE_PATH,
    PRODUCTION_AVAILABLE,
    ROOT,
    V0916ParityTestBase,
    runtime_closure_facts,
)
from scripts.canonical_golden_validation import validate_canonical_golden


class RecordingPinnedCanonicalReader:
    """Reads only Git objects at the declared commit; never reads the worktree."""

    _snapshots: dict[str, tuple[list[str], dict[str, bytes]]] = {}

    def __init__(
        self,
        repository_root: Path,
        overrides: dict[str, bytes] | None = None,
        worktree_overrides: dict[str, bytes] | None = None,
    ) -> None:
        self.repository_root = repository_root
        self.overrides = dict(overrides or {})
        self.worktree_overrides = dict(worktree_overrides or {})
        self.calls: list[tuple[str, str]] = []
        self.inventory_calls: list[str] = []
        self.inventory_results: list[list[str]] = []
        self.worktree_calls: list[str] = []

    def list_files(self, commit: str) -> list[str]:
        self.inventory_calls.append(commit)
        if commit not in self._snapshots:
            raw_inventory = subprocess.check_output(
                ["git", "ls-tree", "-r", "-z", "--name-only", commit],
                cwd=self.repository_root,
            )
            files = [
                row.decode("utf-8") for row in raw_inventory.split(b"\0") if row
            ]
            archive = subprocess.check_output(
                ["git", "archive", "--format=zip", commit],
                cwd=self.repository_root,
            )
            with zipfile.ZipFile(io.BytesIO(archive)) as snapshot:
                payloads = {
                    info.filename: snapshot.read(info)
                    for info in snapshot.infolist()
                    if not info.is_dir()
                }
            if set(files) != set(payloads):
                raise AssertionError("pinned ls-tree and archive inventories differ")
            self._snapshots[commit] = (files, payloads)
        files = list(self._snapshots[commit][0])
        self.inventory_results.append(files)
        return list(files)

    def read_file(self, commit: str, path: str) -> bytes:
        self.calls.append((commit, path))
        if path in self.overrides:
            return self.overrides[path]
        return self._snapshots[commit][1][path]

    def read_worktree_file(self, path: str) -> bytes:
        self.worktree_calls.append(path)
        return self.worktree_overrides[path]


class V0916ParityContractTests(V0916ParityTestBase):
    def test_nt51951_raw_diagnostic_identity_is_checked_without_the_production_loader(self) -> None:
        """Keep the payload-free diagnostic reference valid even while production is red."""
        raw_plan = json.loads(self.plan_path.read_text(encoding="utf-8"))
        self.assertEqual(1, len(raw_plan["approvedSemanticCorrections"]))
        reference = raw_plan["approvedSemanticCorrections"][0]["diagnosticRecord"]
        diagnostic_path = ROOT / reference["path"]
        diagnostic_bytes = diagnostic_path.read_bytes()
        self.assertEqual(3952, len(diagnostic_bytes))
        self.assertEqual(reference["size"], len(diagnostic_bytes))
        self.assertEqual(
            "3c53c257201ef2e1014ed1b13e4d4f9eda7b19306d52a3d7e89f373dd68fec8f",
            hashlib.sha256(diagnostic_bytes).hexdigest(),
        )
        self.assertEqual(reference["sha256"], hashlib.sha256(diagnostic_bytes).hexdigest())

    def test_plan_binds_exact_source_baseline_executor_contract_raw_bytes(self) -> None:
        plan = json.loads(self.plan_path.read_text(encoding="utf-8"))
        executor_path = self.plan_path.parent / "v0916-baseline-executor-v1.json"
        loaded = MODULE.load_and_validate_baseline_executor_contract(plan, executor_path)
        self.assertEqual("exact-tag-source-built-cli", loaded["kind"])
        self.assertEqual("10.0.303", loaded["toolchain"]["resolvedSdkVersion"])
        self.assertEqual(
            "093212528d1048acdb43563ba795353e1ac872f8b6a3aa99251782e958ad1a30",
            loaded["cliAssembly"]["sha256"],
        )
        raw_identity = hashlib.sha256(executor_path.read_bytes()).hexdigest()
        self.assertEqual(
            plan["baseline"]["executorContract"]["sha256"], raw_identity
        )
        self.assertNotEqual(raw_identity, MODULE.canonical_json_sha256(loaded))
        authority = MODULE.load_baseline_executor_authority(plan, executor_path)
        self.assertEqual(raw_identity, authority.identity_sha256)
        self.assertEqual(loaded, authority.contract)
        original_capture = MODULE.capture_local_artifact
        original_payload = executor_path.read_bytes()

        def capture_executor_then_swap(path: Path, role: str):
            captured = original_capture(path, role)
            if role == "baseline-executor-contract":
                path.write_bytes(b"post-capture-baseline-contract-swap")
            return captured

        try:
            with mock.patch.object(
                MODULE,
                "capture_local_artifact",
                side_effect=capture_executor_then_swap,
            ):
                captured_authority = MODULE.load_baseline_executor_authority(
                    plan, executor_path
                )
        finally:
            executor_path.write_bytes(original_payload)
        self.assertEqual(raw_identity, captured_authority.identity_sha256)
        self.assertEqual(loaded, captured_authority.contract)
        for supplied in (MODULE.canonical_json_sha256(loaded), "0" * 64):
            with self.subTest(non_raw_identity=supplied):
                with self.assertRaises(MODULE.ParityError) as identity_error:
                    MODULE.validate_baseline_executor_identity(
                        authority, supplied
                    )
                self.assertEqual(
                    "PARITY_AUTHORITY_MISMATCH", identity_error.exception.code
                )

        with tempfile.TemporaryDirectory() as temporary:
            drifted = Path(temporary) / executor_path.name
            drifted.write_bytes(executor_path.read_bytes() + b"\n")
            with self.assertRaises(MODULE.ParityError) as captured:
                MODULE.load_and_validate_baseline_executor_contract(plan, drifted)
        self.assertEqual("PARITY_AUTHORITY_MISMATCH", captured.exception.code)

    def test_source_baseline_executor_contract_schema_rejects_shape_and_inventory_drift(self) -> None:
        executor_path = self.plan_path.parent / "v0916-baseline-executor-v1.json"
        contract = json.loads(executor_path.read_text(encoding="utf-8"))
        MODULE.validate_baseline_executor_contract_schema(contract)
        for mutation in ("missing-lock", "duplicate-lock", "missing-tool", "extra", "sdk-type", "absolute-path"):
            invalid = copy.deepcopy(contract)
            if mutation == "missing-lock":
                invalid["lockFiles"].pop()
            elif mutation == "duplicate-lock":
                invalid["lockFiles"][-1] = copy.deepcopy(invalid["lockFiles"][0])
            elif mutation == "missing-tool":
                invalid["externalTools"].pop()
            elif mutation == "extra":
                invalid["unexpected"] = True
            elif mutation == "sdk-type":
                invalid["toolchain"]["resolvedSdkVersion"] = 10000303
            else:
                invalid["lockFiles"][0]["path"] = "C:/private/packages.lock.json"
            with self.subTest(mutation=mutation):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.validate_baseline_executor_contract_schema(invalid)
                self.assertEqual("PARITY_PLAN_INVALID", captured.exception.code)

    def test_canonical_plan_selects_exactly_64_routes_with_closed_proof_counts(self) -> None:
        plan = MODULE.load_and_validate_plan(self.plan_path, self.policy_path)
        raw_plan = json.loads(self.plan_path.read_text(encoding="utf-8"))

        self.assertEqual(64, len(plan.routes))
        self.assertEqual(
            {"standard-merge": 14, "ab-merge": 6, "ctrlram-replace": 44},
            plan.workflow_counts,
        )
        self.assertEqual(53, sum(route.proof_kind == "exact-output" for route in plan.routes))
        self.assertEqual(
            11,
            sum(route.proof_kind == "tp-prefix-transitive" for route in plan.routes),
        )
        self.assertTrue(
            {"NT51920", "NT51930", "NT51931"}.isdisjoint(
                route.ic_id for route in plan.routes
            )
        )
        by_id = {route.route_id: route for route in plan.routes}
        for route in plan.routes:
            if route.proof_kind != "tp-prefix-transitive":
                continue
            full = by_id[route.full_route_id]
            self.assertEqual("exact-output", full.proof_kind)
            self.assertEqual(route.full_capability_fingerprint, full.capability_fingerprint)
            self.assertGreater(route.tp_length, 0)
        self.assertEqual(
            "scripts/release_promotion_policy.py",
            raw_plan["candidateAuthority"]["protectedBuild"][
                "candidateVerifierPath"
            ],
        )
        self.assertEqual(
            "scripts/smoke-release.ps1",
            raw_plan["candidateAuthority"]["protectedBuild"][
                "packageVerifierPath"
            ],
        )
        self.assertNotIn("reportComparisonExceptions", raw_plan)
        for binding in raw_plan["canonicalInputAuthority"]["ctrlRamExecutionBindings"]:
            self.assertNotIn("fullBaseArtifactId", binding)
            self.assertEqual("standard-merge", binding["fullBaseRecipe"]["workflowId"])
            self.assertNotIn(
                "expected-output",
                {
                    binding["fullBaseRecipe"]["dpArtifactId"],
                    binding["fullBaseRecipe"]["tpArtifactId"],
                    binding["tpBaseArtifactId"],
                },
            )
        base_routes = raw_plan["canonicalInputAuthority"]["ctrlRamBaseRoutes"]
        self.assertEqual(24, len(base_routes))
        self.assertEqual(24, len({row["routeId"] for row in base_routes}))
        self.assertEqual(
            {"tp-input", "standard-merge"},
            {row["kind"] for row in base_routes},
        )
        by_id = {route.route_id: route for route in plan.routes}
        for base_route in base_routes:
            route = by_id[base_route["routeId"]]
            self.assertEqual(
                route.capability_fingerprint,
                base_route["capabilityFingerprint"],
            )
            if base_route["kind"] == "standard-merge":
                standard = by_id[base_route["standardMergeRouteId"]]
                self.assertEqual("standard-merge", standard.workflow_id)
                self.assertEqual(route.ic_id, standard.ic_id)
                self.assertEqual(
                    standard.capability_fingerprint,
                    base_route["standardMergeCapabilityFingerprint"],
                )
                self.assertEqual(
                    standard.map_variant,
                    base_route["standardMergeMapVariant"],
                )
        alias = raw_plan["inputIdentityAliases"][0]
        self.assertEqual(
            "route-7-nt51928-14-standard-merge-13-selector-free-31-nt51928-dual-capacity-256k-512k",
            alias["routeId"],
        )
        self.assertEqual(
            ("ldc", "ld-input", "ldc-input"),
            (alias["logicalInputId"], alias["baselineInputSlotId"], alias["candidateInputSlotId"]),
        )

    def test_canonical_input_authority_is_raw_pinned_and_exposes_all_27_missing_bindings(self) -> None:
        canonical_errors: list[str] = []
        validate_canonical_golden(ROOT, canonical_errors)
        self.assertEqual([], canonical_errors)
        raw_plan = json.loads(self.plan_path.read_text(encoding="utf-8"))
        authority = raw_plan["canonicalInputAuthority"]
        manifest_path = ROOT / authority["manifestPath"]
        manifest_bytes = manifest_path.read_bytes()
        self.assertEqual(authority["manifestSize"], len(manifest_bytes))
        self.assertEqual(
            authority["manifestRawSha256"], hashlib.sha256(manifest_bytes).hexdigest()
        )
        self.assertEqual(
            authority["canonicalRootTree"],
            subprocess.check_output(
                [
                    "git",
                    "rev-parse",
                    f"{authority['repositoryCommit']}:testdata/golden/canonical",
                ],
                cwd=ROOT,
                text=True,
            ).strip(),
        )
        self.assertEqual(
            authority["manifestBlob"],
            subprocess.check_output(
                ["git", "hash-object", str(manifest_path)], cwd=ROOT, text=True
            ).strip(),
        )

        policy = json.loads(self.policy_path.read_text(encoding="utf-8"))
        selected = [
            route
            for route in policy["routes"]
            if route["workflowId"] in {"standard-merge", "ab-merge", "ctrlram-replace"}
            and route["authoring"]["value"] == "available"
            and route["publication"]["value"] == "supported"
        ]
        manifest = json.loads(manifest_bytes)
        evidence_rows = manifest["routeEvidence"]
        evidence_route_ids = [row["routeId"] for row in evidence_rows]
        self.assertEqual(len(evidence_route_ids), len(set(evidence_route_ids)))
        route_evidence = {row["routeId"]: row for row in evidence_rows}
        self.assertEqual(len(selected), len({row["routeId"] for row in selected}))
        self.assertTrue({row["routeId"] for row in selected} <= set(route_evidence))
        for route in selected:
            evidence = route_evidence[route["routeId"]]
            self.assertEqual(
                route["capabilityFingerprint"], evidence["capabilityFingerprint"]
            )
        missing = [
            row["routeId"]
            for row in selected
            if "caseId" not in route_evidence[row["routeId"]]
        ]
        self.assertEqual(authority["currentlyMissingRouteIds"], missing)
        self.assertEqual(authority["currentlyMissingRouteCount"], len(missing))
        self.assertEqual(
            authority["currentlyBoundRouteCount"], len(selected) - len(missing)
        )
        self.assertEqual(64, authority["requiredRouteCount"])
        self.assertEqual("PARITY_FIXTURE_MISSING", authority["missingApplicableFixtureFailureCode"])
        self.assertTrue(
            {"NT51920", "NT51930", "NT51931"}.isdisjoint(
                route_id.split("-")[2].upper() for route_id in missing
            )
        )

        canonical_root = manifest_path.parent
        cases = {row["caseId"]: row for row in manifest["cases"]}
        for route in selected:
            evidence = route_evidence[route["routeId"]]
            if "caseId" not in evidence:
                continue
            case_index = cases[evidence["caseId"]]
            case_path = canonical_root / case_index["manifestPath"]
            case = json.loads(case_path.read_text(encoding="utf-8"))
            self.assertEqual(evidence["caseId"], case["caseId"])
            while "alias" in case:
                source_id = case["alias"]["sourceCaseId"]
                source_index = cases[source_id]
                case_path = canonical_root / source_index["manifestPath"]
                case = json.loads(case_path.read_text(encoding="utf-8"))
                self.assertEqual(source_id, case["caseId"])
            inputs = [item for item in case["artifacts"] if item["role"] == "input"]
            self.assertGreater(len(inputs), 0)
            for item in inputs:
                payload = canonical_root / item["path"]
                self.assertEqual(item["size"], payload.stat().st_size)
                self.assertEqual(
                    item["sha256"], hashlib.sha256(payload.read_bytes()).hexdigest()
                )

    def test_pinned_canonical_materialization_ignores_worktree_drift_and_reuses_validator(self) -> None:
        raw_plan = json.loads(self.plan_path.read_text(encoding="utf-8"))
        authority = raw_plan["canonicalInputAuthority"]
        # Keep the host-created Windows snapshot root short enough for the
        # existing canonical validator's longest governed artifact path.
        with tempfile.TemporaryDirectory(dir=ROOT.parent) as temporary:
            root = Path(temporary)
            drifted_worktree = root / "drifted-worktree"
            drifted_manifest = drifted_worktree / authority["manifestPath"]
            drifted_manifest.parent.mkdir(parents=True)
            pinned_manifest = (ROOT / authority["manifestPath"]).read_bytes()
            drifted_manifest.write_bytes(pinned_manifest.replace(b'"schemaVersion": "1.1"', b'"schemaVersion": "drifted"'))

            reader = RecordingPinnedCanonicalReader(
                ROOT,
                worktree_overrides={authority["manifestPath"]: drifted_manifest.read_bytes()},
            )
            destination = root / "pinned-authority"
            with mock.patch.object(
                MODULE,
                "_validate_canonical_golden",
                wraps=validate_canonical_golden,
                create=True,
            ) as validator_spy:
                materialized = MODULE.materialize_and_validate_canonical_input_authority(
                    raw_plan,
                    git_reader=reader,
                    destination=destination,
                )
            validator_spy.assert_called_once()
            self.assertEqual(
                Path(MODULE._native_path(destination)), validator_spy.call_args.args[0]
            )
            self.assertEqual(authority["manifestRawSha256"], materialized.manifest_sha256)
            self.assertNotEqual(
                materialized.manifest_sha256,
                hashlib.sha256(drifted_manifest.read_bytes()).hexdigest(),
            )
            self.assertEqual([authority["repositoryCommit"]], reader.inventory_calls)
            inventory = reader.inventory_results[0]
            self.assertEqual(inventory, [path for _, path in reader.calls])
            self.assertEqual(
                set(inventory),
                {
                    path.relative_to(destination).as_posix()
                    for path in destination.rglob("*")
                    if path.is_file()
                },
            )
            self.assertTrue(all(commit == authority["repositoryCommit"] for commit, _ in reader.calls))
            self.assertEqual([], reader.worktree_calls)

            manifest_from_reader = reader.read_file(
                authority["repositoryCommit"], authority["manifestPath"]
            )
            self.assertEqual(pinned_manifest, manifest_from_reader)
            self.assertNotEqual(drifted_manifest.read_bytes(), manifest_from_reader)

    def test_pinned_snapshot_is_bounded_blob_only_and_cleans_every_failure(self) -> None:
        raw_plan = json.loads(self.plan_path.read_text(encoding="utf-8"))

        class BoundedReader:
            def __init__(self, mode: str = "100644", kind: str = "blob") -> None:
                self.mode = mode
                self.kind = kind

            def list_files(self, _commit: str) -> list[str]:
                return ["snapshot-entry.json"]

            def entry(self, _path: str) -> tuple[str, str, str]:
                return self.mode, self.kind, "0" * 40

            def read_file(self, _commit: str, _path: str) -> bytes:
                return b"{}"

        with tempfile.TemporaryDirectory(dir=ROOT.parent) as temporary:
            root = Path(temporary)
            for label, reader in (
                ("symlink", BoundedReader("120000", "blob")),
                ("gitlink", BoundedReader("160000", "commit")),
                ("non-blob", BoundedReader("100644", "tree")),
            ):
                with self.subTest(label=label):
                    destination = root / label
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.materialize_and_validate_canonical_input_authority(
                            raw_plan, git_reader=reader, destination=destination
                        )
                    self.assertEqual("PARITY_AUTHORITY_MISMATCH", captured.exception.code)
                    self.assertFalse(destination.exists())

            for label, patch_name, limit in (
                ("file-count", "MAX_SNAPSHOT_FILES", 0),
                ("byte-count", "MAX_SNAPSHOT_BYTES", 1),
            ):
                destination = root / label
                with self.subTest(label=label), mock.patch.object(MODULE, patch_name, limit):
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.materialize_and_validate_canonical_input_authority(
                            raw_plan,
                            git_reader=BoundedReader(),
                            destination=destination,
                        )
                    self.assertEqual("PARITY_AUTHORITY_MISMATCH", captured.exception.code)
                    self.assertFalse(destination.exists())

            destination = root / "validator-failure"
            with mock.patch.object(
                MODULE,
                "_validate_canonical_golden",
                side_effect=RuntimeError("validator interrupted"),
            ):
                with self.assertRaisesRegex(RuntimeError, "validator interrupted"):
                    MODULE.materialize_and_validate_canonical_input_authority(
                        raw_plan,
                        git_reader=BoundedReader(),
                        destination=destination,
                    )
            self.assertFalse(destination.exists())

    def test_pinned_alias_case_drift_is_rejected_by_existing_canonical_validator(self) -> None:
        raw_plan = json.loads(self.plan_path.read_text(encoding="utf-8"))
        authority = raw_plan["canonicalInputAuthority"]
        alias_path = (
            "testdata/golden/canonical/NT51917/standard-merge/gen-flash/"
            "topology-unscoped/nt51917-standard-merge-gen-flash-alias/"
            "provenance/case.json"
        )
        second_alias_id = "nt51919-standard-merge-gen-flash-alias"
        pinned_alias = json.loads(
            subprocess.check_output(
                ["git", "show", f"{authority['repositoryCommit']}:{alias_path}"],
                cwd=ROOT,
                text=True,
            )
        )
        manifest_bytes = subprocess.check_output(
            [
                "git",
                "show",
                f"{authority['repositoryCommit']}:{authority['manifestPath']}",
            ],
            cwd=ROOT,
        )
        if not PRODUCTION_AVAILABLE:
            with tempfile.TemporaryDirectory(dir=ROOT.parent) as temporary:
                reader = RecordingPinnedCanonicalReader(ROOT, {alias_path: b"{}"})
                MODULE.materialize_and_validate_canonical_input_authority(
                    raw_plan,
                    git_reader=reader,
                    destination=Path(temporary) / "missing-production",
                )
        for mutation in ("cycle", "chain", "case-substitution"):
            invalid_alias = copy.deepcopy(pinned_alias)
            if mutation == "cycle":
                invalid_alias["alias"]["sourceCaseId"] = invalid_alias["caseId"]
            elif mutation == "chain":
                invalid_alias["alias"]["sourceCaseId"] = second_alias_id
            else:
                invalid_alias["caseId"] = second_alias_id
            invalid_bytes = (
                json.dumps(invalid_alias, indent=2, ensure_ascii=False) + "\n"
            ).encode("utf-8")
            reader = RecordingPinnedCanonicalReader(ROOT, {alias_path: invalid_bytes})
            with tempfile.TemporaryDirectory(dir=ROOT.parent) as temporary:
                destination = Path(temporary) / f"rejected-{mutation}"
                with mock.patch.object(
                    MODULE,
                    "_validate_canonical_golden",
                    wraps=validate_canonical_golden,
                    create=True,
                ) as validator_spy:
                    with self.subTest(alias_mutation=mutation):
                        with self.assertRaises(MODULE.ParityError) as captured:
                            MODULE.materialize_and_validate_canonical_input_authority(
                                raw_plan,
                                git_reader=reader,
                                destination=destination,
                            )
                        self.assertEqual(
                            "PARITY_AUTHORITY_MISMATCH", captured.exception.code
                        )
                validator_spy.assert_called_once()
                self.assertEqual(
                    Path(MODULE._native_path(destination)), validator_spy.call_args.args[0]
                )
                self.assertEqual(
                    manifest_bytes,
                    reader.read_file(
                        authority["repositoryCommit"], authority["manifestPath"]
                    ),
                )

    def test_execution_fails_closed_until_every_route_has_canonical_input_authority(self) -> None:
        plan = MODULE.load_and_validate_plan(self.plan_path, self.policy_path)
        with self.assertRaises(MODULE.ParityError) as captured:
            MODULE.resolve_all_canonical_route_inputs(
                plan,
                MODULE.capture_canonical_authority_from_manifest_for_test(
                    ROOT / "testdata/golden/canonical/manifest.json"
                ),
            )
        self.assertEqual("PARITY_FIXTURE_MISSING", captured.exception.code)
        self.assertEqual(
            json.loads(self.plan_path.read_text(encoding="utf-8"))[
                "canonicalInputAuthority"
            ]["currentlyMissingRouteIds"],
            captured.exception.details["routeIds"],
        )

    def test_compare_command_uses_pinned_snapshot_and_leaves_no_authority_copy(self) -> None:
        before = {
            path.resolve()
            for path in Path(tempfile.gettempdir()).glob("nfc-v0916-authority-*")
        }
        with tempfile.TemporaryDirectory(dir=ROOT.parent) as temporary:
            output_root = Path(temporary) / "comparison-output"
            with self.assertRaises(MODULE.ParityError) as captured:
                MODULE.main(
                    [
                        "compare",
                        "--plan",
                        str(self.plan_path),
                        "--candidate-artifact-dir",
                        str(Path(temporary) / "candidate-assets"),
                        "--output-root",
                        str(output_root),
                    ]
                )
            self.assertEqual("PARITY_FIXTURE_MISSING", captured.exception.code)
            self.assertTrue(output_root.is_dir())
            self.assertEqual([], list(output_root.iterdir()))
        after = {
            path.resolve()
            for path in Path(tempfile.gettempdir()).glob("nfc-v0916-authority-*")
        }
        self.assertEqual(before, after)

    def test_compare_command_has_a_complete_117_execution_success_path(self) -> None:
        plan = MODULE.load_and_validate_plan(self.plan_path, self.policy_path)
        candidate_contract = MODULE.load_and_validate_candidate_source_executor_contract(
            ROOT / "docs/contracts/v100-candidate-source-executor-v1.json",
            plan.raw["candidateAuthority"]["sourceExecutorContract"],
        )
        candidate_identity = candidate_contract.identity_sha256
        source = candidate_contract.contract["source"]

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            cli = root / "cli.exe"
            cli.write_bytes(b"cli")
            runtime_sha, runtime_count, runtime_size = runtime_closure_facts(root)
            baseline_executor = MODULE.VerifiedSourceExecutor(
                "exact-tag-source-built-cli",
                root,
                plan.raw["baseline"]["peeledCommit"],
                plan.raw["baseline"]["sourceTree"],
                "92e400212b5cdbb5e164b4d1401d59cdd1adbb0aef9a490be4777554d5b1e659",
                cli,
                162304,
                "093212528d1048acdb43563ba795353e1ac872f8b6a3aa99251782e958ad1a30",
                (str(cli),),
                True,
                runtime_sha,
                runtime_count,
                runtime_size,
            )
            candidate_executor = baseline_executor.with_changes(
                kind="candidate-source-built-cli",
                source_head=source["implementationHead"],
                source_tree=source["implementationTree"],
                contract_identity_sha256=candidate_identity,
            )
            workflow_contract = ROOT / "docs/contracts/v0916-parity-workflow-v1.json"
            workflow_contract_sha = hashlib.sha256(workflow_contract.read_bytes()).hexdigest()
            package_source_head = "5" * 40
            package_source_tree = "4" * 40
            declared = {
                "repository": "Dennis40816/nvt_fw_combiner",
                "workflowPath": ".github/workflows/release.yml",
                "workflowRef": "refs/heads/main",
                "workflowCommitSha": "5" * 40,
                "workflowBlobSha": "6" * 40,
                "workflowRawSha256": "7" * 64,
                "workflowSemanticContractSha256": workflow_contract_sha,
                "runId": 123,
                "artifactId": 456,
                "artifactName": f"stable-candidate-123-{package_source_head}",
                "artifactDigest": "sha256:" + "8" * 64,
                "candidateManifest": {"size": 10, "sha256": "9" * 64},
                "candidateSbom": {"size": 10, "sha256": "a" * 64},
                "candidateProvenance": {"size": 10, "sha256": "b" * 64},
                "releaseNotes": {"size": 10, "sha256": "c" * 64},
                "assetChecksums": {"size": 10, "sha256": "d" * 64},
                "candidateSourceExecutorIdentitySha256": candidate_identity,
                "provenanceSubjectsSha256": "e" * 64,
                "candidateVerifierSha256": "f" * 64,
                "packageVerifierSha256": "0" * 64,
            }
            package = {
                "name": "NvtFwCombiner-v1.0.0-win-x64.zip",
                "size": 100,
                "sha256": "1" * 64,
                "version": "1.0.0",
                "sourceCommit": package_source_head,
            }
            artifact_run = {
                "id": 123,
                "headSha": "5" * 40,
                "headBranch": "main",
                "repository": "Dennis40816/nvt_fw_combiner",
                "repositoryId": 40816,
                "headRepositoryId": 40816,
            }
            exact_index = 0
            transitive_index = 0

            def exact_row(**kwargs: object) -> dict[str, object]:
                nonlocal exact_index
                route = kwargs["route"]
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
                correction = next(
                    (
                        item
                        for item in plan.raw["approvedSemanticCorrections"]
                        if item["routeId"] == route.route_id
                    ),
                    None,
                )
                if correction is not None:
                    row["proofKind"] = correction["requiredProofKind"]
                    row["baselineOutput"] = copy.deepcopy(
                        correction["baselineOutput"]
                    )
                    row["candidateOutput"] = copy.deepcopy(
                        correction["candidateOutput"]
                    )
                    row["equal"] = False
                    row["differenceValidation"] = {
                        "kind": correction["kind"],
                        "ownerDecision": correction["ownerDecision"],
                        "differentByteCount": correction["differentByteCount"],
                        "differentRanges": copy.deepcopy(
                            correction["differentRanges"]
                        ),
                    }
                minimum_full_capacity = 1 + max(
                    (
                        item.tp_length
                        for item in plan.routes
                        if item.full_route_id == route.route_id
                        and item.tp_length is not None
                    ),
                    default=0,
                )
                if correction is None:
                    row["baselineOutput"]["size"] = max(
                        row["baselineOutput"]["size"], minimum_full_capacity
                    )
                    row["candidateOutput"]["size"] = row["baselineOutput"]["size"]
                row["scenario"]["outputCapacity"] = row["baselineOutput"]["size"]
                row["scenario"]["orderedInputs"][0]["size"] = row[
                    "scenario"
                ]["outputCapacity"]
                for receipt_index, receipt in enumerate(row["receipts"]):
                    receipt["receiptSha256"] = hashlib.sha256(
                        f"exact-receipt-{exact_index}-{receipt_index}".encode()
                    ).hexdigest()
                    receipt["invocationSha256"] = hashlib.sha256(
                        f"exact-invocation-{exact_index}-{receipt_index}".encode()
                    ).hexdigest()
                row["receipts"][1]["executorIdentitySha256"] = candidate_identity
                exact_index += 1
                MODULE.validate_exact_evidence_row_schema(row)
                return row

            def transitive_row(**kwargs: object) -> dict[str, object]:
                nonlocal transitive_index
                route = kwargs["route"]
                full_route = kwargs["full_route"]
                full_evidence = kwargs["full_evidence"]
                row = copy.deepcopy(
                    self.schema_transitive_evidence_row(
                        MODULE.canonical_route_row_sha256(full_evidence)
                    )
                )
                row["routeId"] = route.route_id
                row["capabilityFingerprint"] = route.capability_fingerprint
                row["fullEvidence"].update(
                    routeId=full_route.route_id,
                    capabilityFingerprint=full_route.capability_fingerprint,
                )
                row["tpLength"] = route.tp_length
                row["tpScenario"].update(
                    icId=route.ic_id,
                    workflowId=route.workflow_id,
                    icCountVariant=route.ic_count_variant,
                    mapVariant=route.map_variant,
                    selectionToken="fixture-tp",
                    outputCapacity=route.tp_length,
                )
                row["candidateCompilationFingerprint"] = route.capability_fingerprint
                row["candidateTpOutput"]["size"] = route.tp_length
                row["candidateFullInput"] = {
                    key: full_evidence["scenario"]["orderedInputs"][0][key]
                    for key in ("size", "sha256")
                }
                row["receipts"][0]["executorIdentitySha256"] = candidate_identity
                row["receipts"][0]["receiptSha256"] = hashlib.sha256(
                    f"tp-receipt-{transitive_index}".encode()
                ).hexdigest()
                row["receipts"][0]["invocationSha256"] = hashlib.sha256(
                    f"tp-invocation-{transitive_index}".encode()
                ).hexdigest()
                transitive_index += 1
                MODULE.validate_transitive_evidence_reference(full_evidence, row)
                return row

            def resolved_input(
                _plan: object,
                _manifest: object,
                *,
                admitted_input_root: Path,
                route_id: str,
                execution_role: str,
            ) -> object:
                route = next(row for row in plan.routes if row.route_id == route_id)
                return MODULE.VerifiedCanonicalInputs(
                    route_id,
                    execution_role,
                    route.capability_fingerprint,
                    {
                        "routeId": route_id,
                        "executionRole": execution_role,
                        "capabilityFingerprint": route.capability_fingerprint,
                    },
                )

            def projected_receipt(**kwargs: object) -> dict[str, object]:
                verified = kwargs["verified_inputs"]
                executor = kwargs["verified_executor"]
                return {
                    "role": verified.execution_role,
                    "routeId": verified.route_id,
                    "executorIdentitySha256": executor.contract_identity_sha256,
                }

            output_root = root / "comparison"
            with (
                mock.patch.dict(
                    MODULE.os.environ,
                    {
                        "GITHUB_ACTOR": "dennis40816",
                        "GITHUB_REPOSITORY": "Dennis40816/nvt_fw_combiner",
                        "GITHUB_WORKFLOW_SHA": "5" * 40,
                        "GITHUB_RUN_ID": "123",
                    },
                ),
                mock.patch.object(MODULE, "materialize_and_validate_canonical_input_authority"),
                mock.patch.object(
                    MODULE,
                    "resolve_all_canonical_route_inputs",
                    return_value={row.route_id: {} for row in plan.routes},
                ),
                mock.patch.object(
                    MODULE,
                    "discover_candidate_build_declaration",
                    return_value=(
                        {},
                        declared,
                        {
                            "manifest": {},
                            "package": package,
                            "packageSourceHead": package_source_head,
                            "packageSourceTree": package_source_tree,
                        },
                    ),
                ),
                mock.patch.object(
                    MODULE,
                    "verify_protected_candidate_build",
                    return_value={"artifactWorkflowRun": artifact_run, "passed": True},
                ),
                mock.patch.object(
                    MODULE,
                    "validate_repository_parity_authority_transfer",
                    return_value={
                        "implementationHead": source["implementationHead"],
                        "bindingHead": package_source_head,
                    },
                ),
                mock.patch.object(
                    MODULE,
                    "detached_git_worktree",
                    side_effect=lambda *args, **kwargs: contextlib.nullcontext(root),
                ),
                mock.patch.object(
                    MODULE,
                    "verify_source_baseline_executor",
                    return_value=baseline_executor,
                ),
                mock.patch.object(
                    MODULE,
                    "verify_candidate_source_executor",
                    return_value=candidate_executor,
                ),
                mock.patch.object(
                    MODULE,
                    "resolve_canonical_route_input",
                    side_effect=resolved_input,
                ),
                mock.patch.object(
                    MODULE,
                    "execute_cli_capture",
                    return_value={"processDriven": True},
                ) as execute,
                mock.patch.object(
                    MODULE,
                    "build_process_receipt",
                    side_effect=projected_receipt,
                ),
                mock.patch.object(
                    MODULE, "build_exact_route_evidence", side_effect=exact_row
                ) as exact,
                mock.patch.object(
                    MODULE,
                    "build_transitive_route_evidence",
                    side_effect=transitive_row,
                ) as transitive,
            ):
                result = MODULE.main(
                    [
                        "compare",
                        "--plan",
                        str(self.plan_path),
                        "--candidate-artifact-dir",
                        str(root / "candidate"),
                        "--output-root",
                        str(output_root),
                    ]
                )

            self.assertEqual(0, result)
            self.assertEqual(117, execute.call_count)
            self.assertEqual(53, exact.call_count)
            self.assertEqual(11, transitive.call_count)
            comparison = json.loads(
                (output_root / "comparison.json").read_text(encoding="utf-8")
            )
            MODULE.validate_comparison_schema(comparison)
            self.assertEqual("provisional", comparison["verdict"])

    def test_policy_byte_drift_fails_with_stable_code(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            policy = Path(temporary) / "policy.json"
            policy.write_bytes(self.policy_path.read_bytes() + b"\n")

            with self.assertRaises(MODULE.ParityError) as captured:
                MODULE.load_and_validate_plan(self.plan_path, policy)

        self.assertEqual("PARITY_POLICY_DRIFT", captured.exception.code)

    def test_policy_parse_uses_the_same_bytes_as_its_hash_binding(self) -> None:
        original_capture = MODULE.capture_local_artifact
        original_policy = self.policy_path.read_bytes()

        def capture_policy_then_swap(path: Path, role: str):
            captured = original_capture(path, role)
            if role == "capability-policy":
                path.write_bytes(b"post-capture-policy-swap")
            return captured

        try:
            with mock.patch.object(
                MODULE,
                "capture_local_artifact",
                side_effect=capture_policy_then_swap,
            ):
                plan = MODULE.load_and_validate_plan(
                    self.plan_path, self.policy_path
                )
        finally:
            self.policy_path.write_bytes(original_policy)
        self.assertEqual(64, len(plan.routes))

    def test_missing_or_reclassified_transitive_route_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            plan = json.loads(self.plan_path.read_text(encoding="utf-8"))
            plan["transitiveRoutes"].pop()
            path = Path(temporary) / "plan.json"
            path.write_text(json.dumps(plan), encoding="utf-8")

            with self.assertRaises(MODULE.ParityError) as captured:
                MODULE.load_and_validate_plan(path, self.policy_path)

        self.assertEqual("PARITY_PLAN_INVALID", captured.exception.code)

    def test_nt51928_input_identity_alias_is_one_closed_version_specific_exception(self) -> None:
        for mutation in (
            "missing",
            "route",
            "fingerprint",
            "logical-id",
            "baseline-option",
            "candidate-option",
            "baseline-slot",
            "candidate-slot",
            "extra",
        ):
            with tempfile.TemporaryDirectory() as temporary:
                plan = json.loads(self.plan_path.read_text(encoding="utf-8"))
                exception = plan["inputIdentityAliases"][0]
                if mutation == "missing":
                    plan["inputIdentityAliases"].pop(0)
                elif mutation == "route":
                    exception["routeId"] = "route-other"
                elif mutation == "fingerprint":
                    exception["capabilityFingerprint"] = "f" * 64
                elif mutation == "logical-id":
                    exception["logicalInputId"] = "initial-code"
                elif mutation == "baseline-option":
                    exception["baselineInvocationOption"] = "--ldc"
                elif mutation == "candidate-option":
                    exception["candidateInvocationOption"] = "--ld"
                elif mutation == "baseline-slot":
                    exception["baselineInputSlotId"] = "ldc-input"
                elif mutation == "candidate-slot":
                    exception["candidateInputSlotId"] = "ld-input"
                else:
                    exception["unexpected"] = True
                path = Path(temporary) / "plan.json"
                path.write_text(json.dumps(plan), encoding="utf-8")
                with self.subTest(mutation=mutation):
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.load_and_validate_plan(path, self.policy_path)
                    self.assertEqual("PARITY_PLAN_INVALID", captured.exception.code)

    def test_exact_comparison_hashes_complete_bytes_and_rejects_one_byte_drift(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            baseline = root / "baseline.bin"
            candidate = root / "candidate.bin"
            baseline.write_bytes(bytes(range(32)))
            candidate.write_bytes(baseline.read_bytes())

            evidence = MODULE.compare_exact_files(baseline, candidate)
            self.assertTrue(evidence["equal"])
            self.assertEqual(32, evidence["baselineOutput"]["size"])
            self.assertEqual(
                evidence["baselineOutput"]["sha256"],
                evidence["candidateOutput"]["sha256"],
            )

            candidate.write_bytes(bytes(range(31)) + b"\xff")
            with self.assertRaises(MODULE.ParityError) as captured:
                MODULE.compare_exact_files(baseline, candidate)

        self.assertEqual("PARITY_EXACT_MISMATCH", captured.exception.code)

    def test_nt51951_cascade2_diff_nf_preservation_is_exactly_bounded_owner_approved_correction(self) -> None:
        plan = MODULE.load_and_validate_plan(self.plan_path, self.policy_path)
        route_id = (
            "route-7-nt51951-15-ctrlram-replace-4-2-ic-39-"
            "nt51951-ctrlram-fw1x-cascade-full-flash"
        )
        route = next(route for route in plan.routes if route.route_id == route_id)
        self.assertEqual("exact-output", route.proof_kind)
        raw_plan = json.loads(self.plan_path.read_text(encoding="utf-8"))
        corrections = raw_plan["approvedSemanticCorrections"]
        self.assertEqual(1, len(corrections))
        correction = corrections[0]
        self.assertEqual(route_id, correction["routeId"])
        self.assertEqual(
            "owner-approved-diff-nf-preservation", correction["kind"]
        )
        self.assertEqual(
            "owner-decision:2026-08-28:nt51951-diff-nf-preservation-is-correct",
            correction["ownerDecision"],
        )
        self.assertEqual(route.capability_fingerprint, correction["capabilityFingerprint"])
        self.assertEqual(524288, correction["baselineOutput"]["size"])
        self.assertEqual(524288, correction["candidateOutput"]["size"])
        self.assertEqual(
            "7d657a3d0abc2cc6779e759c17567b40740de95235bf6d1e71c147d815edcca2",
            correction["baselineOutput"]["sha256"],
        )
        self.assertEqual(
            "1536d344af83aafd29e5884d9d2d904f1efa03c8fdcc4e913832253814644ebd",
            correction["candidateOutput"]["sha256"],
        )
        self.assertEqual(2816, correction["differentByteCount"])
        self.assertEqual(
            2816,
            sum(
                item["endExclusive"] - item["start"]
                for item in correction["differentRanges"]
            ),
        )
        self.assertEqual(
            "exact-output-with-approved-semantic-correction",
            correction["requiredProofKind"],
        )
        self.assertEqual(
            "92e400212b5cdbb5e164b4d1401d59cdd1adbb0aef9a490be4777554d5b1e659",
            correction["baselineProvenance"]["executorContractSha256"],
        )
        case_manifest = correction["candidateProvenance"]["canonicalCaseManifest"]
        case_path = ROOT / case_manifest["path"]
        self.assertEqual(case_path.stat().st_size, case_manifest["size"])
        self.assertEqual(
            hashlib.sha256(case_path.read_bytes()).hexdigest(),
            case_manifest["sha256"],
        )
        MODULE.validate_approved_semantic_correction(correction, route)
        self.assertTrue(
            all(alias["routeId"] != route_id for alias in raw_plan["inputIdentityAliases"])
        )
        with tempfile.TemporaryDirectory() as temporary:
            baseline = Path(temporary) / "baseline.bin"
            candidate = Path(temporary) / "candidate.bin"
            baseline.write_bytes(b"same-prefix" + b"old-nf-tail")
            candidate.write_bytes(b"same-prefix" + b"new-nf-tail")
            with self.assertRaises(MODULE.ParityError) as captured:
                MODULE.compare_exact_files(baseline, candidate)
        self.assertEqual("PARITY_EXACT_MISMATCH", captured.exception.code)

        with tempfile.TemporaryDirectory() as temporary:
            baseline = Path(temporary) / "baseline.bin"
            candidate = Path(temporary) / "candidate.bin"
            baseline.write_bytes(b"ABCD")
            candidate.write_bytes(b"ABxD")
            bounded = {
                "kind": "owner-approved-diff-nf-preservation",
                "ownerDecision": correction["ownerDecision"],
                "baselineOutput": {
                    "size": 4,
                    "sha256": hashlib.sha256(b"ABCD").hexdigest(),
                },
                "candidateOutput": {
                    "size": 4,
                    "sha256": hashlib.sha256(b"ABxD").hexdigest(),
                },
                "differentByteCount": 1,
                "differentRanges": [{"start": 2, "endExclusive": 3}],
            }
            result = MODULE.compare_approved_semantic_correction(
                baseline, candidate, bounded
            )
            self.assertFalse(result["equal"])
            candidate.write_bytes(b"AyxD")
            with self.assertRaises(MODULE.ParityError) as escaped:
                MODULE.compare_approved_semantic_correction(
                    baseline, candidate, bounded
                )
            self.assertEqual("PARITY_EXACT_MISMATCH", escaped.exception.code)

        diagnostic_ref = correction["diagnosticRecord"]
        diagnostic_path = ROOT / diagnostic_ref["path"]
        self.assertEqual(diagnostic_path.stat().st_size, diagnostic_ref["size"])
        self.assertEqual(
            hashlib.sha256(diagnostic_path.read_bytes()).hexdigest(),
            diagnostic_ref["sha256"],
        )
        self.assertEqual(
            "blocked-incomplete-independent-observation",
            diagnostic_ref["status"],
        )

        for mutation in ("hash", "size", "count", "range", "fingerprint", "provenance", "code"):
            invalid = copy.deepcopy(correction)
            if mutation == "hash":
                invalid["baselineOutput"]["sha256"] = "0" * 64
            elif mutation == "size":
                invalid["candidateOutput"]["size"] -= 1
            elif mutation == "count":
                invalid["differentByteCount"] -= 1
            elif mutation == "range":
                invalid["differentRanges"][-1]["endExclusive"] -= 1
            elif mutation == "fingerprint":
                invalid["capabilityFingerprint"] = "0" * 64
            elif mutation == "provenance":
                invalid["candidateProvenance"]["canonicalCaseManifest"]["sha256"] = "0" * 64
            else:
                invalid["requiredProofKind"] = "exact-output"
            with self.subTest(correction_mutation=mutation):
                with self.assertRaises(MODULE.ParityError) as correction_error:
                    MODULE.validate_approved_semantic_correction(invalid, route)
                self.assertEqual("PARITY_PLAN_INVALID", correction_error.exception.code)

    def test_nt51951_historical_diagnostic_remains_payload_free_and_cannot_replace_same_run_proof(self) -> None:
        diagnostic_path = (
            ROOT / "docs/contracts/v0916-nt51951-c2-diagnostic-v1.json"
        )
        diagnostic = json.loads(diagnostic_path.read_text(encoding="utf-8"))
        MODULE.validate_nt51951_diagnostic(diagnostic)
        self.assertEqual(
            [
                "full-base-input",
                "normal-ctrlram-input",
                "vn-ctrlram-input",
                "diffdlm-input",
            ],
            [item["slotId"] for item in diagnostic["orderedInputs"]],
        )
        self.assertEqual(
            list(range(4)),
            [item["order"] for item in diagnostic["orderedInputs"]],
        )
        self.assertEqual(
            ["initial-code-input", "tp-firmware-input"],
            [item["artifactId"] for item in diagnostic["baseRecipeSources"]],
        )
        self.assertEqual(
            "861fa0fae7bf5904cac88a4bcb6ed6e0aef1a54518e0903914f2121fbc411bfb",
            diagnostic["executors"]["baseline"]["contractRawSha256"],
        )
        self.assertNotEqual(
            json.loads(
                (
                    ROOT / "docs/contracts/v0916-baseline-executor-v1.json"
                ).read_text(encoding="utf-8")
            )["cliAssembly"]["sha256"],
            diagnostic["executors"]["baseline"]["cliAssemblySha256"],
            "historical observations must not be rebound to a new baseline executor",
        )
        self.assertEqual(
            "bb78da481040a368890743ea6b35b228ef44675988b9b78086d9350dc42525f6",
            diagnostic["executors"]["candidate"]["contractRawSha256"],
        )
        self.assertEqual(
            "4b0aaec9d7aeb2cc28ae669063f6bf4a6c6e6177",
            diagnostic["executors"]["candidate"]["head"],
        )
        self.assertNotEqual(
            json.loads(
                (
                    ROOT
                    / "docs/contracts/v100-candidate-source-executor-v1.json"
                ).read_text(encoding="utf-8")
            )["source"]["implementationHead"],
            diagnostic["executors"]["candidate"]["head"],
            "historical observations must not be rebound to a new executor",
        )
        state = diagnostic["evidenceState"]
        self.assertEqual("diagnostic-only-not-admitted", state["claimQuality"])
        self.assertEqual(9, len(state["missingRequiredArtifacts"]))
        self.assertEqual("PARITY_EXACT_MISMATCH", state["requiredFailureCode"])

        for mutation in (
            "input-order",
            "input-hash",
            "baseline-head",
            "candidate-tree",
            "baseline-receipt-claimed",
            "candidate-report-claimed",
            "comparison-claimed",
            "output-hash",
            "difference-count",
            "failure-code",
        ):
            invalid = copy.deepcopy(diagnostic)
            if mutation == "input-order":
                invalid["orderedInputs"][0], invalid["orderedInputs"][1] = (
                    invalid["orderedInputs"][1],
                    invalid["orderedInputs"][0],
                )
            elif mutation == "input-hash":
                invalid["orderedInputs"][0]["sha256"] = "0" * 64
            elif mutation == "baseline-head":
                invalid["executors"]["baseline"]["head"] = "0" * 40
            elif mutation == "candidate-tree":
                invalid["executors"]["candidate"]["tree"] = "0" * 40
            elif mutation == "baseline-receipt-claimed":
                invalid["evidenceState"]["missingRequiredArtifacts"].remove(
                    "baseline-build-receipt"
                )
            elif mutation == "candidate-report-claimed":
                invalid["evidenceState"]["missingRequiredArtifacts"].remove(
                    "candidate-build-report"
                )
            elif mutation == "comparison-claimed":
                invalid["evidenceState"]["missingRequiredArtifacts"].remove(
                    "independent-comparison-record"
                )
            elif mutation == "output-hash":
                invalid["reportedObservation"]["candidateOutput"]["sha256"] = "0" * 64
            elif mutation == "difference-count":
                invalid["reportedObservation"]["differentByteCount"] -= 1
            else:
                invalid["evidenceState"]["requiredFailureCode"] = (
                    "PARITY_PREFIX_MISMATCH"
                )
            with self.subTest(mutation=mutation):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.validate_nt51951_diagnostic(invalid)
                self.assertEqual("PARITY_PLAN_INVALID", captured.exception.code)

    def test_transitive_comparison_proves_both_prefixes_and_immutable_tail(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            prefix = b"TP-PREFIX"
            tail = b"IMMUTABLE-TAIL"
            paths = {
                "baseline": root / "baseline-full.bin",
                "current": root / "current-full.bin",
                "tp": root / "current-tp.bin",
                "base": root / "current-base.bin",
            }
            paths["baseline"].write_bytes(prefix + tail)
            paths["current"].write_bytes(prefix + tail)
            paths["tp"].write_bytes(prefix)
            paths["base"].write_bytes(b"BASEHEAD!" + tail)

            evidence = MODULE.compare_transitive_files(
                paths["baseline"],
                paths["current"],
                paths["tp"],
                paths["base"],
                len(prefix),
            )

            self.assertTrue(evidence["candidateTpEqualsCandidateFullPrefix"])
            self.assertTrue(evidence["candidateTpEqualsBaselineFullPrefix"])
            self.assertTrue(evidence["candidateFullTailImmutable"])

    def test_transitive_prefix_mismatch_and_tail_mutation_have_distinct_codes(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            baseline = root / "baseline.bin"
            current = root / "current.bin"
            tp = root / "tp.bin"
            base = root / "base.bin"
            baseline.write_bytes(b"ABCDtail")
            current.write_bytes(b"ABCDtail")
            base.write_bytes(b"xxxxTAIL")
            tp.write_bytes(b"ABCE")

            with self.assertRaises(MODULE.ParityError) as prefix_error:
                MODULE.compare_transitive_files(baseline, current, tp, base, 4)
            self.assertEqual("PARITY_TP_PREFIX_MISMATCH", prefix_error.exception.code)

            tp.write_bytes(b"ABCD")
            with self.assertRaises(MODULE.ParityError) as tail_error:
                MODULE.compare_transitive_files(baseline, current, tp, base, 4)
            self.assertEqual("PARITY_TAIL_MUTATED", tail_error.exception.code)

    def test_transitive_run_binds_full_inputs_replacements_and_tp_base_prefix(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            full_base = root / "full-base.bin"
            tp_base = root / "tp-base.bin"
            replacement = root / "replacement.bin"
            full_base.write_bytes(b"TP-PREFIX" + b"FULL-TAIL")
            tp_base.write_bytes(b"TP-PREFIX")
            replacement.write_bytes(b"REPL")

            def receipt(
                base: Path, base_sha: str, output_capacity: int
            ) -> dict[str, object]:
                return {
                    "scenario": {
                        "icId": "NT51927",
                        "workflowId": "ctrlram-replace",
                        "icCountVariant": "1-ic",
                        "mapVariant": "full-map",
                        "selectionToken": "single",
                        "resolvedProfileId": "profile-route-full",
                        "outputCapacity": output_capacity,
                        "compilationFingerprint": "c" * 64,
                    },
                    "inputs": [
                        {
                            "slotId": "reference-base",
                            "role": "base",
                            "path": str(base),
                            "size": base.stat().st_size,
                            "sha256": base_sha,
                        },
                        {
                            "slotId": "nf",
                            "role": "replacement",
                            "path": str(replacement),
                            "size": replacement.stat().st_size,
                            "sha256": hashlib.sha256(replacement.read_bytes()).hexdigest(),
                        },
                    ],
                }

            full_sha = hashlib.sha256(full_base.read_bytes()).hexdigest()
            tp_sha = hashlib.sha256(tp_base.read_bytes()).hexdigest()
            baseline = receipt(full_base, full_sha, full_base.stat().st_size)
            current = receipt(full_base, full_sha, full_base.stat().st_size)
            tp = receipt(tp_base, tp_sha, tp_base.stat().st_size)
            full_evidence = {
                "routeId": "route-full",
                "capabilityFingerprint": "a" * 64,
                "proofKind": "exact-output",
                "evidenceSha256": "b" * 64,
                "equal": True,
                "baselineReceipt": baseline,
                "candidateReceipt": current,
            }
            MODULE.validate_transitive_inputs(
                full_evidence,
                tp,
                full_base.read_bytes(),
                tp_base.read_bytes(),
                9,
            )

            tp["inputs"][1]["sha256"] = "f" * 64
            with self.assertRaises(MODULE.ParityError) as captured:
                MODULE.validate_transitive_inputs(
                    full_evidence,
                    tp,
                    full_base.read_bytes(),
                    tp_base.read_bytes(),
                    9,
                )

        self.assertEqual("PARITY_INPUT_SCENARIO_MISMATCH", captured.exception.code)

    def test_candidate_authority_rejects_tree_or_policy_mismatch(self) -> None:
        declared = {
            "implementationHead": "1" * 40,
            "implementationTree": "2" * 40,
            "authorityTrees": {
                "src": "3" * 40,
                "profiles": "4" * 40,
                "external-tools": "5" * 40,
                "tools/crc-worker": "6" * 40,
            },
            "policySha256": "7" * 64,
            "sourceExecutorContract": {"size": 10, "sha256": "8" * 64},
        }
        for mutation in (
            ("authorityTrees", "src", "9" * 40),
            ("policySha256", None, "9" * 64),
            ("sourceExecutorContract", "sha256", "a" * 64),
        ):
            observed = copy.deepcopy(declared)
            field, child, value = mutation
            if child is None:
                observed[field] = value
            else:
                observed[field][child] = value
            with self.subTest(field=field, child=child):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.validate_candidate_authority(declared, observed)
                self.assertEqual("PARITY_AUTHORITY_MISMATCH", captured.exception.code)

    def test_only_direct_exact_evidence_child_paths_transfer_parity(self) -> None:
        implementation_head = "1" * 40
        allowed = [
            "docs/governance/change-records/RELEASE-100-FINAL-01.json",
            "docs/release-evidence/v1.0.0-v0916-parity.json",
        ]
        MODULE.validate_evidence_child_transfer(
            implementation_head=implementation_head,
            release_parent=implementation_head,
            changed_paths=allowed,
            allowed_paths=allowed,
        )

        for parent, paths in (
            ("2" * 40, allowed),
            (implementation_head, [*allowed, "docs/contracts/changed.json"]),
            (implementation_head, allowed[:1]),
        ):
            with self.subTest(parent=parent, paths=paths):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.validate_evidence_child_transfer(
                        implementation_head=implementation_head,
                        release_parent=parent,
                        changed_paths=paths,
                        allowed_paths=allowed,
                    )
                self.assertEqual("PARITY_AUTHORITY_MISMATCH", captured.exception.code)

    def test_repository_authority_transfer_enforces_exact_direct_binding_head(self) -> None:
        plan = json.loads(self.plan_path.read_text(encoding="utf-8"))
        transfer = plan["candidateAuthority"]["authorityTransfer"]
        source_contract_path = plan["candidateAuthority"][
            "sourceExecutorContract"
        ]["path"]
        source_contract = json.loads(
            (ROOT / source_contract_path).read_text(encoding="utf-8")
        )
        source = source_contract["source"]
        policy_bytes = (ROOT / plan["policyBinding"]["path"]).read_bytes()
        implementation = source["implementationHead"]
        binding = "b" * 40

        class Reader:
            def __init__(self) -> None:
                self.parents = {binding: implementation}
                self.path_changes = {
                    (implementation, binding): list(
                        transfer["allowedBindingChildPaths"]
                    ),
                }
                self.last_changes = {
                    "docs/contracts/v0916-parity-certification-v1.json": binding,
                }
                self.trees = dict(source["authorityTrees"])
                self.drift_commit: str | None = None
                self.resolved_head = binding
                self.binding_plan = copy.deepcopy(plan)
                self.implementation_plan = copy.deepcopy(plan)

            def resolve_commit(self, commit: str) -> str:
                del commit
                return self.resolved_head

            def parent(self, commit: str) -> str:
                return self.parents[commit]

            def changed_paths(self, parent: str, child: str) -> list[str]:
                return self.path_changes[(parent, child)]

            def tree_for_path(self, commit: str, path: str) -> str:
                if commit == self.drift_commit and path == "src":
                    return "0" * 40
                return self.trees[path]

            def file_bytes(self, commit: str, path: str) -> bytes:
                if path == "docs/contracts/v0916-parity-certification-v1.json":
                    selected = (
                        self.binding_plan
                        if commit == binding
                        else self.implementation_plan
                    )
                    return json.dumps(selected, sort_keys=True).encode("utf-8")
                if commit == binding and path == source_contract_path:
                    return json.dumps(source_contract, sort_keys=True).encode("utf-8")
                if path == "docs/contracts/canonical-capability-policy-v1.json":
                    return policy_bytes
                raise AssertionError((commit, path))

            def last_change(
                self, head: str, path: str, *, required: bool = True
            ) -> str | None:
                del head
                value = self.last_changes.get(path)
                if value is None and required:
                    raise MODULE.ParityError("PARITY_AUTHORITY_MISMATCH")
                return value

        valid = Reader()
        result = MODULE.validate_repository_parity_authority_transfer(
            ROOT, reader=valid
        )
        self.assertEqual(
            {
                "implementationHead": implementation,
                "bindingHead": binding,
            },
            result,
        )

        mutations = {
            "extra-binding-path": lambda reader: reader.path_changes[
                (implementation, binding)
            ].append("docs/contracts/unreviewed.json"),
            "wrong-binding-parent": lambda reader: reader.parents.__setitem__(
                binding, "d" * 40
            ),
            "later-descendant": lambda reader: setattr(
                reader, "resolved_head", "c" * 40
            ),
            "authority-tree-drift": lambda reader: setattr(
                reader, "drift_commit", binding
            ),
            "self-authorized-binding-path": lambda reader: (
                reader.binding_plan["candidateAuthority"]["authorityTransfer"].__setitem__(
                    "allowedBindingChildPaths",
                    sorted(
                        [
                            *reader.binding_plan["candidateAuthority"]["authorityTransfer"]["allowedBindingChildPaths"],
                            "docs/contracts/unreviewed.json",
                        ]
                    ),
                ),
                reader.path_changes[(implementation, binding)].append(
                    "docs/contracts/unreviewed.json"
                ),
            ),
        }
        for name, mutate in mutations.items():
            reader = Reader()
            mutate(reader)
            with self.subTest(mutation=name):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.validate_repository_parity_authority_transfer(
                        ROOT, reader=reader
                    )
                self.assertEqual(
                    "PARITY_AUTHORITY_MISMATCH", captured.exception.code
                )

    def test_same_scenario_requires_topology_capacity_and_ordered_inputs(self) -> None:
        receipt = {
            "scenario": {
                "icId": "NT51927",
                "workflowId": "ctrlram-replace",
                "icCountVariant": "1-ic",
                "mapVariant": "full-map",
                "selectionToken": "single",
                "resolvedProfileId": "profile-route-full",
                "outputCapacity": 8,
                "compilationFingerprint": "a" * 64,
            },
            "inputs": [
                {"slotId": "reference-base", "role": "base", "size": 8, "sha256": "b" * 64},
                {"slotId": "nf", "role": "replacement", "size": 4, "sha256": "c" * 64},
            ],
            "output": {"size": 8, "sha256": "f" * 64},
        }
        MODULE.validate_same_scenario(receipt, copy.deepcopy(receipt))

        predecessor = copy.deepcopy(receipt)
        predecessor["scenario"]["compilationFingerprint"] = "9" * 64
        MODULE.validate_same_scenario(predecessor, receipt)

        for changed in (
            "ic",
            "workflow",
            "topology",
            "map",
            "selection",
            "capacity",
            "input-order",
            "input-sha",
        ):
            candidate = copy.deepcopy(receipt)
            if changed == "ic":
                candidate["scenario"]["icId"] = "NT51928"
            elif changed == "workflow":
                candidate["scenario"]["workflowId"] = "ab-merge"
            elif changed == "topology":
                candidate["scenario"]["icCountVariant"] = "2-ic"
            elif changed == "map":
                candidate["scenario"]["mapVariant"] = "other-map"
            elif changed == "selection":
                candidate["scenario"]["selectionToken"] = "other"
            elif changed == "capacity":
                candidate["scenario"]["outputCapacity"] = 16
            elif changed == "input-order":
                candidate["inputs"].reverse()
            else:
                candidate["inputs"][1]["sha256"] = "d" * 64
            with self.subTest(changed=changed):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.validate_same_scenario(receipt, candidate)
                self.assertEqual(
                    "PARITY_INPUT_SCENARIO_MISMATCH", captured.exception.code
                )

    def test_comparator_identity_is_script_hash_not_free_form_executor(self) -> None:
        identity = MODULE.comparator_identity(MODULE_PATH)
        self.assertEqual("1.0", identity["contractVersion"])
        self.assertEqual(
            hashlib.sha256(MODULE_PATH.read_bytes()).hexdigest(),
            identity["scriptSha256"],
        )
        self.assertNotIn("executor", identity)

    def test_rfc8785_jcs_unicode_and_property_order_match_locked_vector(self) -> None:
        first = MODULE.load_json_reject_duplicates(
            '{ "z": "\\u4e2d\\u6587", "items": ["\\u03b2", "a"], "a": 1 }'
        )
        second = MODULE.load_json_reject_duplicates(
            '{"a":1,"items":["β","a"],"z":"中文"}'
        )
        expected = "31c7db457f755d371b2733a121f58ed1dd0a1a013f9a0d8e5370258ceffc2d5e"

        self.assertEqual(expected, MODULE.canonical_json_sha256(first))
        self.assertEqual(expected, MODULE.canonical_json_sha256(second))

        with self.assertRaises(MODULE.ParityError) as captured:
            MODULE.load_json_reject_duplicates('{"a":1,"a":2}')
        self.assertEqual("PARITY_PROVENANCE_INVALID", captured.exception.code)

    def test_rfc8785_uses_utf16_property_order_and_ijson_integer_subset(self) -> None:
        # RFC 8785 sorts object names by UTF-16 code units. U+1F600 therefore
        # precedes U+E000 even though Python's scalar-value sort does the reverse.
        value = MODULE.load_json_reject_duplicates(
            '{"\\ue000":2,"\\ud83d\\ude00":1}'
        )
        expected_bytes = '{"😀":1,"":2}'.encode("utf-8")
        self.assertEqual(expected_bytes, MODULE.canonical_json_bytes(value))
        self.assertEqual(
            "04208f6cdb854e2ab1b07dd3633a39dec854344fe72824cf7f2fdb4e2e33129e",
            MODULE.canonical_json_sha256(value),
        )

        integer_value = MODULE.load_json_reject_duplicates(
            '{"zero":0,"max":9007199254740991,"min":-9007199254740991}'
        )
        self.assertEqual(
            b'{"max":9007199254740991,"min":-9007199254740991,"zero":0}',
            MODULE.canonical_json_bytes(integer_value),
        )
        self.assertEqual(
            "b7b2401ddca2165824e98c61890c0aaec470258d3119dd265d02be9438bf47e6",
            MODULE.canonical_json_sha256(integer_value),
        )

        for source in (
            '{"fraction":1.5}',
            '{"exponent":1e2}',
            '{"unsafe":9007199254740992}',
            '{"unsafe":-9007199254740992}',
            '{"nan":NaN}',
            '{"positive":Infinity}',
            '{"negative":-Infinity}',
            '{"lone":"\\ud800"}',
        ):
            with self.subTest(source=source):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.load_json_reject_duplicates(source)
                self.assertEqual(
                    "PARITY_PROVENANCE_INVALID", captured.exception.code
                )

    def test_schema_format_checker_requires_canonical_utc_and_monotonic_time(self) -> None:
        for value in (
            "2026-08-26T00:00:00Z",
            "2026-08-26T00:00:00.1Z",
            "2026-08-26T00:00:00.1234567Z",
        ):
            MODULE.parse_canonical_utc(value)

        for value in (
            "2026-08-26T08:00:00+08:00",
            "2026-08-26t00:00:00z",
            "2026-08-26T00:00:00.12345678Z",
            "2026-02-30T00:00:00Z",
            "2026-13-01T00:00:00Z",
            "2026-08-26T24:00:00Z",
        ):
            with self.subTest(value=value):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.parse_canonical_utc(value)
                self.assertEqual(
                    "PARITY_PROVENANCE_INVALID", captured.exception.code
                )

        MODULE.validate_time_order(
            "2026-08-26T00:00:00Z",
            "2026-08-26T00:00:00Z",
            error_code="PARITY_PROVENANCE_INVALID",
        )
        with self.assertRaises(MODULE.ParityError) as captured:
            MODULE.validate_time_order(
                "2026-08-26T00:00:01Z",
                "2026-08-26T00:00:00Z",
                error_code="PARITY_PROVENANCE_INVALID",
            )
        self.assertEqual("PARITY_PROVENANCE_INVALID", captured.exception.code)

    def test_locked_route_receipt_provenance_and_operator_digest_vectors(self) -> None:
        receipts = [
            {"routeId": "route-a", "role": "baseline-exact", "receiptSha256": "1" * 64},
            {"routeId": "route-a", "role": "candidate-exact", "receiptSha256": "2" * 64},
            {"routeId": "route-b", "role": "candidate-tp", "receiptSha256": "3" * 64},
        ]
        subjects = [
            {"name": "NvtFwCombiner.exe", "sha256": "a" * 64},
            {"name": "profiles/中.json", "sha256": "b" * 64},
        ]
        exact = self.schema_exact_evidence_row()
        exact_digest = (
            "5e64d7e13d9d83736b5741a2c1d9123a8579f7080422417f7ed355680e15d2cf"
        )
        routes = [exact, self.schema_transitive_evidence_row(exact_digest)]
        self.assertEqual(
            "c7a4f2c2531fa5787135cd17ae2996e46d7060b740aa3790954303f429d3dfe7",
            MODULE.canonical_receipt_set_sha256(list(reversed(receipts))),
        )
        self.assertEqual(
            "b8c72d66da20771f8793b3516611ee720e17c1009fdfc426f282f1518e11884c",
            MODULE.canonical_provenance_subjects_sha256(list(reversed(subjects))),
        )
        self.assertEqual(
            "4148fe4e0b98176438b87bd08d507780bd0810e62724ce31cba05eb47334b6a8",
            MODULE.canonical_route_evidence_sha256(list(reversed(routes))),
        )
        self.assertEqual(
            exact_digest,
            MODULE.canonical_route_row_sha256(routes[0]),
        )
        self.assertEqual(
            "f6267fa1b9a83dabcd5b53b56431b72e6e0d39213e2c58587df8bc516cae651a",
            MODULE.canonical_operator_set_sha256(["fw-owner", "dennis40816"]),
        )

        duplicate_cases = (
            ("receipt", lambda: MODULE.canonical_receipt_set_sha256([receipts[0], receipts[0]])),
            ("subject", lambda: MODULE.canonical_provenance_subjects_sha256([subjects[0], subjects[0]])),
            ("route", lambda: MODULE.canonical_route_evidence_sha256([routes[0], routes[0]])),
            ("operator", lambda: MODULE.canonical_operator_set_sha256(["dennis40816", "dennis40816"])),
        )
        for kind, action in duplicate_cases:
            with self.subTest(kind=kind):
                with self.assertRaises(MODULE.ParityError) as captured:
                    action()
                self.assertEqual("PARITY_PROVENANCE_INVALID", captured.exception.code)

    def test_transitive_row_must_consume_the_passing_exact_full_evidence(self) -> None:
        exact = self.schema_exact_evidence_row()
        exact_digest = (
            "5e64d7e13d9d83736b5741a2c1d9123a8579f7080422417f7ed355680e15d2cf"
        )
        self.assertEqual(exact_digest, MODULE.canonical_route_row_sha256(exact))
        MODULE.validate_exact_evidence_row_schema(exact)
        transitive = self.schema_transitive_evidence_row(exact_digest)
        MODULE.validate_transitive_evidence_reference(exact, transitive)

        for mutation in (
            "wrong-digest",
            "stale-row",
            "false-equal",
            "missing-equal",
            "false-passed",
            "missing-passed",
            "wrong-route",
            "input-array-order",
            "zero-tp-length",
            "tp-output-size",
            "full-input-size",
            "false-prefix-proof",
            "invalid-output-sha",
            "duplicate-receipt",
            "extra-transitive-property",
        ):
            candidate_exact = copy.deepcopy(exact)
            candidate_transitive = copy.deepcopy(transitive)
            if mutation == "wrong-digest":
                candidate_transitive["fullEvidence"]["evidenceSha256"] = "c" * 64
            elif mutation == "stale-row":
                candidate_exact["candidateOutput"]["sha256"] = "d" * 64
            elif mutation == "false-equal":
                candidate_exact["equal"] = False
            elif mutation == "missing-equal":
                candidate_exact.pop("equal")
            elif mutation == "false-passed":
                candidate_exact["passed"] = False
            elif mutation == "missing-passed":
                candidate_exact.pop("passed")
            elif mutation == "wrong-route":
                candidate_transitive["fullEvidence"]["routeId"] = "route-other"
            elif mutation == "zero-tp-length":
                candidate_transitive["tpLength"] = 0
            elif mutation == "tp-output-size":
                candidate_transitive["candidateTpOutput"]["size"] = 3
            elif mutation == "full-input-size":
                candidate_transitive["candidateFullInput"]["size"] = 4
            elif mutation == "false-prefix-proof":
                candidate_transitive["candidateTpEqualsCandidateFullPrefix"] = False
            elif mutation == "invalid-output-sha":
                candidate_transitive["candidateTpOutput"]["sha256"] = "not-a-sha"
            elif mutation == "duplicate-receipt":
                candidate_transitive["receipts"].append(
                    copy.deepcopy(candidate_transitive["receipts"][0])
                )
            elif mutation == "extra-transitive-property":
                candidate_transitive["unexpected"] = True
            else:
                candidate_exact["scenario"]["orderedInputs"].reverse()
            with self.subTest(mutation=mutation):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.validate_transitive_evidence_reference(
                        candidate_exact, candidate_transitive
                    )
                self.assertEqual("PARITY_EVIDENCE_INCOMPLETE", captured.exception.code)

    def test_exact_evidence_row_is_schema_validated_before_digest_or_reuse(self) -> None:
        exact = self.schema_exact_evidence_row()
        MODULE.validate_exact_evidence_row_schema(exact)

        for mutation in (
            "extra-property",
            "route-type",
            "receipt-cardinality-low",
            "receipt-cardinality-high",
            "receipt-role",
            "output-size-type",
            "scenario-capacity-type",
            "scenario-capacity-missing",
            "report-validation-missing",
            "report-validation-extra",
            "scenario-extra",
        ):
            candidate = copy.deepcopy(exact)
            if mutation == "extra-property":
                candidate["unexpected"] = True
            elif mutation == "route-type":
                candidate["routeId"] = 1
            elif mutation == "receipt-cardinality-low":
                candidate["receipts"].pop()
            elif mutation == "receipt-cardinality-high":
                candidate["receipts"].append(copy.deepcopy(candidate["receipts"][-1]))
            elif mutation == "receipt-role":
                candidate["receipts"][0]["role"] = "candidate-exact"
            elif mutation == "output-size-type":
                candidate["candidateOutput"]["size"] = "8"
            elif mutation == "scenario-capacity-type":
                candidate["scenario"]["outputCapacity"] = "8"
            elif mutation == "scenario-capacity-missing":
                candidate["scenario"].pop("outputCapacity")
            elif mutation == "report-validation-missing":
                candidate.pop("reportValidation")
            elif mutation == "report-validation-extra":
                candidate["reportValidation"]["unexpected"] = True
            else:
                candidate["scenario"]["unexpected"] = True
            with self.subTest(mutation=mutation):
                for action in (
                    lambda: MODULE.validate_exact_evidence_row_schema(candidate),
                    lambda: MODULE.canonical_route_row_sha256(candidate),
                ):
                    with self.assertRaises(MODULE.ParityError) as captured:
                        action()
                    self.assertEqual(
                        "PARITY_EVIDENCE_INCOMPLETE", captured.exception.code
                    )

    def test_transitive_length_bounds_fail_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            baseline = root / "baseline.bin"
            current = root / "current.bin"
            tp = root / "tp.bin"
            base = root / "base.bin"
            for path in (baseline, current, base):
                path.write_bytes(b"ABCDtail")
            tp.write_bytes(b"ABCD")

            for tp_length in (0, 4 + 1, 8, 9):
                with self.subTest(tp_length=tp_length):
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.compare_transitive_files(
                            baseline, current, tp, base, tp_length
                        )
                    self.assertEqual("PARITY_PLAN_INVALID", captured.exception.code)

    def test_independent_report_validation_shape_preserves_both_authorities(self) -> None:
        row = self.schema_exact_evidence_row()
        MODULE.validate_exact_evidence_row_schema(row)
        self.assertNotEqual(
            row["reportValidation"]["baseline"]["rawReportSha256"],
            row["reportValidation"]["candidate"]["rawReportSha256"],
        )
        for mutation in ("baseline-failed", "candidate-failed", "comparison-enabled", "missing-authority"):
            invalid = copy.deepcopy(row)
            if mutation == "baseline-failed":
                invalid["reportValidation"]["baseline"]["passed"] = False
            elif mutation == "candidate-failed":
                invalid["reportValidation"]["candidate"]["passed"] = False
            elif mutation == "comparison-enabled":
                invalid["reportValidation"]["crossVersionOperationComparison"] = "exact"
            else:
                invalid["reportValidation"]["candidate"].pop("compiledAuthoritySha256")
            with self.subTest(mutation=mutation):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.validate_exact_evidence_row_schema(invalid)
                self.assertEqual("PARITY_EVIDENCE_INCOMPLETE", captured.exception.code)

    def test_receipt_roles_are_closed_and_ordered(self) -> None:
        MODULE.validate_receipt_roles(
            "exact-output", ["baseline-exact", "candidate-exact"]
        )
        MODULE.validate_receipt_roles("tp-prefix-transitive", ["candidate-tp"])
        for proof, roles in (
            ("exact-output", ["candidate-exact", "baseline-exact"]),
            ("exact-output", ["baseline-exact", "baseline-exact"]),
            ("tp-prefix-transitive", ["candidate-tp", "candidate-exact"]),
        ):
            with self.subTest(proof=proof, roles=roles):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.validate_receipt_roles(proof, roles)
                self.assertEqual("PARITY_PROVENANCE_INVALID", captured.exception.code)

    def test_complete_evidence_requires_every_route_once_and_all_passed(self) -> None:
        plan = MODULE.load_and_validate_plan(self.plan_path, self.policy_path)
        correction_routes = {
            row["routeId"] for row in plan.raw["approvedSemanticCorrections"]
        }
        rows = [
            {
                "routeId": route.route_id,
                "capabilityFingerprint": route.capability_fingerprint,
                "proofKind": (
                    "exact-output-with-approved-semantic-correction"
                    if route.route_id in correction_routes
                    else route.proof_kind
                ),
                "passed": True,
            }
            for route in plan.routes
        ]
        MODULE.validate_evidence_route_coverage(plan, rows)

        for mutation in (
            "missing",
            "extra",
            "duplicate",
            "fingerprint",
            "proof-kind",
            "failed",
        ):
            invalid = copy.deepcopy(rows)
            if mutation == "missing":
                invalid.pop()
            elif mutation == "extra":
                invalid.append(
                    {
                        "routeId": "route-extra",
                        "capabilityFingerprint": "a" * 64,
                        "proofKind": "exact-output",
                        "passed": True,
                    }
                )
            elif mutation == "duplicate":
                invalid[-1] = copy.deepcopy(invalid[0])
            elif mutation == "fingerprint":
                invalid[-1]["capabilityFingerprint"] = "b" * 64
            elif mutation == "proof-kind":
                invalid[-1]["proofKind"] = (
                    "exact-output"
                    if invalid[-1]["proofKind"] == "tp-prefix-transitive"
                    else "tp-prefix-transitive"
                )
            else:
                invalid[-1]["passed"] = False

            with self.subTest(mutation=mutation):
                with self.assertRaises(MODULE.ParityError) as captured:
                    MODULE.validate_evidence_route_coverage(plan, invalid)
                self.assertEqual("PARITY_EVIDENCE_INCOMPLETE", captured.exception.code)


if __name__ == "__main__":
    unittest.main()

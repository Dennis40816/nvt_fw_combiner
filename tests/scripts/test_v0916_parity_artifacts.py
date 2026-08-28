"""Behavioral red tests for v0.9.16 source-executor and GitHub artifact authority."""

from __future__ import annotations

import copy
import base64
import hashlib
import importlib.util
import io
import json
import os
import stat
import subprocess
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest import mock

import yaml

from tests.scripts.v0916_parity_test_support import (
    BoundedReadStream,
    MODULE,
    MODULE_PATH,
    PRODUCTION_AVAILABLE,
    ROOT,
    RecordingGithubReader,
    RecordingBaselineExecutorHost,
    RecordingProcessRunner,
    UnavailableGithubReader,
    V0916ParityTestBase,
    parity_workflow_fixture_from_contract,
)


RELEASE_POLICY_PATH = ROOT / "scripts" / "release_promotion_policy.py"
RELEASE_POLICY_SPEC = importlib.util.spec_from_file_location(
    "release_promotion_policy_for_parity_tests", RELEASE_POLICY_PATH
)
assert RELEASE_POLICY_SPEC is not None and RELEASE_POLICY_SPEC.loader is not None
RELEASE_POLICY = importlib.util.module_from_spec(RELEASE_POLICY_SPEC)
RELEASE_POLICY_SPEC.loader.exec_module(RELEASE_POLICY)


class V0916ParityArtifactTests(V0916ParityTestBase):
    def test_normative_parity_documents_name_the_exact_candidate_head(self) -> None:
        plan = json.loads(self.plan_path.read_text(encoding="utf-8"))
        contract_path = ROOT / plan["candidateAuthority"]["sourceExecutorContract"]["path"]
        expected = json.loads(contract_path.read_text(encoding="utf-8"))["source"][
            "implementationHead"
        ]

        for relative_path in (
            "docs/adr/0057-v0916-black-box-parity-certification.md",
            "docs/contracts/v0916-parity-certification-v1.md",
        ):
            with self.subTest(path=relative_path):
                content = (ROOT / relative_path).read_text(encoding="utf-8")
                self.assertIn(expected, content)
                self.assertNotIn("e712842d", content)

    def test_plan_pins_exact_fresh_candidate_source_executor_raw_contract(self) -> None:
        plan = json.loads(self.plan_path.read_text(encoding="utf-8"))
        declared = plan["candidateAuthority"]["sourceExecutorContract"]
        path = ROOT / declared["path"]
        self.assertEqual(
            "docs/contracts/v100-candidate-source-executor-v1.json",
            declared["path"],
        )
        self.assertEqual(4219, declared["size"])
        self.assertEqual(
            "e3bd602a82281be782bef5140bd3c54c242b1bbf26b15fb83dfb0d81346c4ac2",
            declared["sha256"],
        )
        self.assertEqual(4219, path.stat().st_size)
        self.assertEqual(
            "e3bd602a82281be782bef5140bd3c54c242b1bbf26b15fb83dfb0d81346c4ac2",
            hashlib.sha256(path.read_bytes()).hexdigest(),
        )
        contract = json.loads(path.read_text(encoding="utf-8"))
        self.assertEqual("1d1d1cfcad7f0963dd3ed1e3e920d9a3425d6220", contract["source"]["implementationHead"])
        self.assertEqual("1bc350cd3217f826ba841dfe098e919390f23546", contract["source"]["implementationTree"])
        self.assertEqual(
            {
                "src": "e98ac8df13a64a53e34c4c6fc08bcde39a3c35f5",
                "profiles": "7f8bd06e23ee78954e2e2c222f7b44a315049330",
                "external-tools": "8d83e508ec3b48e000e1bef39b4b215c81b886ad",
                "tools/crc-worker": "bba57c51cab02ddf89fefdf449eb585de7b34ae5",
            },
            contract["source"]["authorityTrees"],
        )
        self.assertEqual("10.0.303", contract["toolchain"]["resolvedSdkVersion"])
        self.assertEqual("detached-git-worktree", contract["freshBuild"]["sourceMaterialization"])
        self.assertEqual(178688, contract["cliAssembly"]["size"])
        self.assertEqual("81f050116e563800240c95d800f410e2084a5c78ee8089e774fd7536e966fe73", contract["cliAssembly"]["sha256"])
        self.assertTrue(contract["freshBuild"]["emptyDestinationRequired"])
        self.assertTrue(contract["freshBuild"]["rejectIgnoredBuildOutputsBeforeRestore"])
        head = contract["source"]["implementationHead"]
        self.assertEqual(
            contract["source"]["implementationTree"],
            subprocess.check_output(
                ["git", "show", "-s", "--format=%T", head],
                cwd=ROOT,
                text=True,
            ).strip(),
        )
        for relative_path, expected_tree in contract["source"]["authorityTrees"].items():
            observed = subprocess.check_output(
                ["git", "rev-parse", f"{head}:{relative_path}"],
                cwd=ROOT,
                text=True,
            ).strip()
            self.assertEqual(expected_tree, observed, relative_path)

    def test_exact_tag_source_baseline_executor_fails_closed_on_every_authority_drift(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            contract = json.loads(
                (ROOT / "docs/contracts/v0916-baseline-executor-v1.json").read_text(
                    encoding="utf-8"
                )
            )
            declared = [
                contract["toolchain"]["globalJson"],
                *contract["lockFiles"],
                *contract["externalTools"],
                contract["cliAssembly"],
            ]
            for index, item in enumerate(declared):
                path = root / item["path"]
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(f"synthetic-authority-{index}".encode())
                item["size"] = path.stat().st_size
                item["sha256"] = hashlib.sha256(path.read_bytes()).hexdigest()
            host = RecordingBaselineExecutorHost(
                head=contract["source"]["peeledCommit"],
                tree=contract["source"]["sourceTree"],
                tag_object=contract["source"]["tagObject"],
                sdk_version="10.0.303",
            )
            synthetic_contract_path = root / "baseline-executor-contract.json"
            synthetic_contract_path.write_text(
                json.dumps(contract, ensure_ascii=False, indent=2) + "\n",
                encoding="utf-8",
            )
            executor_identity_sha256 = hashlib.sha256(
                synthetic_contract_path.read_bytes()
            ).hexdigest()
            self.assertNotEqual(
                executor_identity_sha256, MODULE.canonical_json_sha256(contract)
            )
            executor = MODULE.verify_source_baseline_executor(
                root,
                contract,
                host,
                executor_identity_sha256=executor_identity_sha256,
            )
            self.assertIsInstance(executor, MODULE.VerifiedSourceExecutor)
            self.assertEqual(executor_identity_sha256, executor.contract_identity_sha256)
            self.assertEqual(contract["cliAssembly"]["sha256"], executor.cli_sha256)
            self.assertEqual(root / contract["cliAssembly"]["path"], executor.cli_path)
            self.assertEqual(root, executor.source_root)
            self.assertTrue(executor.fresh_build)
            self.assertEqual(
                [
                    ("process", (contract["restore"]["arguments"], root)),
                    ("process", (contract["build"]["arguments"], root)),
                ],
                [call for call in host.calls if call[0] == "process"],
            )

            for mutation in (
                "head",
                "tree",
                "tag-object",
                "dirty",
                "sdk",
                "global",
                "lock",
                "tool",
                "dll",
                "restore",
                "build",
            ):
                candidate_contract = copy.deepcopy(contract)
                candidate_host = RecordingBaselineExecutorHost(
                    head=contract["source"]["peeledCommit"],
                    tree=contract["source"]["sourceTree"],
                    tag_object=contract["source"]["tagObject"],
                    sdk_version="10.0.303",
                )
                drift_path: Path | None = None
                if mutation == "head":
                    candidate_host.head = "4" * 40
                elif mutation == "tree":
                    candidate_host.tree = "4" * 40
                elif mutation == "tag-object":
                    candidate_host.tag_object = "4" * 40
                elif mutation == "dirty":
                    candidate_host.dirty_paths = ["src/changed.cs"]
                elif mutation == "sdk":
                    candidate_host.sdk_version = "10.0.304"
                elif mutation == "global":
                    drift_path = root / candidate_contract["toolchain"]["globalJson"]["path"]
                elif mutation == "lock":
                    drift_path = root / candidate_contract["lockFiles"][0]["path"]
                elif mutation == "tool":
                    drift_path = root / candidate_contract["externalTools"][0]["path"]
                elif mutation == "dll":
                    drift_path = root / candidate_contract["cliAssembly"]["path"]
                elif mutation == "restore":
                    candidate_host.process_results = [
                        subprocess.CompletedProcess([], 1, "", "restore failed")
                    ]
                else:
                    candidate_host.process_results = [
                        subprocess.CompletedProcess([], 0, "restore ok", ""),
                        subprocess.CompletedProcess([], 1, "", "build failed"),
                    ]
                original = drift_path.read_bytes() if drift_path is not None else None
                if drift_path is not None:
                    drift_path.write_bytes(original + b"drift")
                with self.subTest(mutation=mutation):
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.verify_source_baseline_executor(
                            root,
                            candidate_contract,
                            candidate_host,
                            executor_identity_sha256=executor_identity_sha256,
                        )
                    self.assertEqual(
                        "PARITY_AUTHORITY_MISMATCH", captured.exception.code
                    )
                if drift_path is not None:
                    drift_path.write_bytes(original)

    def test_candidate_source_executor_has_one_raw_contract_identity_and_fails_closed_on_drift(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            files = []
            for relative, payload in (
                ("global.json", b"global"),
                ("src/NvtFwCombiner.Cli/packages.lock.json", b"lock"),
                ("external-tools/catalog.json", b"tools"),
                ("src/NvtFwCombiner.Cli/bin/Release/net10.0/NvtFwCombiner.Cli.dll", b"cli"),
            ):
                path = root / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(payload)
                files.append(
                    {
                        "path": relative,
                        "size": len(payload),
                        "sha256": hashlib.sha256(payload).hexdigest(),
                    }
                )
            contract = {
                "schemaVersion": "1.0",
                "kind": "candidate-source-built-cli",
                "source": {
                    "implementationHead": "1" * 40,
                    "implementationTree": "2" * 40,
                    "authorityTrees": {
                        "src": "3" * 40,
                        "profiles": "4" * 40,
                        "external-tools": "5" * 40,
                        "tools/crc-worker": "6" * 40,
                    },
                    "cleanTreeRequired": True,
                },
                "freshBuild": {
                    "sourceMaterialization": "detached-git-worktree",
                    "emptyDestinationRequired": True,
                    "rejectIgnoredBuildOutputsBeforeRestore": True,
                    "forbiddenPathSegments": ["bin", "obj"],
                },
                "toolchain": {"resolvedSdkVersion": "10.0.303", "globalJson": files[0]},
                "lockFiles": [files[1]],
                "externalTools": [files[2]],
                "restore": {
                    "workingDirectory": ".",
                    "arguments": [
                        "dotnet", "restore",
                        "src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj",
                        "--locked-mode", "--disable-parallel",
                    ],
                },
                "build": {
                    "workingDirectory": ".",
                    "arguments": [
                        "dotnet", "build",
                        "src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj",
                        "--configuration", "Release", "--no-restore", "-m:1",
                        "-p:ContinuousIntegrationBuild=true",
                        "-p:PathMap={sourceRoot}=/_/src",
                    ],
                },
                "cliAssembly": files[3],
            }
            contract_path = root / "candidate-source-executor.json"
            contract_path.write_text(
                json.dumps(contract, ensure_ascii=False, indent=2) + "\n",
                encoding="utf-8",
            )
            raw_identity = hashlib.sha256(contract_path.read_bytes()).hexdigest()
            self.assertNotEqual(raw_identity, MODULE.canonical_json_sha256(contract))
            validated = MODULE.load_and_validate_candidate_source_executor_contract(
                contract_path,
                {"path": str(contract_path), "size": contract_path.stat().st_size, "sha256": raw_identity},
            )
            self.assertEqual(raw_identity, validated.identity_sha256)
            for mutation in ("materialization", "ci-build", "path-map"):
                invalid_contract = copy.deepcopy(contract)
                if mutation == "materialization":
                    invalid_contract["freshBuild"]["sourceMaterialization"] = (
                        "detached-git-archive"
                    )
                elif mutation == "ci-build":
                    invalid_contract["build"]["arguments"].remove(
                        "-p:ContinuousIntegrationBuild=true"
                    )
                else:
                    invalid_contract["build"]["arguments"][-1] = (
                        "-p:PathMap={sourceRoot}=C:/mutable"
                    )
                invalid_path = root / f"invalid-{mutation}.json"
                invalid_path.write_text(json.dumps(invalid_contract), encoding="utf-8")
                with self.subTest(deterministic_build_mutation=mutation):
                    with self.assertRaises(MODULE.ParityError) as deterministic_error:
                        MODULE.load_and_validate_candidate_source_executor_contract(
                            invalid_path,
                            {
                                "path": str(invalid_path),
                                "size": invalid_path.stat().st_size,
                                "sha256": hashlib.sha256(
                                    invalid_path.read_bytes()
                                ).hexdigest(),
                            },
                        )
                    self.assertEqual(
                        "PARITY_AUTHORITY_MISMATCH",
                        deterministic_error.exception.code,
                    )
            for traversal in ("../packages.lock.json", "..\\packages.lock.json"):
                invalid_contract = copy.deepcopy(contract)
                invalid_contract["lockFiles"][0]["path"] = traversal
                invalid_path = root / ("invalid-backslash.json" if "\\" in traversal else "invalid-slash.json")
                invalid_path.write_text(json.dumps(invalid_contract), encoding="utf-8")
                with self.subTest(traversal=traversal):
                    with self.assertRaises(MODULE.ParityError) as traversal_error:
                        MODULE.load_and_validate_candidate_source_executor_contract(
                            invalid_path,
                            {
                                "path": str(invalid_path),
                                "size": invalid_path.stat().st_size,
                                "sha256": hashlib.sha256(invalid_path.read_bytes()).hexdigest(),
                            },
                        )
                    self.assertEqual("PARITY_AUTHORITY_MISMATCH", traversal_error.exception.code)
            host = RecordingBaselineExecutorHost(
                head="1" * 40,
                tree="2" * 40,
                authority_trees=contract["source"]["authorityTrees"],
                sdk_version="10.0.303",
            )
            observed = MODULE.verify_candidate_source_executor(root, validated, host)
            self.assertIsInstance(observed, MODULE.VerifiedSourceExecutor)
            self.assertEqual(raw_identity, observed.contract_identity_sha256)
            self.assertEqual(root / contract["cliAssembly"]["path"], observed.cli_path)
            self.assertEqual(contract["cliAssembly"]["sha256"], observed.cli_sha256)
            self.assertEqual(("dotnet", str(observed.cli_path)), observed.argv_prefix)
            self.assertTrue(observed.fresh_build)
            expected_build_arguments = [
                f"-p:PathMap={root}=/_/src"
                if argument == "-p:PathMap={sourceRoot}=/_/src"
                else argument
                for argument in contract["build"]["arguments"]
            ]
            self.assertEqual(
                [
                    ("process", (contract["restore"]["arguments"], root)),
                    ("process", (expected_build_arguments, root)),
                ],
                [call for call in host.calls if call[0] == "process"],
            )
            MODULE.validate_candidate_source_executor_identity(
                candidate_authority={
                    "implementationHead": "1" * 40,
                    "implementationTree": "2" * 40,
                    "sourceExecutorContract": {"sha256": raw_identity},
                },
                candidate_source_contract=contract,
                candidate_build={"candidateSourceExecutorIdentitySha256": raw_identity},
                receipt_executor_identities=[raw_identity] * 64,
                comparison_identity_sha256=raw_identity,
                evidence_identity_sha256=raw_identity,
            )

            for mutation in ("head", "tree", "subtree", "dirty", "ignored-bin", "stale-obj", "sdk", "lock", "tool", "dll", "restore", "build", "authority-head", "authority-tree", "build-identity", "receipt-identity", "comparison-identity", "evidence-identity"):
                drift_host = RecordingBaselineExecutorHost(
                    head="1" * 40,
                    tree="2" * 40,
                    authority_trees=contract["source"]["authorityTrees"],
                    sdk_version="10.0.303",
                )
                drift_path = None
                if mutation == "head":
                    drift_host.head = "7" * 40
                elif mutation == "tree":
                    drift_host.tree = "7" * 40
                elif mutation == "subtree":
                    drift_host.authority_trees["profiles"] = "7" * 40
                elif mutation == "dirty":
                    drift_host.dirty_paths = ["src/changed.cs"]
                elif mutation == "ignored-bin":
                    drift_host.ignored_build_paths = ["src/NvtFwCombiner.Cli/bin/stale.dll"]
                elif mutation == "stale-obj":
                    drift_host.ignored_build_paths = ["src/NvtFwCombiner.Cli/obj/stale.assets.json"]
                elif mutation == "sdk":
                    drift_host.sdk_version = "10.0.304"
                elif mutation == "lock":
                    drift_path = root / files[1]["path"]
                elif mutation == "tool":
                    drift_path = root / files[2]["path"]
                elif mutation == "dll":
                    drift_path = root / files[3]["path"]
                elif mutation == "restore":
                    drift_host.process_results[0] = subprocess.CompletedProcess([], 1, "", "restore failed")
                elif mutation == "build":
                    drift_host.process_results[1] = subprocess.CompletedProcess([], 1, "", "build failed")
                if mutation.endswith("identity"):
                    candidate_build_identity = "0" * 64 if mutation == "build-identity" else raw_identity
                    receipt_identities = [raw_identity] * 64
                    if mutation == "receipt-identity":
                        receipt_identities[12] = "0" * 64
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.validate_candidate_source_executor_identity(
                            candidate_authority={
                                "implementationHead": "1" * 40,
                                "implementationTree": "2" * 40,
                                "sourceExecutorContract": {"sha256": raw_identity},
                            },
                            candidate_source_contract=contract,
                            candidate_build={"candidateSourceExecutorIdentitySha256": candidate_build_identity},
                            receipt_executor_identities=receipt_identities,
                            comparison_identity_sha256="0" * 64 if mutation == "comparison-identity" else raw_identity,
                            evidence_identity_sha256="0" * 64 if mutation == "evidence-identity" else raw_identity,
                        )
                elif mutation in ("authority-head", "authority-tree"):
                    authority = {
                        "implementationHead": "1" * 40,
                        "implementationTree": "2" * 40,
                        "sourceExecutorContract": {"sha256": raw_identity},
                    }
                    authority["implementationHead" if mutation == "authority-head" else "implementationTree"] = "0" * 40
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.validate_candidate_source_executor_identity(
                            candidate_authority=authority,
                            candidate_source_contract=contract,
                            candidate_build={"candidateSourceExecutorIdentitySha256": raw_identity},
                            receipt_executor_identities=[raw_identity] * 64,
                            comparison_identity_sha256=raw_identity,
                            evidence_identity_sha256=raw_identity,
                        )
                else:
                    original = drift_path.read_bytes() if drift_path else None
                    if drift_path:
                        drift_path.write_bytes(original + b"drift")
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.verify_candidate_source_executor(root, validated, drift_host)
                    if drift_path:
                        drift_path.write_bytes(original)
                self.assertEqual("PARITY_AUTHORITY_MISMATCH", captured.exception.code)

    def test_official_package_is_an_exact_release_reference_not_execution_authority(self) -> None:
        plan = json.loads(self.plan_path.read_text(encoding="utf-8"))
        release_reference = plan["baseline"]["releaseReference"]
        self.assertEqual("release-provenance-reference-only", release_reference["purpose"])
        self.assertEqual(
            "e55687f9d98ca3a2b02eac5789f4443697a249dcc60b261e3e6cfeae7dc03c84",
            release_reference["packageSha256"],
        )
        self.assertNotIn("packageSha256", plan["baseline"])

    def test_candidate_desktop_package_is_not_invented_as_a_cli_execution_surface(self) -> None:
        receipt_schema = json.loads(
            (ROOT / "docs/contracts/v0916-parity-receipt-v1.schema.json").read_text(
                encoding="utf-8"
            )
        )
        run_schema = json.loads(
            (ROOT / "docs/contracts/v0916-parity-run-v1.schema.json").read_text(
                encoding="utf-8"
            )
        )
        schema_text = json.dumps(receipt_schema, sort_keys=True)
        self.assertIn("candidate-source-cli", schema_text)
        self.assertNotIn("candidate-package-cli", schema_text)
        candidate_build = run_schema["properties"]["candidateBuild"]
        self.assertIn(
            "candidateSourceExecutorIdentitySha256", candidate_build["required"]
        )
        self.assertNotIn("candidateExecutorIdentitySha256", candidate_build["required"])
        self.assertIn("outputRoot", run_schema["required"])
        exact = run_schema["$defs"]["exactRoute"]
        transitive = run_schema["$defs"]["transitiveRoute"]
        self.assertNotIn("baselineExecution", exact["required"])
        self.assertNotIn("candidateExecution", exact["required"])
        self.assertNotIn("baselineExecution", exact["properties"])
        self.assertNotIn("candidateExecution", exact["properties"])
        self.assertNotIn("baselineReceipt", exact["properties"])
        self.assertNotIn("candidateReceipt", exact["properties"])
        self.assertNotIn("candidateTpExecution", transitive["required"])
        self.assertNotIn("candidateTpExecution", transitive["properties"])
        self.assertNotIn("executionRequest", run_schema["$defs"])
        self.assertNotIn("executionInput", run_schema["$defs"])
        self.assertNotIn("candidateTpReceipt", transitive["properties"])
        plan = json.loads(self.plan_path.read_text(encoding="utf-8"))
        self.assertEqual(
            "docs/contracts/v0916-candidate-source-executor-v1.schema.json",
            plan["candidateAuthority"]["candidateSourceExecutorSchemaPath"],
        )
        package_script = (ROOT / "scripts/package.ps1").read_text(encoding="utf-8")
        self.assertIn(
            "src/NvtFwCombiner.Desktop/NvtFwCombiner.Desktop.csproj",
            package_script,
        )
        self.assertNotIn(
            "src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj", package_script
        )

    def test_missing_declared_artifacts_have_one_stable_code(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            present = root / "present.bin"
            present.write_bytes(b"ABCDtail")
            missing = root / "missing.bin"
            actions = [
                lambda: MODULE.compare_exact_files(missing, present),
                lambda: MODULE.compare_exact_files(present, missing),
                lambda: MODULE.compare_transitive_files(missing, present, present, present, 4),
                lambda: MODULE.compare_transitive_files(present, missing, present, present, 4),
                lambda: MODULE.compare_transitive_files(present, present, missing, present, 4),
                lambda: MODULE.compare_transitive_files(present, present, present, missing, 4),
                lambda: MODULE.load_and_validate_receipt(missing),
                lambda: MODULE.require_local_artifact(missing, "report"),
                lambda: MODULE.require_local_artifact(missing, "input"),
                lambda: MODULE.require_local_artifact(missing, "output"),
                lambda: MODULE.require_local_artifact(missing, "candidate-zip"),
                lambda: MODULE.require_local_artifact(missing, "candidate-sbom"),
                lambda: MODULE.require_local_artifact(missing, "candidate-provenance"),
                lambda: MODULE.require_local_artifact(missing, "release-notes"),
                lambda: MODULE.require_local_artifact(missing, "candidate-manifest"),
                lambda: MODULE.require_local_artifact(missing, "asset-checksums"),
            ]
            for index, action in enumerate(actions):
                with self.subTest(index=index):
                    with self.assertRaises(MODULE.ParityError) as captured:
                        action()
                    self.assertEqual("PARITY_ARTIFACT_MISSING", captured.exception.code)

    def test_candidate_package_inner_manifest_must_bind_implementation_head(self) -> None:
        expected_head = "1" * 40
        with tempfile.TemporaryDirectory() as temporary:
            package = Path(temporary) / "NvtFwCombiner-v1.0.0-win-x64.zip"
            with zipfile.ZipFile(package, "w") as archive:
                archive.writestr(
                    "package/RELEASE-MANIFEST.json",
                    json.dumps(
                        {
                            "schemaVersion": "1.1",
                            "product": "NVT FW Combiner",
                            "version": "1.0.0",
                            "sourceCommit": "2" * 40,
                            "sourceTag": "v1.0.0",
                            "runtimeIdentifier": "win-x64",
                        }
                    ),
                )
            with self.assertRaises(MODULE.ParityError) as captured:
                MODULE.validate_candidate_package(package, expected_head)
        self.assertEqual("PARITY_PACKAGE_MISMATCH", captured.exception.code)

    def test_candidate_package_rejects_malformed_or_duplicate_inner_manifest(self) -> None:
        expected_head = "1" * 40
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for case in ("malformed", "duplicate"):
                package = root / f"{case}.zip"
                with zipfile.ZipFile(package, "w") as archive:
                    archive.writestr("package/RELEASE-MANIFEST.json", "{")
                    if case == "duplicate":
                        archive.writestr("other/RELEASE-MANIFEST.json", "{}")
                with self.subTest(case=case):
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.validate_candidate_package(package, expected_head)
                    self.assertEqual("PARITY_PACKAGE_MISMATCH", captured.exception.code)

    def test_protected_build_uses_real_canonical_six_file_candidate_surface(self) -> None:
        head = "1" * 40
        tree = "8" * 40
        workflow_sha = "b" * 40
        self.assertNotEqual(head, workflow_sha)
        workflow_contract = json.loads(
            (ROOT / "docs/contracts/v0916-parity-workflow-v1.json").read_text(
                encoding="utf-8"
            )
        )
        workflow_fixture = parity_workflow_fixture_from_contract(workflow_contract)
        workflow_bytes = yaml.safe_dump(workflow_fixture, sort_keys=False).encode("utf-8")
        workflow_blob_sha = hashlib.sha1(
            f"blob {len(workflow_bytes)}\0".encode() + workflow_bytes
        ).hexdigest()
        workflow_raw_sha256 = hashlib.sha256(workflow_bytes).hexdigest()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            assets = self._create_canonical_candidate_assets(
                root, head=head, tree=tree, workflow_sha=workflow_sha
            )
            artifact_archive = self._archive_assets(assets)
            artifact_digest = "sha256:" + hashlib.sha256(artifact_archive).hexdigest()
            artifact_download_root = root / "downloaded-artifact"
            extracted_root = artifact_download_root / "artifact-456"
            candidate_verifier = ROOT / "scripts/release_promotion_policy.py"
            package_verifier = ROOT / "scripts/smoke-release.ps1"
            provenance = json.loads(assets["provenance"].read_text(encoding="utf-8"))
            declared = {
                "repository": "Dennis40816/nvt_fw_combiner",
                "workflowPath": ".github/workflows/release.yml",
                "workflowRef": "refs/heads/main",
                "workflowCommitSha": workflow_sha,
                "workflowBlobSha": workflow_blob_sha,
                "workflowRawSha256": workflow_raw_sha256,
                "workflowSemanticContractSha256": "b4c91eb8b74a0f9b1e26784f4cb98b99e1720208a7128107cc3bcddfbfdbf029",
                "runId": 123,
                "artifactId": 456,
                "artifactName": f"stable-candidate-123-{head}",
                "artifactDigest": artifact_digest,
                "candidateManifest": self.artifact(assets["manifest"]),
                "candidateSbom": self.artifact(assets["sbom"]),
                "candidateProvenance": self.artifact(assets["provenance"]),
                "releaseNotes": self.artifact(assets["notes"]),
                "assetChecksums": self.artifact(assets["checksums"]),
                "candidateSourceExecutorIdentitySha256": "d" * 64,
                "provenanceSubjectsSha256": MODULE.canonical_provenance_subjects_sha256(
                    provenance["subjects"]
                ),
                "candidateVerifierSha256": hashlib.sha256(candidate_verifier.read_bytes()).hexdigest(),
                "packageVerifierSha256": hashlib.sha256(package_verifier.read_bytes()).hexdigest(),
            }
            github = self._github_reader(
                declared, head=workflow_sha, workflow_bytes=workflow_bytes, archive=artifact_archive
            )
            runner = RecordingProcessRunner(
                [
                    subprocess.CompletedProcess([], 0, "manifest verified", ""),
                    subprocess.CompletedProcess([], 0, "Release smoke passed", ""),
                ]
            )
            validated = MODULE.verify_protected_candidate_build(
                repository_root=ROOT,
                local_assets=assets,
                declared=declared,
                firmware_executor_head="f" * 40,
                firmware_executor_tree="e" * 40,
                package_source_head=head,
                package_source_tree=tree,
                process_runner=runner,
                github_reader=github,
                artifact_download_root=artifact_download_root,
                workflow_semantic_contract=workflow_contract,
            )
            self.assertEqual(
                {"head": "f" * 40, "tree": "e" * 40},
                validated["firmwareExecutorAuthority"],
            )
            self.assertEqual(
                {"head": head, "tree": tree},
                validated["packageSourceAuthority"],
            )
            self.assertEqual(
                {
                    "id": 123, "headSha": workflow_sha, "headBranch": "main",
                    "repository": declared["repository"], "repositoryId": 1001,
                    "headRepositoryId": 1001,
                },
                validated["artifactWorkflowRun"],
            )
            self.assertEqual(
                [
                    (
                        [
                            sys.executable,
                            str(candidate_verifier),
                            "verify-manifest",
                            "--asset-dir",
                            str(extracted_root),
                            "--manifest",
                            str(extracted_root / assets["manifest"].name),
                            "--source-sha",
                            head,
                            "--source-tree",
                            tree,
                            "--run-id",
                            "123",
                            "--workflow-sha",
                            workflow_sha,
                            "--workflow-ref",
                            "refs/heads/main",
                        ],
                        ROOT,
                    ),
                    (
                        [
                            "pwsh",
                            "-NoProfile",
                            "-File",
                            str(package_verifier),
                            "-PackagePath",
                            str(extracted_root / assets["package"].name),
                            "-SkipUiLaunch",
                        ],
                        ROOT,
                    ),
                ],
                runner.calls,
            )
            self.assertEqual(
                [
                    ("Dennis40816/nvt_fw_combiner/contents/.github/workflows/release.yml", workflow_sha),
                    ("Dennis40816/nvt_fw_combiner/run", 123),
                    ("Dennis40816/nvt_fw_combiner/artifact", 456),
                    ("Dennis40816/nvt_fw_combiner/artifact-download", 456),
                ],
                github.calls,
            )

            for role, path in assets.items():
                original = path.read_bytes()
                path.write_bytes(original + b"\nlocal-drift")
                with self.subTest(local_asset_drift=role):
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.verify_protected_candidate_build(
                            repository_root=ROOT,
                            local_assets=assets,
                            declared=declared,
                            firmware_executor_head=head,
                            firmware_executor_tree=tree,
                            package_source_head=head,
                            package_source_tree=tree,
                            process_runner=RecordingProcessRunner([]),
                            github_reader=self._github_reader(
                                declared,
                                head=workflow_sha,
                                workflow_bytes=workflow_bytes,
                                archive=artifact_archive,
                            ),
                            artifact_download_root=root / f"drift-{role}",
                            workflow_semantic_contract=workflow_contract,
                        )
                    self.assertEqual("PARITY_PACKAGE_MISMATCH", captured.exception.code)
                path.write_bytes(original)

            for mutation in (
                "workflow-commit",
                "workflow-blob",
                "workflow-bytes",
                "workflow-semantic",
                "run-head",
                "run-branch",
                "run-repository-id",
                "run-repository-name",
                "run-head-repository-id",
                "run-head-repository-name",
                "run-conclusion",
                "artifact-digest",
                "artifact-run",
                "artifact-head",
                "artifact-branch",
                "artifact-repository-id",
                "artifact-head-repository-id",
                "artifact-expired",
                "unsupported-digest",
                "github-unavailable",
                "manifest-verifier-failed",
                "smoke-verifier-failed",
            ):
                candidate_github = self._github_reader(
                    declared, head=workflow_sha, workflow_bytes=workflow_bytes, archive=artifact_archive
                )
                candidate_declared = copy.deepcopy(declared)
                candidate_runner = RecordingProcessRunner([])
                expected_code = "PARITY_AUTHORITY_MISMATCH"
                if mutation == "workflow-commit":
                    candidate_declared["workflowCommitSha"] = "0" * 40
                elif mutation == "workflow-blob":
                    candidate_github.workflow_content["sha"] = "0" * 40
                elif mutation == "workflow-bytes":
                    candidate_github.workflow_content["content"] = base64.b64encode(
                        b"name: substituted\n"
                    ).decode("ascii")
                elif mutation == "workflow-semantic":
                    invalid_workflow = copy.deepcopy(workflow_fixture)
                    invalid_workflow["jobs"]["v0916-parity-compare"]["steps"][3][
                        "with"
                    ]["path"] = "artifacts/other.json"
                    invalid_workflow_bytes = yaml.safe_dump(
                        invalid_workflow, sort_keys=False
                    ).encode("utf-8")
                    invalid_blob = hashlib.sha1(
                        f"blob {len(invalid_workflow_bytes)}\0".encode()
                        + invalid_workflow_bytes
                    ).hexdigest()
                    candidate_github.workflow_content.update(
                        sha=invalid_blob,
                        content=base64.b64encode(invalid_workflow_bytes).decode("ascii"),
                    )
                    candidate_declared.update(
                        workflowBlobSha=invalid_blob,
                        workflowRawSha256=hashlib.sha256(
                            invalid_workflow_bytes
                        ).hexdigest(),
                    )
                    expected_code = "PARITY_WORKFLOW_MISMATCH"
                elif mutation == "run-head":
                    candidate_github.run["head_sha"] = "0" * 40
                elif mutation == "run-branch":
                    candidate_github.run["head_branch"] = "other"
                elif mutation == "run-repository-id":
                    candidate_github.run["repository"]["id"] = 999
                elif mutation == "run-repository-name":
                    candidate_github.run["repository"]["full_name"] = "other/repository"
                elif mutation == "run-head-repository-id":
                    candidate_github.run["head_repository"]["id"] = 999
                elif mutation == "run-head-repository-name":
                    candidate_github.run["head_repository"]["full_name"] = "other/repository"
                elif mutation == "run-conclusion":
                    candidate_github.run["conclusion"] = "failure"
                elif mutation == "artifact-digest":
                    candidate_github.artifact["digest"] = "sha256:" + "0" * 64
                elif mutation == "artifact-run":
                    candidate_github.artifact["workflow_run"]["id"] = 999
                elif mutation == "artifact-head":
                    candidate_github.artifact["workflow_run"]["head_sha"] = "f" * 40
                elif mutation == "artifact-branch":
                    candidate_github.artifact["workflow_run"]["head_branch"] = "other"
                elif mutation == "artifact-repository-id":
                    candidate_github.artifact["workflow_run"]["repository_id"] = 999
                elif mutation == "artifact-head-repository-id":
                    candidate_github.artifact["workflow_run"]["head_repository_id"] = 999
                elif mutation == "artifact-expired":
                    candidate_github.artifact["expired"] = True
                elif mutation == "unsupported-digest":
                    invalid_digest = "sha512:" + "0" * 128
                    candidate_github.artifact["digest"] = invalid_digest
                    candidate_declared["artifactDigest"] = invalid_digest
                elif mutation == "github-unavailable":
                    candidate_github = UnavailableGithubReader()
                elif mutation == "manifest-verifier-failed":
                    candidate_runner = RecordingProcessRunner(
                        [subprocess.CompletedProcess([], 1, "", "manifest rejected")]
                    )
                    expected_code = "PARITY_PACKAGE_MISMATCH"
                else:
                    candidate_runner = RecordingProcessRunner(
                        [
                            subprocess.CompletedProcess([], 0, "manifest verified", ""),
                            subprocess.CompletedProcess([], 1, "", "smoke rejected"),
                        ]
                    )
                    expected_code = "PARITY_PACKAGE_MISMATCH"
                with self.subTest(mutation=mutation):
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.verify_protected_candidate_build(
                            repository_root=ROOT,
                            local_assets=assets,
                            declared=candidate_declared,
                            firmware_executor_head=head,
                            firmware_executor_tree=tree,
                            package_source_head=head,
                            package_source_tree=tree,
                            process_runner=candidate_runner,
                            github_reader=candidate_github,
                            artifact_download_root=root / f"negative-{mutation}",
                            workflow_semantic_contract=workflow_contract,
                        )
                    self.assertEqual(expected_code, captured.exception.code)

    def test_candidate_discovery_binds_exact_surface_to_same_workflow_run(self) -> None:
        head = "1" * 40
        tree = "8" * 40
        workflow_sha = "b" * 40
        workflow_bytes = b"name: release\n"
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            assets = self._create_canonical_candidate_assets(
                root, head=head, tree=tree, workflow_sha=workflow_sha
            )
            archive = self._archive_assets(assets)
            declared_seed = {
                "repository": "Dennis40816/nvt_fw_combiner",
                "workflowPath": ".github/workflows/release.yml",
                "workflowBlobSha": hashlib.sha1(
                    f"blob {len(workflow_bytes)}\0".encode("ascii") + workflow_bytes
                ).hexdigest(),
                "artifactName": f"stable-candidate-123-{head}",
                "artifactDigest": "sha256:" + hashlib.sha256(archive).hexdigest(),
            }
            github = self._github_reader(
                declared_seed,
                head=workflow_sha,
                workflow_bytes=workflow_bytes,
                archive=archive,
            )

            local_assets, declared, package = MODULE.discover_candidate_build_declaration(
                repository_root=ROOT,
                candidate_artifact_dir=assets["package"].parent,
                candidate_source_identity_sha256="d" * 64,
                github_reader=github,
                repository="Dennis40816/nvt_fw_combiner",
                run_id=123,
                workflow_sha=workflow_sha,
            )

            self.assertEqual(set(assets), set(local_assets))
            self.assertEqual(456, declared["artifactId"])
            self.assertEqual(head, package["packageSourceHead"])
            self.assertEqual(tree, package["packageSourceTree"])
            self.assertIn(
                ("Dennis40816/nvt_fw_combiner/artifacts", 123), github.calls
            )

    def test_candidate_discovery_rejects_cross_run_or_ambiguous_artifact(self) -> None:
        head = "1" * 40
        tree = "8" * 40
        workflow_sha = "b" * 40
        workflow_bytes = b"name: release\n"
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            assets = self._create_canonical_candidate_assets(
                root, head=head, tree=tree, workflow_sha=workflow_sha
            )
            archive = self._archive_assets(assets)
            seed = {
                "repository": "Dennis40816/nvt_fw_combiner",
                "workflowPath": ".github/workflows/release.yml",
                "workflowBlobSha": hashlib.sha1(
                    f"blob {len(workflow_bytes)}\0".encode("ascii") + workflow_bytes
                ).hexdigest(),
                "artifactName": f"stable-candidate-123-{head}",
                "artifactDigest": "sha256:" + hashlib.sha256(archive).hexdigest(),
            }
            for mutation in (
                "cross-run",
                "cross-head",
                "cross-repository",
                "expired",
                "duplicate",
            ):
                github = self._github_reader(
                    seed,
                    head=workflow_sha,
                    workflow_bytes=workflow_bytes,
                    archive=archive,
                )
                if mutation == "cross-run":
                    github.artifact["workflow_run"]["id"] = 999
                elif mutation == "cross-head":
                    github.artifact["workflow_run"]["head_sha"] = "0" * 40
                elif mutation == "cross-repository":
                    github.run["repository"]["full_name"] = "other/repository"
                elif mutation == "expired":
                    github.artifact["expired"] = True
                else:
                    github.list_run_artifacts = lambda _repository, _run_id: [
                        copy.deepcopy(github.artifact),
                        copy.deepcopy(github.artifact),
                    ]
                with self.subTest(mutation=mutation):
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.discover_candidate_build_declaration(
                            repository_root=ROOT,
                            candidate_artifact_dir=assets["package"].parent,
                            candidate_source_identity_sha256="d" * 64,
                            github_reader=github,
                            repository="Dennis40816/nvt_fw_combiner",
                            run_id=123,
                            workflow_sha=workflow_sha,
                        )
                    self.assertEqual(
                        "PARITY_AUTHORITY_MISMATCH", captured.exception.code
                    )

            (assets["package"].parent / "unexpected.bin").write_bytes(b"unexpected")
            github = self._github_reader(
                seed,
                head=workflow_sha,
                workflow_bytes=workflow_bytes,
                archive=archive,
            )
            with self.assertRaises(MODULE.ParityError) as captured:
                MODULE.discover_candidate_build_declaration(
                    repository_root=ROOT,
                    candidate_artifact_dir=assets["package"].parent,
                    candidate_source_identity_sha256="d" * 64,
                    github_reader=github,
                    repository="Dennis40816/nvt_fw_combiner",
                    run_id=123,
                    workflow_sha=workflow_sha,
                )
            self.assertEqual("PARITY_PACKAGE_MISMATCH", captured.exception.code)

    def test_verify_candidate_cli_handler_dispatches_both_canonical_verifiers(self) -> None:
        source_contract_path = ROOT / "docs/contracts/v100-candidate-source-executor-v1.json"
        source_contract = json.loads(source_contract_path.read_text(encoding="utf-8"))
        source = source_contract["source"]
        workflow_sha = "b" * 40
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            assets = self._create_canonical_candidate_assets(
                root,
                head=source["implementationHead"],
                tree=source["implementationTree"],
                workflow_sha=workflow_sha,
            )
            output_root = root / "verification-output"
            candidate_build = {
                "candidateSourceExecutorIdentitySha256": hashlib.sha256(
                    source_contract_path.read_bytes()
                ).hexdigest(),
                "candidateManifest": self.artifact(assets["manifest"]),
                "candidateSbom": self.artifact(assets["sbom"]),
                "candidateProvenance": self.artifact(assets["provenance"]),
                "releaseNotes": self.artifact(assets["notes"]),
                "assetChecksums": self.artifact(assets["checksums"]),
            }
            run = {
                "schemaVersion": "1.0",
                "candidateAuthority": {
                    "implementationHead": source["implementationHead"],
                    "implementationTree": source["implementationTree"],
                    "authorityTrees": source["authorityTrees"],
                    "sourceExecutorContract": self.artifact(source_contract_path),
                },
                "candidateBuild": candidate_build,
                "candidatePackage": str(assets["package"]),
                "outputRoot": str(output_root),
            }
            run_path = root / "run.json"
            run_path.write_text(json.dumps(run), encoding="utf-8")

            with (
                mock.patch.object(
                    MODULE,
                    "validate_repository_parity_authority_transfer",
                    return_value={
                        "implementationHead": source["implementationHead"],
                        "bindingHead": source["implementationHead"],
                    },
                ),
                mock.patch.object(
                    MODULE,
                    "verify_protected_candidate_build",
                    return_value={"passed": True},
                ) as verifier,
            ):
                result = MODULE._verify_candidate_command(
                    __import__("argparse").Namespace(run=str(run_path))
                )

            self.assertEqual(0, result)
            call = verifier.call_args.kwargs
            self.assertEqual(source["implementationHead"], call["firmware_executor_head"])
            self.assertEqual(source["implementationTree"], call["firmware_executor_tree"])
            self.assertEqual(assets, call["local_assets"])
            self.assertEqual(
                output_root / "candidate-artifact-proof",
                call["artifact_download_root"],
            )

    def test_github_artifact_extraction_is_closed_bounded_and_path_safe(self) -> None:
        names = [
            "NvtFwCombiner-v1.0.0-win-x64.zip",
            "NvtFwCombiner-v1.0.0-win-x64.spdx.json",
            "NvtFwCombiner-v1.0.0-win-x64.provenance.json",
            "RELEASE-NOTES.md",
            "NvtFwCombiner-v1.0.0-candidate.json",
            "NvtFwCombiner-v1.0.0-assets.sha256",
        ]
        allowed = set(names)

        def archive(replacements: dict[str, tuple[str, bytes, zipfile.ZipInfo | None]] | None = None) -> bytes:
            replacements = replacements or {}
            buffer = io.BytesIO()
            with zipfile.ZipFile(buffer, "w", zipfile.ZIP_DEFLATED) as package:
                for name in names:
                    target, payload, info = replacements.get(name, (name, f"valid:{name}".encode(), None))
                    package.writestr(info if info is not None else target, payload)
            return buffer.getvalue()

        valid = archive()
        package_name = names[0]
        symlink = zipfile.ZipInfo(package_name)
        symlink.create_system = 3
        symlink.external_attr = (stat.S_IFLNK | 0o777) << 16
        malformed = {
            "traversal-forward": archive({package_name: ("../candidate.zip", b"bad", None)}),
            "traversal-backslash": archive({package_name: ("..\\candidate.zip", b"bad", None)}),
            "drive-absolute": archive({package_name: ("C:\\candidate.zip", b"bad", None)}),
            "unc-absolute": archive({package_name: ("\\\\server\\share\\candidate.zip", b"bad", None)}),
            "root-absolute": archive({package_name: ("/candidate.zip", b"bad", None)}),
            "symlink": archive({package_name: (package_name, b"target", symlink)}),
            "entry-limit": archive({package_name: (package_name, b"x" * 65, None)}),
            "ratio-limit": archive({package_name: (package_name, b"\x00" * 64, None)}),
            "compressed-budget": valid,
        }
        duplicate = io.BytesIO()
        with zipfile.ZipFile(duplicate, "w", zipfile.ZIP_DEFLATED) as package:
            for name in names:
                package.writestr(name, f"valid:{name}".encode())
            package.writestr(package_name.upper(), b"duplicate")
        malformed["casefold-duplicate"] = duplicate.getvalue()
        missing = io.BytesIO()
        with zipfile.ZipFile(missing, "w", zipfile.ZIP_DEFLATED) as package:
            for name in names[1:]:
                package.writestr(name, f"valid:{name}".encode())
        malformed["missing-entry"] = missing.getvalue()
        malformed["crc"] = self._corrupt_first_entry_crc(valid)

        with tempfile.TemporaryDirectory() as temporary:
            extracted = MODULE.extract_verified_github_artifact(
                BoundedReadStream(valid),
                github_digest="sha256:" + hashlib.sha256(valid).hexdigest(),
                destination=Path(temporary) / "valid",
                allowed_entries=allowed,
                max_entry_bytes=64,
                max_total_bytes=384,
                max_total_compressed_bytes=384,
                max_compression_ratio=100,
            )
            self.assertEqual(allowed, set(extracted))
            for mutation, payload in malformed.items():
                with self.subTest(mutation=mutation):
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.extract_verified_github_artifact(
                            BoundedReadStream(payload),
                            github_digest="sha256:" + hashlib.sha256(payload).hexdigest(),
                            destination=Path(temporary) / mutation,
                            allowed_entries=allowed,
                            max_entry_bytes=64,
                            max_total_bytes=384,
                            max_total_compressed_bytes=1 if mutation == "compressed-budget" else 384,
                            max_compression_ratio=1 if mutation == "ratio-limit" else 100,
                        )
                    self.assertEqual("PARITY_PACKAGE_MISMATCH", captured.exception.code)

    @unittest.skipUnless(os.name == "nt", "Windows directory-handle semantics")
    def test_controlled_directory_lease_prevents_check_use_replacement_on_windows(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            destination = root / "capture-0001"
            moved = root / "attacker-moved-capture"
            with MODULE.acquire_controlled_directory_lease(destination) as lease:
                self.assertEqual(destination, lease.path)
                self.assertTrue(destination.is_dir())
                self.assertFalse(MODULE.path_is_reparse_point(destination))
                with self.assertRaises(OSError):
                    destination.rename(moved)
                with lease.create_exclusive_file("receipt.json") as stream:
                    stream.write(b"{}")
                self.assertEqual(b"{}", (destination / "receipt.json").read_bytes())

            destination.rename(moved)
            self.assertTrue(moved.is_dir())
            with self.assertRaises(MODULE.ParityError) as captured:
                with MODULE.acquire_controlled_directory_lease(moved):
                    self.fail("an existing staging directory must not be leased")
            self.assertEqual("PARITY_WRITE_CONFLICT", captured.exception.code)

    def test_controlled_writes_reject_existing_or_reparse_destinations_and_are_atomic(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            existing = root / "existing"
            existing.mkdir()
            (existing / "foreign.txt").write_text("do not overwrite", encoding="utf-8")
            for mutation, destination, classifier in (
                ("existing", existing, None),
                ("reparse", root / "reparse", lambda _: "reparse-point"),
            ):
                with self.subTest(mutation=mutation):
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.prepare_extraction_destination(
                            destination, path_classifier=classifier
                        )
                    self.assertEqual("PARITY_WRITE_CONFLICT", captured.exception.code)

            allowed_root = root / "allowed"
            allowed_root.mkdir()
            ancestor = allowed_root / "ancestor"
            ancestor.mkdir()
            for mutation, destination, classifier in (
                ("ancestor-reparse", ancestor / "child", lambda path: "reparse-point" if path == ancestor else "directory"),
                ("path-escape", root / "outside" / "child", lambda _: "directory"),
            ):
                with self.subTest(mutation=mutation):
                    with self.assertRaises(MODULE.ParityError) as captured:
                        MODULE.prepare_extraction_destination(
                            destination,
                            allowed_root=allowed_root,
                            path_classifier=classifier,
                        )
                    self.assertEqual("PARITY_WRITE_CONFLICT", captured.exception.code)

            capture_root = root / "capture-root"
            capture_root.mkdir()
            output_path = capture_root / "route-test" / "output.bin"
            output_path.parent.mkdir()
            output_path.write_bytes(b"do-not-overwrite")
            with self.assertRaises(MODULE.ParityError) as existing_output:
                MODULE.prepare_capture_paths(capture_root, "route-test")
            self.assertEqual("PARITY_WRITE_CONFLICT", existing_output.exception.code)

            metadata = root / "metadata/evidence.json"
            MODULE.write_json_exclusive_atomic(metadata, {"schemaVersion": "1.0"})
            self.assertEqual(
                {"schemaVersion": "1.0"},
                json.loads(metadata.read_text(encoding="utf-8")),
            )
            self.assertEqual([], list(metadata.parent.glob("*.tmp")))
            with self.assertRaises(MODULE.ParityError) as duplicate:
                MODULE.write_json_exclusive_atomic(metadata, {"schemaVersion": "2.0"})
            self.assertEqual("PARITY_WRITE_CONFLICT", duplicate.exception.code)

    def test_capture_workspace_is_empty_and_cleanup_runs_after_partial_failure(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            output_root = Path(temporary) / "explicit-output"
            output_root.mkdir()
            if not PRODUCTION_AVAILABLE:
                with self.assertRaises(RuntimeError):
                    with MODULE.capture_workspace(output_root, "route-test"):
                        self.fail("missing production marker did not fail closed")
                return
            workspace_path = None
            with self.assertRaises(RuntimeError):
                with MODULE.capture_workspace(output_root, "route-test") as workspace:
                    workspace_path = workspace
                    self.assertTrue(workspace.is_dir())
                    self.assertEqual([], list(workspace.iterdir()))
                    (workspace / "partial-report.json").write_text("{}", encoding="utf-8")
                    raise RuntimeError("simulated process failure")
            self.assertIsNotNone(workspace_path)
            self.assertFalse(workspace_path.exists())
            self.assertEqual([], list(output_root.iterdir()))

    @unittest.skipUnless(
        os.environ.get("NFC_PARITY_PACKAGE_LAB"),
        "set NFC_PARITY_PACKAGE_LAB to an external non-firmware candidate lab",
    )
    def test_real_local_package_lab_runs_both_canonical_verifiers(self) -> None:
        """Manual package-authority integration; no firmware payload is committed."""
        lab = Path(os.environ["NFC_PARITY_PACKAGE_LAB"])
        result = subprocess.run(
            [sys.executable, str(MODULE_PATH), "verify-candidate", "--run", str(lab / "v0916-parity-run.json")],
            cwd=ROOT,
            check=False,
            capture_output=True,
            text=True,
        )
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("canonical candidate manifest verified", result.stdout)
        self.assertIn("canonical release smoke verified", result.stdout)

    @staticmethod
    def _create_canonical_candidate_assets(root: Path, *, head: str, tree: str, workflow_sha: str) -> dict[str, Path]:
        asset_root = root / "local-assets"
        asset_root.mkdir()
        version = "1.0.0"
        prefix = f"NvtFwCombiner-v{version}-win-x64"
        package = asset_root / f"{prefix}.zip"
        with zipfile.ZipFile(package, "w", zipfile.ZIP_DEFLATED) as bundle:
            bundle.writestr(
                f"{prefix}/RELEASE-MANIFEST.json",
                json.dumps(
                    {
                        "schemaVersion": "1.1",
                        "product": "NVT FW Combiner",
                        "version": version,
                        "sourceCommit": head,
                        "sourceTag": f"v{version}",
                        "runtimeIdentifier": "win-x64",
                    }
                ),
            )
        sbom = asset_root / f"{prefix}.spdx.json"
        sbom.write_text(json.dumps({"spdxVersion": "SPDX-2.3", "files": []}), encoding="utf-8")
        provenance = asset_root / f"{prefix}.provenance.json"
        provenance.write_text(
            json.dumps(
                {
                    "schemaVersion": "1.0",
                    "product": "NVT FW Combiner",
                    "version": version,
                    "sourceCommit": head,
                    "sourceTag": f"v{version}",
                    "runtimeIdentifier": "win-x64",
                    "subjects": [{"name": "NvtFwCombiner.exe", "sha256": "9" * 64}],
                }
            ),
            encoding="utf-8",
        )
        notes = asset_root / "RELEASE-NOTES.md"
        notes.write_text("Synthetic package-authority fixture only.\n", encoding="utf-8")
        review = root / "review-snapshot.json"
        review.write_text("{}", encoding="utf-8")
        manifest = RELEASE_POLICY.create_candidate_manifest(
            asset_root,
            version=version,
            source_sha=head,
            source_tree=tree,
            run_id="123",
            workflow_sha=workflow_sha,
            workflow_ref="refs/heads/main",
            notes_path=notes,
            review_snapshot_path=review,
        )
        checksums = asset_root / f"NvtFwCombiner-v{version}-assets.sha256"
        RELEASE_POLICY.verify_candidate_manifest(
            manifest,
            source_sha=head,
            source_tree=tree,
            run_id="123",
            workflow_sha=workflow_sha,
            workflow_ref="refs/heads/main",
        )
        assets = {
            "package": package,
            "sbom": sbom,
            "provenance": provenance,
            "notes": notes,
            "manifest": manifest,
            "checksums": checksums,
        }
        actual = {path.name for path in asset_root.iterdir() if path.is_file()}
        if {path.name for path in assets.values()} != actual:
            raise AssertionError("fixture must equal the canonical six-file surface")
        return assets

    @staticmethod
    def _archive_assets(assets: dict[str, Path]) -> bytes:
        buffer = io.BytesIO()
        with zipfile.ZipFile(buffer, "w", zipfile.ZIP_DEFLATED) as archive:
            for path in assets.values():
                archive.write(path, path.name)
        return buffer.getvalue()

    @staticmethod
    def _github_reader(
        declared: dict[str, object], *, head: str, workflow_bytes: bytes, archive: bytes
    ) -> RecordingGithubReader:
        return RecordingGithubReader(
            {
                "type": "file",
                "path": declared["workflowPath"],
                "sha": declared["workflowBlobSha"],
                "encoding": "base64",
                "content": base64.b64encode(workflow_bytes).decode("ascii"),
            },
            {
                "repository": {"full_name": declared["repository"], "id": 1001},
                "head_repository": {"full_name": declared["repository"], "id": 1001},
                "head_branch": "main",
                "head_sha": head,
                "id": 123,
                "status": "completed",
                "conclusion": "success",
            },
            {
                "id": 456,
                "name": declared["artifactName"],
                "digest": declared["artifactDigest"],
                "expired": False,
                "workflow_run": {
                    "id": 123,
                    "head_sha": head,
                    "head_branch": "main",
                    "repository_id": 1001,
                    "head_repository_id": 1001,
                },
            },
            archive,
        )

    @staticmethod
    def _corrupt_first_entry_crc(archive: bytes) -> bytes:
        corrupted = bytearray(archive)
        with zipfile.ZipFile(io.BytesIO(archive), "r") as package:
            entry = package.infolist()[0]
            offset = entry.header_offset
            name_length = int.from_bytes(corrupted[offset + 26 : offset + 28], "little")
            extra_length = int.from_bytes(corrupted[offset + 28 : offset + 30], "little")
            data_start = offset + 30 + name_length + extra_length
            if entry.compress_size == 0:
                raise AssertionError("fixture entry must contain compressed bytes")
            corrupted[data_start + entry.compress_size // 2] ^= 0x01
        return bytes(corrupted)


if __name__ == "__main__":
    unittest.main()

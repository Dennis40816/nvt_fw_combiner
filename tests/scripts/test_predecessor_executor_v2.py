"""Executor v2 and PDB tests: synthetic PE/closure bytes and fake hosts only."""

import copy
from contextlib import nullcontext
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from scripts import predecessor_comparison as execution
from scripts import predecessor_validation as validation
from scripts import v0916_parity_certification as parity
from scripts.predecessor_pdb_probe import PdbProbeError, probe_compilation_options
from tests.scripts.predecessor_test_support import (
    COMPILER_VERSION, HOST_INFO, RUNTIME_LIST, RUNTIME_VERSION, compiler_identity,
    contract_for_fake_processes, synthetic_managed_pe, write_synthetic_cli_graph,
)
from tests.scripts.test_predecessor_comparison import FakeGitHost, FakeProcessHost
from tests.scripts import test_predecessor_v0916 as milestone_tests
from tests.scripts.test_predecessor_rolling import CANDIDATE


ROOT = Path(__file__).resolve().parents[2]
CONTRACTS = ROOT / "docs/contracts"


def load(name):
    return json.loads((CONTRACTS / name).read_bytes())


def identity(payload):
    return {"size": len(payload), "sha256": hashlib.sha256(payload).hexdigest()}


def assert_schema(value, schema, document):
    """Exercise this closed schema's vocabulary; the .NET engine is authoritative.

    This small test assertion supports only the vocabulary used by the v2 and
    activation schemas; it is not a production Draft 2020-12 validator.
    """
    if "$ref" in schema:
        target = document
        for part in schema["$ref"].removeprefix("#/").split("/"):
            target = target[part]
        assert_schema(value, target, document)
    for keyword in ("oneOf", "allOf"):
        if keyword in schema:
            matches = 0
            for branch in schema[keyword]:
                try:
                    assert_schema(value, branch, document)
                    matches += 1
                except AssertionError:
                    pass
            assert matches == (1 if keyword == "oneOf" else len(schema[keyword]))
    if "const" in schema:
        assert value == schema["const"] and type(value) is type(schema["const"])
    if "enum" in schema:
        assert value in schema["enum"]
    kinds = {"object": dict, "array": list, "string": str, "integer": int, "null": type(None), "boolean": bool}
    if "type" in schema:
        assert type(value) is kinds[schema["type"]]
    if isinstance(value, dict):
        assert set(schema.get("required", [])) <= set(value)
        properties = schema.get("properties", {})
        if schema.get("additionalProperties") is False:
            assert set(value) <= set(properties)
        for key, member in value.items():
            if key in properties:
                assert_schema(member, properties[key], document)
    if isinstance(value, list):
        assert schema.get("minItems", 0) <= len(value) <= schema.get("maxItems", len(value))
        if schema.get("uniqueItems"):
            assert len({json.dumps(member, sort_keys=True) for member in value}) == len(value)
        for member in value:
            if "items" in schema:
                assert_schema(member, schema["items"], document)
    if isinstance(value, str):
        assert len(value) >= schema.get("minLength", 0)
        if "pattern" in schema:
            assert re.search(schema["pattern"], value)
    if type(value) is int:
        assert value >= schema.get("minimum", value)


class PdbProbeTests(unittest.TestCase):
    def test_portable_metadata_compilation_options_are_found(self):
        result = probe_compilation_options(synthetic_managed_pe())
        self.assertEqual((RUNTIME_VERSION, COMPILER_VERSION), result)
        # The format's version=2 is deliberately different from compiler-version.
        self.assertNotEqual("2", result.compiler_version)

    def test_missing_ambiguous_and_corrupt_evidence_is_typed(self):
        cases = [{"debug_directory": False}, {"debug_type": 2}, {"corrupt_deflate": True},
                 {"options": b"compiler-version\0compiler\0"},
                 {"options": b"runtime-version\0runtime\0"},
                 {"options": b"runtime-version\0a\0runtime-version\0b\0compiler-version\0c\0"},
                 {"options": b"runtime-version\0a\0compiler-version\0b\0compiler-version\0c\0"}]
        for case in cases:
            with self.subTest(case=case), self.assertRaises(PdbProbeError):
                probe_compilation_options(synthetic_managed_pe(**case))
        for data in (b"", b"MZ", synthetic_managed_pe()[:-1]):
            with self.subTest(size=len(data)), self.assertRaises(PdbProbeError):
                probe_compilation_options(data)


class ExecutorContractTests(unittest.TestCase):
    def test_runtime_framework_build_argument_equals_required_runtime_in_both_directions(self):
        for name in ("predecessor-comparison-v1", "v0916-baseline-executor-v2"):
            document = load(name + ".json")
            host = document["executor"]["compilerHost"] if name.startswith("predecessor") else document["compilerHost"]
            self.assertIn("-p:RuntimeFrameworkVersion=" + host["requiredRuntime"]["version"], host["extraBuildArguments"])
            self.assertEqual([], validation.executor_compiler_host_failures(host))
            for member in ("extraBuildArguments", "requiredRuntime"):
                with self.subTest(name=name, member=member):
                    changed = copy.deepcopy(host)
                    if member == "extraBuildArguments":
                        changed[member][-1] = "-p:RuntimeFrameworkVersion=10.0.12"
                    else:
                        changed[member]["version"] = "10.0.12"
                    failures = validation.executor_compiler_host_failures(changed)
                    self.assertEqual("PREDECESSOR_EXECUTOR_INVALID", failures[0].code)
                    self.assertIn("RuntimeFrameworkVersion must equal requiredRuntime.version", failures[0].detail)

    def test_v2_closed_instance_binding_and_shared_host(self):
        record, schema = load("v0916-baseline-executor-v2.json"), load("v0916-baseline-executor-v2.schema.json")
        assert_schema(record, schema, schema)
        self.assertEqual([], validation.v0916_executor_contract_failures(record))
        comparison, amendment = load("predecessor-comparison-v1.json"), load("v0916-parity-1x-amendment-v1.json")
        self.assertEqual(comparison["executor"]["compilerHost"], record["compilerHost"])
        raw = (CONTRACTS / "v0916-baseline-executor-v2.json").read_bytes()
        self.assertEqual([], validation.baseline_executor_binding_failures(amendment["baselineExecutor"], raw))
        self.assertEqual(identity((CONTRACTS / "v0916-baseline-executor-v1.json").read_bytes()),
                         {key: record["v1Relation"]["contract"][key] for key in ("size", "sha256")})
        for name in ("predecessor-comparison-v1", "v0916-parity-1x-amendment-v1"):
            schema = load(name + ".schema.json")
            assert_schema(load(name + ".json"), schema, schema)

    def test_closed_objects_and_activation_require_complete_settings(self):
        def object_paths(value, prefix=()):
            if isinstance(value, dict):
                yield prefix
                for key, member in value.items():
                    yield from object_paths(member, prefix + (key,))
            elif isinstance(value, list):
                for index, member in enumerate(value):
                    yield from object_paths(member, prefix + (index,))

        for name in ("v0916-baseline-executor-v2", "predecessor-comparison-v1", "v0916-parity-1x-amendment-v1"):
            original, schema = load(name + ".json"), load(name + ".schema.json")
            paths = list(object_paths(original)) if name == "v0916-baseline-executor-v2" else [
                ("executor", "compilerHost") if name.startswith("predecessor") else ("baselineExecutor",)]
            for path in paths:
                with self.subTest(name=name, path=path):
                    changed = copy.deepcopy(original)
                    target = changed
                    for key in path:
                        target = target[key]
                    target["unknown"] = True
                    with self.assertRaises(AssertionError):
                        assert_schema(changed, schema, schema)
            if name == "v0916-parity-1x-amendment-v1":
                changed = copy.deepcopy(original)
                changed["baselineExecutor"]["contract"] = None
                with self.assertRaises(AssertionError):
                    assert_schema(changed, schema, schema)
            else:
                path = ("executor", "compilerHost") if name.startswith("predecessor") else ("compilerHost",)
                for member in original["executor"]["compilerHost"] if len(path) == 2 else original["compilerHost"]:
                    if member == "status":
                        continue
                    with self.subTest(name=name, missing=member):
                        changed = copy.deepcopy(original)
                        host = changed["executor"]["compilerHost"] if len(path) == 2 else changed["compilerHost"]
                        del host[member]
                        with self.assertRaises(AssertionError):
                            assert_schema(changed, schema, schema)

    def test_admission_validates_settings_and_raw_binding(self):
        comparison = load("predecessor-comparison-v1.json")
        amendment = load("v0916-parity-1x-amendment-v1.json")
        for fault in ("requiredRuntime", "verification", "extraBuildArguments", "unknown", "null-contract"):
            with self.subTest(fault=fault):
                contract, altered = copy.deepcopy(comparison), copy.deepcopy(amendment)
                host = contract["executor"]["compilerHost"]
                if fault == "unknown":
                    host["unknown"] = True
                elif fault == "null-contract":
                    altered["baselineExecutor"]["contract"] = None
                else:
                    del host[fault]
                with self.assertRaises(execution.ExecutionError):
                    execution.admit_loaded_execution_contract(contract, mode="v0916-1x", formal=True, amendment=altered)
        for member in ("path", "size", "sha256"):
            with self.subTest(member=member):
                altered = copy.deepcopy(amendment)
                altered["baselineExecutor"]["contract"][member] = {"path": "elsewhere.json", "size": 1, "sha256": "0" * 64}[member]
                self.assertTrue(validation.baseline_executor_binding_failures(
                    altered["baselineExecutor"], (CONTRACTS / "v0916-baseline-executor-v2.json").read_bytes()))


class BaselineGit(FakeGitHost):
    def __init__(self, record):
        super().__init__()
        self.record = record
        self.head = record["source"]["peeledCommit"]
        self.tag = record["source"]["tagObject"]
        self.tree = record["source"]["sourceTree"]
        self.tags = {self.tag: self.head}
        rows = [record["toolchain"]["globalJson"], *record["lockFiles"], *record["externalTools"]]
        self.files = {row["path"]: ("original " + row["path"]).encode() for row in rows}
        self.files["src/NvtFwCombiner.Presentation.Avalonia/packages.lock.json"] = b"unchanged eighth lock"
        for row in rows:
            row.update(identity(self.files[row["path"]]))
        self.files["src/source.cs"] = b"unchanged source"
        self.rewritten = {row["path"]: ("rewritten " + row["path"] + "\r\n{}").encode() for row in record["lockFileRewrites"]}
        for row in record["lockFileRewrites"]:
            row.update(identity(self.rewritten[row["path"]]))

    def git_tag_object(self, ref):
        return self.tag if ref in (self.tag, "v0.9.16") else "0" * 40

    def git_tree(self, root):
        return self.tree

    def git_dirty_paths(self, root):
        changes = [" M " + path for path, original in self.files.items()
                   if not (root / path).exists() or (root / path).read_bytes() != original]
        return self.dirty + changes


class BaselineBuilderTests(unittest.TestCase):
    def setUp(self):
        self.scratch = tempfile.TemporaryDirectory(prefix="v2-", dir=os.environ["TEMP"])
        self.root = Path(self.scratch.name)

    def tearDown(self):
        for path in self.root.rglob("*"):
            if path.is_file():
                path.chmod(0o600)
        self.scratch.cleanup()

    def prepare(self, fault=None):
        record = load("v0916-baseline-executor-v2.json")
        git = BaselineGit(record)
        seed = self.root / ("closure-" + str(len(list(self.root.iterdir()))))
        seed.mkdir()
        names = [row["path"].removesuffix(".dll") for row in record["managedAssemblies"]]
        write_synthetic_cli_graph(seed, names)
        (seed / "NvtFwCombiner.Cli.exe").write_bytes(b"synthetic apphost")
        closure = parity.runtime_closure_inventory(seed, cli_relative="NvtFwCombiner.Cli.exe")
        record["cliAssembly"].update(identity(closure.files["NvtFwCombiner.Cli.exe"]))
        for row in record["managedAssemblies"]:
            row.update(identity(closure.files[row["path"]]))
        record["runtimeClosure"].update(fileCount=closure.file_count, totalSize=closure.total_size, sha256=closure.identity_sha256)
        call_environment = []

        def respond(argv, cwd):
            call_environment.append((argv, os.environ.get("DOTNET_ROLL_FORWARD")))
            if argv == ["dotnet", "--version"]:
                return subprocess.CompletedProcess(argv, 0, "10.0.304" if fault == "sdk" else "10.0.303", "")
            if argv == ["dotnet", "--list-runtimes"]:
                return subprocess.CompletedProcess(argv, 0, RUNTIME_LIST.replace("10.0.11", "10.0.12") if fault == "runtime" else RUNTIME_LIST, "")
            if argv == ["dotnet", "--info"]:
                return subprocess.CompletedProcess(argv, 0, HOST_INFO.replace("x64", "arm64") if fault == "architecture" else HOST_INFO, "")
            if argv[1] == "restore":
                for path, payload in git.rewritten.items():
                    (cwd / path).write_bytes(payload)
                if fault == "restore-lock":
                    (cwd / record["lockFileRewrites"][0]["path"]).write_bytes(b"wrong")
                if fault == "eighth-change":
                    (cwd / "src/source.cs").write_bytes(b"wrong")
                if fault == "new-file":
                    git.dirty = ["?? unauthorized.txt"]
                if fault == "ignored-new-file":
                    ignored = cwd / "artifacts" / "unauthorized.bin"
                    ignored.parent.mkdir()
                    ignored.write_bytes(b"unauthorized ignored file")
            if argv[1] == "build":
                target = cwd / record["runtimeClosure"]["root"]
                target.mkdir(parents=True)
                for path, payload in closure.files.items():
                    (target / path).write_bytes(payload)
                if fault == "build-lock":
                    (cwd / record["lockFileRewrites"][0]["path"]).write_bytes(b"changed after build")
                if fault in ("assembly", "cli", "closure"):
                    path = {"assembly": names[0] + ".dll", "cli": "NvtFwCombiner.Cli.exe", "closure": "extra.txt"}[fault]
                    (target / path).write_bytes(synthetic_managed_pe(compiler="different") if fault == "assembly" else b"different")
                if fault in ("pdb-other", "pdb-missing", "pdb-mixed"):
                    for name in names if fault == "pdb-other" else names[:1]:
                        (target / (name + ".dll")).write_bytes(b"missing" if fault == "pdb-missing" else synthetic_managed_pe(runtime="10.0.12"))
                if fault == "empty-graph":
                    (target / "NvtFwCombiner.Cli.deps.json").write_text(json.dumps({"runtimeTarget": {"name": "synthetic"}, "targets": {"synthetic": {}}, "libraries": {}}))
            return subprocess.CompletedProcess(argv, 1 if fault in ("restore", "build") and argv[1] == fault else 0, "", "")

        contract = contract_for_fake_processes(load("predecessor-comparison-v1.json"), self.root)
        runner = execution.ProcessRunner(FakeProcessHost(respond), self.root, self.root / "settings",
                                         admission=execution.admit_loaded_execution_contract(contract, mode="rolling", formal=False),
                                         custody=lambda paths: nullcontext())
        return record, git, runner, call_environment

    def test_fake_hosts_reproduce_all_pins_and_git_blob_lock_inventory(self):
        record, git, runner, environments = self.prepare()
        previous = os.environ.get("DOTNET_ROLL_FORWARD")
        result = execution.V0916BaselineExecutorBuilder().build(git, runner, git.head, record)
        self.assertEqual("v0916", result.report_version)
        self.assertEqual(compiler_identity(7), result.identity["compilerHost"])
        self.assertEqual(record["runtimeClosure"]["sha256"], result.identity["runtimeClosureSha256"])
        locks = [{"path": path, **identity(payload)} for path, payload in sorted(git.files.items()) if path.endswith("packages.lock.json")]
        self.assertEqual(8, len(locks))
        self.assertEqual(parity.canonical_json_sha256(locks), result.identity["lockFileSetSha256"])
        for argv, observed in environments:
            self.assertEqual("Disable" if argv[1] in ("restore", "build") else previous, observed)
            if argv[1] == "build":
                for argument in record["compilerHost"]["extraBuildArguments"]:
                    self.assertEqual(1, argv.count(argument))
                self.assertNotIn("-m:1", argv)
        self.assertEqual(previous, os.environ.get("DOTNET_ROLL_FORWARD"))

    def test_baseline_refuses_source_sdk_lock_process_and_identity_drift(self):
        cases = ("tag", "commit", "tree", "dirty", "bin", "obj", "blob-pin", "sdk", "runtime", "architecture",
                 "restore", "build", "restore-lock", "eighth-change", "new-file", "ignored-new-file", "build-lock", "assembly", "cli",
                 "closure", "pdb-other", "pdb-missing", "pdb-mixed", "empty-graph")
        for fault in cases:
            with self.subTest(fault=fault):
                record, git, runner, _ = self.prepare(fault)
                commit = git.head
                if fault == "tag":
                    git.tag = "0" * 40
                elif fault == "commit":
                    commit = "0" * 40
                elif fault == "tree":
                    git.tree = "0" * 40
                elif fault == "dirty":
                    git.dirty = [" M source"]
                elif fault in ("bin", "obj"):
                    git.paths = ["src/project/" + fault + "/pre-existing"]
                elif fault == "blob-pin":
                    record["externalTools"][0]["sha256"] = "0" * 64
                with self.assertRaises(execution.ExecutionError) as found:
                    execution.V0916BaselineExecutorBuilder().build(git, runner, commit, record)
                self.assertEqual("PREDECESSOR_BASELINE_INVALID" if fault in ("tag", "commit") else "PREDECESSOR_EXECUTOR_INVALID", found.exception.code)

    def test_build_environment_is_restored_after_each_failure(self):
        for previous in (None, "LatestPatch"):
            for fault in ("restore", "build", "restore-lock", "pdb-other"):
                with self.subTest(previous=previous, fault=fault), patch.dict(os.environ, {}, clear=False):
                    if previous is None:
                        os.environ.pop("DOTNET_ROLL_FORWARD", None)
                    else:
                        os.environ["DOTNET_ROLL_FORWARD"] = previous
                    record, git, runner, _ = self.prepare(fault)
                    with self.assertRaises(execution.ExecutionError):
                        execution.V0916BaselineExecutorBuilder().build(git, runner, git.head, record)
                    self.assertEqual(previous, os.environ.get("DOTNET_ROLL_FORWARD"))

    def test_1x_uses_same_pin_and_still_refuses_lock_changes(self):
        for fault in (None, "restore-lock", "runtime", "pdb-other"):
            with self.subTest(fault=fault):
                _, git, runner, _ = self.prepare(fault)
                # The 1.x fake restore has no authorized rewrite.
                git.rewritten = {}
                if fault is None:
                    result = execution.build_1x_executor(git, runner, git.head, runner.admission.contract)
                    self.assertEqual(compiler_identity(7), result.identity["compilerHost"])
                else:
                    with self.assertRaises(execution.ExecutionError) as found:
                        execution.build_1x_executor(git, runner, git.head, runner.admission.contract)
                    self.assertEqual("PREDECESSOR_EXECUTOR_INVALID", found.exception.code)

    def test_formal_run_refuses_snapshot_binding_mismatch_before_builder(self):
        world = milestone_tests.v0916_world()
        git = milestone_tests.V0916FakeGit(world)
        git.commits[CANDIDATE][validation.BASELINE_EXECUTOR_V2_PATH] += b" "
        case = milestone_tests.V0916Tests()
        case.setUp()
        try:
            with patch.object(execution.V0916BaselineExecutorBuilder, "build") as build:
                with self.assertRaises(execution.ExecutionError) as found:
                    case.run_world(git=git, formal=True)
            self.assertEqual("PREDECESSOR_SOURCE_MISMATCH", found.exception.code)
            build.assert_not_called()
            self.assertEqual([], git.detached)
        finally:
            case.tearDown()

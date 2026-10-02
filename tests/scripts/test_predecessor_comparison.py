"""Shared execution tests: fake hosts, synthetic bytes, no child processes."""

from __future__ import annotations

import copy
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from contextlib import contextmanager, nullcontext
from typing import Any
from unittest.mock import patch

from scripts import predecessor_comparison as comparison
from scripts import v0916_parity_certification as parity
from tests.scripts.predecessor_test_support import contract_for_fake_processes
from tests.scripts.test_predecessor_report_reader import raw_report


ROOT = Path(__file__).resolve().parents[2]
CONTRACT = ROOT / "docs/contracts/predecessor-comparison-v1.json"
AMENDMENT = ROOT / "docs/contracts/v0916-parity-1x-amendment-v1.json"
PAYLOAD = b"abcdefgh"


def digest(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def report(*, preview: bool = False) -> dict[str, Any]:
    value = raw_report()
    value["Inputs"][0].update(ArtifactId="source", Sha256=digest(PAYLOAD))
    value["Output"]["Sha256"] = digest(PAYLOAD)
    if preview:
        value.update(Output=None, Mutations=[])
    return value


class FakeProcessHost:
    def __init__(self, callback=None):
        self.callback = callback
        self.calls = []

    def run(self, argv: list[str], cwd: Path) -> subprocess.CompletedProcess[str]:
        self.calls.append((list(argv), cwd, {name: os.environ[name] for name in ("TEMP", "TMP", "TMPDIR")}))
        if self.callback is not None:
            return self.callback(argv, cwd)
        return subprocess.CompletedProcess(argv, 0, "synthetic stdout", "synthetic stderr")


class FakeGitHost:
    def __init__(self):
        self.files = {"global.json": b'{"sdk":{"version":"10.0.100"}}',
                      "src/Cli/packages.lock.json": b'{"version":1}',
                      "profiles/profile.json": b"{}"}
        self.paths = []
        self.dirty = []
        self.detached = []
        self.head = "1" * 40
        self.tags = {"7" * 40: self.head}

    def list_files(self, commit):
        return list(self.files)

    def read_file(self, commit, path):
        return self.files[path]

    def git_head(self, root):
        return self.head

    def git_tag_object(self, ref):
        return ref if ref in self.tags else "0" * 40

    def git_tag_commit(self, tag_object):
        return self.tags.get(tag_object, "0" * 40)

    def git_tree(self, root):
        return "2" * 40

    def git_tree_for_path(self, root, path):
        return "3" * 40

    def git_dirty_paths(self, root):
        return self.dirty

    def git_ignored_build_paths(self, root):
        return self.paths

    @contextmanager
    def detached_worktree(self, commit, temporary_root, name):
        destination = temporary_root / name
        destination.mkdir()
        for path, payload in self.files.items():
            target = destination / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(payload)
        self.detached.append((commit, destination))
        yield destination


class ComparisonTests(unittest.TestCase):
    def setUp(self):
        self.scratch = tempfile.TemporaryDirectory(prefix="b", dir=os.environ["TEMP"])
        self.root = Path(self.scratch.name)
        self.settings = self.root / "settings"
        self.settings.mkdir()
        self.contract = parity.load_json_reject_duplicates(CONTRACT.read_bytes())
        self.runner = comparison.ProcessRunner(FakeProcessHost(), self.root, self.settings, admission=self.admission())

    def admission(self, formal=False):
        contract = contract_for_fake_processes(self.contract, self.root)
        if formal:
            contract["executor"]["compilerHost"]["status"] = "in-effect"
        return comparison.admit_loaded_execution_contract(contract, mode="rolling", formal=formal)

    def tearDown(self):
        # Admission deliberately makes copies read-only, including on Windows.
        for path in self.root.rglob("*"):
            if path.is_file():
                path.chmod(0o600)
        self.scratch.cleanup()

    def capture(self, stage="build", raw=None, exit_code=0, output=PAYLOAD, exception=None, stderr=""):
        work = self.root / f"process-{len(self.runner.captures)}"
        work.mkdir()
        input_path = work / "input.bin"
        input_path.write_bytes(PAYLOAD)
        report_path = work / "report.json"
        output_path = work / "output.bin"

        def run(argv, cwd):
            if raw is not None:
                report_path.write_text(json.dumps(raw), encoding="utf-8")
            if output is not None:
                output_path.write_bytes(output)
            if exception is not None:
                raise exception
            return subprocess.CompletedProcess(argv, exit_code, "out", stderr)

        self.runner.host = FakeProcessHost(run)
        return self.runner.run(stage=stage, argv=["synthetic"], staging_root=work,
                               inputs=[{"path": str(input_path), "expectedReportAddressSpaceId": "source", "expectedReportArtifactId": "source",
                                        "size": 8, "sha256": digest(PAYLOAD)}],
                               report_path=report_path, output_path=output_path, report_version="1x")

    def pair(self, build=None):
        preview = self.capture("preview", report(preview=True), output=None)
        final = self.capture(raw=build or report())
        return preview, final

    def test_scratch_uses_pinned_temp_outside_repository(self):
        self.assertEqual(Path(os.environ["TEMP"]).resolve(), self.root.parent.resolve())
        self.assertFalse(self.root.resolve().is_relative_to(ROOT.resolve()))

    def test_temporary_root_at_bound_is_accepted_and_one_over_is_refused(self):
        at_bound = self.root / "x"
        over_bound = self.root / "xx"
        contract = copy.deepcopy(self.contract)
        contract["environment"]["temporaryRootMaxLength"] = len(str(at_bound.resolve()))
        admission = comparison.admit_loaded_execution_contract(contract, mode="rolling", formal=False)
        host = FakeProcessHost()
        comparison.ProcessRunner(host, at_bound, self.settings, admission=admission)
        with self.assertRaises(comparison.ExecutionError) as found:
            comparison.ProcessRunner(host, over_bound, self.settings, admission=admission)
        self.assertEqual("PREDECESSOR_ENVIRONMENT_INVALID", found.exception.code)
        self.assertEqual([], host.calls)
        self.assertFalse(over_bound.exists())

    def test_committed_temporary_root_bound_is_64_and_loader_passes_it_unchanged(self):
        self.assertEqual(64, self.contract["environment"]["temporaryRootMaxLength"])
        admission = comparison.admit_execution_contract(CONTRACT, mode="rolling", formal=False)
        check = comparison.validation.execution_environment_failures
        with (patch.object(comparison.validation, "execution_environment_failures", wraps=check) as validate,
              self.assertRaises(comparison.ExecutionError) as found):
            comparison.ProcessRunner(FakeProcessHost(), self.root / ("x" * 65), self.settings, admission=admission)
        self.assertEqual("PREDECESSOR_ENVIRONMENT_INVALID", found.exception.code)
        self.assertEqual(64, validate.call_args.kwargs["maximum_length"])

    def test_process_inputs_and_captures_are_confined_to_fresh_staging(self):
        work = self.root / "confined"
        work.mkdir()
        path = self.root / "outside.bin"
        path.write_bytes(PAYLOAD)
        variants = [({"inputs": [{"path": str(path), "size": 8, "sha256": digest(PAYLOAD)}]}, "PREDECESSOR_INPUT_INVALID"),
                    ({"inputs": [], "report_path": self.root / "outside.json"}, "PREDECESSOR_REPORT_INVALID"),
                    ({"inputs": [], "output_path": path}, "PREDECESSOR_REPORT_INVALID")]
        stale = work / "report.json"
        stale.write_bytes(b"stale")
        variants.append(({"inputs": [], "report_path": stale}, "PREDECESSOR_REPORT_INVALID"))
        for arguments, code in variants:
            with self.subTest(arguments=arguments):
                with self.assertRaises(comparison.ExecutionError) as found:
                    self.runner.run(stage="build", argv=["synthetic"], staging_root=work, **arguments)
                self.assertEqual(code, found.exception.code)
                self.assertEqual([], self.runner.host.calls)

    def test_process_environment_is_fresh_and_restored_without_settings_redirection(self):
        before = {name: os.environ.get(name) for name in ("TEMP", "TMP", "TMPDIR", "LOCALAPPDATA", "APPDATA", "USERPROFILE")}
        self.capture(raw=report())
        first = self.runner.host.calls[0][2]
        self.capture(raw=report())
        second = self.runner.host.calls[0][2]
        self.assertEqual(1, len(set(first.values())))
        self.assertNotEqual(first, second)
        self.assertTrue(Path(first["TEMP"]).is_relative_to(self.root))
        self.assertEqual(before, {name: os.environ.get(name) for name in before})

    def test_nonzero_exit_and_stream_hashes_are_recorded(self):
        result = self.capture(raw=report(), exit_code=7, stderr="err")
        self.assertEqual(7, result.record["exitCode"])
        self.assertEqual(digest(b"out"), result.record["stdoutSha256"])
        self.assertEqual(digest(b"err"), result.record["stderrSha256"])
        self.assertEqual(digest(json.dumps(report()).encode()), result.record["report"]["sha256"])

    def test_timeout_preserves_partial_streams_and_has_null_exit(self):
        result = self.capture(exception=subprocess.TimeoutExpired(["synthetic"], 1800, output=b"partial", stderr=b"err"))
        self.assertTrue(result.record["timedOut"])
        self.assertIsNone(result.record["exitCode"])
        self.assertEqual(digest(b"partial"), result.record["stdoutSha256"])
        self.assertEqual(digest(b"err"), result.record["stderrSha256"])

    def test_input_mutation_is_detected(self):
        work = self.root / "mutated"
        work.mkdir()
        path = work / "input.bin"
        path.write_bytes(PAYLOAD)
        def mutate(argv, cwd):
            path.write_bytes(b"changed!")
            return subprocess.CompletedProcess(argv, 0, "", "")
        self.runner.host = FakeProcessHost(mutate)
        self.runner.custody = lambda paths: nullcontext()
        result = self.runner.run(stage="build", argv=["synthetic"], staging_root=work,
                                 inputs=[{"path": str(path), "size": 8, "sha256": digest(PAYLOAD)}])
        self.assertFalse(result.record["inputsUnchanged"])
        self.assertIn("PREDECESSOR_INPUT_INVALID", [item.code for item in result.failures])

    def test_settings_present_before_formal_run_are_refused(self):
        for name in self.contract["environment"]["perUserSettingsFiles"]:
            with self.subTest(name=name):
                path = self.settings / name
                path.write_bytes(b"{}")
                with self.assertRaises(comparison.ExecutionError) as found:
                    comparison.ProcessRunner(FakeProcessHost(), self.root, self.settings, admission=self.admission(formal=True))
                self.assertEqual("PREDECESSOR_ENVIRONMENT_INVALID", found.exception.code)
                path.unlink()

    def test_settings_changed_after_process_or_after_run_fail(self):
        for phase in ("process", "run"):
            with self.subTest(phase=phase):
                runner = comparison.ProcessRunner(FakeProcessHost(), self.root, self.settings, admission=self.admission(formal=True))
                path = self.settings / "event-buffer-format.v1.json"
                if phase == "process":
                    def changed(argv, cwd):
                        path.write_bytes(b"{}")
                        return subprocess.CompletedProcess(argv, 1, "", "AB_FORMAT_CONFIGURATION_INVALID")
                    runner.host = FakeProcessHost(changed)
                    work = self.root / phase
                    work.mkdir()
                    result = runner.run(stage="preview", argv=["synthetic"], staging_root=work, inputs=[])
                    self.assertIn("PREDECESSOR_ENVIRONMENT_INVALID", [item.code for item in result.failures])
                else:
                    path.write_bytes(b"{}")
                self.assertEqual("PREDECESSOR_ENVIRONMENT_INVALID", runner.finish()[1][0].code)
                path.unlink()

    def test_diagnostic_settings_unchanged_are_allowed_but_no_report_is_environment_failure(self):
        (self.settings / "toolchain-runtime.v1.json").write_bytes(b"{}")
        self.runner = comparison.ProcessRunner(FakeProcessHost(), self.root, self.settings, admission=self.admission())
        result = self.capture("preview", exit_code=1, output=None, stderr="AB_FORMAT_CONFIGURATION_INVALID")
        side = comparison.assemble_side_result([result], capacities={"source": 8, "output-image": 8})
        self.assertEqual("invalid", side.side["status"])
        self.assertEqual("PREDECESSOR_ENVIRONMENT_INVALID", side.failures[0].code)
        self.assertEqual([], self.runner.finish()[1])

    def test_output_side_has_payload_free_identities_and_exact_schema_members(self):
        result = comparison.assemble_side_result(self.pair(), capacities={"source": 8, "output-image": 8})
        self.assertEqual([], result.failures)
        self.assertEqual("output", result.side["status"])
        self.assertEqual({"size": 8, "sha256": digest(PAYLOAD)}, result.side["output"])
        self.assertIsNone(result.side["stoppedAt"])
        self.assertEqual({"status", "stoppedAt", "issues", "precursor", "output", "processes"}, set(result.side))

    def test_preview_and_build_typed_rejections(self):
        for stage in ("preview", "build"):
            with self.subTest(stage=stage):
                raw = report(preview=True)
                if stage == "preview":
                    raw.update(Operations=[], Mutations=[], CompilationFingerprint=None)
                raw["Issues"] = [{"Code": "product.rejected", "Severity": "Error"}]
                captures = [] if stage == "preview" else [self.capture("preview", report(preview=True), output=None)]
                captures.append(self.capture(stage, raw, exit_code=1, output=None))
                result = comparison.assemble_side_result(captures, capacities={"source": 8, "output-image": 8})
                self.assertEqual("rejected", result.side["status"])
                self.assertEqual(stage, result.side["stoppedAt"])
                self.assertEqual([], result.failures)
                self.assertEqual("report", result.side["issues"][-1]["source"])

    def test_process_failures_never_become_rejections(self):
        variants = [dict(exception=OSError("crash")),
                    dict(exception=subprocess.TimeoutExpired("synthetic", 1800)),
                    dict(exit_code=1), dict(exit_code=1, stderr="product.code")]
        for code in ("external-tool.process.failed", "external-tool.process.start-failed"):
            for severity in ("Error", "Warning", "Info"):
                raw = report(preview=True)
                raw["Issues"] = [{"Code": code, "Severity": severity}, {"Code": "product", "Severity": "Error"}]
                variants.append(dict(raw=raw, exit_code=1))
        for variant in variants:
            with self.subTest(variant=variant):
                capture = self.capture("preview", output=None, **variant)
                result = comparison.assemble_side_result([capture], capacities={"source": 8, "output-image": 8})
                self.assertEqual("invalid", result.side["status"])
                self.assertEqual("PREDECESSOR_PROCESS_FAILED", result.failures[0].code)

    def test_crash_or_timeout_with_written_error_report_is_invalid(self):
        raw = report(preview=True)
        raw["Issues"] = [{"Code": "product.rejected", "Severity": "Error"}]
        variants = [dict(exit_code=code) for code in (-1, 0x80000000, 3762504530, 0xFFFFFFFF)]
        variants.append(dict(exception=subprocess.TimeoutExpired("synthetic", 1800)))
        for variant in variants:
            with self.subTest(variant=variant):
                preview = self.capture("preview", report(preview=True), output=None)
                build = self.capture("build", raw, output=None, **variant)
                self.assertIsNotNone(build.record["report"])
                result = comparison.assemble_side_result([preview, build], capacities={"source": 8, "output-image": 8})
                self.assertEqual("invalid", result.side["status"])
                self.assertEqual("PREDECESSOR_PROCESS_FAILED", result.failures[0].code)

    def test_output_file_requires_committed_boolean_true(self):
        for committed in (False, "false", "true", 1, None):
            with self.subTest(committed=committed):
                raw = report()
                raw["Output"]["Committed"] = committed
                captures = self.pair(raw)
                self.assertIsNotNone(captures[-1].output)
                result = comparison.assemble_side_result(captures, capacities={"source": 8, "output-image": 8})
                self.assertEqual("invalid", result.side["status"])
                self.assertEqual("PREDECESSOR_REPORT_INVALID", result.failures[0].code)

    def test_every_successful_build_requires_captured_output_before_continuation(self):
        for stage in ("precursor-build", "build"):
            with self.subTest(stage=stage):
                preview_stage = stage.replace("build", "preview")
                captures = [self.capture(preview_stage, report(preview=True), output=None),
                            self.capture(stage, report(preview=True), output=None)]
                verdict = comparison.validation.side_execution_verdict(
                    [capture.evidence() for capture in captures], capacities={"source": 8, "output-image": 8}, complete=False)
                self.assertEqual("invalid", verdict.status)
                self.assertEqual(stage, verdict.stopped_at)
                self.assertEqual("PREDECESSOR_PROCESS_FAILED", verdict.failures[0].code)
                if stage == "precursor-build":
                    captures.extend(self.pair())
                    result = comparison.assemble_side_result(captures, capacities={"source": 8, "output-image": 8})
                    self.assertEqual("invalid", result.side["status"])
                    self.assertEqual(stage, result.side["stoppedAt"])

    def test_output_with_error_issue_is_invalid(self):
        raw = report()
        raw["Issues"] = [{"Code": "product", "Severity": "Error"}]
        result = comparison.assemble_side_result(self.pair(raw), capacities={"source": 8, "output-image": 8})
        self.assertEqual("invalid", result.side["status"])
        self.assertEqual("PREDECESSOR_REPORT_INVALID", result.failures[0].code)

    def test_each_per_side_check_and_fingerprint_fail_closed(self):
        def sequence(raw):
            raw["Operations"][0]["Sequence"] = 1
        def projection(raw):
            raw["Operations"][0]["Reason"] = "self-widened-authority"
        variants = [("sequence", sequence, 8), ("projection", projection, 8),
                    ("null fingerprint", lambda raw: raw.update(CompilationFingerprint=None), 8),
                    ("different fingerprint", lambda raw: raw.update(CompilationFingerprint="d" * 64), 8),
                    ("range", lambda raw: None, 7)]
        for name, change, capacity in variants:
            with self.subTest(name=name):
                raw = report()
                change(raw)
                capacities = {"source": 8, "output-image": capacity}
                result = comparison.assemble_side_result(self.pair(raw), capacities=capacities)
                self.assertEqual("invalid", result.side["status"])
                self.assertEqual("PREDECESSOR_REPORT_INVALID", result.failures[0].code)

    def test_safety_owners_are_called_unchanged_in_contract_order(self):
        calls = []
        def invoke(name, owner):
            def wrapped(*args, **kwargs):
                calls.append(name)
                return owner(*args, **kwargs)
            return wrapped
        names = self.contract["perSideSafety"]["checks"]
        from scripts import predecessor_validation as validation
        with patch.object(validation, names[0], invoke(names[0], parity.validate_report_sequence)), \
             patch.object(validation, names[1], invoke(names[1], parity.validate_report_projection_against_compiled_authority)), \
             patch.object(validation, names[2], invoke(names[2], parity.validate_semantic_report_ranges)):
            result = comparison.assemble_side_result(self.pair(), capacities={"source": 8, "output-image": 8})
        self.assertEqual("output", result.side["status"])
        self.assertEqual(names + names, calls)

    def test_precursor_and_main_outputs_have_separate_preview_authority(self):
        captures = [self.capture("precursor-preview", report(preview=True), output=None),
                    self.capture("precursor-build", report())]
        captures.extend(self.pair())
        result = comparison.assemble_side_result(captures, capacities={"source": 8, "output-image": 8})
        self.assertEqual("output", result.side["status"])
        self.assertEqual({"size": 8, "sha256": digest(PAYLOAD)}, result.side["precursor"])
        wrong = report()
        wrong["Operations"][0]["Reason"] = "other executor"
        bad = comparison.assemble_side_result([captures[0], self.capture("precursor-build", wrong)],
                                              capacities={"source": 8, "output-image": 8})
        self.assertEqual("PREDECESSOR_REPORT_INVALID", bad.failures[0].code)

    def test_out_of_order_or_continued_after_rejection_is_invalid(self):
        preview, build = self.pair()
        raw = report(preview=True)
        raw["Issues"] = [{"Code": "product", "Severity": "Error"}]
        rejected = self.capture("preview", raw, exit_code=1, output=None)
        for captures in ([build], [build, preview], [preview, preview, build], [rejected, build]):
            with self.subTest(stages=[capture.record["stage"] for capture in captures]):
                result = comparison.assemble_side_result(captures, capacities={"source": 8, "output-image": 8})
                self.assertEqual("invalid", result.side["status"])
                self.assertEqual("PREDECESSOR_REPORT_INVALID", result.failures[0].code)

    def test_report_capture_disagreement_and_duplicate_json_are_invalid(self):
        for member in ("input", "output", "duplicates"):
            with self.subTest(member=member):
                raw = report()
                if member == "input":
                    raw["Inputs"][0]["Size"] = 7
                elif member == "output":
                    raw["Output"]["Sha256"] = "0" * 64
                if member == "duplicates":
                    work = self.root / "duplicates"
                    work.mkdir()
                    path = work / "report.json"
                    def duplicate(argv, cwd):
                        path.write_bytes(b'{"RunId":1,"RunId":2}')
                        return subprocess.CompletedProcess(argv, 0, "", "")
                    self.runner.host = FakeProcessHost(duplicate)
                    captures = [self.runner.run(stage="preview", argv=["synthetic"], staging_root=work,
                                                inputs=[], report_path=path)]
                else:
                    captures = self.pair(raw)
                result = comparison.assemble_side_result(captures, capacities={"source": 8, "output-image": 8})
                self.assertEqual("PREDECESSOR_REPORT_INVALID", result.failures[0].code)

    def test_pending_interfaces_refuse_formal_and_allow_diagnostic(self):
        for mode in ("rolling", "v0916-1x"):
            with self.subTest(mode=mode):
                with self.assertRaises(comparison.ExecutionError) as found:
                    comparison.admit_execution_contract(CONTRACT, amendment_path=AMENDMENT, mode=mode, formal=True)
                self.assertEqual("PREDECESSOR_CONTRACT_PENDING", found.exception.code)
                admission = comparison.admit_execution_contract(CONTRACT, amendment_path=AMENDMENT, mode=mode, formal=False)
                self.assertFalse(admission.formal)
                self.assertTrue(admission.diagnostic)
                self.assertIn("compilerHost", admission.pending)
                self.assertEqual(mode == "v0916-1x", "baselineExecutor" in admission.pending)

    def test_unknown_mode_is_refused_before_amendment_or_process(self):
        with self.assertRaises(comparison.ExecutionError) as found:
            comparison.admit_execution_contract(CONTRACT, mode="typo", formal=False)
        self.assertEqual("PREDECESSOR_CONTRACT_PENDING", found.exception.code)
        self.assertEqual([], self.runner.host.calls)

    def test_every_interface_and_amendment_status_is_read_from_json(self):
        active = copy.deepcopy(self.contract)
        active["executor"]["compilerHost"]["status"] = "in-effect"
        amendment = parity.load_json_reject_duplicates(AMENDMENT.read_bytes())
        amendment["baselineExecutor"]["status"] = "in-effect"
        contract_path, amendment_path = self.root / "contract.json", self.root / "amendment.json"
        cases = [(name, "interfaces") for name in ("declarationSchema", "reportSchema", "reportReader")]
        cases += [("compilerHost", "executor"), ("baselineExecutor", "amendment"), (None, None)]
        for name, parent in cases:
            with self.subTest(name=name):
                contract = copy.deepcopy(active)
                baseline = copy.deepcopy(amendment)
                if name is not None:
                    (baseline if parent == "amendment" else contract[parent])[name]["status"] = "proposed"
                contract_path.write_text(json.dumps(contract), encoding="utf-8")
                amendment_path.write_text(json.dumps(baseline), encoding="utf-8")
                if name is None:
                    admission = comparison.admit_execution_contract(contract_path, amendment_path=amendment_path,
                                                                    mode="v0916-1x", formal=True)
                    self.assertTrue(admission.formal)
                    self.assertFalse(admission.diagnostic)
                else:
                    with self.assertRaises(comparison.ExecutionError) as found:
                        comparison.admit_execution_contract(contract_path, amendment_path=amendment_path,
                                                            mode="v0916-1x", formal=True)
                    self.assertEqual("PREDECESSOR_CONTRACT_PENDING", found.exception.code)

    def test_complete_byte_comparison_including_unequal_lengths_and_range_digest(self):
        examples = [(b"", b"", []), (b"abc", b"abc", []), (b"abc", b"axc", [(1, 2)]),
                    (b"abcdef", b"xbcdyz", [(0, 1), (4, 6)]), (b"ab", b"abcd", [(2, 4)]),
                    (b"abcd", b"ax", [(1, 4)]), (b"", b"a", [(0, 1)]),
                    (b"ab" * 40, b"xb" * 40, [(index * 2, index * 2 + 1) for index in range(40)])]
        for left, right, spans in examples:
            with self.subTest(left=left, right=right):
                result = comparison.compare_output_bytes(left, right)
                ranges = [{"start": start, "endExclusive": end} for start, end in spans]
                self.assertEqual("output-file-offset", result.address_space)
                self.assertEqual(ranges, result.ranges)
                self.assertEqual(ranges if ranges else None, result.scope_evidence)
                self.assertEqual(sum(end - start for start, end in spans), result.different_byte_count)
                self.assertEqual(parity.canonical_json_sha256(ranges), result.range_list_sha256)
                self.assertEqual({"size": len(left), "sha256": digest(left)}, result.baseline)
                self.assertEqual({"size": len(right), "sha256": digest(right)}, result.candidate)
        self.assertEqual(digest(b'[{"endExclusive":2,"start":1}]'),
                         comparison.compare_output_bytes(b"abc", b"axc").range_list_sha256)

    def test_only_settings_files_are_observed_and_diagnostic_changes_fail(self):
        (self.settings / "unrelated.tmp").write_bytes(b"other application")
        self.assertEqual([], self.runner.finish()[1])
        path = self.settings / "toolchain-runtime.v1.json"
        path.write_bytes(b"before")
        runner = comparison.ProcessRunner(FakeProcessHost(), self.root, self.settings, admission=self.admission())
        path.write_bytes(b"after")
        self.assertEqual("PREDECESSOR_ENVIRONMENT_INVALID", runner.finish()[1][0].code)

    def build_host(self, git, corrupt=None):
        def build(argv, cwd):
            if argv == ["dotnet", "--version"]:
                return subprocess.CompletedProcess(argv, 0, "10.0.100\n", "")
            if argv[1] == "build":
                closure = cwd / self.contract["executor"]["runtimeClosureRoot"]
                closure.mkdir(parents=True)
                (closure / "NvtFwCombiner.Cli.exe").write_bytes(b"synthetic apphost")
                (closure / "NvtFwCombiner.Cli.dll").write_bytes(b"synthetic dll")
            if corrupt is not None:
                corrupt(argv, cwd)
            return subprocess.CompletedProcess(argv, 0, "", "")
        self.runner.host = FakeProcessHost(build)
        return self.runner.host

    def test_executor_records_exact_members_and_reuses_unpinned_runtime_closure(self):
        git = FakeGitHost()
        host = self.build_host(git)
        result = comparison.build_1x_executor(git, self.runner, "1" * 40, self.contract)
        self.assertEqual(set(self.contract["executor"]["recordedIdentity"]), set(result.identity))
        self.assertEqual("1" * 40, result.identity["commit"])
        self.assertIsNone(result.identity["tagObject"])
        self.assertEqual({path: "3" * 40 for path in self.contract["executor"]["authorityTrees"]}, result.identity["authorityTrees"])
        self.assertEqual(digest(b"synthetic apphost"), result.identity["cliSha256"])
        measured = parity.runtime_closure_inventory(result.closure.root, cli_relative=result.closure.cli_relative)
        self.assertEqual(measured.identity_sha256, result.identity["runtimeClosureSha256"])
        self.assertEqual(parity.canonical_json_sha256([{"path": "src/Cli/packages.lock.json", "size": len(git.files["src/Cli/packages.lock.json"]), "sha256": digest(git.files["src/Cli/packages.lock.json"])}]), result.identity["lockFileSetSha256"])
        self.assertEqual(self.contract["executor"]["restore"]["arguments"], host.calls[1][0])
        self.assertIn(f"-p:PathMap={git.detached[0][1]}=/_/src", host.calls[2][0])

    def test_executor_refuses_dirty_build_paths_changed_locks_sdk_and_build_failure(self):
        for fault in ("dirty", "bin", "tracked-bin", "lock", "sdk", "build"):
            with self.subTest(fault=fault):
                git = FakeGitHost()
                if fault == "dirty":
                    git.dirty = [" M source"]
                if fault == "bin":
                    git.paths = ["src/Cli/bin/x"]
                if fault == "tracked-bin":
                    git.files["src/Cli/obj/x"] = b"x"
                def corrupt(argv, cwd):
                    if fault == "lock" and argv[1] == "restore":
                        (cwd / "src/Cli/packages.lock.json").write_bytes(b"changed")
                    if fault == "sdk" and argv == ["dotnet", "--version"]:
                        raise OSError("SDK unavailable")
                    if fault == "build" and argv[1] == "build":
                        raise OSError("build failed")
                host = self.build_host(git, corrupt)
                if fault == "sdk":
                    host.callback = lambda argv, cwd: subprocess.CompletedProcess(argv, 1, "", "SDK unavailable")
                with self.assertRaises(comparison.ExecutionError) as found:
                    comparison.build_1x_executor(git, self.runner, "1" * 40, self.contract)
                self.assertEqual("PREDECESSOR_EXECUTOR_INVALID", found.exception.code)

    def test_executor_refuses_in_effect_compiler_host_until_pinning_is_applied(self):
        contract = copy.deepcopy(self.contract)
        contract["executor"]["compilerHost"]["status"] = "in-effect"
        git = FakeGitHost()
        host = self.build_host(git)
        with self.assertRaises(comparison.ExecutionError) as found:
            comparison.build_1x_executor(git, self.runner, "1" * 40, contract)
        self.assertEqual("PREDECESSOR_EXECUTOR_INVALID", found.exception.code)
        self.assertEqual([], host.calls)
        self.assertEqual([], git.detached)

    def test_executor_tag_object_must_peel_to_built_commit(self):
        for tag_object, peel in (("7" * 40, "1" * 40), ("7" * 40, "4" * 40), ("8" * 40, "1" * 40)):
            with self.subTest(tag_object=tag_object, peel=peel):
                git = FakeGitHost()
                git.tags["7" * 40] = peel
                host = self.build_host(git)
                if peel == "1" * 40 and tag_object == "7" * 40:
                    result = comparison.build_1x_executor(git, self.runner, "1" * 40, self.contract, tag_object=tag_object)
                    self.assertEqual("7" * 40, result.identity["tagObject"])
                else:
                    with self.assertRaises(comparison.ExecutionError) as found:
                        comparison.build_1x_executor(git, self.runner, "1" * 40, self.contract, tag_object=tag_object)
                    self.assertEqual("PREDECESSOR_EXECUTOR_INVALID", found.exception.code)
                    self.assertEqual([], host.calls)

    def test_executor_refuses_wrong_head_and_source_or_lock_drift_after_build(self):
        for fault in ("head-before", "head-after", "dirty-after", "lock-after"):
            with self.subTest(fault=fault):
                git = FakeGitHost()
                if fault == "head-before":
                    git.head = "4" * 40
                def corrupt(argv, cwd):
                    if argv[1] == "build":
                        if fault == "head-after":
                            git.head = "4" * 40
                        elif fault == "dirty-after":
                            git.dirty = [" M source"]
                        elif fault == "lock-after":
                            (cwd / "src/Cli/packages.lock.json").write_bytes(b"changed")
                host = self.build_host(git, corrupt)
                with self.assertRaises(comparison.ExecutionError) as found:
                    comparison.build_1x_executor(git, self.runner, "1" * 40, self.contract)
                self.assertEqual("PREDECESSOR_EXECUTOR_INVALID", found.exception.code)
                if fault == "head-before":
                    self.assertEqual([], host.calls)
                else:
                    self.assertEqual("build", host.calls[-1][0][1])

    def test_report_binding_ids_are_unconditional_and_independent_of_golden_ids(self):
        authority = parity.MaterializedCanonicalAuthority(self.root, "0" * 64, "golden/manifest.json", {"golden/input.bin": PAYLOAD})
        artifacts = {"golden-input": {"role": "input", "path": "input.bin", "size": 8, "sha256": digest(PAYLOAD)}}
        for version in ("v0916", "1x"):
            for slot, expected in (("dp-input", "dp-input"), ("replace-base", "reference-base"),
                                   ("replace-ctrlram-nf", "replace-ctrlram-nf")):
                for wrong in (None, "ArtifactId", "AddressSpaceId", "missing-address", "missing-artifact", "Size", "Sha256"):
                    with self.subTest(version=version, slot=slot, wrong=wrong):
                        work = self.root / f"binding-{version}-{slot}-{wrong}"
                        work.mkdir()
                        rows = comparison.stage_case_inputs(authority, artifacts, [("golden-input", slot)], work / "inputs")
                        self.assertNotIn("artifactId", rows[0])
                        self.assertEqual(expected, rows[0]["expectedReportAddressSpaceId"])
                        self.assertEqual(expected, rows[0]["expectedReportArtifactId"])
                        raw = report(preview=True)
                        raw["Inputs"][0].update(AddressSpaceId=expected, ArtifactId=expected)
                        raw["Operations"][0]["SourceSpaceId"] = expected
                        if wrong == "missing-address":
                            del rows[0]["expectedReportAddressSpaceId"]
                        elif wrong == "missing-artifact":
                            del rows[0]["expectedReportArtifactId"]
                        elif wrong == "Size":
                            raw["Inputs"][0][wrong] = 7
                        elif wrong == "Sha256":
                            raw["Inputs"][0][wrong] = "0" * 64
                        elif wrong is not None:
                            raw["Inputs"][0][wrong] = "golden-input"
                        report_path = work / "report.json"
                        def cli(argv, cwd):
                            report_path.write_text(json.dumps(raw), encoding="utf-8")
                            return subprocess.CompletedProcess(argv, 0, "", "")
                        self.runner.host = FakeProcessHost(cli)
                        capture = self.runner.run(stage="preview", argv=["synthetic"], staging_root=work,
                                                  inputs=rows, report_path=report_path, report_version=version)
                        verdict = comparison.validation.side_execution_verdict([capture.evidence()],
                            capacities={expected: 8, "golden-input": 8, "output-image": 8}, complete=False)
                        self.assertEqual("invalid" if wrong else "ready", verdict.status)
                        if wrong:
                            self.assertEqual("PREDECESSOR_REPORT_INVALID", verdict.failures[0].code)

    def test_report_input_order_is_checked_even_when_input_bytes_are_identical(self):
        authority = parity.MaterializedCanonicalAuthority(self.root, "0" * 64, "golden/manifest.json", {"golden/input.bin": PAYLOAD})
        artifacts = {"golden-input": {"role": "input", "path": "input.bin", "size": 8, "sha256": digest(PAYLOAD)}}
        work = self.root / "binding-order"
        work.mkdir()
        rows = comparison.stage_case_inputs(authority, artifacts,
                                            [("golden-input", "dp-input"), ("golden-input", "tp-input")], work / "inputs")
        raw = report(preview=True)
        raw["Inputs"] = [{**raw["Inputs"][0], "AddressSpaceId": slot, "ArtifactId": slot}
                         for slot in ("tp-input", "dp-input")]
        raw["Operations"][0]["SourceSpaceId"] = "dp-input"
        report_path = work / "report.json"
        def cli(argv, cwd):
            report_path.write_text(json.dumps(raw), encoding="utf-8")
            return subprocess.CompletedProcess(argv, 0, "", "")
        self.runner.host = FakeProcessHost(cli)
        capture = self.runner.run(stage="preview", argv=["synthetic"], staging_root=work,
                                  inputs=rows, report_path=report_path)
        verdict = comparison.validation.side_execution_verdict([capture.evidence()],
            capacities={"dp-input": 8, "tp-input": 8, "output-image": 8}, complete=False)
        self.assertEqual("invalid", verdict.status)
        self.assertEqual("PREDECESSOR_REPORT_INVALID", verdict.failures[0].code)

    def test_input_admission_and_cli_stage_use_fresh_copies(self):
        authority = parity.MaterializedCanonicalAuthority(self.root, "0" * 64, "golden/manifest.json", {"golden/input.bin": PAYLOAD})
        artifacts = {"input": {"role": "input", "path": "input.bin", "size": 8, "sha256": digest(PAYLOAD)}}
        git = FakeGitHost()
        self.build_host(git)
        executor = comparison.build_1x_executor(git, self.runner, "1" * 40, self.contract)
        def cli(argv, cwd):
            target = Path(argv[argv.index("--report") + 1])
            raw = report(preview=True)
            raw["Inputs"][0].update(AddressSpaceId="dp-input", ArtifactId="dp-input")
            raw["Operations"][0]["SourceSpaceId"] = "dp-input"
            target.write_text(json.dumps(raw), encoding="utf-8")
            return subprocess.CompletedProcess(argv, 0, "", "")
        self.runner.host = FakeProcessHost(cli)
        request = {"workflowId": "standard-merge", "profileId": "test", "cliSelectionToken": None}
        for _ in range(2):
            capture = comparison.execute_cli_stage(self.runner, executor, request, authority, artifacts, [("input", "dp-input")], stage="preview")
            self.assertNotIn("artifactId", capture.inputs[0])
            self.assertEqual("dp-input", capture.inputs[0]["expectedReportArtifactId"])
            verdict = comparison.validation.side_execution_verdict([capture.evidence()], capacities={"dp-input": 8, "output-image": 8}, complete=False)
            self.assertEqual("ready", verdict.status)
        first, second = self.runner.host.calls
        self.assertNotEqual(first[1], second[1])
        self.assertNotEqual(first[0][first[0].index("--dp") + 1], second[0][second[0].index("--dp") + 1])
        self.assertEqual(PAYLOAD, authority.files["golden/input.bin"])
        bad = copy.deepcopy(artifacts)
        bad["input"]["size"] = 7
        with self.assertRaises(comparison.ExecutionError) as found:
            comparison.stage_case_inputs(authority, bad, [("input", "dp-input")], self.root / "bad")
        self.assertEqual("PREDECESSOR_INPUT_INVALID", found.exception.code)


if __name__ == "__main__":
    unittest.main()

"""Shared execution tests: fake hosts, synthetic bytes, no child processes."""

from __future__ import annotations

import copy
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from contextlib import contextmanager, nullcontext, redirect_stderr
from typing import Any
from unittest.mock import patch

from scripts import predecessor_comparison as comparison
from scripts import v0916_parity_certification as parity
from tests.scripts.predecessor_test_support import (contract_for_fake_processes, write_synthetic_cli_graph, RUNTIME_LIST, HOST_INFO, compiler_identity,
                                                    written_1x_merge_report, written_1x_processor_report,
                                                    written_1x_ab_merge_report, written_output_difference, PREVIEW_MARKER)
from tests.scripts.test_predecessor_report_reader import raw_report


ROOT = Path(__file__).resolve().parents[2]
CONTRACT = ROOT / "docs/contracts/predecessor-comparison-v1.json"
AMENDMENT = ROOT / "docs/contracts/v0916-parity-1x-amendment-v1.json"
PAYLOAD = b"abcdefgh"


def digest(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


MERGE_DP, MERGE_TP, MERGE_OUTPUT = b"DPDPdpdp", b"TPtp", b"TPtpdpdp"
TOOL = "external-tools/legacy-combiner/1.13.0/Combiner.exe"
TOOLS = {TOOL: b"synthetic tool", "external-tools/legacy-combiner/1.13.0/manifest.json": b"{}"}
BASE, NF, PROCESSED = b"B" * 16, b"N" * 4, b"P" * 16
DP_AB, TP_B = b"A" * 16, b"T" * 8


def merge_report(*, committed: Any, overlay: bool = False) -> dict[str, Any]:
    return written_1x_merge_report(committed=committed, dp_sha256=digest(MERGE_DP), tp_sha256=digest(MERGE_TP),
                                   output_sha256=digest(MERGE_OUTPUT), overlay=overlay)


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

    def merge_capture(self, stage, raw, output=None):
        """One process over the two Standard Merge inputs, with the written 1.x report shape."""
        work = self.root / f"merge-{len(self.runner.captures)}"
        work.mkdir()
        rows = []
        for slot, payload in (("dp-input", MERGE_DP), ("tp-input", MERGE_TP)):
            path = work / f"{slot}.bin"
            path.write_bytes(payload)
            rows.append({"path": str(path), "expectedReportAddressSpaceId": slot, "expectedReportArtifactId": slot,
                         "size": len(payload), "sha256": digest(payload)})
        report_path, output_path = work / "report.json", work / "output.bin"

        def run(argv, cwd):
            report_path.write_text(json.dumps(raw), encoding="utf-8")
            if output is not None:
                output_path.write_bytes(output)
            return subprocess.CompletedProcess(argv, 0, "", "")

        self.runner.host = FakeProcessHost(run)
        return self.runner.run(stage=stage, argv=["synthetic"], staging_root=work, inputs=rows,
                               report_path=report_path, output_path=output_path, report_version="1x")

    def merge_side(self, captures):
        capacities = {capture.record["stage"]: comparison.validation.execution_capacities(capture.evidence())
                      for capture in captures}
        return comparison.assemble_side_result(captures, capacities={}, capacities_by_stage=capacities)

    def test_written_1x_preview_and_build_reports_give_an_output_side(self):
        """Real shape: sequences 100 and 200, mutations in the Preview too, and a described Preview output."""
        preview = self.merge_capture("preview", merge_report(committed=False))
        build = self.merge_capture("build", merge_report(committed=True), output=MERGE_OUTPUT)
        self.assertIsNone(preview.output)
        self.assertEqual(["/Inputs/0/ExecutionSnapshot", "/Inputs/1/ExecutionSnapshot"],
                         preview.record["report"]["unknownMembers"])
        result = self.merge_side([preview, build])
        self.assertEqual([], result.failures)
        self.assertEqual("output", result.side["status"])
        self.assertEqual({"size": 8, "sha256": digest(MERGE_OUTPUT)}, result.side["output"])

    def test_written_mutations_must_follow_the_operation_order(self):
        """The Preview is its own authority, so only the order check can refuse these rows."""
        def reverse(raw):
            raw["Mutations"].reverse()

        def undeclared(raw):
            raw["Mutations"][1]["OperationId"] = "copy-other"

        def skipped(raw):
            del raw["Mutations"][0]

        def repeated(raw):
            raw["Mutations"].append(copy.deepcopy(raw["Mutations"][0]))

        for change in (reverse, undeclared, skipped, repeated):
            with self.subTest(change=change.__name__):
                raw = merge_report(committed=False)
                change(raw)
                result = self.merge_side([self.merge_capture("preview", raw)])
                self.assertEqual("invalid", result.side["status"])
                self.assertEqual(("PREDECESSOR_REPORT_INVALID", "preview", "PARITY_PROVENANCE_INVALID"),
                                 result.failures[0])
        prefix = merge_report(committed=False)
        del prefix["Mutations"][1]
        verdict = comparison.validation.side_execution_verdict(
            [self.merge_capture("preview", prefix).evidence()],
            capacities={"dp-input": 8, "tp-input": 4, "output-image": 8}, complete=False)
        self.assertEqual("ready", verdict.status)

    def test_declared_overlay_is_admitted_and_any_other_target_overlap_is_refused(self):
        """Real shape: the DP container is copied whole and the TP is written over it with `ReplaceExisting`."""
        preview = self.merge_capture("preview", merge_report(committed=False, overlay=True))
        build = self.merge_capture("build", merge_report(committed=True, overlay=True), output=MERGE_OUTPUT)
        result = self.merge_side([preview, build])
        self.assertEqual([], result.failures)
        self.assertEqual("output", result.side["status"])

        def overlay_rejects(raw):
            raw["Operations"][1]["OverlapPolicy"] = "Reject"

        def overlay_unknown_policy(raw):
            raw["Operations"][1]["OverlapPolicy"] = "Allow"

        def only_the_earlier_declares(raw):
            raw["Operations"][0]["OverlapPolicy"] = "ReplaceExisting"
            raw["Operations"][1]["OverlapPolicy"] = "Reject"

        for change in (overlay_rejects, overlay_unknown_policy, only_the_earlier_declares):
            with self.subTest(change=change.__name__):
                raw = merge_report(committed=False, overlay=True)
                change(raw)
                result = self.merge_side([self.merge_capture("preview", raw)])
                self.assertEqual(("PREDECESSOR_REPORT_INVALID", "preview", "PARITY_REPORT_RANGE_INVALID"),
                                 result.failures[0])

    def test_declared_overlap_is_per_address_space_and_the_terminal_default_is_unchanged(self):
        overlay = comparison.read_cli_report(merge_report(committed=False, overlay=True), report_version="1x").projection
        capacities = {"dp-input": 8, "tp-input": 4, "output-image": 8, "work": 8}
        parity.validate_semantic_report_ranges(overlay, capacities, declared_overlap=True)
        with self.assertRaises(parity.ParityError) as found:
            parity.validate_semantic_report_ranges(overlay, capacities)
        self.assertEqual("PARITY_REPORT_RANGE_INVALID", found.exception.code)
        spaces = copy.deepcopy(overlay)
        second = spaces["compiledOperations"][1]
        second.update(overlapPolicy="Reject", targetSpaceId="work",
                      targetRange={**second["targetRange"], "addressSpace": "work"})
        spaces["compiledMutations"][1].update(targetSpaceId="work",
                                              targetRange={**spaces["compiledMutations"][1]["targetRange"], "addressSpace": "work"})
        parity.validate_semantic_report_ranges(spaces, capacities, declared_overlap=True)
        with self.assertRaises(parity.ParityError):
            parity.validate_semantic_report_ranges(spaces, capacities)

    def test_described_output_without_a_file_needs_committed_false(self):
        capacities = {"dp-input": 8, "tp-input": 4, "output-image": 8}
        for committed, expected in ((False, "ready"), (True, "invalid"), (None, "invalid"), ("false", "invalid"), (0, "invalid")):
            with self.subTest(committed=committed):
                capture = self.merge_capture("preview", merge_report(committed=committed))
                verdict = comparison.validation.side_execution_verdict([capture.evidence()], capacities=capacities, complete=False)
                self.assertEqual(expected, verdict.status)
                if expected == "invalid":
                    self.assertEqual(("PREDECESSOR_REPORT_INVALID", "preview", "report output differs from capture"),
                                     verdict.failures[0])
        with_file = self.merge_capture("preview", merge_report(committed=False), output=MERGE_OUTPUT)
        verdict = comparison.validation.side_execution_verdict([with_file.evidence()], capacities=capacities, complete=False)
        self.assertEqual(("PREDECESSOR_REPORT_INVALID", "preview", "output is uncommitted or has an error issue"),
                         verdict.failures[0])
        other = merge_report(committed=True)
        other["Output"]["Sha256"] = "0" * 64
        result = self.merge_side([self.merge_capture("preview", merge_report(committed=False)),
                                  self.merge_capture("build", other, output=MERGE_OUTPUT)])
        self.assertEqual(("PREDECESSOR_REPORT_INVALID", "build", "report output differs from capture"), result.failures[0])
        described = self.merge_side([self.merge_capture("preview", merge_report(committed=False)),
                                     self.merge_capture("build", merge_report(committed=False))])
        self.assertEqual(("PREDECESSOR_PROCESS_FAILED", "build", "successful Build has no captured output"),
                         described.failures[0])

    def test_active_interfaces_admit_formal_and_diagnostic(self):
        for mode in ("rolling", "v0916-1x"):
            for formal in (False, True):
                with self.subTest(mode=mode, formal=formal):
                    admission = comparison.admit_execution_contract(CONTRACT, amendment_path=AMENDMENT, mode=mode, formal=formal)
                    self.assertEqual(formal, admission.formal)
                    self.assertEqual(not formal, admission.diagnostic)
                    self.assertEqual([], admission.pending)

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
            if argv == ["dotnet", "--list-runtimes"]:
                return subprocess.CompletedProcess(argv, 0, RUNTIME_LIST, "")
            if argv == ["dotnet", "--info"]:
                return subprocess.CompletedProcess(argv, 0, HOST_INFO, "")
            if argv[1] == "build":
                closure = cwd / self.contract["executor"]["runtimeClosureRoot"]
                closure.mkdir(parents=True)
                (closure / "NvtFwCombiner.Cli.exe").write_bytes(b"synthetic apphost")
                write_synthetic_cli_graph(closure)
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
        self.assertEqual(self.contract["executor"]["restore"]["arguments"], host.calls[3][0])
        self.assertIn(f"-p:PathMap={git.detached[0][1]}=/_/src", host.calls[4][0])

    def test_failed_executor_build_prints_only_command_and_last_thirty_lines(self):
        arguments = ["dotnet", "build", "synthetic.csproj", "--no-restore"]
        stdout = b"\n".join(f"stdout-{index}".encode() for index in range(40)) + b"\xff\n"
        stderr = b"\n".join(f"stderr-{index}".encode() for index in range(40)) + b"\xff\n"
        self.runner.host = FakeProcessHost(
            lambda argv, cwd: subprocess.CompletedProcess(argv, 1, stdout, stderr))
        printed = io.StringIO()
        with redirect_stderr(printed), self.assertRaises(comparison.ExecutionError) as found:
            comparison._executor_process(self.runner, self.root, arguments)
        self.assertEqual("PREDECESSOR_EXECUTOR_INVALID", found.exception.code)
        self.assertEqual("Executor command failed: dotnet build synthetic.csproj\n"
                         "stdout (last 30 lines):\n" + "".join(f"stdout-{index}\n" for index in range(10, 39))
                         + "stdout-39\ufffd\n"
                         + "stderr (last 30 lines):\n" + "".join(f"stderr-{index}\n" for index in range(10, 39))
                         + "stderr-39\ufffd\n",
                         printed.getvalue())
        self.assertEqual(digest(stdout), self.runner.captures[-1].record["stdoutSha256"])
        self.assertEqual(digest(stderr), self.runner.captures[-1].record["stderrSha256"])
        self.assertNotIn("stdout", self.runner.captures[-1].record)
        self.assertNotIn("stderr", self.runner.captures[-1].record)

    def test_successful_executor_build_prints_nothing(self):
        printed = io.StringIO()
        with redirect_stderr(printed):
            capture = comparison._executor_process(
                self.runner, self.root, ["dotnet", "build", "synthetic.csproj"])
        self.assertEqual(0, capture.record["exitCode"])
        self.assertEqual("", printed.getvalue())

    def test_executor_capture_failure_prints_output_and_preserves_refusal(self):
        def change_settings(argv, cwd):
            (self.settings / "toolchain-runtime.v1.json").write_bytes(b"changed")
            return subprocess.CompletedProcess(argv, 0, "captured stdout", "captured stderr")
        self.runner.host = FakeProcessHost(change_settings)
        printed = io.StringIO()
        with redirect_stderr(printed), self.assertRaises(comparison.ExecutionError) as found:
            comparison._executor_process(self.runner, self.root, ["dotnet", "build", "synthetic.csproj"])
        self.assertEqual("PREDECESSOR_ENVIRONMENT_INVALID", found.exception.code)
        self.assertEqual("Executor command failed: dotnet build synthetic.csproj\n"
                         "stdout (last 30 lines):\ncaptured stdout\n"
                         "stderr (last 30 lines):\ncaptured stderr\n", printed.getvalue())

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

    def test_executor_refuses_incomplete_in_effect_compiler_host(self):
        contract = copy.deepcopy(self.contract)
        contract["executor"]["compilerHost"].pop("requiredRuntime")
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

    def test_ctrlram_inputs_are_staged_and_expected_in_the_cli_report_order(self):
        """Real shape: the CLI sorts its Replace bindings by slot id; a reviewed binding names normal, vn, diff."""
        payloads = {"base": b"B" * 16, "normal": b"N" * 12, "vn": b"V" * 4, "diff": b"D" * 8}
        authority = parity.MaterializedCanonicalAuthority(
            self.root, "0" * 64, "golden/manifest.json", {f"golden/{name}.bin": payload for name, payload in payloads.items()})
        artifacts = {name: {"role": "input", "path": f"{name}.bin", "size": len(payload), "sha256": digest(payload)}
                     for name, payload in payloads.items()}
        bindings = [("base", "replace-base"), ("normal", "replace-ctrlram-normal"), ("vn", "replace-ctrlram-vn"),
                    ("diff", "replace-ctrlram-diff")]
        reported = [("reference-base", "base"), ("replace-ctrlram-diff", "diff"), ("replace-ctrlram-normal", "normal"),
                    ("replace-ctrlram-vn", "vn")]
        git = FakeGitHost()
        self.build_host(git)
        executor = comparison.build_1x_executor(git, self.runner, "1" * 40, self.contract)
        request = {"workflowId": "ctrlram-replace", "profileId": "test", "cliSelectionToken": "cascade"}

        def span(start, end):
            return {"Start": start, "Length": end - start, "EndExclusive": end}

        def written(order):
            raw = report(preview=True)
            raw.update(ModeId="ctrlram-replace", ExperienceId="ctrlram-replace", CompositionKind="Replace",
                       Output={"FileName": "output.bin", "Size": 16, "Sha256": "b" * 64, "Committed": False},
                       Inputs=[{"AddressSpaceId": space, "ArtifactId": space, "Size": len(payloads[name]),
                                "Sha256": digest(payloads[name]), "OriginalFileName": None} for space, name in order],
                       Operations=[], Mutations=[])
            for sequence, (space, start, end) in enumerate((("replace-ctrlram-normal", 0, 6), ("replace-ctrlram-vn", 6, 10),
                                                            ("replace-ctrlram-diff", 10, 16))):
                operation = copy.deepcopy(report()["Operations"][0])
                operation.update(OperationId=f"replace-{sequence}", Sequence=100 + sequence, Kind="ReplaceRange",
                                 SourceSpaceId=space, SourceRange=span(0, end - start), TargetRange=span(start, end))
                raw["Operations"].append(operation)
                raw["Mutations"].append({"OperationId": operation["OperationId"], "Kind": "ReplaceRange",
                                         "TargetSpaceId": "output-image", "TargetRange": span(start, end),
                                         "ChangedByteCount": 0, "BeforeSha256": "1" * 64, "AfterSha256": "1" * 64,
                                         "Reason": "synthetic"})
            return raw

        for order, expected in ((reported, "ready"), ([reported[0], reported[2], reported[3], reported[1]], "invalid")):
            with self.subTest(expected=expected):
                def cli(argv, cwd):
                    Path(argv[argv.index("--report") + 1]).write_text(json.dumps(written(order)), encoding="utf-8")
                    return subprocess.CompletedProcess(argv, 0, "", "")

                self.runner.host = FakeProcessHost(cli)
                capture = comparison.execute_cli_stage(self.runner, executor, request, authority, artifacts, bindings, stage="preview")
                argv = self.runner.host.calls[0][0]
                self.assertEqual(["replace-ctrlram-diff", "replace-ctrlram-normal", "replace-ctrlram-vn"],
                                 [value.split("=")[0] for value in argv if value.startswith("replace-ctrlram-")])
                self.assertEqual(["replace-base", "replace-ctrlram-diff", "replace-ctrlram-normal", "replace-ctrlram-vn"],
                                 [row["slotId"] for row in capture.inputs])
                self.assertEqual([0, 1, 2, 3], [row["order"] for row in capture.inputs])
                self.assertEqual([16, 8, 12, 4], [row["size"] for row in capture.inputs])
                evidence = capture.evidence()
                verdict = comparison.validation.side_execution_verdict(
                    [evidence], capacities=comparison.validation.execution_capacities(evidence), complete=False)
                self.assertEqual(expected, verdict.status)
                if expected == "invalid":
                    self.assertEqual("PREDECESSOR_REPORT_INVALID", verdict.failures[0].code)
        merge = [{"slotId": slot, "order": 9} for slot in ("tp-input", "dp-input")]
        self.assertEqual([("tp-input", 0), ("dp-input", 1)],
                         [(row["slotId"], row["order"]) for row in comparison.validation.report_ordered_inputs("standard-merge", merge)])

    def cli_stage_with_tools(self, tools, cli, custody=None):
        authority = parity.MaterializedCanonicalAuthority(self.root, "0" * 64, "golden/manifest.json", {"golden/input.bin": PAYLOAD})
        artifacts = {"input": {"role": "input", "path": "input.bin", "size": 8, "sha256": digest(PAYLOAD)}}
        git = FakeGitHost()
        git.files.update(tools)
        self.build_host(git)
        executor = comparison.build_1x_executor(git, self.runner, "1" * 40, self.contract)
        if custody is not None:
            self.runner.custody = custody
        self.runner.host = FakeProcessHost(cli)
        request = {"workflowId": "standard-merge", "profileId": "test", "cliSelectionToken": None}
        return executor, comparison.execute_cli_stage(self.runner, executor, request, authority, artifacts,
                                                      [("input", "dp-input")], stage="preview")

    def test_cli_stage_gets_the_executor_commit_tools_beside_its_closure(self):
        """The CLI searches upwards from its base directory for `external-tools`; no per-user setting supplies them."""
        tools = {"external-tools/legacy-combiner/1.13.0/Combiner.exe": b"synthetic tool",
                 "external-tools/legacy-combiner/1.13.0/manifest.json": b'{"toolId":"legacy-combiner"}',
                 "external-tools/catalog.json": b"{}"}
        seen = {}

        def cli(argv, cwd):
            base = Path(argv[0]).parent
            seen["search"] = [parent / "external-tools" for parent in (base, *base.parents) if (parent / "external-tools").is_dir()][0]
            seen["files"] = {path.relative_to(cwd).as_posix(): path.read_bytes() for path in seen["search"].rglob("*") if path.is_file()}
            seen["writable"] = [path for path in seen["search"].rglob("*") if path.is_file() and os.access(path, os.W_OK)]
            return subprocess.CompletedProcess(argv, 0, "", "")

        executor, capture = self.cli_stage_with_tools(tools, cli)
        self.assertEqual(tools, dict(executor.external_tools))
        self.assertEqual(Path(self.runner.host.calls[0][1]) / "external-tools", seen["search"])
        self.assertEqual(tools, seen["files"])
        self.assertEqual([], seen["writable"])
        self.assertEqual("runtime", Path(self.runner.host.calls[0][0][0]).parent.name)
        self.assertNotIn("PREDECESSOR_EXECUTOR_INVALID", [failure.code for failure in capture.failures])
        measured = parity.runtime_closure_inventory(executor.closure.root, cli_relative=executor.closure.cli_relative)
        self.assertEqual(measured.identity_sha256, executor.identity["runtimeClosureSha256"])
        self.assertFalse(any(path.startswith("external-tools") for path in executor.closure.files))

    def test_cli_stage_without_a_tool_still_owns_the_search_directory(self):
        seen = {}

        def cli(argv, cwd):
            seen["entries"] = list((cwd / "external-tools").iterdir())
            return subprocess.CompletedProcess(argv, 0, "", "")

        executor, _ = self.cli_stage_with_tools({}, cli)
        self.assertEqual({}, dict(executor.external_tools))
        self.assertEqual([], seen["entries"])

    def test_changed_or_removed_staged_tool_is_an_executor_failure(self):
        tools = {"external-tools/legacy-combiner/1.13.0/Combiner.exe": b"synthetic tool"}
        for fault in ("changed", "removed"):
            with self.subTest(fault=fault):
                def cli(argv, cwd):
                    target = cwd / "external-tools/legacy-combiner/1.13.0/Combiner.exe"
                    target.chmod(0o600)
                    if fault == "changed":
                        target.write_bytes(b"another tool")
                    else:
                        target.unlink()
                    return subprocess.CompletedProcess(argv, 0, "", "")

                _, capture = self.cli_stage_with_tools(tools, cli, custody=lambda paths: nullcontext())
                self.assertEqual(("PREDECESSOR_EXECUTOR_INVALID", "preview", "execution closure changed or disappeared"),
                                 capture.failures[0])
                self.assertEqual("invalid", comparison.assemble_side_result([capture], capacities={}).side["status"])


    # Decision 261: the three per-side rules, on the report shapes a CLI writes.

    def written_stages(self, workflow, payloads, bindings, write, *, token=None, stages=("preview", "build"),
                       output=PROCESSED):
        """Run Preview and Build through the real staging; `write(action, staging, temporary)` returns the report."""
        authority = parity.MaterializedCanonicalAuthority(
            self.root, "0" * 64, "golden/manifest.json", {f"golden/{name}.bin": payload for name, payload in payloads.items()})
        artifacts = {name: {"role": "input", "path": f"{name}.bin", "size": len(payload), "sha256": digest(payload)}
                     for name, payload in payloads.items()}
        git = FakeGitHost()
        git.files.update(TOOLS)
        self.build_host(git)
        executor = comparison.build_1x_executor(git, self.runner, "1" * 40, self.contract)
        request = {"workflowId": workflow, "profileId": "test", "cliSelectionToken": token}

        def cli(argv, cwd):
            action = argv[2]
            raw = write(action, cwd, Path(os.environ["TEMP"]))
            Path(argv[argv.index("--report") + 1]).write_text(json.dumps(raw), encoding="utf-8")
            if action == "build" and output is not None:
                Path(argv[argv.index("--output") + 1]).write_bytes(output)
            return subprocess.CompletedProcess(argv, 0, "", "")

        captures = []
        for stage in stages:
            self.runner.host = FakeProcessHost(cli)
            captures.append(comparison.execute_cli_stage(self.runner, executor, request, authority, artifacts, bindings, stage=stage))
        return captures

    def processor_side(self, change=None, *, only=None, stages=("preview", "build")):
        def write(action, staging, temporary):
            working = temporary / "nvt-fw-combiner" / "external-tools" / f"synthetic-{action}.postbuild-single"
            raw = written_1x_processor_report(
                committed=action == "build", tool=staging / TOOL, working=working, base_sha256=digest(BASE),
                replacement_sha256=digest(NF), output_sha256=digest(PROCESSED))
            if change is not None and only in (None, action):
                change(raw, staging, temporary, working)
            return raw

        captures = self.written_stages("ctrlram-replace", {"base": BASE, "nf": NF},
                                       [("base", "replace-base"), ("nf", "replace-ctrlram-nf")], write,
                                       token="single", stages=stages)
        return captures, self.merge_side(captures)

    def ab_side(self, *, combiner=False, change=None, only=None):
        def write(action, staging, temporary):
            working = temporary / "nvt-fw-combiner" / "external-tools" / f"synthetic-{action}.run-ab-combiner"
            raw = written_1x_ab_merge_report(
                committed=action == "build", dp_ab_sha256=digest(DP_AB), tp_b_sha256=digest(TP_B),
                output_sha256=digest(PROCESSED), combiner=(staging / TOOL, working) if combiner else None)
            if change is not None and only in (None, action):
                change(raw)
            return raw

        captures = self.written_stages("ab-merge", {"dpab": DP_AB, "tpb": TP_B},
                                       [("dpab", "dp-ab-input"), ("tpb", "tp-b-input")], write)
        return captures, self.merge_side(captures)

    def assert_refused(self, result, stage, detail):
        self.assertEqual("invalid", result.side["status"])
        self.assertEqual(("PREDECESSOR_REPORT_INVALID", stage, detail), result.failures[0])

    def test_written_processor_report_gives_an_output_side_and_keeps_a_repeated_command(self):
        captures, result = self.processor_side()
        self.assertEqual([], result.failures)
        self.assertEqual("output", result.side["status"])
        self.assertEqual({"size": 16, "sha256": digest(PROCESSED)}, result.side["output"])
        for capture in captures:
            commands = capture.report.projection["compiledOperations"][1]["executedCommands"]
            self.assertEqual([0, 1, 2], [row["sequence"] for row in commands])
            self.assertEqual({TOOL}, {row["executablePackagePath"] for row in commands})
            self.assertEqual(commands[0]["canonicalArgumentsSha256"], commands[1]["canonicalArgumentsSha256"])
            self.assertNotEqual(commands[0]["canonicalArgumentsSha256"], commands[2]["canonicalArgumentsSha256"])
            self.assertEqual([{"start": 0, "endExclusive": 2}, {"start": 12, "endExclusive": 13}],
                             capture.report.context["outputDifferenceRanges"])
            self.assertNotIn(PREVIEW_MARKER, repr(capture.report))
            self.assertEqual(2, len(capture.staged_tools))
        preview, build = (capture.report.projection["compiledOperations"] for capture in captures)
        self.assertEqual(preview, build)

    def test_executable_must_be_a_staged_hash_checked_tool(self):
        def beside_the_staging(raw, staging, temporary, working):
            for command in raw["Operations"][1]["ExecutedCommands"]:
                command["ExecutablePath"] = str(staging.parent / "other" / TOOL)

        def unstaged_file_in_the_tool_root(raw, staging, temporary, working):
            raw["Operations"][1]["ExecutedCommands"][2]["ExecutablePath"] = str(staging / TOOL).replace("Combiner.exe", "Other.exe")

        def runtime_closure_file(raw, staging, temporary, working):
            raw["Operations"][1]["ExecutedCommands"][0]["ExecutablePath"] = str(staging / "runtime" / "external-tools" / "Combiner.exe")

        for change in (beside_the_staging, unstaged_file_in_the_tool_root, runtime_closure_file):
            with self.subTest(change=change.__name__):
                _, result = self.processor_side(change)
                self.assert_refused(result, "preview", "executed command is not a staged external tool")

        def no_tool_folder(raw, staging, temporary, working):
            raw["Operations"][1]["ExecutedCommands"][0]["ExecutablePath"] = str(staging / "runtime" / "Combiner.exe")

        _, result = self.processor_side(no_tool_folder)
        self.assert_refused(result, "preview", "written report format invalid")

    def test_file_arguments_and_working_directory_stay_inside_the_process_staging(self):
        def argument_outside(raw, staging, temporary, working):
            raw["Operations"][1]["ExecutedCommands"][2]["Arguments"][1] = str(staging / "inputs" / "00-base.bin")

        def argument_steps_back(raw, staging, temporary, working):
            raw["Operations"][1]["ExecutedCommands"][2]["Arguments"][1] = str(working / ".." / "other.bin")

        def no_arguments(raw, staging, temporary, working):
            raw["Operations"][1]["ExecutedCommands"][2]["Arguments"] = []

        def no_commands(raw, staging, temporary, working):
            raw["Operations"][1]["ExecutedCommands"] = []

        for change in (argument_outside, argument_steps_back, no_arguments, no_commands):
            with self.subTest(change=change.__name__):
                _, result = self.processor_side(change)
                self.assert_refused(result, "preview", "written report format invalid")

        def works_in_the_staging_root(raw, staging, temporary, working):
            for command in raw["Operations"][1]["ExecutedCommands"]:
                command["WorkingDirectory"] = str(staging / "work")
                command["Arguments"] = [value.replace(str(working), str(staging / "work")) for value in command["Arguments"]]

        def works_in_the_temporary_directory_itself(raw, staging, temporary, working):
            for command in raw["Operations"][1]["ExecutedCommands"]:
                command["WorkingDirectory"] = str(temporary)
                command["Arguments"] = [value.replace(str(working), str(temporary)) for value in command["Arguments"]]

        for change in (works_in_the_staging_root, works_in_the_temporary_directory_itself):
            with self.subTest(change=change.__name__):
                _, result = self.processor_side(change)
                self.assert_refused(result, "preview", "executed command worked outside the process temporary directory")

    def test_build_commands_are_compared_with_the_preview_in_order(self):
        def reorder(raw, staging, temporary, working):
            raw["Operations"][1]["ExecutedCommands"].reverse()

        def drop_the_repeat(raw, staging, temporary, working):
            del raw["Operations"][1]["ExecutedCommands"][1]

        for change in (reorder, drop_the_repeat):
            with self.subTest(change=change.__name__):
                _, result = self.processor_side(change, only="build")
                self.assert_refused(result, "build", "PARITY_PROVENANCE_INVALID")

    def test_output_differences_are_audited_against_the_preview_allowed_write_ranges(self):
        def outside_every_write_range(raw, staging, temporary, working):
            raw["OutputDifferences"].append(written_output_difference(3, 8, 10))

        def across_two_write_ranges(raw, staging, temporary, working):
            raw["OutputDifferences"][0]["Range"] = {"Start": 0, "Length": 6, "EndExclusive": 6}

        def empty_range(raw, staging, temporary, working):
            raw["OutputDifferences"][0]["Range"] = {"Start": 1, "Length": 0, "EndExclusive": 1}

        for change in (outside_every_write_range, across_two_write_ranges, empty_range):
            for only in ("preview", "build"):
                with self.subTest(change=change.__name__, only=only):
                    _, result = self.processor_side(change, only=only)
                    self.assert_refused(result, only, "output difference outside every write range the Preview allows")

        def none_listed(raw, staging, temporary, working):
            raw["OutputDifferences"] = []

        for only in ("preview", "build"):
            with self.subTest(change="none_listed", only=only):
                _, result = self.processor_side(none_listed, only=only)
                self.assert_refused(result, only, "processor changed bytes without a listed output difference")

        def widened_with_the_difference(raw, staging, temporary, working):
            raw["Operations"][1]["ProcessorAllowedWriteRanges"][0] = {"Start": 0, "Length": 3, "EndExclusive": 3}
            raw["OutputDifferences"][0]["Range"] = {"Start": 0, "Length": 3, "EndExclusive": 3}

        _, result = self.processor_side(widened_with_the_difference, only="build")
        self.assert_refused(result, "build", "PARITY_PROVENANCE_INVALID")

        def nothing_changed(raw, staging, temporary, working):
            raw["OutputDifferences"] = []
            raw["Mutations"][1].update(ChangedByteCount=0, AfterSha256=raw["Mutations"][1]["BeforeSha256"])

        _, result = self.processor_side(nothing_changed)
        self.assertEqual("output", result.side["status"])

        def malformed_range(raw, staging, temporary, working):
            raw["OutputDifferences"][0]["Range"] = {"Start": 0, "Length": 3, "EndExclusive": 2}

        def missing_range(raw, staging, temporary, working):
            del raw["OutputDifferences"][0]["Range"]

        for change in (malformed_range, missing_range):
            with self.subTest(change=change.__name__):
                _, result = self.processor_side(change)
                self.assert_refused(result, "preview", "written report format invalid")

    def test_difference_without_any_processor_is_refused(self):
        raw = merge_report(committed=False)
        raw["OutputDifferences"] = [written_output_difference(1, 0, 2)]
        result = self.merge_side([self.merge_capture("preview", raw)])
        self.assert_refused(result, "preview", "output difference outside every write range the Preview allows")

    def test_work_address_space_ranges_are_held_to_the_preview(self):
        captures, result = self.ab_side()
        self.assertEqual([], result.failures)
        self.assertEqual("output", result.side["status"])
        self.assertNotIn("tp-b-work", comparison.validation.execution_capacities(captures[0].evidence()))
        self.assertEqual(16, comparison.validation.execution_capacities(captures[0].evidence())["output-image"])

        def undeclared_work_space(raw):
            for row in (raw["Operations"][1], raw["Mutations"][1]):
                row["TargetSpaceId"] = "a-bank-work"
            raw["Operations"][1]["SourceSpaceId"] = "a-bank-work"

        _, result = self.ab_side(change=undeclared_work_space)
        self.assert_refused(result, "preview", "PARITY_REPORT_RANGE_INVALID")

        def build_mutation_beyond_the_preview(raw):
            raw["Mutations"][1]["TargetRange"] = {"Start": 4, "Length": 8, "EndExclusive": 12}

        _, result = self.ab_side(change=build_mutation_beyond_the_preview, only="build")
        self.assertEqual("invalid", result.side["status"])
        self.assertEqual(("PREDECESSOR_REPORT_INVALID", "build"), result.failures[0][:2])

    def test_work_range_rule_of_the_range_function(self):
        preview = comparison.read_cli_report(
            written_1x_ab_merge_report(committed=False, dp_ab_sha256="a" * 64, tp_b_sha256="b" * 64, output_sha256="c" * 64),
            report_version="1x").projection
        capacities = {"dp-ab-input": 16, "tp-b-input": 8, "output-image": 16}
        declared = comparison.validation._declared_work_ranges(preview)
        self.assertEqual({"ab-combiner-work": [], "tp-b-work": [(4, 8), (4, 8), (0, 8)]}, declared)
        options = dict(declared_overlap=True, audited_processor_writes=True)
        parity.validate_semantic_report_ranges(preview, capacities, declared_work_ranges=declared, **options)
        for missing in (None, {}, {"tp-b-work": []}, {"tp-b-work": [(4, 8)]}, {"tp-b-work": [(0, 4), (4, 8)]}):
            with self.subTest(declared=missing), self.assertRaises(parity.ParityError) as found:
                parity.validate_semantic_report_ranges(preview, capacities, declared_work_ranges=missing, **options)
            self.assertEqual("PARITY_REPORT_RANGE_INVALID", found.exception.code)
        build = copy.deepcopy(preview)
        build["compiledMutations"][1]["targetRange"].update(endExclusive=12)
        build["compiledOperations"][1]["targetRange"].update(endExclusive=12)
        build["compiledOperations"][1]["sourceRange"].update(endExclusive=12)
        with self.assertRaises(parity.ParityError):
            parity.validate_semantic_report_ranges(build, capacities, declared_work_ranges=declared, **options)
        measured = {**capacities, "tp-b-work": 6}
        with self.assertRaises(parity.ParityError):
            parity.validate_semantic_report_ranges(preview, measured, declared_work_ranges=declared, **options)

    def test_processor_that_writes_a_work_address_space_lists_no_difference_and_is_refused(self):
        """The NT51950 AB Merge shape. Decision 261 audits output differences; this report lists none."""
        captures, result = self.ab_side(combiner=True)
        self.assert_refused(result, "preview", "processor changed bytes without a listed output difference")
        self.assertEqual([], captures[0].report.context["outputDifferenceRanges"])

    def test_terminal_defaults_still_refuse_the_written_shapes(self):
        captures, _ = self.processor_side(stages=("preview",))
        raw = written_1x_processor_report(
            committed=False, tool=self.root / TOOL, working=self.root / "tmp" / "run", base_sha256="a" * 64,
            replacement_sha256="b" * 64, output_sha256="c" * 64)
        with self.assertRaises(parity.ParityError):
            parity.normalize_raw_operation(raw["Operations"][1])
        projection = captures[0].report.projection
        capacities = {"reference-base": 16, "replace-ctrlram-nf": 4, "output-image": 16}
        parity.validate_semantic_report_ranges(projection, capacities, declared_overlap=True, audited_processor_writes=True)
        with self.assertRaises(parity.ParityError):
            parity.validate_semantic_report_ranges(projection, capacities, declared_overlap=True)


if __name__ == "__main__":
    unittest.main()

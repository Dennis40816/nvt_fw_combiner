"""Decisions 271-274: comparator-only admission of written report shapes."""

from __future__ import annotations

import copy
from pathlib import Path
import unittest
from unittest.mock import patch

from scripts import predecessor_comparison as comparison
from scripts import predecessor_validation as validation
from scripts import v0916_parity_certification as parity
from tests.scripts.predecessor_test_support import (
    _written_mutation, _written_operation, _written_report,
    written_1x_ab_merge_report, written_output_difference,
)
from tests.scripts.test_predecessor_report_reader import raw_report


TEMPORARY = Path("C:/synthetic/temp")
TOOL = Path("C:/synthetic/external-tools/tool.exe")
CAPACITIES = {"dp-ab-input": 16, "tp-b-input": 8, "source": 8, "output-image": 16}


def evidence(raw, stage="preview", *, exit_code=0, output=None, report_version="1x"):
    read = comparison.read_cli_report(raw, report_version=report_version)
    inputs = [{**row, "expectedReportArtifactId": row["artifactId"],
               "expectedReportAddressSpaceId": row["addressSpaceId"]} for row in read.context["orderedInputs"]]
    process = {"stage": stage, "exitCode": exit_code, "timedOut": False, "report": {"size": 1, "sha256": "a" * 64}}
    return validation.SideProcessEvidence(process, read.projection, read.context, read.issues, inputs, output, [], False,
                                          [str(TOOL)], str(TEMPORARY))


def ab_report(*, processor=False):
    return written_1x_ab_merge_report(
        committed=False, dp_ab_sha256="a" * 64, tp_b_sha256="b" * 64, output_sha256="c" * 64,
        combiner=(TOOL, TEMPORARY / "work") if processor else None)


def rejected_report():
    raw = raw_report()
    raw.update(Mutations=[], Output=None, OutputDifferences=[],
               Issues=[{"Code": "product.rejected", "Severity": "Error"}])
    for operation in raw["Operations"]:
        operation["Status"] = "Skipped"
    return raw


def verdict(raw, *, exit_code=0, output=None, v0916_executor=False):
    return validation.side_execution_verdict([evidence(raw, exit_code=exit_code, output=output)],
                                              capacities=CAPACITIES, complete=False,
                                              **({"v0916_executor": True} if v0916_executor else {}))


def whole_bank_report(*, space="ab-combiner-work", start=262144, end=524288):
    """Synthetic written shape of the v0.9.16 NT51950 AB Merge whole-bank import."""
    command = {"ExecutablePath": str(TOOL), "WorkingDirectory": str(TEMPORARY / "work"),
               "Arguments": ["AB_MODE", str(TEMPORARY / "work" / "output.bin")]}
    operations = [
        _written_operation("copy-dp-ab-image", 100, "CopyRange", "Reject",
                           ("dp-ab-input", 0, 524288), ("output-image", 0, 524288)),
        _written_operation("copy-a-bank-to-combiner-work", 600, "CopyRange", "Reject",
                           ("output-image", 0, 262144), (space, 0, 262144)),
        _written_operation("copy-b-bank-to-combiner-work", 650, "CopyRange", "Reject",
                           ("output-image", 262144, 524288), (space, 262144, 524288)),
        _written_operation("run-nt51950-ab-combiner", 700, "RunExternalProcessor", "ReplaceExisting", None,
                           (space, 0, 524288), processor={
                               "id": "nfc.synthetic.ab-combiner-v1",
                               "writes": [(303360, 303364), (303376, 303380), (303408, 303412)],
                               "commands": [command]}),
        _written_operation("copy-postbuild-b-bank-to-output", 900, "CopyRange", "ReplaceExisting",
                           (space, start, end), ("output-image", 524288 - (end - start), 524288)),
    ]
    return _written_report(
        "ab-merge", "Merge", [("dp-ab-input", 524288, "a" * 64)], operations,
        [_written_mutation(operation, 6 if operation["Kind"] == "RunExternalProcessor" else 0,
                           same=operation["Kind"] != "RunExternalProcessor") for operation in operations], [],
        committed=False, output_size=524288, output_sha256="c" * 64)


class WholeBankDecision278Tests(unittest.TestCase):
    def check_report(self, raw, *, report_version="v0916", stage="preview"):
        item = evidence(raw, stage, report_version=report_version)
        capacities = {"dp-ab-input": 524288, "output-image": 524288}
        return validation.side_execution_verdict(
            [item], capacities=capacities, complete=False, v0916_executor=report_version == "v0916")

    def assert_read_refused(self, raw, *, report_version="v0916"):
        result = self.check_report(raw, report_version=report_version)
        self.assertEqual(("invalid", [validation.Failure(
            "PREDECESSOR_REPORT_INVALID", "preview",
            "later work-space read outside every processor allowed write range")]),
            (result.status, result.failures))

    def test_v0916_whole_b_bank_with_three_four_byte_writes_is_accepted(self):
        result = self.check_report(whole_bank_report())
        self.assertEqual(("ready", []), (result.status, result.failures))

    def test_v0916_whole_bank_read_refuses_one_byte_more_or_less_at_either_end(self):
        for start, end in ((262143, 524288), (262145, 524288), (262144, 524287), (262144, 524289)):
            with self.subTest(start=start, end=end):
                self.assert_read_refused(whole_bank_report(start=start, end=end))

    def test_v0916_whole_a_bank_read_is_refused(self):
        self.assert_read_refused(whole_bank_report(start=0, end=262144))

    def test_v0916_later_write_to_combiner_work_is_still_refused(self):
        raw = whole_bank_report()
        raw["Operations"][-1]["TargetSpaceId"] = "ab-combiner-work"
        raw["Mutations"][-1]["TargetSpaceId"] = "ab-combiner-work"
        result = self.check_report(raw)
        self.assertEqual(("invalid", [validation.Failure(
            "PREDECESSOR_REPORT_INVALID", "preview",
            "later operation writes the processor work address space")]), (result.status, result.failures))

    def test_1x_whole_b_bank_read_is_refused_with_or_without_payload_version_claim(self):
        for profile_version in ("0.7.0", "v0.9.16"):
            with self.subTest(profile_version=profile_version):
                raw = whole_bank_report()
                raw["ProfileVersion"] = profile_version
                self.assert_read_refused(raw, report_version="1x")

    def test_v0916_whole_bank_read_in_other_work_spaces_is_refused(self):
        for space in ("tp-b-work", "a-bank-work", "b-bank-work"):
            with self.subTest(space=space):
                self.assert_read_refused(whole_bank_report(space=space))

    def test_executor_gate_reaches_incremental_and_final_preview_build_audits(self):
        preview = whole_bank_report()
        build = copy.deepcopy(preview)
        build["Output"]["Committed"] = True
        for version, status in (("v0916", "output"), ("1x", "invalid")):
            with self.subTest(report_version=version):
                captures = []
                for stage, raw, output in (("preview", preview, None),
                                           ("build", build, {"size": 524288, "sha256": "c" * 64})):
                    read = comparison.read_cli_report(raw, report_version=version)
                    item = evidence(raw, stage, output=output, report_version=version)
                    captures.append(comparison.ProcessCapture(
                        item.process, read, item.inputs, output, None, b"", b"", [], False,
                        None, (str(TOOL),), str(TEMPORARY)))
                executor = comparison.Executor({}, None, version, {})
                with patch.object(comparison, "execute_cli_stage", side_effect=captures) as execute:
                    result = comparison.execute_side_stages(None, executor, {}, None, {}, [])
                self.assertEqual(status, result.result.side["status"])
                self.assertEqual(2 if version == "v0916" else 1, execute.call_count)
                self.assertEqual([] if version == "v0916" else [validation.Failure(
                    "PREDECESSOR_REPORT_INVALID", "preview",
                    "later work-space read outside every processor allowed write range")], result.result.failures)

    def test_whole_bank_exception_does_not_admit_build_ranges_different_from_preview(self):
        preview = whole_bank_report()
        build = whole_bank_report(start=262143)
        build["Output"]["Committed"] = True
        pair = [evidence(preview, report_version="v0916"), evidence(
            build, "build", output={"size": 524288, "sha256": "c" * 64}, report_version="v0916")]
        result = validation.side_execution_verdict(
            pair, capacities={"dp-ab-input": 524288, "output-image": 524288}, v0916_executor=True)
        self.assertEqual("invalid", result.status)
        self.assertEqual("build", result.stopped_at)
        self.assertEqual("PREDECESSOR_REPORT_INVALID", result.failures[0].code)

    def test_output_comparison_still_counts_bytes_outside_processor_allowed_writes(self):
        baseline = bytes(524288)
        candidate = bytearray(baseline)
        candidate[262144] = 1
        candidate[524287] = 2
        result = comparison.compare_output_bytes(baseline, bytes(candidate))
        self.assertEqual(2, result.different_byte_count)
        self.assertEqual([{"start": 262144, "endExclusive": 262145},
                          {"start": 524287, "endExclusive": 524288}], result.ranges)


class OwnerAnswersTests(unittest.TestCase):
    def test_zero_work_space_processor_count_requires_equal_hashes_in_each_stage(self):
        for same_hash in (False, True):
            with self.subTest(same_hash=same_hash):
                preview = ab_report(processor=True)
                mutation = preview["Mutations"][4]
                mutation["ChangedByteCount"] = 0
                if same_hash:
                    mutation["AfterSha256"] = mutation["BeforeSha256"]
                build = copy.deepcopy(preview)
                build["Output"]["Committed"] = True
                pair = [evidence(preview), evidence(build, "build", output={"size": 16, "sha256": "c" * 64})]
                result = validation.side_execution_verdict(pair, capacities=CAPACITIES)
                self.assertEqual("output" if same_hash else "invalid", result.status)
                if same_hash:
                    self.assertEqual([], result.failures)
                else:
                    self.assertEqual([validation.Failure("PREDECESSOR_REPORT_INVALID", "preview",
                        "zero processor changed-byte count has differing hashes")], result.failures)

    def assert_work_processor_accepted(self):
        result = verdict(ab_report(processor=True))
        self.assertEqual(("ready", []), (result.status, result.failures))

    def test_work_processor_accepts_later_reads_inside_each_single_write_range(self):
        self.assert_work_processor_accepted()
        raw = ab_report(processor=True)
        # Two adjacent allowed ranges remain distinct; each later read fits one.
        raw["Operations"][4]["ProcessorAllowedWriteRanges"] = [
            {"Start": 10, "Length": 1, "EndExclusive": 11}, {"Start": 11, "Length": 1, "EndExclusive": 12}]
        first = raw["Operations"][5]
        first["SourceRange"] = first["TargetRange"] = {"Start": 10, "Length": 1, "EndExclusive": 11}
        second = copy.deepcopy(first)
        second.update(OperationId="import-second", Sequence=900,
                      SourceRange={"Start": 11, "Length": 1, "EndExclusive": 12},
                      TargetRange={"Start": 11, "Length": 1, "EndExclusive": 12})
        raw["Operations"].append(second)
        raw["Mutations"] = []
        # A processor report needs its mutation row even with zero changed bytes.
        original = ab_report(processor=True)
        raw["Mutations"] = original["Mutations"][:5]
        self.assertEqual("ready", verdict(raw).status)

    def test_work_processor_refuses_uncontained_straddling_writes_and_unclassifiable_operations(self):
        self.assert_work_processor_accepted()
        for shape in ("outside", "outside-zero-change", "straddles", "write", "unknown", "missing-range", "unnamed-space"):
            with self.subTest(shape=shape):
                raw = ab_report(processor=True)
                later = raw["Operations"][5]
                if shape in ("outside", "outside-zero-change"):
                    later["SourceRange"] = {"Start": 8, "Length": 2, "EndExclusive": 10}
                    if shape == "outside-zero-change":
                        raw["Mutations"][4].update(ChangedByteCount=0, AfterSha256="1" * 64)
                elif shape == "straddles":
                    raw["Operations"][4]["ProcessorAllowedWriteRanges"] = [
                        {"Start": 10, "Length": 1, "EndExclusive": 11},
                        {"Start": 11, "Length": 1, "EndExclusive": 12}]
                elif shape == "write":
                    later["TargetSpaceId"] = "ab-combiner-work"
                    raw["Mutations"][5]["TargetSpaceId"] = "ab-combiner-work"
                elif shape == "unknown":
                    later["Kind"] = raw["Mutations"][5]["Kind"] = "FutureOperation"
                elif shape == "missing-range":
                    later["SourceRange"] = None
                else:
                    later["SourceSpaceId"] = None
                result = verdict(raw)
                self.assertEqual("invalid", result.status)
                self.assertEqual("PREDECESSOR_REPORT_INVALID", result.failures[0].code)
                # Isolate the named processor audit from the shared range checker:
                # unnamed spaces can also fail that earlier checker.
                measured = evidence(raw)
                details = validation._processor_write_audit_failures(
                    "preview", measured.projection, measured.context, measured.projection,
                    declared_work_ranges=validation._declared_work_ranges(measured.projection))
                expected = {
                    "outside": "later work-space read outside every processor allowed write range",
                    "outside-zero-change": "later work-space read outside every processor allowed write range",
                    "straddles": "later work-space read outside every processor allowed write range",
                    "write": "later operation writes the processor work address space",
                    "unknown": "unknown later operation kind in work-space processor audit",
                    "missing-range": "later operation has no named read range",
                    "unnamed-space": "later operation has no named read range",
                }
                self.assertEqual([validation.Failure("PREDECESSOR_REPORT_INVALID", "preview", expected[shape])], details)

    def test_work_processor_audit_refuses_wrong_kind_and_later_processor_without_declaration(self):
        for shape, detail in (
            ("wrong-kind", "unknown work-space processor operation kind"),
            ("undeclared-later-processor", "later processor has no declared ranges"),
        ):
            with self.subTest(shape=shape):
                measured = evidence(ab_report(processor=True))
                operations = measured.projection["compiledOperations"]
                if shape == "wrong-kind":
                    operations[4]["kind"] = "CopyRange"
                else:
                    operations[5]["kind"] = "RunExternalProcessor"
                result = validation._processor_write_audit_failures(
                    "preview", measured.projection, measured.context, measured.projection,
                    declared_work_ranges=validation._declared_work_ranges(measured.projection))
                self.assertEqual([validation.Failure("PREDECESSOR_REPORT_INVALID", "preview", detail)], result)

    def test_all_skipped_output_processor_refuses_even_a_difference_inside_its_write_range(self):
        raw = ab_report(processor=True)
        raw.update(Mutations=[], Output=None, Issues=[{"Code": "product.rejected", "Severity": "Error"}])
        # Keep only the output container and an output-image processor.
        raw["Operations"] = [raw["Operations"][0], raw["Operations"][4]]
        raw["Operations"][1]["TargetSpaceId"] = "output-image"
        for operation in raw["Operations"]:
            operation.update(Status="Skipped", ExecutedCommands=[])
        self.assertEqual("rejected", verdict(raw, exit_code=1).status)
        raw["OutputDifferences"] = [written_output_difference(1, 10, 11)]
        result = verdict(raw, exit_code=1)
        self.assertEqual(("invalid", [validation.Failure(
            "PREDECESSOR_REPORT_INVALID", "preview",
            "Skipped operations do not satisfy no-write typed rejection conditions")]), (result.status, result.failures))

    def test_all_skipped_rejection_accepts_preview_and_build_without_writes(self):
        raw = rejected_report()
        self.assertEqual("rejected", verdict(raw, exit_code=1).status)
        preview = copy.deepcopy(raw)
        preview["Operations"][0]["Status"] = "Succeeded"
        preview["Issues"] = []
        preview["Output"] = {"FileName": "synthetic.bin", "Size": 8, "Sha256": "b" * 64, "Committed": False}
        pair = [evidence(preview), evidence(raw, "build", exit_code=1)]
        self.assertEqual("rejected", validation.side_execution_verdict(pair, capacities=CAPACITIES).status)

    def test_all_skipped_build_accepts_the_products_uncommitted_empty_output_description(self):
        raw = ab_report(processor=True)
        raw.update(Mutations=[], Issues=[{"Code": "product.rejected", "Severity": "Error"}])
        raw["Output"].update(Size=0, Sha256="e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", Committed=False)
        for operation in raw["Operations"]:
            operation.update(Status="Skipped", ExecutedCommands=[])
        pair = [evidence(ab_report(processor=True)), evidence(raw, "build", exit_code=1)]
        result = validation.side_execution_verdict(pair, capacities=CAPACITIES)
        self.assertEqual(("rejected", []), (result.status, result.failures))
        raw["Output"]["Committed"] = True
        result = validation.side_execution_verdict([pair[0], evidence(raw, "build", exit_code=1)], capacities=CAPACITIES)
        self.assertEqual("invalid", result.status)

    def test_build_rejection_after_succeeded_operations_and_mutations_remains_rejected(self):
        preview = ab_report(processor=True)
        build = copy.deepcopy(preview)
        build["Issues"] = [{"Code": "product.publication-blocked", "Severity": "Error"}]
        self.assertTrue(build["Mutations"])
        self.assertTrue(all(row["Status"] == "Succeeded" for row in build["Operations"]))
        result = validation.side_execution_verdict(
            [evidence(preview), evidence(build, "build", exit_code=1)], capacities=CAPACITIES)
        self.assertEqual(("rejected", []), (result.status, result.failures))

    def test_all_skipped_processors_without_commands_are_rejections_only_with_all_five_conditions(self):
        raw = ab_report(processor=True)
        raw.update(Mutations=[], Output=None, Issues=[{"Code": "product.rejected", "Severity": "Error"}])
        for operation in raw["Operations"]:
            operation.update(Status="Skipped", ExecutedCommands=[])
        self.assertEqual("rejected", verdict(raw, exit_code=1).status)
        pair = [evidence(ab_report(processor=True)), evidence(raw, "build", exit_code=1)]
        self.assertEqual("rejected", validation.side_execution_verdict(pair, capacities=CAPACITIES).status)
        for field in ("allowedReadRanges", "allowedWriteRanges"):
            with self.subTest(compiled_authority=field):
                changed = copy.deepcopy(pair)
                changed[1].projection["compiledOperations"][4]["processor"][field][0]["endExclusive"] -= 1
                self.assertEqual("invalid", validation.side_execution_verdict(changed, capacities=CAPACITIES).status)
        for shape in ("zero-exit", "no-error", "one-ran", "mutation", "command", "difference", "output"):
            with self.subTest(shape=shape):
                item = copy.deepcopy(raw)
                code, output = 1, None
                if shape == "zero-exit":
                    code = 0
                elif shape == "no-error":
                    item["Issues"] = []
                elif shape == "one-ran":
                    item["Operations"][0]["Status"] = "Succeeded"
                elif shape == "mutation":
                    item["Mutations"] = ab_report(processor=True)["Mutations"][:1]
                elif shape == "command":
                    item["Operations"][4]["ExecutedCommands"] = ab_report(processor=True)["Operations"][4]["ExecutedCommands"]
                elif shape == "difference":
                    item["OutputDifferences"] = [written_output_difference(1, 10, 11)]
                else:
                    output = {"size": 16, "sha256": "c" * 64}
                self.assertEqual("invalid", verdict(item, exit_code=code, output=output).status)
        # ADR 0057 still refuses the command-free processor at the format boundary.
        with self.assertRaises(parity.ParityError):
            parity.normalize_raw_operation(raw["Operations"][4])

    def test_all_skipped_rejection_refuses_each_missing_condition_and_each_write_record(self):
        self.assertEqual("rejected", verdict(rejected_report(), exit_code=1).status)
        for shape in ("zero-exit", "no-error", "one-ran", "mutation", "command", "difference", "output"):
            with self.subTest(shape=shape):
                raw = rejected_report()
                exit_code, output = 1, None
                if shape == "zero-exit":
                    exit_code = 0
                elif shape == "no-error":
                    raw["Issues"][0]["Severity"] = "Warning"
                elif shape == "one-ran":
                    raw["Operations"][0]["Status"] = "Succeeded"
                    skipped = copy.deepcopy(raw["Operations"][0])
                    skipped.update(OperationId="skipped-copy", Sequence=1, Status="Skipped",
                                   TargetRange={"Start": 8, "Length": 8, "EndExclusive": 16})
                    raw["Operations"].append(skipped)
                elif shape == "mutation":
                    raw["Mutations"] = raw_report()["Mutations"]
                elif shape == "command":
                    item = evidence(raw, exit_code=1)
                    item.projection["compiledOperations"][0]["executedCommands"] = [{"sequence": 0}]
                    result = validation.side_execution_verdict([item], capacities=CAPACITIES, complete=False)
                    self.assertEqual("invalid", result.status)
                    continue
                elif shape == "difference":
                    raw["OutputDifferences"] = [written_output_difference(1, 0, 1)]
                else:
                    output = {"size": 8, "sha256": "b" * 64}
                self.assertEqual("invalid", verdict(raw, exit_code=exit_code, output=output).status)

    def test_bank_work_names_are_admitted_only_for_the_v0916_executor_inside_preview_ranges(self):
        for space in ("a-bank-work", "b-bank-work"):
            with self.subTest(space=space):
                raw = ab_report()
                for operation in raw["Operations"]:
                    for member in ("SourceSpaceId", "TargetSpaceId"):
                        if operation[member] == "tp-b-work":
                            operation[member] = space
                for mutation in raw["Mutations"]:
                    if mutation["TargetSpaceId"] == "tp-b-work":
                        mutation["TargetSpaceId"] = space
                self.assertEqual("ready", verdict(raw, v0916_executor=True).status)
                self.assertEqual("invalid", verdict(raw).status)
                # Exercise both actual call sites: incremental stop and final assembly.
                build = copy.deepcopy(raw)
                build["Output"]["Committed"] = True
                captures = []
                for stage, item, output in (("preview", raw, None), ("build", build, {"size": 16, "sha256": "c" * 64})):
                    read = comparison.read_cli_report(item, report_version="v0916")
                    measured = evidence(item, stage, output=output, report_version="v0916")
                    captures.append(comparison.ProcessCapture(
                        measured.process, read, measured.inputs, output, None, b"", b"", [], False,
                        None, (str(TOOL),), str(TEMPORARY)))
                for identity, status in (("v0916", "output"), ("1x", "invalid")):
                    executor = comparison.Executor({}, None, identity, {})
                    with patch.object(comparison, "execute_cli_stage", side_effect=captures) as execute:
                        result = comparison.execute_side_stages(None, executor, {}, None, {}, [])
                    self.assertEqual(status, result.result.side["status"])
                    self.assertEqual(2 if identity == "v0916" else 1, execute.call_count)
                preview = evidence(raw, report_version="v0916")
                declared = validation._declared_work_ranges(preview.projection, v0916_executor=True)
                build = copy.deepcopy(preview.projection)
                build["compiledOperations"][1]["sourceRange"]["endExclusive"] = 9
                with self.assertRaises(parity.ParityError):
                    parity.validate_semantic_report_ranges(build, CAPACITIES, declared_overlap=True,
                                                           declared_work_ranges=declared, audited_processor_writes=True)

    def test_preview_build_output_identity_must_match_when_both_describe_output(self):
        for shape in ("equal", "size", "hash"):
            with self.subTest(shape=shape):
                preview = ab_report()
                build = copy.deepcopy(preview)
                build["Output"]["Committed"] = True
                if shape == "size":
                    preview["Output"]["Size"] = 15
                elif shape == "hash":
                    preview["Output"]["Sha256"] = "d" * 64
                pair = [evidence(preview), evidence(build, "build", output={"size": 16, "sha256": "c" * 64})]
                result = validation.side_execution_verdict(pair, capacities=CAPACITIES)
                self.assertEqual("invalid" if shape in ("size", "hash") else "output", result.status)
                if result.status == "invalid":
                    self.assertIn("Build output size or hash differs from Preview prediction", result.failures[0].detail)
        precursor = ab_report()
        precursor_build = copy.deepcopy(precursor)
        precursor_build["Output"]["Committed"] = True
        main = ab_report()
        main["Output"]["Sha256"] = "f" * 64
        main_build = copy.deepcopy(main)
        main_build["Output"]["Committed"] = True
        pairs = [evidence(precursor, "precursor-preview"),
                 evidence(precursor_build, "precursor-build", output={"size": 16, "sha256": "c" * 64}),
                 evidence(main), evidence(main_build, "build", output={"size": 16, "sha256": "f" * 64})]
        self.assertEqual("output", validation.side_execution_verdict(pairs, capacities=CAPACITIES).status)
        pairs[0].context["output"]["sha256"] = "d" * 64
        result = validation.side_execution_verdict(pairs, capacities=CAPACITIES)
        self.assertEqual(("invalid", "precursor-build"), (result.status, result.stopped_at))
        self.assertIn("Build output size or hash differs from Preview prediction", result.failures[0].detail)

    def test_successful_preview_requires_output_prediction_for_every_executor(self):
        raw = ab_report()
        raw["Output"] = None
        for stage in ("preview", "precursor-preview"):
            for old_executor in (False, True):
                with self.subTest(stage=stage, v0916_executor=old_executor):
                    result = validation.side_execution_verdict(
                        [evidence(raw, stage)], capacities=CAPACITIES, complete=False, v0916_executor=old_executor)
                    self.assertEqual(("invalid", [validation.Failure(
                        "PREDECESSOR_REPORT_INVALID", stage, "successful Preview has no output prediction")]),
                        (result.status, result.failures))

    def test_missing_prediction_refuses_completed_and_precursor_flows_for_every_executor(self):
        for old_executor in (False, True):
            for precursor in (False, True):
                for missing in (False, True):
                    with self.subTest(v0916_executor=old_executor, precursor=precursor, missing=missing):
                        preview = ab_report()
                        build = copy.deepcopy(preview)
                        build["Output"]["Committed"] = True
                        pair = [evidence(preview), evidence(build, "build", output={"size": 16, "sha256": "c" * 64})]
                        if precursor:
                            pair = [evidence(preview, "precursor-preview"),
                                    evidence(build, "precursor-build", output={"size": 16, "sha256": "c" * 64}), *pair]
                        if missing:
                            pair[0].context["output"] = None
                        result = validation.side_execution_verdict(pair, capacities=CAPACITIES, v0916_executor=old_executor)
                        self.assertEqual("invalid" if missing else "output", result.status)
                        if missing:
                            self.assertEqual("precursor-preview" if precursor else "preview", result.stopped_at)
                            self.assertEqual("PREDECESSOR_REPORT_INVALID", result.failures[0].code)
                        else:
                            self.assertEqual([], result.failures)

    def test_terminal_projection_default_still_accepts_succeeded_and_refuses_skipped(self):
        rejected = evidence(rejected_report(), exit_code=1).projection
        succeeded = copy.deepcopy(rejected)
        succeeded["compiledOperations"][0]["status"] = "succeeded"
        parity.validate_report_projection_against_compiled_authority(succeeded, succeeded)
        with self.assertRaises(parity.ParityError):
            parity.validate_report_projection_against_compiled_authority(rejected, rejected)
        self.assertEqual("rejected", verdict(rejected_report(), exit_code=1).status)


if __name__ == "__main__":
    unittest.main()

"""Decisions 271-274: comparator-only admission of written report shapes."""

from __future__ import annotations

import copy
from pathlib import Path
import unittest
from unittest.mock import patch

from scripts import predecessor_comparison as comparison
from scripts import predecessor_validation as validation
from scripts import v0916_parity_certification as parity
from tests.scripts.predecessor_test_support import written_1x_ab_merge_report, written_output_difference
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


class OwnerAnswersTests(unittest.TestCase):
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

    def test_all_skipped_rejection_accepts_preview_and_build_without_writes(self):
        raw = rejected_report()
        self.assertEqual("rejected", verdict(raw, exit_code=1).status)
        preview = copy.deepcopy(raw)
        preview["Operations"][0]["Status"] = "Succeeded"
        preview["Issues"] = []
        pair = [evidence(preview), evidence(raw, "build", exit_code=1)]
        self.assertEqual("rejected", validation.side_execution_verdict(pair, capacities=CAPACITIES).status)

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
        for shape in ("equal", "size", "hash", "no-prediction"):
            with self.subTest(shape=shape):
                preview = ab_report()
                build = copy.deepcopy(preview)
                build["Output"]["Committed"] = True
                if shape == "size":
                    preview["Output"]["Size"] = 15
                elif shape == "hash":
                    preview["Output"]["Sha256"] = "d" * 64
                elif shape == "no-prediction":
                    preview["Output"] = None
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

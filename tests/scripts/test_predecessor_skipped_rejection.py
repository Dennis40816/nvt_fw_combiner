"""Decision 272: unexecuted ranges, synthetic reports and pinned-base parity."""

from __future__ import annotations

import copy
from contextlib import contextmanager
from pathlib import Path
import subprocess
import sys
from types import ModuleType
import unittest

from scripts import predecessor_validation as validation
from scripts.predecessor_report_reader import ReportReaderError
from scripts import v0916_parity_certification as parity
from tests.scripts.predecessor_test_support import (
    _written_mutation, _written_operation, _written_report, written_output_difference,
)
from tests.scripts.test_predecessor_owner_answers import (
    CAPACITIES, TEMPORARY, TOOL, ab_report, evidence, rejected_report,
)
from tests.scripts.test_predecessor_report_reader import raw_report


ROOT = Path(__file__).resolve().parents[2]
BASE = "9f806f182ccb4099cd3864b0c5fc66f37035bc93"


def ctrlram_rejection():
    """Nine unexecuted rows, including two processors; no local report is read."""
    operations = [
        _written_operation(f"replace-{index}", 100 + index, "ReplaceRange", "ReplaceExisting",
                           ("reference-base", index * 4, index * 4 + 4),
                           ("NF_Ctrlram.bin", index * 4, index * 4 + 4))
        for index in range(7)
    ]
    for index in (7, 8):
        operations.append(_written_operation(
            f"processor-{index}", 100 + index, "RunExternalProcessor", "ReplaceExisting", None,
            ("NF_Ctrlram.bin", 0, 32),
            processor={"id": f"synthetic-processor-{index}", "writes": [(0, 4)], "commands": []}))
    for operation in operations:
        operation["Status"] = "Skipped"
    raw = _written_report("ctrlram-replace", "Replace", [("source", 8, "a" * 64)],
                          operations, [], [], committed=False, output_size=0, output_sha256="b" * 64)
    raw.update(Output=None, CompilationFingerprint=None,
               Issues=[{"Code": "product.rejected", "Severity": "Error"}])
    return raw


def variant(label):
    raw, code, output = ctrlram_rejection(), 1, None
    if label == "one-succeeded":
        raw["Operations"][0]["Status"] = "Succeeded"
    elif label == "mutation":
        raw["Mutations"] = [_written_mutation(raw["Operations"][0], 0, same=True)]
    elif label == "command":
        raw["Operations"][-1]["ExecutedCommands"] = [{
            "ExecutablePath": str(TOOL), "WorkingDirectory": str(TEMPORARY / "work"), "Arguments": ["CRC8"]}]
    elif label == "difference":
        raw["OutputDifferences"] = [written_output_difference(1, 0, 1)]
    elif label in ("committed", "captured", "described"):
        raw["Output"] = {"Size": 0, "Sha256": "b" * 64, "Committed": label != "described"}
        if label == "captured":
            output = {"size": 0, "sha256": "b" * 64}
    elif label == "zero-exit":
        code = 0
    elif label == "no-error":
        raw["Issues"] = []
    elif label == "warning-only":
        raw["Issues"][0]["Severity"] = "Warning"
    elif label == "unknown-kind":
        raw["Operations"][0]["Kind"] = "FutureOperation"
    elif label in ("repeated-sequence", "descending-sequence", "bool-sequence"):
        raw["Operations"][1]["Sequence"] = {
            "repeated-sequence": 100, "descending-sequence": 99, "bool-sequence": True,
        }[label]
    elif label == "failed-status":
        raw["Operations"][0]["Status"] = "Failed"
    elif label == "process-error":
        raw["Issues"].append({"Code": "external-tool.process.failed", "Severity": "Warning"})
    return raw, code, output


VARIANTS = (
    "unexecuted", "one-succeeded", "mutation", "command", "difference", "committed", "captured",
    "described", "zero-exit", "no-error", "warning-only", "unknown-kind", "repeated-sequence",
    "descending-sequence", "bool-sequence", "failed-status", "process-error",
)


@contextmanager
def pinned_module(relative, name):
    """Execute only the pinned Python blob in memory; no checkout or report file."""
    source = subprocess.check_output(["git", "cat-file", "blob", f"{BASE}:{relative}"], cwd=ROOT)
    module = ModuleType(name)
    module.__file__ = str(ROOT / relative)
    module.__package__ = "scripts"
    sys.modules[name] = module
    try:
        exec(compile(source, module.__file__, "exec"), module.__dict__)
        yield module
    finally:
        del sys.modules[name]


def corpus():
    """Both reader versions, rejected and executed shapes, process/capture refusals."""
    for version in ("v0916", "1x"):
        for label in VARIANTS:
            raw, code, output = variant(label)
            yield f"{version}:{label}", evidence(raw, exit_code=code, output=output, report_version=version), version, raw
        for label, raw, code in (
            ("succeeded-copy", raw_report(), 0),
            ("succeeded-ab", ab_report(), 0),
            ("succeeded-processor", ab_report(processor=True), 0),
            ("executed-rejection", ab_report(processor=True), 1),
            ("known-skipped", rejected_report(), 1),
        ):
            if label == "succeeded-copy":
                raw["Output"]["Committed"] = False
            if label == "executed-rejection":
                raw["Issues"] = [{"Code": "product.rejected", "Severity": "Error"}]
            yield f"{version}:{label}", evidence(raw, exit_code=code, report_version=version), version, raw
        for label in ("timeout", "crash", "input-changed", "input-binding", "no-report"):
            raw = ctrlram_rejection()
            item = evidence(raw, exit_code=1, report_version=version)
            if label == "timeout":
                item.process["timedOut"] = True
            elif label == "crash":
                item.process["exitCode"] = -1
            elif label == "input-changed":
                item.failures.append(validation.Failure("PREDECESSOR_INPUT_INVALID", "preview", "input changed"))
            elif label == "input-binding":
                item.inputs[0]["expectedReportArtifactId"] = "other-input"
            else:
                item.process["report"] = None
            yield f"{version}:{label}", item, version, raw


class SkippedRejectionTests(unittest.TestCase):
    def test_unknown_ctrlram_ranges_are_a_typed_rejection_only_when_all_five_conditions_hold(self):
        for version in ("v0916", "1x"):
            for label in VARIANTS:
                with self.subTest(version=version, shape=label):
                    raw, code, output = variant(label)
                    item = evidence(raw, exit_code=code, output=output, report_version=version)
                    result = validation.side_execution_verdict(
                        [item], capacities=CAPACITIES, complete=False, v0916_executor=version == "v0916")
                    accepted = label in ("unexecuted", "described")
                    self.assertEqual("rejected" if accepted else "invalid", result.status, result)
                    if accepted:
                        self.assertEqual([], result.failures)
                    elif label != "process-error":
                        self.assertEqual("PREDECESSOR_REPORT_INVALID", result.failures[0].code)
                    # The range owner is the first refusal for these readable no-exemption shapes.
                    if label in ("one-succeeded", "mutation", "command", "difference", "committed", "captured",
                                 "zero-exit", "no-error", "warning-only", "failed-status"):
                        self.assertEqual("PARITY_REPORT_RANGE_INVALID", result.failures[0].detail)

    def test_unknown_source_alone_and_unexecuted_overlap_are_not_read_or_write_evidence(self):
        raw = ctrlram_rejection()
        for operation in raw["Operations"]:
            operation["TargetSpaceId"] = "output-image"
        self.assertEqual("rejected", validation.side_execution_verdict(
            [evidence(raw, exit_code=1)], capacities=CAPACITIES, complete=False).status)
        # Exit zero gets no exemption even when the target space is known: the source is unknown.
        result = validation.side_execution_verdict([evidence(raw)], capacities=CAPACITIES, complete=False)
        self.assertEqual(("invalid", "PARITY_REPORT_RANGE_INVALID"), (result.status, result.failures[0].detail))

    def test_unexecuted_work_processor_is_not_a_later_read_or_write(self):
        raw = ab_report(processor=True)
        raw.update(Mutations=[], Output=None, OutputDifferences=[],
                   Issues=[{"Code": "product.rejected", "Severity": "Error"}])
        for operation in raw["Operations"]:
            operation.update(Status="Skipped", ExecutedCommands=[])
        # A whole-bank read would fail decision 271 if it ran; these rows never ran.
        raw["Operations"][-1]["SourceRange"] = {"Start": 0, "Length": 16, "EndExclusive": 16}
        result = validation.side_execution_verdict([evidence(raw, exit_code=1)], capacities=CAPACITIES, complete=False)
        self.assertEqual(("rejected", []), (result.status, result.failures))

    def test_schema_and_process_capture_checks_remain_required(self):
        for label in ("missing-identity", "unknown-operation-member", "malformed-range"):
            with self.subTest(shape=label):
                raw = ctrlram_rejection()
                if label == "missing-identity":
                    del raw["ProfileId"]
                elif label == "unknown-operation-member":
                    raw["Operations"][0]["FutureAuthority"] = True
                else:
                    raw["Operations"][0]["TargetRange"]["Length"] += 1
                with self.assertRaises(ReportReaderError):
                    evidence(raw, exit_code=1)
        for label, item, version, _ in corpus():
            if label.split(":")[1] in ("timeout", "crash", "input-changed", "input-binding", "no-report"):
                with self.subTest(shape=label):
                    self.assertEqual("invalid", validation.side_execution_verdict(
                        [item], capacities=CAPACITIES, complete=False, v0916_executor=version == "v0916").status)

    def test_skipped_rows_keep_intrinsic_range_format_checks_without_capacity_authority(self):
        for member in ("target", "source", "processor-read", "processor-write"):
            for shape in ("null", "float", "bool", "negative", "empty"):
                with self.subTest(member=member, shape=shape):
                    raw = ctrlram_rejection()
                    span = {
                        "null": None,
                        "float": {"Start": 0.5, "Length": 4.0, "EndExclusive": 4.5},
                        "bool": {"Start": False, "Length": 4, "EndExclusive": 4},
                        "negative": {"Start": -1, "Length": 4, "EndExclusive": 3},
                        "empty": {"Start": 0, "Length": 0, "EndExclusive": 0},
                    }[shape]
                    if member == "target":
                        raw["Operations"][0]["TargetRange"] = span
                    elif member == "source":
                        # A null source must not silently erase a named read declaration.
                        raw["Operations"][0]["SourceRange"] = span
                    else:
                        field = "ProcessorAllowedReadRanges" if member == "processor-read" else "ProcessorAllowedWriteRanges"
                        raw["Operations"][-1][field] = [span]
                    try:
                        item = evidence(raw, exit_code=1)
                    except ReportReaderError:
                        continue
                    self.assertEqual("invalid", validation.side_execution_verdict(
                        [item], capacities=CAPACITIES, complete=False).status)

    def test_default_range_and_projection_results_match_the_pinned_base_for_every_corpus_shape(self):
        with pinned_module("scripts/v0916_parity_certification.py", "scripts._skipped_base_parity") as old:
            count = 0
            for label, item, _, raw in corpus():
                with self.subTest(shape=label):
                    before = copy.deepcopy(item.projection)
                    for function in ("validate_semantic_report_ranges", "validate_report_projection_against_compiled_authority"):
                        results = []
                        for module in (old, parity):
                            try:
                                args = (item.projection, CAPACITIES) if function.endswith("ranges") else (item.projection, item.projection)
                                getattr(module, function)(*args)
                                results.append(("accepted", None))
                            except module.ParityError as error:
                                results.append(("refused", error.code, str(error)))
                        self.assertEqual(results[0], results[1])
                        self.assertEqual(before, item.projection)
                    # Compare the terminal normalizers too, including command-free processors.
                    original = copy.deepcopy(raw)
                    for member, function in (("Operations", "normalize_raw_operation"), ("Mutations", "normalize_raw_mutation")):
                        results = []
                        for module in (old, parity):
                            try:
                                results.append([getattr(module, function)(row) for row in raw[member]])
                            except module.ParityError as error:
                                results.append(("refused", error.code, str(error)))
                        self.assertEqual(results[0], results[1])
                    self.assertEqual(original, raw)
                    count += 1
            self.assertEqual(54, count)

    def test_no_non_all_skipped_verdict_changes_against_the_pinned_base(self):
        with pinned_module("scripts/v0916_parity_certification.py", "scripts._skipped_base_parity") as old_parity:
            with pinned_module("scripts/predecessor_validation.py", "scripts._skipped_base_validation") as old:
                # The baseline validator must use its own baseline shared owners, not today's imports.
                for name in ("ParityError", "validate_report_sequence", "validate_report_projection_against_compiled_authority",
                             "validate_semantic_report_ranges"):
                    setattr(old, name, getattr(old_parity, name))
                total = non_skipped = changed = 0
                for label, item, version, _ in corpus():
                    options = {"capacities": CAPACITIES, "complete": False, "v0916_executor": version == "v0916"}
                    previous = old.side_execution_verdict([copy.deepcopy(item)], **options)
                    current = validation.side_execution_verdict([copy.deepcopy(item)], **options)
                    all_skipped = all(row["status"] == "skipped" for row in item.projection["compiledOperations"])
                    with self.subTest(shape=label):
                        if not all_skipped:
                            self.assertEqual(previous.status, current.status)
                            non_skipped += 1
                        if previous.status != current.status:
                            self.assertTrue(all_skipped)
                            self.assertIn(label.split(":")[1], ("unexecuted", "described"))
                            self.assertEqual(("invalid", "rejected"), (previous.status, current.status))
                            changed += 1
                    total += 1
                self.assertEqual((54, 12, 4), (total, non_skipped, changed))


if __name__ == "__main__":
    unittest.main()

"""Payload-free CLI report fixtures; format conversion and shared safety seams."""

from __future__ import annotations

import copy
import unittest
from typing import Any
from unittest.mock import patch

from scripts import predecessor_report_reader as reader
from scripts import v0916_parity_certification as parity


def raw_report() -> dict[str, Any]:
    span = {"Start": 0, "Length": 8, "EndExclusive": 8}
    return {
        "RunId": "synthetic", "ProfileId": "test", "ProfileVersion": "1.0.0",
        "IcId": "test", "ModeId": "standard-merge", "ExperienceId": "standard-merge",
        "CompositionKind": "Merge", "StartedAtUtc": "2026-09-26T00:00:00Z",
        "CompletedAtUtc": "2026-09-26T00:00:01Z",
        "Inputs": [{"AddressSpaceId": "source", "ArtifactId": "input", "Size": 8,
                    "Sha256": "a" * 64, "OriginalFileName": None}],
        "Operations": [{
            "OperationId": "copy", "Sequence": 0, "Kind": "CopyRange", "Status": "Succeeded",
            "SourceSpaceId": "source", "SourceRange": dict(span), "TargetSpaceId": "output-image",
            "TargetRange": dict(span), "OverlapPolicy": "Reject", "ProcessorId": None,
            "ToolBindingId": None, "ProcessorAllowedReadRanges": [], "ProcessorAllowedWriteRanges": [],
            "ExecutedCommands": [], "Reason": "synthetic", "Provenance": {
                "Kind": "built-in-profile", "SourceId": None, "SourceVersion": None},
        }],
        "Mutations": [{
            "OperationId": "copy", "Kind": "CopyRange", "TargetSpaceId": "output-image",
            "TargetRange": dict(span), "ChangedByteCount": 8, "BeforeSha256": "0" * 64,
            "AfterSha256": "b" * 64, "Reason": "synthetic",
        }],
        "Issues": [], "Output": {"FileName": "synthetic.bin", "Size": 8, "Sha256": "b" * 64,
                                   "Committed": True},
        "OutputDifferences": [], "CompilationFingerprint": "c" * 64,
        "Validations": [], "OutputNaming": None,
    }


class ReportReaderTests(unittest.TestCase):
    def test_v0916_has_no_invented_map_and_reuses_both_public_normalizers(self) -> None:
        raw = raw_report()
        before = copy.deepcopy(raw)
        with patch.object(reader, "normalize_raw_operation", wraps=parity.normalize_raw_operation) as operation:
            with patch.object(reader, "normalize_raw_mutation", wraps=parity.normalize_raw_mutation) as mutation:
                result = reader.read_cli_report(raw, report_version="v0916")
        self.assertEqual("cli-v0916-v1", result.reader_version)
        self.assertIsNone(result.context["mapId"])
        self.assertEqual(parity.normalize_raw_operation(raw["Operations"][0]), result.projection["compiledOperations"][0])
        self.assertEqual(parity.normalize_raw_mutation(raw["Mutations"][0]), result.projection["compiledMutations"][0])
        operation.assert_called_once()
        mutation.assert_called_once()
        self.assertEqual(before, raw)

    def test_1x_extensions_are_recorded_by_name_without_becoming_authority(self) -> None:
        raw = raw_report()
        raw.update(MapId="declared-map", AbMergeFormat={"authority": "fake"}, SourceEnvelope="fake")
        raw["Inputs"][0]["FutureInput"] = "private-value"
        raw["Output"]["FutureOutput"] = "private-value"
        raw["Issues"] = [{"Code": "input.address-space.truncated", "Severity": "Warning",
                          "Message": "synthetic", "OperationId": "copy", "Future/Member~": "private-value"}]
        result = reader.read_cli_report(raw, report_version="1x")
        self.assertEqual("cli-1x-v1", result.reader_version)
        self.assertEqual("declared-map", result.context["mapId"])
        self.assertEqual(["/AbMergeFormat", "/Inputs/0/FutureInput", "/Issues/0/Future~1Member~0",
                          "/Output/FutureOutput", "/SourceEnvelope"], result.unknown_members)
        self.assertNotIn("private-value", repr(result))
        self.assertNotIn("fake", repr(result))

    def test_unknown_members_in_operation_and_mutation_rows_are_refused(self) -> None:
        """Rows that carry write authority keep the ADR 0057 exact-member rule; nothing is dropped."""

        def processor_command(raw: dict[str, Any]) -> None:
            raw["Operations"][0].update(
                ProcessorId="processor", ToolBindingId="tool",
                ProcessorAllowedReadRanges=[{"Start": 0, "Length": 8, "EndExclusive": 8}],
                ProcessorAllowedWriteRanges=[{"Start": 0, "Length": 8, "EndExclusive": 8}],
                ExecutedCommands=[{"ExecutablePath": "C:/package/external-tools/tool.exe",
                                   "WorkingDirectory": "C:/staging", "Arguments": ["run"]}],
            )

        self.assertEqual([], reader.read_cli_report(raw_report(), report_version="1x").unknown_members)
        with_command = raw_report()
        processor_command(with_command)
        self.assertEqual([], reader.read_cli_report(with_command, report_version="1x").unknown_members)

        def operation(raw: dict[str, Any]) -> None:
            raw["Operations"][0]["AdditionalTargetRanges"] = [{"Start": 4096, "Length": 16, "EndExclusive": 4112}]

        def operation_range(raw: dict[str, Any]) -> None:
            raw["Operations"][0]["TargetRange"]["FutureCapacity"] = 9999

        def provenance(raw: dict[str, Any]) -> None:
            raw["Operations"][0]["Provenance"]["FutureSource"] = "x"

        def command(raw: dict[str, Any]) -> None:
            processor_command(raw)
            raw["Operations"][0]["ExecutedCommands"][0]["FutureArgument"] = "x"

        def processor_range(raw: dict[str, Any]) -> None:
            processor_command(raw)
            raw["Operations"][0]["ProcessorAllowedWriteRanges"][0]["FutureCapacity"] = 9999

        def mutation(raw: dict[str, Any]) -> None:
            raw["Mutations"][0]["FutureWrite"] = {"end": 9999}

        def mutation_range(raw: dict[str, Any]) -> None:
            raw["Mutations"][0]["TargetRange"]["FutureCapacity"] = 9999

        for change in (operation, operation_range, provenance, command, processor_range, mutation, mutation_range):
            with self.subTest(change=change.__name__):
                raw = raw_report()
                change(raw)
                with self.assertRaises(reader.ReportReaderError) as raised:
                    reader.read_cli_report(raw, report_version="1x")
                self.assertEqual("PREDECESSOR_REPORT_INVALID", raised.exception.code)

    def test_issue_codes_and_severities_are_preserved_without_classification(self) -> None:
        raw = raw_report()
        raw["Issues"] = [
            {"Code": "DP_SIZE_WARNING", "Severity": "warning"},
            {"Code": "profile.v2.compile.map-selection-invalid", "Severity": "error"},
            {"Code": "external-tool.process.start-failed", "Severity": "error"},
            {"Code": "future.code", "Severity": "info"},
        ]
        result = reader.read_cli_report(raw, report_version="v0916")
        self.assertEqual([{"code": issue["Code"], "severity": issue["Severity"], "source": "report"}
                          for issue in raw["Issues"]], result.issues)
        self.assertNotIn("status", result.projection)

    def test_rejected_preview_needs_no_output_or_compiled_operations(self) -> None:
        raw = raw_report()
        raw.update(Output=None, Operations=[], Mutations=[], CompilationFingerprint=None)
        raw["Issues"] = [{"Code": "profile.v2.compile.map-selection-invalid", "Severity": "error"}]
        result = reader.read_cli_report(raw, report_version="v0916")
        self.assertIsNone(result.context["output"])
        self.assertEqual([], result.projection["compiledOperations"])

    def test_stderr_only_codes_never_create_a_report(self) -> None:
        for raw in (None, "AB_FORMAT_CONFIGURATION_INVALID", {"stderr": "capability.readiness.runtime-dependency-blocked"}):
            with self.subTest(raw=raw), self.assertRaises(reader.ReportReaderError) as found:
                reader.read_cli_report(raw, report_version="1x")
            self.assertEqual("PREDECESSOR_REPORT_INVALID", found.exception.code)

    def test_malformed_format_or_unknown_version_fails_closed(self) -> None:
        mutations = [
            lambda raw: raw.pop("Operations"),
            lambda raw: raw["Operations"][0].pop("TargetRange"),
            lambda raw: raw["Mutations"][0].update(TargetRange={"Start": 0, "Length": 7, "EndExclusive": 8}),
            lambda raw: raw.update(Issues=[{"Code": "test", "Severity": "fatal"}]),
            lambda raw: raw.update(Issues=[{"Severity": "error"}]),
            lambda raw: raw.update(Operations="not-an-array"),
        ]
        for mutate in mutations:
            raw = raw_report()
            mutate(raw)
            with self.subTest(mutate=mutate), self.assertRaises(reader.ReportReaderError):
                reader.read_cli_report(raw, report_version="v0916")
        with self.assertRaises(reader.ReportReaderError):
            reader.read_cli_report(raw_report(), report_version="future")

    def test_reader_leaves_sequence_compiled_authority_and_range_checks_with_their_owner(self) -> None:
        raw = raw_report()
        authority = reader.read_cli_report(raw, report_version="v0916").projection
        projection = reader.read_cli_report(raw, report_version="v0916").projection
        parity.validate_report_sequence(authority_operations=authority["compiledOperations"],
                                       observed_operations=projection["compiledOperations"],
                                       observed_mutations=projection["compiledMutations"])
        parity.validate_report_projection_against_compiled_authority(projection, authority)
        parity.validate_semantic_report_ranges(projection, {"source": 8, "output-image": 8})
        raw["Operations"][0]["TargetRange"].update(Length=16, EndExclusive=16)
        widened = reader.read_cli_report(raw, report_version="v0916").projection
        with self.assertRaises(parity.ParityError):
            parity.validate_report_projection_against_compiled_authority(widened, authority)
        with self.assertRaises(parity.ParityError):
            parity.validate_semantic_report_ranges(widened, {"source": 8, "output-image": 8})
        raw = raw_report()
        raw["Operations"][0]["Sequence"] = 1
        reordered = reader.read_cli_report(raw, report_version="v0916").projection
        with self.assertRaises(parity.ParityError):
            parity.validate_report_sequence(authority_operations=authority["compiledOperations"],
                                           observed_operations=reordered["compiledOperations"],
                                           observed_mutations=reordered["compiledMutations"])


if __name__ == "__main__":
    unittest.main()

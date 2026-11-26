"""Baseline self-report identity: synthetic values, no build or firmware bytes."""

import copy
import json
from pathlib import Path
import unittest

from scripts import predecessor_validation as validation
from scripts import predecessor_report_reader as reader
from tests.scripts.test_predecessor_report_reader import raw_report


ROOT = Path(__file__).resolve().parents[2]
MISSING = "PREDECESSOR_BASELINE_IDENTITY_MISSING"
MISMATCH = "PREDECESSOR_BASELINE_IDENTITY_MISMATCH"


class BaselineIdentityTests(unittest.TestCase):
    def setUp(self):
        self.rebuilt = {
            "authorityTrees": {name: "1" * 40 for name in ("external-tools", "profiles", "src", "tools/crc-worker")},
            "cliSha256": "2" * 64, "commit": "3" * 40,
            "compilerHost": {"runtimeVersion": "runtime", "compilerVersion": "compiler", "verifiedAssemblyCount": 7},
            "lockFileSetSha256": "4" * 64, "resolvedSdkVersion": "10.0.100",
            "runtimeClosureSha256": "5" * 64, "tagObject": "6" * 40, "tree": "7" * 40,
        }
        self.report = {"schemaVersion": "1.0", "kind": "predecessor-comparison-report", "certification": "none",
                       "terminal": False, "mode": "rolling",
                       "candidate": {"version": "1.2.1", "executor": copy.deepcopy(self.rebuilt)}}
        self.report["candidate"]["executor"]["tagObject"] = None

    def check(self, report=None):
        return validation.baseline_identity_failures(self.rebuilt, self.report if report is None else report,
                                                     baseline_version="1.2.1")

    def test_equal_identity_accepts_candidate_null_tag_and_matching_tag_without_mutating_inputs(self):
        for tag in (None, self.rebuilt["tagObject"]):
            self.report["candidate"]["executor"]["tagObject"] = tag
            before = copy.deepcopy((self.rebuilt, self.report))
            self.assertEqual([], self.check())
            self.assertEqual(before, (self.rebuilt, self.report))

    def test_each_identity_member_is_compared_and_required(self):
        for member in self.rebuilt:
            for change in ("different", "missing", "null"):
                if member == "tagObject" and change == "null":
                    continue
                with self.subTest(member=member, change=change):
                    report = copy.deepcopy(self.report)
                    recorded = report["candidate"]["executor"]
                    if change == "different":
                        recorded[member] = {} if isinstance(self.rebuilt[member], dict) else "different"
                    elif change == "missing":
                        del recorded[member]
                    else:
                        recorded[member] = None
                    codes = {row.code for row in self.check(report)}
                    self.assertIn(MISMATCH if change == "different" and not isinstance(self.rebuilt[member], dict)
                                  else MISSING, codes)

    def test_nested_authority_and_compiler_values_cannot_be_different_missing_or_wrongly_typed(self):
        for group in ("authorityTrees", "compilerHost"):
            for member in self.rebuilt[group]:
                for change in ("different", "missing", "wrong-type"):
                    with self.subTest(group=group, member=member, change=change):
                        report = copy.deepcopy(self.report)
                        recorded = report["candidate"]["executor"][group]
                        if change == "missing":
                            del recorded[member]
                        elif change == "wrong-type":
                            recorded[member] = True if member == "verifiedAssemblyCount" else [recorded[member]]
                        else:
                            recorded[member] = 8 if member == "verifiedAssemblyCount" else "different"
                        self.assertEqual({MISSING if change == "missing" else MISMATCH},
                                         {row.code for row in self.check(report)})
        # Python equality equates True and 1; identity comparison must not.
        self.rebuilt["compilerHost"]["verifiedAssemblyCount"] = 1
        self.report["candidate"]["executor"]["compilerHost"]["verifiedAssemblyCount"] = True
        self.assertEqual({MISMATCH}, {row.code for row in self.check()})

    def test_wrong_report_family_version_commit_and_extra_identity_refuse(self):
        changes = [lambda r: r.update(kind="v0916-parity-evidence"),
                   lambda r: r.update(mode=[]), lambda r: r.update(terminal=True),
                   lambda r: r["candidate"].update(version="1.2.0"),
                   lambda r: r["candidate"]["executor"].update(commit="8" * 40),
                   lambda r: r["candidate"]["executor"].update(extra="ignored?"),
                   lambda r: r["candidate"]["executor"]["compilerHost"].update(extra="ignored?")]
        for mutate in changes:
            report = copy.deepcopy(self.report)
            mutate(report)
            self.assertEqual({MISMATCH}, {row.code for row in self.check(report)})

    def test_cli_reports_have_no_program_identity_for_either_family(self):
        for version in ("1x", "v0916"):
            report = raw_report()
            # Matching firmware/profile fields must not fabricate program identity.
            report["ProfileVersion"] = "1.2.1"
            read = reader.read_cli_report(report, report_version=version)
            self.assertNotIn("executor", read.context)
            self.assertEqual({MISSING}, {row.code for row in self.check(report)})
        for report in ({}, [], {"candidate": None}, {"candidate": {"executor": None}}):
            self.assertEqual({MISSING}, {row.code for row in self.check(report)})

    def test_contract_and_schema_name_only_the_common_program_fields(self):
        contracts = ROOT / "docs/contracts"
        contract = json.loads((contracts / "predecessor-comparison-v1.json").read_bytes())
        schema = json.loads((contracts / "predecessor-comparison-v1.schema.json").read_bytes())
        policy = contract["executor"]["baselineIdentityComparison"]
        self.assertEqual(sorted(self.rebuilt.keys() - {"tagObject"}), policy["comparedMembers"])
        self.assertEqual(policy, schema["properties"]["executor"]["properties"]["baselineIdentityComparison"]["const"])
        self.assertEqual("not-implemented", policy["v0916"]["status"])


if __name__ == "__main__":
    unittest.main()

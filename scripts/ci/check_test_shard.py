"""Fail closed on missing, incomplete, skipped or below-floor .NET evidence."""
from __future__ import annotations

import os
from pathlib import Path
import re
import sys

if __package__:
    from .fixed_argparse import FixedArgumentParser
    from .baseline_contract import ROOT, SHARDS, UPDATE_MODE_VARIABLES, load_baseline, source_commit, write_json
    from .read_trx import empty_results, get_example_data_test_names, read_trx_results
else:
    from fixed_argparse import FixedArgumentParser
    from baseline_contract import ROOT, SHARDS, UPDATE_MODE_VARIABLES, load_baseline, source_commit, write_json
    from read_trx import empty_results, get_example_data_test_names, read_trx_results


def check_test_shard(*, shard: str, results_directory: Path, baseline_path: Path,
                     summary_path: Path, example_data_present: bool,
                     commit: str = "", test_run_failed: bool = False,
                     unverified: bool = False) -> dict:
    invalid = False
    counts = empty_results()
    checks = dict(belowFloor=False, skippedNotZero=False,
                  requiredClassMissing=False, updateModeVariableSet=False)
    example_executed = 0
    try:
        counts = read_trx_results(results_directory)
        floor = load_baseline(baseline_path)["shards"][shard]
        checks["belowFloor"] = counts["Total"] < floor["minTotal"]
        checks["skippedNotZero"] = counts["Skipped"] != 0
        if not example_data_present and not unverified and (floor["exampleMinTests"] or floor["requiredClasses"]):
            checks["requiredClassMissing"] = bool(floor["requiredClasses"])
            checks["belowFloor"] = True
        if example_data_present and (floor["exampleMinTests"] or floor["requiredClasses"]):
            for cls in floor["requiredClasses"]:
                rows = [r for r in counts["Results"] if r["ClassName"] == cls]
                if not rows or any(r["Outcome"] != "Passed" for r in rows):
                    checks["requiredClassMissing"] = True
            names = get_example_data_test_names(ROOT / "tests")
            example_executed = len({r["MethodName"] for r in counts["Results"]
                                    if r["Outcome"] in ("Passed", "Failed")
                                    and r["MethodName"] in names})
            checks["belowFloor"] |= example_executed < floor["exampleMinTests"]
    except (OSError, ValueError, KeyError, TypeError):
        invalid = True
        checks["belowFloor"] = True
    checks["updateModeVariableSet"] = any(name in os.environ for name in UPDATE_MODE_VARIABLES)
    commit = commit or source_commit()
    if re.fullmatch(r"[0-9a-f]{40}", commit) is None:
        commit = "0" * 40
        invalid = True
    failed = (invalid or test_run_failed or not counts["Completed"] or counts["Failed"] > 0
              or counts["NotExecuted"] > 0 or any(checks.values()))
    summary = dict(schemaVersion=1, shard=shard, commit=commit,
                   outcome="unverified" if unverified else "failed" if failed else "passed",
                   total=counts["Total"], passed=counts["Passed"], failed=counts["Failed"],
                   skipped=counts["Skipped"], notExecuted=counts["NotExecuted"],
                   failedTests=counts["FailedTests"],
                   exampleData=dict(present=example_data_present, executed=example_executed),
                   checks=checks)
    try:
        write_json(summary_path, summary)
    except OSError:
        raise ValueError("Shard summary could not be written.") from None
    return summary


def main() -> int:
    parser = FixedArgumentParser(description=__doc__, allow_abbrev=False)
    parser.add_argument("--shard", choices=SHARDS, required=True)
    parser.add_argument("--results-directory", type=Path, required=True)
    parser.add_argument("--baseline-path", type=Path, default=Path(__file__).with_name("test-baseline.json"))
    parser.add_argument("--summary-path", type=Path)
    parser.add_argument("--example-data-present", action="store_true")
    parser.add_argument("--commit", default="")
    parser.add_argument("--test-run-failed", action="store_true")
    parser.add_argument("--unverified", action="store_true")
    args = parser.parse_args()
    args.summary_path = args.summary_path or args.results_directory / "shard-summary.json"
    try:
        summary = check_test_shard(**vars(args))
        print("[check-test-shard] " + " ".join(f"{key} {summary[key]}" for key in
                                              ("total", "passed", "failed", "skipped", "notExecuted")))
        if summary["outcome"] != "passed":
            print("unverified, cannot merge" if args.unverified else "Shard validation failed.",
                  file=sys.stderr)
            return 1
        return 0
    except (OSError, ValueError):
        print("Shard summary could not be written.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())

"""Raise test floors only from complete, passing, zero-skip TRX runs."""
from pathlib import Path
import sys

if __package__:
    from .fixed_argparse import FixedArgumentParser
    from .baseline_contract import ROOT, SHARDS, load_baseline, source_commit, validate_baseline, write_json
    from .read_trx import get_example_data_test_names, read_trx_results
else:
    from fixed_argparse import FixedArgumentParser
    from baseline_contract import ROOT, SHARDS, load_baseline, source_commit, validate_baseline, write_json
    from read_trx import get_example_data_test_names, read_trx_results


def update_test_baseline(*, shard: str, results_directory: Path, baseline_path: Path,
                         measured_at_commit: str = "", example_data_present: bool = False) -> bool:
    try:
        baseline = load_baseline(baseline_path)
        floor = baseline["shards"][shard]
        counts = read_trx_results(results_directory)
        if (not counts["Completed"] or counts["Failed"] or counts["Skipped"]
                or counts["NotExecuted"] or counts["Total"] < floor["minTotal"]):
            raise ValueError("Unsuccessful tests.")
        example_floor = floor["exampleMinTests"]
        if example_data_present:
            names = get_example_data_test_names(ROOT / "tests")
            executed = len({r["MethodName"] for r in counts["Results"]
                            if r["Outcome"] == "Passed" and r["MethodName"] in names})
            if executed < example_floor:
                raise ValueError("Lower example count refused.")
            example_floor = executed
        if counts["Total"] == floor["minTotal"] and example_floor == floor["exampleMinTests"]:
            return False
        floor["minTotal"] = counts["Total"]
        floor["exampleMinTests"] = example_floor
        baseline["measuredAtCommit"] = measured_at_commit or source_commit()
        validate_baseline(baseline)
        write_json(baseline_path, baseline)
        return True
    except (OSError, ValueError, KeyError, TypeError):
        raise ValueError("Test baseline update failed.") from None


def main() -> int:
    parser = FixedArgumentParser(description=__doc__, allow_abbrev=False)
    parser.add_argument("--shard", choices=SHARDS, required=True)
    parser.add_argument("--results-directory", type=Path, required=True)
    parser.add_argument("--baseline-path", type=Path, default=Path(__file__).with_name("test-baseline.json"))
    parser.add_argument("--measured-at-commit", default="")
    parser.add_argument("--example-data-present", action="store_true")
    args = parser.parse_args()
    try:
        changed = update_test_baseline(**vars(args))
        floor = load_baseline(args.baseline_path)["shards"][args.shard]
        print(f"[update-test-baseline] minTotal {floor['minTotal']} exampleMinTests {floor['exampleMinTests']}"
              if changed else "[update-test-baseline] unchanged")
        return 0
    except (OSError, ValueError):
        print("Test baseline update failed.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())

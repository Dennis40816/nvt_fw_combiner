"""Stage manifest-declared IC reference evidence as a candidate-only bundle."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from ic_reference_candidate_intake import CandidateIntakeError, stage_manifest_request


def parse_args() -> argparse.Namespace:
    """Parse the manifest intake contract and reject the retired folder-scan surface."""
    parser = argparse.ArgumentParser(description=__doc__)
    input_source = parser.add_mutually_exclusive_group(required=True)
    input_source.add_argument(
        "--request",
        type=Path,
        help="Manifest-driven candidate intake request.",
    )
    input_source.add_argument("--source", type=Path, help=argparse.SUPPRESS)
    parser.add_argument(
        "--source-root", type=Path, help="Root containing files declared by --request."
    )
    parser.add_argument(
        "--output-dir", type=Path, help="New candidate staging directory."
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Validate and print candidate output without staging files.",
    )

    # Parse retired options only to reject them without echoing user paths.
    parser.add_argument("--ic", help=argparse.SUPPRESS)
    parser.add_argument("--mode", help=argparse.SUPPRESS)
    parser.add_argument("--case", help=argparse.SUPPRESS)
    parser.add_argument("--owner", help=argparse.SUPPRESS)
    parser.add_argument("--source-ref", help=argparse.SUPPRESS)
    parser.add_argument("--output-root", type=Path, help=argparse.SUPPRESS)
    parser.add_argument("--run-id", help=argparse.SUPPRESS)
    return parser.parse_args()


def main() -> int:
    """Run only the declared manifest contract; never scan a caller directory."""
    args = parse_args()
    if args.source is not None or any(
        value is not None
        for value in (
            args.ic,
            args.mode,
            args.case,
            args.owner,
            args.source_ref,
            args.output_root,
            args.run_id,
        )
    ):
        print(
            "ERROR: This CLI only accepts a manifest request; folder-scan options are not supported.",
            file=sys.stderr,
        )
        return 1

    try:
        return stage_manifest_request(args)
    except (CandidateIntakeError, FileNotFoundError, ValueError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

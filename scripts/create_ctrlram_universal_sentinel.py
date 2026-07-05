"""Create the deterministic CtrlRAM universal sentinel BIN.

The file is owner-side validation input only. It is not a golden output and
does not replace byte-for-byte CtrlRAM Replace parity evidence.
"""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path


DEFAULT_LENGTH = 0x23000
DEFAULT_SEED = 0x5A


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("ctrlram-universal-sentinel.bin"),
        help="Output BIN path. Default: ctrlram-universal-sentinel.bin",
    )
    parser.add_argument(
        "--length",
        type=parse_non_negative_int,
        default=DEFAULT_LENGTH,
        help="Output length in bytes. Accepts decimal or 0x-prefixed hex. Default: 0x23000.",
    )
    parser.add_argument(
        "--seed",
        type=parse_byte,
        default=DEFAULT_SEED,
        help="Pattern seed byte. Accepts decimal or 0x-prefixed hex. Default: 0x5A.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print deterministic metadata without writing the BIN file.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    payload = create_payload(args.length, args.seed)
    digest = hashlib.sha256(payload).hexdigest()
    if args.dry_run:
        print_metadata(args.length, args.seed, digest, None)
        return 0

    output_path = args.output.resolve()
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_bytes(payload)
    print_metadata(args.length, args.seed, digest, output_path)
    return 0


def create_payload(length: int, seed: int) -> bytes:
    return bytes((seed + index) & 0xFF for index in range(length))


def parse_non_negative_int(value: str) -> int:
    try:
        parsed = int(value, 0)
    except ValueError as exc:
        raise argparse.ArgumentTypeError(f"invalid integer: {value}") from exc
    if parsed < 0:
        raise argparse.ArgumentTypeError("value must be non-negative")
    return parsed


def parse_byte(value: str) -> int:
    parsed = parse_non_negative_int(value)
    if parsed > 0xFF:
        raise argparse.ArgumentTypeError("seed must be in byte range 0x00..0xFF")
    return parsed


def print_metadata(length: int, seed: int, digest: str, output_path: Path | None) -> None:
    print(f"length=0x{length:X} ({length})")
    print(f"seed=0x{seed:02X}")
    print(f"sha256={digest}")
    if output_path is not None:
        print(f"path={output_path}")


if __name__ == "__main__":
    raise SystemExit(main())

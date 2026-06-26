# -*- coding: utf-8 -*-
"""
CLI argument validation and runtime checks (size, existence, etc.).
All validation errors raise ValueError/IOError – the caller will catch them.
"""

from __future__ import print_function
import os
import argparse

from .logger import log_info


def _int_auto_base(s):
    """Convert decimal/hex/octal/binary string to int (int(s, 0))."""
    try:
        return int(s, 0)
    except Exception:
        raise argparse.ArgumentTypeError("Invalid integer value: %s" % s)


def parse_args():
    """Parse command‑line arguments (identical behaviour on Py2 & Py3)."""
    parser = argparse.ArgumentParser(
        description="Generate a merged flash image for a given IC series."
    )
    parser.add_argument("--ic", required=True, help="IC series identifier, e.g. 51928")
    parser.add_argument(
        "--padding-byte",
        default=None,
        type=_int_auto_base,
        help=(
            "Byte used for unwritten flash locations (default to None, decided by config.py). "
            "Accepts decimal, 0x.., 0o.. or 0b.. notation."
        ),
    )
    parser.add_argument(
        "--cfg-path",
        default="./ic_config.json",
        help=(
            "Path to the IC configuration JSON file. " "Defaults to './ic_config.json'."
        ),
    )
    parser.add_argument(
        "--test-mode",
        action="store_true",
        help="Run in test mode – locate bin files by IC prefix and compare "
        "the generated flash image with the reference file in test/expected.",
    )

    return parser.parse_args()


def validate_cli_args(args):
    """
    Perform basic validation on the parsed arguments.
    Raise ValueError with a clear message if something is wrong.
    """
    if not args.ic:
        raise ValueError("IC identifier (--ic) must be supplied.")
    # padding-byte is already converted to int by argparse, no further check needed.


def check_tp_size(ic, tp_path, expected_size):
    """
    Verify that the TP binary size matches the expected size.
    Raise ValueError if the size is abnormal.
    """

    # -----------------------------------------------------------------
    # 1. Maintainable version list for dynamic ICs
    # -----------------------------------------------------------------
    # Exclude version 51931 from the dynamic group.
    DYNAMIC_EXCLUDE = {51931}
    # Explicitly include version 51929 in the dynamic group.
    DYNAMIC_INCLUDE = {51929}
    # All versions >= 51930 are considered dynamic unless excluded.
    DYNAMIC_MIN = 51930

    ic = int(ic)

    # Determine whether the supplied IC version belongs to the dynamic group.
    is_dynamic = (
        ic >= DYNAMIC_MIN and ic not in DYNAMIC_EXCLUDE
    ) or (  # >= 51930 and not excluded
        ic in DYNAMIC_INCLUDE
    )  # explicitly included

    log_info("{} type size check".format("Dynamic" if is_dynamic else "Fixed"))

    # -----------------------------------------------------------------
    # 2. Choose the expected binary size based on the IC type
    # -----------------------------------------------------------------
    EXPECTED_DYNAMIC = 256 * 1024  # 256 kB for dynamic ICs
    target_size = EXPECTED_DYNAMIC if is_dynamic else expected_size
    

    # -----------------------------------------------------------------
    # 3. Retrieve the actual file size from the filesystem
    # -----------------------------------------------------------------
    actual_size = os.path.getsize(tp_path)

    # -----------------------------------------------------------------
    # 4. Compare the actual size with the target size and raise an error if mismatched
    # -----------------------------------------------------------------
    if actual_size != target_size:
        raise ValueError(
            "[ERROR] TP bin size = 0x%X abnormal (expected 0x%X)"
            % (actual_size, target_size)
        )

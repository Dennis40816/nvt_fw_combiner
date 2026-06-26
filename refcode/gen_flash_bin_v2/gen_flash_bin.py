#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
Entry point - orchestrates the whole workflow.
All heavy-lifting is delegated to the `lib` package.

---
Usage:

python gen_flash_bin.py --ic 51926

python gen_flash_bin.py --ic 51926 --padding-byte 0xFF

python gen_flash_bin.py --ic 51926 --cfg-path ./ic_config.json --padding-byte 0xFF
"""

# for python 2 (important)
from __future__ import unicode_literals, print_function
import sys
import os
import glob

# ----------------------------------------------------------------------
# Centralised imports from the lib package
# ----------------------------------------------------------------------
from lib import (
    get_py_meta_info,
    log_info,
    log_error,
    read_bin_segment,
    write_bin,
    norm_path,
    ensure_dir,
    load_config,
    parse_args,
    validate_cli_args,
    check_tp_size,
    build_flash,
    make_filename,
)


def _latest_bin(folder):
    """Find the newest *.bin file in *folder*."""
    pattern = os.path.join(folder, "*.bin")
    files = glob.glob(pattern)
    if not files:
        raise IOError("No .bin file found in %s" % folder)
    files.sort(key=os.path.getctime)
    return files[-1]  # newest by filename order


def _ic_prefixed_bin(folder, ic, suffix=""):
    pattern = os.path.join(folder, "*{}{}*.bin".format(ic, suffix))
    matches = glob.glob(pattern)
    if not matches:
        raise IOError("No bin file matching %s in %s" % (pattern, folder))
    matches.sort(key=os.path.getctime)
    return matches[-1]


def main():
    # -------------------------------------------------
    # 1️⃣ Print interpreter meta‑information (if available)
    # -------------------------------------------------
    if get_py_meta_info:
        py_path, py_major_ver, _ = get_py_meta_info()
        print("")
        print("============== Python Interpreter Information ==============")
        print("User python path: {}".format(py_path))
        print("User python major version: {}".format(py_major_ver))
        print("Generate flash code with py{}".format(py_major_ver))
        print("============================================================")
        print("")
        if py_major_ver not in (2, 3):
            log_error("Invalid python major version detected: {}".format(py_major_ver))
            sys.exit(-1)
    else:
        log_error(
            "lib.py_meta.get_py_meta_info() not found – skipping interpreter info."
        )

    # -------------------------------------------------
    # 2️⃣ Parse command‑line arguments
    # -------------------------------------------------
    args = parse_args()
    try:
        validate_cli_args(args)
    except Exception as e:
        log_error(e)
        sys.exit(-1)

    ic = args.ic
    padding = args.padding_byte
    cfg_path = args.cfg_path
    is_test_mode = args.test_mode

    # -------------------------------------------------
    # 3️⃣ Load configuration (global prefixes are merged in)
    # -------------------------------------------------
    log_info("Loading ic_config.json...")
    try:
        cfg = load_config(ic, cfg_path)

        # update padding (priority: cli args --> ic_config.json)
        cfg["padding_byte"] = (
            padding if padding is not None else cfg.get("padding_byte")
        )

    except Exception as e:
        log_error("Failed to load configuration: {}".format(e))
        sys.exit(-1)
    log_info("IC-{} config file loaded\n".format(cfg["ic"]))

    # -------------------------------------------------
    # 4️⃣ Locate TP, DP and possible extra bin files
    # -------------------------------------------------
    try:
        tp_dir = norm_path(os.getcwd(), cfg.get("tp_dir"))
        dp_dir = norm_path(os.getcwd(), cfg.get("dp_dir"))

        log_info("Try to locate TP bin...")

        if is_test_mode:
            tp_path = _ic_prefixed_bin(tp_dir, cfg.get("ic"))
        else:
            tp_path = _latest_bin(tp_dir)

        log_info("Try to locate DP bin...")

        if is_test_mode:
            dp_path = _ic_prefixed_bin(dp_dir, cfg.get("ic"))
        else:
            dp_path = _latest_bin(dp_dir)
    except IOError as e:
        log_error(e)
        sys.exit(-1)

    log_info("Located TP bin: {}".format(tp_path))
    log_info("Located DP bin: {}".format(dp_path))

    extra_paths = {}
    if cfg.get("extra_bins"):
        for extra in cfg["extra_bins"]:
            try:
                extra_dir_name = extra.get("dir", "%s_Bin" % extra["name"])
                extra_dir = norm_path(os.getcwd(), extra_dir_name)

                if is_test_mode:
                    extra_path = _ic_prefixed_bin(extra_dir, cfg.get("ic"))
                else:
                    extra_path = _latest_bin(extra_dir)

                extra_paths[extra["name"]] = extra_path
            except IOError as e:
                log_error(e)
                # continue – extra bins are optional
        for name, path in extra_paths.items():
            log_info("choose extra bin: {} -> {}".format(name, path))
    else:
        log_info("No extra bin is specified")
    log_info("All bins are located\n")

    # -------------------------------------------------
    # 5️⃣ Optional TP size check
    # -------------------------------------------------
    if cfg.get("tp_size_check"):
        log_info("Checking TP bin size...")
        expected = cfg["tp_bin_end_address"] - cfg["tp_bin_start_address"] + 1
        try:
            check_tp_size(cfg["ic"], tp_path, expected)
        except Exception as e:
            log_error(e)
            sys.exit(-1)
        log_info("TP bin size ok\n")
    else:
        log_info("Skip TP bin size check\n")

    # -------------------------------------------------
    # 6️⃣ Read all binaries
    # -------------------------------------------------
    log_info("Loading all bins...\n")
    log_info(
        "  TP bin range : 0x{:06X} ~ 0x{:06X}".format(
            cfg["tp_bin_start_address"], cfg["tp_bin_end_address"]
        )
    )
    tp_bin = read_bin_segment(
        tp_path, cfg["tp_bin_start_address"], cfg["tp_bin_end_address"]
    )
    log_info(
        "    loaded : {} bytes (0x{:06X}, {:.2f} KB)".format(
            len(tp_bin), len(tp_bin), len(tp_bin) / 1024.0
        )
    )

    log_info(
        "  DP bin range : 0x{:06X} ~ 0x{:06X}".format(
            cfg["dp_bin_start_address"], cfg["dp_bin_end_address"]
        )
    )
    dp_bin = read_bin_segment(
        dp_path, cfg["dp_bin_start_address"], cfg["dp_bin_end_address"]
    )
    log_info(
        "    loaded : {} bytes (0x{:06X}, {:.2f} KB)".format(
            len(dp_bin), len(dp_bin), len(dp_bin) / 1024.0
        )
    )

    extra_bins_data = {}
    if cfg.get("extra_bins"):
        for extra in cfg["extra_bins"]:
            name = extra["name"]
            log_info(
                '  Extra bin "{0}" range : 0x{1:06X} ~ 0x{2:06X}'.format(
                    name, extra["start"], extra["end"]
                )
            )
            extra_bins_data[name] = read_bin_segment(
                extra_paths[name], extra["start"], extra["end"]
            )
            log_info("    loaded : {} bytes".format(len(extra_bins_data[name])))
            log_info(
                "    loaded : {} bytes (0x{:06X}, {:.2f} KB)".format(
                    len(extra_bins_data[name]),
                    len(extra_bins_data[name]),
                    len(extra_bins_data[name]) / 1024.0,
                )
            )
    else:
        log_info("  No extra bins defined for this IC.")
    print("")

    # -------------------------------------------------
    # 7️⃣ Build flash image
    # -------------------------------------------------
    try:
        flash, versions = build_flash(
            cfg, tp_bin, dp_bin, extra_bins_data, cfg["padding_byte"]
        )
    except Exception as e:
        log_error("Failed to build flash image: {}".format(e))
        sys.exit(-1)

    # -------------------------------------------------
    # 8️⃣ Write the final file
    # -------------------------------------------------
    out_name = make_filename(cfg, versions, cfg["join_version"])
    out_dir = norm_path(os.getcwd(), cfg.get("out_dir"))
    ensure_dir(out_dir)

    out_path = os.path.join(out_dir, out_name)
    write_bin(out_path, flash)

    log_info(
        "Flash image written to: {}, flash size: 0x{:06X}\n".format(
            out_path, len(flash)
        )
    )


if __name__ == "__main__":
    main()

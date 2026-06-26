# -*- coding: utf-8 -*-
"""
Configuration loading and tiny arithmetic helpers.
All numeric conversion / expression evaluation lives here.
"""

from __future__ import unicode_literals, print_function
import json

# XXX: workaround
# Python 2: basestring exists and includes str & unicode.
# Python 3: basestring is not defined, so we create an equivalent tuple.
try:                     # pragma: no cover   (Python 2)
    basestring          # noqa: F821
except NameError:        # pragma: no cover   (Python 3)
    basestring = (str, bytes)   # type: ignore

def eval_expr(expr):
    """
    Evaluate a tiny arithmetic expression (+ / -) and return an int.
    Supported forms:
        - plain numbers (decimal, 0x..., 0o..., 0b...)
        - a+b   (e.g. "0x3E000+0x15")
        - a-b   (e.g. "0x34FFF-0xFFF")
    Leading minus sign is also supported.
    """
    expr = expr.strip()
    try:
        return int(expr, 0)
    except ValueError:
        pass

    if "+" in expr:
        left, right = expr.split("+", 1)
        return int(left, 0) + int(right, 0)

    if "-" in expr:
        left, right = expr.rsplit("-", 1)
        return int(left, 0) - int(right, 0)

    raise ValueError("Unable to evaluate expression: %r" % expr)

def resolve_version_basic(start, end, ref, offset):
    """
    Resolve a version address from a reference point.
    * If ref is "start" or "end" → return integer address (start/end + offset).
    * If ref is a dict (JSON object) → return the dict unchanged,
      but add a field "_offset" containing the evaluated offset.
    * Otherwise raise ValueError.
    """
    off = eval_expr(offset)

    # 1️⃣ string reference (original behaviour)
    if isinstance(ref, basestring):
        if ref == "start":
            return start + off, off
        if ref == "end":
            return end + off, off
        raise ValueError("version_ref must be 'start' or 'end', got %s" % ref)

    # 2️⃣ dict reference (new behaviour)
    if isinstance(ref, dict):
        result = dict(ref)          # do not modify the original JSON object
        result["_offset"] = off
        return result, off
    
    # 3️⃣ anything else is illegal
    raise ValueError("Unsupported version_ref type: %r" % type(ref))

def load_config(ic_number, cfg_path):
    """
    Load ic_config.json and return a dictionary ready for the flash builder.
    All numeric fields are converted to int, version addresses are resolved,
    and global prefixes / directories are merged.
    """
    with open(cfg_path, "r") as f:
        cfg = json.load(f)

    # ----- Global section -------------------------------------------------
    # XXX: There's a better way to write this
    global_section = cfg.get("global", {}) or {}
    dp_prefix = global_section.get("dp_version_prefix", "D") or "D"
    tp_prefix = global_section.get("tp_version_prefix", "T") or "T"
    
    # path --> prevent empty path str, use or
    dp_dir = global_section.get("dp_dir") or "DP_Bin"
    tp_dir = global_section.get("tp_dir") or "TP_Bin"
    out_dir = global_section.get("out_dir") or "."
    
    padding = int(global_section.get("padding_byte", "0x00") or "0x00", 0)
    join_version = global_section.get("join_version", True)

    # ----- Per‑IC section -------------------------------------------------
    ic_key = str(ic_number)
    if ic_key not in cfg:
        raise KeyError("IC number %s not found in %s" % (ic_number, cfg_path))

    raw = cfg[ic_key]

    # flash_size → flash_end (inclusive)
    flash_size = int(raw["flash_size"], 0)
    raw["flash_size"] = flash_size

    # Primary addresses → int
    for key in (
        "tp_bin_start_address",
        "tp_bin_end_address",
        "dp_bin_start_address",
        "dp_bin_end_address",
    ):
        raw[key] = int(raw[key], 0)

    # Resolve version addresses (ref + offset) --> get absolute version address
    raw["tp_version_addr"], raw["tp_version_offset"] = resolve_version_basic(
        raw["tp_bin_start_address"],
        raw["tp_bin_end_address"],
        raw["tp_version_ref"],
        raw["tp_version_offset"],
    )
    raw["dp_version_addr"], raw["dp_version_offset"] = resolve_version_basic(
        raw["dp_bin_start_address"],
        raw["dp_bin_end_address"],
        raw["dp_version_ref"],
        raw["dp_version_offset"],
    )

    # Extra bins (if any) – same scheme
    for extra in raw.get("extra_bins", []):
        extra["start"] = int(extra["start"], 0)
        extra["end"] = int(extra["end"], 0)

        if "version_ref" in extra and "version_offset" in extra:
            extra["version_addr"], extra["version_offset"] = resolve_version_basic(
                extra["start"],
                extra["end"],
                extra["version_ref"],
                extra["version_offset"],
            )
        else:
            extra["version_addr"] = None

    # Misc flags / defaults
    raw["tp_size_check"] = bool(raw.get("tp_size_check", False))
    raw["flash_name_prefix"] = raw.get("flash_name_prefix", "")
    raw["dp_version_prefix"] = dp_prefix
    raw["tp_version_prefix"] = tp_prefix
    raw["tp_dir"] = tp_dir
    raw["dp_dir"] = dp_dir
    raw["out_dir"] = out_dir
    
    raw["padding_byte"] = padding
    raw["join_version"] = join_version

    # Default prefix for each extra bin (first capital letter)
    for extra in raw.get("extra_bins", []):
        extra.setdefault("version_prefix", extra["name"][0].upper())

    # Other information not in ic_config.json
    raw["ic"] = ic_number

    return raw

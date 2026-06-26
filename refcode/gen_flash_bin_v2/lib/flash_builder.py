# -*- coding: utf-8 -*-
"""
Core flash‑image generation algorithm.
Only pure data‑processing lives here – no file‑system access.
"""

from __future__ import unicode_literals, print_function
import datetime
import sys
from .logger import log_error, log_info

# XXX: workaround
# Python 2: basestring exists and includes str & unicode.
# Python 3: basestring is not defined, so we create an equivalent tuple.
try:                     # pragma: no cover   (Python 2)
    basestring          # noqa: F821
except NameError:        # pragma: no cover   (Python 3)
    basestring = (str, bytes)   # type: ignore
      
PY2 = sys.version_info[0] == 2

if PY2:
    # Python 2 的 Unicode 類別名稱是 unicode
    unicode_type = unicode   # noqa: F821
else:
    unicode_type = str

def calc_relative_addr(address, start_addr):
    """
    Convert *address* (int or dict) to an offset that can be used on the
    already‑loaded binary slice whose first byte corresponds to *start_addr*.
    """
    # ----- pure integer -------------------------------------------------
    if isinstance(address, int):
        return address - start_addr

    # ----- dict – ASCII search -----------------------------------------
    if isinstance(address, dict):
        # keep the dict unchanged; the caller will still need to know the
        # original start address for the final subtraction.
        # We embed the start address so that get_version_char can handle it.
        rel = dict(address)          # copy
        rel["_base_start"] = start_addr
        return rel

    raise ValueError("address must be int or dict, got %r" % type(address))

# ----------------------------------------------------------------------
# Find the last occurrence of an ASCII/Unicode pattern in binary data.
# Supports both Python 2 and Python 3.
# ----------------------------------------------------------------------
def _to_bytes(obj):
    """
    Convert *obj* to an immutable bytes object.
    Supported inputs:
        - bytes / bytearray
        - memoryview
        - unicode_type (Unicode string)
        - list[int] with values 0‑255
    Raises:
        TypeError  – unsupported type
        ValueError – list element out of byte range
    """
    # ---------------------------------------------------------
    # 1. Already a bytes object
    # ---------------------------------------------------------
    if isinstance(obj, bytes):
        return obj

    # ---------------------------------------------------------
    # 2. bytearray → bytes
    # ---------------------------------------------------------
    if isinstance(obj, bytearray):
        # In both Py2 and Py3, bytes(bytearray) returns an immutable bytes object
        return bytes(obj)

    # ---------------------------------------------------------
    # 3. memoryview → bytes
    # ---------------------------------------------------------
    if isinstance(obj, memoryview):
        return obj.tobytes()

    # ---------------------------------------------------------
    # 4. Unicode string → UTF‑8 encoded bytes
    # ---------------------------------------------------------
    if isinstance(obj, unicode_type):
        return obj.encode('utf-8')

    # ---------------------------------------------------------
    # 5. list[int] → bytes
    # ---------------------------------------------------------
    if isinstance(obj, list):
        # Validate that every element is a valid byte value
        for idx, val in enumerate(obj):
            if not isinstance(val, int):
                raise TypeError(
                    "list element {} is not an int: {}".format(idx, type(val).__name__)
                )
            if not (0 <= val <= 255):
                raise ValueError(
                    "list element {} out of byte range (0‑255): {}".format(idx, val)
                )
        # Convert via bytearray – works for both versions
        return bytes(bytearray(obj))

    # ---------------------------------------------------------
    # 6. Anything else is unsupported
    # ---------------------------------------------------------
    raise TypeError("Unsupported type for _to_bytes(): {}".format(type(obj).__name__))

def _find_last_occurrence_ascii(data, needle):
    """
    Return the index of the last occurrence of *needle* in *data*,
    or -1 if not found.
    Both arguments are internally converted to ``bytes`` via ``_to_bytes``,
    so the function works identically on Python 2 and Python 3.
    """
    data_bytes   = _to_bytes(data)
    needle_bytes = _to_bytes(needle)
    return data_bytes.rfind(needle_bytes)

# ----------------------------------------------------------------------
# Extract a version byte and format it as two‑character upper‑case hex.
# *address* can be an int or a dict {"type":"ascii","find_last_value":"..."}.
# ----------------------------------------------------------------------
def get_version_char(bin_data, address):
    """
    Return the version byte at *address* (int) or the byte found by the
    ASCII search defined in a dict.
    """
    if isinstance(address, dict):
        # New ASCII‑search mode – this function is used only after the
        # dict has been resolved to a concrete offset, so we still need
        # to perform the search here.
        if address.get("type") != "ascii":
            raise ValueError("Unsupported address dict type: %s" % address.get("type"))
        needle = address.get("find_last_value")
        if not isinstance(needle, basestring):
            raise ValueError("find_last_value must be a string or bytes")
        if isinstance(needle, str):
            needle = needle.encode('utf-8')
        pos = _find_last_occurrence_ascii(bin_data, needle)
        if pos == -1:
            raise ValueError("Pattern %r not found in binary data" % needle)
        offset = pos + len(needle) - 1 + address.get("_offset")
    else:
        offset = int(address)

    return "%02X" % bin_data[offset]


def build_flash(cfg, tp_bin, dp_bin, extra_bins, padding_byte):
    """
    Build the flash image.

    Returns
    -------
    flash : list[int]          # length = flash_end + 1
    versions : dict           # {"TP": "...", "DP": "...", "LD": "...", ...}
    """
    flash_len = cfg["flash_size"]
    log_info("Using padding byte: 0x{:02X}\n".format(padding_byte))
    flash = [padding_byte] * flash_len
    
    cfg["tp_rel"] = calc_relative_addr(cfg["tp_version_addr"], cfg["tp_bin_start_address"])
    cfg["dp_rel"] = calc_relative_addr(cfg["dp_version_addr"], cfg["dp_bin_start_address"])
    
    tp_ver = get_version_char(tp_bin, cfg["tp_rel"])
    dp_ver = get_version_char(dp_bin, cfg["dp_rel"])

    # Copy TP region
    for i in range(cfg["tp_bin_start_address"], cfg["tp_bin_end_address"] + 1):
        flash[i] = tp_bin[i - cfg["tp_bin_start_address"]]

    # Copy DP region
    for i in range(cfg["dp_bin_start_address"], cfg["dp_bin_end_address"] + 1):
        flash[i] = dp_bin[i - cfg["dp_bin_start_address"]]

    # Extra bins (e.g. LD)
    extra_versions = {}
    for extra in cfg.get("extra_bins", []):
        name = extra["name"]
        start = extra["start"]
        end = extra["end"]
        bin_data = extra_bins[name]

        # Copy the whole region
        for i in range(start, end + 1):
            flash[i] = bin_data[i - start]

        # Extract version if defined
        if extra.get("version_addr") is not None:
            extra["rel"] = calc_relative_addr(extra["version_addr"], start)
            ver = get_version_char(bin_data, extra["rel"])
        else:
            ver = None
        extra_versions[name] = ver

    versions = {"TP": tp_ver, "DP": dp_ver}
    versions.update(extra_versions)

    if len(flash) != flash_len:
        msg = "Unexpected flash length: 0x{:06X}. Expected: 0x{:06X}".format(
            len(flash), flash_len
        )
        log_error(msg)
        log_info("Leaving...")
        sys.exit(-1)
        
    log_info("version info: {}".format(versions))
    return flash, versions


def make_filename(cfg, versions, join_versions=True, version_sep="_"):
    """
    Build the flash file name.
    Parameters
    ----------
    cfg : dict
        Configuration dictionary containing prefixes and optional extra_bins.
    versions : dict
        Mapping of version keys (e.g., "DP", "TP", "L") to their values.
    join_versions : bool, optional
        If True, version parts are concatenated without separator.
        If False, they are joined by `version_sep`.
    version_sep : str, optional
        Separator used when `join_versions` is False. Default is "_".
    Returns
    -------
    str
        Complete file name, e.g. "NT51928_D1A_T2B_L0C_20231123.bin".
    """
    # Base name part
    parts = [cfg["flash_name_prefix"]]

    # Build individual version strings
    dp_part = "{}{}".format(cfg["dp_version_prefix"], versions["DP"])
    tp_part = "{}{}".format(cfg["tp_version_prefix"], versions["TP"])

    version_parts = [dp_part, tp_part]

    # Append extra bins if defined
    for extra in cfg.get("extra_bins", []):
        name = extra["name"]
        ver = versions.get(name)
        if ver:
            prefix = extra.get("version_prefix", name[0].upper())
            version_parts.append("{}{}".format(prefix, ver))

    # Choose concatenation method
    if join_versions:
        version_str = "".join(version_parts)          # no separator
    else:
        version_str = version_sep.join(version_parts) # with separator

    parts.append(version_str)

    # Date suffix YYYYMMDD
    today = datetime.date.today()
    date_str = "{:04d}{:02d}{:02d}".format(today.year, today.month, today.day)
    parts.append(date_str)

    # Final file name
    return "_".join(parts) + ".bin"

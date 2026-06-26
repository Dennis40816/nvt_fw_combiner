# -*- coding: utf-8 -*-
"""
File‑related helpers (binary read / write, directory creation).
All I/O is wrapped here so that the core algorithm never touches the filesystem.
"""

from __future__ import unicode_literals, print_function
import os
import errno
from .py_meta import get_py_major_version


def read_bin_segment(path, start, end):
    """
    Read a *segment* of a binary file and return a list[int] (0‑255).
    Raises ValueError / IOError – callers should catch and handle.
    """
    length = end - start + 1
    if length <= 0:
        raise ValueError("Invalid segment: start (%d) > end (%d)" % (start, end))

    with open(path, "rb") as f:
        f.seek(start)
        data = f.read(length)

    if len(data) != length:
        raise IOError(
            "File %s is too short: expected %d bytes from %d to %d, got %d"
            % (path, length, start, end, len(data))
        )

    # Version‑branch – use the centralised version check
    if get_py_major_version() < 3:  # Python 2
        return [ord(b) for b in data]
    else:  # Python 3+
        return list(data)


def write_bin(path, data):
    """
    Write a list[int] (0‑255) to *path*.
    Works on both Python 2 and Python 3 without explicit version checks.
    """
    # bytearray converts the list of ints to a mutable bytes object.
    blob = bytearray(data)  # -> type: bytearray in both Py2 & Py3

    # In Python 2, file.write accepts a bytearray because it implements the buffer protocol.
    # In Python 3, file.write also accepts a bytearray.
    with open(path, "wb") as f:
        f.write(blob)


def ensure_dir(path):
    """
    Ensure that *path* exists (create recursively if needed).
    Concurrency-safe: ignore EEXIST.
    """
    if not os.path.isdir(path):
        try:
            os.makedirs(path)
        except OSError as exc:
            if exc.errno != errno.EEXIST:
                raise


# ----------------------------------------------------------------------
# 1. Helper – return default when missing or empty string
# ----------------------------------------------------------------------
def get_str(mapping, key, default):
    """
    Retrieve ``mapping[key]`` unless the key is missing **or** the stored value
    is an empty string (``""``).

    Parameters
    ----------
    mapping : dict‑like
        The container that holds configuration values.
    key : str
        The key to look up.
    default : any
        Value to return when the key does not exist or the value is ``""``.

    Returns
    -------
    The stored value (unchanged) or ``default``.
    """
    value = mapping.get(key, default)
    if isinstance(value, str) and value == "":
        return default
    return value


# ----------------------------------------------------------------------
# 2. Normalise a path – two implementations (pathlib if available, else os.path)
# ----------------------------------------------------------------------
def _norm_path_os(base_dir, sub_dir, default_sub):
    """
    Normalise a path using only the standard ``os.path`` module.

    Steps performed:
        1. Replace an empty string with ``default_sub``.
        2. Expand ``~`` and environment variables.
        3. Join with ``base_dir``.
        4. Convert to an absolute path.
        5. Collapse duplicate separators, remove a trailing separator,
           and (on Windows) normalise the case.

    Returns
    -------
    str – a clean, absolute path.
    """
    # 1) empty string → default
    sub_dir = get_str({"tmp": sub_dir}, "tmp", default_sub)

    # 2) expand user home (~/…) and environment variables ($HOME, %USERPROFILE%)
    sub_dir = os.path.expanduser(sub_dir)
    sub_dir = os.path.expandvars(sub_dir)

    # 3) simple join – does not touch separators yet
    joined = os.path.join(base_dir, sub_dir)

    # 4) make absolute and collapse redundant parts
    normalized = os.path.normpath(os.path.abspath(joined))

    # 5) Windows: make the case uniform (C:\Foo == c:\foo)
    if os.name == "nt":
        normalized = os.path.normcase(normalized)

    return normalized


# Try to import pathlib (Python 3.4+ or the back‑port on Python 2)
try:
    from pathlib import Path

    def _norm_path_pathlib(base_dir, sub_dir, default_sub):
        """
        Normalise a path using ``pathlib.Path`` (more Pythonic).

        Mirrors the behaviour of ``_norm_path_os`` but uses the Path API:
            * ``Path.expanduser()`` – expands ``~``.
            * ``/`` operator – joins paths with the correct separator.
            * ``resolve()`` – makes the path absolute and collapses ``..``/``.``.
        Returns a ``Path`` object.
        """
        sub_dir = get_str({"tmp": sub_dir}, "tmp", default_sub)
        return (Path(base_dir) / sub_dir).expanduser().resolve()

    # Public wrapper – always called ``norm_path`` from outside
    def norm_path(base_dir, sub_dir, default_sub=""):
        """Return a clean, absolute path (Path object on Py3, str on Py2)."""
        return _norm_path_pathlib(base_dir, sub_dir, default_sub)

except Exception:  # pathlib not available → fall back to os.path version

    def norm_path(base_dir, sub_dir, default_sub=""):
        """Return a clean, absolute path (always a str)."""
        return _norm_path_os(base_dir, sub_dir, default_sub)

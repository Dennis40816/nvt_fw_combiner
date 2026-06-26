# -*- coding: utf-8 -*-
"""
Utility module that centralises Python interpreter version handling.
All version checks in the project must go through this file.
"""

from __future__ import print_function

import sys
import os
import platform

def get_py_meta_info():
    """
    Return a tuple (exe_path, major_version, full_version_string).
    """
    exe_path = os.path.realpath(sys.executable) if sys.executable else ""
    if not exe_path:
        print("[ERROR] not valid python interpreter path!")
        sys.exit(-1)

    ver = platform.python_version()
    major_ver = int(ver.split('.')[0])
    return exe_path, major_ver, ver

def get_py_major_version():
    """
    Return the major version of the running interpreter (2, 3, 4, …).
    """
    return sys.version_info[0]

def is_python2():
    return get_py_major_version() == 2

def is_python3():
    return get_py_major_version() == 3

def is_python4():
    """Placeholder for future Python 4 support."""
    return get_py_major_version() == 4

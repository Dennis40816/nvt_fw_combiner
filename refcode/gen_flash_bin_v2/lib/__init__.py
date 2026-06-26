# -*- coding: utf-8 -*-
"""
lib package – common utilities for the flash‑bin generator.
All public helpers are re‑exported here for convenience.
"""

from .py_meta import get_py_meta_info
from .logger import log_info, log_error
from .file_utils import read_bin_segment, write_bin, ensure_dir, norm_path
from .config import load_config
from .validation import validate_cli_args, check_tp_size, parse_args
from .flash_builder import build_flash, make_filename

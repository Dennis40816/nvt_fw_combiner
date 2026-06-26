# -*- coding: utf-8 -*-
"""
Simple logger that prints messages in a uniform way.
We avoid using the full `logging` module to keep the dependency footprint zero.
"""

from __future__ import unicode_literals, print_function


def _print(message, prefix):
    print("[{}] {}".format(prefix, message))


def log_info(msg):
    _print(msg, "INFO")


def log_error(msg):
    _print(msg, "ERROR")
    # Do NOT exit here – the caller decides when to terminate.

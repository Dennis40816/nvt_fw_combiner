"""Argument parser whose errors never echo the offending argument."""
from __future__ import annotations

import argparse


class FixedArgumentParser(argparse.ArgumentParser):
    """Argument errors print one fixed sentence. The default text echoes the offending argument,
    which can be a private file name."""

    def error(self, message: str):  # noqa: ARG002 - the message is deliberately not printed
        self.exit(2, "Invalid arguments.\n")

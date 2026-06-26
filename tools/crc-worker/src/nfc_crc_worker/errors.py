"""Stable protocol errors and process exit codes."""

from dataclasses import dataclass
from enum import IntEnum


class ExitCode(IntEnum):
    """Process exit codes defined by protocol 1.0."""

    SUCCESS = 0
    REQUEST_ERROR = 2
    COMPATIBILITY_ERROR = 3
    CALCULATION_ERROR = 4
    SELF_TEST_ERROR = 5


@dataclass(frozen=True, slots=True)
class WorkerError(Exception):
    """Expected failure that can be returned as a stable protocol error."""

    code: str
    message: str
    exit_code: ExitCode
    request_id: str = ""

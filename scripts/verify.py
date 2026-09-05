"""Canonical cross-platform verification entry point for NFC and Codex."""

from __future__ import annotations

import argparse
import base64
import codecs
import ctypes
from fnmatch import fnmatch
import hashlib
import importlib.util
import json
import os
import re
import secrets
import shutil
import signal
import stat
import subprocess
import sys
import tempfile
import threading
import xml.etree.ElementTree as ET
from collections import Counter
from collections.abc import Callable, Sequence
from concurrent.futures import ThreadPoolExecutor, as_completed
from contextlib import ExitStack, contextmanager
from contextvars import ContextVar
from ctypes import wintypes
from dataclasses import dataclass
from importlib import metadata as importlib_metadata
from pathlib import Path, PurePosixPath
from time import monotonic
from unittest.loader import VALID_MODULE_NAME

if __package__:
    from .canonical_golden_validation import validate_canonical_golden
    from .coverage_policy import (
        load_baseline,
        repository_relative_coverage_source,
        verify_coverage,
    )
else:
    from canonical_golden_validation import validate_canonical_golden
    from coverage_policy import (
        load_baseline,
        repository_relative_coverage_source,
        verify_coverage,
    )

ROOT = Path(__file__).resolve().parents[1]
WORKER_ROOT = ROOT / "tools" / "crc-worker"
SOLUTION = ROOT / "NvtFwCombiner.slnx"
CTRL_RAM_SENTINEL_CREATOR = ROOT / "scripts" / "create_ctrlram_universal_sentinel.py"
IDLE_BUILD_WORKER_STOPPER = ROOT / "scripts" / "stop-idle-build-workers.ps1"
REPOSITORY_SCRIPT_TESTS = ROOT / "tests" / "scripts"
REPOSITORY_SCRIPT_TEST_SHARDS = (
    ("repository-scripts-a-q", "test_[a-q]*.py"),
    ("repository-scripts-r", "test_r*.py"),
    ("repository-scripts-s-z", "test_[s-z]*.py"),
)
COVERAGE_ROOT = ROOT / "artifacts" / "coverage"
DOTNET_COVERAGE_WORK_ROOT = ROOT / "artifacts" / "cov-shadow"
CI_DOTNET_EVIDENCE_ROOT = ROOT / "artifacts" / "ci-dotnet-work"
CI_DOTNET_UPLOAD_ROOT = ROOT / "artifacts" / "ci-dotnet-upload"
WINDOWS_PROCESS_ORCHESTRATION_TEST = (
    "tests.scripts.test_verify_orchestration."
    "VerifyOrchestrationTests.test_windows_owned_job_kills_descendants_after_root_exit"
)
DEFAULT_VERIFY_JOBS = 3
MAXIMUM_VERIFY_JOBS = 3
DEFAULT_LANE_TIMEOUT_SECONDS = 900
LOCAL_DOTNET_COVERAGE_TIMEOUT_SECONDS = 480
MINIMUM_LANE_TIMEOUT_SECONDS = 60
MAXIMUM_LANE_TIMEOUT_SECONDS = 900
CLEANUP_TIMEOUT_SECONDS = 30
PROCESS_TERMINATION_TIMEOUT_SECONDS = 5
UNIX_LANE_GRACEFUL_TERMINATION_SECONDS = 2
WINDOWS_CREATE_SUSPENDED = 0x00000004
WINDOWS_CREATE_NEW_PROCESS_GROUP = 0x00000200
LOG_STREAM_CHUNK_BYTES = 64 * 1024
LANE_DEADLINE: ContextVar[float | None] = ContextVar(
    "verification_lane_deadline", default=None
)
CLEANUP_DEADLINE: ContextVar[float | None] = ContextVar(
    "verification_cleanup_deadline", default=None
)
INTERNAL_LANE_ENVIRONMENT_VARIABLE = "NFC_VERIFY_INTERNAL_LANE"
TEST_AREA_ENVIRONMENT_VARIABLE = "NFC_TEST_AREA_ROOT"
TEST_SESSION_ENVIRONMENT_VARIABLE = "NFC_TEST_SESSION_ROOT"
TEST_REPOSITORY_ROOT_ENVIRONMENT_VARIABLE = "NFC_TEST_REPOSITORY_ROOT"
TEST_SESSION_MARKER_NAME = ".nfc-test-session.json"
TEST_SESSION_MARKER_SCHEMA_VERSION = 1
TEST_SESSION_SCRATCH_DIRECTORIES = {
    "TEMP": "t",
    "TMP": "t",
    "TMPDIR": "t",
    "DOTNET_BUNDLE_EXTRACT_BASE_DIR": "dotnet-bundle",
    "RUFF_CACHE_DIR": "ruff-cache",
    "PYTHONPYCACHEPREFIX": "python-bytecode",
}
PYTHON_COVERAGE_OVERRIDE_ENVIRONMENT_VARIABLES = (
    "PYTEST_ADDOPTS",
    "COVERAGE_RCFILE",
    "COVERAGE_PROCESS_START",
)
CI_DOTNET_EVIDENCE_SCHEMA_VERSION = 2
DOTNET_PRODUCER_WINDOWS = "windows"
DOTNET_PRODUCER_NON_WINDOWS = "non-windows"
CI_DOTNET_PRODUCER_PLATFORM = DOTNET_PRODUCER_WINDOWS
CI_SOURCE_SHA_PATTERN = re.compile(r"[0-9a-f]{40}")
NUGET_VERSION_PATTERN = re.compile(r"[0-9]+(?:\.[0-9]+){2}(?:[-+][0-9A-Za-z.-]+)?")


class _JobObjectBasicLimitInformation(ctypes.Structure):
    _fields_ = [
        ("PerProcessUserTimeLimit", ctypes.c_longlong),
        ("PerJobUserTimeLimit", ctypes.c_longlong),
        ("LimitFlags", wintypes.DWORD),
        ("MinimumWorkingSetSize", ctypes.c_size_t),
        ("MaximumWorkingSetSize", ctypes.c_size_t),
        ("ActiveProcessLimit", wintypes.DWORD),
        ("Affinity", ctypes.c_size_t),
        ("PriorityClass", wintypes.DWORD),
        ("SchedulingClass", wintypes.DWORD),
    ]


class _IoCounters(ctypes.Structure):
    _fields_ = [
        ("ReadOperationCount", ctypes.c_ulonglong),
        ("WriteOperationCount", ctypes.c_ulonglong),
        ("OtherOperationCount", ctypes.c_ulonglong),
        ("ReadTransferCount", ctypes.c_ulonglong),
        ("WriteTransferCount", ctypes.c_ulonglong),
        ("OtherTransferCount", ctypes.c_ulonglong),
    ]


class _ByHandleFileInformation(ctypes.Structure):
    _fields_ = [
        ("FileAttributes", wintypes.DWORD),
        ("CreationTime", wintypes.FILETIME),
        ("LastAccessTime", wintypes.FILETIME),
        ("LastWriteTime", wintypes.FILETIME),
        ("VolumeSerialNumber", wintypes.DWORD),
        ("FileSizeHigh", wintypes.DWORD),
        ("FileSizeLow", wintypes.DWORD),
        ("NumberOfLinks", wintypes.DWORD),
        ("FileIndexHigh", wintypes.DWORD),
        ("FileIndexLow", wintypes.DWORD),
    ]


class _FileBasicInformation(ctypes.Structure):
    _fields_ = [
        ("CreationTime", ctypes.c_longlong),
        ("LastAccessTime", ctypes.c_longlong),
        ("LastWriteTime", ctypes.c_longlong),
        ("ChangeTime", ctypes.c_longlong),
        ("FileAttributes", wintypes.DWORD),
    ]


class _FileDispositionInformation(ctypes.Structure):
    _fields_ = [("DeleteFile", wintypes.BOOL)]


class _UnicodeString(ctypes.Structure):
    _fields_ = [
        ("Length", wintypes.USHORT),
        ("MaximumLength", wintypes.USHORT),
        ("Buffer", wintypes.LPWSTR),
    ]


class _ObjectAttributes(ctypes.Structure):
    _fields_ = [
        ("Length", wintypes.ULONG),
        ("RootDirectory", wintypes.HANDLE),
        ("ObjectName", ctypes.POINTER(_UnicodeString)),
        ("Attributes", wintypes.ULONG),
        ("SecurityDescriptor", ctypes.c_void_p),
        ("SecurityQualityOfService", ctypes.c_void_p),
    ]


class _IoStatusBlock(ctypes.Structure):
    _fields_ = [
        ("Status", ctypes.c_void_p),
        ("Information", ctypes.c_size_t),
    ]


class _JobObjectExtendedLimitInformation(ctypes.Structure):
    _fields_ = [
        ("BasicLimitInformation", _JobObjectBasicLimitInformation),
        ("IoInfo", _IoCounters),
        ("ProcessMemoryLimit", ctypes.c_size_t),
        ("JobMemoryLimit", ctypes.c_size_t),
        ("PeakProcessMemoryUsed", ctypes.c_size_t),
        ("PeakJobMemoryUsed", ctypes.c_size_t),
    ]


class WindowsKillOnCloseJob:
    """Own a Windows process tree after its root process exits."""

    JOB_OBJECT_EXTENDED_LIMIT_INFORMATION = 9
    JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000

    def __init__(self, handle: int) -> None:
        self._handle: int | None = handle
        self._lock = threading.Lock()

    @classmethod
    def attach(cls, process: subprocess.Popen[bytes]) -> WindowsKillOnCloseJob:
        kernel32 = cls._kernel32()
        job_handle = kernel32.CreateJobObjectW(None, None)
        if not job_handle:
            raise ctypes.WinError(ctypes.get_last_error())

        try:
            information = _JobObjectExtendedLimitInformation()
            information.BasicLimitInformation.LimitFlags = (
                cls.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            )
            if not kernel32.SetInformationJobObject(
                job_handle,
                cls.JOB_OBJECT_EXTENDED_LIMIT_INFORMATION,
                ctypes.byref(information),
                ctypes.sizeof(information),
            ):
                raise ctypes.WinError(ctypes.get_last_error())

            process_handle = wintypes.HANDLE(int(process._handle))
            if not kernel32.AssignProcessToJobObject(job_handle, process_handle):
                raise ctypes.WinError(ctypes.get_last_error())
            return cls(int(job_handle))
        except BaseException:
            kernel32.CloseHandle(job_handle)
            raise

    @staticmethod
    def _kernel32():
        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel32.CreateJobObjectW.argtypes = [ctypes.c_void_p, wintypes.LPCWSTR]
        kernel32.CreateJobObjectW.restype = wintypes.HANDLE
        kernel32.SetInformationJobObject.argtypes = [
            wintypes.HANDLE,
            ctypes.c_int,
            ctypes.c_void_p,
            wintypes.DWORD,
        ]
        kernel32.SetInformationJobObject.restype = wintypes.BOOL
        kernel32.AssignProcessToJobObject.argtypes = [
            wintypes.HANDLE,
            wintypes.HANDLE,
        ]
        kernel32.AssignProcessToJobObject.restype = wintypes.BOOL
        kernel32.TerminateJobObject.argtypes = [wintypes.HANDLE, wintypes.UINT]
        kernel32.TerminateJobObject.restype = wintypes.BOOL
        kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
        kernel32.CloseHandle.restype = wintypes.BOOL
        return kernel32

    def terminate(self) -> None:
        with self._lock:
            if self._handle is None:
                return
            kernel32 = self._kernel32()
            if not kernel32.TerminateJobObject(wintypes.HANDLE(self._handle), 1):
                raise ctypes.WinError(ctypes.get_last_error())

    def close(self) -> None:
        with self._lock:
            if self._handle is None:
                return
            kernel32 = self._kernel32()
            if not kernel32.CloseHandle(wintypes.HANDLE(self._handle)):
                raise ctypes.WinError(ctypes.get_last_error())
            self._handle = None


@dataclass(frozen=True)
class ProcessTerminationBoundary:
    """Platform ownership retained until every verifier subprocess is released."""

    unix_process_group_id: int | None = None
    unix_graceful_termination_seconds: float = 0
    windows_job: WindowsKillOnCloseJob | None = None

    def close(self) -> None:
        if self.windows_job is not None:
            self.windows_job.close()


class VerificationTerminationRequested(KeyboardInterrupt):
    """Cancellation raised when an external termination signal is received."""

    def __init__(self, signal_number: int) -> None:
        super().__init__(f"verification received signal {signal_number}")
        self.signal_number = signal_number


ACTIVE_PROCESSES: set[subprocess.Popen[bytes]] = set()
PROCESS_TERMINATION_BOUNDARIES: dict[
    subprocess.Popen[bytes], ProcessTerminationBoundary
] = {}
ACTIVE_PROCESSES_LOCK = threading.Lock()
PROCESS_HANDOFF_LOCK = threading.Lock()
PROCESS_CANCELLATION_REQUESTED = threading.Event()


LaneAction = Callable[[Path | None], None]


@dataclass(frozen=True)
class VerificationLane:
    """One independently executable owner in the canonical verification plan."""

    name: str
    action: LaneAction
    isolate_action: bool = False
    internal_name: str | None = None


@dataclass(frozen=True)
class LaneResult:
    """Stable outcome and isolated output location for one verification lane."""

    name: str
    succeeded: bool
    duration_seconds: float
    log_path: Path
    error: str | None = None


@dataclass(frozen=True)
class CiDotnetProject:
    """One exact test-project owner in the closed CI shard map."""

    relative_path: str
    requires_exclusive_local_coverage: bool = False
    requires_external_tools_fixture: bool = False

    @property
    def name(self) -> str:
        return Path(self.relative_path).stem


@dataclass(frozen=True)
class LocalDotnetCoverageStage:
    """One immutable test-output snapshot and its retained evidence location."""

    project: CiDotnetProject
    source_output: Path
    shadow_output: Path
    test_assembly: Path
    discovery_report: Path
    results_directory: Path
    source_hashes: dict[str, str]
    canonical_hashes: tuple[tuple[Path, str], ...]
    external_tools_source_root: Path | None = None
    external_tools_shadow_root: Path | None = None
    external_tools_hashes: dict[str, str] | None = None


@dataclass(frozen=True)
class TestSessionOwnership:
    root: Path
    session: Path
    marker_bytes: bytes
    root_identity: tuple[int, int]
    sessions_identity: tuple[int, int]
    session_identity: tuple[int, int]
    marker_identity: tuple[int, int]
    custody: TestSessionCustody | None = None


@dataclass
class DirectoryCustody:
    path: Path
    identity: tuple[int, int]
    windows_handle: int | None


@dataclass
class TestSessionCustody:
    root: DirectoryCustody
    sessions: DirectoryCustody
    session_handle: int | None
    marker_handle: int | None


CI_DOTNET_SHARDS: dict[str, tuple[CiDotnetProject, ...]] = {
    "bootstrap": (
        CiDotnetProject(
            "tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj",
            requires_external_tools_fixture=True,
        ),
    ),
    "ui": (
        CiDotnetProject(
            "tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj",
            requires_exclusive_local_coverage=True,
        ),
    ),
    "core": (
        CiDotnetProject(
            "tests/NvtFwCombiner.Domain.Tests/NvtFwCombiner.Domain.Tests.csproj",
        ),
        CiDotnetProject(
            "tests/NvtFwCombiner.Application.Tests/"
            "NvtFwCombiner.Application.Tests.csproj",
        ),
        CiDotnetProject(
            "tests/NvtFwCombiner.Infrastructure.Tests/"
            "NvtFwCombiner.Infrastructure.Tests.csproj",
        ),
        CiDotnetProject(
            "tests/NvtFwCombiner.ProfileContract.Tests/"
            "NvtFwCombiner.ProfileContract.Tests.csproj",
        ),
        CiDotnetProject(
            "tests/NvtFwCombiner.GoldenRegression.Tests/"
            "NvtFwCombiner.GoldenRegression.Tests.csproj",
        ),
        CiDotnetProject(
            "tests/NvtFwCombiner.Architecture.Tests/"
            "NvtFwCombiner.Architecture.Tests.csproj",
        ),
    ),
}
INFRASTRUCTURE_TEST_PROJECT = "NvtFwCombiner.Infrastructure.Tests"
INFRASTRUCTURE_VSTEST_SETTINGS = (
    "xUnit.ParallelizeTestCollections=false",
    "xUnit.MaxParallelThreads=1",
    "xUnit.DiagnosticMessages=true",
    "xUnit.LongRunningTestSeconds=30",
)


def remaining_timeout(timeout_seconds: float | None = None) -> float | None:
    """Return a command timeout bounded by every active verification deadline."""

    deadlines = tuple(
        deadline
        for deadline in (LANE_DEADLINE.get(), CLEANUP_DEADLINE.get())
        if deadline is not None
    )
    if not deadlines:
        return timeout_seconds
    remaining = min(deadline - monotonic() for deadline in deadlines)
    if remaining <= 0:
        raise subprocess.TimeoutExpired("verification lane", 0)
    return min(timeout_seconds, remaining) if timeout_seconds is not None else remaining


def force_kill_unix_process_group(
    process: subprocess.Popen[bytes],
    process_group_id: int,
) -> OSError | None:
    """Attempt one group-wide SIGKILL, retaining a root fallback and its error."""

    try:
        os.killpg(process_group_id, signal.SIGKILL)
    except ProcessLookupError:
        if process.poll() is None:
            process.kill()
    except OSError as error:
        if process.poll() is None:
            process.kill()
        return error
    return None


def terminate_process_tree(
    process: subprocess.Popen[bytes],
    boundary: ProcessTerminationBoundary | None = None,
) -> None:
    """Terminate a timed-out verifier command together with every descendant."""

    if boundary is None:
        with ACTIVE_PROCESSES_LOCK:
            boundary = PROCESS_TERMINATION_BOUNDARIES.get(process)
    root_is_running = process.poll() is None
    tree_termination_error: RuntimeError | None = None
    if sys.platform == "win32":
        if boundary is not None and boundary.windows_job is not None:
            try:
                boundary.windows_job.terminate()
            except OSError as error:
                tree_termination_error = RuntimeError(
                    "Windows verification process-tree termination was not "
                    f"confirmed: Job Object termination failed with {error}"
                )
        else:
            try:
                result = subprocess.run(
                    ["taskkill", "/PID", str(process.pid), "/T", "/F"],
                    check=False,
                    stdout=subprocess.DEVNULL,
                    stderr=subprocess.DEVNULL,
                    timeout=PROCESS_TERMINATION_TIMEOUT_SECONDS,
                )
                if result.returncode != 0:
                    tree_termination_error = RuntimeError(
                        "Windows verification process-tree termination was not "
                        f"confirmed: taskkill returned exit code {result.returncode}"
                    )
            except (OSError, subprocess.TimeoutExpired) as error:
                tree_termination_error = RuntimeError(
                    "Windows verification process-tree termination was not "
                    f"confirmed: taskkill failed with {type(error).__name__}: {error}"
                )
        if tree_termination_error is not None and root_is_running:
            process.kill()
    else:
        process_group_id = (
            boundary.unix_process_group_id if boundary is not None else process.pid
        )
        graceful_seconds = (
            boundary.unix_graceful_termination_seconds if boundary is not None else 0
        )
        if graceful_seconds > 0:
            try:
                os.killpg(process_group_id, signal.SIGTERM)
            except ProcessLookupError:
                pass
            except OSError:
                # Graceful shutdown is best-effort; the owned group must still
                # reach the mandatory SIGKILL boundary below.
                pass
            else:
                try:
                    process.wait(timeout=graceful_seconds)
                except subprocess.TimeoutExpired:
                    pass
        first_kill_error = force_kill_unix_process_group(process, process_group_id)
        if first_kill_error is not None:
            retry_kill_error = force_kill_unix_process_group(process, process_group_id)
            if retry_kill_error is not None:
                tree_termination_error = RuntimeError(
                    "Unix verification process-group termination was not "
                    "confirmed: killpg failed with "
                    f"{type(first_kill_error).__name__}: {first_kill_error}; "
                    "retry failed with "
                    f"{type(retry_kill_error).__name__}: {retry_kill_error}"
                )
    try:
        process.wait(timeout=PROCESS_TERMINATION_TIMEOUT_SECONDS)
    except subprocess.TimeoutExpired:
        if sys.platform == "win32":
            process.kill()
        else:
            process_group_id = (
                boundary.unix_process_group_id if boundary is not None else process.pid
            )
            retry_kill_error = force_kill_unix_process_group(process, process_group_id)
            if retry_kill_error is not None:
                if tree_termination_error is None:
                    tree_termination_error = RuntimeError(
                        "Unix verification process-group termination was not "
                        "confirmed: retry killpg failed with "
                        f"{type(retry_kill_error).__name__}: {retry_kill_error}"
                    )
        process.wait(timeout=PROCESS_TERMINATION_TIMEOUT_SECONDS)
    if tree_termination_error is not None:
        raise tree_termination_error


def terminate_active_processes() -> None:
    """Terminate every detached command currently owned by interrupted verification."""

    with ACTIVE_PROCESSES_LOCK:
        active_processes = tuple(
            (process, PROCESS_TERMINATION_BOUNDARIES.get(process))
            for process in ACTIVE_PROCESSES
        )
    errors: list[Exception] = []
    for process, boundary in active_processes:
        try:
            if boundary is None:
                terminate_process_tree(process)
            else:
                terminate_process_tree(process, boundary)
        except Exception as error:
            errors.append(error)
    if errors:
        details = "; ".join(f"{type(error).__name__}: {error}" for error in errors)
        raise RuntimeError(
            f"failed to terminate {len(errors)} active process tree(s): {details}"
        ) from errors[0]


def cancel_active_processes_after_handoffs() -> None:
    """Block new process creation and clean up after in-flight handoffs finish."""

    PROCESS_CANCELLATION_REQUESTED.set()
    with PROCESS_HANDOFF_LOCK:
        terminate_active_processes()


def process_group_options() -> dict[str, int] | dict[str, bool]:
    """Give every owned command a race-free platform termination boundary."""

    if sys.platform == "win32":
        if os.environ.get(INTERNAL_LANE_ENVIRONMENT_VARIABLE) == "1":
            return {"creationflags": WINDOWS_CREATE_SUSPENDED}
        return {
            "creationflags": (
                WINDOWS_CREATE_NEW_PROCESS_GROUP | WINDOWS_CREATE_SUSPENDED
            )
        }
    return {"start_new_session": True}


def register_active_process(
    process: subprocess.Popen[bytes],
    *,
    graceful_termination_seconds: float = 0,
) -> None:
    if sys.platform == "win32":
        boundary = ProcessTerminationBoundary(
            windows_job=WindowsKillOnCloseJob.attach(process)
        )
    else:
        try:
            process_group_id = os.getpgid(process.pid)
        except ProcessLookupError:
            process_group_id = process.pid
        boundary = ProcessTerminationBoundary(
            unix_process_group_id=process_group_id,
            unix_graceful_termination_seconds=graceful_termination_seconds,
        )
    with ACTIVE_PROCESSES_LOCK:
        PROCESS_TERMINATION_BOUNDARIES[process] = boundary
        ACTIVE_PROCESSES.add(process)


def resume_active_process(process: subprocess.Popen[bytes]) -> None:
    """Start a Windows process only after its kill-on-close Job Object is attached."""

    if sys.platform != "win32":
        return
    ntdll = ctypes.WinDLL("ntdll")
    ntdll.NtResumeProcess.argtypes = [wintypes.HANDLE]
    ntdll.NtResumeProcess.restype = wintypes.LONG
    status = ntdll.NtResumeProcess(wintypes.HANDLE(int(process._handle)))
    if status < 0:
        raise OSError(
            f"NtResumeProcess failed with NTSTATUS 0x{status & 0xFFFFFFFF:08x}"
        )


def activate_owned_process(
    process: subprocess.Popen[bytes],
    *,
    graceful_termination_seconds: float = 0,
) -> None:
    """Register the termination boundary before allowing a verifier process to run."""

    register_active_process(
        process,
        graceful_termination_seconds=graceful_termination_seconds,
    )
    resume_active_process(process)


@contextmanager
def handle_external_termination():
    """Translate SIGTERM into the verifier's owned-process cancellation path."""

    cancellation_was_requested = PROCESS_CANCELLATION_REQUESTED.is_set()

    def restore_cancellation_state() -> None:
        if cancellation_was_requested:
            PROCESS_CANCELLATION_REQUESTED.set()
        else:
            PROCESS_CANCELLATION_REQUESTED.clear()

    if threading.current_thread() is not threading.main_thread():
        try:
            yield
        finally:
            restore_cancellation_state()
        return

    def request_termination(signal_number: int, _frame: object) -> None:
        PROCESS_CANCELLATION_REQUESTED.set()
        raise VerificationTerminationRequested(signal_number)

    previous_handler = signal.signal(signal.SIGTERM, request_termination)
    try:
        yield
    finally:
        signal.signal(signal.SIGTERM, previous_handler)
        restore_cancellation_state()


@contextmanager
def defer_termination_signals_during_process_handoff():
    """Replay main-thread interrupts only after a created process has an owner."""

    if threading.current_thread() is not threading.main_thread():
        yield
        return

    deferred_signal: int | None = None

    def remember_interrupt(signal_number: int, _frame: object) -> None:
        nonlocal deferred_signal
        if deferred_signal is None:
            deferred_signal = signal_number

    signal_numbers = (signal.SIGINT, signal.SIGTERM)
    previous_handlers = {
        signal_number: signal.signal(signal_number, remember_interrupt)
        for signal_number in signal_numbers
    }
    try:
        yield
    finally:
        for signal_number, previous_handler in previous_handlers.items():
            signal.signal(signal_number, previous_handler)
        if deferred_signal is not None:
            signal.raise_signal(deferred_signal)


def unregister_active_process(process: subprocess.Popen[bytes]) -> None:
    with ACTIVE_PROCESSES_LOCK:
        ACTIVE_PROCESSES.discard(process)
        boundary = PROCESS_TERMINATION_BOUNDARIES.pop(process, None)
    if boundary is not None:
        boundary.close()


def start_owned_process(
    command: list[str],
    *,
    cwd: Path,
    environment: dict[str, str] | None,
    stdout: object | None = None,
    stderr: object | None = None,
    graceful_termination_seconds: float = 0,
) -> subprocess.Popen[bytes]:
    """Create and register a process inside one cancellation-safe exception scope."""

    process: subprocess.Popen[bytes] | None = None
    try:
        with PROCESS_HANDOFF_LOCK:
            if PROCESS_CANCELLATION_REQUESTED.is_set():
                raise RuntimeError("verification process creation was cancelled")
            with defer_termination_signals_during_process_handoff():
                process = subprocess.Popen(
                    command,
                    cwd=cwd,
                    stdout=stdout,
                    stderr=stderr,
                    env=environment,
                    **process_group_options(),
                )
                if graceful_termination_seconds > 0:
                    activate_owned_process(
                        process,
                        graceful_termination_seconds=graceful_termination_seconds,
                    )
                else:
                    activate_owned_process(process)
        return process
    except BaseException:
        if process is not None:
            try:
                terminate_process_tree(process)
            finally:
                unregister_active_process(process)
        raise


def command_timing_line(started_at: float) -> str:
    """Render one stable duration record for the immediately preceding command."""

    return f"Command timing: {monotonic() - started_at:.1f}s"


def run(
    command: list[str],
    *,
    cwd: Path = ROOT,
    environment: dict[str, str] | None = None,
    log_path: Path | None = None,
    mirror_log_path: Path | None = None,
    timeout_seconds: float | None = None,
    graceful_termination_seconds: float = 0,
) -> None:
    if log_path is None:
        command_started_at = monotonic()
        print(f"\n> {' '.join(command)}", flush=True)
        try:
            process = start_owned_process(
                command,
                cwd=cwd,
                environment=environment,
                graceful_termination_seconds=graceful_termination_seconds,
            )
            try:
                return_code = process.wait(timeout=remaining_timeout(timeout_seconds))
            except (subprocess.TimeoutExpired, KeyboardInterrupt):
                terminate_process_tree(process)
                raise
            finally:
                unregister_active_process(process)
            if return_code != 0:
                raise subprocess.CalledProcessError(return_code, command)
        finally:
            print(command_timing_line(command_started_at), flush=True)
        return

    _run_to_logs(
        command,
        log_paths=(log_path, mirror_log_path),
        cwd=cwd,
        environment=environment,
        echo=False,
        timeout_seconds=timeout_seconds,
        graceful_termination_seconds=graceful_termination_seconds,
    )


def write_console_text(text: str) -> None:
    """Write diagnostics without letting a legacy console encoding fail the gate."""

    encoding = getattr(sys.stdout, "encoding", None)
    if encoding:
        text = text.encode(encoding, errors="backslashreplace").decode(encoding)
    sys.stdout.write(text)


def stream_log_tail(
    primary_path: Path,
    *,
    start_offset: int,
    mirror_paths: Sequence[Path] = (),
    echo: bool,
) -> None:
    """Stream an appended log segment to mirrors and optional console output."""

    if not mirror_paths and not echo:
        return
    for mirror_path in mirror_paths:
        mirror_path.parent.mkdir(parents=True, exist_ok=True)
    decoder = codecs.getincrementaldecoder("utf-8")(errors="replace") if echo else None
    with primary_path.open("rb") as source, ExitStack() as stack:
        source.seek(start_offset)
        mirrors = tuple(
            stack.enter_context(mirror_path.open("ab")) for mirror_path in mirror_paths
        )
        while chunk := source.read(LOG_STREAM_CHUNK_BYTES):
            for mirror in mirrors:
                mirror.write(chunk)
            if decoder is not None:
                write_console_text(decoder.decode(chunk))
        if decoder is not None:
            write_console_text(decoder.decode(b"", final=True))


def _run_to_logs(
    command: list[str],
    *,
    log_paths: tuple[Path | None, ...],
    cwd: Path,
    environment: dict[str, str] | None,
    echo: bool,
    timeout_seconds: float | None = None,
    graceful_termination_seconds: float = 0,
) -> None:
    """Run one command while writing identical output to one or two log files."""

    unique_paths = tuple(dict.fromkeys(path for path in log_paths if path is not None))
    if not unique_paths:
        raise ValueError("captured verification command requires at least one log path")
    primary_path = unique_paths[0]
    primary_path.parent.mkdir(parents=True, exist_ok=True)
    start_offset = primary_path.stat().st_size if primary_path.exists() else 0
    command_started_at = monotonic()
    try:
        with primary_path.open("a", encoding="utf-8", newline="\n") as primary:
            print(f"\n> {' '.join(command)}", file=primary, flush=True)
            try:
                process = start_owned_process(
                    command,
                    cwd=cwd,
                    stdout=primary,
                    stderr=subprocess.STDOUT,
                    environment=environment,
                    graceful_termination_seconds=graceful_termination_seconds,
                )
                try:
                    return_code = process.wait(
                        timeout=remaining_timeout(timeout_seconds)
                    )
                except (subprocess.TimeoutExpired, KeyboardInterrupt):
                    terminate_process_tree(process)
                    raise
                finally:
                    unregister_active_process(process)
            finally:
                print(
                    command_timing_line(command_started_at),
                    file=primary,
                    flush=True,
                )
        stream_log_tail(
            primary_path,
            start_offset=start_offset,
            mirror_paths=unique_paths[1:],
            echo=echo,
        )
        if return_code != 0:
            raise subprocess.CalledProcessError(return_code, command)
    except (subprocess.TimeoutExpired, KeyboardInterrupt):
        stream_log_tail(
            primary_path,
            start_offset=start_offset,
            mirror_paths=unique_paths[1:],
            echo=echo,
        )
        raise


def run_with_log(
    command: list[str],
    log_path: Path,
    *,
    cwd: Path = ROOT,
    environment: dict[str, str] | None = None,
) -> None:
    print(f"\n> {' '.join(command)}", flush=True)
    log_path.unlink(missing_ok=True)
    _run_to_logs(
        command,
        log_paths=(log_path,),
        cwd=cwd,
        environment=environment,
        echo=True,
    )


def verify_structure(log_path: Path | None = None) -> None:
    run([sys.executable, "scripts/sync_derived.py"], log_path=log_path)
    run([sys.executable, "scripts/validate_repository.py"], log_path=log_path)
    run([sys.executable, "scripts/polytail_check.py"], log_path=log_path)
    run(
        [sys.executable, str(CTRL_RAM_SENTINEL_CREATOR), "--dry-run"],
        log_path=log_path,
    )


def verify_repository_scripts(
    log_path: Path | None = None,
    pattern: str = "test_*.py",
) -> None:
    run(
        [
            sys.executable,
            "-m",
            "unittest",
            "discover",
            "-s",
            str(REPOSITORY_SCRIPT_TESTS),
            "-p",
            pattern,
        ],
        log_path=log_path,
    )


def is_reparse_point(path: Path) -> bool:
    """Return whether a path is a symbolic link or Windows junction."""

    try:
        status = os.lstat(path)
    except FileNotFoundError:
        return False
    attributes = getattr(status, "st_file_attributes", 0)
    return stat.S_ISLNK(status.st_mode) or bool(
        attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    )


def normalized_filesystem_path(path: Path) -> str:
    """Return one stable host-native identity for an already absolute path."""

    return os.path.normcase(str(path.resolve(strict=False)))


def validate_existing_path_components(path: Path, *, description: str) -> Path:
    """Reject every existing link, junction, or reparse component without writes."""

    absolute = path.absolute()
    anchor = Path(absolute.anchor)
    current = anchor
    try:
        relative = absolute.relative_to(anchor)
    except ValueError as error:
        raise RuntimeError(f"invalid absolute {description}: {path}") from error
    for part in relative.parts:
        current = current.parent if part == ".." else current / part
        if is_reparse_point(current):
            raise RuntimeError(
                f"symbolic link or junction/reparse-point {description} is forbidden: "
                f"{current}"
            )
    resolved = path.resolve(strict=False)
    resolved_anchor = Path(resolved.anchor)
    current = resolved_anchor
    for part in resolved.relative_to(resolved_anchor).parts:
        current /= part
        if is_reparse_point(current):
            raise RuntimeError(
                f"symbolic link or junction/reparse-point {description} is forbidden: "
                f"{current}"
            )
    return resolved


def filesystem_paths_overlap(first: Path, second: Path) -> bool:
    """Return whether either normalized path contains the other."""

    first_identity = normalized_filesystem_path(first)
    second_identity = normalized_filesystem_path(second)
    try:
        common = os.path.commonpath((first_identity, second_identity))
    except ValueError:
        return False
    return common in {first_identity, second_identity}


def validate_test_area_root(path: Path, *, may_create: bool) -> Path:
    """Validate one canonical test area before any verifier-owned write."""

    if not path.is_absolute():
        raise RuntimeError(f"{TEST_AREA_ENVIRONMENT_VARIABLE} must be absolute")
    root = validate_existing_path_components(path, description="test-area root")
    if root == Path(root.anchor).resolve(strict=False):
        raise RuntimeError("test-area root cannot be a filesystem root")
    if filesystem_paths_overlap(root, ROOT):
        raise RuntimeError("test-area root and repository must not overlap")
    if root.exists():
        if not root.is_dir():
            raise RuntimeError("test-area root must be a directory")
    elif not may_create:
        raise RuntimeError("local test-area root must be an existing directory")
    return root


def resolve_test_area_root() -> tuple[Path, bool]:
    """Resolve the local declaration or the exact GitHub runner-derived root."""

    declared = os.environ.get(TEST_AREA_ENVIRONMENT_VARIABLE)
    if os.environ.get("GITHUB_ACTIONS") != "true":
        if declared is None or not declared.strip():
            raise RuntimeError(
                f"local verification requires explicit {TEST_AREA_ENVIRONMENT_VARIABLE}"
            )
        return validate_test_area_root(Path(declared), may_create=False), False

    runner_temp_value = os.environ.get("RUNNER_TEMP")
    if runner_temp_value is None or not runner_temp_value.strip():
        raise RuntimeError("GitHub Actions verification requires RUNNER_TEMP")
    runner_temp = Path(runner_temp_value)
    if not runner_temp.is_absolute():
        raise RuntimeError("RUNNER_TEMP must be absolute")
    runner_temp = validate_existing_path_components(
        runner_temp, description="GitHub runner temporary root"
    )
    if not runner_temp.is_dir():
        raise RuntimeError("RUNNER_TEMP must be an existing directory")
    derived = validate_test_area_root(
        runner_temp / "NvtFwCombiner-TestArea", may_create=True
    )
    if declared is not None and declared.strip():
        declared_root = validate_test_area_root(Path(declared), may_create=True)
        if normalized_filesystem_path(declared_root) != normalized_filesystem_path(
            derived
        ):
            raise RuntimeError(
                f"{TEST_AREA_ENVIRONMENT_VARIABLE} conflicts with RUNNER_TEMP-derived root"
            )
    return derived, True


def test_session_marker_bytes(root: Path, session: Path) -> bytes:
    """Bind one session marker to its schema, normalized owner, and identity."""

    document = {
        "normalizedRoot": normalized_filesystem_path(root),
        "schemaVersion": TEST_SESSION_MARKER_SCHEMA_VERSION,
        "sessionId": session.name,
    }
    return (json.dumps(document, sort_keys=True) + "\n").encode("utf-8")


def _windows_file_api():
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel32.CreateFileW.argtypes = [
        wintypes.LPCWSTR,
        wintypes.DWORD,
        wintypes.DWORD,
        ctypes.c_void_p,
        wintypes.DWORD,
        wintypes.DWORD,
        wintypes.HANDLE,
    ]
    kernel32.CreateFileW.restype = wintypes.HANDLE
    kernel32.GetFileInformationByHandle.argtypes = [
        wintypes.HANDLE,
        ctypes.POINTER(_ByHandleFileInformation),
    ]
    kernel32.GetFileInformationByHandle.restype = wintypes.BOOL
    kernel32.GetFileInformationByHandleEx.argtypes = [
        wintypes.HANDLE,
        ctypes.c_int,
        ctypes.c_void_p,
        wintypes.DWORD,
    ]
    kernel32.GetFileInformationByHandleEx.restype = wintypes.BOOL
    kernel32.SetFileInformationByHandle.argtypes = [
        wintypes.HANDLE,
        ctypes.c_int,
        ctypes.c_void_p,
        wintypes.DWORD,
    ]
    kernel32.SetFileInformationByHandle.restype = wintypes.BOOL
    kernel32.SetFilePointerEx.argtypes = [
        wintypes.HANDLE,
        ctypes.c_longlong,
        ctypes.POINTER(ctypes.c_longlong),
        wintypes.DWORD,
    ]
    kernel32.SetFilePointerEx.restype = wintypes.BOOL
    kernel32.ReadFile.argtypes = [
        wintypes.HANDLE,
        ctypes.c_void_p,
        wintypes.DWORD,
        ctypes.POINTER(wintypes.DWORD),
        ctypes.c_void_p,
    ]
    kernel32.ReadFile.restype = wintypes.BOOL
    kernel32.WriteFile.argtypes = [
        wintypes.HANDLE,
        ctypes.c_void_p,
        wintypes.DWORD,
        ctypes.POINTER(wintypes.DWORD),
        ctypes.c_void_p,
    ]
    kernel32.WriteFile.restype = wintypes.BOOL
    kernel32.FlushFileBuffers.argtypes = [wintypes.HANDLE]
    kernel32.FlushFileBuffers.restype = wintypes.BOOL
    kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel32.CloseHandle.restype = wintypes.BOOL
    return kernel32


def _windows_file_api_path(path: Path) -> str:
    """Return an absolute extended-length path without resolving filesystem state."""

    absolute = str(path.absolute())
    if absolute.startswith("\\\\.\\"):
        raise RuntimeError("Windows device namespace paths are forbidden")
    if absolute.startswith("\\\\?\\"):
        extended = absolute[4:]
        is_drive = re.match(r"^[A-Za-z]:\\", extended) is not None
        unc_parts = (
            extended[4:].split("\\")
            if extended[:4].casefold() == "unc\\"
            else []
        )
        is_unc = len(unc_parts) >= 2 and all(unc_parts[:2])
        if not (is_drive or is_unc):
            raise RuntimeError("Windows extended namespace paths are forbidden")
        return absolute
    if absolute.startswith("\\\\"):
        return "\\\\?\\UNC\\" + absolute[2:]
    return "\\\\?\\" + absolute


def _open_windows_path(
    path: Path,
    *,
    delete_access: bool,
    share_delete: bool,
    share_write: bool = True,
    read_data: bool = False,
) -> int:
    kernel32 = _windows_file_api()
    desired_access = (
        0x00000080
        | (0x00010100 if delete_access else 0)
        | (0x80000000 if read_data else 0)
    )
    share_mode = 0x00000001
    if share_write:
        share_mode |= 0x00000002
    if share_delete:
        share_mode |= 0x00000004
    flags = 0x02000000 | 0x00200000
    handle = kernel32.CreateFileW(
        _windows_file_api_path(path),
        desired_access,
        share_mode,
        None,
        3,
        flags,
        None,
    )
    invalid_handle = ctypes.c_void_p(-1).value
    if handle == invalid_handle:
        raise ctypes.WinError(ctypes.get_last_error())
    return int(handle)


def _create_windows_relative_path(
    root_handle: int,
    name: str,
    *,
    directory: bool,
) -> int:
    """Create one exclusive direct child bound to an already-owned directory."""

    if not name or Path(name).name != name or name in {".", ".."}:
        raise RuntimeError(f"relative Windows child name is invalid: {name}")
    ntdll = ctypes.WinDLL("ntdll")
    ntdll.NtCreateFile.argtypes = [
        ctypes.POINTER(wintypes.HANDLE),
        wintypes.DWORD,
        ctypes.POINTER(_ObjectAttributes),
        ctypes.POINTER(_IoStatusBlock),
        ctypes.c_void_p,
        wintypes.ULONG,
        wintypes.ULONG,
        wintypes.ULONG,
        wintypes.ULONG,
        ctypes.c_void_p,
        wintypes.ULONG,
    ]
    ntdll.NtCreateFile.restype = ctypes.c_long
    ntdll.RtlNtStatusToDosError.argtypes = [ctypes.c_long]
    ntdll.RtlNtStatusToDosError.restype = wintypes.ULONG
    buffer = ctypes.create_unicode_buffer(name)
    object_name = _UnicodeString(
        len(name) * ctypes.sizeof(ctypes.c_wchar),
        (len(name) + 1) * ctypes.sizeof(ctypes.c_wchar),
        ctypes.cast(buffer, wintypes.LPWSTR),
    )
    attributes = _ObjectAttributes(
        ctypes.sizeof(_ObjectAttributes),
        wintypes.HANDLE(root_handle),
        ctypes.pointer(object_name),
        0x00000040,
        None,
        None,
    )
    io_status = _IoStatusBlock()
    child_handle = wintypes.HANDLE()
    desired_access = 0x00100180 | (0x00000001 if directory else 0x00000003)
    create_options = 0x00200020 | (0x00000001 if directory else 0x00000040)
    status = ntdll.NtCreateFile(
        ctypes.byref(child_handle),
        desired_access,
        ctypes.byref(attributes),
        ctypes.byref(io_status),
        None,
        0,
        0x00000003,
        2,
        create_options,
        None,
        0,
    )
    if status < 0:
        raise ctypes.WinError(int(ntdll.RtlNtStatusToDosError(status)))
    return int(child_handle.value)


def _windows_handle_facts(handle: int) -> tuple[tuple[int, int], int]:
    information = _ByHandleFileInformation()
    if not _windows_file_api().GetFileInformationByHandle(
        wintypes.HANDLE(handle), ctypes.byref(information)
    ):
        raise ctypes.WinError(ctypes.get_last_error())
    identity = (
        int(information.VolumeSerialNumber),
        (int(information.FileIndexHigh) << 32) | int(information.FileIndexLow),
    )
    return identity, int(information.FileAttributes)


def _close_windows_handle(handle: int | None) -> None:
    if handle is not None and not _windows_file_api().CloseHandle(
        wintypes.HANDLE(handle)
    ):
        raise ctypes.WinError(ctypes.get_last_error())


def _mark_windows_handle_for_deletion(handle: int) -> None:
    disposition = _FileDispositionInformation(True)
    if not _windows_file_api().SetFileInformationByHandle(
        wintypes.HANDLE(handle),
        4,
        ctypes.byref(disposition),
        ctypes.sizeof(disposition),
    ):
        raise ctypes.WinError(ctypes.get_last_error())


def _clear_windows_readonly(handle: int) -> None:
    information = _FileBasicInformation()
    kernel32 = _windows_file_api()
    if not kernel32.GetFileInformationByHandleEx(
        wintypes.HANDLE(handle),
        0,
        ctypes.byref(information),
        ctypes.sizeof(information),
    ):
        raise ctypes.WinError(ctypes.get_last_error())
    readonly = getattr(stat, "FILE_ATTRIBUTE_READONLY", 0x1)
    if not information.FileAttributes & readonly:
        return
    information.FileAttributes &= ~readonly
    if not kernel32.SetFileInformationByHandle(
        wintypes.HANDLE(handle),
        0,
        ctypes.byref(information),
        ctypes.sizeof(information),
    ):
        raise ctypes.WinError(ctypes.get_last_error())


def _read_windows_handle(handle: int) -> bytes:
    kernel32 = _windows_file_api()
    position = ctypes.c_longlong()
    if not kernel32.SetFilePointerEx(
        wintypes.HANDLE(handle),
        0,
        ctypes.byref(position),
        0,
    ):
        raise ctypes.WinError(ctypes.get_last_error())
    chunks: list[bytes] = []
    while True:
        buffer = ctypes.create_string_buffer(4096)
        read = wintypes.DWORD()
        if not kernel32.ReadFile(
            wintypes.HANDLE(handle),
            buffer,
            len(buffer),
            ctypes.byref(read),
            None,
        ):
            raise ctypes.WinError(ctypes.get_last_error())
        if read.value == 0:
            return b"".join(chunks)
        chunks.append(buffer.raw[: read.value])


def _write_windows_handle(handle: int, content: bytes) -> None:
    kernel32 = _windows_file_api()
    buffer = ctypes.create_string_buffer(content)
    written = wintypes.DWORD()
    if not kernel32.WriteFile(
        wintypes.HANDLE(handle),
        buffer,
        len(content),
        ctypes.byref(written),
        None,
    ):
        raise ctypes.WinError(ctypes.get_last_error())
    if written.value != len(content):
        raise OSError(
            f"short write for test session marker: {written.value}/{len(content)}"
        )
    if not kernel32.FlushFileBuffers(wintypes.HANDLE(handle)):
        raise ctypes.WinError(ctypes.get_last_error())


def _prepare_windows_handle_for_deletion(
    handle: int,
    expected_identity: tuple[int, int],
    *,
    description: str,
) -> None:
    reparse = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    readonly = getattr(stat, "FILE_ATTRIBUTE_READONLY", 0x1)
    identity, attributes = _windows_handle_facts(handle)
    if identity != expected_identity:
        raise RuntimeError(f"{description} identity changed before deletion")
    if attributes & reparse:
        raise RuntimeError(f"reparse-point {description} is forbidden")
    _clear_windows_readonly(handle)
    identity, attributes = _windows_handle_facts(handle)
    if identity != expected_identity:
        raise RuntimeError(f"{description} identity changed before deletion")
    if attributes & reparse:
        raise RuntimeError(f"reparse-point {description} is forbidden")
    if attributes & readonly:
        raise RuntimeError(f"readonly {description} could not be cleared")


def filesystem_identity(path: Path) -> tuple[int, int]:
    """Return the stable host identity used to reject path replacement."""

    if sys.platform != "win32":
        status = os.lstat(path)
        return int(status.st_dev), int(status.st_ino)
    handle = _open_windows_path(path, delete_access=False, share_delete=True)
    try:
        identity, _attributes = _windows_handle_facts(handle)
        return identity
    finally:
        _close_windows_handle(handle)


def directory_custody_from_windows_handle(
    path: Path,
    handle: int,
) -> DirectoryCustody:
    try:
        identity, attributes = _windows_handle_facts(handle)
        if attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400):
            raise RuntimeError(f"reparse-point custody directory is forbidden: {path}")
        if not attributes & getattr(stat, "FILE_ATTRIBUTE_DIRECTORY", 0x10):
            raise RuntimeError(f"custody path must be a directory: {path}")
        return DirectoryCustody(path, identity, handle)
    except BaseException:
        _close_windows_handle(handle)
        raise


def acquire_directory_custody(path: Path) -> DirectoryCustody:
    """Retain one verified directory identity across child creation writes."""

    if is_reparse_point(path):
        raise RuntimeError(f"reparse-point custody directory is forbidden: {path}")
    identity = filesystem_identity(path)
    if sys.platform != "win32":
        if not path.is_dir():
            raise RuntimeError(f"custody path must be a directory: {path}")
        return DirectoryCustody(path, identity, None)
    handle = _open_windows_path(
        path,
        delete_access=False,
        share_delete=False,
    )
    custody = directory_custody_from_windows_handle(path, handle)
    if custody.identity != identity:
        release_directory_custody(custody)
        raise RuntimeError(f"custody directory identity changed: {path}")
    return custody


def revalidate_directory_custody(custody: DirectoryCustody) -> None:
    if is_reparse_point(custody.path):
        raise RuntimeError(
            f"reparse-point custody directory is forbidden: {custody.path}"
        )
    if filesystem_identity(custody.path) != custody.identity:
        raise RuntimeError(f"custody directory identity changed: {custody.path}")
    if custody.windows_handle is None:
        if not custody.path.is_dir():
            raise RuntimeError(f"custody path must be a directory: {custody.path}")
        return
    identity, attributes = _windows_handle_facts(custody.windows_handle)
    if identity != custody.identity:
        raise RuntimeError(f"custody directory identity changed: {custody.path}")
    if attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400):
        raise RuntimeError(
            f"reparse-point custody directory is forbidden: {custody.path}"
        )
    if not attributes & getattr(stat, "FILE_ATTRIBUTE_DIRECTORY", 0x10):
        raise RuntimeError(f"custody path must be a directory: {custody.path}")


def release_directory_custody(custody: DirectoryCustody | None) -> None:
    if custody is not None and custody.windows_handle is not None:
        _close_windows_handle(custody.windows_handle)
        custody.windows_handle = None


def release_test_session_custody(ownership: TestSessionOwnership) -> None:
    custody = ownership.custody
    if custody is None:
        return
    handles = (
        ("marker", custody.marker_handle),
        ("session", custody.session_handle),
        ("sessions root", custody.sessions.windows_handle),
        ("test-area root", custody.root.windows_handle),
    )
    custody.marker_handle = None
    custody.session_handle = None
    custody.sessions.windows_handle = None
    custody.root.windows_handle = None
    failures: list[tuple[str, int, BaseException]] = []
    for label, handle in handles:
        if handle is None:
            continue
        try:
            _close_windows_handle(handle)
        except BaseException as error:
            failures.append((label, handle, error))
    if failures:
        details = "; ".join(
            f"{label} handle {handle}: {type(error).__name__}: {error}"
            for label, handle, error in failures
        )
        raise RuntimeError(f"test session custody release failed: {details}") from (
            failures[0][2]
        )


def revalidate_test_session_custody(ownership: TestSessionOwnership) -> None:
    custody = ownership.custody
    if custody is None:
        return
    revalidate_directory_custody(custody.root)
    revalidate_directory_custody(custody.sessions)
    if custody.session_handle is None or custody.marker_handle is None:
        raise RuntimeError("test session custody was released before workload exit")
    session_identity, session_attributes = _windows_handle_facts(custody.session_handle)
    if session_identity != ownership.session_identity:
        raise RuntimeError("test session custody identity changed")
    if session_attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400):
        raise RuntimeError("test session custody became a reparse point")
    marker_identity, marker_attributes = _windows_handle_facts(custody.marker_handle)
    if marker_identity != ownership.marker_identity:
        raise RuntimeError("test session marker custody identity changed")
    if marker_attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400):
        raise RuntimeError("test session marker custody became a reparse point")
    if _read_windows_handle(custody.marker_handle) != ownership.marker_bytes:
        raise RuntimeError("test session marker custody content changed")
    if filesystem_identity(ownership.session) != ownership.session_identity:
        raise RuntimeError("test session path identity changed")
    marker = ownership.session / TEST_SESSION_MARKER_NAME
    if filesystem_identity(marker) != ownership.marker_identity:
        raise RuntimeError("test session marker path identity changed")


def _require_windows_cleanup_handle(
    path: Path,
    expected_identity: tuple[int, int],
    *,
    share_write: bool = True,
    read_data: bool = False,
) -> tuple[int, int, bool]:
    if is_reparse_point(path):
        raise RuntimeError(f"reparse-point test session entry is forbidden: {path}")
    handle = _open_windows_path(
        path,
        delete_access=True,
        share_delete=False,
        share_write=share_write,
        read_data=read_data,
    )
    try:
        identity, attributes = _windows_handle_facts(handle)
        if identity != expected_identity:
            raise RuntimeError(f"test session entry identity changed: {path}")
        if attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400):
            raise RuntimeError(f"reparse-point test session entry is forbidden: {path}")
        is_directory = bool(
            attributes & getattr(stat, "FILE_ATTRIBUTE_DIRECTORY", 0x10)
        )
        return handle, attributes, is_directory
    except BaseException:
        _close_windows_handle(handle)
        raise


def _delete_windows_session_entry(path: Path) -> None:
    expected_identity = filesystem_identity(path)
    handle, _attributes, is_directory = _require_windows_cleanup_handle(
        path, expected_identity
    )
    try:
        if is_directory:
            for entry in sorted(
                os.scandir(path), key=lambda item: item.name.casefold()
            ):
                _delete_windows_session_entry(Path(entry.path))
        _prepare_windows_handle_for_deletion(
            handle,
            expected_identity,
            description=f"test session entry: {path}",
        )
        _mark_windows_handle_for_deletion(handle)
    finally:
        _close_windows_handle(handle)


def _delete_owned_windows_session(
    ownership: TestSessionOwnership,
    *,
    require_marker: bool,
) -> None:
    root = ownership.root
    sessions_root = root / "sessions"
    hierarchy: list[int] = []
    marker_handle: int | None = None
    retained = ownership.custody
    marker_deleted = False
    session_deleted = False
    try:
        if retained is not None:
            revalidate_test_session_custody(ownership)
            if (
                retained.root.windows_handle is None
                or retained.sessions.windows_handle is None
                or retained.session_handle is None
                or retained.marker_handle is None
            ):
                raise RuntimeError("test session custody was released before cleanup")
            _close_windows_handle(retained.session_handle)
            retained.session_handle = None
            session_handle, _attributes, is_directory = _require_windows_cleanup_handle(
                ownership.session,
                ownership.session_identity,
            )
            if not is_directory:
                _close_windows_handle(session_handle)
                raise RuntimeError(
                    f"test session directory identity changed: {ownership.session}"
                )
            retained.session_handle = session_handle
            hierarchy.extend(
                (
                    retained.root.windows_handle,
                    retained.sessions.windows_handle,
                    session_handle,
                )
            )
            if require_marker:
                _close_windows_handle(retained.marker_handle)
                retained.marker_handle = None
                marker_handle, _attributes, is_directory = (
                    _require_windows_cleanup_handle(
                        ownership.session / TEST_SESSION_MARKER_NAME,
                        ownership.marker_identity,
                        share_write=False,
                        read_data=True,
                    )
                )
                if is_directory:
                    raise RuntimeError("test session marker became a directory")
        else:
            for path, expected in (
                (root, ownership.root_identity),
                (sessions_root, ownership.sessions_identity),
            ):
                custody = acquire_directory_custody(path)
                if custody.identity != expected:
                    release_directory_custody(custody)
                    raise RuntimeError(
                        f"test session directory identity changed: {path}"
                    )
                assert custody.windows_handle is not None
                hierarchy.append(custody.windows_handle)

            session_handle, _attributes, is_directory = _require_windows_cleanup_handle(
                ownership.session, ownership.session_identity
            )
            if not is_directory:
                _close_windows_handle(session_handle)
                raise RuntimeError(
                    f"test session directory identity changed: {ownership.session}"
                )
            hierarchy.append(session_handle)

        marker = ownership.session / TEST_SESSION_MARKER_NAME
        if require_marker and retained is None:
            marker_handle, _attributes, is_directory = _require_windows_cleanup_handle(
                marker,
                ownership.marker_identity,
                share_write=False,
                read_data=True,
            )
            if (
                is_directory
                or _read_windows_handle(marker_handle) != ownership.marker_bytes
            ):
                raise RuntimeError("test session marker does not match its owner")

        for entry in sorted(
            os.scandir(ownership.session), key=lambda item: item.name.casefold()
        ):
            path = Path(entry.path)
            if require_marker and os.path.normcase(path.name) == os.path.normcase(
                TEST_SESSION_MARKER_NAME
            ):
                continue
            _delete_windows_session_entry(path)

        remaining = tuple(os.scandir(ownership.session))
        expected_remaining = (TEST_SESSION_MARKER_NAME,) if require_marker else ()
        if tuple(entry.name for entry in remaining) != expected_remaining:
            raise RuntimeError("test session inventory changed during cleanup")

        if marker_handle is not None:
            if _read_windows_handle(marker_handle) != ownership.marker_bytes:
                raise RuntimeError("test session marker does not match its owner")
            _prepare_windows_handle_for_deletion(
                marker_handle,
                ownership.marker_identity,
                description="test session marker",
            )
            _mark_windows_handle_for_deletion(marker_handle)
            _close_windows_handle(marker_handle)
            if retained is not None:
                retained.marker_handle = None
            marker_handle = None
            marker_deleted = True

        session_handle = hierarchy[-1]
        _prepare_windows_handle_for_deletion(
            session_handle,
            ownership.session_identity,
            description="test session",
        )
        _mark_windows_handle_for_deletion(session_handle)
        _close_windows_handle(session_handle)
        if retained is not None:
            retained.session_handle = None
        hierarchy.pop()
        session_deleted = True
    except BaseException as cleanup_error:
        if (
            marker_deleted
            and not session_deleted
            and not (ownership.session / TEST_SESSION_MARKER_NAME).exists()
        ):
            try:
                (ownership.session / TEST_SESSION_MARKER_NAME).write_bytes(
                    ownership.marker_bytes
                )
            except OSError as restore_error:
                raise RuntimeError(
                    f"test session cleanup failed; exact unmarked diagnostic "
                    f"residue retained at {ownership.session}: {restore_error}"
                ) from cleanup_error
        raise
    finally:
        if retained is not None:
            if marker_handle is not None:
                _close_windows_handle(marker_handle)
            release_test_session_custody(ownership)
        else:
            if marker_handle is not None:
                _close_windows_handle(marker_handle)
            for handle in reversed(hierarchy):
                _close_windows_handle(handle)


def _delete_owned_session(
    ownership: TestSessionOwnership,
    *,
    require_marker: bool,
) -> None:
    if sys.platform == "win32":
        _delete_owned_windows_session(ownership, require_marker=require_marker)
        return
    if not shutil.rmtree.avoids_symlink_attacks:
        raise RuntimeError("safe test session cleanup is unavailable on this platform")
    if filesystem_identity(ownership.root) != ownership.root_identity:
        raise RuntimeError("test-area root identity changed before cleanup")
    if filesystem_identity(ownership.root / "sessions") != ownership.sessions_identity:
        raise RuntimeError("test sessions root identity changed before cleanup")
    if filesystem_identity(ownership.session) != ownership.session_identity:
        raise RuntimeError("test session identity changed before cleanup")
    if require_marker:
        validate_test_session(
            ownership.root,
            ownership.session,
            ownership.marker_bytes,
            expected_root_identity=ownership.root_identity,
            expected_sessions_identity=ownership.sessions_identity,
            expected_session_identity=ownership.session_identity,
            expected_marker_identity=ownership.marker_identity,
        )
    for current, directories, files in os.walk(
        ownership.session, topdown=True, followlinks=False
    ):
        for name in (*directories, *files):
            if is_reparse_point(Path(current) / name):
                raise RuntimeError("reparse-point test session entry is forbidden")
    shutil.rmtree(ownership.session)


def validate_test_session(
    root: Path,
    session: Path,
    expected_marker: bytes | None = None,
    *,
    expected_root_identity: tuple[int, int] | None = None,
    expected_sessions_identity: tuple[int, int] | None = None,
    expected_session_identity: tuple[int, int] | None = None,
    expected_marker_identity: tuple[int, int] | None = None,
) -> TestSessionOwnership:
    """Validate the exact direct child and marker without inspecting siblings."""

    root = validate_existing_path_components(root, description="test-area root")
    sessions_root = validate_existing_path_components(
        root / "sessions", description="test sessions root"
    )
    root_identity = filesystem_identity(root)
    sessions_identity = filesystem_identity(sessions_root)
    if expected_root_identity is not None and root_identity != expected_root_identity:
        raise RuntimeError("test-area root identity changed")
    if (
        expected_sessions_identity is not None
        and sessions_identity != expected_sessions_identity
    ):
        raise RuntimeError("test sessions root identity changed")
    session = validate_existing_path_components(session, description="test session")
    if session.parent != sessions_root or not session.name:
        raise RuntimeError("test session must be a direct child of the sessions root")
    if not session.is_dir():
        raise RuntimeError("test session must be an existing directory")
    session_identity = filesystem_identity(session)
    if (
        expected_session_identity is not None
        and session_identity != expected_session_identity
    ):
        raise RuntimeError("test session identity changed")
    marker_bytes = expected_marker or test_session_marker_bytes(root, session)
    marker = session / TEST_SESSION_MARKER_NAME
    if is_reparse_point(marker):
        raise RuntimeError("test session marker cannot be a reparse point")
    try:
        mode = marker.stat(follow_symlinks=False).st_mode
        actual_marker = marker.read_bytes()
    except OSError as error:
        raise RuntimeError("test session marker is missing or unreadable") from error
    if not stat.S_ISREG(mode) or actual_marker != marker_bytes:
        raise RuntimeError("test session marker does not match its owner")
    marker_identity = filesystem_identity(marker)
    if (
        expected_marker_identity is not None
        and marker_identity != expected_marker_identity
    ):
        raise RuntimeError("test session marker identity changed")
    return TestSessionOwnership(
        root,
        session,
        marker_bytes,
        root_identity,
        sessions_identity,
        session_identity,
        marker_identity,
    )


def _create_windows_test_session_child(
    root: Path,
    root_custody: DirectoryCustody,
    sessions_custody: DirectoryCustody,
    session_name: str,
) -> TestSessionOwnership:
    assert sessions_custody.windows_handle is not None
    session = root / "sessions" / session_name
    session_handle: int | None = None
    marker_handle: int | None = None
    session_identity: tuple[int, int] | None = None
    marker_identity: tuple[int, int] | None = None
    marker_bytes = b""
    try:
        session_handle = _create_windows_relative_path(
            sessions_custody.windows_handle,
            session_name,
            directory=True,
        )
        session_identity, session_attributes = _windows_handle_facts(session_handle)
        if session_attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400):
            raise RuntimeError("created test session cannot be a reparse point")
        marker_bytes = test_session_marker_bytes(root, session)
        marker_handle = _create_windows_relative_path(
            session_handle,
            TEST_SESSION_MARKER_NAME,
            directory=False,
        )
        marker_identity, marker_attributes = _windows_handle_facts(marker_handle)
        if marker_attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400):
            raise RuntimeError("created test session marker cannot be a reparse point")
        _write_windows_handle(marker_handle, marker_bytes)
        revalidate_directory_custody(root_custody)
        revalidate_directory_custody(sessions_custody)
        if filesystem_identity(session) != session_identity:
            raise RuntimeError("test session identity changed during setup")
        marker = session / TEST_SESSION_MARKER_NAME
        if filesystem_identity(marker) != marker_identity:
            raise RuntimeError("test session marker identity changed during setup")
        if _read_windows_handle(marker_handle) != marker_bytes:
            raise RuntimeError("test session marker changed during setup")
        ownership = TestSessionOwnership(
            root,
            session,
            marker_bytes,
            root_custody.identity,
            sessions_custody.identity,
            session_identity,
            marker_identity,
            TestSessionCustody(
                root_custody,
                sessions_custody,
                session_handle,
                marker_handle,
            ),
        )
        session_handle = None
        marker_handle = None
        return ownership
    except BaseException as setup_error:
        try:
            if session_handle is not None and session_identity is not None:
                _close_windows_handle(session_handle)
                session_handle = None
                session_handle, _attributes, is_directory = (
                    _require_windows_cleanup_handle(
                        session,
                        session_identity,
                    )
                )
                if not is_directory:
                    raise RuntimeError(
                        "provisional test session became a non-directory"
                    )
            if marker_handle is not None and marker_identity is not None:
                _close_windows_handle(marker_handle)
                marker_handle = None
                marker_handle, _attributes, is_directory = (
                    _require_windows_cleanup_handle(
                        session / TEST_SESSION_MARKER_NAME,
                        marker_identity,
                        share_write=False,
                        read_data=True,
                    )
                )
                if is_directory:
                    raise RuntimeError(
                        "provisional test session marker became a directory"
                    )
                _prepare_windows_handle_for_deletion(
                    marker_handle,
                    marker_identity,
                    description="provisional test session marker",
                )
                _mark_windows_handle_for_deletion(marker_handle)
                _close_windows_handle(marker_handle)
                marker_handle = None
            if session_handle is not None and session_identity is not None:
                _prepare_windows_handle_for_deletion(
                    session_handle,
                    session_identity,
                    description="provisional test session",
                )
                _mark_windows_handle_for_deletion(session_handle)
                _close_windows_handle(session_handle)
                session_handle = None
        except BaseException as cleanup_error:
            raise RuntimeError(
                f"test session setup failed; exact handle-owned diagnostic residue "
                f"retained for {session}: {cleanup_error}"
            ) from setup_error
        raise
    finally:
        if marker_handle is not None:
            _close_windows_handle(marker_handle)
        if session_handle is not None:
            _close_windows_handle(session_handle)


def create_test_session(root: Path, *, create_root: bool) -> TestSessionOwnership:
    """Create one exclusive random direct child and write its marker first."""

    parent_custody: DirectoryCustody | None = None
    root_custody: DirectoryCustody | None = None
    sessions_custody: DirectoryCustody | None = None
    try:
        if create_root:
            parent_custody = acquire_directory_custody(root.parent)
            revalidate_directory_custody(parent_custody)
            if not root.exists():
                if sys.platform == "win32":
                    assert parent_custody.windows_handle is not None
                    try:
                        handle = _create_windows_relative_path(
                            parent_custody.windows_handle,
                            root.name,
                            directory=True,
                        )
                    except FileExistsError:
                        pass
                    else:
                        root_custody = directory_custody_from_windows_handle(
                            root, handle
                        )
                else:
                    root.mkdir()
        root = validate_test_area_root(root, may_create=False)
        if root_custody is None:
            root_custody = acquire_directory_custody(root)
        if parent_custody is not None:
            revalidate_directory_custody(parent_custody)
        revalidate_directory_custody(root_custody)

        sessions_root = root / "sessions"
        if sessions_root.exists():
            if not sessions_root.is_dir() or is_reparse_point(sessions_root):
                raise RuntimeError("test sessions root must be a non-reparse directory")
        else:
            if sys.platform == "win32":
                assert root_custody.windows_handle is not None
                try:
                    handle = _create_windows_relative_path(
                        root_custody.windows_handle,
                        "sessions",
                        directory=True,
                    )
                except FileExistsError:
                    pass
                else:
                    sessions_custody = directory_custody_from_windows_handle(
                        sessions_root, handle
                    )
            else:
                sessions_root.mkdir()
        sessions_root = validate_existing_path_components(
            sessions_root, description="test sessions root"
        )
        if sessions_custody is None:
            sessions_custody = acquire_directory_custody(sessions_root)
        revalidate_directory_custody(root_custody)
        revalidate_directory_custody(sessions_custody)

        for _attempt in range(16):
            revalidate_directory_custody(root_custody)
            revalidate_directory_custody(sessions_custody)
            session_token = (
                base64.b32encode(secrets.token_bytes(16))
                .decode("ascii")
                .rstrip("=")
                .lower()
            )
            session_name = f"s-{session_token}"
            if sys.platform == "win32":
                try:
                    ownership = _create_windows_test_session_child(
                        root,
                        root_custody,
                        sessions_custody,
                        session_name,
                    )
                except FileExistsError:
                    continue
                root_custody = None
                sessions_custody = None
                return ownership
            session = (sessions_root / session_name).resolve(strict=False)
            try:
                session.mkdir()
            except FileExistsError:
                continue
            session_identity: tuple[int, int] | None = None
            marker_bytes = b""
            try:
                session_identity = filesystem_identity(session)
                marker_bytes = test_session_marker_bytes(root, session)
                with (session / TEST_SESSION_MARKER_NAME).open("xb") as marker:
                    marker.write(marker_bytes)
                marker_identity = filesystem_identity(
                    session / TEST_SESSION_MARKER_NAME
                )
                ownership = validate_test_session(
                    root,
                    session,
                    marker_bytes,
                    expected_root_identity=root_custody.identity,
                    expected_sessions_identity=sessions_custody.identity,
                    expected_session_identity=session_identity,
                    expected_marker_identity=marker_identity,
                )
                revalidate_directory_custody(root_custody)
                revalidate_directory_custody(sessions_custody)
                return ownership
            except BaseException as setup_error:
                if session_identity is None:
                    try:
                        session_identity = filesystem_identity(session)
                    except BaseException as identity_error:
                        raise RuntimeError(
                            f"test session setup failed; exact unmarked diagnostic "
                            f"residue retained at {session}: {identity_error}"
                        ) from setup_error
                provisional = TestSessionOwnership(
                    root,
                    session,
                    marker_bytes,
                    root_custody.identity,
                    sessions_custody.identity,
                    session_identity,
                    (0, 0),
                )
                try:
                    _delete_owned_session(provisional, require_marker=False)
                except BaseException as cleanup_error:
                    raise RuntimeError(
                        f"test session setup failed; exact diagnostic residue "
                        f"retained at {session}: {cleanup_error}"
                    ) from setup_error
                raise
        raise RuntimeError("could not allocate an exclusive test session")
    finally:
        release_directory_custody(sessions_custody)
        release_directory_custody(root_custody)
        release_directory_custody(parent_custody)


def cleanup_test_session(ownership: TestSessionOwnership) -> None:
    """Delete only one exact marker-bound session after immediate revalidation."""

    if ownership.custody is not None:
        revalidate_test_session_custody(ownership)
    else:
        validate_test_session(
            ownership.root,
            ownership.session,
            ownership.marker_bytes,
            expected_root_identity=ownership.root_identity,
            expected_sessions_identity=ownership.sessions_identity,
            expected_session_identity=ownership.session_identity,
            expected_marker_identity=ownership.marker_identity,
        )
    _delete_owned_session(ownership, require_marker=True)


@contextmanager
def verification_test_session(*, internal_lane: bool):
    """Own one canonical verifier session and restore all process state on exit."""

    global DOTNET_COVERAGE_WORK_ROOT
    global CI_DOTNET_EVIDENCE_ROOT

    root, create_root = resolve_test_area_root()
    owns_session = not internal_lane
    if owns_session:
        if TEST_SESSION_ENVIRONMENT_VARIABLE in os.environ:
            raise RuntimeError(
                f"public verification forbids externally supplied "
                f"{TEST_SESSION_ENVIRONMENT_VARIABLE}"
            )
        ownership = create_test_session(root, create_root=create_root)
    else:
        inherited = os.environ.get(TEST_SESSION_ENVIRONMENT_VARIABLE)
        if inherited is None or not inherited.strip():
            raise RuntimeError(
                f"internal verification requires {TEST_SESSION_ENVIRONMENT_VARIABLE}"
            )
        ownership = validate_test_session(root, Path(inherited))
    session = ownership.session

    managed_names = (
        TEST_AREA_ENVIRONMENT_VARIABLE,
        TEST_SESSION_ENVIRONMENT_VARIABLE,
        *TEST_SESSION_SCRATCH_DIRECTORIES,
    )
    missing = object()
    previous_environment: dict[str, str | object] = {
        name: os.environ.get(name, missing) for name in managed_names
    }
    previous_tempdir = tempfile.tempdir
    previous_dotnet_root = DOTNET_COVERAGE_WORK_ROOT
    previous_ci_root = CI_DOTNET_EVIDENCE_ROOT
    primary_error: BaseException | None = None
    try:
        revalidate_test_session_custody(ownership)
        os.environ[TEST_AREA_ENVIRONMENT_VARIABLE] = str(root)
        os.environ[TEST_SESSION_ENVIRONMENT_VARIABLE] = str(session)
        for name, relative in TEST_SESSION_SCRATCH_DIRECTORIES.items():
            scratch = session / relative
            scratch.mkdir(parents=True, exist_ok=True)
            os.environ[name] = str(scratch)
        tempfile.tempdir = os.environ["TEMP"]
        work_root = session / "work"
        DOTNET_COVERAGE_WORK_ROOT = work_root / "dotnet-coverage"
        CI_DOTNET_EVIDENCE_ROOT = work_root / "ci-dotnet"
        revalidate_test_session_custody(ownership)
        yield session
    except BaseException as error:
        primary_error = error
        raise
    finally:
        cleanup_error: BaseException | None = None
        release_error: BaseException | None = None
        try:
            if owns_session:
                with defer_termination_signals_during_process_handoff():
                    cleanup_test_session(ownership)
        except BaseException as error:
            cleanup_error = error
        try:
            if owns_session:
                try:
                    release_test_session_custody(ownership)
                except BaseException as error:
                    release_error = error
        finally:
            tempfile.tempdir = previous_tempdir
            DOTNET_COVERAGE_WORK_ROOT = previous_dotnet_root
            CI_DOTNET_EVIDENCE_ROOT = previous_ci_root
            for name, value in previous_environment.items():
                if value is missing:
                    os.environ.pop(name, None)
                else:
                    assert isinstance(value, str)
                    os.environ[name] = value
        if primary_error is not None:
            if cleanup_error is not None:
                primary_error.add_note(
                    f"test session cleanup also failed; exact residue may remain at "
                    f"{session}: {type(cleanup_error).__name__}: {cleanup_error}"
                )
            if release_error is not None:
                primary_error.add_note(
                    f"test session custody release also failed; exact residue may "
                    f"remain at {session}: {type(release_error).__name__}: "
                    f"{release_error}"
                )
        elif cleanup_error is not None:
            if release_error is not None:
                cleanup_error.add_note(
                    f"test session custody release also failed; exact residue may "
                    f"remain at {session}: {type(release_error).__name__}: "
                    f"{release_error}"
                )
            raise cleanup_error
        elif release_error is not None:
            raise release_error


def validated_path_within_root(
    path: Path,
    root: Path,
    *,
    description: str,
) -> Path:
    """Resolve one lexical path without following a link or escaping its owner."""

    lexical_root = root.absolute()
    lexical_path = path.absolute()
    try:
        relative = lexical_path.relative_to(lexical_root)
    except ValueError as error:
        raise RuntimeError(f"{description} path escapes its root: {path}") from error

    candidate = lexical_root
    if is_reparse_point(candidate):
        raise RuntimeError(
            f"symbolic link or junction/reparse-point {description} is forbidden: "
            f"{candidate}"
        )
    for part in relative.parts:
        candidate /= part
        if is_reparse_point(candidate):
            raise RuntimeError(
                f"symbolic link or junction/reparse-point {description} is forbidden: "
                f"{candidate}"
            )

    resolved_root = lexical_root.resolve()
    resolved_path = lexical_path.resolve(strict=False)
    try:
        resolved_path.relative_to(resolved_root)
    except ValueError as error:
        raise RuntimeError(f"{description} path escapes its root: {path}") from error
    return lexical_path


def validated_disposable_directory(directory: Path, root: Path) -> Path:
    """Resolve a disposable directory without following repository-internal links."""

    target = validated_path_within_root(
        directory,
        root,
        description="disposable directory",
    )
    if target == root.absolute():
        raise RuntimeError("refusing to delete the repository root")
    return target


def reset_coverage_directory(language: str) -> Path:
    """Create one known disposable report directory below ignored artifacts."""

    if language not in {"dotnet", "python"}:
        raise ValueError(f"unsupported coverage directory: {language}")
    directory = validated_disposable_directory(COVERAGE_ROOT / language, ROOT)
    if directory.exists():
        shutil.rmtree(directory)
    directory.mkdir(parents=True, exist_ok=True)
    return directory


def require_python_modules(names: tuple[str, ...]) -> None:
    missing = [name for name in names if importlib.util.find_spec(name) is None]
    if missing:
        extras = str(WORKER_ROOT) + "[dev]"
        raise RuntimeError(
            "missing Python verification modules: "
            + ", ".join(missing)
            + f". Install them with: {sys.executable} -m pip install -e '{extras}'"
        )


def require_python_distribution_versions(expected: dict[str, str]) -> None:
    """Require the active environment to match the recorded collector versions."""

    mismatches: list[str] = []
    for distribution, expected_version in sorted(expected.items()):
        try:
            actual_version = importlib_metadata.version(distribution)
        except importlib_metadata.PackageNotFoundError:
            actual_version = "<missing>"
        if actual_version != expected_version:
            mismatches.append(
                f"{distribution} expected {expected_version}, found {actual_version}"
            )
    if mismatches:
        raise RuntimeError(
            "Python coverage collector version mismatch: " + "; ".join(mismatches)
        )


def verify_python(log_path: Path | None = None) -> None:
    coverage_overrides = [
        name
        for name in PYTHON_COVERAGE_OVERRIDE_ENVIRONMENT_VARIABLES
        if os.environ.get(name, "").strip()
    ]
    if coverage_overrides:
        raise RuntimeError(
            "Python coverage environment overrides are forbidden: "
            + ", ".join(coverage_overrides)
        )
    require_python_modules(
        ("ruff", "pyright", "pylint", "pytest", "pytest_cov", "coverage")
    )
    python_collection = load_baseline()["collection"]["python"]
    require_python_distribution_versions(
        {
            "coverage": python_collection["coveragePyVersion"],
            "pytest-cov": python_collection["pytestCovVersion"],
        }
    )
    coverage_report = reset_coverage_directory("python") / "coverage.json"
    active_session_value = os.environ.get(TEST_SESSION_ENVIRONMENT_VARIABLE)
    pytest_scratch = (
        Path(active_session_value) / "pytest"
        if active_session_value is not None and active_session_value.strip()
        else None
    )
    commands = (
        [
            sys.executable,
            "-m",
            "ruff",
            "format",
            "--check",
            "src",
            "tests",
            "packaging",
        ],
        [sys.executable, "-m", "ruff", "check", "src", "tests", "packaging"],
        [sys.executable, "-m", "pyright", "src", "tests", "packaging"],
        [sys.executable, "-m", "pylint", "src/nfc_crc_worker"],
        [
            sys.executable,
            "-m",
            "pytest",
            "--cov=nfc_crc_worker",
            "--cov-branch",
            "--cov-report=term-missing",
            f"--cov-report=json:{coverage_report}",
            *(
                (
                    f"--basetemp={pytest_scratch / 'base'}",
                    "-o",
                    f"cache_dir={pytest_scratch / 'cache'}",
                )
                if pytest_scratch is not None
                else ()
            ),
        ],
    )
    for command in commands:
        run(command, cwd=WORKER_ROOT, log_path=log_path)
    verify_coverage("python", coverage_report)


def resolve_dotnet() -> str:
    executable_name = "dotnet.exe" if sys.platform == "win32" else "dotnet"
    repository_dotnet = ROOT / ".dotnet" / executable_name
    if repository_dotnet.is_file():
        return str(repository_dotnet)
    system_dotnet = shutil.which("dotnet")
    if system_dotnet is not None:
        return system_dotnet
    install_command = (
        ".\\scripts\\install-dotnet.ps1 -Scope Repository"
        if sys.platform == "win32"
        else "./scripts/install-dotnet.sh --scope repository"
    )
    raise RuntimeError(f".NET SDK is not installed. Run: {install_command}")


def stop_idle_build_workers(
    log_path: Path | None = None, *, timeout_seconds: float | None = None
) -> None:
    """Stops only the repo-bound Avalonia collector left after a batch build on Windows."""
    if sys.platform != "win32":
        return

    powershell = shutil.which("powershell") or shutil.which("pwsh")
    if powershell is None:
        write_cleanup_warning(
            "warning: PowerShell was unavailable; idle Avalonia build worker cleanup was skipped.",
            log_path,
        )
        return

    command = [
        powershell,
        "-NoProfile",
        "-File",
        str(IDLE_BUILD_WORKER_STOPPER),
        "-RepositoryRoot",
        str(ROOT),
    ]
    if log_path is None:
        try:
            run(command, timeout_seconds=timeout_seconds)
            return
        except subprocess.CalledProcessError as error:
            result = error
        except subprocess.TimeoutExpired:
            write_cleanup_warning(
                "warning: idle Avalonia build worker cleanup exceeded its timeout.",
                log_path,
            )
            return
    else:
        try:
            _run_to_logs(
                command,
                log_paths=(log_path,),
                cwd=ROOT,
                environment=None,
                echo=False,
                timeout_seconds=timeout_seconds,
            )
            return
        except subprocess.CalledProcessError as error:
            result = error
        except subprocess.TimeoutExpired:
            write_cleanup_warning(
                "warning: idle Avalonia build worker cleanup exceeded its timeout.",
                log_path,
            )
            return
    if result.returncode != 0:
        write_cleanup_warning(
            f"warning: idle Avalonia build worker cleanup returned exit code {result.returncode}.",
            log_path,
        )


def write_cleanup_warning(message: str, log_path: Path | None) -> None:
    """Keep optional cleanup diagnostics inside the active lane log when one exists."""

    if log_path is None:
        print(message)
        return
    log_path.parent.mkdir(parents=True, exist_ok=True)
    with log_path.open("a", encoding="utf-8", newline="\n") as log:
        print(message, file=log)


def dotnet_batch_environment() -> dict[str, str]:
    """Return the shared non-interactive MSBuild environment for every .NET owner."""

    environment = os.environ.copy()
    environment[TEST_REPOSITORY_ROOT_ENVIRONMENT_VARIABLE] = str(ROOT)
    environment["MSBUILDDISABLENODEREUSE"] = "1"
    environment["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1"
    return environment


def dotnet_build_commands(
    dotnet: str, *, include_prebuild_checks: bool = True
) -> tuple[list[str], ...]:
    """Return the canonical post-restore Release plan, with optional source prechecks."""

    release_build = [dotnet, "build", str(SOLUTION), "-c", "Release", "--no-restore"]
    if not include_prebuild_checks:
        return (release_build,)
    return (
        [
            sys.executable,
            "scripts/validate_repository.py",
            "--evaluated-source-ownership-only",
        ],
        [
            dotnet,
            "format",
            str(SOLUTION),
            "whitespace",
            "--verify-no-changes",
            "--no-restore",
        ],
        release_build,
    )


def solution_package_lock_paths(
    repository_root: Path = ROOT,
    solution: Path = SOLUTION,
) -> tuple[Path, ...]:
    """Return the exact ordinary package locks owned by the solution projects."""

    solution = require_regular_file(
        solution,
        repository_root,
        description="canonical solution package-lock inventory",
    )
    paths: list[Path] = []
    projects: set[Path] = set()
    for node in ET.parse(solution).findall(".//Project"):
        project_path = node.attrib.get("Path")
        if not isinstance(project_path, str) or not project_path:
            raise RuntimeError("solution project is missing its Path attribute")
        relative_text = project_path.replace("\\", "/")
        relative = PurePosixPath(relative_text)
        if (
            relative.is_absolute()
            or relative.as_posix() != relative_text
            or not relative.parts
            or relative.parts[0] not in {"src", "tests"}
            or any(part in {"", ".", ".."} for part in relative_text.split("/"))
            or relative.suffix.casefold() != ".csproj"
        ):
            raise RuntimeError(f"invalid solution project path: {relative}")
        project = require_regular_file(
            repository_root.joinpath(*relative.parts),
            repository_root,
            description="solution project",
        )
        if project in projects:
            raise RuntimeError(f"duplicate solution project: {project}")
        projects.add(project)
        lock = require_regular_file(
            project.parent / "packages.lock.json",
            repository_root,
            description="solution package lock",
        )
        if lock in paths:
            raise RuntimeError(f"duplicate solution package lock: {lock}")
        paths.append(lock)
    if len(projects) != 25 or len(paths) != 25:
        raise RuntimeError(
            "solution package-lock inventory must contain exactly 25 projects and locks"
        )
    return tuple(paths)


def load_package_lock(raw: bytes) -> object:
    """Decode one package lock while rejecting duplicate JSON keys."""

    def unique_object(pairs: list[tuple[str, object]]) -> dict[str, object]:
        document: dict[str, object] = {}
        for name, value in pairs:
            if name in document:
                raise ValueError(f"duplicate package-lock key: {name}")
            document[name] = value
        return document

    return json.loads(raw.decode("utf-8"), object_pairs_hook=unique_object)


def lock_without_windows_rid_projections(document: object) -> object:
    """Remove only Windows RID targets that normal solution restore omits."""

    if not isinstance(document, dict):
        raise ValueError("package lock root must be an object")
    dependencies = document.get("dependencies")
    if not isinstance(dependencies, dict):
        raise ValueError("package lock dependencies must be an object")
    normalized = dict(document)
    normalized_dependencies = dict(dependencies)
    removed = 0
    for target in tuple(normalized_dependencies):
        if isinstance(target, str) and target.endswith("/win-x64"):
            normalized_dependencies.pop(target)
            removed += 1
    if removed == 0:
        raise ValueError("changed package lock removed no case-exact /win-x64 target")
    normalized["dependencies"] = normalized_dependencies
    return normalized


def run_solution_restore_preserving_lock_projections(
    command: list[str],
    *,
    environment: dict[str, str],
    log_path: Path | None,
    repository_root: Path = ROOT,
    solution: Path = SOLUTION,
) -> None:
    """Run solution restore without leaving its known RID projection mutation."""

    lock_paths = solution_package_lock_paths(repository_root, solution)
    snapshots = {path: path.read_bytes() for path in lock_paths}
    for path, snapshot in snapshots.items():
        try:
            load_package_lock(snapshot)
        except (UnicodeDecodeError, json.JSONDecodeError, ValueError) as error:
            raise RuntimeError(
                f"invalid committed solution package lock "
                f"{path.relative_to(repository_root).as_posix()}: {error}"
            ) from error
    failure: BaseException | None = None
    try:
        run(command, environment=environment, log_path=log_path)
    except BaseException as error:
        failure = error

    inspection_errors: list[str] = []
    unexpected: list[str] = []
    restorable: list[Path] = []
    try:
        observed_paths = solution_package_lock_paths(repository_root, solution)
    except (OSError, RuntimeError, ET.ParseError) as error:
        observed_paths = ()
        inspection_errors.append(f"package-lock inventory: {error}")
    if observed_paths and observed_paths != lock_paths:
        unexpected.append("the canonical solution package-lock inventory changed")
    snapshot_path_set = set(lock_paths)
    observed_path_set = set(observed_paths)
    for path in sorted(snapshot_path_set - observed_path_set):
        unexpected.append(
            f"snapshot-owned package lock disappeared: "
            f"{path.relative_to(repository_root).as_posix()}"
        )
    for path in sorted(observed_path_set - snapshot_path_set):
        unexpected.append(
            f"unowned package lock appeared: "
            f"{path.relative_to(repository_root).as_posix()}"
        )
    for path in observed_paths:
        if path not in snapshots:
            continue
        try:
            observed = path.read_bytes()
        except OSError as error:
            inspection_errors.append(
                f"{path.relative_to(repository_root).as_posix()}: {error}"
            )
            continue
        expected = snapshots[path]
        if observed == expected:
            continue
        try:
            before = load_package_lock(expected)
            after = load_package_lock(observed)
            if after != lock_without_windows_rid_projections(before):
                raise ValueError(
                    "change is not the normal Windows RID projection removal"
                )
        except (UnicodeDecodeError, json.JSONDecodeError, ValueError) as error:
            unexpected.append(
                f"{path.relative_to(repository_root).as_posix()}: {error}"
            )
            continue
        restorable.append(path)

    if inspection_errors or unexpected:
        error = RuntimeError(
            "solution restore package-lock inspection failed; unexpected bytes were "
            "retained for inspection: "
            + "; ".join(inspection_errors + unexpected)
        )
        if failure is not None:
            error.add_note(
                f"solution restore also failed: {type(failure).__name__}: {failure}"
            )
        raise error

    restore_errors: list[str] = []
    restored_paths: list[str] = []
    for path in restorable:
        temporary: Path | None = None
        try:
            path = require_regular_file(
                path,
                repository_root,
                description="solution package lock before restoration",
            )
            with tempfile.NamedTemporaryFile(
                mode="wb",
                prefix=f".{path.name}.nfc-restore-",
                suffix=".tmp",
                dir=path.parent,
                delete=False,
            ) as stream:
                temporary = Path(stream.name)
                stream.write(snapshots[path])
                stream.flush()
                os.fsync(stream.fileno())
            require_regular_file(
                temporary,
                repository_root,
                description="solution package-lock restoration staging",
            )
            path = require_regular_file(
                path,
                repository_root,
                description="solution package lock before atomic restoration",
            )
            os.replace(temporary, path)
            temporary = None
            path = require_regular_file(
                path,
                repository_root,
                description="atomically restored solution package lock",
            )
            if path.read_bytes() != snapshots[path]:
                raise RuntimeError(
                    f"atomically restored solution package lock differs: {path}"
                )
            restored_paths.append(path.relative_to(repository_root).as_posix())
        except OSError as error:
            restore_errors.append(
                f"{path.relative_to(repository_root).as_posix()}: {error}"
            )
        except RuntimeError as error:
            restore_errors.append(str(error))
        if temporary is not None:
            restore_errors.append(f"restoration residue retained at {temporary}")
    if restore_errors:
        restored_evidence = (
            "; successfully restored before failure: " + ", ".join(restored_paths)
            if restored_paths
            else ""
        )
        error = RuntimeError(
            "solution restore lock-projection cleanup failed: "
            + "; ".join(restore_errors)
            + restored_evidence
        )
        if failure is not None:
            error.add_note(
                f"solution restore also failed: {type(failure).__name__}: {failure}"
            )
        raise error
    if failure is not None:
        raise failure


def flatten_ci_dotnet_projects() -> tuple[CiDotnetProject, ...]:
    """Return the one closed local/CI project-and-counter inventory."""

    projects = tuple(
        project
        for shard_projects in CI_DOTNET_SHARDS.values()
        for project in shard_projects
    )
    paths = tuple(project.relative_path for project in projects)
    names = tuple(project.name for project in projects)
    if (
        len(projects) != 8
        or len(paths) != len(set(paths))
        or len(names) != len(set(names))
    ):
        raise RuntimeError("invalid closed .NET test project inventory")
    return projects


def resolve_coverlet_adapter_path(
    repository_root: Path = ROOT,
    baseline: dict[str, object] | None = None,
) -> Path:
    """Resolve the exact repository-pinned Coverlet adapter without fallback."""

    document = load_baseline() if baseline is None else baseline
    try:
        collection = document["collection"]
        dotnet = collection["dotnet"]  # type: ignore[index]
        collector = dotnet["collector"]  # type: ignore[index]
        version = dotnet["version"]  # type: ignore[index]
        report_format = dotnet["format"]  # type: ignore[index]
    except (KeyError, TypeError) as error:
        raise RuntimeError(
            "coverage baseline is missing .NET collector authority"
        ) from error
    if collector != "coverlet.collector":
        raise RuntimeError(f"unsupported .NET coverage collector: {collector}")
    if not isinstance(version, str) or NUGET_VERSION_PATTERN.fullmatch(version) is None:
        raise RuntimeError(f"invalid .NET coverage collector version: {version}")
    if report_format != "json,cobertura":
        raise RuntimeError(f"unsupported .NET coverage format: {report_format}")

    adapter = (
        repository_root
        / ".packages"
        / "coverlet.collector"
        / version
        / "build"
        / "netstandard2.0"
    )
    hashes = regular_tree_hashes(
        adapter,
        boundary=repository_root,
        description="Coverlet adapter",
    )
    for required in ("coverlet.collector.dll", "coverlet.collector.deps.json"):
        if required not in hashes:
            raise RuntimeError(f"Coverlet adapter is missing {required}")
    return adapter


def local_dotnet_vstest_command(
    dotnet: str,
    test_assembly: Path,
    adapter_path: Path | None,
    results_directory: Path,
) -> list[str]:
    """Build one unfiltered exact-assembly command, optionally collecting coverage."""

    command = [dotnet, "vstest", str(test_assembly)]
    settings: list[str] = []
    if adapter_path is not None:
        command.extend([
            f"--TestAdapterPath:{adapter_path}",
            "--Collect:XPlat Code Coverage",
        ])
        settings.append(
            "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=json,cobertura"
        )
    command.extend([
        f"--ResultsDirectory:{results_directory}",
        "--Logger:trx;LogFileName=test-results.trx",
    ])
    if test_assembly.stem == INFRASTRUCTURE_TEST_PROJECT:
        settings.extend(INFRASTRUCTURE_VSTEST_SETTINGS)
    if settings:
        command.extend(["--", *settings])
    return command


def dotnet_vstest_discovery_command(dotnet: str, test_assembly: Path) -> list[str]:
    """Return the compiled-test discovery command used by local and CI gates."""

    command = [dotnet, "vstest", str(test_assembly), "--ListTests"]
    if test_assembly.stem == INFRASTRUCTURE_TEST_PROJECT:
        command.append("--")
        command.extend(INFRASTRUCTURE_VSTEST_SETTINGS)
    return command


def canonical_vstest_identity(display_name: str) -> str:
    """Return the stable fully-qualified method identity, preserving case multiplicity."""

    identity = display_name.split("(", 1)[0].strip()
    if "." not in identity or any(character.isspace() for character in identity):
        raise RuntimeError(f"invalid VSTest identity: {display_name!r}")
    return identity


def parse_vstest_discovery(path: Path) -> Counter[str]:
    """Read VSTest's discovered method-identity multiset."""

    try:
        lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    except OSError as error:
        raise RuntimeError(
            f"VSTest discovery evidence could not be read: {path}"
        ) from error
    discovered = Counter(
        canonical_vstest_identity(line[4:])
        for line in lines
        if re.fullmatch(r" {4}\S.*", line)
    )
    if not discovered:
        raise RuntimeError(f"VSTest discovery produced no active inventory: {path}")
    return discovered


def current_dotnet_producer_platform() -> str:
    """Return the closed producer identity used by local verification."""

    return DOTNET_PRODUCER_WINDOWS if os.name == "nt" else DOTNET_PRODUCER_NON_WINDOWS


WINDOWS_PROCESSOR_BOOTSTRAP_SKIPS = (
    'NvtFwCombiner.Bootstrap.Tests.AbMergeGoldenRegressionTests.Nt51950CandidateMatchesOwnerApprovedAbGoldenWithCombinerAsync(caseId: "nt51950-ab-boe-d82t80")',
    'NvtFwCombiner.Bootstrap.Tests.AbMergeGoldenRegressionTests.Nt51950CandidateMatchesOwnerApprovedAbGoldenWithCombinerAsync(caseId: "nt51950-ab-hiway-d82t80")',
    "NvtFwCombiner.Bootstrap.Tests.AbMergeGoldenRegressionTests.Nt51951CandidatePlanWithCombinerMatchesPythonReferenceAsync",
    "NvtFwCombiner.Bootstrap.Tests.AbMergeGoldenRegressionTests.Nt51950PublicHostBuildAcceptsOneCanonicalTpFileForBothLogicalSlotsAsync",
    "NvtFwCombiner.Bootstrap.Tests.AbMergeGoldenRegressionTests.Nt51951PublicHostBuildAcceptsOneTpFileForBothLogicalSlotsAsync",
)
UNIX_SPECIAL_FILE_INFRASTRUCTURE_SKIPS = (
    "NvtFwCombiner.Infrastructure.Tests.Bundles.ProfileBundleFileSnapshotTests.ReadRejectsUnixFifoBeforeOpening",
    "NvtFwCombiner.Infrastructure.Tests.Bundles.ProfileBundleInventoryVerifierTests.VerifyClosedInventoryRejectsUnixDomainSocket",
)


def approved_platform_skip_identities(
    project: CiDotnetProject,
    producer_platform: str,
) -> Counter[str]:
    """Return the exact owner-approved omissions for one producer and project."""

    if producer_platform == DOTNET_PRODUCER_WINDOWS:
        identities = (
            UNIX_SPECIAL_FILE_INFRASTRUCTURE_SKIPS
            if project.name == "NvtFwCombiner.Infrastructure.Tests"
            else ()
        )
    elif producer_platform == DOTNET_PRODUCER_NON_WINDOWS:
        identities = (
            WINDOWS_PROCESSOR_BOOTSTRAP_SKIPS
            if project.name == "NvtFwCombiner.Bootstrap.Tests"
            else ()
        )
    else:
        raise RuntimeError(f"unsupported .NET producer platform: {producer_platform}")
    return Counter(canonical_vstest_identity(identity) for identity in identities)


def parse_trx_test_outcomes(
    path: Path, *, preserve_case_identity: bool = False,
) -> dict[str, Counter[str]]:
    """Read terminal outcomes, optionally preserving resolved theory display identities."""

    try:
        document = ET.parse(path)
    except ET.ParseError as error:
        raise RuntimeError(f"TRX result is invalid XML: {path}") from error
    results = document.findall(".//{*}UnitTestResult")
    if not results:
        raise RuntimeError(f"TRX result has no test identities: {path}")
    definitions: dict[str, list[str]] = {}
    for definition in document.findall(".//{*}UnitTest"):
        test_id = definition.attrib.get("id", "").strip()
        identity = definition.attrib.get("name", "").strip()
        if test_id and identity:
            definitions.setdefault(test_id, []).append(identity)
    outcomes = {name: Counter() for name in ("Passed", "Failed", "NotExecuted")}
    for result in results:
        identity = result.attrib.get("testName", "").strip()
        outcome = result.attrib.get("outcome", "").strip()
        if not identity or outcome not in outcomes:
            raise RuntimeError(f"TRX result has an unsupported test outcome: {path}")
        if re.fullmatch(r"<unknown test ID [0-9a-f]{64}>", identity):
            test_id = result.attrib.get("testId", "").strip()
            candidates = definitions.get(test_id, [])
            if len(candidates) != 1:
                raise RuntimeError(
                    f"TRX placeholder identity has no unique test definition: {path}"
                )
            identity = candidates[0]
        method = canonical_vstest_identity(identity)
        outcomes[outcome][identity if preserve_case_identity else method] += 1
    return outcomes


def require_discovered_test_results(
    project: CiDotnetProject,
    discovery_report: Path,
    trx_report: Path,
    counters: dict[str, int],
    producer_platform: str,
) -> None:
    """Reconcile exact compiled discovery and owner-admitted TRX outcomes."""

    discovered = parse_vstest_discovery(discovery_report)
    outcomes = parse_trx_test_outcomes(trx_report)
    observed = outcomes["Passed"] + outcomes["Failed"] + outcomes["NotExecuted"]
    if observed != discovered:
        raise RuntimeError(
            f"{project.name} discovered/executed test identities changed"
        )
    if outcomes["Failed"]:
        raise RuntimeError(f"{project.name} contains failed test identities")
    approved_skips = approved_platform_skip_identities(project, producer_platform)
    if outcomes["NotExecuted"] != approved_skips:
        raise RuntimeError(
            f"{project.name} contains unapproved skipped test identities for "
            f"{producer_platform}"
        )
    expected_counters = {
        "total": sum(discovered.values()),
        "passed": sum(outcomes["Passed"].values()),
        "failed": 0,
        "skipped": sum(approved_skips.values()),
    }
    if counters != expected_counters:
        raise RuntimeError(
            f"{project.name} discovered/executed test inventory changed: "
            f"expected {expected_counters}, observed {counters}"
        )


def find_project_release_output(
    project: CiDotnetProject,
    repository_root: Path = ROOT,
) -> tuple[Path, Path]:
    """Find the one built Release output and its project-relative layout suffix."""

    project_file = validated_path_within_root(
        repository_root / project.relative_path,
        repository_root,
        description=".NET test project",
    )
    project_directory = project_file.parent
    release_root = project_directory / "bin" / "Release"
    candidates = tuple(sorted(release_root.glob(f"*/{project.name}.dll")))
    if len(candidates) != 1:
        raise RuntimeError(
            f"{project.name} must have exactly one canonical Release test assembly"
        )
    output = validated_path_within_root(
        candidates[0].parent,
        project_directory,
        description=f"{project.name} Release output",
    )
    suffix = output.relative_to(project_directory)
    if len(suffix.parts) != 3 or suffix.parts[:2] != ("bin", "Release"):
        raise RuntimeError(f"unexpected {project.name} Release output layout: {suffix}")
    return output, suffix


def canonical_production_release_outputs(
    release_suffix: Path,
    repository_root: Path = ROOT,
    solution: Path = SOLUTION,
) -> dict[str, Path]:
    """Map solution-owned production assembly names to canonical Release outputs."""

    outputs: dict[str, Path] = {}
    for node in ET.parse(solution).findall(".//Project"):
        relative_text = node.attrib["Path"].replace("\\", "/")
        relative = PurePosixPath(relative_text)
        if not relative.parts or relative.parts[0] != "src":
            continue
        name = relative.stem
        output = repository_root.joinpath(*relative.parent.parts) / release_suffix
        dll = validated_path_within_root(
            output / f"{name}.dll",
            repository_root,
            description="canonical production DLL",
        )
        try:
            mode = dll.stat(follow_symlinks=False).st_mode
        except OSError as error:
            raise RuntimeError(f"missing canonical production DLL: {dll}") from error
        if is_reparse_point(dll) or not stat.S_ISREG(mode):
            raise RuntimeError(f"invalid canonical production DLL: {dll}")
        if name in outputs:
            raise RuntimeError(f"duplicate canonical production assembly: {name}")
        outputs[name] = output
    if not outputs:
        raise RuntimeError("solution contains no canonical production outputs")
    return outputs


def require_production_release_matches(
    test_output: Path,
    canonical_outputs: dict[str, Path],
) -> tuple[tuple[Path, str], ...]:
    """Require every referenced production DLL and optional PDB to be canonical."""

    canonical_hashes: dict[Path, str] = {}
    for name, canonical_output in sorted(canonical_outputs.items()):
        test_dll = test_output / f"{name}.dll"
        test_pdb = test_output / f"{name}.pdb"
        canonical_dll = canonical_output / f"{name}.dll"
        canonical_pdb = canonical_output / f"{name}.pdb"
        test_dll_file = optional_regular_file(
            test_dll,
            test_output,
            description="referenced production DLL",
        )
        test_pdb_file = optional_regular_file(
            test_pdb,
            test_output,
            description="referenced production PDB",
        )
        if test_dll_file is None and test_pdb_file is None:
            continue
        if test_dll_file is None:
            raise RuntimeError(f"invalid referenced production DLL: {test_dll}")
        canonical_dll_file = optional_regular_file(
            canonical_dll,
            canonical_output,
            description="canonical production DLL",
        )
        if canonical_dll_file is None:
            raise RuntimeError(f"missing canonical production DLL: {canonical_dll}")
        canonical_dll_hash = sha256_file(canonical_dll_file)
        if sha256_file(test_dll_file) != canonical_dll_hash:
            raise RuntimeError(f"referenced production DLL hash mismatch: {name}")
        canonical_hashes[canonical_dll_file] = canonical_dll_hash
        canonical_pdb_file = optional_regular_file(
            canonical_pdb,
            canonical_output,
            description="canonical production PDB",
        )
        if (canonical_pdb_file is None) != (test_pdb_file is None):
            raise RuntimeError(f"referenced production PDB pairing mismatch: {name}")
        if canonical_pdb_file is not None and test_pdb_file is not None:
            canonical_pdb_hash = sha256_file(canonical_pdb_file)
            if sha256_file(test_pdb_file) != canonical_pdb_hash:
                raise RuntimeError(f"referenced production PDB hash mismatch: {name}")
            canonical_hashes[canonical_pdb_file] = canonical_pdb_hash
    return tuple(sorted(canonical_hashes.items(), key=lambda item: str(item[0])))


def prepare_local_dotnet_coverage_stage(
    project: CiDotnetProject,
    work_root: Path,
    coverage_directory: Path,
    repository_root: Path = ROOT,
) -> LocalDotnetCoverageStage:
    """Validate and snapshot one project's immutable Release test output."""

    source_output, release_suffix = find_project_release_output(
        project,
        repository_root,
    )
    canonical_outputs = canonical_production_release_outputs(
        release_suffix,
        repository_root,
        repository_root / "NvtFwCombiner.slnx",
    )
    canonical_hashes = require_production_release_matches(
        source_output,
        canonical_outputs,
    )
    project_token = hashlib.sha256(project.relative_path.encode("utf-8")).hexdigest()[
        :8
    ]
    shadow_output = work_root / project_token / release_suffix
    source_hashes = snapshot_regular_tree(
        source_output,
        shadow_output,
        source_boundary=repository_root,
        destination_boundary=work_root,
    )
    external_tools_source_root: Path | None = None
    external_tools_shadow_root: Path | None = None
    external_tools_hashes: dict[str, str] | None = None
    if project.requires_external_tools_fixture:
        external_tools_source_root = repository_root / "external-tools"
        external_tools_shadow_root = work_root / project_token / "external-tools"
        external_tools_hashes = snapshot_regular_tree(
            external_tools_source_root,
            external_tools_shadow_root,
            source_boundary=repository_root,
            destination_boundary=work_root,
        )
    test_assembly = shadow_output / f"{project.name}.dll"
    if not test_assembly.is_file() or is_reparse_point(test_assembly):
        raise RuntimeError(f"missing shadow test assembly: {test_assembly}")
    results_directory = coverage_directory / project.name
    if results_directory.exists():
        raise RuntimeError(f"duplicate local .NET results directory: {project.name}")
    results_directory.mkdir(parents=True)
    discovery_report = work_root / project_token / "discovered-tests.txt"
    return LocalDotnetCoverageStage(
        project,
        source_output,
        shadow_output,
        test_assembly,
        discovery_report,
        results_directory,
        source_hashes,
        canonical_hashes,
        external_tools_source_root,
        external_tools_shadow_root,
        external_tools_hashes,
    )


def require_local_dotnet_project_evidence(
    project: CiDotnetProject,
    results_directory: Path,
    discovery_report: Path,
    *,
    require_coverage: bool = True,
) -> None:
    """Validate one exact TRX, its discovery counters, and optional coverage evidence."""

    regular_files = enumerate_ci_regular_files(results_directory)
    if require_coverage:
        trx_report, _, _ = canonicalize_dotnet_project_reports_from_files(
            project.name,
            results_directory,
            regular_files,
        )
    else:
        trx_reports = tuple(
            path for path in regular_files if path.suffix.casefold() == ".trx"
        )
        if len(trx_reports) != 1 or trx_reports[0].name != "test-results.trx":
            raise RuntimeError(f"{project.name} must emit exactly one TRX")
        trx_report = trx_reports[0]
    counters = parse_trx_counters(trx_report)
    require_discovered_test_results(
        project,
        discovery_report,
        trx_report,
        counters,
        current_dotnet_producer_platform(),
    )


def run_local_dotnet_coverage_project(
    stage: LocalDotnetCoverageStage,
    dotnet: str,
    adapter_path: Path | None,
    environment: dict[str, str],
    log_path: Path | None,
) -> None:
    """Run and validate one isolated test-project producer."""

    run(
        dotnet_vstest_discovery_command(dotnet, stage.test_assembly),
        environment=environment,
        log_path=stage.discovery_report,
    )
    run(
        local_dotnet_vstest_command(
            dotnet,
            stage.test_assembly,
            adapter_path,
            stage.results_directory,
        ),
        environment=environment,
        log_path=log_path,
    )
    require_local_dotnet_project_evidence(
        stage.project,
        stage.results_directory,
        stage.discovery_report,
        require_coverage=adapter_path is not None,
    )


def require_local_dotnet_sources_unchanged(
    stages: Sequence[LocalDotnetCoverageStage],
    repository_root: Path,
    work_owner_root: Path | None = None,
) -> None:
    """Prove source, execution shadow, and canonical production stayed immutable."""

    shadow_owner_root = repository_root if work_owner_root is None else work_owner_root
    for stage in stages:
        external_tools_evidence = (
            stage.external_tools_source_root,
            stage.external_tools_shadow_root,
            stage.external_tools_hashes,
        )
        has_external_tools_evidence = all(
            evidence is not None for evidence in external_tools_evidence
        )
        has_any_external_tools_evidence = any(
            evidence is not None for evidence in external_tools_evidence
        )
        if (
            has_any_external_tools_evidence != has_external_tools_evidence
            or stage.project.requires_external_tools_fixture
            != has_external_tools_evidence
        ):
            raise RuntimeError(
                f"{stage.project.name} external-tools fixture evidence is incomplete"
            )
        require_regular_tree_hashes(
            stage.source_output,
            stage.source_hashes,
            boundary=repository_root,
            description=f"{stage.project.name} canonical test output",
        )
        require_regular_tree_hashes(
            stage.shadow_output,
            stage.source_hashes,
            boundary=shadow_owner_root,
            description=f"{stage.project.name} shadow test output",
        )
        if (
            stage.external_tools_source_root is not None
            and stage.external_tools_shadow_root is not None
            and stage.external_tools_hashes is not None
        ):
            require_regular_tree_hashes(
                stage.external_tools_source_root,
                stage.external_tools_hashes,
                boundary=repository_root,
                description=f"{stage.project.name} external-tools source fixture",
            )
            require_regular_tree_hashes(
                stage.external_tools_shadow_root,
                stage.external_tools_hashes,
                boundary=shadow_owner_root,
                description=f"{stage.project.name} external-tools shadow fixture",
            )
        for path, expected_hash in stage.canonical_hashes:
            current = optional_regular_file(
                path,
                repository_root,
                description="canonical production output",
            )
            if current is None:
                raise RuntimeError(f"canonical production output changed: {path}")
            if sha256_file(current) != expected_hash:
                raise RuntimeError(f"canonical production output hash changed: {path}")


def collect_local_dotnet_coverage(
    dotnet: str,
    coverage_directory: Path,
    work_root: Path,
    environment: dict[str, str],
    _aggregate_log_path: Path | None,
    *,
    repository_root: Path = ROOT,
    work_owner_root: Path | None = None,
    projects: tuple[CiDotnetProject, ...] | None = None,
    collect_coverage: bool = True,
) -> None:
    """Run private project snapshots; apply aggregate policy only for the full inventory."""

    full_coverage = projects is None
    if full_coverage and not collect_coverage:
        raise ValueError("full .NET verification requires coverage collection")
    projects = flatten_ci_dotnet_projects() if projects is None else projects
    owner_root = repository_root if work_owner_root is None else work_owner_root
    work = validated_disposable_directory(
        work_root,
        owner_root,
    )
    if work.exists():
        shutil.rmtree(work)
    work.mkdir(parents=True)
    stages: list[LocalDotnetCoverageStage] = []
    failure: BaseException | None = None
    try:
        adapter_path = (
            resolve_coverlet_adapter_path(repository_root) if collect_coverage else None
        )
        for project in projects:
            stages.append(
                prepare_local_dotnet_coverage_stage(
                    project,
                    work,
                    coverage_directory,
                    repository_root,
                )
            )

        batches = (
            *(
                (stage,)
                for stage in stages
                if stage.project.requires_exclusive_local_coverage
            ),
            tuple(
                stage
                for stage in stages
                if not stage.project.requires_exclusive_local_coverage
            ),
        )
        results = ()
        for batch in (batch for batch in batches if batch):
            lanes = tuple(
                VerificationLane(
                    stage.project.name,
                    lambda project_log, current=stage: (
                        run_local_dotnet_coverage_project(
                            current,
                            dotnet,
                            adapter_path,
                            environment,
                            project_log,
                        )
                    ),
                )
                for stage in batch
            )
            batch_results = run_lanes(
                lanes,
                jobs=min(MAXIMUM_VERIFY_JOBS, len(lanes)),
                log_directory=coverage_directory / "logs",
                lane_timeout_seconds=LOCAL_DOTNET_COVERAGE_TIMEOUT_SECONDS,
                preserve_cancellation_request=True,
            )
            results += batch_results
            if any(not result.succeeded for result in batch_results):
                break
        report_lane_results(results)
        try:
            require_local_dotnet_sources_unchanged(
                stages,
                repository_root,
                owner_root,
            )
        except BaseException as error:
            failure = combine_failures(failure, error, secondary_label="freshness gate")
        failed_projects = tuple(
            result.name for result in results if not result.succeeded
        )
        if failed_projects:
            failure = combine_failures(
                failure,
                RuntimeError(
                    "local .NET coverage projects failed: " + ", ".join(failed_projects)
                ),
                secondary_label="project collection",
            )
    except BaseException as error:
        failure = combine_failures(failure, error, secondary_label="collection")
    finally:
        try:
            if work.exists():
                shutil.rmtree(work)
        except BaseException as error:
            failure = combine_failures(failure, error, secondary_label="shadow cleanup")
    if failure is not None:
        raise failure
    if full_coverage:
        verify_coverage("dotnet", coverage_directory)


def run_dotnet_commands(
    commands: Sequence[list[str]],
    *,
    environment: dict[str, str],
    log_path: Path | None,
) -> None:
    """Run one .NET plan while retaining the optional CI build-log mirror."""

    build_log = os.environ.get("NFC_DOTNET_BUILD_LOG")
    for command in commands:
        if build_log and len(command) > 1 and command[1] == "build":
            build_log_path = Path(build_log)
            build_log_path.unlink(missing_ok=True)
            if log_path is None:
                run_with_log(command, build_log_path, environment=environment)
            else:
                run(
                    command,
                    environment=environment,
                    log_path=log_path,
                    mirror_log_path=build_log_path,
                )
        else:
            run(command, environment=environment, log_path=log_path)


def run_dotnet_build_plan(
    dotnet: str,
    *,
    environment: dict[str, str],
    log_path: Path | None,
    include_prebuild_checks: bool = True,
) -> None:
    """Run the SDK, solution-restore, and Release-build owner."""

    run([dotnet, "--version"], environment=environment, log_path=log_path)
    run_solution_restore_preserving_lock_projections(
        [dotnet, "restore", str(SOLUTION)],
        environment=environment,
        log_path=log_path,
        repository_root=ROOT,
        solution=SOLUTION,
    )
    run_dotnet_commands(
        dotnet_build_commands(dotnet, include_prebuild_checks=include_prebuild_checks),
        environment=environment,
        log_path=log_path,
    )


def cleanup_dotnet_batch(
    dotnet: str,
    environment: dict[str, str],
    log_path: Path | None,
) -> None:
    """Stop repository-owned build servers within the shared cleanup ceiling."""

    if PROCESS_CANCELLATION_REQUESTED.is_set():
        return
    cleanup_timeout = remaining_timeout(CLEANUP_TIMEOUT_SECONDS)
    if cleanup_timeout is None:
        cleanup_timeout = CLEANUP_TIMEOUT_SECONDS
    cleanup_deadline_token = CLEANUP_DEADLINE.set(monotonic() + cleanup_timeout)
    try:
        run(
            [dotnet, "build-server", "shutdown"],
            environment=environment,
            log_path=log_path,
            timeout_seconds=CLEANUP_TIMEOUT_SECONDS,
        )
        stop_idle_build_workers(log_path, timeout_seconds=CLEANUP_TIMEOUT_SECONDS)
    finally:
        CLEANUP_DEADLINE.reset(cleanup_deadline_token)


def release_golden_plan() -> tuple[
    tuple[CiDotnetProject, ...], dict[str, tuple[tuple[str, str], ...]],
]:
    """Reuse canonical evidence ownership, then close the whole-project selection."""
    if current_dotnet_producer_platform() != DOTNET_PRODUCER_WINDOWS:
        raise RuntimeError("release Golden execution requires Windows")
    errors: list[str] = []
    validate_canonical_golden(ROOT, errors)
    if errors:
        raise RuntimeError("release Golden canonical evidence invalid: " + "; ".join(errors))
    required = {"NvtFwCombiner.Bootstrap.Tests", "NvtFwCombiner.GoldenRegression.Tests"}
    projects = tuple(project for project in flatten_ci_dotnet_projects() if project.name in required)
    if len(projects) != len(required) or {project.name for project in projects} != required:
        raise RuntimeError("release Golden project inventory is incomplete or duplicated")
    canonical = ROOT / "testdata/golden/canonical"
    manifest = json.loads((canonical / "manifest.json").read_text(encoding="utf-8"))
    cases: dict[str, tuple[tuple[str, str], ...]] = {}
    for entry in manifest["cases"]:
        case = json.loads((canonical / entry["manifestPath"]).read_text(encoding="utf-8"))
        if not case["directGolden"]:
            continue  # Input-only and alias evidence cannot invent expected output.
        refs = []
        for reference in case["testDisposition"]["evidenceRefs"]:
            path, _, symbol = reference.partition("#")
            project = PurePosixPath(path).parts[1]
            if project not in required:
                raise RuntimeError(f"release Golden case outside selected projects: {case['caseId']}")
            refs.append((project, symbol))
        cases[case["caseId"]] = tuple(refs)
    if not cases:
        raise RuntimeError("release Golden plan contains no direct output cases")
    return projects, cases


def require_release_golden_results(
    coverage_directory: Path, cases: dict[str, tuple[tuple[str, str], ...]],
) -> None:
    """Require one fresh passed execution per case, including shared theory methods."""
    outcomes = {}
    for project in {project for refs in cases.values() for project, _symbol in refs}:
        reports = tuple((coverage_directory / project).rglob("*.trx"))
        if len(reports) != 1:
            raise RuntimeError(f"release Golden needs one fresh TRX: {project}")
        outcomes[project] = parse_trx_test_outcomes(reports[0], preserve_case_identity=True)
    references: dict[tuple[str, str], list[str]] = {}
    for case_id, refs in cases.items():
        for reference in refs:
            references.setdefault(reference, []).append(case_id)
    for (project, symbol), case_ids in references.items():
        rows = [(identity, outcome, count)
                for outcome, identities in outcomes[project].items()
                for identity, count in identities.items()
                if canonical_vstest_identity(identity).endswith("." + symbol)]
        if len({canonical_vstest_identity(identity) for identity, _, _ in rows}) != 1:
            raise RuntimeError(f"release Golden case lacks unique method: {case_ids[0]}")
        terminals: dict[str, Counter[str]] = {case_id: Counter() for case_id in case_ids}
        for identity, outcome, count in rows:
            case_id = case_ids[0]
            if len(case_ids) > 1:
                match = re.match(r'^[^(]+\(caseId: "([^"]+)"(?:, |\))', identity)
                if match is None or match[1] not in terminals:
                    raise RuntimeError(f"release Golden has an undeclared case identity: {symbol}")
                case_id = match[1]
            terminals[case_id][outcome] += count
        for case_id, counts in terminals.items():
            if counts != Counter({"Passed": 1}):
                raise RuntimeError(f"release Golden case lacks unique passed execution: {case_id}")
    for case_id in cases:
        print(f"Release Golden executed: {case_id}")
    print(f"Release Golden: {len(cases)} direct output cases; not a full-suite coverage gate.")


def verify_dotnet(log_path: Path | None = None, *, release_golden: bool = False) -> None:
    golden_plan = release_golden_plan() if release_golden else None
    dotnet = resolve_dotnet()
    coverage_directory = reset_coverage_directory("dotnet")
    environment = dotnet_batch_environment()
    failure: BaseException | None = None
    try:
        run_dotnet_build_plan(
            dotnet, environment=environment, log_path=log_path,
            **({"include_prebuild_checks": False} if golden_plan is not None else {}),
        )
        collect_local_dotnet_coverage(
            dotnet,
            coverage_directory,
            DOTNET_COVERAGE_WORK_ROOT,
            environment,
            log_path,
            work_owner_root=(
                Path(os.environ[TEST_SESSION_ENVIRONMENT_VARIABLE])
                if os.environ.get(TEST_SESSION_ENVIRONMENT_VARIABLE)
                else ROOT
            ),
            **({"projects": golden_plan[0], "collect_coverage": False}
               if golden_plan is not None else {}),
        )
        if golden_plan is not None:
            require_release_golden_results(coverage_directory, golden_plan[1])
    except BaseException as error:
        failure = error
    finally:
        # Avalonia/Roslyn may start compiler servers even with node reuse disabled.
        # Stop only servers from the repository-selected SDK after every verification run.
        try:
            cleanup_dotnet_batch(dotnet, environment, log_path)
        except BaseException as error:
            failure = combine_failures(failure, error)
    if failure is not None:
        raise failure


def ci_dotnet_build_command(
    dotnet: str,
    project: CiDotnetProject,
) -> list[str]:
    """Build one Release test project before taking its immutable snapshot."""

    return [
        dotnet,
        "build",
        str(ROOT / project.relative_path),
        "-c",
        "Release",
        "--no-restore",
    ]


def require_ci_source_sha() -> str:
    source_sha = os.environ.get("GITHUB_SHA", "").strip().casefold()
    if CI_SOURCE_SHA_PATTERN.fullmatch(source_sha) is None:
        raise RuntimeError(
            "GITHUB_SHA must contain the exact 40-character CI source SHA"
        )
    return source_sha


def repository_sdk_version() -> str:
    document = json.loads((ROOT / "global.json").read_text(encoding="utf-8"))
    version = document.get("sdk", {}).get("version")
    if not isinstance(version, str) or not version:
        raise RuntimeError("global.json is missing the pinned SDK version")
    return version


def require_logged_sdk_version(log_path: Path, expected: str) -> None:
    lines = {line.strip() for line in log_path.read_text(encoding="utf-8").splitlines()}
    if expected not in lines:
        raise RuntimeError(
            f"resolved .NET SDK does not match global.json: expected {expected}"
        )


def reset_ci_dotnet_evidence_directory(
    directory: Path, *, boundary: Path | None = None
) -> Path:
    target = validated_disposable_directory(
        directory,
        ROOT if boundary is None else boundary,
    )
    if target.exists():
        shutil.rmtree(target)
    target.mkdir(parents=True)
    return target


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        while chunk := source.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def regular_tree_hashes(
    directory: Path,
    *,
    boundary: Path,
    description: str,
) -> dict[str, str]:
    """Hash one closed regular-file tree without following reparse points."""

    root = directory.absolute()
    return {
        path.relative_to(root).as_posix(): sha256_file(path)
        for path in enumerate_regular_files(
            directory,
            boundary=boundary,
            description=description,
        )
    }


def require_regular_tree_hashes(
    directory: Path,
    expected: dict[str, str],
    *,
    boundary: Path,
    description: str,
) -> None:
    """Fail closed when a regular-file inventory or any content hash changed."""

    observed = regular_tree_hashes(
        directory,
        boundary=boundary,
        description=description,
    )
    if observed != expected:
        raise RuntimeError(f"{description} inventory or hash changed")


def snapshot_regular_tree(
    source: Path,
    destination: Path,
    *,
    source_boundary: Path,
    destination_boundary: Path,
) -> dict[str, str]:
    """Copy one immutable regular-file snapshot and verify both sides exactly."""

    source_hashes = regular_tree_hashes(
        source,
        boundary=source_boundary,
        description="coverage source output",
    )
    target = validated_path_within_root(
        destination,
        destination_boundary,
        description="coverage shadow output",
    )
    if target.exists():
        raise RuntimeError(f"coverage shadow output already exists: {target}")
    target.mkdir(parents=True)
    for relative_path in source_hashes:
        source_file = source.joinpath(*PurePosixPath(relative_path).parts)
        destination_file = target.joinpath(*PurePosixPath(relative_path).parts)
        destination_file.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source_file, destination_file, follow_symlinks=False)
    require_regular_tree_hashes(
        source,
        source_hashes,
        boundary=source_boundary,
        description="coverage source output changed during snapshot",
    )
    require_regular_tree_hashes(
        target,
        source_hashes,
        boundary=destination_boundary,
        description="coverage shadow output",
    )
    return source_hashes


def require_regular_file(path: Path, root: Path, *, description: str) -> Path:
    current = validated_path_within_root(path, root, description=description)
    relative = current.relative_to(root.absolute())
    try:
        mode = current.stat(follow_symlinks=False).st_mode
    except OSError as error:
        raise RuntimeError(
            f"missing {description} file: {relative.as_posix()}"
        ) from error
    if not stat.S_ISREG(mode):
        raise RuntimeError(f"non-regular {description} is forbidden: {current}")
    return current


def optional_regular_file(
    path: Path,
    root: Path,
    *,
    description: str,
) -> Path | None:
    """Return an optional regular file while rejecting every invalid entry."""

    current = validated_path_within_root(path, root, description=description)
    try:
        mode = current.stat(follow_symlinks=False).st_mode
    except FileNotFoundError:
        return None
    except OSError as error:
        raise RuntimeError(f"invalid {description}: {current}") from error
    if not stat.S_ISREG(mode):
        raise RuntimeError(f"non-regular {description} is forbidden: {current}")
    return current


def enumerate_regular_files(
    directory: Path,
    *,
    boundary: Path,
    description: str,
) -> tuple[Path, ...]:
    root = validated_path_within_root(directory, boundary, description=description)
    if not root.is_dir() or is_reparse_point(root):
        raise RuntimeError(f"invalid {description} directory: {directory}")
    files: list[Path] = []
    for current_text, directory_names, file_names in os.walk(
        root,
        topdown=True,
        followlinks=False,
    ):
        current = Path(current_text)
        for name in directory_names:
            child = current / name
            if is_reparse_point(child) or not child.is_dir():
                raise RuntimeError(f"reparse-point {description} is forbidden: {child}")
        for name in file_names:
            files.append(
                require_regular_file(current / name, root, description=description)
            )
    return tuple(sorted(files))


def require_ci_regular_file(path: Path, evidence_root: Path) -> Path:
    return require_regular_file(path, evidence_root, description=".NET CI evidence")


def enumerate_ci_regular_files(directory: Path) -> tuple[Path, ...]:
    return enumerate_regular_files(
        directory,
        boundary=directory,
        description=".NET CI evidence",
    )


def ci_relative_path(path: Path, evidence_root: Path) -> str:
    return path.absolute().relative_to(evidence_root.absolute()).as_posix()


def write_ci_manifest(path: Path, document: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(document, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )


def ci_file_hashes(paths: Sequence[Path], evidence_root: Path) -> dict[str, str]:
    hashes: dict[str, str] = {}
    for path in sorted(paths):
        if not path.exists() and not is_reparse_point(path):
            continue
        regular = require_ci_regular_file(path, evidence_root)
        hashes[ci_relative_path(regular, evidence_root)] = sha256_file(regular)
    return hashes


def publish_ci_dotnet_artifact(
    source_root: Path,
    upload_root: Path,
    manifest_relative_path: str,
    file_hashes: dict[str, str],
    *,
    upload_boundary: Path | None = None,
) -> None:
    """Copy only declared regular evidence into a clean upload tree."""

    expected_paths = {manifest_relative_path, *file_hashes}
    target = reset_ci_dotnet_evidence_directory(
        upload_root,
        boundary=ROOT if upload_boundary is None else upload_boundary,
    )
    source_files: dict[str, Path] = {}
    for relative_path in sorted(expected_paths):
        source = resolve_ci_evidence_file(source_root, relative_path)
        if (
            relative_path in file_hashes
            and sha256_file(source) != file_hashes[relative_path]
        ):
            raise RuntimeError(
                f".NET CI evidence hash changed before upload: {relative_path}"
            )
        source_files[relative_path] = source

    for relative_path, source in source_files.items():
        destination = target.joinpath(*PurePosixPath(relative_path).parts)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination, follow_symlinks=False)

    actual_paths = {
        ci_relative_path(path, target) for path in enumerate_ci_regular_files(target)
    }
    if actual_paths != expected_paths:
        raise RuntimeError(".NET CI upload staging contains missing or extra files")
    for relative_path, expected_hash in file_hashes.items():
        staged = resolve_ci_evidence_file(target, relative_path)
        if sha256_file(staged) != expected_hash:
            raise RuntimeError(
                f"staged .NET CI evidence hash mismatch: {relative_path}"
            )


def parse_trx_counters(path: Path) -> dict[str, int]:
    try:
        counters = ET.parse(path).find(".//{*}Counters")
    except ET.ParseError as error:
        raise RuntimeError(f"TRX result is invalid XML: {path}") from error
    if counters is None:
        raise RuntimeError(f"TRX result has no counters: {path}")
    names = ("total", "executed", "passed", "failed")
    try:
        values = {name: int(counters.attrib[name]) for name in names}
    except (KeyError, ValueError) as error:
        raise RuntimeError(f"TRX result has invalid counters: {path}") from error
    skipped = values["total"] - values["executed"]
    if skipped < 0 or values["passed"] + values["failed"] != values["executed"]:
        raise RuntimeError(f"TRX result has inconsistent counters: {path}")
    return {
        "total": values["total"],
        "passed": values["passed"],
        "failed": values["failed"],
        "skipped": skipped,
    }


def reject_duplicate_json_keys(pairs: list[tuple[str, object]]) -> dict[str, object]:
    document: dict[str, object] = {}
    for key, value in pairs:
        if key in document:
            raise RuntimeError(f"duplicate JSON key in .NET CI evidence: {key}")
        document[key] = value
    return document


def normalize_ci_dotnet_coverage_reports(
    json_report: Path,
    cobertura_report: Path,
    repository_root: Path | None = None,
) -> None:
    """Remove runner-specific roots while preserving exact coverage evidence."""

    repository_root = ROOT if repository_root is None else repository_root
    try:
        with json_report.open(encoding="utf-8") as handle:
            json_document = json.load(
                handle,
                object_pairs_hook=reject_duplicate_json_keys,
            )
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise RuntimeError(f"invalid Coverlet JSON evidence: {json_report}") from error
    if not isinstance(json_document, dict):
        raise RuntimeError(f"invalid Coverlet JSON evidence: {json_report}")
    normalized_document: dict[str, object] = {}
    json_source_identities: dict[str, str] = {}
    for module_name, raw_sources in json_document.items():
        if not isinstance(module_name, str) or not isinstance(raw_sources, dict):
            raise RuntimeError(f"invalid Coverlet JSON evidence: {json_report}")
        normalized_sources: dict[str, object] = {}
        for source_path, classes in raw_sources.items():
            if not isinstance(source_path, str) or not isinstance(classes, dict):
                raise RuntimeError(f"invalid Coverlet JSON evidence: {json_report}")
            try:
                relative_path = repository_relative_coverage_source(
                    source_path,
                    repository_root,
                )
            except ValueError as error:
                raise RuntimeError(
                    f"Coverlet JSON source is outside the producer repository: {source_path}"
                ) from error
            prior_identity = json_source_identities.setdefault(
                relative_path,
                source_path,
            )
            if prior_identity != source_path or relative_path in normalized_sources:
                raise RuntimeError(
                    f"Coverlet JSON source identity collides after normalization: {relative_path}"
                )
            normalized_sources[relative_path] = classes
        normalized_document[module_name] = normalized_sources
    normalized_json = (
        json.dumps(normalized_document, indent=2, ensure_ascii=False) + "\n"
    )

    try:
        cobertura_tree = ET.parse(cobertura_report)
    except (OSError, ET.ParseError) as error:
        raise RuntimeError(f"invalid Cobertura evidence: {cobertura_report}") from error
    cobertura_root = cobertura_tree.getroot()
    source_containers = cobertura_root.findall("./sources")
    if len(source_containers) > 1:
        raise RuntimeError("Cobertura evidence has multiple source-root containers")
    source_roots = tuple(
        source.text.strip()
        for source in cobertura_root.findall("./sources/source")
        if source.text and source.text.strip()
    )
    cobertura_source_identities: dict[str, str] = {}
    for class_node in cobertura_root.findall(".//class"):
        filename = class_node.get("filename")
        if not filename:
            raise RuntimeError("Cobertura class is missing its source filename")
        try:
            relative_path = repository_relative_coverage_source(
                filename,
                repository_root,
                source_roots,
            )
        except ValueError as error:
            raise RuntimeError(
                f"Cobertura source is not a unique producer repository path: {filename}"
            ) from error
        prior_identity = cobertura_source_identities.setdefault(
            relative_path,
            filename,
        )
        if prior_identity != filename:
            raise RuntimeError(
                f"Cobertura source identity collides after normalization: {relative_path}"
            )
        class_node.set("filename", relative_path)
    if source_containers:
        sources_node = source_containers[0]
    else:
        sources_node = ET.Element("sources")
        cobertura_root.insert(0, sources_node)
    sources_node.clear()
    ET.SubElement(sources_node, "source").text = "."
    ET.indent(cobertura_tree, space="  ")
    json_report.write_text(
        normalized_json,
        encoding="utf-8",
        newline="\n",
    )
    cobertura_tree.write(
        cobertura_report,
        encoding="utf-8",
        xml_declaration=True,
    )


def canonicalize_dotnet_project_reports(
    owner_name: str,
    results_directory: Path,
) -> tuple[Path, Path, Path]:
    regular_files = enumerate_ci_regular_files(results_directory)
    return canonicalize_dotnet_project_reports_from_files(
        owner_name,
        results_directory,
        regular_files,
    )


def canonicalize_dotnet_project_reports_from_files(
    owner_name: str,
    results_directory: Path,
    regular_files: Sequence[Path],
) -> tuple[Path, Path, Path]:
    """Retain one exact TRX and one hash-identical coverage attachment pair."""

    trx_reports = tuple(
        path for path in regular_files if path.suffix.casefold() == ".trx"
    )
    json_reports = tuple(path for path in regular_files if path.name == "coverage.json")
    cobertura_reports = tuple(
        path for path in regular_files if path.name == "coverage.cobertura.xml"
    )
    if (
        len(trx_reports) != 1
        or trx_reports[0].name != "test-results.trx"
        or not json_reports
        or {report.parent for report in json_reports}
        != {report.parent for report in cobertura_reports}
    ):
        raise RuntimeError(
            f"{owner_name} must emit exactly one TRX and one paired coverage report"
        )
    json_hashes = {sha256_file(report) for report in json_reports}
    cobertura_hashes = {sha256_file(report) for report in cobertura_reports}
    if len(json_hashes) != 1 or len(cobertura_hashes) != 1:
        raise RuntimeError(f"{owner_name} emitted divergent coverage attachments")
    canonical_parent = min(
        (report.parent for report in json_reports),
        key=lambda parent: len(parent.relative_to(results_directory).parts),
    )
    for report in (*json_reports, *cobertura_reports):
        if report.parent != canonical_parent:
            report.unlink()
    json_report = canonical_parent / "coverage.json"
    cobertura_report = canonical_parent / "coverage.cobertura.xml"
    return trx_reports[0], json_report, cobertura_report


def collect_ci_project_evidence(
    project: CiDotnetProject,
    results_directory: Path,
    evidence_root: Path,
    discovery_report: Path,
    test_assembly_sha256: str,
    producer_platform: str,
) -> tuple[dict[str, object], tuple[Path, ...]]:
    trx_report, json_report, cobertura_report = canonicalize_dotnet_project_reports(
        project.name,
        results_directory,
    )
    normalize_ci_dotnet_coverage_reports(json_report, cobertura_report)

    counters = parse_trx_counters(trx_report)
    require_discovered_test_results(
        project,
        discovery_report,
        trx_report,
        counters,
        producer_platform,
    )

    evidence_paths = (discovery_report, trx_report, json_report, cobertura_report)
    return (
        {
            "relativePath": project.relative_path,
            "total": counters["total"],
            "passed": counters["passed"],
            "failed": counters["failed"],
            "skipped": counters["skipped"],
            "testAssemblySha256": test_assembly_sha256,
            "discovery": ci_relative_path(discovery_report, evidence_root),
            "trx": ci_relative_path(trx_report, evidence_root),
            "coverageJson": ci_relative_path(json_report, evidence_root),
            "coverageCobertura": ci_relative_path(cobertura_report, evidence_root),
        },
        evidence_paths,
    )


def combine_failures(
    primary: BaseException | None,
    secondary: BaseException,
    *,
    secondary_label: str = "cleanup",
) -> BaseException:
    if primary is None:
        return secondary
    return RuntimeError(f"{primary}; {secondary_label} also failed: {secondary}")


def verify_ci_dotnet_build() -> None:
    """Produce the full Release-build evidence independently of test shards."""

    evidence_root = reset_ci_dotnet_evidence_directory(
        CI_DOTNET_EVIDENCE_ROOT,
        boundary=(
            Path(os.environ[TEST_SESSION_ENVIRONMENT_VARIABLE])
            if os.environ.get(TEST_SESSION_ENVIRONMENT_VARIABLE)
            else ROOT
        ),
    )
    reset_ci_dotnet_evidence_directory(CI_DOTNET_UPLOAD_ROOT)
    output = evidence_root / "build"
    output.mkdir(parents=True)
    log_path = output / "build.log"
    dotnet = resolve_dotnet()
    environment = dotnet_batch_environment()
    failure: BaseException | None = None
    try:
        verify_windows_process_orchestration(log_path)
        run_dotnet_build_plan(dotnet, environment=environment, log_path=log_path)
        require_logged_sdk_version(log_path, repository_sdk_version())
    except BaseException as error:
        failure = error
    try:
        cleanup_dotnet_batch(dotnet, environment, log_path)
    except BaseException as error:
        failure = combine_failures(failure, error)

    file_hashes = ci_file_hashes((log_path,), evidence_root)
    document = {
        "schemaVersion": CI_DOTNET_EVIDENCE_SCHEMA_VERSION,
        "kind": "dotnet-build",
        "sourceSha": require_ci_source_sha(),
        "sdkVersion": repository_sdk_version(),
        "success": failure is None,
        "files": file_hashes,
    }
    write_ci_manifest(output / "manifest.json", document)
    try:
        publish_ci_dotnet_artifact(
            evidence_root,
            CI_DOTNET_UPLOAD_ROOT,
            "build/manifest.json",
            file_hashes,
        )
    except BaseException as error:
        failure = combine_failures(
            failure,
            error,
            secondary_label="evidence staging",
        )
    if failure is not None:
        raise failure


def verify_ci_dotnet_test_shard(shard: str) -> None:
    """Run every project in one closed shard and retain all ordinary failures."""

    projects = CI_DOTNET_SHARDS[shard]
    evidence_root = reset_ci_dotnet_evidence_directory(
        CI_DOTNET_EVIDENCE_ROOT,
        boundary=(
            Path(os.environ[TEST_SESSION_ENVIRONMENT_VARIABLE])
            if os.environ.get(TEST_SESSION_ENVIRONMENT_VARIABLE)
            else ROOT
        ),
    )
    reset_ci_dotnet_evidence_directory(CI_DOTNET_UPLOAD_ROOT)
    output = evidence_root / "shards" / shard
    output.mkdir(parents=True)
    results_root = output / "results"
    log_path = output / "shard.log"
    dotnet = resolve_dotnet()
    environment = dotnet_batch_environment()
    project_rows: list[dict[str, object]] = []
    evidence_paths: list[Path] = [log_path]
    failures: list[str] = []
    fatal_failure: BaseException | None = None
    try:
        run(
            [dotnet, "--version"],
            environment=environment,
            log_path=log_path,
        )
        run_solution_restore_preserving_lock_projections(
            [dotnet, "restore", str(SOLUTION)],
            environment=environment,
            log_path=log_path,
            repository_root=ROOT,
            solution=SOLUTION,
        )
        adapter_path = resolve_coverlet_adapter_path(ROOT)
        for project in projects:
            results_directory = results_root / project.name
            results_directory.mkdir(parents=True)
            discovery_report = results_directory / "discovered-tests.txt"
            try:
                run(
                    ci_dotnet_build_command(dotnet, project),
                    environment=environment,
                    log_path=log_path,
                )
                source_output, _ = find_project_release_output(project)
                source_hashes = regular_tree_hashes(
                    source_output,
                    boundary=ROOT,
                    description=f"{project.name} CI Release output",
                )
                test_assembly = source_output / f"{project.name}.dll"
                test_assembly_sha256 = sha256_file(test_assembly)
                run(
                    dotnet_vstest_discovery_command(dotnet, test_assembly),
                    environment=environment,
                    log_path=discovery_report,
                )
                run(
                    local_dotnet_vstest_command(
                        dotnet,
                        test_assembly,
                        adapter_path,
                        results_directory,
                    ),
                    environment=environment,
                    log_path=log_path,
                )
                require_regular_tree_hashes(
                    source_output,
                    source_hashes,
                    boundary=ROOT,
                    description=f"{project.name} CI Release output",
                )
                row, paths = collect_ci_project_evidence(
                    project,
                    results_directory,
                    evidence_root,
                    discovery_report,
                    test_assembly_sha256,
                    CI_DOTNET_PRODUCER_PLATFORM,
                )
                if sha256_file(test_assembly) != test_assembly_sha256:
                    raise RuntimeError(
                        f"{project.name} captured test assembly hash changed"
                    )
            except (subprocess.CalledProcessError, RuntimeError, ValueError) as error:
                failures.append(f"{project.name}: {error}")
                continue
            project_rows.append(row)
            evidence_paths.extend(paths)
        require_logged_sdk_version(log_path, repository_sdk_version())
    except BaseException as error:
        fatal_failure = error
    if fatal_failure is None and failures:
        fatal_failure = RuntimeError("; ".join(failures))
    try:
        cleanup_dotnet_batch(dotnet, environment, log_path)
    except BaseException as error:
        fatal_failure = combine_failures(fatal_failure, error)

    file_hashes = ci_file_hashes(evidence_paths, evidence_root)
    document = {
        "schemaVersion": CI_DOTNET_EVIDENCE_SCHEMA_VERSION,
        "kind": "dotnet-test-shard",
        "sourceSha": require_ci_source_sha(),
        "sdkVersion": repository_sdk_version(),
        "success": fatal_failure is None,
        "shard": shard,
        "producerPlatform": CI_DOTNET_PRODUCER_PLATFORM,
        "projects": project_rows,
        "files": file_hashes,
    }
    write_ci_manifest(output / "manifest.json", document)
    try:
        publish_ci_dotnet_artifact(
            evidence_root,
            CI_DOTNET_UPLOAD_ROOT,
            f"shards/{shard}/manifest.json",
            file_hashes,
        )
    except BaseException as error:
        fatal_failure = combine_failures(
            fatal_failure,
            error,
            secondary_label="evidence staging",
        )
    if fatal_failure is not None:
        raise fatal_failure


def load_ci_manifest(path: Path) -> dict[str, object]:
    try:
        document = json.loads(
            path.read_text(encoding="utf-8"),
            object_pairs_hook=reject_duplicate_json_keys,
        )
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise RuntimeError(f"invalid .NET CI evidence manifest: {path}") from error
    if not isinstance(document, dict):
        raise RuntimeError(f".NET CI evidence manifest must be an object: {path}")
    return document


def resolve_ci_evidence_file(evidence_root: Path, relative_path: object) -> Path:
    if not isinstance(relative_path, str) or "\\" in relative_path:
        raise RuntimeError(".NET CI evidence paths must be canonical relative paths")
    pure = PurePosixPath(relative_path)
    if (
        pure.is_absolute()
        or not pure.parts
        or pure.as_posix() != relative_path
        or any(part in {"", ".", ".."} for part in pure.parts)
    ):
        raise RuntimeError(f"unsafe .NET CI evidence path: {relative_path}")
    return require_ci_regular_file(
        evidence_root.joinpath(*pure.parts),
        evidence_root,
    )


def require_ci_dotnet_artifact_roots(download_root: Path) -> dict[str, Path]:
    expected_names = {
        "build": "dotnet-build-evidence",
        **{shard: f"dotnet-test-{shard}-evidence" for shard in CI_DOTNET_SHARDS},
    }
    root = download_root.absolute()
    if not root.is_dir() or is_reparse_point(root):
        raise RuntimeError(f"missing or invalid .NET CI evidence root: {download_root}")
    entries = {path.name: path for path in root.iterdir()}
    if set(entries) != set(expected_names.values()):
        raise RuntimeError("missing or extra .NET CI evidence producer artifacts")
    artifact_roots: dict[str, Path] = {}
    for owner, name in expected_names.items():
        artifact_root = entries[name]
        if is_reparse_point(artifact_root) or not artifact_root.is_dir():
            raise RuntimeError(f"invalid .NET CI producer artifact root: {name}")
        artifact_roots[owner] = artifact_root
    return artifact_roots


def require_manifest_keys(
    document: dict[str, object],
    expected: set[str],
    label: str,
) -> None:
    if set(document) != expected:
        raise RuntimeError(f"{label} manifest fields changed")


def validate_manifest_files(
    document: dict[str, object],
    evidence_root: Path,
    expected_paths: set[str],
) -> set[str]:
    files = document.get("files")
    if not isinstance(files, dict) or set(files) != expected_paths:
        raise RuntimeError(".NET CI evidence file inventory changed")
    for relative_path, expected_hash in files.items():
        if (
            not isinstance(expected_hash, str)
            or re.fullmatch(r"[0-9a-f]{64}", expected_hash) is None
        ):
            raise RuntimeError(f"invalid .NET CI evidence hash: {relative_path}")
        path = resolve_ci_evidence_file(evidence_root, relative_path)
        if sha256_file(path) != expected_hash:
            raise RuntimeError(f".NET CI evidence hash mismatch: {relative_path}")
    return set(files)


def require_ci_project_evidence_paths(
    row: dict[str, object],
    shard: str,
    project: CiDotnetProject,
    artifact_root: Path,
    seen_paths: set[str],
) -> tuple[Path, Path, Path, Path]:
    raw_discovery = row["discovery"]
    raw_trx = row["trx"]
    raw_json = row["coverageJson"]
    raw_cobertura = row["coverageCobertura"]
    if (
        not isinstance(raw_discovery, str)
        or not isinstance(raw_trx, str)
        or not isinstance(raw_json, str)
        or not isinstance(raw_cobertura, str)
    ):
        raise RuntimeError(f"{project.name} .NET CI evidence paths are invalid")
    discovery_path = PurePosixPath(raw_discovery)
    trx_path = PurePosixPath(raw_trx)
    json_path = PurePosixPath(raw_json)
    cobertura_path = PurePosixPath(raw_cobertura)
    project_root = PurePosixPath("shards", shard, "results", project.name)
    if discovery_path != project_root / "discovered-tests.txt":
        raise RuntimeError(f"{project.name} discovery path changed")
    if trx_path != project_root / "test-results.trx":
        raise RuntimeError(f"{project.name} TRX path changed")
    if any(
        (
            json_path.name != "coverage.json",
            cobertura_path.name != "coverage.cobertura.xml",
            json_path.parent != cobertura_path.parent,
            not json_path.parent.is_relative_to(project_root),
        )
    ):
        raise RuntimeError(f"{project.name} coverage report pairing changed")
    row_paths = {raw_discovery, raw_trx, raw_json, raw_cobertura}
    if len(row_paths) != 4 or seen_paths.intersection(row_paths):
        raise RuntimeError(f"{project.name} reuses .NET CI evidence")
    seen_paths.update(row_paths)
    return (
        resolve_ci_evidence_file(artifact_root, raw_discovery),
        resolve_ci_evidence_file(artifact_root, raw_trx),
        resolve_ci_evidence_file(artifact_root, raw_json),
        resolve_ci_evidence_file(artifact_root, raw_cobertura),
    )


def finalize_ci_dotnet_evidence(download_root: Path) -> None:
    """Validate the complete build/test evidence set and publish canonical coverage."""

    job_results = {
        "build": os.environ.get("NFC_CI_DOTNET_BUILD_RESULT"),
        "test": os.environ.get("NFC_CI_DOTNET_TEST_RESULT"),
    }
    if job_results["build"] not in {None, "success"}:
        raise RuntimeError(f".NET CI build producer failed: {job_results['build']}")
    if job_results["test"] not in {None, "success"}:
        raise RuntimeError(f".NET CI test producer failed: {job_results['test']}")
    artifact_roots = require_ci_dotnet_artifact_roots(download_root)
    manifests = {
        "build": resolve_ci_evidence_file(
            artifact_roots["build"],
            "build/manifest.json",
        ),
        **{
            shard: resolve_ci_evidence_file(
                artifact_roots[shard],
                f"shards/{shard}/manifest.json",
            )
            for shard in CI_DOTNET_SHARDS
        },
    }

    source_sha = require_ci_source_sha()
    sdk_version = repository_sdk_version()
    build = load_ci_manifest(manifests["build"])
    require_manifest_keys(
        build,
        {"schemaVersion", "kind", "sourceSha", "sdkVersion", "success", "files"},
        "build",
    )
    if build != {
        **build,
        "schemaVersion": CI_DOTNET_EVIDENCE_SCHEMA_VERSION,
        "kind": "dotnet-build",
        "sourceSha": source_sha,
        "sdkVersion": sdk_version,
        "success": True,
    }:
        raise RuntimeError("build .NET CI evidence identity or result changed")
    declared_files = validate_manifest_files(
        build,
        artifact_roots["build"],
        {"build/build.log"},
    )
    build_files = {"build/manifest.json", *declared_files}
    actual_build_files = {
        ci_relative_path(path, artifact_roots["build"])
        for path in enumerate_ci_regular_files(artifact_roots["build"])
    }
    if actual_build_files != build_files:
        raise RuntimeError("build .NET CI artifact contains missing or extra files")
    coverage_sources: list[tuple[CiDotnetProject, Path, Path]] = []
    project_counters: list[dict[str, int]] = []

    for shard, projects in CI_DOTNET_SHARDS.items():
        artifact_root = artifact_roots[shard]
        manifest = load_ci_manifest(manifests[shard])
        require_manifest_keys(
            manifest,
            {
                "schemaVersion",
                "kind",
                "sourceSha",
                "sdkVersion",
                "success",
                "shard",
                "producerPlatform",
                "projects",
                "files",
            },
            shard,
        )
        if any(
            (
                manifest.get("schemaVersion") != CI_DOTNET_EVIDENCE_SCHEMA_VERSION,
                manifest.get("kind") != "dotnet-test-shard",
                manifest.get("sourceSha") != source_sha,
                manifest.get("sdkVersion") != sdk_version,
                manifest.get("success") is not True,
                manifest.get("shard") != shard,
                manifest.get("producerPlatform") != CI_DOTNET_PRODUCER_PLATFORM,
            )
        ):
            raise RuntimeError(f"{shard} .NET CI evidence identity or result changed")
        rows = manifest.get("projects")
        if not isinstance(rows, list) or len(rows) != len(projects):
            raise RuntimeError(f"{shard} .NET CI project inventory changed")
        expected_paths = {f"shards/{shard}/shard.log"}
        seen_row_paths: set[str] = set()
        for project, row in zip(projects, rows, strict=True):
            if not isinstance(row, dict):
                raise RuntimeError(f"{shard} .NET CI project row is invalid")
            require_manifest_keys(
                row,
                {
                    "relativePath",
                    "total",
                    "passed",
                    "failed",
                    "skipped",
                    "testAssemblySha256",
                    "discovery",
                    "trx",
                    "coverageJson",
                    "coverageCobertura",
                },
                project.name,
            )
            if row.get("relativePath") != project.relative_path:
                raise RuntimeError(f"{project.name} .NET CI project identity changed")
            test_assembly_sha256 = row.get("testAssemblySha256")
            if (
                not isinstance(test_assembly_sha256, str)
                or re.fullmatch(r"[0-9a-f]{64}", test_assembly_sha256) is None
            ):
                raise RuntimeError(
                    f"{project.name} captured test assembly hash changed"
                )
            discovery, trx, json_report, cobertura_report = (
                require_ci_project_evidence_paths(
                    row,
                    shard,
                    project,
                    artifact_root,
                    seen_row_paths,
                )
            )
            evidence_paths = {
                row["discovery"],
                row["trx"],
                row["coverageJson"],
                row["coverageCobertura"],
            }
            expected_paths.update(evidence_paths)
            counters = parse_trx_counters(trx)
            manifest_counters = {
                name: row.get(name) for name in ("total", "passed", "failed", "skipped")
            }
            if manifest_counters != counters:
                raise RuntimeError(f"{project.name} manifest/TRX counters changed")
            require_discovered_test_results(
                project,
                discovery,
                trx,
                counters,
                CI_DOTNET_PRODUCER_PLATFORM,
            )
            project_counters.append(counters)
            coverage_sources.append(
                (
                    project,
                    json_report,
                    cobertura_report,
                )
            )
        shard_files = validate_manifest_files(
            manifest,
            artifact_root,
            expected_paths,
        )
        if declared_files.intersection(shard_files):
            raise RuntimeError("duplicate .NET CI evidence ownership")
        declared_files.update(shard_files)
        expected_artifact_files = {
            f"shards/{shard}/manifest.json",
            *shard_files,
        }
        actual_artifact_files = {
            ci_relative_path(path, artifact_root)
            for path in enumerate_ci_regular_files(artifact_root)
        }
        if actual_artifact_files != expected_artifact_files:
            raise RuntimeError(
                f"{shard} .NET CI artifact contains missing or extra files"
            )

    if any(result != "success" for result in job_results.values()):
        raise RuntimeError(f".NET CI producer jobs did not all succeed: {job_results}")

    coverage_root = reset_coverage_directory("dotnet")
    for project, json_report, cobertura_report in coverage_sources:
        destination = coverage_root / project.name
        destination.mkdir()
        shutil.copyfile(json_report, destination / "coverage.json")
        shutil.copyfile(cobertura_report, destination / "coverage.cobertura.xml")
    verify_coverage("dotnet", coverage_root)
    projects = flatten_ci_dotnet_projects()
    print(
        ".NET CI evidence: "
        f"{len(projects)} projects, "
        f"{sum(counters['passed'] for counters in project_counters)} active tests, "
        f"{sum(counters['skipped'] for counters in project_counters)} excluded skips, "
        f"{sum(counters['total'] for counters in project_counters)} discovered, "
        "Golden 18/18."
    )


def verify_windows_process_orchestration(log_path: Path | None = None) -> None:
    """Exercise the Windows-only Job Object integration when Python is skipped."""

    run(
        [
            sys.executable,
            "-m",
            "unittest",
            WINDOWS_PROCESS_ORCHESTRATION_TEST,
        ],
        log_path=log_path,
    )


def verify_windows_process_orchestration_and_dotnet(
    log_path: Path | None = None,
) -> None:
    """Retain one Windows platform-test owner before the normal .NET lane."""

    verify_windows_process_orchestration(log_path)
    verify_dotnet(log_path)


def parse_lane_timeout(value: str) -> int:
    """Parse the bounded wall-clock budget for one top-level lane."""

    try:
        timeout = int(value)
    except ValueError as error:
        raise argparse.ArgumentTypeError(
            "lane timeout must be an integer number of seconds"
        ) from error
    if not MINIMUM_LANE_TIMEOUT_SECONDS <= timeout <= MAXIMUM_LANE_TIMEOUT_SECONDS:
        raise argparse.ArgumentTypeError(
            "lane timeout must be between "
            f"{MINIMUM_LANE_TIMEOUT_SECONDS} and {MAXIMUM_LANE_TIMEOUT_SECONDS} seconds"
        )
    return timeout


def parse_args(arguments: Sequence[str] | None = None) -> argparse.Namespace:
    raw_arguments = tuple(sys.argv[1:] if arguments is None else arguments)
    parser = argparse.ArgumentParser(description=__doc__, allow_abbrev=False)
    parser.add_argument(
        "--all",
        action="store_true",
        help="Run every public gate. This is the CI/Codex completion command.",
    )
    parser.add_argument(
        "--structure-only",
        action="store_true",
        help="Validate repository structure, contracts, governance, and reference provenance only.",
    )
    parser.add_argument("--skip-python", action="store_true")
    parser.add_argument("--skip-dotnet", action="store_true")
    parser.add_argument("--skip-structure", action="store_true")
    parser.add_argument(
        "--release-golden", action="store_true",
        help="Fresh complete Golden-owner projects on Windows; not a full verifier pass.",
    )
    parser.add_argument(
        "--internal-lane",
        choices=(
            "structure",
            *(name for name, _pattern in REPOSITORY_SCRIPT_TEST_SHARDS),
            "python",
            "dotnet",
            "dotnet-windows",
        ),
        help=argparse.SUPPRESS,
    )
    parser.add_argument(
        "--ci-python-shard",
        choices=(*(name for name, _pattern in REPOSITORY_SCRIPT_TEST_SHARDS), "python"),
        help=argparse.SUPPRESS,
    )
    parser.add_argument(
        "--ci-dotnet-build",
        action="store_true",
        help=argparse.SUPPRESS,
    )
    parser.add_argument(
        "--ci-dotnet-test-shard",
        choices=tuple(CI_DOTNET_SHARDS),
        help=argparse.SUPPRESS,
    )
    parser.add_argument(
        "--ci-dotnet-finalize",
        type=Path,
        help=argparse.SUPPRESS,
    )
    parser.add_argument(
        "--jobs",
        type=int,
        choices=range(1, MAXIMUM_VERIFY_JOBS + 1),
        default=DEFAULT_VERIFY_JOBS,
        help=(
            "Maximum concurrent top-level verification lanes "
            f"(1-{MAXIMUM_VERIFY_JOBS}; default: {DEFAULT_VERIFY_JOBS})."
        ),
    )
    parser.add_argument(
        "--lane-timeout-seconds",
        type=parse_lane_timeout,
        default=DEFAULT_LANE_TIMEOUT_SECONDS,
        help=(
            "Wall-clock deadline for each top-level lane "
            f"({MINIMUM_LANE_TIMEOUT_SECONDS}-{MAXIMUM_LANE_TIMEOUT_SECONDS} seconds; "
            f"default: {DEFAULT_LANE_TIMEOUT_SECONDS})."
        ),
    )
    args = parser.parse_args(raw_arguments)
    args.jobs_was_supplied = any(
        argument == "--jobs" or argument.startswith("--jobs=")
        for argument in raw_arguments
    )
    args.lane_timeout_was_supplied = any(
        argument == "--lane-timeout-seconds"
        or argument.startswith("--lane-timeout-seconds=")
        for argument in raw_arguments
    )
    return args


def repository_script_test_shards() -> tuple[tuple[str, str], ...]:
    """Fail closed unless every repository-script test belongs to one shard."""

    test_paths = tuple(sorted(REPOSITORY_SCRIPT_TESTS.rglob("test_*.py")))
    invalid = []
    for path in test_paths:
        relative_path = path.relative_to(REPOSITORY_SCRIPT_TESTS).as_posix()
        if path.parent != REPOSITORY_SCRIPT_TESTS:
            invalid.append(f"{relative_path}=unsupported nesting")
            continue
        if VALID_MODULE_NAME.match(path.name) is None:
            invalid.append(f"{relative_path}=invalid module filename")
            continue
        matches = tuple(
            name
            for name, pattern in REPOSITORY_SCRIPT_TEST_SHARDS
            if fnmatch(path.name, pattern)
        )
        if len(matches) != 1:
            invalid.append(f"{relative_path}={','.join(matches) or '<none>'}")
    if not test_paths or invalid:
        details = "; ".join(invalid) if invalid else "no test_*.py files found"
        raise RuntimeError(
            "repository-script test shards must assign each test_*.py filename "
            f"exactly once: {details}"
        )
    return REPOSITORY_SCRIPT_TEST_SHARDS


def repository_script_test_action(pattern: str) -> LaneAction:
    """Project one discovered repository-script shard onto its existing owner."""

    def run_lane(log_path: Path | None) -> None:
        verify_repository_scripts(log_path, pattern)

    return run_lane


def ci_python_lane(name: str) -> VerificationLane:
    """Select one existing Python owner after validating the complete partition."""

    shards = dict(repository_script_test_shards())
    if name == "python":
        action = verify_python
    elif name in shards:
        action = repository_script_test_action(shards[name])
    else:
        raise ValueError(f"unknown CI Python shard: {name}")
    return VerificationLane(name, action, isolate_action=True)


def selected_lanes(args: argparse.Namespace) -> tuple[VerificationLane, ...]:
    """Resolve each enabled verification owner exactly once in stable order."""

    lanes: list[VerificationLane] = []
    if not args.skip_structure:
        lanes.append(
            VerificationLane("structure", verify_structure, isolate_action=True)
        )
    if not args.structure_only:
        if not args.skip_python:
            for name, pattern in repository_script_test_shards():
                lanes.append(
                    VerificationLane(
                        name,
                        repository_script_test_action(pattern),
                        isolate_action=True,
                    )
                )
            lanes.append(VerificationLane("python", verify_python, isolate_action=True))
        if not args.skip_dotnet:
            internal_name = (
                "dotnet-windows"
                if sys.platform == "win32" and args.skip_python
                else None
            )
            lanes.append(
                VerificationLane(
                    "dotnet",
                    verify_dotnet,
                    isolate_action=True,
                    internal_name=internal_name,
                )
            )
    return tuple(lanes)


def run_internal_lane(name: str) -> None:
    """Run one public lane directly inside the parent-owned lane process tree."""

    actions: dict[str, LaneAction] = {
        "structure": verify_structure,
        "python": verify_python,
        "dotnet": verify_dotnet,
        "dotnet-windows": verify_windows_process_orchestration_and_dotnet,
    }
    actions.update(
        {
            shard_name: repository_script_test_action(pattern)
            for shard_name, pattern in repository_script_test_shards()
        }
    )
    actions[name](None)


def run_isolated_lane(name: str, log_path: Path) -> None:
    """Give a whole lane one terminable process boundary and wall-clock deadline."""

    environment = os.environ.copy()
    environment[INTERNAL_LANE_ENVIRONMENT_VARIABLE] = "1"
    run(
        [sys.executable, str(Path(__file__).resolve()), "--internal-lane", name],
        environment=environment,
        log_path=log_path,
        graceful_termination_seconds=UNIX_LANE_GRACEFUL_TERMINATION_SECONDS,
    )


def run_lanes(
    lanes: Sequence[VerificationLane],
    *,
    jobs: int,
    log_directory: Path,
    lane_timeout_seconds: float = DEFAULT_LANE_TIMEOUT_SECONDS,
    preserve_cancellation_request: bool = False,
) -> tuple[LaneResult, ...]:
    """Run independent lanes once, preserving declared result order and isolated logs."""

    if not 1 <= jobs <= MAXIMUM_VERIFY_JOBS:
        raise ValueError(
            f"verification jobs must be between 1 and {MAXIMUM_VERIFY_JOBS}"
        )
    if lane_timeout_seconds <= 0:
        raise ValueError("verification lane timeout must be positive")
    names = tuple(lane.name for lane in lanes)
    if len(names) != len(set(names)):
        raise ValueError("verification lane names must be unique")
    log_directory.mkdir(parents=True, exist_ok=True)

    def run_lane(lane: VerificationLane) -> LaneResult:
        log_path = log_directory / f"{lane.name}.log"
        started = monotonic()
        deadline_token = LANE_DEADLINE.set(started + lane_timeout_seconds)
        try:
            if lane.isolate_action:
                run_isolated_lane(lane.internal_name or lane.name, log_path)
            else:
                lane.action(log_path)
            remaining_timeout()
        except Exception as error:
            return LaneResult(
                lane.name,
                False,
                monotonic() - started,
                log_path,
                f"{type(error).__name__}: {error}",
            )
        finally:
            LANE_DEADLINE.reset(deadline_token)
        return LaneResult(lane.name, True, monotonic() - started, log_path)

    if jobs == 1 or len(lanes) < 2:
        PROCESS_CANCELLATION_REQUESTED.clear()
        try:
            return tuple(run_lane(lane) for lane in lanes)
        except KeyboardInterrupt:
            cancel_active_processes_after_handoffs()
            raise
        finally:
            if not preserve_cancellation_request:
                PROCESS_CANCELLATION_REQUESTED.clear()

    PROCESS_CANCELLATION_REQUESTED.clear()
    results: dict[str, LaneResult] = {}
    executor = ThreadPoolExecutor(max_workers=min(jobs, len(lanes)))
    futures = {}
    try:
        for lane in lanes:
            futures[executor.submit(run_lane, lane)] = lane.name
        for future in as_completed(futures):
            result = future.result()
            results[result.name] = result
    except KeyboardInterrupt:
        cleanup_error: Exception | None = None
        try:
            cancel_active_processes_after_handoffs()
        except Exception as error:
            cleanup_error = error
        finally:
            for future in futures:
                future.cancel()
            executor.shutdown(wait=True, cancel_futures=True)
        if cleanup_error is not None:
            raise cleanup_error
        raise
    except BaseException:
        for future in futures:
            future.cancel()
        executor.shutdown(wait=True, cancel_futures=True)
        raise
    else:
        executor.shutdown(wait=True)
        return tuple(results[lane.name] for lane in lanes)
    finally:
        if not preserve_cancellation_request:
            PROCESS_CANCELLATION_REQUESTED.clear()


def report_lane_results(results: Sequence[LaneResult]) -> None:
    """Emit isolated lane logs and a stable aggregate verdict in declaration order."""

    for result in results:
        print(f"\n=== {result.name} lane ({result.duration_seconds:.1f}s) ===")
        if result.log_path.is_file():
            stream_log_tail(
                result.log_path,
                start_offset=0,
                echo=True,
            )
        if not result.succeeded:
            print(f"[lane failed] {result.error}")
    summary = ", ".join(
        f"{result.name}={'PASS' if result.succeeded else 'FAIL'} ({result.duration_seconds:.1f}s)"
        for result in results
    )
    print(f"\nVerification lane summary: {summary}")


def run_selected_lanes(
    lanes: Sequence[VerificationLane], *, jobs: int, lane_timeout_seconds: int
) -> None:
    """Use temporary isolated logs and fail only after every selected lane reports."""

    if not lanes:
        raise RuntimeError("verification plan selected no lanes")
    with tempfile.TemporaryDirectory(prefix="nfc-verify-") as temporary:
        print(
            "Verification policy: "
            f"jobs={jobs}, lane-timeout={lane_timeout_seconds}s, "
            f"cleanup-ceiling={CLEANUP_TIMEOUT_SECONDS}s"
        )
        results = run_lanes(
            lanes,
            jobs=jobs,
            log_directory=Path(temporary),
            lane_timeout_seconds=lane_timeout_seconds,
        )
        report_lane_results(results)
    failures = [result.name for result in results if not result.succeeded]
    if failures:
        raise RuntimeError(f"verification lanes failed: {', '.join(failures)}")


def validate_internal_lane_arguments(args: argparse.Namespace) -> None:
    """Reject public verifier policy from the parent-owned lane entry point."""

    if (
        args.all
        or args.release_golden
        or args.structure_only
        or args.skip_structure
        or args.skip_python
        or args.skip_dotnet
        or args.ci_dotnet_build
        or args.ci_python_shard is not None
        or args.ci_dotnet_test_shard is not None
        or args.ci_dotnet_finalize is not None
        or args.jobs_was_supplied
        or args.lane_timeout_was_supplied
        or args.jobs != DEFAULT_VERIFY_JOBS
        or args.lane_timeout_seconds != DEFAULT_LANE_TIMEOUT_SECONDS
    ):
        raise SystemExit(
            "--internal-lane cannot be combined with public verification flags"
        )
    if os.environ.get(INTERNAL_LANE_ENVIRONMENT_VARIABLE) != "1":
        raise SystemExit("--internal-lane requires a parent-owned process marker")


def execute_verification(args: argparse.Namespace) -> int:
    """Validate one parsed invocation inside an installed signal boundary."""

    ci_modes = tuple(
        selected
        for selected in (
            args.ci_dotnet_build,
            args.release_golden,
            args.ci_python_shard is not None,
            args.ci_dotnet_test_shard is not None,
            args.ci_dotnet_finalize is not None,
        )
        if selected
    )
    if len(ci_modes) > 1:
        raise SystemExit("CI modes cannot be combined")
    if ci_modes:
        if (
            args.all
            or args.structure_only
            or args.skip_structure
            or args.skip_python
            or args.skip_dotnet
            or args.internal_lane is not None
            or args.jobs_was_supplied
            or args.lane_timeout_was_supplied
        ):
            raise SystemExit(
                "CI modes cannot be combined with public verification flags"
            )
        try:
            if args.ci_python_shard is not None:
                run_selected_lanes(
                    (ci_python_lane(args.ci_python_shard),),
                    jobs=1,
                    lane_timeout_seconds=DEFAULT_LANE_TIMEOUT_SECONDS,
                )
            elif args.release_golden:
                verify_dotnet(release_golden=True)
            elif args.ci_dotnet_build:
                verify_ci_dotnet_build()
            elif args.ci_dotnet_test_shard is not None:
                verify_ci_dotnet_test_shard(args.ci_dotnet_test_shard)
            else:
                assert args.ci_dotnet_finalize is not None
                finalize_ci_dotnet_evidence(args.ci_dotnet_finalize)
        except (
            RuntimeError,
            ValueError,
            OSError,
            subprocess.CalledProcessError,
        ) as exc:
            print(f"\nVERIFICATION FAILED: {exc}", file=sys.stderr)
            return 1
        print("\nRelease Golden verification passed (not full verification)."
              if args.release_golden else "\nVerification passed.")
        return 0

    if args.internal_lane:
        validate_internal_lane_arguments(args)
        run_internal_lane(args.internal_lane)
        return 0
    if os.environ.get(INTERNAL_LANE_ENVIRONMENT_VARIABLE) == "1":
        raise SystemExit("parent-owned process marker is reserved for --internal-lane")
    structure_only = args.structure_only
    if args.all and (args.skip_structure or args.skip_python or args.skip_dotnet):
        raise SystemExit("--all cannot be combined with skip flags")
    if structure_only and (
        args.all or args.skip_structure or args.skip_python or args.skip_dotnet
    ):
        raise SystemExit(
            "--structure-only cannot be combined with other selection flags"
        )

    try:
        lanes = selected_lanes(args)
        if not lanes:
            raise RuntimeError("verification plan selected no lanes")
        for lane in lanes:
            run_selected_lanes(
                (lane,),
                jobs=args.jobs,
                lane_timeout_seconds=args.lane_timeout_seconds,
            )
    except (RuntimeError, ValueError, subprocess.CalledProcessError) as exc:
        print(f"\nVERIFICATION FAILED: {exc}", file=sys.stderr)
        return 1

    print("\nVerification passed.")
    return 0


def main() -> int:
    args = parse_args()
    if args.internal_lane is not None:
        validate_internal_lane_arguments(args)
    try:
        with handle_external_termination():
            with verification_test_session(
                internal_lane=args.internal_lane is not None
            ):
                return execute_verification(args)
    except VerificationTerminationRequested as error:
        return 128 + error.signal_number
    except (RuntimeError, ValueError, OSError, subprocess.CalledProcessError) as error:
        print(f"\nVERIFICATION FAILED: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

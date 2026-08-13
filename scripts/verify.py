"""Canonical cross-platform verification entry point for NFC and Codex."""

from __future__ import annotations

import argparse
import codecs
import ctypes
import hashlib
import importlib.util
import json
import os
import re
import shutil
import signal
import stat
import subprocess
import sys
import tempfile
import threading
import xml.etree.ElementTree as ET
from collections.abc import Callable, Sequence
from concurrent.futures import ThreadPoolExecutor, as_completed
from contextlib import ExitStack, contextmanager
from contextvars import ContextVar
from ctypes import wintypes
from dataclasses import dataclass
from importlib import metadata as importlib_metadata
from pathlib import Path, PurePosixPath
from time import monotonic

if __package__:
    from .coverage_policy import (
        load_baseline,
        repository_relative_coverage_source,
        verify_coverage,
    )
else:
    from coverage_policy import (
        load_baseline,
        repository_relative_coverage_source,
        verify_coverage,
    )

ROOT = Path(__file__).resolve().parents[1]
WORKER_ROOT = ROOT / "tools" / "crc-worker"
SOLUTION = ROOT / "NvtFwCombiner.slnx"
CTRL_RAM_REPLACE_FIXTURE_VERIFIER = (
    ROOT / "scripts" / "verify_ctrlram_replace_fixture.py"
)
CTRL_RAM_SENTINEL_CREATOR = ROOT / "scripts" / "create_ctrlram_universal_sentinel.py"
IDLE_BUILD_WORKER_STOPPER = ROOT / "scripts" / "stop-idle-build-workers.ps1"
REPOSITORY_SCRIPT_TESTS = ROOT / "tests" / "scripts"
COVERAGE_ROOT = ROOT / "artifacts" / "coverage"
CI_DOTNET_EVIDENCE_ROOT = ROOT / "artifacts" / "ci-dotnet-work"
CI_DOTNET_UPLOAD_ROOT = ROOT / "artifacts" / "ci-dotnet-upload"
WINDOWS_PROCESS_ORCHESTRATION_TEST = (
    "tests.scripts.test_verify_orchestration."
    "VerifyOrchestrationTests.test_windows_owned_job_kills_descendants_after_root_exit"
)
DEFAULT_VERIFY_JOBS = 3
MAXIMUM_VERIFY_JOBS = 3
DEFAULT_LANE_TIMEOUT_SECONDS = 600
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
PYTHON_COVERAGE_OVERRIDE_ENVIRONMENT_VARIABLES = (
    "PYTEST_ADDOPTS",
    "COVERAGE_RCFILE",
    "COVERAGE_PROCESS_START",
)
CI_DOTNET_EVIDENCE_SCHEMA_VERSION = 1
CI_SOURCE_SHA_PATTERN = re.compile(r"[0-9a-f]{40}")


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
    expected_total: int
    expected_skipped: int = 0

    @property
    def name(self) -> str:
        return Path(self.relative_path).stem


CI_DOTNET_SHARDS: dict[str, tuple[CiDotnetProject, ...]] = {
    "bootstrap": (
        CiDotnetProject(
            "tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj",
            932,
        ),
    ),
    "ui": (
        CiDotnetProject(
            "tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj",
            469,
        ),
    ),
    "core": (
        CiDotnetProject(
            "tests/NvtFwCombiner.Domain.Tests/NvtFwCombiner.Domain.Tests.csproj",
            404,
        ),
        CiDotnetProject(
            "tests/NvtFwCombiner.Application.Tests/"
            "NvtFwCombiner.Application.Tests.csproj",
            527,
        ),
        CiDotnetProject(
            "tests/NvtFwCombiner.Infrastructure.Tests/"
            "NvtFwCombiner.Infrastructure.Tests.csproj",
            421,
            2,
        ),
        CiDotnetProject(
            "tests/NvtFwCombiner.ProfileContract.Tests/"
            "NvtFwCombiner.ProfileContract.Tests.csproj",
            387,
        ),
        CiDotnetProject(
            "tests/NvtFwCombiner.GoldenRegression.Tests/"
            "NvtFwCombiner.GoldenRegression.Tests.csproj",
            17,
        ),
        CiDotnetProject(
            "tests/NvtFwCombiner.Architecture.Tests/"
            "NvtFwCombiner.Architecture.Tests.csproj",
            214,
        ),
    ),
}


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

    if threading.current_thread() is not threading.main_thread():
        yield
        return

    def request_termination(signal_number: int, _frame: object) -> None:
        PROCESS_CANCELLATION_REQUESTED.set()
        raise VerificationTerminationRequested(signal_number)

    previous_handler = signal.signal(signal.SIGTERM, request_termination)
    try:
        yield
    finally:
        signal.signal(signal.SIGTERM, previous_handler)


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
                sys.stdout.write(decoder.decode(chunk))
        if decoder is not None:
            sys.stdout.write(decoder.decode(b"", final=True))


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
    run([sys.executable, "scripts/validate_repository.py"], log_path=log_path)
    run([sys.executable, "scripts/polytail_check.py"], log_path=log_path)
    run(
        [sys.executable, str(CTRL_RAM_SENTINEL_CREATOR), "--dry-run"],
        log_path=log_path,
    )


def verify_repository_scripts(log_path: Path | None = None) -> None:
    run(
        [
            sys.executable,
            "-m",
            "unittest",
            "discover",
            "-s",
            str(REPOSITORY_SCRIPT_TESTS),
            "-p",
            "test_*.py",
        ],
        log_path=log_path,
    )


def validated_disposable_directory(directory: Path, root: Path) -> Path:
    """Resolve a disposable directory without following repository-internal links."""

    lexical_root = root.absolute()
    lexical_directory = directory.absolute()
    try:
        relative = lexical_directory.relative_to(lexical_root)
    except ValueError as error:
        raise RuntimeError(
            f"refusing to delete a directory outside the repository: {directory}"
        ) from error
    if not relative.parts:
        raise RuntimeError("refusing to delete the repository root")

    candidate = lexical_root
    for part in relative.parts:
        candidate /= part
        is_junction = getattr(candidate, "is_junction", None)
        if candidate.is_symlink() or (is_junction is not None and is_junction()):
            raise RuntimeError(
                f"refusing to delete through a symbolic link or junction: {candidate}"
            )

    resolved_root = lexical_root.resolve()
    resolved_directory = lexical_directory.resolve(strict=False)
    try:
        resolved_directory.relative_to(resolved_root)
    except ValueError as error:
        raise RuntimeError(
            f"refusing to delete a directory outside the repository: {directory}"
        ) from error
    return lexical_directory


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
    environment["MSBUILDDISABLENODEREUSE"] = "1"
    environment["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1"
    return environment


def dotnet_build_commands(dotnet: str) -> tuple[list[str], ...]:
    """Return the one canonical restore, ownership, format, and Release build plan."""

    return (
        [dotnet, "--version"],
        [dotnet, "restore", str(SOLUTION)],
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
        [dotnet, "build", str(SOLUTION), "-c", "Release", "--no-restore"],
    )


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


def verify_dotnet(log_path: Path | None = None) -> None:
    dotnet = resolve_dotnet()
    coverage_directory = reset_coverage_directory("dotnet")
    environment = dotnet_batch_environment()
    commands = (
        *dotnet_build_commands(dotnet),
        [
            dotnet,
            "test",
            str(SOLUTION),
            "-c",
            "Release",
            "--no-build",
            "--collect:XPlat Code Coverage",
            "--results-directory",
            str(coverage_directory),
            "--",
            "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=json,cobertura",
        ],
        # The full solution test command already runs CtrlRAM UI smoke tests. Keep the
        # fixture gate here for manifest and payload-hash validation only.
        [sys.executable, str(CTRL_RAM_REPLACE_FIXTURE_VERIFIER), "--skip-public-smoke"],
    )
    try:
        run_dotnet_commands(commands, environment=environment, log_path=log_path)
        verify_coverage("dotnet", coverage_directory)
    finally:
        # Avalonia/Roslyn may start compiler servers even with node reuse disabled.
        # Stop only servers from the repository-selected SDK after every verification run.
        cleanup_dotnet_batch(dotnet, environment, log_path)


def ci_dotnet_test_command(
    dotnet: str,
    project: CiDotnetProject,
    results_directory: Path,
) -> list[str]:
    """Build one unfiltered project test command with paired coverage and TRX evidence."""

    return [
        dotnet,
        "test",
        str(ROOT / project.relative_path),
        "-c",
        "Release",
        "--no-restore",
        "--collect:XPlat Code Coverage",
        "--results-directory",
        str(results_directory),
        "--logger",
        "trx;LogFileName=test-results.trx",
        "--",
        "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=json,cobertura",
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


def reset_ci_dotnet_evidence_directory(directory: Path) -> Path:
    target = validated_disposable_directory(directory, ROOT)
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


def is_ci_evidence_reparse_point(path: Path) -> bool:
    is_junction = getattr(path, "is_junction", None)
    return path.is_symlink() or (is_junction is not None and is_junction())


def require_ci_regular_file(path: Path, evidence_root: Path) -> Path:
    lexical_root = evidence_root.absolute()
    lexical_path = path.absolute()
    try:
        relative = lexical_path.relative_to(lexical_root)
    except ValueError as error:
        raise RuntimeError(f".NET CI evidence path escapes its root: {path}") from error
    current = lexical_root
    if is_ci_evidence_reparse_point(current):
        raise RuntimeError(f"reparse-point .NET CI evidence is forbidden: {current}")
    for part in relative.parts:
        current /= part
        if is_ci_evidence_reparse_point(current):
            raise RuntimeError(
                f"reparse-point .NET CI evidence is forbidden: {current}"
            )
    try:
        mode = current.stat(follow_symlinks=False).st_mode
    except OSError as error:
        raise RuntimeError(
            f"missing .NET CI evidence file: {relative.as_posix()}"
        ) from error
    if not stat.S_ISREG(mode):
        raise RuntimeError(f"non-regular .NET CI evidence is forbidden: {current}")
    return current


def enumerate_ci_regular_files(directory: Path) -> tuple[Path, ...]:
    root = directory.absolute()
    if not root.is_dir() or is_ci_evidence_reparse_point(root):
        raise RuntimeError(f"invalid .NET CI evidence directory: {directory}")
    files: list[Path] = []
    for current_text, directory_names, file_names in os.walk(
        root,
        topdown=True,
        followlinks=False,
    ):
        current = Path(current_text)
        for name in directory_names:
            child = current / name
            if is_ci_evidence_reparse_point(child) or not child.is_dir():
                raise RuntimeError(
                    f"reparse-point .NET CI evidence is forbidden: {child}"
                )
        for name in file_names:
            files.append(require_ci_regular_file(current / name, root))
    return tuple(sorted(files))


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
        if not path.exists() and not is_ci_evidence_reparse_point(path):
            continue
        regular = require_ci_regular_file(path, evidence_root)
        hashes[ci_relative_path(regular, evidence_root)] = sha256_file(regular)
    return hashes


def publish_ci_dotnet_artifact(
    source_root: Path,
    upload_root: Path,
    manifest_relative_path: str,
    file_hashes: dict[str, str],
) -> None:
    """Copy only declared regular evidence into a clean upload tree."""

    expected_paths = {manifest_relative_path, *file_hashes}
    target = reset_ci_dotnet_evidence_directory(upload_root)
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


def collect_ci_project_evidence(
    project: CiDotnetProject,
    results_directory: Path,
    evidence_root: Path,
) -> tuple[dict[str, object], tuple[Path, ...]]:
    regular_files = enumerate_ci_regular_files(results_directory)
    trx_reports = tuple(
        path for path in regular_files if path.name == "test-results.trx"
    )
    json_reports = tuple(path for path in regular_files if path.name == "coverage.json")
    cobertura_reports = tuple(
        path for path in regular_files if path.name == "coverage.cobertura.xml"
    )
    if (
        len(trx_reports) != 1
        or not json_reports
        or {report.parent for report in json_reports}
        != {report.parent for report in cobertura_reports}
    ):
        raise RuntimeError(
            f"{project.name} must emit exactly one TRX and one paired coverage report"
        )
    json_hashes = {sha256_file(report) for report in json_reports}
    cobertura_hashes = {sha256_file(report) for report in cobertura_reports}
    if len(json_hashes) != 1 or len(cobertura_hashes) != 1:
        raise RuntimeError(f"{project.name} emitted divergent coverage attachments")
    canonical_parent = min(
        (report.parent for report in json_reports),
        key=lambda parent: len(parent.relative_to(results_directory).parts),
    )
    for report in (*json_reports, *cobertura_reports):
        if report.parent != canonical_parent:
            report.unlink()
    json_report = canonical_parent / "coverage.json"
    cobertura_report = canonical_parent / "coverage.cobertura.xml"
    normalize_ci_dotnet_coverage_reports(json_report, cobertura_report)

    counters = parse_trx_counters(trx_reports[0])
    expected_passed = project.expected_total - project.expected_skipped
    expected = {
        "total": project.expected_total,
        "passed": expected_passed,
        "failed": 0,
        "skipped": project.expected_skipped,
    }
    if counters != expected:
        raise RuntimeError(
            f"{project.name} test counters changed: expected {expected}, observed {counters}"
        )

    evidence_paths = (trx_reports[0], json_report, cobertura_report)
    return (
        {
            "relativePath": project.relative_path,
            "total": counters["total"],
            "passed": counters["passed"],
            "failed": counters["failed"],
            "skipped": counters["skipped"],
            "trx": ci_relative_path(trx_reports[0], evidence_root),
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

    evidence_root = reset_ci_dotnet_evidence_directory(CI_DOTNET_EVIDENCE_ROOT)
    reset_ci_dotnet_evidence_directory(CI_DOTNET_UPLOAD_ROOT)
    output = evidence_root / "build"
    output.mkdir(parents=True)
    log_path = output / "build.log"
    dotnet = resolve_dotnet()
    environment = dotnet_batch_environment()
    failure: BaseException | None = None
    try:
        verify_windows_process_orchestration(log_path)
        run_dotnet_commands(
            dotnet_build_commands(dotnet),
            environment=environment,
            log_path=log_path,
        )
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
    evidence_root = reset_ci_dotnet_evidence_directory(CI_DOTNET_EVIDENCE_ROOT)
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
        run(
            [dotnet, "restore", str(SOLUTION)],
            environment=environment,
            log_path=log_path,
        )
        for project in projects:
            results_directory = results_root / project.name
            results_directory.mkdir(parents=True)
            try:
                run(
                    ci_dotnet_test_command(dotnet, project, results_directory),
                    environment=environment,
                    log_path=log_path,
                )
            except subprocess.CalledProcessError as error:
                failures.append(f"{project.name}: {error}")
            else:
                try:
                    row, paths = collect_ci_project_evidence(
                        project,
                        results_directory,
                        evidence_root,
                    )
                except (RuntimeError, ValueError) as error:
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
    if not root.is_dir() or is_ci_evidence_reparse_point(root):
        raise RuntimeError(f"missing or invalid .NET CI evidence root: {download_root}")
    entries = {path.name: path for path in root.iterdir()}
    if set(entries) != set(expected_names.values()):
        raise RuntimeError("missing or extra .NET CI evidence producer artifacts")
    artifact_roots: dict[str, Path] = {}
    for owner, name in expected_names.items():
        artifact_root = entries[name]
        if is_ci_evidence_reparse_point(artifact_root) or not artifact_root.is_dir():
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
) -> tuple[Path, Path, Path]:
    raw_trx = row["trx"]
    raw_json = row["coverageJson"]
    raw_cobertura = row["coverageCobertura"]
    if (
        not isinstance(raw_trx, str)
        or not isinstance(raw_json, str)
        or not isinstance(raw_cobertura, str)
    ):
        raise RuntimeError(f"{project.name} .NET CI evidence paths are invalid")
    trx_path = PurePosixPath(raw_trx)
    json_path = PurePosixPath(raw_json)
    cobertura_path = PurePosixPath(raw_cobertura)
    project_root = PurePosixPath("shards", shard, "results", project.name)
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
    row_paths = {raw_trx, raw_json, raw_cobertura}
    if len(row_paths) != 3 or seen_paths.intersection(row_paths):
        raise RuntimeError(f"{project.name} reuses .NET CI evidence")
    seen_paths.update(row_paths)
    return (
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
                    "trx",
                    "coverageJson",
                    "coverageCobertura",
                },
                project.name,
            )
            expected_row = {
                **row,
                "relativePath": project.relative_path,
                "total": project.expected_total,
                "passed": project.expected_total - project.expected_skipped,
                "failed": 0,
                "skipped": project.expected_skipped,
            }
            if row != expected_row:
                raise RuntimeError(f"{project.name} .NET CI test counters changed")
            trx, json_report, cobertura_report = require_ci_project_evidence_paths(
                row,
                shard,
                project,
                artifact_root,
                seen_row_paths,
            )
            evidence_paths = {
                row["trx"],
                row["coverageJson"],
                row["coverageCobertura"],
            }
            expected_paths.update(evidence_paths)
            counters = parse_trx_counters(trx)
            if counters != {
                "total": project.expected_total,
                "passed": project.expected_total - project.expected_skipped,
                "failed": 0,
                "skipped": project.expected_skipped,
            }:
                raise RuntimeError(f"{project.name} TRX counters changed")
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
    run([sys.executable, str(CTRL_RAM_REPLACE_FIXTURE_VERIFIER), "--skip-public-smoke"])
    verify_coverage("dotnet", coverage_root)
    print(".NET CI evidence: 8 projects, 3371 tests, 2 skips, Golden 17/17.")


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
        "--internal-lane",
        choices=("structure", "python", "dotnet", "dotnet-windows"),
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


def repository_script_and_python_lane() -> LaneAction:
    """Return the sole owner for repository-script and Python verification."""

    def run_lane(log_path: Path | None) -> None:
        verify_repository_scripts(log_path)
        verify_python(log_path)

    return run_lane


def selected_lanes(args: argparse.Namespace) -> tuple[VerificationLane, ...]:
    """Resolve each enabled verification owner exactly once in stable order."""

    lanes: list[VerificationLane] = []
    if not args.skip_structure:
        lanes.append(
            VerificationLane("structure", verify_structure, isolate_action=True)
        )
    if not args.structure_only:
        if not args.skip_python:
            lanes.append(
                VerificationLane(
                    "python", repository_script_and_python_lane(), isolate_action=True
                )
            )
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
        "python": repository_script_and_python_lane(),
        "dotnet": verify_dotnet,
        "dotnet-windows": verify_windows_process_orchestration_and_dotnet,
    }
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
        or args.structure_only
        or args.skip_structure
        or args.skip_python
        or args.skip_dotnet
        or args.ci_dotnet_build
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
            args.ci_dotnet_test_shard is not None,
            args.ci_dotnet_finalize is not None,
        )
        if selected
    )
    if len(ci_modes) > 1:
        raise SystemExit("CI .NET modes cannot be combined")
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
                "CI .NET modes cannot be combined with public verification flags"
            )
        try:
            if args.ci_dotnet_build:
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
        print("\nVerification passed.")
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
        run_selected_lanes(
            selected_lanes(args),
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
    try:
        with handle_external_termination():
            return execute_verification(args)
    except VerificationTerminationRequested as error:
        return 128 + error.signal_number


if __name__ == "__main__":
    raise SystemExit(main())

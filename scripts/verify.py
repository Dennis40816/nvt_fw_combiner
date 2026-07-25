"""Canonical cross-platform verification entry point for NFC and Codex."""

from __future__ import annotations

import argparse
import importlib.util
import os
import shutil
import signal
import subprocess
import sys
import tempfile
import threading
from collections.abc import Callable, Sequence
from concurrent.futures import ThreadPoolExecutor, as_completed
from contextvars import ContextVar
from dataclasses import dataclass
from pathlib import Path
from time import monotonic

ROOT = Path(__file__).resolve().parents[1]
WORKER_ROOT = ROOT / "tools" / "crc-worker"
SOLUTION = ROOT / "NvtFwCombiner.slnx"
CTRL_RAM_REPLACE_FIXTURE_VERIFIER = (
    ROOT / "scripts" / "verify_ctrlram_replace_fixture.py"
)
CTRL_RAM_SENTINEL_CREATOR = ROOT / "scripts" / "create_ctrlram_universal_sentinel.py"
IDLE_BUILD_WORKER_STOPPER = ROOT / "scripts" / "stop-idle-build-workers.ps1"
REPOSITORY_SCRIPT_TESTS = ROOT / "tests" / "scripts"
DEFAULT_VERIFY_JOBS = 3
MAXIMUM_VERIFY_JOBS = 3
DEFAULT_LANE_TIMEOUT_SECONDS = 300
MINIMUM_LANE_TIMEOUT_SECONDS = 60
MAXIMUM_LANE_TIMEOUT_SECONDS = 900
CLEANUP_TIMEOUT_SECONDS = 30
PROCESS_TERMINATION_TIMEOUT_SECONDS = 5
LANE_DEADLINE: ContextVar[float | None] = ContextVar(
    "verification_lane_deadline", default=None
)
CLEANUP_DEADLINE: ContextVar[float | None] = ContextVar(
    "verification_cleanup_deadline", default=None
)
INTERNAL_LANE_ENVIRONMENT_VARIABLE = "NFC_VERIFY_INTERNAL_LANE"
ACTIVE_PROCESSES: set[subprocess.Popen[bytes]] = set()
ACTIVE_PROCESSES_LOCK = threading.Lock()


LaneAction = Callable[[Path | None], None]


@dataclass(frozen=True)
class VerificationLane:
    """One independently executable owner in the canonical verification plan."""

    name: str
    action: LaneAction
    isolate_action: bool = False


@dataclass(frozen=True)
class LaneResult:
    """Stable outcome and isolated output location for one verification lane."""

    name: str
    succeeded: bool
    duration_seconds: float
    log_path: Path
    error: str | None = None


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


def terminate_process_tree(process: subprocess.Popen[bytes]) -> None:
    """Terminate a timed-out verifier command together with every descendant."""

    if process.poll() is not None:
        return
    discovery_error: RuntimeError | None = None
    if sys.platform == "win32":
        try:
            subprocess.run(
                ["taskkill", "/PID", str(process.pid), "/T", "/F"],
                check=False,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                timeout=PROCESS_TERMINATION_TIMEOUT_SECONDS,
            )
        except subprocess.TimeoutExpired:
            process.kill()
    else:
        try:
            descendant_process_ids = unix_descendant_process_ids(process.pid)
        except RuntimeError as error:
            descendant_process_ids = ()
            discovery_error = error
        for process_id in descendant_process_ids:
            try:
                os.kill(process_id, signal.SIGKILL)
            except ProcessLookupError:
                pass
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            process.kill()
    try:
        process.wait(timeout=PROCESS_TERMINATION_TIMEOUT_SECONDS)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=PROCESS_TERMINATION_TIMEOUT_SECONDS)
    if sys.platform != "win32" and discovery_error is not None:
        raise discovery_error


def unix_descendant_process_ids(root_process_id: int) -> tuple[int, ...]:
    """Return a Unix process tree in leaf-first order without a runtime dependency."""

    try:
        process_table = subprocess.run(
            ["ps", "-eo", "pid=,ppid="],
            check=False,
            capture_output=True,
            text=True,
            timeout=PROCESS_TERMINATION_TIMEOUT_SECONDS,
        )
    except OSError as error:
        raise RuntimeError("unable to inspect Unix verification process tree") from error
    except subprocess.TimeoutExpired as error:
        raise RuntimeError("Unix verification process-tree inspection timed out") from error
    if process_table.returncode != 0:
        raise RuntimeError(
            "Unix verification process-tree inspection failed with exit code "
            f"{process_table.returncode}"
        )

    children_by_parent: dict[int, list[int]] = {}
    observed_process_ids: set[int] = set()
    for row in process_table.stdout.splitlines():
        parts = row.split()
        if len(parts) != 2:
            continue
        try:
            process_id, parent_process_id = (int(part) for part in parts)
        except ValueError:
            continue
        observed_process_ids.add(process_id)
        children_by_parent.setdefault(parent_process_id, []).append(process_id)
    if root_process_id not in observed_process_ids:
        raise RuntimeError(
            "Unix verification process-tree inspection did not include the root process"
        )

    descendants: list[int] = []
    pending = [root_process_id]
    while pending:
        children = children_by_parent.get(pending.pop(), [])
        descendants.extend(children)
        pending.extend(children)
    return tuple(reversed(descendants))


def terminate_active_processes() -> None:
    """Terminate every detached command currently owned by interrupted verification."""

    with ACTIVE_PROCESSES_LOCK:
        active_processes = tuple(ACTIVE_PROCESSES)
    for process in active_processes:
        terminate_process_tree(process)


def process_group_options() -> dict[str, int] | dict[str, bool]:
    """Keep internal lane commands inside their parent process tree for cancellation."""

    if os.environ.get(INTERNAL_LANE_ENVIRONMENT_VARIABLE) == "1":
        return {}
    if sys.platform == "win32":
        return {"creationflags": subprocess.CREATE_NEW_PROCESS_GROUP}
    return {"start_new_session": True}


def register_active_process(process: subprocess.Popen[bytes]) -> None:
    with ACTIVE_PROCESSES_LOCK:
        ACTIVE_PROCESSES.add(process)


def unregister_active_process(process: subprocess.Popen[bytes]) -> None:
    with ACTIVE_PROCESSES_LOCK:
        ACTIVE_PROCESSES.discard(process)


def run(
    command: list[str],
    *,
    cwd: Path = ROOT,
    environment: dict[str, str] | None = None,
    log_path: Path | None = None,
    mirror_log_path: Path | None = None,
    timeout_seconds: float | None = None,
) -> None:
    if log_path is None:
        print(f"\n> {' '.join(command)}", flush=True)
        process = subprocess.Popen(
            command,
            cwd=cwd,
            env=environment,
            **process_group_options(),
        )
        register_active_process(process)
        try:
            return_code = process.wait(timeout=remaining_timeout(timeout_seconds))
        except (subprocess.TimeoutExpired, KeyboardInterrupt):
            terminate_process_tree(process)
            raise
        finally:
            unregister_active_process(process)
        if return_code != 0:
            raise subprocess.CalledProcessError(return_code, command)
        return

    _run_to_logs(
        command,
        log_paths=(log_path, mirror_log_path),
        cwd=cwd,
        environment=environment,
        echo=False,
        timeout_seconds=timeout_seconds,
    )


def _run_to_logs(
    command: list[str],
    *,
    log_paths: tuple[Path | None, ...],
    cwd: Path,
    environment: dict[str, str] | None,
    echo: bool,
    timeout_seconds: float | None = None,
) -> None:
    """Run one command while writing identical output to one or two log files."""

    unique_paths = tuple(dict.fromkeys(path for path in log_paths if path is not None))
    if not unique_paths:
        raise ValueError("captured verification command requires at least one log path")
    primary_path = unique_paths[0]
    primary_path.parent.mkdir(parents=True, exist_ok=True)
    start_offset = primary_path.stat().st_size if primary_path.exists() else 0
    try:
        with primary_path.open("a", encoding="utf-8", newline="\n") as primary:
            print(f"\n> {' '.join(command)}", file=primary, flush=True)
            process = subprocess.Popen(
                command,
                cwd=cwd,
                stdout=primary,
                stderr=subprocess.STDOUT,
                env=environment,
                **process_group_options(),
            )
            register_active_process(process)
            try:
                return_code = process.wait(timeout=remaining_timeout(timeout_seconds))
            except (subprocess.TimeoutExpired, KeyboardInterrupt):
                terminate_process_tree(process)
                raise
            finally:
                unregister_active_process(process)
        output = primary_path.read_bytes()[start_offset:]
        for mirror_path in unique_paths[1:]:
            mirror_path.parent.mkdir(parents=True, exist_ok=True)
            with mirror_path.open("ab") as mirror:
                mirror.write(output)
        if echo:
            print(output.decode("utf-8", errors="replace"), end="")
        if return_code != 0:
            raise subprocess.CalledProcessError(return_code, command)
    except (subprocess.TimeoutExpired, KeyboardInterrupt):
        output = primary_path.read_bytes()[start_offset:]
        for mirror_path in unique_paths[1:]:
            mirror_path.parent.mkdir(parents=True, exist_ok=True)
            with mirror_path.open("ab") as mirror:
                mirror.write(output)
        if echo:
            print(output.decode("utf-8", errors="replace"), end="")
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


def require_python_modules(names: tuple[str, ...]) -> None:
    missing = [name for name in names if importlib.util.find_spec(name) is None]
    if missing:
        extras = str(WORKER_ROOT) + "[dev]"
        raise RuntimeError(
            "missing Python verification modules: "
            + ", ".join(missing)
            + f". Install them with: {sys.executable} -m pip install -e '{extras}'"
        )


def verify_python(log_path: Path | None = None) -> None:
    require_python_modules(("ruff", "pyright", "pylint", "pytest", "coverage"))
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
        ],
    )
    for command in commands:
        run(command, cwd=WORKER_ROOT, log_path=log_path)


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
        print(f"\n> {' '.join(command)}", flush=True)
        result = subprocess.run(
            command,
            cwd=ROOT,
            check=False,
            timeout=remaining_timeout(timeout_seconds),
        )
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


def verify_dotnet(log_path: Path | None = None) -> None:
    dotnet = resolve_dotnet()
    environment = os.environ.copy()
    # Verification is a batch task, not an interactive build session. Avoid retaining MSBuild nodes after it ends.
    environment["MSBUILDDISABLENODEREUSE"] = "1"
    environment["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1"
    commands = (
        [dotnet, "--version"],
        [dotnet, "restore", str(SOLUTION)],
        [dotnet, "format", str(SOLUTION), "--verify-no-changes", "--no-restore"],
        [dotnet, "build", str(SOLUTION), "-c", "Release", "--no-restore"],
        [dotnet, "test", str(SOLUTION), "-c", "Release", "--no-build"],
        # The full solution test command already runs CtrlRAM UI smoke tests. Keep the
        # fixture gate here for manifest and payload-hash validation only.
        [sys.executable, str(CTRL_RAM_REPLACE_FIXTURE_VERIFIER), "--skip-public-smoke"],
    )
    build_log = os.environ.get("NFC_DOTNET_BUILD_LOG")
    try:
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
    finally:
        # Avalonia/Roslyn may start compiler servers even with node reuse disabled.
        # Stop only servers from the repository-selected SDK after every verification run.
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
    parser = argparse.ArgumentParser(description=__doc__)
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
        choices=("structure", "python", "dotnet"),
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
    return parser.parse_args(arguments)


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
            lanes.append(
                VerificationLane("dotnet", verify_dotnet, isolate_action=True)
            )
    return tuple(lanes)


def run_internal_lane(name: str) -> None:
    """Run one public lane directly inside the parent-owned lane process tree."""

    actions: dict[str, LaneAction] = {
        "structure": verify_structure,
        "python": repository_script_and_python_lane(),
        "dotnet": verify_dotnet,
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
                run_isolated_lane(lane.name, log_path)
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

    try:
        if jobs == 1 or len(lanes) < 2:
            return tuple(run_lane(lane) for lane in lanes)

        results: dict[str, LaneResult] = {}
        with ThreadPoolExecutor(max_workers=min(jobs, len(lanes))) as executor:
            futures = {executor.submit(run_lane, lane): lane.name for lane in lanes}
            for future in as_completed(futures):
                result = future.result()
                results[result.name] = result
        return tuple(results[lane.name] for lane in lanes)
    except KeyboardInterrupt:
        terminate_active_processes()
        raise


def report_lane_results(results: Sequence[LaneResult]) -> None:
    """Emit isolated lane logs and a stable aggregate verdict in declaration order."""

    for result in results:
        print(f"\n=== {result.name} lane ({result.duration_seconds:.1f}s) ===")
        if result.log_path.is_file():
            print(result.log_path.read_text(encoding="utf-8", errors="replace"), end="")
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


def main() -> int:
    args = parse_args()
    if args.internal_lane:
        run_internal_lane(args.internal_lane)
        return 0
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
    except (RuntimeError, subprocess.CalledProcessError) as exc:
        print(f"\nVERIFICATION FAILED: {exc}", file=sys.stderr)
        return 1

    print("\nVerification passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

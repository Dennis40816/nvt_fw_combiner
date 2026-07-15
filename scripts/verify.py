"""Canonical cross-platform verification entry point for NFC and Codex."""

from __future__ import annotations

import argparse
import importlib.util
import os
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WORKER_ROOT = ROOT / "tools" / "crc-worker"
SOLUTION = ROOT / "NvtFwCombiner.slnx"
CTRL_RAM_REPLACE_FIXTURE_VERIFIER = ROOT / "scripts" / "verify_ctrlram_replace_fixture.py"
CTRL_RAM_SENTINEL_CREATOR = ROOT / "scripts" / "create_ctrlram_universal_sentinel.py"
IDLE_BUILD_WORKER_STOPPER = ROOT / "scripts" / "stop-idle-build-workers.ps1"
REPOSITORY_SCRIPT_TESTS = ROOT / "tests" / "scripts"


def run(
    command: list[str], *, cwd: Path = ROOT, environment: dict[str, str] | None = None
) -> None:
    print(f"\n> {' '.join(command)}", flush=True)
    subprocess.run(command, cwd=cwd, check=True, env=environment)


def run_with_log(
    command: list[str],
    log_path: Path,
    *,
    cwd: Path = ROOT,
    environment: dict[str, str] | None = None,
) -> None:
    print(f"\n> {' '.join(command)}", flush=True)
    log_path.parent.mkdir(parents=True, exist_ok=True)
    process = subprocess.Popen(
        command,
        cwd=cwd,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        errors="replace",
        env=environment,
    )
    if process.stdout is None:
        raise RuntimeError("failed to capture process output")

    with log_path.open("w", encoding="utf-8") as log:
        for line in process.stdout:
            print(line, end="")
            log.write(line)

    return_code = process.wait()
    if return_code != 0:
        raise subprocess.CalledProcessError(return_code, command)


def verify_structure() -> None:
    run([sys.executable, "scripts/validate_repository.py"])
    run([sys.executable, "scripts/polytail_check.py"])
    run([sys.executable, str(CTRL_RAM_SENTINEL_CREATOR), "--dry-run"])


def verify_repository_scripts() -> None:
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
        ]
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


def verify_python() -> None:
    require_python_modules(("ruff", "pyright", "pylint", "pytest", "coverage"))
    commands = (
        [sys.executable, "-m", "ruff", "format", "--check", "src", "tests", "packaging"],
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
        run(command, cwd=WORKER_ROOT)


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


def stop_idle_build_workers() -> None:
    """Stops only the repo-bound Avalonia collector left after a batch build on Windows."""
    if sys.platform != "win32":
        return

    powershell = shutil.which("powershell") or shutil.which("pwsh")
    if powershell is None:
        print("warning: PowerShell was unavailable; idle Avalonia build worker cleanup was skipped.")
        return

    print(f"\n> {powershell} -File {IDLE_BUILD_WORKER_STOPPER}", flush=True)
    result = subprocess.run(
        [powershell, "-NoProfile", "-File", str(IDLE_BUILD_WORKER_STOPPER), "-RepositoryRoot", str(ROOT)],
        cwd=ROOT,
        check=False,
    )
    if result.returncode != 0:
        print(f"warning: idle Avalonia build worker cleanup returned exit code {result.returncode}.")


def verify_dotnet() -> None:
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
                run_with_log(command, Path(build_log), environment=environment)
            else:
                run(command, environment=environment)
    finally:
        # Avalonia/Roslyn may start compiler servers even with node reuse disabled.
        # Stop only servers from the repository-selected SDK after every verification run.
        run([dotnet, "build-server", "shutdown"], environment=environment)
        stop_idle_build_workers()


def parse_args() -> argparse.Namespace:
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
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    structure_only = args.structure_only
    if args.all and (args.skip_structure or args.skip_python or args.skip_dotnet):
        raise SystemExit("--all cannot be combined with skip flags")
    if structure_only and (args.all or args.skip_structure or args.skip_python or args.skip_dotnet):
        raise SystemExit("--structure-only cannot be combined with other selection flags")

    try:
        if not args.skip_structure:
            verify_structure()
        if not structure_only:
            verify_repository_scripts()
            if not args.skip_python:
                verify_python()
            if not args.skip_dotnet:
                verify_dotnet()
    except (RuntimeError, subprocess.CalledProcessError) as exc:
        print(f"\nVERIFICATION FAILED: {exc}", file=sys.stderr)
        return 1

    print("\nVerification passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

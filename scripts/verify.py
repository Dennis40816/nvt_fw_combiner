"""Canonical cross-platform verification entry point for NFC and Codex."""

from __future__ import annotations

import argparse
import importlib.util
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WORKER_ROOT = ROOT / "tools" / "crc-worker"
SOLUTION = ROOT / "NvtFwCombiner.slnx"


def run(command: list[str], *, cwd: Path = ROOT) -> None:
    print(f"\n> {' '.join(command)}", flush=True)
    subprocess.run(command, cwd=cwd, check=True)


def verify_structure() -> None:
    run([sys.executable, "scripts/validate_repository.py"])
    run([sys.executable, "scripts/polytail_check.py"])


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


def verify_dotnet() -> None:
    dotnet = resolve_dotnet()
    commands = (
        [dotnet, "--version"],
        [dotnet, "restore", str(SOLUTION)],
        [dotnet, "format", str(SOLUTION), "--verify-no-changes", "--no-restore"],
        [dotnet, "build", str(SOLUTION), "-c", "Release", "--no-restore"],
        [dotnet, "test", str(SOLUTION), "-c", "Release", "--no-build"],
    )
    for command in commands:
        run(command)


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
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    structure_only = args.structure_only
    if structure_only and (args.all or args.skip_python or args.skip_dotnet):
        raise SystemExit("--structure-only cannot be combined with other selection flags")

    try:
        verify_structure()
        if not structure_only:
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

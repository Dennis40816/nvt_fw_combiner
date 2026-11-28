"""Compare the repository test-data copy with the pinned nvt-testdata mirror.

The mirror named by NFC_TESTDATA_ROOT must hold the same relative files with the
same SHA-256 as the repository copy. Only the pin file is excluded, because it
lives only in this repository and names the mirror commit.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
from collections.abc import Sequence
from pathlib import Path, PurePosixPath

try:
    from scripts.testdata_paths import REPOSITORY_ROOT, resolve_testdata_root
except ModuleNotFoundError:  # Direct `python scripts/...` execution.
    from testdata_paths import REPOSITORY_ROOT, resolve_testdata_root


REPOSITORY_TESTDATA_NAME = "testdata"
PIN_FILE = PurePosixPath("nvt-testdata.json")
PIN_SCHEMA_VERSION = 1
FULL_COMMIT = re.compile(r"[0-9a-f]{40}")
REPOSITORY_NAME = re.compile(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+")
ROOT_NAME = re.compile(r"[A-Za-z0-9_.-]+")
REPORT_LIMIT = 20


def repository_copy(repository_root: Path = REPOSITORY_ROOT) -> Path:
    """Return the physical repository copy; the resolver must not redirect it."""
    return repository_root / REPOSITORY_TESTDATA_NAME


def read_pin(repository_root: Path = REPOSITORY_ROOT) -> dict[str, str]:
    """Read and validate the pinned nvt-testdata repository, commit and root."""
    path = repository_copy(repository_root).joinpath(*PIN_FILE.parts)
    data = json.loads(path.read_text(encoding="utf-8"))
    expected_keys = {"schemaVersion", "repository", "commit", "root"}
    if not isinstance(data, dict) or set(data) != expected_keys:
        raise ValueError(f"{PIN_FILE} must declare exactly: {', '.join(sorted(expected_keys))}")
    if data["schemaVersion"] != PIN_SCHEMA_VERSION:
        raise ValueError(f"{PIN_FILE} schemaVersion must be {PIN_SCHEMA_VERSION}.")
    checks = (
        ("repository", REPOSITORY_NAME),
        ("commit", FULL_COMMIT),
        ("root", ROOT_NAME),
    )
    for key, pattern in checks:
        value = data[key]
        if not isinstance(value, str) or pattern.fullmatch(value) is None or value in {".", ".."}:
            raise ValueError(f"{PIN_FILE} has an invalid {key}.")
    return {key: data[key] for key, _ in checks}


def configured_mirror_root() -> Path:
    """Require an explicit mirror; an unset variable never compares the copy with itself."""
    if os.environ.get("NFC_TESTDATA_ROOT") is None:
        raise ValueError("NFC_TESTDATA_ROOT must be set to the nvt-testdata mirror directory.")
    return resolve_testdata_root()


def tree_hashes(root: Path, excluded: frozenset[PurePosixPath] = frozenset()) -> dict[PurePosixPath, str]:
    """Hash every regular file below root by its relative POSIX path."""
    hashes: dict[PurePosixPath, str] = {}
    for path in sorted(root.rglob("*")):
        relative = PurePosixPath(path.relative_to(root).as_posix())
        if path.is_symlink():
            raise ValueError(f"symbolic links are not allowed in test data: {relative}")
        if not path.is_file() or relative in excluded:
            continue
        hashes[relative] = hashlib.sha256(path.read_bytes()).hexdigest()
    return hashes


def compare_trees(repository: Path, mirror: Path) -> dict[str, list[str]]:
    """Return missing, extra and changed relative paths of the mirror."""
    expected = tree_hashes(repository, frozenset({PIN_FILE}))
    actual = tree_hashes(mirror, frozenset({PIN_FILE}))
    return {
        "missing": sorted(str(path) for path in expected.keys() - actual.keys()),
        "extra": sorted(str(path) for path in actual.keys() - expected.keys()),
        "changed": sorted(
            str(path) for path in expected.keys() & actual.keys() if expected[path] != actual[path]
        ),
    }


def report(differences: dict[str, list[str]]) -> str:
    lines: list[str] = []
    for category, paths in differences.items():
        if not paths:
            continue
        lines.append(f"{category}: {len(paths)}")
        lines.extend(f"  {path}" for path in paths[:REPORT_LIMIT])
        if len(paths) > REPORT_LIMIT:
            lines.append(f"  ... {len(paths) - REPORT_LIMIT} more")
    return "\n".join(lines)


def check_mirror(repository_root: Path = REPOSITORY_ROOT) -> int:
    repository = repository_copy(repository_root).resolve()
    mirror = configured_mirror_root()
    if mirror == repository:
        raise ValueError("NFC_TESTDATA_ROOT names the repository copy, not the nvt-testdata mirror.")
    differences = compare_trees(repository, mirror)
    if any(differences.values()):
        print("The nvt-testdata mirror differs from the repository test data.", file=sys.stderr)
        print(report(differences), file=sys.stderr)
        return 1
    count = len(tree_hashes(repository, frozenset({PIN_FILE})))
    print(f"The nvt-testdata mirror matches the repository test data ({count} files).")
    return 0


def write_pin_outputs(output: Path, repository_root: Path = REPOSITORY_ROOT) -> None:
    """Append the validated pin as GitHub step outputs."""
    pin = read_pin(repository_root)
    with output.open("a", encoding="utf-8", newline="\n") as stream:
        for key, value in pin.items():
            stream.write(f"{key}={value}\n")


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    subcommands = parser.add_subparsers(dest="command")
    pin = subcommands.add_parser("pin", help="Write the validated pin as step outputs.")
    pin.add_argument("--output", type=Path, required=True)
    arguments = parser.parse_args(argv)
    try:
        if arguments.command == "pin":
            write_pin_outputs(arguments.output)
            return 0
        return check_mirror()
    except (OSError, ValueError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

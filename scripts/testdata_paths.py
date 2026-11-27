"""Resolve test-data storage while preserving repository-relative identities."""

from __future__ import annotations

import os
from pathlib import Path, PurePosixPath


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]


def resolve_testdata_root(repository_root: Path = REPOSITORY_ROOT) -> Path:
    """Use the configured existing absolute directory, or the repository copy."""
    configured = os.environ.get("NFC_TESTDATA_ROOT")
    if configured is None:
        return repository_root / "testdata"
    if not configured.strip() or not Path(configured).is_absolute():
        raise ValueError("NFC_TESTDATA_ROOT must name a fully qualified directory.")
    root = Path(configured).resolve()
    if not root.is_dir():
        raise ValueError(f"NFC_TESTDATA_ROOT directory does not exist: {root}")
    return root


def repository_path(relative: str | PurePosixPath, repository_root: Path = REPOSITORY_ROOT) -> Path:
    """Resolve a repository identity for reading without changing its key."""
    path = PurePosixPath(relative)
    if path.parts and path.parts[0] == "testdata":
        return resolve_testdata_root(repository_root).joinpath(*path.parts[1:])
    return repository_root.joinpath(*path.parts)

"""Measure and enforce the repository's non-gameable source-size ratchets."""

from __future__ import annotations

import hashlib
import re
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path


EXCLUDED_DIRECTORY_NAMES = frozenset(
    {
        ".git",
        ".pytest_cache",
        ".ruff_cache",
        ".venv",
        "__pycache__",
        "artifacts",
        "bin",
        "obj",
        "release",
    }
)
NAMESPACE_PATTERN = re.compile(
    r"^\s*namespace\s+([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*[;{]",
    re.MULTILINE,
)
PARTIAL_TYPE_PATTERN = re.compile(
    r"\bpartial\s+(?:class|record(?:\s+(?:class|struct))?|struct)\s+([A-Za-z_]\w*)"
)


@dataclass(frozen=True)
class CodeSizeLimits:
    """Exact ratchets and the default ceiling for a partial type aggregate."""

    production_nonblank: int
    duplicate_json_nonblank: int
    partial_type_default_max: int
    partial_type_exact_ratchets: dict[str, int]


@dataclass(frozen=True)
class PartialTypeAggregate:
    """Nonblank C# lines owned by one partial type across source files."""

    name: str
    file_count: int
    nonblank_lines: int


@dataclass(frozen=True)
class CodeSizeSnapshot:
    """Reproducible measurements used by the repository validator."""

    production_files: int
    production_nonblank: int
    duplicate_json_groups: int
    duplicate_json_copies: int
    duplicate_json_nonblank: int
    partial_types: tuple[PartialTypeAggregate, ...]


DEFAULT_LIMITS = CodeSizeLimits(
    production_nonblank=55_772,
    duplicate_json_nonblank=1_156,
    partial_type_default_max=2_500,
    partial_type_exact_ratchets={
        "NvtFwCombiner.Bootstrap.WorkbenchCompositionService": 4_478,
        "NvtFwCombiner.Presentation.Avalonia.ViewModels.MainWindowViewModel": 2_733,
    },
)


def _is_included(path: Path, root: Path) -> bool:
    relative_parts = path.relative_to(root).parts[:-1]
    return not any(
        part.casefold() in EXCLUDED_DIRECTORY_NAMES for part in relative_parts
    )


def _matching_files(root: Path, directory: str, suffixes: frozenset[str]) -> list[Path]:
    search_root = root / directory
    if not search_root.is_dir():
        return []
    return sorted(
        path
        for path in search_root.rglob("*")
        if path.is_file()
        and path.suffix.casefold() in suffixes
        and _is_included(path, root)
    )


def _nonblank_line_count(path: Path) -> int:
    return sum(
        bool(line.strip()) for line in path.read_text(encoding="utf-8-sig").splitlines()
    )


def measure_code_size(root: Path) -> CodeSizeSnapshot:
    """Measure production source, exact JSON duplication, and partial aggregates."""

    source_files = _matching_files(root, "src", frozenset({".cs", ".axaml"}))
    source_line_counts = {path: _nonblank_line_count(path) for path in source_files}

    duplicate_candidates = [
        *_matching_files(root, "profiles", frozenset({".json"})),
        *_matching_files(root, "docs/contracts", frozenset({".json"})),
    ]
    files_by_hash: dict[bytes, list[Path]] = defaultdict(list)
    for path in duplicate_candidates:
        files_by_hash[hashlib.sha256(path.read_bytes()).digest()].append(path)
    duplicate_groups = [paths for paths in files_by_hash.values() if len(paths) > 1]
    duplicate_json_nonblank = sum(
        (len(paths) - 1) * _nonblank_line_count(paths[0]) for paths in duplicate_groups
    )

    partial_files: dict[str, list[Path]] = defaultdict(list)
    for path in source_files:
        if path.suffix.casefold() != ".cs":
            continue
        text = path.read_text(encoding="utf-8-sig")
        namespace_match = NAMESPACE_PATTERN.search(text)
        if namespace_match:
            qualified_names = {
                f"{namespace_match.group(1)}.{partial_match.group(1)}"
                for partial_match in PARTIAL_TYPE_PATTERN.finditer(text)
            }
            for qualified_name in qualified_names:
                partial_files[qualified_name].append(path)

    partial_types = tuple(
        PartialTypeAggregate(
            name=name,
            file_count=len(paths),
            nonblank_lines=sum(source_line_counts[path] for path in paths),
        )
        for name, paths in sorted(partial_files.items())
        if len(paths) > 1
    )
    return CodeSizeSnapshot(
        production_files=len(source_files),
        production_nonblank=sum(source_line_counts.values()),
        duplicate_json_groups=len(duplicate_groups),
        duplicate_json_copies=sum(len(paths) - 1 for paths in duplicate_groups),
        duplicate_json_nonblank=duplicate_json_nonblank,
        partial_types=partial_types,
    )


def _validate_exact_ratchet(
    label: str, actual: int, expected: int, errors: list[str]
) -> None:
    if actual > expected:
        errors.append(f"code-size {label} grew: {actual} > ratchet {expected}")
    elif actual < expected:
        errors.append(
            f"code-size {label} improved: lower the ratchet from {expected} to {actual}"
        )


def validate_code_size_policy(
    root: Path,
    errors: list[str],
    limits: CodeSizeLimits = DEFAULT_LIMITS,
) -> None:
    """Append deterministic ratchet and aggregate-limit violations."""

    snapshot = measure_code_size(root)
    _validate_exact_ratchet(
        "production nonblank lines",
        snapshot.production_nonblank,
        limits.production_nonblank,
        errors,
    )
    _validate_exact_ratchet(
        "exact duplicate JSON nonblank lines",
        snapshot.duplicate_json_nonblank,
        limits.duplicate_json_nonblank,
        errors,
    )

    aggregates = {aggregate.name: aggregate for aggregate in snapshot.partial_types}
    for name, expected in limits.partial_type_exact_ratchets.items():
        actual = aggregates.get(name)
        actual_lines = actual.nonblank_lines if actual else 0
        _validate_exact_ratchet(
            f"partial aggregate {name}", actual_lines, expected, errors
        )

    for aggregate in snapshot.partial_types:
        if aggregate.name in limits.partial_type_exact_ratchets:
            continue
        if aggregate.nonblank_lines > limits.partial_type_default_max:
            errors.append(
                "code-size partial aggregate "
                f"{aggregate.name} has {aggregate.nonblank_lines} nonblank lines across "
                f"{aggregate.file_count} files; maximum is {limits.partial_type_default_max}"
            )

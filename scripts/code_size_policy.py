"""Measure repository source size and emit non-blocking maintainability findings."""

from __future__ import annotations

import hashlib
import re
from collections import defaultdict
from dataclasses import dataclass, field
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
PYTHON_RUNTIME_EXCLUDED_DIRECTORIES = frozenset(
    {".mypy_cache", ".pytest_cache", ".ruff_cache", ".venv", "__pycache__", "venv"}
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
    """Review thresholds for production size, duplicate JSON, and partial aggregates."""

    production_nonblank: int
    duplicate_json_nonblank: int
    partial_type_default_max: int
    partial_type_exact_ratchets: dict[str, int]
    partial_type_named_maximums: dict[str, int] = field(default_factory=dict)
    runtime_production_baseline: int | None = None
    runtime_production_ratchet: int | None = None
    domain_profiles_ratchet: int | None = None
    application_ratchet: int | None = None
    bootstrap_cli_ratchet: int | None = None
    infrastructure_contracts_worker_ratchet: int | None = None
    full_production_ratchet: int | None = None
    runtime_production_allowance: int = 0
    domain_profiles_allowance: int = 0
    application_allowance: int = 0
    bootstrap_cli_allowance: int = 0
    infrastructure_contracts_worker_allowance: int = 0
    full_production_allowance: int = 0


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
    runtime_production_files: int
    runtime_production_nonblank: int
    domain_profiles_files: int
    domain_profiles_nonblank: int
    application_files: int
    application_nonblank: int
    bootstrap_cli_files: int
    bootstrap_cli_nonblank: int
    infrastructure_contracts_worker_files: int
    infrastructure_contracts_worker_nonblank: int


DEFAULT_LIMITS = CodeSizeLimits(
    production_nonblank=134_374,
    duplicate_json_nonblank=0,
    partial_type_default_max=2_500,
    partial_type_exact_ratchets={},
    partial_type_named_maximums={
        "NvtFwCombiner.Presentation.Avalonia.ViewModels.MainWindowViewModel": 985,
        "NvtFwCombiner.Presentation.Avalonia.ViewModels.ShellTextResources": 2_501,
        "NvtFwCombiner.Presentation.Avalonia.ViewModels.WorkflowSessionPresentationViewModel": 2_626,
        "NvtFwCombiner.Profiles.V2.V2CompositionPlanCompiler": 2_798,
    },
    runtime_production_baseline=45_214,
    runtime_production_ratchet=70_056,
    domain_profiles_ratchet=20_627,
    application_ratchet=30_690,
    bootstrap_cli_ratchet=3_378,
    infrastructure_contracts_worker_ratchet=15_356,
    full_production_ratchet=102_896,
    runtime_production_allowance=26_506,
    domain_profiles_allowance=5,
    application_allowance=10_357,
    bootstrap_cli_allowance=1_476,
    infrastructure_contracts_worker_allowance=14_673,
    full_production_allowance=31_478,
)


def is_physical_source_file(
    path: Path,
    root: Path,
    suffixes: frozenset[str],
) -> bool:
    """Return whether a real source file belongs to the measured physical tree."""

    try:
        relative = path.resolve().relative_to(root.resolve())
    except ValueError:
        return False
    return (
        path.is_file()
        and path.suffix.casefold() in suffixes
        and not any(
            part.casefold() in EXCLUDED_DIRECTORY_NAMES for part in relative.parts[:-1]
        )
    )


def _matching_files(root: Path, directory: str, suffixes: frozenset[str]) -> list[Path]:
    search_root = root / directory
    if not search_root.is_dir():
        return []
    return sorted(
        path
        for path in search_root.rglob("*")
        if is_physical_source_file(path, root, suffixes)
    )


def _nonblank_line_count(path: Path) -> int:
    return sum(
        bool(line.strip()) for line in path.read_text(encoding="utf-8-sig").splitlines()
    )


def _worker_runtime_files(root: Path) -> list[Path]:
    """Measure every owned Python source below the canonical worker package root."""

    search_root = root / "tools/crc-worker/src"
    if not search_root.is_dir():
        return []
    resolved_root = search_root.resolve()
    files: list[Path] = []
    for path in search_root.rglob("*"):
        try:
            relative = path.resolve().relative_to(resolved_root)
        except ValueError:
            continue
        if (
            path.is_file()
            and path.suffix.casefold() == ".py"
            and not any(
                part.casefold() in PYTHON_RUNTIME_EXCLUDED_DIRECTORIES
                for part in relative.parts[:-1]
            )
        ):
            files.append(path)
    return sorted(files)


def _runtime_production_files(root: Path) -> list[Path]:
    """Return the fixed 0.10.x non-UI/runtime source measurement set."""

    csharp_files = [
        path
        for path in _matching_files(root, "src", frozenset({".cs"}))
        if path.relative_to(root).parts[1:2] != ("NvtFwCombiner.Presentation.Avalonia",)
    ]
    worker_files = _worker_runtime_files(root)
    return [*csharp_files, *worker_files]


def _domain_profiles_files(root: Path) -> list[Path]:
    """Return the fixed Canonical Core Domain + Profiles slice."""

    return [
        *_matching_files(root, "src/NvtFwCombiner.Domain", frozenset({".cs"})),
        *_matching_files(root, "src/NvtFwCombiner.Profiles", frozenset({".cs"})),
    ]


def _application_files(root: Path) -> list[Path]:
    """Return the fixed Canonical Core Application slice."""

    return [
        *_matching_files(root, "src/NvtFwCombiner.Application", frozenset({".cs"})),
        *_matching_files(
            root,
            "src/NvtFwCombiner.VersionManagement.Application",
            frozenset({".cs"}),
        ),
    ]


def _bootstrap_cli_files(root: Path) -> list[Path]:
    """Return the fixed Bootstrap, launcher, CLI, and desktop host slice."""

    return [
        *_matching_files(root, "src/NvtFwCombiner.Bootstrap", frozenset({".cs"})),
        *_matching_files(root, "src/NvtFwCombiner.Cli", frozenset({".cs"})),
        *_matching_files(root, "src/NvtFwCombiner.Desktop", frozenset({".cs"})),
        *_matching_files(
            root,
            "src/NvtFwCombiner.DistributionLauncher",
            frozenset({".cs"}),
        ),
        *_matching_files(root, "src/NvtFwCombiner.Launcher", frozenset({".cs"})),
        *_matching_files(
            root,
            "src/NvtFwCombiner.LauncherBootstrap",
            frozenset({".cs"}),
        ),
    ]


def _infrastructure_contracts_worker_files(root: Path) -> list[Path]:
    """Return the fixed Canonical Core adapter, contract, and worker slice."""

    return [
        *_matching_files(
            root,
            "src/NvtFwCombiner.Infrastructure",
            frozenset({".cs"}),
        ),
        *_matching_files(root, "src/NvtFwCombiner.Platform", frozenset({".cs"})),
        *_matching_files(root, "src/NvtFwCombiner.Contracts", frozenset({".cs"})),
        *_matching_files(
            root,
            "src/NvtFwCombiner.VersionManagement.Infrastructure",
            frozenset({".cs"}),
        ),
        *_worker_runtime_files(root),
    ]


def measure_code_size(root: Path) -> CodeSizeSnapshot:
    """Measure production source, exact JSON duplication, and partial aggregates."""

    source_files = _matching_files(root, "src", frozenset({".cs", ".axaml"}))
    source_line_counts = {path: _nonblank_line_count(path) for path in source_files}
    runtime_source_files = _runtime_production_files(root)
    domain_profiles_files = _domain_profiles_files(root)
    application_files = _application_files(root)
    bootstrap_cli_files = _bootstrap_cli_files(root)
    infrastructure_contracts_worker_files = _infrastructure_contracts_worker_files(root)

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
        runtime_production_files=len(runtime_source_files),
        runtime_production_nonblank=sum(
            _nonblank_line_count(path) for path in runtime_source_files
        ),
        domain_profiles_files=len(domain_profiles_files),
        domain_profiles_nonblank=sum(
            _nonblank_line_count(path) for path in domain_profiles_files
        ),
        application_files=len(application_files),
        application_nonblank=sum(
            _nonblank_line_count(path) for path in application_files
        ),
        bootstrap_cli_files=len(bootstrap_cli_files),
        bootstrap_cli_nonblank=sum(
            _nonblank_line_count(path) for path in bootstrap_cli_files
        ),
        infrastructure_contracts_worker_files=len(
            infrastructure_contracts_worker_files
        ),
        infrastructure_contracts_worker_nonblank=sum(
            _nonblank_line_count(path) for path in infrastructure_contracts_worker_files
        ),
    )


def _review_exact_ratchet(
    label: str, actual: int, expected: int, findings: list[str]
) -> None:
    if actual > expected:
        findings.append(f"code-size review {label} grew: {actual} > ratchet {expected}")
    elif actual < expected:
        findings.append(
            "code-size review "
            f"{label} improved: consider lowering the ratchet from {expected} to {actual}"
        )


def _review_maximum(label: str, actual: int, maximum: int, findings: list[str]) -> None:
    if actual > maximum:
        findings.append(
            f"code-size review {label} exceeded threshold: {actual} > {maximum}"
        )


def _review_slice_metric(
    label: str,
    file_count: int,
    actual: int,
    ratchet: int | None,
    allowance: int,
    findings: list[str],
) -> None:
    if ratchet is None:
        return
    effective = ratchet + allowance
    budget = (
        f"ratchet {ratchet}"
        if allowance == 0
        else f"ratchet {ratchet} + approved allowance {allowance} = {effective}"
    )
    findings.append(
        f"{label} metric: {file_count} files / {actual} nonblank lines ({budget})"
    )
    _review_exact_ratchet(f"{label} slice", actual, effective, findings)


def review_code_size_policy(
    root: Path,
    limits: CodeSizeLimits = DEFAULT_LIMITS,
) -> list[str]:
    """Return deterministic source-size findings without affecting verification success."""

    findings: list[str] = []
    snapshot = measure_code_size(root)
    _review_maximum(
        "production nonblank lines",
        snapshot.production_nonblank,
        limits.production_nonblank,
        findings,
    )
    _review_exact_ratchet(
        "exact duplicate JSON nonblank lines",
        snapshot.duplicate_json_nonblank,
        limits.duplicate_json_nonblank,
        findings,
    )

    if limits.runtime_production_baseline is not None:
        delta = (
            snapshot.runtime_production_nonblank - limits.runtime_production_baseline
        )
        findings.append(
            "runtime production metric: "
            f"{snapshot.runtime_production_files} files / "
            f"{snapshot.runtime_production_nonblank} nonblank lines "
            f"(baseline {limits.runtime_production_baseline}, delta {delta:+d})"
        )

    if limits.runtime_production_ratchet is not None:
        _review_exact_ratchet(
            "runtime production",
            snapshot.runtime_production_nonblank,
            limits.runtime_production_ratchet + limits.runtime_production_allowance,
            findings,
        )

    for label, file_count, actual, ratchet, allowance in (
        (
            "Domain + Profiles",
            snapshot.domain_profiles_files,
            snapshot.domain_profiles_nonblank,
            limits.domain_profiles_ratchet,
            limits.domain_profiles_allowance,
        ),
        (
            "Application",
            snapshot.application_files,
            snapshot.application_nonblank,
            limits.application_ratchet,
            limits.application_allowance,
        ),
        (
            "Bootstrap + CLI + Desktop host",
            snapshot.bootstrap_cli_files,
            snapshot.bootstrap_cli_nonblank,
            limits.bootstrap_cli_ratchet,
            limits.bootstrap_cli_allowance,
        ),
        (
            "Infrastructure + Contracts + CRC worker",
            snapshot.infrastructure_contracts_worker_files,
            snapshot.infrastructure_contracts_worker_nonblank,
            limits.infrastructure_contracts_worker_ratchet,
            limits.infrastructure_contracts_worker_allowance,
        ),
    ):
        _review_slice_metric(
            label,
            file_count,
            actual,
            ratchet,
            allowance,
            findings,
        )
    aggregates = {aggregate.name: aggregate for aggregate in snapshot.partial_types}
    for name, expected in limits.partial_type_exact_ratchets.items():
        actual = aggregates.get(name)
        actual_lines = actual.nonblank_lines if actual else 0
        _review_exact_ratchet(
            f"partial aggregate {name}", actual_lines, expected, findings
        )

    for name, maximum in limits.partial_type_named_maximums.items():
        actual = aggregates.get(name)
        actual_lines = actual.nonblank_lines if actual else 0
        _review_maximum(f"partial aggregate {name}", actual_lines, maximum, findings)

    for aggregate in snapshot.partial_types:
        if (
            aggregate.name in limits.partial_type_exact_ratchets
            or aggregate.name in limits.partial_type_named_maximums
        ):
            continue
        if aggregate.nonblank_lines > limits.partial_type_default_max:
            findings.append(
                "code-size review partial aggregate "
                f"{aggregate.name} has {aggregate.nonblank_lines} nonblank lines across "
                f"{aggregate.file_count} files; threshold is {limits.partial_type_default_max}"
            )

    return findings


def validate_code_size_policy(
    root: Path,
    limits: CodeSizeLimits = DEFAULT_LIMITS,
) -> list[str]:
    """Fail closed on Core ratchet drift and cross-slice relocation."""

    snapshot = measure_code_size(root)
    errors: list[str] = []
    metrics = (
        (
            "full production",
            snapshot.production_nonblank,
            limits.full_production_ratchet,
            limits.full_production_allowance,
        ),
        (
            "runtime production",
            snapshot.runtime_production_nonblank,
            limits.runtime_production_ratchet,
            limits.runtime_production_allowance,
        ),
        (
            "Domain + Profiles slice",
            snapshot.domain_profiles_nonblank,
            limits.domain_profiles_ratchet,
            limits.domain_profiles_allowance,
        ),
        (
            "Application slice",
            snapshot.application_nonblank,
            limits.application_ratchet,
            limits.application_allowance,
        ),
        (
            "Bootstrap + CLI + Desktop host slice",
            snapshot.bootstrap_cli_nonblank,
            limits.bootstrap_cli_ratchet,
            limits.bootstrap_cli_allowance,
        ),
        (
            "Infrastructure + Contracts + CRC worker slice",
            snapshot.infrastructure_contracts_worker_nonblank,
            limits.infrastructure_contracts_worker_ratchet,
            limits.infrastructure_contracts_worker_allowance,
        ),
    )
    slices = metrics[2:]
    if all(ratchet is not None for _, _, ratchet, _ in slices):
        allocated = sum(actual for _, actual, _, _ in slices)
        if allocated != snapshot.runtime_production_nonblank:
            errors.append(
                "code-size runtime slice allocation mismatch: "
                f"{allocated} != total {snapshot.runtime_production_nonblank}"
            )
    for label, actual, ratchet, allowance in metrics:
        effective = None if ratchet is None else ratchet + allowance
        if effective is not None and actual > effective:
            errors.append(f"code-size {label} grew: {actual} > ratchet {effective}")
        elif effective is not None and actual < effective:
            errors.append(
                f"code-size {label} improved: lower ratchet {effective} to {actual}"
            )
    return errors

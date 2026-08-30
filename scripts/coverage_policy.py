"""Parse real coverage reports and enforce the 0.10.x coverage policy.

This module is deliberately library-only. ``scripts/verify.py`` remains the
sole public verification entry point and owns report collection and policy
invocation.
"""

from __future__ import annotations

import json
import math
import re
import subprocess
import xml.etree.ElementTree as element_tree
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Any, Iterable

if __package__:
    from .code_size_policy import PYTHON_RUNTIME_EXCLUDED_DIRECTORIES
else:
    from code_size_policy import PYTHON_RUNTIME_EXCLUDED_DIRECTORIES


ROOT = Path(__file__).resolve().parents[1]
BASELINE_PATH = ROOT / "docs" / "contracts" / "coverage-baseline-v1.json"
SCHEMA_VERSION = "1.0"
DOTNET_LANGUAGE = "dotnet"
PYTHON_LANGUAGE = "python"
LANGUAGES = frozenset({DOTNET_LANGUAGE, PYTHON_LANGUAGE})
PRODUCTION_MODULES = {
    "Domain": (Path("src/NvtFwCombiner.Domain"),),
    "Contracts": (Path("src/NvtFwCombiner.Contracts"),),
    "Application": (
        Path("src/NvtFwCombiner.Application"),
        Path("src/NvtFwCombiner.VersionManagement.Application"),
    ),
    "Profiles": (Path("src/NvtFwCombiner.Profiles"),),
    "Infrastructure": (
        Path("src/NvtFwCombiner.Infrastructure"),
        Path("src/NvtFwCombiner.VersionManagement.Infrastructure"),
    ),
    "Bootstrap": (
        Path("src/NvtFwCombiner.Bootstrap"),
        Path("src/NvtFwCombiner.Desktop"),
        Path("src/NvtFwCombiner.DistributionLauncher"),
        Path("src/NvtFwCombiner.Launcher"),
        Path("src/NvtFwCombiner.LauncherBootstrap"),
    ),
    "Cli": (Path("src/NvtFwCombiner.Cli"),),
    "PresentationAvalonia": (Path("src/NvtFwCombiner.Presentation.Avalonia"),),
}
RATCHET_MODULES = {name: PRODUCTION_MODULES[name] for name in ("Domain", "Application")}
FULL_SHA_PATTERN = re.compile(r"^[0-9a-f]{40}$")
HUNK_PATTERN = re.compile(
    r"^@@ -\d+(?:,(?P<old>\d+))? \+\d+(?:,(?P<new>\d+))? @@",
    re.MULTILINE,
)
COBERTURA_BRANCH_COUNT_PATTERN = re.compile(
    r"^\s*\d+(?:\.\d+)?%\s+\((?P<covered>\d+)/(?P<total>\d+)\)\s*$"
)


@dataclass(frozen=True)
class CoverageMeasure:
    """One executable coverage measure represented as integers, never floats."""

    covered: int
    total: int

    def __post_init__(self) -> None:
        if self.covered < 0 or self.total < 0 or self.covered > self.total:
            raise ValueError(
                f"invalid coverage measure: covered={self.covered}, total={self.total}"
            )

    def as_document(self) -> dict[str, int]:
        return {"covered": self.covered, "total": self.total}

    def is_at_least(self, other: "CoverageMeasure") -> bool:
        """Compare rates exactly, including source-size changes."""

        if other.total == 0:
            return True
        if self.total == 0:
            return False
        return self.covered * other.total >= other.covered * self.total

    def meets_percent(self, minimum: int) -> bool:
        return self.total > 0 and self.covered * 100 >= minimum * self.total

    def percentage_label(self) -> str:
        if self.total == 0:
            return "n/a (0/0)"
        return f"{self.covered * 100 / self.total:.2f}% ({self.covered}/{self.total})"


@dataclass(frozen=True)
class CoverageSummary:
    """Line and branch coverage for one language or source module."""

    lines: CoverageMeasure
    branches: CoverageMeasure

    def as_document(self) -> dict[str, dict[str, int]]:
        return {
            "lines": self.lines.as_document(),
            "branches": self.branches.as_document(),
        }


@dataclass(frozen=True)
class CoverageInventory:
    """A report's overall and module-scoped coverage without duplicate ownership."""

    overall: CoverageSummary
    modules: dict[str, CoverageSummary]

    def as_document(self) -> dict[str, Any]:
        return {
            "overall": self.overall.as_document(),
            "modules": {
                name: summary.as_document()
                for name, summary in sorted(self.modules.items())
            },
        }


@dataclass(frozen=True)
class _CoverageLine:
    line_hit: bool


@dataclass
class _CoberturaClassStructure:
    """Physical lines and declared branch cardinalities for one report class."""

    lines: set[int]
    branch_measures: dict[int, CoverageMeasure]


def _document_measure(document: Any, label: str) -> CoverageMeasure:
    if not isinstance(document, dict):
        raise ValueError(f"{label} must be an object")
    covered = document.get("covered")
    total = document.get("total")
    if (
        not isinstance(covered, int)
        or isinstance(covered, bool)
        or not isinstance(total, int)
        or isinstance(total, bool)
    ):
        raise ValueError(f"{label} must contain integer covered and total values")
    return CoverageMeasure(covered, total)


def _document_summary(document: Any, label: str) -> CoverageSummary:
    if not isinstance(document, dict):
        raise ValueError(f"{label} must be an object")
    return CoverageSummary(
        _document_measure(document.get("lines"), f"{label}.lines"),
        _document_measure(document.get("branches"), f"{label}.branches"),
    )


def load_baseline(path: Path = BASELINE_PATH) -> dict[str, Any]:
    """Load and validate the checked-in coverage baseline source of truth."""

    with path.open(encoding="utf-8") as handle:
        document = json.load(handle)
    if not isinstance(document, dict):
        raise ValueError("coverage baseline root must be an object")
    if document.get("schemaVersion") != SCHEMA_VERSION:
        raise ValueError(f"coverage baseline schemaVersion must be {SCHEMA_VERSION}")
    baseline_commit = document.get("baselineCommit")
    if (
        not isinstance(baseline_commit, str)
        or FULL_SHA_PATTERN.fullmatch(baseline_commit) is None
    ):
        raise ValueError(
            "coverage baseline baselineCommit must be a lowercase full Git SHA"
        )
    revision = document.get("changeBaseRevision")
    if revision != baseline_commit:
        raise ValueError(
            "coverage baseline changeBaseRevision must equal baselineCommit"
        )

    collection = document.get("collection")
    if not isinstance(collection, dict) or set(collection) != LANGUAGES:
        raise ValueError(
            "coverage baseline must define exactly dotnet and python collectors"
        )
    if collection[DOTNET_LANGUAGE] != {
        "collector": "coverlet.collector",
        "version": "6.0.4",
        "format": "json,cobertura",
    }:
        raise ValueError(
            "coverage baseline must use the pinned paired Coverlet JSON/Cobertura collector"
        )
    if collection[PYTHON_LANGUAGE] != {
        "collector": "pytest-cov / coverage.py",
        "pytestCovVersion": "6.3.0",
        "coveragePyVersion": "7.14.3",
        "format": "coverage-json",
    }:
        raise ValueError(
            "coverage baseline must use the approved Python coverage collector"
        )

    languages = document.get("languages")
    if not isinstance(languages, dict) or set(languages) != LANGUAGES:
        raise ValueError(
            "coverage baseline must define exactly dotnet and python languages"
        )
    for language in LANGUAGES:
        language_document = languages[language]
        if not isinstance(language_document, dict):
            raise ValueError(f"coverage baseline {language} must be an object")
        _document_summary(language_document.get("overall"), f"{language}.overall")
        modules = language_document.get("modules")
        if not isinstance(modules, dict):
            raise ValueError(f"coverage baseline {language}.modules must be an object")
        if language == DOTNET_LANGUAGE and set(modules) != set(PRODUCTION_MODULES):
            raise ValueError(
                "coverage baseline dotnet.modules must define every production assembly"
            )
        for name, summary in modules.items():
            if not isinstance(name, str) or not name:
                raise ValueError(
                    f"coverage baseline {language} has an invalid module name"
                )
            _document_summary(summary, f"{language}.modules.{name}")

    ratchets = document.get("changedModuleRatchets")
    if not isinstance(ratchets, dict) or set(ratchets) != set(RATCHET_MODULES):
        raise ValueError(
            "coverage baseline must define Domain and Application ratchets"
        )
    for name, ratchet in ratchets.items():
        if not isinstance(ratchet, dict):
            raise ValueError(f"coverage baseline ratchet {name} must be an object")
        baseline_lines = ratchet.get("baselineNonblankLines")
        line_minimum = ratchet.get("lineMinimumPercent")
        branch_minimum = ratchet.get("branchMinimumPercent")
        if not isinstance(baseline_lines, int) or baseline_lines <= 0:
            raise ValueError(
                f"coverage baseline ratchet {name} has invalid baselineNonblankLines"
            )
        if line_minimum != 85 or branch_minimum != 80:
            raise ValueError(
                f"coverage baseline ratchet {name} must use approved 85/80 thresholds"
            )
    return document


def _path_under_root(candidate: Path, root: Path, source: str) -> str:
    """Resolve one report path and reject every spelling outside ``root``."""

    try:
        return candidate.resolve().relative_to(root.resolve()).as_posix()
    except ValueError as exc:
        raise ValueError(
            f"coverage report source is outside the repository root: {source}"
        ) from exc


def _relative_source_path(
    filename: str, root: Path, source_roots: Iterable[str] = ()
) -> str:
    """Resolve a reported source path to one verified repository-relative key."""

    normalized = filename.replace("\\", "/").removeprefix("./")
    if normalized.startswith("/_/"):
        return _path_under_root(root / normalized.removeprefix("/_/"), root, filename)

    candidate = Path(filename)
    if candidate.is_absolute():
        return _path_under_root(candidate, root, filename)

    if normalized.startswith(
        ("src/", "tests/", "tools/", "profiles/", "docs/", "scripts/")
    ):
        return _path_under_root(root / normalized, root, filename)
    candidates: set[str] = set()
    for source_root in source_roots:
        source_root_candidate = Path(source_root)
        if not source_root_candidate.is_absolute():
            source_root_candidate = root / source_root_candidate
        _path_under_root(source_root_candidate, root, source_root)
        candidates.add(
            _path_under_root(source_root_candidate / normalized, root, filename)
        )
    if len(candidates) == 1:
        return candidates.pop()
    if candidates:
        raise ValueError(f"coverage report source is ambiguous: {filename}")
    raise ValueError(f"coverage report source is not repository-relative: {filename}")


def repository_relative_coverage_source(
    filename: str,
    root: Path,
    source_roots: Iterable[str] = (),
) -> str:
    """Expose the canonical coverage source identity for trusted producers."""

    return _relative_source_path(filename, root, source_roots)


def _module_name(relative_path: str, language: str) -> str | None:
    if language == DOTNET_LANGUAGE:
        for name, directories in PRODUCTION_MODULES.items():
            if any(
                relative_path.startswith(f"{directory.as_posix()}/")
                for directory in directories
            ):
                return name
        return None
    if relative_path.startswith(
        ("tools/crc-worker/src/nfc_crc_worker/", "src/nfc_crc_worker/")
    ):
        return "nfc_crc_worker"
    return None


def _is_generated_path(relative_path: str) -> bool:
    return any(
        part.casefold() in {"bin", "obj", "__pycache__"}
        for part in Path(relative_path).parts
    )


def _summary_from_lines(
    lines: Iterable[_CoverageLine],
    branches: Iterable[CoverageMeasure] = (),
) -> CoverageSummary:
    records = tuple(lines)
    branch_records = tuple(branches)
    return CoverageSummary(
        CoverageMeasure(sum(record.line_hit for record in records), len(records)),
        CoverageMeasure(
            sum(branch.covered for branch in branch_records),
            sum(branch.total for branch in branch_records),
        ),
    )


def _coverlet_branch_identity(
    module_name: str,
    class_name: str,
    method_name: str,
    branch: dict[str, Any],
    report: Path,
) -> tuple[int, str, bool]:
    """Return one exact Coverlet JSON branch outcome and its stable identity."""

    names = ("Line", "Offset", "EndOffset", "Path", "Ordinal", "Hits")
    values = tuple(branch.get(name) for name in names)
    if not all(
        isinstance(value, int) and not isinstance(value, bool) for value in values
    ):
        raise ValueError(f"{report} has an invalid Coverlet JSON branch record")
    line, offset, end_offset, path, ordinal, hits = values
    if line <= 0 or min(offset, end_offset, path, ordinal, hits) < 0:
        raise ValueError(f"{report} has an invalid Coverlet JSON branch record")
    identity = "|".join(
        (
            module_name,
            class_name,
            method_name,
            str(line),
            str(offset),
            str(end_offset),
            str(path),
            str(ordinal),
        )
    )
    return line, identity, hits > 0


def _cobertura_branch_measure(
    line_node: element_tree.Element, report: Path
) -> CoverageMeasure | None:
    """Return a branch-bearing line's declared covered and total outcomes."""

    if line_node.get("branch", "").casefold() != "true":
        return None
    condition_coverage = line_node.get("condition-coverage", "")
    match = COBERTURA_BRANCH_COUNT_PATTERN.fullmatch(condition_coverage)
    if match is None:
        raise ValueError(
            f"{report} has an invalid Cobertura branch cardinality: "
            f"{condition_coverage!r}"
        )
    covered = int(match.group("covered"))
    total = int(match.group("total"))
    if total <= 0 or covered > total:
        raise ValueError(
            f"{report} has an invalid Cobertura branch cardinality: "
            f"{condition_coverage!r}"
        )
    return CoverageMeasure(covered, total)


def _cobertura_report_branch_measure(
    document: element_tree.Element,
    report: Path,
) -> CoverageMeasure:
    """Return the collector's report-wide branch evidence totals."""

    covered_value = document.get("branches-covered")
    total_value = document.get("branches-valid")
    try:
        covered = int(covered_value) if covered_value is not None else -1
        total = int(total_value) if total_value is not None else -1
        return CoverageMeasure(covered, total)
    except ValueError as error:
        raise ValueError(
            f"{report} has invalid report-wide Cobertura branch evidence"
        ) from error


def _dotnet_source_line_count(
    relative_path: str,
    root: Path,
    source_line_counts: dict[str, int],
) -> int:
    """Verify one retained report source is a physical owned production file."""

    existing = source_line_counts.get(relative_path)
    if existing is not None:
        return existing
    module = _module_name(relative_path, DOTNET_LANGUAGE)
    suffix = Path(relative_path).suffix.casefold()
    if (
        module is None
        or suffix != ".cs"
        and not (module == "PresentationAvalonia" and suffix == ".axaml")
    ):
        raise ValueError(
            f"coverage report source is not an owned production file: {relative_path}"
        )
    source_path = root / _path_under_root(
        root / relative_path,
        root,
        relative_path,
    )
    if not source_path.is_file():
        raise ValueError(
            f"coverage report source is not a current physical file: {relative_path}"
        )
    line_count = len(source_path.read_text(encoding="utf-8-sig").splitlines())
    source_line_counts[relative_path] = line_count
    return line_count


def _merge_cobertura_report(
    report: Path,
    records_by_path: dict[str, dict[int, _CoverageLine]],
    source_line_counts: dict[str, int],
    root: Path,
) -> tuple[
    dict[tuple[str, str], _CoberturaClassStructure],
    CoverageMeasure,
]:
    """Merge physical lines and retain the paired report's branch structure."""

    document = element_tree.parse(report).getroot()
    source_roots = tuple(
        source.text.strip()
        for source in document.findall("./sources/source")
        if source.text and source.text.strip()
    )
    structures: dict[tuple[str, str], _CoberturaClassStructure] = {}
    for class_node in document.findall(".//class"):
        filename = class_node.get("filename")
        if not filename:
            continue
        relative_path = _relative_source_path(filename, root, source_roots)
        if not relative_path.startswith("src/") or _is_generated_path(relative_path):
            continue
        source_line_count = _dotnet_source_line_count(
            relative_path,
            root,
            source_line_counts,
        )
        class_name = class_node.get("name")
        if not class_name:
            raise ValueError(f"{report} has a production class without a name")
        structure = structures.setdefault(
            (relative_path, class_name),
            _CoberturaClassStructure(set(), {}),
        )
        file_records = records_by_path.setdefault(relative_path, {})
        for line_node in class_node.findall("./lines/line"):
            number = line_node.get("number")
            hits = line_node.get("hits")
            if number is None or hits is None:
                raise ValueError(f"{report} has a coverage line without number or hits")
            line_number = int(number)
            hit_count = int(hits)
            if line_number <= 0 or hit_count < 0:
                raise ValueError(f"{report} has an invalid Cobertura coverage line")
            if line_number > source_line_count:
                raise ValueError(
                    f"{report} coverage line exceeds physical source: "
                    f"{relative_path}:{line_number}"
                )
            structure.lines.add(line_number)
            branch_measure = _cobertura_branch_measure(line_node, report)
            if branch_measure is not None:
                existing_measure = structure.branch_measures.get(line_number)
                if existing_measure is not None and existing_measure != branch_measure:
                    raise ValueError(
                        f"{report} has conflicting Cobertura branch evidence "
                        f"for {relative_path}:{class_name}:{line_number}"
                    )
                structure.branch_measures[line_number] = branch_measure
            existing = file_records.get(line_number)
            if existing is None:
                file_records[line_number] = _CoverageLine(hit_count > 0)
            else:
                file_records[line_number] = _CoverageLine(
                    existing.line_hit or hit_count > 0,
                )
    return structures, _cobertura_report_branch_measure(document, report)


def _merge_coverlet_json_branches(
    reports: Iterable[Path],
    structures_by_report: dict[Path, dict[tuple[str, str], _CoberturaClassStructure]],
    report_branch_measures: dict[Path, CoverageMeasure],
    branch_records_by_path: dict[str, dict[str, CoverageMeasure]],
    source_line_counts: dict[str, int],
    root: Path,
) -> None:
    """Merge exact branch outcomes across paired Coverlet JSON reports."""

    for report in reports:
        structures = structures_by_report[report.parent.resolve()]
        expected_report_measure = report_branch_measures[report.parent.resolve()]
        observed_report_measure = CoverageMeasure(0, 0)
        observed_measures: dict[tuple[str, str, int], CoverageMeasure] = {}
        report_identities: set[tuple[str, str]] = set()
        with report.open(encoding="utf-8") as handle:
            document = json.load(handle)
        if not isinstance(document, dict):
            raise ValueError(f"{report} must contain a Coverlet JSON object")
        for module_name, sources in document.items():
            if not isinstance(module_name, str) or not isinstance(sources, dict):
                raise ValueError(f"{report} has an invalid Coverlet JSON module")
            for filename, classes in sources.items():
                if not isinstance(filename, str) or not isinstance(classes, dict):
                    raise ValueError(f"{report} has an invalid Coverlet JSON source")
                relative_path = _relative_source_path(filename, root)
                retain_source = relative_path.startswith(
                    "src/"
                ) and not _is_generated_path(relative_path)
                source_records = (
                    branch_records_by_path.setdefault(relative_path, {})
                    if retain_source
                    else None
                )
                for class_name, methods in classes.items():
                    if not isinstance(class_name, str) or not isinstance(methods, dict):
                        raise ValueError(f"{report} has an invalid Coverlet JSON class")
                    structure = structures.get((relative_path, class_name))
                    for method_name, method in methods.items():
                        branches = (
                            method.get("Branches") if isinstance(method, dict) else None
                        )
                        if not isinstance(method_name, str) or not isinstance(
                            branches, list
                        ):
                            raise ValueError(
                                f"{report} has an invalid Coverlet JSON method"
                            )
                        for branch in branches:
                            if not isinstance(branch, dict):
                                raise ValueError(
                                    f"{report} has an invalid Coverlet JSON branch record"
                                )
                            line, identity, hit = _coverlet_branch_identity(
                                module_name,
                                class_name,
                                method_name,
                                branch,
                                report,
                            )
                            observed_report_measure = CoverageMeasure(
                                observed_report_measure.covered + int(hit),
                                observed_report_measure.total + 1,
                            )
                            if not retain_source:
                                continue
                            if structure is None:
                                raise ValueError(
                                    f"{report} branch class has no paired Cobertura "
                                    f"class: {relative_path}:{class_name}"
                                )
                            if line not in structure.lines:
                                source_line_count = _dotnet_source_line_count(
                                    relative_path,
                                    root,
                                    source_line_counts,
                                )
                            else:
                                source_line_count = line
                            if line > source_line_count:
                                raise ValueError(
                                    f"{report} branch has no paired coverage/source line: "
                                    f"{relative_path}:{class_name}:{line}"
                                )
                            report_identity = (relative_path, identity)
                            if report_identity in report_identities:
                                raise ValueError(
                                    f"{report} repeats a Coverlet JSON branch identity"
                                )
                            report_identities.add(report_identity)
                            measure_key = (relative_path, class_name, line)
                            observed_measure = observed_measures.get(
                                measure_key, CoverageMeasure(0, 0)
                            )
                            observed_measures[measure_key] = CoverageMeasure(
                                observed_measure.covered + int(hit),
                                observed_measure.total + 1,
                            )
                            observed = source_records.get(identity)
                            source_records[identity] = CoverageMeasure(
                                max(observed.covered if observed else 0, int(hit)),
                                1,
                            )
        if observed_report_measure != expected_report_measure:
            raise ValueError(
                f"{report} overall JSON branch evidence does not match paired "
                f"Cobertura: expected {expected_report_measure.covered}/"
                f"{expected_report_measure.total}, observed "
                f"{observed_report_measure.covered}/{observed_report_measure.total}"
            )
        for (relative_path, class_name), structure in structures.items():
            for line, expected_measure in structure.branch_measures.items():
                observed_measure = observed_measures.get(
                    (relative_path, class_name, line),
                    CoverageMeasure(0, 0),
                )
                if observed_measure != expected_measure:
                    raise ValueError(
                        f"{report} Coverlet JSON branch evidence does not match "
                        f"paired Cobertura for {relative_path}:{class_name}:{line}: "
                        f"expected {expected_measure.covered}/"
                        f"{expected_measure.total}, observed "
                        f"{observed_measure.covered}/{observed_measure.total}"
                    )


def parse_dotnet_cobertura_reports(
    report_root: Path, root: Path = ROOT
) -> CoverageInventory:
    """Merge paired Coverlet reports by physical line and exact branch outcome.

    Cobertura supplies stable physical line evidence. Coverlet JSON supplies
    branch outcome identities that Cobertura's aggregate ``covered/total``
    cannot union correctly across independently instrumented test assemblies.
    """

    reports = sorted(report_root.rglob("coverage.cobertura.xml"))
    if not reports:
        raise ValueError(f"no Cobertura reports found under {report_root}")
    json_reports = sorted(report_root.rglob("coverage.json"))
    cobertura_parents = {report.parent.resolve() for report in reports}
    json_parents = {report.parent.resolve() for report in json_reports}
    if cobertura_parents != json_parents:
        raise ValueError(
            "Coverlet coverage evidence must pair one Cobertura and one JSON "
            "report in every result directory"
        )
    records_by_path: dict[str, dict[int, _CoverageLine]] = {}
    source_line_counts: dict[str, int] = {}
    structures_by_report: dict[
        Path, dict[tuple[str, str], _CoberturaClassStructure]
    ] = {}
    report_branch_measures: dict[Path, CoverageMeasure] = {}
    for report in reports:
        structures, report_branch_measure = _merge_cobertura_report(
            report,
            records_by_path,
            source_line_counts,
            root,
        )
        report_parent = report.parent.resolve()
        structures_by_report[report_parent] = structures
        report_branch_measures[report_parent] = report_branch_measure

    branch_records_by_path: dict[str, dict[str, CoverageMeasure]] = {}
    _merge_coverlet_json_branches(
        json_reports,
        structures_by_report,
        report_branch_measures,
        branch_records_by_path,
        source_line_counts,
        root,
    )
    all_records: list[_CoverageLine] = []
    module_records: dict[str, list[_CoverageLine]] = {}
    all_branch_records: list[CoverageMeasure] = []
    module_branch_records: dict[str, list[CoverageMeasure]] = {}
    for relative_path, source_lines in sorted(records_by_path.items()):
        file_lines = list(source_lines.values())
        all_records.extend(file_lines)
        module = _module_name(relative_path, DOTNET_LANGUAGE)
        if module is not None:
            module_records.setdefault(module, []).extend(file_lines)
    for relative_path, source_branches in sorted(branch_records_by_path.items()):
        file_branches = list(source_branches.values())
        all_branch_records.extend(file_branches)
        module = _module_name(relative_path, DOTNET_LANGUAGE)
        if module is not None:
            module_branch_records.setdefault(module, []).extend(file_branches)
    if not all_records:
        raise ValueError(f"no production source coverage found under {report_root}")
    return CoverageInventory(
        _summary_from_lines(all_records, all_branch_records),
        {
            name: _summary_from_lines(
                lines,
                module_branch_records.get(name, ()),
            )
            for name, lines in module_records.items()
        },
    )


def _canonical_python_source_path(relative_path: str) -> str:
    """Map coverage.py's worker-relative source path to repository ownership."""

    if relative_path.startswith("src/nfc_crc_worker/"):
        return f"tools/crc-worker/{relative_path}"
    return relative_path


def _is_python_runtime_source_path(relative_path: str) -> bool:
    path = PurePosixPath(relative_path)
    return (
        relative_path.startswith("tools/crc-worker/src/nfc_crc_worker/")
        and path.suffix.casefold() == ".py"
        and not any(
            part.casefold() in PYTHON_RUNTIME_EXCLUDED_DIRECTORIES
            for part in path.parts
        )
    )


def _expected_python_runtime_sources(root: Path) -> set[str]:
    source_root = root / "tools/crc-worker/src/nfc_crc_worker"
    if not source_root.is_dir():
        return set()
    expected: set[str] = set()
    for source_path in source_root.rglob("*.py"):
        relative_path = _path_under_root(source_path, root, str(source_path))
        if _is_python_runtime_source_path(relative_path):
            expected.add(relative_path)
    return expected


def parse_python_coverage_report(
    report_path: Path, root: Path = ROOT
) -> CoverageInventory:
    """Parse coverage.py JSON report totals for the CRC worker only."""

    with report_path.open(encoding="utf-8") as handle:
        document = json.load(handle)
    files = document.get("files") if isinstance(document, dict) else None
    if not isinstance(files, dict):
        raise ValueError(f"{report_path} must contain a coverage.py files object")

    summaries_by_path: dict[str, CoverageSummary] = {}
    for filename, item in files.items():
        if not isinstance(filename, str) or not isinstance(item, dict):
            raise ValueError(f"{report_path} has an invalid file entry")
        relative_path = _canonical_python_source_path(
            _relative_source_path(filename, root)
        )
        if not _is_python_runtime_source_path(relative_path):
            continue
        summary = item.get("summary")
        if not isinstance(summary, dict):
            raise ValueError(f"{report_path} file {filename} has no summary")
        values = (
            summary.get("covered_lines"),
            summary.get("num_statements"),
            summary.get("covered_branches"),
            summary.get("num_branches"),
        )
        if not all(
            isinstance(value, int) and not isinstance(value, bool) for value in values
        ):
            raise ValueError(
                f"{report_path} file {filename} has invalid summary values"
            )
        if relative_path in summaries_by_path:
            raise ValueError(
                f"{report_path} contains duplicate source aliases for {relative_path}"
            )
        summaries_by_path[relative_path] = CoverageSummary(
            CoverageMeasure(values[0], values[1]),
            CoverageMeasure(values[2], values[3]),
        )
    if not summaries_by_path:
        raise ValueError(f"{report_path} has no CRC worker source coverage")
    expected_sources = _expected_python_runtime_sources(root)
    if set(summaries_by_path) != expected_sources:
        missing = sorted(expected_sources - set(summaries_by_path))
        unexpected = sorted(set(summaries_by_path) - expected_sources)
        raise ValueError(
            f"{report_path} CRC worker source inventory mismatch: "
            f"missing={missing}, unexpected={unexpected}"
        )
    summaries = tuple(summaries_by_path.values())
    summary = CoverageSummary(
        CoverageMeasure(
            sum(item.lines.covered for item in summaries),
            sum(item.lines.total for item in summaries),
        ),
        CoverageMeasure(
            sum(item.branches.covered for item in summaries),
            sum(item.branches.total for item in summaries),
        ),
    )
    return CoverageInventory(summary, {"nfc_crc_worker": summary})


def changed_lines_from_zero_context_diff(diff: str) -> int:
    """Count additions, removals, and substitutions once per changed source line."""

    total = 0
    for match in HUNK_PATTERN.finditer(diff):
        old_count = int(match.group("old") or 1)
        new_count = int(match.group("new") or 1)
        total += max(old_count, new_count)
    return total


def _module_csharp_pathspecs(module_path: Path) -> tuple[str, ...]:
    """Select only physical C# source, never project metadata or build output."""

    relative = module_path.as_posix()
    return (
        f":(top,glob,icase){relative}/**/*.cs",
        f":(top,glob,icase,exclude){relative}/**/bin/**/*.cs",
        f":(top,glob,icase,exclude){relative}/**/obj/**/*.cs",
    )


def _is_physical_csharp_source(relative_path: str) -> bool:
    path = Path(relative_path)
    return path.suffix.casefold() == ".cs" and not any(
        part.casefold() in {"bin", "obj"} for part in path.parts
    )


def changed_module_lines(root: Path, base_revision: str, module_path: Path) -> int:
    """Measure committed, staged, and untracked source changes from the fixed base."""

    resolved = subprocess.run(
        ["git", "rev-parse", "--verify", f"{base_revision}^{{commit}}"],
        cwd=root,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    if resolved.returncode != 0:
        raise ValueError(
            f"coverage change base {base_revision!r} is unavailable; fetch repository history"
        )
    diff = subprocess.run(
        [
            "git",
            "diff",
            "--no-ext-diff",
            "--no-renames",
            "--unified=0",
            base_revision,
            "--",
            *_module_csharp_pathspecs(module_path),
        ],
        cwd=root,
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    ).stdout
    changed = changed_lines_from_zero_context_diff(diff)
    untracked = subprocess.run(
        ["git", "ls-files", "--others", "--exclude-standard", "--", str(module_path)],
        cwd=root,
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    ).stdout.splitlines()
    for relative_path in untracked:
        candidate = root / relative_path
        if _is_physical_csharp_source(relative_path) and candidate.is_file():
            changed += len(candidate.read_text(encoding="utf-8-sig").splitlines())
    return changed


def _require_no_regression(
    errors: list[str], label: str, actual: CoverageMeasure, baseline: CoverageMeasure
) -> None:
    if not actual.is_at_least(baseline):
        errors.append(
            f"coverage regression: {label} is {actual.percentage_label()}, "
            f"below baseline {baseline.percentage_label()}"
        )


def validate_inventory(
    inventory: CoverageInventory,
    baseline: dict[str, Any],
    language: str,
    changed_lines: dict[str, int] | None = None,
) -> list[str]:
    """Return deterministic policy errors for a language's collected coverage."""

    if language not in LANGUAGES:
        raise ValueError(f"unsupported coverage language: {language}")
    language_baseline = baseline["languages"][language]
    expected_overall = _document_summary(
        language_baseline["overall"], f"{language}.overall"
    )
    errors: list[str] = []
    _require_no_regression(
        errors,
        f"{language} overall line coverage",
        inventory.overall.lines,
        expected_overall.lines,
    )
    _require_no_regression(
        errors,
        f"{language} overall branch coverage",
        inventory.overall.branches,
        expected_overall.branches,
    )

    expected_modules = language_baseline["modules"]
    for name in expected_modules:
        if name not in inventory.modules:
            errors.append(
                f"coverage inventory integrity: {language} baseline module {name} is missing"
            )
    if language != DOTNET_LANGUAGE:
        return errors
    changed_lines = changed_lines or {}
    for name, ratchet in baseline["changedModuleRatchets"].items():
        changed = changed_lines.get(name, 0)
        threshold = min(20, math.ceil(ratchet["baselineNonblankLines"] * 0.10))
        if changed < threshold:
            continue
        actual = inventory.modules.get(name)
        if actual is None:
            errors.append(
                f"coverage ratchet: {name} changed by {changed} lines but has no collected coverage"
            )
            continue
        expected = _document_summary(
            expected_modules.get(name), f"dotnet.modules.{name}"
        )
        _require_no_regression(
            errors, f"{name} changed-module line coverage", actual.lines, expected.lines
        )
        _require_no_regression(
            errors,
            f"{name} changed-module branch coverage",
            actual.branches,
            expected.branches,
        )
        if not actual.lines.meets_percent(ratchet["lineMinimumPercent"]):
            errors.append(
                f"coverage ratchet: {name} changed by {changed} lines and line coverage "
                f"is {actual.lines.percentage_label()}, below "
                f"{ratchet['lineMinimumPercent']}%"
            )
        if not actual.branches.meets_percent(ratchet["branchMinimumPercent"]):
            errors.append(
                f"coverage ratchet: {name} changed by {changed} lines and branch coverage "
                f"is {actual.branches.percentage_label()}, below "
                f"{ratchet['branchMinimumPercent']}%"
            )
    return errors


def verify_coverage(
    language: str,
    report_path: Path,
    *,
    root: Path = ROOT,
    baseline_path: Path = BASELINE_PATH,
) -> CoverageInventory:
    """Parse one language report and fail the canonical verifier on policy drift."""

    baseline = load_baseline(baseline_path)
    if language == DOTNET_LANGUAGE:
        inventory = parse_dotnet_cobertura_reports(report_path, root)
        changed = {
            name: sum(
                changed_module_lines(root, baseline["changeBaseRevision"], module_path)
                for module_path in module_paths
            )
            for name, module_paths in RATCHET_MODULES.items()
        }
    elif language == PYTHON_LANGUAGE:
        inventory = parse_python_coverage_report(report_path, root)
        changed = None
    else:
        raise ValueError(f"unsupported coverage language: {language}")
    errors = validate_inventory(inventory, baseline, language, changed)
    if errors:
        raise RuntimeError("\n".join(errors))
    print(
        f"{language} coverage: lines {inventory.overall.lines.percentage_label()}; "
        f"branches {inventory.overall.branches.percentage_label()}"
    )
    return inventory

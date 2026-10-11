"""Closed public projections of producer-validated .NET shard evidence."""

from __future__ import annotations

from collections.abc import Iterable
import json
from decimal import Decimal
from pathlib import Path, PurePosixPath
import re
import xml.etree.ElementTree as ET

from .read_trx import SAFE_NAME

if __package__ == "scripts.ci":
    from ..coverage_policy import _cobertura_branch_measure
else:
    from coverage_policy import _cobertura_branch_measure

SCHEMA_VERSION = 5
INVALID_SUMMARY = "Public evidence summary is invalid."
OLD_SCHEMA = "Previous public evidence schema is not supported."
WRONG_IDENTITY = "Public evidence identity does not match."
STALE_PROVENANCE = "Public evidence provenance is not current."
INVALID_COUNTERS = "Public evidence counters are inconsistent."
BELOW_FLOOR = "Public evidence does not meet the required floor."
FAILED_SHARD = "Public evidence reports an unsuccessful shard."
INVALID_FILES = "Public evidence file inventory is invalid."
BAD_HASH = "Public evidence hash does not match."
INVALID_COVERAGE = "Public coverage evidence is invalid."
PROJECTION_FAILED = "Public evidence projection could not be created."
STAGING_RESET_FAILED = "Public evidence staging could not be cleared."
RAW_RECEIPT_FAILED = "Runner-local evidence receipt could not be refreshed."
MANIFEST_KEYS = {
    "schemaVersion",
    "kind",
    "sourceSha",
    "runId",
    "runAttempt",
    "sdkVersion",
    "success",
    "shard",
    "producerPlatform",
    "projects",
    "files",
}
PROJECT_KEYS = {
    "relativePath",
    "testAssemblySha256",
    "total",
    "passed",
    "failed",
    "skipped",
    "retry",
    "failedTests",
    "flakyTests",
}
SUMMARY_KEYS = {
    "schemaVersion",
    "shard",
    "commit",
    "outcome",
    "total",
    "passed",
    "failed",
    "skipped",
    "notExecuted",
    "failedTests",
    "exampleData",
    "checks",
}
CHECK_KEYS = {
    "belowFloor",
    "skippedNotZero",
    "requiredClassMissing",
    "updateModeVariableSet",
}
COUNTERS = ("total", "passed", "failed", "skipped")


def closed_keys(value: object, keys: set[str]) -> None:
    if not isinstance(value, dict) or set(value) != keys:
        raise RuntimeError(INVALID_SUMMARY)


def public_method_name(value: str) -> str:
    name = value.split("(", 1)[0].strip().replace("+", ".")
    if SAFE_NAME.fullmatch(name) is None or "." not in name:
        raise RuntimeError(INVALID_SUMMARY)
    return name


def method_names(value: object) -> list[str]:
    if (
        not isinstance(value, list)
        or any(
            not isinstance(name, str) or public_method_name(name) != name
            for name in value
        )
        or value != sorted(set(value))
    ):
        raise RuntimeError(INVALID_SUMMARY)
    return value


def project_method_names(values: Iterable[str]) -> list[str]:
    """Omit non-public identities from name lists without changing counters."""
    names = set()
    for value in values:
        try:
            names.add(public_method_name(value))
        except RuntimeError:
            continue
    return sorted(names)


def _unique_keys(pairs: list[tuple[str, object]]) -> dict:
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(INVALID_SUMMARY)
        result[key] = value
    return result


def read_document(path: Path) -> dict:
    try:
        value = json.loads(
            path.read_text(encoding="utf-8-sig"), object_pairs_hook=_unique_keys
        )
        if not isinstance(value, dict):
            raise ValueError(INVALID_SUMMARY)
        return value
    except (OSError, ValueError, UnicodeError):
        raise RuntimeError(INVALID_SUMMARY) from None


def coverage_paths(shard: str, project_name: str) -> tuple[str, str]:
    root = f"shards/{shard}/coverage/{project_name}"
    return f"{root}/coverage.json", f"{root}/coverage.cobertura.xml"


def allowed_paths(shard: str, project_names: list[str]) -> set[str]:
    return {
        f"shards/{shard}/manifest.json",
        f"shards/{shard}/shard-summary.json",
        *(path for name in project_names for path in coverage_paths(shard, name)),
    }


def unexpected_files(count: int) -> str:
    return f"Public evidence contains unexpected files (count: {count})."


def _counters(value: dict, *, unfinished: bool = False) -> None:
    names = (*COUNTERS, "notExecuted") if unfinished else COUNTERS
    if any(type(value[name]) is not int or value[name] < 0 for name in names):
        raise RuntimeError(INVALID_COUNTERS)
    if value["total"] != sum(value[name] for name in names[1:]):
        raise RuntimeError(INVALID_COUNTERS)


def validate_documents(
    manifest: dict,
    summary: dict,
    *,
    shard: str,
    project_paths: list[str],
    source_sha: str,
    run_id: str,
    run_attempt: int,
    sdk_version: str,
    platform: str,
    floor: dict,
    require_success: bool = False,
) -> None:
    if type(manifest.get("schemaVersion")) is not int:
        raise RuntimeError(INVALID_SUMMARY)
    if manifest["schemaVersion"] < SCHEMA_VERSION:
        raise RuntimeError(OLD_SCHEMA)
    if manifest["schemaVersion"] > SCHEMA_VERSION:
        raise RuntimeError(INVALID_SUMMARY)
    closed_keys(manifest, MANIFEST_KEYS)
    closed_keys(summary, SUMMARY_KEYS)
    if (
        type(summary["schemaVersion"]) is not int
        or summary["schemaVersion"] != 1
        or type(manifest["success"]) is not bool
    ):
        raise RuntimeError(INVALID_SUMMARY)
    if (
        manifest["kind"] != "dotnet-test-shard"
        or manifest["shard"] != shard
        or manifest["sourceSha"] != source_sha
        or manifest["sdkVersion"] != sdk_version
        or manifest["producerPlatform"] != platform
        or summary["shard"] != shard
        or summary["commit"] != source_sha
    ):
        raise RuntimeError(WRONG_IDENTITY)
    if (
        manifest["runId"] != run_id
        or type(manifest["runAttempt"]) is not int
        or manifest["runAttempt"] != run_attempt
    ):
        raise RuntimeError(STALE_PROVENANCE)
    rows = manifest["projects"]
    if not isinstance(rows, list):
        raise RuntimeError(INVALID_SUMMARY)
    seen = []
    for row in rows:
        closed_keys(row, PROJECT_KEYS)
        seen.append(row["relativePath"])
        if (
            not isinstance(row["testAssemblySha256"], str)
            or re.fullmatch(r"[0-9a-f]{64}", row["testAssemblySha256"]) is None
        ):
            raise RuntimeError(INVALID_SUMMARY)
        _counters(row)
        if manifest["success"] and row["total"] == 0:
            raise RuntimeError(INVALID_COUNTERS)
        failed, flaky = (
            method_names(row["failedTests"]),
            method_names(row["flakyTests"]),
        )
        closed_keys(row["retry"], {"retried", "recoveredTests"})
        retry = row["retry"]
        if (
            type(retry["retried"]) is not bool
            or type(retry["recoveredTests"]) is not int
            or not len(flaky) <= retry["recoveredTests"] <= row["passed"]
            or (not retry["retried"] and (flaky or retry["recoveredTests"]))
            or len(failed) > row["failed"]
            or (
                manifest["success"]
                and retry["retried"]
                and (retry["recoveredTests"] == 0 or not flaky)
            )
            or (
                row["relativePath"]
                == "tests/NvtFwCombiner.GoldenRegression.Tests/NvtFwCombiner.GoldenRegression.Tests.csproj"
                and retry["retried"]
            )
            or set(failed).intersection(flaky)
        ):
            raise RuntimeError(INVALID_SUMMARY)
    if (
        any(not isinstance(path, str) for path in seen)
        or len(set(seen)) != len(seen)
        or seen != [path for path in project_paths if path in seen]
        or ((manifest["success"] or require_success) and seen != project_paths)
    ):
        raise RuntimeError(INVALID_SUMMARY)
    _counters(summary, unfinished=True)
    if any(
        summary[name] != sum(row[name] for row in rows) for name in COUNTERS
    ) or method_names(summary["failedTests"]) != sorted(
        {name for row in rows for name in row["failedTests"]}
    ):
        raise RuntimeError(INVALID_COUNTERS)
    closed_keys(summary["exampleData"], {"present", "executed"})
    example = summary["exampleData"]
    closed_keys(summary["checks"], CHECK_KEYS)
    checks = summary["checks"]
    if (
        type(example["present"]) is not bool
        or type(example["executed"]) is not int
        or not 0 <= example["executed"] <= summary["total"]
        or any(type(flag) is not bool for flag in checks.values())
        or summary["outcome"] not in {"passed", "failed", "unverified"}
        or manifest["success"] != (summary["outcome"] == "passed")
    ):
        raise RuntimeError(INVALID_SUMMARY)
    below = (
        summary["total"] < floor["minTotal"]
        or example["executed"] < floor["exampleMinTests"]
        or (not example["present"] and bool(floor["requiredClasses"]))
    )
    if (
        checks["belowFloor"] != below
        or checks["skippedNotZero"] != (summary["skipped"] != 0)
        or (not floor["requiredClasses"] and checks["requiredClassMissing"])
    ):
        raise RuntimeError(INVALID_SUMMARY)
    if (manifest["success"] or require_success) and below:
        raise RuntimeError(BELOW_FLOOR)
    if manifest["success"] and (
        summary["failed"]
        or summary["skipped"]
        or summary["notExecuted"]
        or any(checks.values())
    ):
        raise RuntimeError(FAILED_SHARD)
    if require_success and not manifest["success"]:
        raise RuntimeError(FAILED_SHARD)


def _production_source(value: str, production_directories: set[str]) -> bool:
    path = PurePosixPath(value)
    return any(
        path.is_relative_to(root) for root in production_directories
    ) and not any(
        part.casefold() in {"bin", "obj", "__pycache__"} for part in path.parts
    )


def project_coverage(
    json_report: Path, cobertura_report: Path, production_directories: set[str]
) -> None:
    """Filter normalized copies by the coverage policy's production roots."""
    try:
        modules = {PurePosixPath(path).name for path in production_directories}
        document = {
            module: {
                filename: classes
                for filename, classes in sources.items()
                if _production_source(filename, production_directories)
            }
            for module, sources in read_document(json_report).items()
            if module.removesuffix(".dll") in modules
        }
        document = {module: sources for module, sources in document.items() if sources}
        text = cobertura_report.read_text(encoding="utf-8-sig")
        if "<!DOCTYPE" in text or "<!ENTITY" in text:
            raise ValueError(INVALID_COVERAGE)
        tree = ET.ElementTree(
            ET.fromstring(
                text,
                parser=ET.XMLParser(
                    target=ET.TreeBuilder(insert_comments=True, insert_pis=True)
                ),
            )
        )
        root = tree.getroot()
        for packages in root.findall("./packages"):
            for package in packages.findall("./package"):
                if package.get("name", "").removesuffix(".dll") not in modules:
                    packages.remove(package)
                    continue
                for classes in package.findall("./classes"):
                    for cls in classes.findall("./class"):
                        if not _production_source(
                            cls.get("filename", ""), production_directories
                        ):
                            classes.remove(cls)
                if not package.findall("./classes/class"):
                    packages.remove(package)
        for line in root.findall(".//line"):
            if line.get("branch") in {"True", "False"}:
                line.set("branch", line.get("branch").lower())
        json_report.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")
        tree.write(cobertura_report, encoding="utf-8", xml_declaration=True)
        # Validate before using numeric payload, including malformed production lines.
        validate_coverage(json_report, cobertura_report, production_directories)
        for parent in [root, *root.findall("./packages/package")]:
            classes = (
                parent.findall("./classes/class")
                if parent is not root
                else root.findall("./packages/package/classes/class")
            )
            lines = [line for cls in classes for line in cls.findall("./lines/line")]
            covered_lines = sum(int(line.get("hits", "0")) > 0 for line in lines)
            for name, value in (
                ("lines-valid", str(len(lines))),
                ("lines-covered", str(covered_lines)),
                ("line-rate", f"{covered_lines / len(lines) if lines else 0:.4f}"),
            ):
                if name in parent.attrib:
                    parent.set(name, value)
            if "complexity" in parent.attrib:
                parent.set(
                    "complexity",
                    format(
                        sum(
                            (Decimal(cls.get("complexity", "0")) for cls in classes),
                            Decimal(0),
                        ),
                        "f",
                    ),
                )
            if parent is not root and "branch-rate" in parent.attrib:
                measures = [
                    measure
                    for line in lines
                    if (measure := _cobertura_branch_measure(line, cobertura_report))
                    is not None
                ]
                covered = sum(measure.covered for measure in measures)
                total = sum(measure.total for measure in measures)
                parent.set("branch-rate", f"{covered / total if total else 0:.4f}")
        if "branches-valid" in root.attrib or "branches-covered" in root.attrib:
            branches = [
                branch
                for sources in document.values()
                for classes in sources.values()
                for methods in classes.values()
                for counts in methods.values()
                for branch in counts["Branches"]
            ]
            covered = sum(branch["Hits"] > 0 for branch in branches)
            root.set("branches-valid", str(len(branches)))
            root.set("branches-covered", str(covered))
            if "branch-rate" in root.attrib:
                root.set(
                    "branch-rate", f"{covered / len(branches) if branches else 0:.4f}"
                )
        tree.write(cobertura_report, encoding="utf-8", xml_declaration=True)
    except (
        OSError,
        ArithmeticError,
        ValueError,
        TypeError,
        KeyError,
        AttributeError,
        ET.ParseError,
        RuntimeError,
    ):
        raise RuntimeError(INVALID_COVERAGE) from None


def validate_coverage(
    json_report: Path, cobertura_report: Path, production_directories: set[str]
) -> None:
    """Admit only the collector's product identities and numeric coverage fields."""
    try:
        document = read_document(json_report)
        modules = {PurePosixPath(path).name for path in production_directories}

        def source(value):
            path = PurePosixPath(value)
            return (
                path.as_posix() == value
                and not path.is_absolute()
                and ".." not in path.parts
                and path.suffix in {".cs", ".axaml"}
                and _production_source(value, production_directories)
            )

        def identity(value):
            return (
                isinstance(value, str)
                and bool(value)
                and re.fullmatch(r"[A-Za-z0-9_.+`<>:,()\[\]&* /!?=|$-]+", value)
                is not None
                and not re.search(r"(?:^|[./])Tests(?:[./:+]|$)", value)
            )

        for module, sources in document.items():
            if module.removesuffix(".dll") not in modules or not isinstance(
                sources, dict
            ):
                raise ValueError(INVALID_COVERAGE)
            for filename, classes in sources.items():
                if not source(filename) or not isinstance(classes, dict):
                    raise ValueError(INVALID_COVERAGE)
                for cls, methods in classes.items():
                    if not identity(cls) or not isinstance(methods, dict):
                        raise ValueError(INVALID_COVERAGE)
                    for method, counts in methods.items():
                        if not identity(method) or set(counts) != {"Lines", "Branches"}:
                            raise ValueError(INVALID_COVERAGE)
                        if not isinstance(counts["Lines"], dict) or not isinstance(
                            counts["Branches"], list
                        ):
                            raise ValueError(INVALID_COVERAGE)
                        for line, hits in counts["Lines"].items():
                            if (
                                re.fullmatch(r"[1-9][0-9]*", line) is None
                                or type(hits) is not int
                                or hits < 0
                            ):
                                raise ValueError(INVALID_COVERAGE)
                        for branch in counts["Branches"]:
                            if set(branch) != {
                                "Line",
                                "Offset",
                                "EndOffset",
                                "Path",
                                "Ordinal",
                                "Hits",
                            } or any(
                                type(v) is not int or v < 0 for v in branch.values()
                            ):
                                raise ValueError(INVALID_COVERAGE)
        text = cobertura_report.read_text(encoding="utf-8-sig")
        if "<!DOCTYPE" in text or "<!ENTITY" in text:
            raise ValueError(INVALID_COVERAGE)
        root = ET.fromstring(
            text,
            parser=ET.XMLParser(
                target=ET.TreeBuilder(insert_comments=True, insert_pis=True)
            ),
        )
        edges = {
            "coverage": {"sources", "packages"},
            "sources": {"source"},
            "packages": {"package"},
            "package": {"classes"},
            "classes": {"class"},
            "class": {"methods", "lines"},
            "methods": {"method"},
            "method": {"lines"},
            "lines": {"line"},
            "line": {"conditions"},
            "conditions": {"condition"},
            "condition": set(),
            "source": set(),
        }
        metrics = {"line-rate", "branch-rate", "complexity"}
        attributes = {
            "coverage": metrics
            | {
                "version",
                "timestamp",
                "lines-covered",
                "lines-valid",
                "branches-covered",
                "branches-valid",
            },
            "package": metrics | {"name"},
            "class": metrics | {"name", "filename"},
            "method": metrics | {"name", "signature"},
            "line": {"number", "hits", "branch", "condition-coverage"},
            "condition": {"number", "type", "coverage"},
        }
        if root.tag != "coverage":
            raise ValueError(INVALID_COVERAGE)
        for node in root.iter():
            if node.tag not in edges or any(
                child.tag not in edges[node.tag] for child in node
            ):
                raise ValueError(INVALID_COVERAGE)
            if (
                node.text
                and node.text.strip()
                and (node.tag != "source" or node.text != ".")
            ):
                raise ValueError(INVALID_COVERAGE)
            if node.tail and node.tail.strip():
                raise ValueError(INVALID_COVERAGE)
            if set(node.attrib) - attributes.get(node.tag, set()):
                raise ValueError(INVALID_COVERAGE)
            for key, value in node.attrib.items():
                if key == "filename":
                    valid = source(value)
                elif key in {"name", "signature"}:
                    valid = identity(value) and (
                        node.tag != "package" or value.removesuffix(".dll") in modules
                    )
                elif key == "branch":
                    valid = value in {"true", "false"}
                elif key == "type":
                    valid = value in {"jump", "switch"}
                else:
                    valid = (
                        re.fullmatch(
                            r"[0-9]+(?:\.[0-9]+)?(?:%\s*(?:\([0-9]+/[0-9]+\))?)?", value
                        )
                        is not None
                    )
                if not valid:
                    raise ValueError(INVALID_COVERAGE)
            if node.tag == "line":
                _cobertura_branch_measure(node, cobertura_report)
    except (
        OSError,
        ValueError,
        TypeError,
        KeyError,
        AttributeError,
        ET.ParseError,
        RuntimeError,
    ):
        raise RuntimeError(INVALID_COVERAGE) from None

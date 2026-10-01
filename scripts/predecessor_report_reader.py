"""Versioned CLI report format projection for the predecessor comparator.

No process, Git, capture validation or outcome classification lives here.
The caller applies sequence, compiled-authority and range checks unchanged,
in contract order, using the typed Preview of the same side as authority.
Unknown optional members are recorded as JSON pointers, never as values.
"""

from __future__ import annotations

from typing import Any, Mapping, NamedTuple

try:
    from scripts.v0916_parity_certification import (
        ParityError,
        normalize_raw_mutation,
        normalize_raw_operation,
    )
except ModuleNotFoundError as error:
    if error.name != "scripts":
        raise
    from v0916_parity_certification import (  # type: ignore[no-redef]
        ParityError,
        normalize_raw_mutation,
        normalize_raw_operation,
    )


READER_VERSIONS = {"v0916": "cli-v0916-v1", "1x": "cli-1x-v1"}
REPORT_MEMBERS = frozenset({
    "RunId", "ProfileId", "ProfileVersion", "IcId", "ModeId", "ExperienceId", "CompositionKind",
    "StartedAtUtc", "CompletedAtUtc", "Inputs", "Operations", "Mutations", "Issues", "Output",
    "OutputDifferences", "CompilationFingerprint", "Validations", "OutputNaming",
})
OPERATION_MEMBERS = frozenset({
    "OperationId", "Sequence", "Kind", "Status", "SourceSpaceId", "SourceRange", "TargetSpaceId",
    "TargetRange", "OverlapPolicy", "ProcessorId", "ToolBindingId", "ProcessorAllowedReadRanges",
    "ProcessorAllowedWriteRanges", "ExecutedCommands", "Reason", "Provenance",
})
MUTATION_MEMBERS = frozenset({
    "OperationId", "Kind", "TargetSpaceId", "TargetRange", "ChangedByteCount", "BeforeSha256",
    "AfterSha256", "Reason",
})
RANGE_MEMBERS = frozenset({"Start", "Length", "EndExclusive"})
PROVENANCE_MEMBERS = frozenset({"Kind", "SourceId", "SourceVersion"})
COMMAND_MEMBERS = frozenset({"ExecutablePath", "WorkingDirectory", "Arguments"})
INPUT_MEMBERS = frozenset({"AddressSpaceId", "ArtifactId", "Size", "Sha256"})
OUTPUT_MEMBERS = frozenset({"Size", "Sha256", "Committed"})
ISSUE_MEMBERS = frozenset({"Code", "Severity"})
ISSUE_SEVERITIES = frozenset({"error", "info", "unspecified", "warning"})


class ReportReaderError(ValueError):
    """A missing report, unsupported version or malformed format; never a rejection."""

    code = "PREDECESSOR_REPORT_INVALID"


class ReadReport(NamedTuple):
    """Format-only projection and payload-free metadata, with no semantic verdict."""

    reader_version: str
    projection: dict[str, Any]
    context: dict[str, Any]
    issues: list[dict[str, str]]
    unknown_members: list[str]


def _pointer(parent: str, member: str) -> str:
    return f"{parent}/{member.replace('~', '~0').replace('/', '~1')}"


def _members(
    raw: Any, required: frozenset[str], path: str, unknown: list[str],
    optional: frozenset[str] = frozenset(),
) -> dict[str, Any]:
    if not isinstance(raw, Mapping) or any(not isinstance(key, str) for key in raw) or not required <= raw.keys():
        raise ReportReaderError(f"missing or malformed members at {path or '/'}")
    unknown.extend(_pointer(path, key) for key in raw.keys() - required - optional)
    return {key: raw[key] for key in required}


def _rows(raw: Any, path: str) -> list[Any]:
    if not isinstance(raw, list):
        raise ReportReaderError(f"expected array at {path}")
    return raw


def _range(raw: Any, path: str, unknown: list[str]) -> dict[str, Any] | None:
    return None if raw is None else _members(raw, RANGE_MEMBERS, path, unknown)


def _operation(raw: Any, path: str, unknown: list[str]) -> dict[str, Any]:
    row = _members(raw, OPERATION_MEMBERS, path, unknown)
    for key in ("SourceRange", "TargetRange"):
        row[key] = _range(row[key], _pointer(path, key), unknown)
    for key in ("ProcessorAllowedReadRanges", "ProcessorAllowedWriteRanges"):
        row[key] = [_range(span, f"{path}/{key}/{index}", unknown)
                    for index, span in enumerate(_rows(row[key], f"{path}/{key}"))]
    row["Provenance"] = _members(row["Provenance"], PROVENANCE_MEMBERS, f"{path}/Provenance", unknown)
    row["ExecutedCommands"] = [
        _members(command, COMMAND_MEMBERS, f"{path}/ExecutedCommands/{index}", unknown)
        for index, command in enumerate(_rows(row["ExecutedCommands"], f"{path}/ExecutedCommands"))
    ]
    return normalize_raw_operation(row)


def _mutation(raw: Any, path: str, unknown: list[str]) -> dict[str, Any]:
    row = _members(raw, MUTATION_MEMBERS, path, unknown)
    row["TargetRange"] = _range(row["TargetRange"], f"{path}/TargetRange", unknown)
    return normalize_raw_mutation(row)


def _issue(raw: Any, path: str, unknown: list[str]) -> dict[str, str]:
    row = _members(raw, ISSUE_MEMBERS, path, unknown, frozenset({"Message"}))
    if not isinstance(row["Code"], str) or not row["Code"] or not isinstance(row["Severity"], str):
        raise ReportReaderError(f"malformed issue at {path}")
    severity = row["Severity"].lower()
    if severity not in ISSUE_SEVERITIES:
        raise ReportReaderError(f"unknown issue severity at {path}")
    return {"code": row["Code"], "severity": severity, "source": "report"}


def read_cli_report(raw: Mapping[str, Any], *, report_version: str) -> ReadReport:
    """Read a written `v0916` or `1x` CLI report, selected by executor version.

    MapId is optional and is never inferred. AbMergeFormat, SourceEnvelope
    and future optional members are recorded only by name. Issues come only
    from this report; stderr and missing-report handling belong to the caller.
    The caller loads JSON with the ADR 0057 duplicate-rejecting loader.
    """

    if report_version not in READER_VERSIONS:
        raise ReportReaderError("unsupported CLI report version")
    unknown: list[str] = []
    report = _members(raw, REPORT_MEMBERS, "", unknown, frozenset({"MapId"}))
    try:
        operations = [_operation(row, f"/Operations/{index}", unknown)
                      for index, row in enumerate(_rows(report["Operations"], "/Operations"))]
        mutations = [_mutation(row, f"/Mutations/{index}", unknown)
                     for index, row in enumerate(_rows(report["Mutations"], "/Mutations"))]
        inputs = []
        for index, row in enumerate(_rows(report["Inputs"], "/Inputs")):
            item = _members(row, INPUT_MEMBERS, f"/Inputs/{index}", unknown, frozenset({"OriginalFileName"}))
            inputs.append({"addressSpaceId": item["AddressSpaceId"], "artifactId": item["ArtifactId"],
                           "size": item["Size"], "sha256": item["Sha256"]})
        issues = [_issue(row, f"/Issues/{index}", unknown)
                  for index, row in enumerate(_rows(report["Issues"], "/Issues"))]
        output = report["Output"]
        if output is not None:
            output = _members(output, OUTPUT_MEMBERS, "/Output", unknown, frozenset({"FileName"}))
            output = {"size": output["Size"], "sha256": output["Sha256"], "committed": output["Committed"]}
    except (ParityError, KeyError, TypeError, ValueError, AttributeError) as error:
        if isinstance(error, ReportReaderError):
            raise
        raise ReportReaderError("malformed CLI report format") from error
    projection = {"compiledOperations": operations, "compiledMutations": mutations,
                  "compilationFingerprint": report["CompilationFingerprint"]}
    context = {
        "runId": report["RunId"], "profileId": report["ProfileId"], "profileVersion": report["ProfileVersion"],
        "icId": report["IcId"], "modeId": report["ModeId"], "experienceId": report["ExperienceId"],
        "mapId": raw.get("MapId"), "compositionKind": report["CompositionKind"],
        "startedAtUtc": report["StartedAtUtc"], "completedAtUtc": report["CompletedAtUtc"],
        "orderedInputs": inputs, "output": output, "issueCount": len(issues),
    }
    return ReadReport(READER_VERSIONS[report_version], projection, context, issues, sorted(unknown))

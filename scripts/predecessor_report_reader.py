"""Versioned CLI report format projection for the predecessor comparator.

No process, Git, capture validation or outcome classification lives here.
The caller applies sequence, compiled-authority and range checks unchanged,
in contract order, using the typed Preview of the same side as authority.
Unknown optional members are recorded as JSON pointers, never as values;
an unknown member of an operation or mutation row is refused. Executed commands
are read in the shape a CLI writes them, and of an output difference only its
range is read: its content previews are never read or kept.
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
PROVENANCE_MEMBERS = frozenset({"Kind", "SourceId", "SourceVersion"})
COMMAND_MEMBERS = frozenset({"ExecutablePath", "WorkingDirectory", "Arguments"})
INPUT_MEMBERS = frozenset({"AddressSpaceId", "ArtifactId", "Size", "Sha256"})
OUTPUT_MEMBERS = frozenset({"Size", "Sha256", "Committed"})
ISSUE_MEMBERS = frozenset({"Code", "Severity"})
ISSUE_PRESENTATION_MEMBERS = frozenset({"Message", "OperationId"})
ISSUE_SEVERITIES = frozenset({"error", "info", "unspecified", "warning"})
RANGE_MEMBERS = frozenset({"Start", "Length", "EndExclusive"})


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


def _exact(raw: Any, members: frozenset[str], path: str) -> None:
    if not isinstance(raw, Mapping) or set(raw) != members:
        raise ReportReaderError(f"missing or unknown members at {path}")


def _operation(raw: Any, path: str) -> dict[str, Any]:
    """Operation rows carry write authority: an unknown member is refused, never dropped.

    The ADR 0057 normalizer keeps its exact-member rule for the row and its
    ranges; provenance and command objects, which it reads by name, are held
    to the same rule here.
    """

    if not isinstance(raw, Mapping):
        raise ReportReaderError(f"malformed operation at {path}")
    _exact(raw.get("Provenance"), PROVENANCE_MEMBERS, f"{path}/Provenance")
    for index, command in enumerate(_rows(raw.get("ExecutedCommands"), f"{path}/ExecutedCommands")):
        _exact(command, COMMAND_MEMBERS, f"{path}/ExecutedCommands/{index}")
    return normalize_raw_operation(raw, written_commands=True)


def _executed_commands(operations: Any) -> list[dict[str, Any]]:
    """Where each command ran, for the caller's check against the tools and directory it staged."""

    rows = []
    for operation in operations:
        for sequence, command in enumerate(operation["ExecutedCommands"]):
            located = {"operationId": operation["OperationId"], "sequence": sequence,
                       "executablePath": command["ExecutablePath"], "workingDirectory": command["WorkingDirectory"]}
            if not all(isinstance(located[member], str) for member in ("executablePath", "workingDirectory")):
                raise ReportReaderError("malformed executed command")
            rows.append(located)
    return rows


def _output_difference_range(raw: Any, path: str) -> dict[str, int]:
    """Only the range of an output difference; no other member of the row is read."""

    span = raw.get("Range") if isinstance(raw, Mapping) else None
    if (not isinstance(span, Mapping) or set(span) != RANGE_MEMBERS
            or any(type(span[member]) is not int for member in RANGE_MEMBERS)
            or span["Length"] != span["EndExclusive"] - span["Start"]):
        raise ReportReaderError(f"malformed output difference range at {path}")
    return {"start": span["Start"], "endExclusive": span["EndExclusive"]}


def _issue(raw: Any, path: str, unknown: list[str]) -> dict[str, str]:
    row = _members(raw, ISSUE_MEMBERS, path, unknown, ISSUE_PRESENTATION_MEMBERS)
    if not isinstance(row["Code"], str) or not row["Code"] or not isinstance(row["Severity"], str):
        raise ReportReaderError(f"malformed issue at {path}")
    severity = row["Severity"].lower()
    if severity not in ISSUE_SEVERITIES:
        raise ReportReaderError(f"unknown issue severity at {path}")
    return {"code": row["Code"], "severity": severity, "source": "report"}


def read_cli_report(raw: Mapping[str, Any], *, report_version: str) -> ReadReport:
    """Read a written `v0916` or `1x` CLI report, selected by executor version.

    MapId is optional and is never inferred. AbMergeFormat, SourceEnvelope
    and future optional members outside operation and mutation rows are
    recorded only by name; inside those rows they are refused. Issues come only
    from this report; stderr and missing-report handling belong to the caller.
    The context also carries where each command ran and the ranges of the
    output differences, for the caller's checks.
    The caller loads JSON with the ADR 0057 duplicate-rejecting loader.
    """

    if report_version not in READER_VERSIONS:
        raise ReportReaderError("unsupported CLI report version")
    unknown: list[str] = []
    report = _members(raw, REPORT_MEMBERS, "", unknown, frozenset({"MapId"}))
    try:
        operations = [_operation(row, f"/Operations/{index}")
                      for index, row in enumerate(_rows(report["Operations"], "/Operations"))]
        mutations = [normalize_raw_mutation(row) for row in _rows(report["Mutations"], "/Mutations")]
        commands = _executed_commands(report["Operations"])
        differences = [_output_difference_range(row, f"/OutputDifferences/{index}")
                       for index, row in enumerate(_rows(report["OutputDifferences"], "/OutputDifferences"))]
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
        "executedCommands": commands, "outputDifferenceRanges": differences,
    }
    return ReadReport(READER_VERSIONS[report_version], projection, context, issues, sorted(unknown))

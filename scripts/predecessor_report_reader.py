"""CLI report reading and owner-list projection for the predecessor comparator.

No process, Git, capture validation or outcome classification lives here.
The caller applies sequence, compiled-authority and range checks unchanged,
in contract order, using the typed Preview of the same side as authority.
Unknown optional members are recorded as JSON pointers, never as values;
an unknown member of an operation or mutation row is refused. Executed commands
are read in the shape a CLI writes them, and of an output difference only its
range is read: its content previews are never read or kept.
"""

from __future__ import annotations

import argparse
import hashlib
import html
from pathlib import Path
import re
import sys
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


# These are presentation labels, not outcome classification or approval rules.
VERDICT_LABELS = {
    "equal": "equal（完整輸出相同）", "different": "different（完整輸出或前置輸出不同）",
    "baseline-rejects": "baseline-rejects（基準拒絕輸入）",
    "candidate-rejects": "candidate-rejects（候選版拒絕輸入）",
    "both-reject": "both-reject（兩側均拒絕輸入）", "invalid": "invalid（執行或證據無效）",
    "consistent": "consistent（符合該項證明）", "inconsistent": "inconsistent（不符合該項證明）",
    "not-covered": "not-covered（未比較）",
}
GAP_LABELS = {
    "debt-set": "無 canonical 認證案例；歷史 debt set",
    "accepted-gap": "無 canonical 認證案例；本版已列為 accepted gap（仍須核對本版核准）",
    "pending-gap": "無 canonical 認證案例；待本版 owner 核准",
}


def _owner_text(value: Any) -> str:
    """Plain table text: redact absolute local paths; escape Markdown and HTML."""
    text = str(value)
    path = r"(?:[A-Za-z]:[\\/]|\\\\|/)[^\s\"'<>|]*"
    text = re.sub(r'["\'](?:[A-Za-z]:[\\/]|\\\\|/)[^"\']*["\']',
                  "[local path omitted]", text)
    text = re.sub(r"(?<![\w:/])" + path, "[local path omitted]", text)
    text = " ".join(text.split())
    return html.escape(text, quote=False).replace("|", "&#124;").replace("`", "&#96;")


def _side_label(side: Mapping[str, Any] | None) -> str:
    if side is None:
        return "—"
    label = side["status"]
    if side["stoppedAt"] is not None:
        label += f" at {side['stoppedAt']}"
    codes = sorted({issue["code"] for issue in side["issues"] if issue["severity"] == "error"})
    return label + (": " + ", ".join(codes) if codes else "")


def _range_lines(comparison: Mapping[str, Any] | None, scope: str) -> list[str]:
    if comparison is None:
        return []
    lines = [f"- {scope} 不同 bytes：{comparison['differentByteCount']}；範圍數：{comparison['rangeCount']}；"
             f"range-list SHA-256：{comparison['rangeListSha256']}。"]
    lines.extend(f"  - {scope} [{row['start']}, {row['endExclusive']})"
                 for row in sorted(comparison["ranges"], key=lambda row: (row["start"], row["endExclusive"])))
    if comparison["rangesTruncated"]:
        lines.append("  - 僅列出結果保留的前段範圍；完整宣告邊界須讀取該次綁定的 declaration 或 plan/amendment。")
    return lines


def _list_rows(report: Mapping[str, Any]) -> list[dict[str, Any]]:
    """Adapt the two existing result shapes without deriving a new verdict."""
    if report["mode"] == "rolling":
        return [{"route": row["routeId"], "scenario": row["scenarioId"], "verdict": row["outcome"],
                 "proof": f"inputRevision {row['inputRevision']}", "record": row}
                for row in report["scenarios"]]
    return [{"route": row["planRouteId"], "scenario": row["planRouteId"], "verdict": row["result"],
             "proof": row["proofKind"], "record": row} for row in report["routes"]]


def _check_list_input(report: Mapping[str, Any], rows: list[dict[str, Any]]) -> None:
    """Projection admission only; cannot validate execution or release authority."""
    from scripts import predecessor_validation as validation

    if (report["schemaVersion"] != "1.0" or report["kind"] != "predecessor-comparison-report"
            or report["certification"] != "none" or report["terminal"] is not False
            or type(report["formal"]) is not bool):
        raise ReportReaderError("unsupported predecessor comparison result")
    if validation.deterministic_digest_failures(report):
        raise ReportReaderError("comparison result digest mismatch")
    if not rows:
        raise ReportReaderError("comparison result has no comparison units")
    if len({row["scenario"] for row in rows}) != len(rows):
        raise ReportReaderError("duplicate comparison unit")
    if any(row["verdict"] not in VERDICT_LABELS for row in rows):
        raise ReportReaderError("unknown recorded verdict")
    if report["mode"] == "rolling":
        coverage = report["coverage"]
        gate = report["gate"]
        expected_gate = validation.rolling_gate([validation.Failure(**row) for row in gate["failures"]])
        if (gate["result"] != expected_gate["result"]
                or gate["result"] == "clear" and any(
                    row["verdict"] == "invalid" or
                    row["verdict"] != "equal" and row["record"]["declarationEntryId"] is None for row in rows)):
            raise ReportReaderError("recorded rolling gate contradicts comparison units or failures")
        covered = {row["route"] for row in rows}
        gaps = [row["routeId"] for row in coverage["notCovered"]]
        if (coverage["scenarios"] != len(rows) or coverage["coveredRoutes"] != len(covered)
                or len(set(gaps)) != len(gaps) or covered & set(gaps)
                or coverage["universe"] != len(covered) + len(gaps)):
            raise ReportReaderError("incomplete recorded coverage")
    else:
        results = [row["verdict"] for row in rows]
        if report["summary"] != validation.v0916_summary(results):
            raise ReportReaderError("incomplete recorded historical coverage")
        if (report["result"] != validation.v0916_result(results)
                or bool(report["failures"]) != (report["result"] != "consistent")):
            raise ReportReaderError("recorded historical result contradicts comparison units or failures")


def _bound_list_documents(report: Mapping[str, Any], documents: Mapping[str, bytes]) -> dict[str, Any]:
    """Read only explicitly supplied documents whose raw bytes the result binds."""
    from scripts.v0916_parity_certification import load_json_reject_duplicates

    hashes = {row["path"]: row["sha256"] for row in report["comparator"]["contracts"]}
    expected = {"declaration": report.get("declarationSha256"),
                "amendment": report.get("amendmentSha256"),
                "plan": hashes.get("docs/contracts/v0916-parity-certification-v1.json")}
    parsed = {}
    for name, raw in documents.items():
        if expected.get(name) is None or hashlib.sha256(raw).hexdigest() != expected[name]:
            raise ReportReaderError("supplemental disposition does not match result binding")
        parsed[name] = load_json_reject_duplicates(raw)
    return parsed


def _declared_list_lines(record: Mapping[str, Any], documents: Mapping[str, Any]) -> list[str]:
    """Display exact source rows; no declaration matching or approval verdict."""
    reference = record.get("dispositionRow")
    entry = record.get("declarationEntryId")
    if entry:
        source = documents.get("declaration")
        if source is None:
            return ["- 完整宣告邊界未載入；結果的 entry 引用不代替 declaration 核對。"]
        matches = [row for row in source["entries"] if row["id"] == entry]
    elif reference:
        source = documents.get(reference["source"])
        if source is None:
            return ["- 完整宣告邊界未載入；須核對結果綁定的 plan/amendment row。"]
        matches = [row for row in source[reference["member"]] if row["routeId"] == reference["routeId"]]
    else:
        return []
    if len(matches) != 1:
        raise ReportReaderError("referenced disposition row is missing or duplicated")
    row = matches[0]
    lines = ["- 結果綁定文件的完整宣告邊界（不代表本次已重現；仍看結果裁定）："]
    if entry:
        lines.append("  - kind: " + _owner_text(row["kind"]))
        for scope, difference in sorted(row["differences"].items()):
            if difference is not None:
                lines.append(f"  - {scope} 不同 bytes：{difference['differentByteCount']}；範圍數：{difference['rangeCount']}")
                lines.extend(f"    - {scope} output-image [{span['start']}, {span['endExclusive']})"
                             for span in sorted(difference["ranges"], key=lambda span: span["start"]))
        for name, side in sorted(row["expected"].items()):
            lines.append("  - " + _owner_text(f"expected {name}: {side['result']}; stage: {side['stage']}; issueCodes: {side['issueCodes']}"))
            for scope in ("output", "precursor"):
                artifact = side[scope]
                if artifact is not None:
                    lines.append(f"    - {scope}: size {artifact['size']}; SHA-256 {artifact['sha256']}")
    else:
        if "differentRanges" in row:
            lines.append(f"  - 不同 bytes：{row['differentByteCount']}")
            lines.extend(f"    - output-image [{span['start']}, {span['endExclusive']})"
                         for span in sorted(row["differentRanges"], key=lambda span: span["start"]))
        for member in ("ownerDecision", "boardDecision", "scope", "reason"):
            if member in row:
                lines.append(f"  - {member}: {_owner_text(row[member])}")
        for member in ("baselineOutput", "candidateOutput", "baselinePrecursor", "candidatePrecursor"):
            artifact = row.get(member)
            if artifact is not None:
                lines.append(f"  - {member}: size {artifact['size']}; SHA-256 {artifact['sha256']}")
        if "expectedBaseline" in row:
            rejection = row["expectedBaseline"]
            lines.append("  - " + _owner_text(f"baseline rejection: {rejection['rejectingStage']}; issueCodes: {rejection['issueCodes']}"))
            for name in ("expectedBaseline", "expectedCandidate"):
                for member in ("precursorOutput", "output"):
                    artifact = row[name].get(member)
                    if artifact is not None:
                        lines.append(f"  - {name} {member}: size {artifact['size']}; SHA-256 {artifact['sha256']}")
            lines.append("  - " + _owner_text(f"canonical binding: {row['binding']}"))
    return lines


def render_owner_list(
    report: Mapping[str, Any], *, candidate_policy: Mapping[str, Any] | None = None,
    bound_documents: Mapping[str, bytes] | None = None,
) -> str:
    """Deterministic, payload-free owner projection of one recorded mode.

    No source acquisition or per-side safety rerun. Declaration references and
    observed bounds are reported as recorded, never treated as new approvals.
    An optional candidate policy is a supplemental catalogue, not run evidence.
    """
    from scripts import predecessor_validation as validation

    try:
        if report["mode"] not in {"rolling", "v0916-1x"}:
            raise ReportReaderError("unsupported comparison mode")
        rows = _list_rows(report)
        _check_list_input(report, rows)
        documents = _bound_list_documents(report, bound_documents or {})
        rows.sort(key=lambda row: (row["route"], row["scenario"]))
        rolling = report["mode"] == "rolling"
        verdict = report["gate"]["result"] if rolling else report["result"]
        status = "formal result；此清單不核發 report of record" if report["formal"] else "diagnostic rehearsal, not a report of record"
        lines = ["# Predecessor coverage and difference list", "", f"Status: {status}", "",
                 f"Mode: {report['mode']}; candidate: {report['candidate']['version']}; baseline: {report['baseline']['tag']}; result: {verdict}.",
                 "certification: none; terminal: false", "",
                 f"Candidate commit: {report['candidate']['executor']['commit']}",
                 f"Result deterministic SHA-256: {report['deterministicSha256']}", "",
                 "此清單未重新比較 bytes，也不核准 gap 或差異。宣告引用及裁定沿用結果；範圍是觀察值，不擴大核准邊界。",
                 "正式證據與本版 owner 核准仍須另行確認；過去版本的 gap 核准不沿用。", ""]
        if not rolling:
            lines.extend([f"Milestone: {report['milestone']}", "",
                          "歷史模式每個 plan route 是一個比較單位；proofKind 說明其情境。", ""])
        coverage = report["coverage"] if rolling else report["summary"]
        lines.extend(["## Coverage", "", "; ".join(f"{key}: {value}" for key, value in sorted(coverage.items()) if type(value) is int), "",
                      "| Route | Scenario | Verdict | 情境／未比較原因 | Baseline | Candidate |",
                      "| --- | --- | --- | --- | --- | --- |"])
        table_rows = []
        for row in rows:
            record = row["record"]
            reason = "無 canonical 認證案例；plan 列為 not-covered" if row["verdict"] == "not-covered" else row["proof"]
            cells = (row["route"], row["scenario"], VERDICT_LABELS[row["verdict"]], reason,
                     _side_label(record["baseline"]), _side_label(record["candidate"]))
            table_rows.append((row["route"], row["scenario"], "| " + " | ".join(_owner_text(cell) for cell in cells) + " |"))
        if rolling:
            for gap in sorted(coverage["notCovered"], key=lambda row: row["routeId"]):
                reason = GAP_LABELS[gap["reason"]] + f"；evidenceKind: {gap['evidenceKind']}"
                table_rows.append((gap["routeId"], "", f"| {_owner_text(gap['routeId'])} | — | not-covered（未比較） | {_owner_text(reason)} | — | — |"))
        lines.extend(line for _, _, line in sorted(table_rows))
        represented = {row["route"] for row in rows} | ({gap["routeId"] for gap in coverage["notCovered"]} if rolling else set())
        if candidate_policy is not None:
            published = validation.universe_routes(candidate_policy)
            lines.extend(["", "候選版 published routes 補充目錄：只按 routeId 列出，不推定 renamed route；不作為該次執行的 authority。", "",
                          "| Route | Scenario | 未比較原因 |", "| --- | --- | --- |"])
            for route in sorted(published - represented):
                label = "unlisted（結果未列入；須核對 coverage）" if rolling else "outside-mode（不在本模式 plan route 集合）"
                lines.append(f"| {_owner_text(route)} | — | {label} |")
            if not published - represented:
                lines.append("| — | — | 補充目錄沒有其他 route |")
        else:
            lines.extend(["", "未提供候選版 policy 補充目錄；只列出結果的模式範圍，未宣稱包含模式外 routes。"])
        lines.extend(["", "## Differences and rejected inputs", "",
                      "所有範圍採具名 address space 的半開區間；precursor 為前置 Standard Merge 輸出。", ""])
        differences = 0
        for row in rows:
            record = row["record"]
            disposition = record.get("dispositionRow")
            if (row["verdict"] in {"equal", "not-covered"} or
                    (row["verdict"] == "consistent" and disposition is None and record["comparison"] is None)):
                continue
            differences += 1
            entry = record.get("declarationEntryId")
            declared = f"已引用宣告 {entry}" if entry else (
                f"已引用 {disposition['source']}/{disposition['member']}（裁定仍為 {row['verdict']}）" if disposition else
                "未宣告差異" if row["verdict"] not in {"invalid", "consistent"} else "無可接受差異裁定")
            lines.extend([f"### {_owner_text(row['scenario'])}", "", f"{_owner_text(declared)}；{VERDICT_LABELS[row['verdict']]}。", ""])
            for name in ("baseline", "candidate"):
                side = record[name]
                lines.append(f"- {name}: {_owner_text(_side_label(side))}")
                if side is not None:
                    for scope in ("output", "precursor"):
                        artifact = side[scope]
                        if artifact is not None:
                            lines.append(f"  - {scope}: size {artifact['size']}; SHA-256 {artifact['sha256']}")
                    if side["status"] == "rejected":
                        lines.append("  - 拒絕輸入沒有可比較的完整輸出；這是 acceptance 差異，不能算 equal。")
            lines.extend(_range_lines(record["comparison"], "output-image"))
            lines.extend(_range_lines(record.get("precursorComparison"), "precursor output-image"))
            lines.extend(_declared_list_lines(record, documents))
            if record["failureCode"] is not None:
                lines.append(f"- 無效／不一致原因碼：{record['failureCode']}")
            lines.append("")
        if not differences:
            lines.extend(["結果未列出 byte 或 acceptance 差異。", ""])
        lines.extend(["## Failures and coverage changes", ""])
        failures = report["gate"]["failures"] if rolling else report["failures"]
        for failure in sorted(failures, key=lambda row: (row["subject"], row["code"], row["detail"])):
            lines.append("- " + _owner_text(f"{failure['subject']}: {failure['code']} — {failure['detail']}"))
        if not failures:
            lines.append("結果未列出 failure。")
        if rolling:
            for change in sorted(coverage["changesSinceBaseline"], key=lambda row: (row["kind"], row["subject"])):
                lines.append("- " + _owner_text(f"{change['kind']}: {change['subject']}; declaration: {change['declarationEntryId']}"))
        lines.extend(["", "## Informational differences", "", "這些欄位沿用結果的 informational 分類，不是 byte／acceptance 差異。", ""])
        info = [(row["scenario"], item) for row in rows for item in row["record"]["informational"]]
        for scenario, item in sorted(info, key=lambda pair: (pair[0], pair[1]["field"])):
            lines.append("- " + _owner_text(f"{scenario}: {item['field']}; baseline={item['baseline']}; candidate={item['candidate']}"))
        if not info:
            lines.append("結果未列出 informational 差異。")
        return "\n".join(lines) + "\n"
    except (KeyError, TypeError, ValueError, AttributeError) as error:
        if isinstance(error, ReportReaderError):
            raise
        raise ReportReaderError("malformed comparison result or supplemental catalogue") from error


def owner_list_main(argv: list[str]) -> int:
    from scripts.v0916_parity_certification import load_json_reject_duplicates

    parser = argparse.ArgumentParser(description="Write an owner list from an existing comparison result; no execution")
    parser.add_argument("--result", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--candidate-policy", type=Path, help="optional supplemental published route catalogue; not run evidence")
    for name in ("declaration", "plan", "amendment"):
        parser.add_argument(f"--{name}", type=Path, help="optional result-bound document for complete declared bounds")
    args = parser.parse_args(argv)
    try:
        raw = args.result.read_bytes()
        report = load_json_reject_duplicates(raw)
        policy_raw = None if args.candidate_policy is None else args.candidate_policy.read_bytes()
        policy = None if policy_raw is None else load_json_reject_duplicates(policy_raw)
        documents = {name: getattr(args, name).read_bytes() for name in ("declaration", "plan", "amendment")
                     if getattr(args, name) is not None}
        text = render_owner_list(report, candidate_policy=policy, bound_documents=documents)
        text += f"\nResult file SHA-256: {hashlib.sha256(raw).hexdigest()}\n"
        if policy_raw is not None:
            text += f"Supplemental catalogue SHA-256: {hashlib.sha256(policy_raw).hexdigest()}\n"
        for name, payload in sorted(documents.items()):
            text += f"Bound {name} SHA-256: {hashlib.sha256(payload).hexdigest()}\n"
        # Exclusive creation keeps previous owner-review lists intact.
        with args.output.open("x", encoding="utf-8", newline="\n") as stream:
            stream.write(text)
        return 0
    except (OSError, ParityError, ValueError, TypeError):
        print("PREDECESSOR_REPORT_INVALID: cannot write owner list (malformed input or unavailable output)", file=sys.stderr)
        return 1

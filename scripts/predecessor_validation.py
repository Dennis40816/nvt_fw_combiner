"""Single semantic validator of the predecessor comparison (ADR 0078).

The report and declaration schemas hold the rules that can be decided inside
one document. This module holds every rule that spans documents or compares
values: the coverage ledger against the capability policy, the canonical
manifest, the case manifests and the ADR 0057 plan; a rolling report against
the ledger and the baseline tag's ledger; a release declaration against the
ledger, the CHANGELOG and the run; the v0.9.16 1.x mode against the plan and
the amendment; and the values inside a report that a schema cannot compare.

Every function takes documents and computed evidence that the caller has
already loaded or measured (case manifests as a mapping, byte comparisons as
complete range lists, transitive proofs as the ADR 0057 primitive's own
result) and does no Git, file or process access. Every check returns failures
with the contract's codes and never raises for a finding.
"""

from __future__ import annotations

import re
import copy
import hashlib
from datetime import datetime
from pathlib import PurePath
from typing import Any, Iterable, Mapping, NamedTuple, Sequence

try:
    from scripts.render_release_notes import render_release_notes
    from scripts.v0916_parity_certification import (
        ParityError,
        ReportRequestIdentity,
        canonical_json_sha256,
        cli_selection_token,
        compare_approved_semantic_correction_payloads,
        compare_transitive_payloads,
        expected_report_identity,
        report_identity_mismatches,
        validate_report_sequence,
        validate_report_projection_against_compiled_authority,
        validate_semantic_report_ranges,
        _contained,
    )
except ModuleNotFoundError as error:
    if error.name != "scripts":
        raise
    # `python ./scripts/predecessor_comparison.py` puts the scripts directory,
    # not the repository root, on sys.path.
    from render_release_notes import render_release_notes  # type: ignore[no-redef]
    from v0916_parity_certification import (  # type: ignore[no-redef]
        ParityError,
        ReportRequestIdentity,
        canonical_json_sha256,
        cli_selection_token,
        compare_approved_semantic_correction_payloads,
        compare_transitive_payloads,
        expected_report_identity,
        report_identity_mismatches,
        validate_report_sequence,
        validate_report_projection_against_compiled_authority,
        validate_semantic_report_ranges,
        _contained,
    )


REPORTED_RANGE_LIMIT = 32
SCOPES = ("output", "precursor")
UNIVERSE_PUBLICATION = frozenset({"candidate", "supported"})
OUTCOME_ENTRY_KINDS = {
    "different": "byte-difference",
    "baseline-rejects": "baseline-rejects",
    "candidate-rejects": "candidate-rejects",
    "both-reject": "both-reject",
}
COVERAGE_ENTRY_KINDS = frozenset({"accepted-gap", "input-revision", "scenario-retired"})
# Stages before a Standard Merge precursor exists; a side stopped at one has none.
PRECURSOR_STAGES = frozenset({"precursor-preview", "precursor-build"})
PROCESS_FAILURE_ISSUE_CODES = frozenset({"external-tool.process.failed", "external-tool.process.start-failed"})
# 1.2.x board decision 261: the address spaces a report uses without declaring a capacity, and the
# address space whose changed ranges a report lists as output differences.
WORK_ADDRESS_SPACES = frozenset({"ab-combiner-work", "tp-b-work"})
V0916_WORK_ADDRESS_SPACES = frozenset({"a-bank-work", "b-bank-work"})
OUTPUT_ADDRESS_SPACE = "output-image"
# The codes of the shared execution failures (contract section "Shared
# execution"; the report schema's scenarioFailureCode): an executor, the
# environment, a staged input, a process, or a report or per-side safety check.
# A v0.9.16 route is invalid only with one of them.
EXECUTION_FAILURE_CODES = frozenset(
    {
        "PREDECESSOR_ENVIRONMENT_INVALID",
        "PREDECESSOR_EXECUTOR_INVALID",
        "PREDECESSOR_INPUT_INVALID",
        "PREDECESSOR_PROCESS_FAILED",
        "PREDECESSOR_REPORT_INVALID",
    }
)
TRANSITIVE_CHECKS = (
    "candidateTpEqualsCandidateFullPrefix",
    "candidateTpEqualsBaselineFullPrefix",
    "candidateFullTailImmutable",
)
ENTRY_ID = re.compile(r"RP-(?P<version>(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*))-(?P<number>[0-9]{2,})\Z")

# Computed evidence of one scenario or route: for each scope both sides can
# be compared in, the complete differing range list, or None when the bytes
# are equal. A scope a side lacks is absent from the mapping.
ScopeEvidence = Mapping[str, "Sequence[Mapping[str, int]] | None"]


class Failure(NamedTuple):
    """One finding, with a failure code of the predecessor comparison contract."""

    code: str
    subject: str
    detail: str


def _failure(code: str, subject: str, detail: str) -> Failure:
    return Failure(f"PREDECESSOR_{code}", subject, detail)


class SideProcessEvidence(NamedTuple):
    """Measured process and reader projection; no file or process access."""

    process: Mapping[str, Any]
    projection: Mapping[str, Any] | None
    context: Mapping[str, Any] | None
    issues: Sequence[Mapping[str, str]]
    inputs: Sequence[Mapping[str, Any]]
    output: Mapping[str, Any] | None
    failures: Sequence[Failure]
    settings_present: bool
    # The external tool files the comparator staged and hash-checked for this process, and the
    # temporary directory it created for it; an executed command is held to both.
    staged_tools: Sequence[str] = ()
    temporary_directory: str | None = None
    expected_identity: ReportRequestIdentity | None = None


class SideVerdict(NamedTuple):
    status: str
    stopped_at: str | None
    failures: list[Failure]


def pending_execution_interfaces(
    contract: Mapping[str, Any], amendment: Mapping[str, Any] | None = None,
) -> list[str]:
    """Only statuses explicitly in effect admit a formal execution."""

    pending = [name for name, value in contract["interfaces"].items()
               if isinstance(value, Mapping) and value.get("status") != "in-effect"]
    if contract["executor"]["compilerHost"].get("status") != "in-effect":
        pending.append("compilerHost")
    if amendment is not None and amendment["baselineExecutor"].get("status") != "in-effect":
        pending.append("baselineExecutor")
    return sorted(pending)


def execution_mode_failures(contract: Mapping[str, Any], mode: str) -> list[Failure]:
    return [] if mode in contract["modes"] else [_failure("CONTRACT_PENDING", mode, "unknown comparison mode")]


def executor_compiler_host_failures(compiler_host: Mapping[str, Any]) -> list[Failure]:
    """Admit closed compiler-host settings and the comparator-only runtime pin approved by decisions 275 and 277."""
    if compiler_host.get("status") != "in-effect":
        return []
    required = compiler_host.get("requiredRuntime")
    arguments = compiler_host.get("extraBuildArguments")
    if (isinstance(required, Mapping) and isinstance(arguments, list)
            and [argument for argument in arguments if isinstance(argument, str)
                 and argument.startswith("-p:RuntimeFrameworkVersion=")]
            != ["-p:RuntimeFrameworkVersion=" + str(required.get("version"))]):
        return [_failure("EXECUTOR_INVALID", "compilerHost", "RuntimeFrameworkVersion must equal requiredRuntime.version")]
    expected = {
        "status": "in-effect", "boardDecisions": ["1.1.12 board decision 79", "1.2.x board decision 275", "1.2.x board decision 277"],
        "requiredRuntime": {"framework": "Microsoft.NETCore.App", "version": "10.0.11", "architecture": "x64"},
        "environmentVariables": {"DOTNET_ROLL_FORWARD": "Disable"},
        "extraBuildArguments": ["-p:UseSharedCompilation=false", "-nodeReuse:false", "-p:RuntimeFrameworkVersion=10.0.11"],
        "missingRuntimePolicy": "refuse",
        "verification": {"kind": "embedded-portable-pdb", "scope": "first-party-cli-project-graph",
                         "runtimeVersion": "10.0.11-servicing.26373.116+e2f47b0110ed922f21a1522da67279133ce28f32"},
    }
    return ([] if dict(compiler_host) == expected else
            [_failure("EXECUTOR_INVALID", "compilerHost", "incomplete or unsupported compiler-host settings")])


BASELINE_EXECUTOR_V2_PATH = "docs/contracts/v0916-baseline-executor-v2.json"


def baseline_executor_binding_failures(baseline: Mapping[str, Any], raw: bytes | None = None) -> list[Failure]:
    """Check the amendment's closed activation and, when supplied, raw bytes."""
    if baseline.get("status") != "in-effect":
        return []
    binding = baseline.get("contract")
    valid = (set(baseline) == {"status", "boardDecisions", "contract"}
             and baseline.get("boardDecisions") == ["1.1.12 board decision 63", "1.1.12 board decision 79",
                                                     "1.2.x board decision 275", "1.2.x board decision 277"]
             and isinstance(binding, Mapping) and set(binding) == {"path", "size", "sha256"}
             and binding.get("path") == BASELINE_EXECUTOR_V2_PATH
             and type(binding.get("size")) is int and binding["size"] > 0
             and isinstance(binding.get("sha256"), str) and re.fullmatch(r"[0-9a-f]{64}", binding["sha256"]))
    if not valid:
        return [_failure("SOURCE_MISMATCH", "baselineExecutor", "incomplete baseline executor contract binding")]
    if raw is not None and (len(raw), hashlib.sha256(raw).hexdigest()) != (binding["size"], binding["sha256"]):
        return [_failure("SOURCE_MISMATCH", binding["path"], "baseline executor contract bytes differ from binding")]
    return []


def v0916_executor_contract_failures(record: Mapping[str, Any]) -> list[Failure]:
    """Closed v2 structure and cross-member rules, before materialization."""
    def closed(value: Any, keys: set[str]) -> bool:
        return isinstance(value, Mapping) and set(value) == keys

    def path_valid(value: Any) -> bool:
        return (isinstance(value, str) and bool(value) and not any(char in value for char in "\\:")
                and all(part not in ("", ".", "..") for part in value.split("/")))

    def artifact(value: Any, extra: set[str] = frozenset()) -> bool:
        return (closed(value, {"path", "size", "sha256"} | extra) and path_valid(value["path"])
                and type(value["size"]) is int and value["size"] > 0
                and isinstance(value["sha256"], str) and bool(re.fullmatch(r"[0-9a-f]{64}", value["sha256"])))

    try:
        keys = {"schemaVersion", "kind", "certification", "terminal", "materialization", "platform", "source",
                "toolchain", "lockFiles", "externalTools", "restore", "build", "compilerHost", "lockFileRewrites",
                "lockFileDiff", "managedAssemblies", "cliAssembly", "runtimeClosure", "v1Relation"}
        valid = (set(record) == keys and record["schemaVersion"] == "2.0"
                 and record["kind"] == "exact-tag-source-built-cli" and record["certification"] == "none"
                 and record["terminal"] is False and record["materialization"] == "fresh-detached-git-worktree"
                 and record["platform"] == "windows-x64" and record["source"]["cleanTreeRequired"] is True
                 and closed(record["source"], {"tag", "tagObject", "peeledCommit", "sourceTree", "cleanTreeRequired"})
                 and all(isinstance(record["source"][key], str) and re.fullmatch(r"[0-9a-f]{40}", record["source"][key])
                         for key in ("tagObject", "peeledCommit", "sourceTree"))
                 and closed(record["toolchain"], {"resolvedSdkVersion", "globalJson"})
                 and artifact(record["toolchain"]["globalJson"]) and record["toolchain"]["globalJson"]["path"] == "global.json"
                 and record["source"]["tag"] == "v0.9.16" and record["toolchain"]["resolvedSdkVersion"] == "10.0.303"
                 and record["compilerHost"]["status"] == "in-effect"
                 and not executor_compiler_host_failures(record["compilerHost"]))
        for member in ("lockFiles", "externalTools", "lockFileRewrites", "managedAssemblies"):
            items = record[member]
            extra = {"explanationClasses"} if member == "lockFileRewrites" else set()
            valid = (valid and isinstance(items, list) and len(items) == 7
                     and all(artifact(row, extra) for row in items) and len({row["path"] for row in items}) == 7)
        for row in record["lockFileRewrites"]:
            explanations = row["explanationClasses"]
            valid = valid and isinstance(explanations, list) and len(explanations) in (2, 3)
            valid = valid and set(explanations) in (
                {"add-empty-win-x64-target", "windows-nuget-serialization"},
                {"add-empty-win-x64-target", "windows-nuget-serialization", "refresh-first-party-project-ranges"})
            valid = valid and len(set(explanations)) == len(explanations)
        project = "src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj"
        commands = {"restore": ["dotnet", "restore", project, "--force-evaluate", "--runtime", "win-x64"],
                    "build": ["dotnet", "build", project, "--configuration", "Release", "--runtime", "win-x64",
                              "--self-contained", "true", "--no-restore", "-p:ContinuousIntegrationBuild=true",
                              "-p:PathMap={sourceRoot}=/_/src"]}
        valid = valid and all(record[action] == {"workingDirectory": ".", "arguments": arguments}
                              for action, arguments in commands.items())
        valid = valid and {row["path"] for row in record["lockFiles"]} == {row["path"] for row in record["lockFileRewrites"]}
        diff = record["lockFileDiff"]
        valid = valid and closed(diff, {"format", "text", "size", "sha256"}) and type(diff["size"]) is int and diff["size"] > 0
        diff_bytes = diff["text"].encode("utf-8")
        valid = valid and diff["format"] == "unified-diff-lf" and b"\r" not in diff_bytes
        valid = valid and (len(diff_bytes), hashlib.sha256(diff_bytes).hexdigest()) == (diff["size"], diff["sha256"])
        relation = record["v1Relation"]
        valid = (valid and closed(relation, {"contract", "relation", "differingFiles"}) and artifact(relation["contract"])
                 and relation["contract"]["path"] == "docs/contracts/v0916-baseline-executor-v1.json"
                 and relation["relation"] == "identical-runtime-closure" and relation["differingFiles"] == [])
        closure = record["runtimeClosure"]
        root = "src/NvtFwCombiner.Cli/bin/Release/net10.0/win-x64"
        valid = (valid and closed(closure, {"root", "fileCount", "totalSize", "sha256"}) and closure["root"] == root
                 and all(type(closure[key]) is int and closure[key] > 0 for key in ("fileCount", "totalSize"))
                 and isinstance(closure["sha256"], str) and bool(re.fullmatch(r"[0-9a-f]{64}", closure["sha256"]))
                 and artifact(record["cliAssembly"]) and record["cliAssembly"]["path"] == root + "/NvtFwCombiner.Cli.exe"
                 and closure["totalSize"] >= record["cliAssembly"]["size"])
        valid = valid and all(row["path"].endswith(".dll") and "/" not in row["path"] for row in record["managedAssemblies"])
        return [] if valid else [_failure("EXECUTOR_INVALID", "baselineExecutor", "invalid v2 executor contract")]
    except (KeyError, TypeError, ValueError, AttributeError):
        return [_failure("EXECUTOR_INVALID", "baselineExecutor", "incomplete v2 executor contract")]


def executor_tag_failures(
    tag_object: str, observed_tag_object: str, peeled_commit: str, commit: str,
) -> list[Failure]:
    if (not re.fullmatch(r"[0-9a-f]{40}", tag_object)
            or observed_tag_object != tag_object or peeled_commit != commit):
        return [_failure("EXECUTOR_INVALID", tag_object, "annotated tag object does not peel to the built commit")]
    return []


def execution_environment_failures(
    *, temporary_root_length: int, maximum_length: int, formal: bool,
    before: Mapping[str, str | None], after: Mapping[str, str | None] | None = None,
    subject: str = "environment",
) -> list[Failure]:
    failures: list[Failure] = []
    if not 0 < temporary_root_length <= maximum_length:
        failures.append(_failure("ENVIRONMENT_INVALID", subject, "temporary root exceeds its length bound"))
    if formal and any(value is not None for value in (after if after is not None else before).values()):
        failures.append(_failure("ENVIRONMENT_INVALID", subject, "per-user settings present in a formal run"))
    if after is not None and dict(before) != dict(after):
        failures.append(_failure("ENVIRONMENT_INVALID", subject, "per-user settings changed"))
    return failures


def input_capture_failures(
    expected: Sequence[Mapping[str, Any]], before: Sequence[Mapping[str, Any] | None],
    after: Sequence[Mapping[str, Any] | None], subject: str,
) -> list[Failure]:
    identities = [_identity(item) for item in expected]
    if identities != list(before) or list(before) != list(after):
        return [_failure("INPUT_INVALID", subject, "staged input differs from admission or changed during process")]
    return []


def executor_source_failures(
    *, commit: str, observed_commit: str, dirty_paths: Sequence[str],
    build_paths: Sequence[str], tracked_paths: Sequence[str], forbidden_segments: Sequence[str],
) -> list[Failure]:
    failures: list[Failure] = []
    if not re.fullmatch(r"[0-9a-f]{40}", commit) or observed_commit != commit:
        failures.append(_failure("EXECUTOR_INVALID", commit, "worktree is not the exact commit"))
    if dirty_paths:
        failures.append(_failure("EXECUTOR_INVALID", commit, "worktree is dirty"))
    if build_paths or any(set(path.split("/")) & set(forbidden_segments) for path in tracked_paths):
        failures.append(_failure("EXECUTOR_INVALID", commit, "pre-existing bin or obj path"))
    return failures


def executor_lock_failures(
    expected: Mapping[str, bytes], observed: Mapping[str, bytes | None], subject: str,
) -> list[Failure]:
    if dict(expected) != dict(observed):
        return [_failure("EXECUTOR_INVALID", subject, "lock file differs from its Git blob")]
    return []


def executor_process_failures(process: Mapping[str, Any]) -> list[Failure]:
    if process["timedOut"] or process["exitCode"] != 0:
        return [_failure("EXECUTOR_INVALID", process["stage"], "SDK resolution, restore or build failed")]
    return []


def executor_sdk_failures(version: str) -> list[Failure]:
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version):
        return [_failure("EXECUTOR_INVALID", "sdk", "SDK resolution did not return a version")]
    return []


def baseline_identity_failures(
    rebuilt: Mapping[str, Any], own_report: Any, *, baseline_version: str,
) -> list[Failure]:
    """Compare a rolling rebuild with the release's own candidate.executor.

    The tag is assigned after the candidate report; its null tagObject is
    admitted, but a recorded non-null tag must be the rebuilt tag object.
    No CLI profile, compilation fingerprint or output identity identifies
    the program. The caller supplies the already measured 1.x Executor.
    """
    candidate = own_report.get("candidate") if isinstance(own_report, Mapping) else None
    recorded = candidate.get("executor") if isinstance(candidate, Mapping) else None
    if not isinstance(recorded, Mapping):
        return [_failure("BASELINE_IDENTITY_MISSING", "candidate.executor", "baseline own report records no executor identity")]

    def compare(expected: Any, observed: Any, path: str) -> list[Failure]:
        if observed is None:
            return [_failure("BASELINE_IDENTITY_MISSING", path, "recorded identity value is missing")]
        if isinstance(expected, Mapping) and isinstance(observed, Mapping):
            failures = []
            for member, value in expected.items():
                failures.extend(compare(value, observed.get(member), f"{path}.{member}"))
            if set(observed) - set(expected):
                failures.append(_failure("BASELINE_IDENTITY_MISMATCH", path, "recorded identity has unexpected members"))
            return failures
        if type(observed) is not type(expected) or observed != expected:
            return [_failure("BASELINE_IDENTITY_MISMATCH", path, "rebuilt and recorded identity values differ")]
        return []

    failures = compare({key: value for key, value in rebuilt.items() if key != "tagObject"},
                       {key: value for key, value in recorded.items() if key != "tagObject"}, "candidate.executor")
    if "tagObject" not in recorded:
        failures.append(_failure("BASELINE_IDENTITY_MISSING", "candidate.executor.tagObject", "recorded tag member is missing"))
    elif recorded["tagObject"] is not None and recorded["tagObject"] != rebuilt["tagObject"]:
        failures.append(_failure("BASELINE_IDENTITY_MISMATCH", "candidate.executor.tagObject", "recorded tag differs from baseline tag"))
    if candidate.get("version") is None:
        failures.append(_failure("BASELINE_IDENTITY_MISSING", "candidate.version", "recorded candidate version is missing"))
    elif candidate["version"] != baseline_version:
        failures.append(_failure("BASELINE_IDENTITY_MISMATCH", "candidate.version", "recorded version differs from baseline version"))
    if (own_report.get("schemaVersion") != "1.0" or own_report.get("kind") != "predecessor-comparison-report"
            or own_report.get("mode") not in ("rolling", "v0916-1x")
            or own_report.get("certification") != "none" or own_report.get("terminal") is not False):
        failures.append(_failure("BASELINE_IDENTITY_MISMATCH", "baseline-report", "not a non-terminal predecessor report"))
    return failures


def executor_closure_failures(expected: Mapping[str, str], observed: Mapping[str, str | None]) -> list[Failure]:
    if dict(expected) != dict(observed):
        return [_failure("EXECUTOR_INVALID", "closure", "execution closure changed or disappeared")]
    return []


def _side_capture_failures(evidence: SideProcessEvidence) -> list[Failure]:
    assert evidence.context is not None
    subject = evidence.process["stage"]
    expected_inputs = [
        {"size": item["size"], "sha256": item["sha256"]} for item in evidence.inputs
    ]
    actual_inputs = [_identity(item) for item in evidence.context["orderedInputs"]]
    if actual_inputs != expected_inputs:
        return [_failure("REPORT_INVALID", subject, "report input identities differ from capture")]
    for reported, captured in zip(evidence.context["orderedInputs"], evidence.inputs):
        if (reported["artifactId"] != captured.get("expectedReportArtifactId")
                or reported["addressSpaceId"] != captured.get("expectedReportAddressSpaceId")):
            return [_failure("REPORT_INVALID", subject, "report input binding differs from capture")]
    reported_output = evidence.context["output"]
    # A Preview, or a run that stops before it writes, describes the output it would write with
    # `Committed: false` and leaves no file; only a file identity can agree or disagree with a capture.
    described_only = (evidence.output is None and reported_output is not None
                      and reported_output["committed"] is False)
    if not described_only and _identity(reported_output) != _identity(evidence.output):
        return [_failure("REPORT_INVALID", subject, "report output differs from capture")]
    if evidence.output is not None and (
        reported_output["committed"] is not True or any(issue["severity"] == "error" for issue in evidence.issues)
    ):
        return [_failure("REPORT_INVALID", subject, "output is uncommitted or has an error issue")]
    return _executed_command_failures(evidence)


def _executed_command_failures(evidence: SideProcessEvidence) -> list[Failure]:
    """Each executed command ran a staged, hash-checked external tool below the process's own temporary directory.

    The reader has already held the command's file arguments to its working directory. The path text alone is
    not trusted: the executable must be one of the files the comparator staged from the executor's commit and
    checked by hash before and after the process.
    """
    assert evidence.context is not None
    subject = evidence.process["stage"]
    tools = {PurePath(path) for path in evidence.staged_tools}
    temporary = None if evidence.temporary_directory is None else PurePath(evidence.temporary_directory)
    for command in evidence.context["executedCommands"]:
        if PurePath(command["executablePath"]) not in tools:
            return [_failure("REPORT_INVALID", subject, "executed command is not a staged external tool")]
        working = PurePath(command["workingDirectory"])
        if temporary is None or working == temporary or not working.is_relative_to(temporary):
            return [_failure("REPORT_INVALID", subject, "executed command worked outside the process temporary directory")]
    return []


def _declared_work_ranges(
    authority: Mapping[str, Any], *, v0916_executor: bool = False,
) -> dict[str, list[tuple[int, int]]]:
    """The ranges a Preview declares in each work address space; a report gives no capacity for them."""
    spaces = WORK_ADDRESS_SPACES | (V0916_WORK_ADDRESS_SPACES if v0916_executor else frozenset())
    declared: dict[str, list[tuple[int, int]]] = {space: [] for space in sorted(spaces)}
    for operation in authority["compiledOperations"]:
        target = operation.get("targetSpaceId")
        rows = [(operation.get("sourceSpaceId"), operation.get("sourceRange")), (target, operation.get("targetRange"))]
        processor = operation.get("processor") or {}
        rows += [(target, row) for member in ("allowedReadRanges", "allowedWriteRanges") for row in processor.get(member, [])]
        for space, row in rows:
            if (space in declared and isinstance(row, Mapping)
                    and type(row.get("start")) is int and type(row.get("endExclusive")) is int):
                declared[space].append((row["start"], row["endExclusive"]))
    return declared


def _processor_write_audit_failures(
    stage: str, projection: Mapping[str, Any], context: Mapping[str, Any], authority: Mapping[str, Any],
    *, declared_work_ranges: Mapping[str, Sequence[tuple[int, int]]] | None = None,
    v0916_executor: bool = False,
) -> list[Failure]:
    """Decision 261 output audit; decision 271 audits later compiled uses of a work space.

    The shared range check has already validated all present named ranges. Work-space
    results may only be read inside one allowed write range, and may never be overwritten.
    Decision 278 admits the exact ab-combiner-work B bank read for the v0.9.16 executor only.
    Unknown operation semantics or missing read authority refuse instead of guessing.
    """

    def allowed(operation: Mapping[str, Any]) -> list[tuple[int, int]]:
        return [(row["start"], row["endExclusive"]) for row in operation["processor"]["allowedWriteRanges"]]

    def inside(row: Mapping[str, int], spans: Sequence[tuple[int, int]]) -> bool:
        return any(_contained((row["start"], row["endExclusive"]), span) for span in spans)

    output_spans = [span for operation in authority["compiledOperations"]
                    if operation.get("processor") and operation["targetSpaceId"] == OUTPUT_ADDRESS_SPACE
                    for span in allowed(operation)]
    differences = context["outputDifferenceRanges"]
    if any(row["start"] < 0 or row["endExclusive"] <= row["start"] or not inside(row, output_spans) for row in differences):
        return [_failure("REPORT_INVALID", stage, "output difference outside every write range the Preview allows")]
    mutations = {row["operationId"]: row for row in projection["compiledMutations"]}
    for operation in projection["compiledOperations"]:
        mutation = mutations.get(operation["operationId"])
        if (operation.get("processor") and mutation is not None and mutation["changedByteCount"] == 0
                and mutation["beforeSha256"] != mutation["afterSha256"]):
            return [_failure("REPORT_INVALID", stage, "zero processor changed-byte count has differing hashes")]
    operations = authority["compiledOperations"]
    for index, operation in enumerate(operations):
        space = operation["targetSpaceId"]
        if not operation.get("processor") or space not in (declared_work_ranges or {}):
            continue
        if operation["kind"] != "RunExternalProcessor":
            return [_failure("REPORT_INVALID", stage, "unknown work-space processor operation kind")]
        for later in operations[index + 1:]:
            kind = later["kind"]
            if kind not in {"CopyRange", "ReplaceRange", "TransformScalar", "FillRange", "PatchScalar", "RunExternalProcessor"}:
                return [_failure("REPORT_INVALID", stage, "unknown later operation kind in work-space processor audit")]
            if later["targetSpaceId"] == space:
                return [_failure("REPORT_INVALID", stage, "later operation writes the processor work address space")]
            source_space, source = later.get("sourceSpaceId"), later.get("sourceRange")
            if (kind in {"CopyRange", "ReplaceRange", "TransformScalar"} and (not source_space or source is None)
                    or (source_space is None) != (source is None)):
                return [_failure("REPORT_INVALID", stage, "later operation has no named read range")]
            if (source_space == space and not inside(source, allowed(operation))
                    and not (v0916_executor and space == "ab-combiner-work"
                             and (source["start"], source["endExclusive"]) == (262144, 524288))):
                return [_failure("REPORT_INVALID", stage, "later work-space read outside every processor allowed write range")]
            if kind == "RunExternalProcessor" and not later.get("processor"):
                return [_failure("REPORT_INVALID", stage, "later processor has no declared ranges")]
    for operation in projection["compiledOperations"]:
        if not operation.get("processor") or mutations.get(operation["operationId"], {}).get("changedByteCount", 0) == 0:
            continue
        if operation["targetSpaceId"] in (declared_work_ranges or {}):
            continue
        if (operation["targetSpaceId"] != OUTPUT_ADDRESS_SPACE
                or not any(inside(row, allowed(operation)) for row in differences)):
            return [_failure("REPORT_INVALID", stage, "processor changed bytes without a listed output difference")]
    return []


def _mutations_in_operation_order(projection: Mapping[str, Any]) -> list[dict[str, Any]]:
    """Give each mutation row the sequence its own report declares for the operation it names.

    A written mutation row names its operation and carries no sequence. The ADR 0057 order check
    then compares an operation sequence (a profile value such as 100) with a list position; with
    the declared sequence it compares the order of the operations, which is the contract's rule.
    A mutation of an operation the report does not declare gets no sequence and fails that check.
    """
    sequences = {row.get("operationId"): row.get("sequence") for row in projection["compiledOperations"]}
    return [{**row, "sequence": sequences.get(row.get("operationId"))} for row in projection["compiledMutations"]]


def report_input_binding(
    slot_id: str, *, request: Mapping[str, Any] | None = None, execution_role: str | None = None,
) -> dict[str, str]:
    """V2 v0.9.16 and 1.x reports use the compiled address-space id for both ids.

    Golden artifact ids only locate materialization bytes. The CLI's base
    option uses replace-base; its compiled report binding is reference-base.
    """
    address_space = "reference-base" if slot_id == "replace-base" else slot_id
    # The historical plan authorizes only the baseline report alias. Golden
    # materialization and CLI slots keep the resolver's original identities.
    if request is not None and execution_role == "baseline-exact":
        alias = next((row for row in request.get("inputIdentityAliases", ())
                      if row["routeId"] == request["routeId"]
                      and row["capabilityFingerprint"] == request["capabilityFingerprint"]), None)
        if alias is not None and slot_id == alias["candidateInputSlotId"]:
            address_space = alias["baselineInputSlotId"]
    return {"expectedReportAddressSpaceId": address_space, "expectedReportArtifactId": address_space}


def report_ordered_inputs(workflow_id: str, rows: Sequence[Mapping[str, Any]]) -> list[dict[str, Any]]:
    """Staged inputs in the order the CLI's report lists them, numbered from 0.

    A CtrlRAM Replace CLI sorts its bindings by slot id (ordinal) whatever the order of its
    arguments, so its report lists `reference-base` first and the replacements by name. A reviewed
    binding may name them in another order; the capture is compared with the report by position,
    so the staging follows the CLI. A Merge report keeps the binding order.
    """
    ordered = sorted(rows, key=lambda row: row["slotId"]) if workflow_id == "ctrlram-replace" else list(rows)
    return [{**row, "order": order} for order, row in enumerate(ordered)]


def v0916_milestone_failures(*, formal: bool, milestone: str | None) -> list[Failure]:
    """A formal v0.9.16 comparison must identify its milestone."""
    return ([_failure("INPUT_INVALID", "milestone", "formal comparison requires a milestone")]
            if formal and milestone is None else [])


def output_destination_failures(*, exists: bool, is_symlink: bool) -> list[Failure]:
    """Apply the final exclusive writer's conflict predicate before execution."""
    if exists or is_symlink:
        return [Failure("PARITY_WRITE_CONFLICT", "output", "output destination already exists")]
    return []


def side_execution_verdict(
    processes: Sequence[SideProcessEvidence], *, capacities: Mapping[str, int],
    capacities_by_stage: Mapping[str, Mapping[str, int]] | None = None,
    complete: bool = True,
    v0916_executor: bool = False,
) -> SideVerdict:
    """Shared side classification; ADR 0057 safety owners remain unchanged.

    A rejected Preview supplies its own (possibly empty) compiled authority.
    Build, including a rejected Build, uses only this side's preceding Preview.
    Sequence, projection and ranges run in contract order, then capture checks.
    """

    if not processes:
        return SideVerdict("invalid", None, [_failure("PROCESS_FAILED", "side", "no processes captured")])
    stages = [item.process["stage"] for item in processes]
    expected_stages = (["precursor-preview", "precursor-build"] if stages[0].startswith("precursor-") else [])
    expected_stages += ["preview", "build"]
    if stages != expected_stages[:len(stages)]:
        return SideVerdict("invalid", stages[-1], [_failure("REPORT_INVALID", "side", "invalid Preview/Build process order")])
    authority: Mapping[str, Any] | None = None
    preview_output: Mapping[str, Any] | None = None
    phase_profiles: dict[str, Any] = {}
    for index, evidence in enumerate(processes):
        process = evidence.process
        stage = process["stage"]
        failures = list(evidence.failures)
        capture_failures = [item for item in failures if item.code != "PREDECESSOR_REPORT_INVALID"]
        if capture_failures:
            return SideVerdict("invalid", stage, capture_failures)
        if (process["timedOut"] or process["exitCode"] is None
                or process["exitCode"] < 0 or process["exitCode"] >= 0x80000000):
            return SideVerdict("invalid", stage, [_failure("PROCESS_FAILED", stage, "process crashed or timed out")])
        if failures:
            return SideVerdict("invalid", stage, failures)
        if process["report"] is None:
            code = "ENVIRONMENT_INVALID" if evidence.settings_present else "PROCESS_FAILED"
            return SideVerdict("invalid", stage, [_failure(code, stage, "process wrote no report")])
        if any(issue["code"] in PROCESS_FAILURE_ISSUE_CODES for issue in evidence.issues):
            return SideVerdict("invalid", stage, [_failure("PROCESS_FAILED", stage, "report contains process-failure issue")])
        if evidence.projection is None or evidence.context is None:
            return SideVerdict("invalid", stage, [_failure("REPORT_INVALID", stage, "report could not be read")])
        if evidence.expected_identity is None:
            return SideVerdict("invalid", stage, [_failure("REPORT_INVALID", stage, "report has no request identity to check")])
        mismatches = report_identity_mismatches(evidence.context, evidence.expected_identity, declared_only=True)
        if mismatches:
            return SideVerdict("invalid", stage, [
                _failure("REPORT_INVALID", stage, f"{stage} {field} differs from the request")
                for field in mismatches])
        # A request that declares no resolved profile still binds Build to the profile its own Preview resolved.
        phase = stage.removesuffix("preview").removesuffix("build")
        if stage.endswith("preview"):
            phase_profiles[phase] = evidence.context.get("profileId")
        elif phase in phase_profiles and evidence.context.get("profileId") != phase_profiles[phase]:
            return SideVerdict("invalid", stage, [
                _failure("REPORT_INVALID", stage, f"{stage} ProfileId differs from the same side's Preview")])
        projection = evidence.projection
        if stage.endswith("preview"):
            authority = projection
            preview_output = evidence.context["output"]
        if authority is None:
            return SideVerdict("invalid", stage, [_failure("REPORT_INVALID", stage, "no same-side Preview authority")])
        try:
            has_skipped = any(row.get("status") == "skipped" for row in projection["compiledOperations"])
            skipped_rejection = (
                has_skipped and process["exitCode"] != 0
                and any(issue["severity"] == "error" for issue in evidence.issues)
                and all(row.get("status") == "skipped" for row in projection["compiledOperations"])
                and not projection["compiledMutations"] and not evidence.context["executedCommands"]
                and not any(row["executedCommands"] for row in projection["compiledOperations"])
                and not evidence.context["outputDifferenceRanges"] and evidence.output is None
                and (evidence.context["output"] is None or evidence.context["output"]["committed"] is False)
            )
            if has_skipped and not skipped_rejection:
                # A missing condition grants no range exemption, even for unexecuted rows.
                declared = _declared_work_ranges(authority, v0916_executor=v0916_executor)
                validate_semantic_report_ranges(projection, (capacities_by_stage or {}).get(stage, capacities),
                                                declared_overlap=True, declared_work_ranges=declared,
                                                audited_processor_writes=True)
                return SideVerdict("invalid", stage, [_failure("REPORT_INVALID", stage, "Skipped operations do not satisfy no-write typed rejection conditions")])
            validate_report_sequence(
                authority_operations=authority["compiledOperations"],
                observed_operations=projection["compiledOperations"],
                observed_mutations=_mutations_in_operation_order(projection),
            )
            validate_report_projection_against_compiled_authority(projection, authority, skipped_rejection=skipped_rejection)
            declared = _declared_work_ranges(authority, v0916_executor=v0916_executor)
            validate_semantic_report_ranges(projection, (capacities_by_stage or {}).get(stage, capacities),
                                            declared_overlap=True, declared_work_ranges=declared,
                                            audited_processor_writes=True, skipped_rejection=skipped_rejection)
            audit = ([] if skipped_rejection else
                     _processor_write_audit_failures(stage, projection, evidence.context, authority,
                                                    declared_work_ranges=declared, v0916_executor=v0916_executor))
        except (ParityError, KeyError, TypeError, ValueError) as error:
            return SideVerdict("invalid", stage, [_failure("REPORT_INVALID", stage, str(error))])
        if audit:
            return SideVerdict("invalid", stage, audit)
        if stage.endswith("build") and (
            projection["compilationFingerprint"] is None
            or projection["compilationFingerprint"] != authority["compilationFingerprint"]
        ):
            return SideVerdict("invalid", stage, [_failure("REPORT_INVALID", stage, "Build fingerprint absent or differs from Preview")])
        failures = _side_capture_failures(evidence)
        if failures:
            return SideVerdict("invalid", stage, failures)
        if (stage.endswith("build") and evidence.output is not None
                and preview_output is not None and evidence.context["output"] is not None
                and _identity(preview_output) != _identity(evidence.context["output"])):
            return SideVerdict("invalid", stage, [_failure("REPORT_INVALID", stage, "Build output size or hash differs from Preview prediction")])
        if process["exitCode"] != 0:
            if evidence.output is None and any(issue["severity"] == "error" for issue in evidence.issues):
                if index != len(processes) - 1:
                    return SideVerdict("invalid", stage, [_failure("REPORT_INVALID", stage, "execution continued after rejection")])
                return SideVerdict("rejected", stage, [])
            return SideVerdict("invalid", stage, [_failure("PROCESS_FAILED", stage, "nonzero exit is not a typed rejection")])
        if any(issue["severity"] == "error" for issue in evidence.issues):
            return SideVerdict("invalid", stage, [_failure("REPORT_INVALID", stage, "successful process has error issue")])
        if stage.endswith("preview") and preview_output is None:
            return SideVerdict("invalid", stage, [_failure("REPORT_INVALID", stage, "successful Preview has no output prediction")])
        if stage.endswith("build") and evidence.output is None:
            return SideVerdict("invalid", stage, [_failure("PROCESS_FAILED", stage, "successful Build has no captured output")])
    if not complete:
        return SideVerdict("ready", None, [])
    last = processes[-1]
    if last.process["stage"] != "build":
        return SideVerdict("invalid", last.process["stage"], [_failure("PROCESS_FAILED", "build", "side did not complete Build")])
    return SideVerdict("output", None, [])


class _RouteKey(NamedTuple):
    ic_id: str
    workflow_id: str
    ic_count_variant: str
    map_variant: str


def route_key(route: Mapping[str, Any]) -> tuple[str, str, str, str]:
    """The route axes that identify a route in the policy and a scenario in the ledger."""

    return (route["icId"], route["workflowId"], route["icCountVariant"], route["mapVariant"])


def universe_routes(policy: Mapping[str, Any]) -> set[str]:
    """Every `available` route published as `supported` or `candidate`."""

    return {
        route["routeId"]
        for route in policy["routes"]
        if route["authoring"]["value"] == "available" and route["publication"]["value"] in UNIVERSE_PUBLICATION
    }


def expected_cli(scenario: Mapping[str, Any]) -> dict[str, Any]:
    """The CLI profile and selection the ADR 0057 owner derives for a scenario's route axes."""

    token = cli_selection_token(
        _RouteKey(scenario["icId"], scenario["workflowId"], scenario["icCountVariant"], scenario["mapVariant"])
    )
    option = None
    if token is not None:
        option = "--ic-num" if scenario["workflowId"] == "ctrlram-replace" else "--ab-topology"
    return {"profile": scenario["icId"], "selectionOption": option, "selectionToken": token}


def _identity(value: Mapping[str, Any] | None) -> dict[str, Any] | None:
    return None if value is None else {"size": value["size"], "sha256": value["sha256"]}


def _duplicates(values: Iterable[str]) -> list[str]:
    seen: set[str] = set()
    repeated: set[str] = set()
    for value in values:
        (repeated if value in seen else seen).add(value)
    return sorted(repeated)


# ---------------------------------------------------------------------------
# Coverage ledger
# ---------------------------------------------------------------------------


def scenario_binding_failures(
    scenario: Mapping[str, Any], *, routes_by_key: Mapping[tuple[str, str, str, str], Any], universe: set[str]
) -> list[Failure]:
    """A scenario names a current universe route and the selection the CLI derives."""

    subject = scenario["scenarioId"]
    failures: list[Failure] = []
    route = routes_by_key.get(route_key(scenario))
    if route is None or route["routeId"] != scenario["routeId"]:
        failures.append(_failure("INPUT_INVALID", subject, f"route axes do not name {scenario['routeId']}"))
    if scenario["routeId"] not in universe:
        failures.append(_failure("INPUT_INVALID", subject, "route is outside the universe"))
    if scenario["cli"] != expected_cli(scenario):
        failures.append(_failure("INPUT_INVALID", subject, "CLI selection differs from the derived selection"))
    if subject != ":".join([*route_key(scenario), scenario["evidenceCaseId"]]):
        failures.append(_failure("INPUT_INVALID", subject, "identifier differs from its route axes and case"))
    return failures


def scenario_input_failures(
    scenario: Mapping[str, Any],
    *,
    evidence: Mapping[str, Any],
    case_manifests: Mapping[str, Mapping[str, Any]],
    plan: Mapping[str, Any],
    routes_by_key: Mapping[tuple[str, str, str, str], Any],
) -> list[Failure]:
    """A scenario's binding, inputs and CtrlRAM base come from the active Golden and the reviewed bindings."""

    subject = scenario["scenarioId"]
    failures: list[Failure] = []
    evidence_case = case_manifests.get(scenario["evidenceCaseId"])
    input_case = case_manifests.get(scenario["inputCaseId"])
    if evidence_case is None or input_case is None:
        return [_failure("INPUT_INVALID", subject, "evidence or input case is not in the canonical manifest")]
    named = evidence.get(scenario["routeId"], {}).get("caseId")
    if scenario["binding"] == "route-evidence" and named != scenario["evidenceCaseId"]:
        failures.append(_failure("INPUT_INVALID", subject, f"route evidence names {named}"))
    if scenario["binding"] == "case" and named is not None:
        failures.append(_failure("INPUT_INVALID", subject, f"a case binding hides route evidence {named}"))
    alias = evidence_case.get("alias")
    if (alias["sourceCaseId"] if alias else scenario["evidenceCaseId"]) != scenario["inputCaseId"]:
        failures.append(_failure("INPUT_INVALID", subject, "input case is not the resolved case"))
    case_inputs = [item for item in input_case["artifacts"] if item["role"] == "input"]
    artifacts = {item["artifactId"]: item for item in case_inputs}
    if [item["order"] for item in scenario["inputs"]] != list(range(len(scenario["inputs"]))):
        failures.append(_failure("INPUT_INVALID", subject, "input order is not contiguous from 0"))
    for item in scenario["inputs"]:
        artifact = artifacts.get(item["artifactId"])
        if artifact is None or (artifact["size"], artifact["sha256"]) != (item["size"], item["sha256"]):
            failures.append(_failure("INPUT_INVALID", subject, f"input {item['artifactId']} differs from its case"))
    pairs = [(item["artifactId"], item["slotId"]) for item in scenario["inputs"]]
    if scenario["workflowId"] != "ctrlram-replace":
        if scenario["ctrlRamBase"] is not None:
            failures.append(_failure("INPUT_INVALID", subject, "CtrlRAM base on a scenario of another workflow"))
        if pairs != [(item["artifactId"], item["artifactId"]) for item in case_inputs]:
            failures.append(_failure("INPUT_INVALID", subject, "inputs are not the case inputs in order"))
        return failures
    authority = plan["canonicalInputAuthority"]
    binding = next(
        (row for row in authority["ctrlRamExecutionBindings"] if row["caseId"] == scenario["inputCaseId"]), None
    )
    base = next((row for row in authority["ctrlRamBaseRoutes"] if row["routeId"] == scenario["planRouteId"]), None)
    if binding is None or base is None:
        # Without a reviewed CtrlRAM binding the slots and base kind cannot be admitted.
        return [*failures, _failure("INPUT_INVALID", subject, "no reviewed CtrlRAM binding for this scenario")]
    replacements = [(item["artifactId"], item["slotId"]) for item in binding["replacements"]]
    if base["kind"] == "tp-input":
        expected_base: dict[str, Any] = {"kind": "tp-input"}
        expected_pairs = [(binding["tpBaseArtifactId"], "replace-base"), *replacements]
    else:
        expected_base = {"kind": "standard-merge", "standardMergeMapVariant": base["standardMergeMapVariant"]}
        recipe = binding["fullBaseRecipe"]
        expected_pairs = [(recipe["dpArtifactId"], "dp-input"), (recipe["tpArtifactId"], "tp-input"), *replacements]
        precursor = (scenario["icId"], "standard-merge", "selector-free", base["standardMergeMapVariant"])
        if precursor not in routes_by_key:
            failures.append(_failure("INPUT_INVALID", subject, f"precursor route {precursor} is not in the policy"))
    if scenario["ctrlRamBase"] != expected_base:
        failures.append(_failure("INPUT_INVALID", subject, "CtrlRAM base differs from the reviewed binding"))
    if pairs != expected_pairs:
        failures.append(_failure("INPUT_INVALID", subject, "CtrlRAM slots differ from the reviewed binding"))
    return failures


def debt_set_failures(ledger: Mapping[str, Any], plan: Mapping[str, Any]) -> list[Failure]:
    """The debt set is exactly the plan's unbound route ids, in order; nothing else inherits it."""

    expected = sorted(plan["canonicalInputAuthority"]["currentlyMissingRouteIds"])
    actual = ledger["debtSet"]["routeIds"]
    if actual == expected:
        return []
    return [
        _failure(
            "COVERAGE_UNDISPOSED",
            "debtSet",
            f"differs from the plan: missing {sorted(set(expected) - set(actual))}, "
            f"extra {sorted(set(actual) - set(expected))}",
        )
    ]


def coverage_failures(ledger: Mapping[str, Any], *, universe: set[str], evidence: Mapping[str, Any]) -> list[Failure]:
    """Partition and completeness of the coverage ledger over the universe."""

    failures: list[Failure] = []
    covered = {row["routeId"] for row in ledger["scenarios"]}
    debt = set(ledger["debtSet"]["routeIds"])
    accepted = {row["routeId"] for row in ledger["acceptedGaps"]}
    pending = set(ledger["pendingAcceptedGaps"]["routeIds"])
    for label, left, right in (
        ("debt set and accepted gaps", debt, accepted),
        ("debt set and pending gaps", debt, pending),
        ("accepted and pending gaps", accepted, pending),
        ("compared routes and gaps", covered, accepted | pending),
    ):
        for route_id in sorted(left & right):
            failures.append(_failure("COVERAGE_UNDISPOSED", route_id, f"listed in both {label}"))
    for route_id in sorted(universe - covered - debt - accepted - pending):
        failures.append(_failure("COVERAGE_UNDISPOSED", route_id, "neither compared, owed nor disposed"))
    for route_id in sorted((accepted | pending) - universe):
        failures.append(_failure("COVERAGE_UNDISPOSED", route_id, "gap outside the universe"))
    declared = {(row["routeId"], row["evidenceCaseId"]) for row in ledger["scenarios"] if row["binding"] == "route-evidence"}
    for route_id in sorted(universe):
        case_id = evidence.get(route_id, {}).get("caseId")
        if case_id and (route_id, case_id) not in declared:
            failures.append(_failure("COVERAGE_INCOMPLETE", route_id, f"route evidence {case_id} has no scenario"))
    return failures


def report_of_record_blockers(ledger: Mapping[str, Any]) -> list[Failure]:
    """Ledger states under which no run may be a report of record."""

    return [
        _failure("COVERAGE_UNDISPOSED", route_id, "pending gap approval")
        for route_id in ledger["pendingAcceptedGaps"]["routeIds"]
    ]


def ledger_failures(
    ledger: Mapping[str, Any],
    *,
    policy: Mapping[str, Any],
    manifest: Mapping[str, Any],
    case_manifests: Mapping[str, Mapping[str, Any]],
    plan: Mapping[str, Any],
) -> list[Failure]:
    """Every ledger rule against the candidate's policy and manifest and the ADR 0057 plan."""

    routes_by_key = {route_key(route): route for route in policy["routes"]}
    universe = universe_routes(policy)
    evidence = {row["routeId"]: row for row in manifest["routeEvidence"]}
    failures = [
        _failure("INPUT_INVALID", scenario_id, "scenario listed twice")
        for scenario_id in _duplicates(row["scenarioId"] for row in ledger["scenarios"])
    ]
    for scenario in ledger["scenarios"]:
        failures.extend(scenario_binding_failures(scenario, routes_by_key=routes_by_key, universe=universe))
        failures.extend(
            scenario_input_failures(
                scenario, evidence=evidence, case_manifests=case_manifests, plan=plan, routes_by_key=routes_by_key
            )
        )
    failures.extend(debt_set_failures(ledger, plan))
    failures.extend(coverage_failures(ledger, universe=universe, evidence=evidence))
    return failures


def coverage_changes(ledger: Mapping[str, Any], baseline_ledger: Mapping[str, Any]) -> list[dict[str, Any]]:
    """Coverage changes since the baseline tag's ledger, in a stable order.

    An accepted gap is a change when its row is new or differs from the
    baseline's row for the route: a new gap, or the renewal of a gap under
    this release's approval (1.1.12 board decision 60, 1.1.13 decision 96).
    """

    current = {row["scenarioId"]: row for row in ledger["scenarios"]}
    before = {row["scenarioId"]: row for row in baseline_ledger["scenarios"]}
    changes = [{"kind": "scenario-retired", "subject": scenario_id} for scenario_id in sorted(set(before) - set(current))]
    changes.extend(
        {"kind": "input-revision", "subject": scenario_id}
        for scenario_id in sorted(set(before) & set(current))
        if current[scenario_id]["inputs"] != before[scenario_id]["inputs"]
        or current[scenario_id]["inputRevision"] != before[scenario_id]["inputRevision"]
    )
    changes.extend({"kind": "scenario-added", "subject": scenario_id} for scenario_id in sorted(set(current) - set(before)))
    accepted_before = {row["routeId"]: row for row in baseline_ledger["acceptedGaps"]}
    changes.extend(
        {"kind": "accepted-gap", "subject": row["routeId"]}
        for row in sorted(ledger["acceptedGaps"], key=lambda item: item["routeId"])
        if accepted_before.get(row["routeId"]) != row
    )
    return changes


def accepted_gap_scope_failures(ledger: Mapping[str, Any], candidate_version: str) -> list[Failure]:
    """An accepted gap covers only the release that approved it.

    Decision 60 declares a route without comparable inputs like a byte
    difference, with an approval, an entry and a CHANGELOG id each time, and
    decision 96 limits its approval to 1.1.13. A gap row carried from an
    earlier release therefore approves nothing for this one.
    """

    return [
        _failure(
            "COVERAGE_UNDISPOSED",
            row["routeId"],
            f"accepted gap approved in {row['approvedInVersion']} does not cover {candidate_version}",
        )
        for row in ledger["acceptedGaps"]
        if row["approvedInVersion"] != candidate_version
    ]


def monotonicity_failures(ledger: Mapping[str, Any], baseline_ledger: Mapping[str, Any]) -> list[Failure]:
    """An input revision increments the revision, and every removal leaves a retirement record."""

    failures: list[Failure] = []
    current = {row["scenarioId"]: row for row in ledger["scenarios"]}
    before = {row["scenarioId"]: row for row in baseline_ledger["scenarios"]}
    retired = {row["scenarioId"] for row in ledger["retiredScenarios"]}
    for scenario_id in sorted(set(before) & set(current)):
        old, new = before[scenario_id], current[scenario_id]
        if old["inputs"] != new["inputs"] and new["inputRevision"] <= old["inputRevision"]:
            failures.append(_failure("COVERAGE_UNDISPOSED", scenario_id, "inputs replaced without a new input revision"))
        if new["inputRevision"] < old["inputRevision"]:
            failures.append(_failure("COVERAGE_UNDISPOSED", scenario_id, "input revision went back"))
    for scenario_id in sorted(set(before) - set(current) - retired):
        failures.append(_failure("COVERAGE_UNDISPOSED", scenario_id, "scenario removed without a retirement record"))
    return failures


# ---------------------------------------------------------------------------
# Byte comparison evidence
# ---------------------------------------------------------------------------


def range_projection(ranges: Sequence[Mapping[str, int]] | None) -> dict[str, Any] | None:
    """The report comparison of a complete computed range list: totals, digest and the first 32 ranges."""

    if ranges is None:
        return None
    return {
        "differentByteCount": sum(item["endExclusive"] - item["start"] for item in ranges),
        "rangeCount": len(ranges),
        "rangeListSha256": canonical_json_sha256([dict(item) for item in ranges]),
        "ranges": [dict(item) for item in ranges[:REPORTED_RANGE_LIMIT]],
        "rangesTruncated": len(ranges) > REPORTED_RANGE_LIMIT,
    }


def _merged(spans: Iterable[tuple[int, int]]) -> list[tuple[int, int]]:
    result: list[tuple[int, int]] = []
    for start, end in sorted(spans):
        if result and start <= result[-1][1]:
            result[-1] = (result[-1][0], max(result[-1][1], end))
        else:
            result.append((start, end))
    return result


def range_list_failures(ranges: Sequence[Mapping[str, int]], subject: str, code: str = "REPORT_INVALID") -> list[Failure]:
    """Ranges are non-empty, half-open, ascending and disjoint."""

    spans = [(item["start"], item["endExclusive"]) for item in ranges]
    if not spans or any(start >= end for start, end in spans) or any(left[1] > right[0] for left, right in zip(spans, spans[1:])):
        return [_failure(code, subject, "ranges are not non-empty, half-open, ascending and disjoint")]
    return []


def scope_evidence_failures(
    subject: str,
    baseline: Mapping[str, Any] | None,
    candidate: Mapping[str, Any] | None,
    comparisons: Mapping[str, Mapping[str, Any] | None],
    evidence: ScopeEvidence | None,
    reported_scopes: Sequence[str] = SCOPES,
) -> list[Failure]:
    """Each scope both sides can be compared in has computed evidence that agrees with the identities and the report.

    `comparisons` maps each scope in `reported_scopes` to the report's
    comparison object. A scope is comparable when both sides carry that
    artifact; its evidence is then required, `None` exactly when the
    identities are equal, and a reported comparison must be its projection.
    A scope a side lacks has neither evidence nor a comparison.
    """

    failures: list[Failure] = []
    evidence = evidence or {}
    for scope in SCOPES:
        member = "comparison" if scope == "output" else "precursorComparison"
        for name, side in (("baseline", baseline), ("candidate", candidate)):
            if side is not None and scope not in side:
                failures.append(_failure("REPORT_INVALID", subject, f"{name} side is missing {scope}"))
        left = None if baseline is None else _identity(baseline.get(scope))
        right = None if candidate is None else _identity(candidate.get(scope))
        comparable = left is not None and right is not None
        if not comparable:
            if scope in evidence:
                failures.append(_failure("REPORT_INVALID", subject, f"{scope} evidence for a side without that artifact"))
            if comparisons.get(scope) is not None:
                failures.append(_failure("REPORT_INVALID", subject, f"{member} for a side without that artifact"))
            continue
        if scope not in evidence:
            failures.append(_failure("REPORT_INVALID", subject, f"{scope} comparison evidence is missing"))
            continue
        ranges = evidence[scope]
        if (left == right) != (ranges is None):
            failures.append(_failure("REPORT_INVALID", subject, f"{scope} identities and computed ranges disagree"))
        if ranges is not None:
            failures.extend(range_list_failures(ranges, f"{subject}:{scope}"))
        if scope in reported_scopes and comparisons.get(scope) != range_projection(ranges):
            failures.append(_failure("REPORT_INVALID", subject, f"{member} is not the projection of the computed ranges"))
    return failures


def declared_side(side: Mapping[str, Any]) -> dict[str, Any]:
    """The side outcome a declaration binds for one report side."""

    return {
        "result": "rejected" if side["status"] == "rejected" else "output",
        "output": _identity(side["output"]),
        "precursor": _identity(side["precursor"]),
        "stage": side["stoppedAt"],
        "issueCodes": sorted({issue["code"] for issue in side["issues"] if issue["severity"] == "error"}),
    }


def process_failure_issue_failures(subject: str, sides: Iterable[Mapping[str, Any] | None]) -> list[Failure]:
    """A process-failure issue at any severity is never an approvable product result.

    An `invalid` side is the faithful report of such a failure and is not repeated here.
    """

    codes = {
        issue["code"]
        for side in sides if side is not None and side.get("status") != "invalid"
        for issue in side.get("issues", ())
        if issue["code"] in PROCESS_FAILURE_ISSUE_CODES
    }
    return [_failure("PROCESS_FAILED", subject, f"process-failure issue {code}") for code in sorted(codes)]


# ---------------------------------------------------------------------------
# Rolling report
# ---------------------------------------------------------------------------


class RollingTag(NamedTuple):
    tag: str
    object_type: str
    tag_object: str
    commit: str
    ancestor: bool
    published: bool | None


def stable_tag_version(tag: str) -> tuple[int, int, int] | None:
    match = re.fullmatch(r"v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)", tag)
    return None if match is None else tuple(map(int, match.groups()))


def published_inventory_failures(inventory: Any) -> list[Failure]:
    """Validate the producer's complete offline inventory, without acquiring releases."""
    def invalid(detail: str) -> list[Failure]:
        return [_failure("BASELINE_INVALID", "publishedInventory", detail)]

    def utc_time(value: Any) -> tuple[int, str]:
        match = None if not isinstance(value, str) else re.fullmatch(
            r"([0-9]{4}-[0-9]{2}-[0-9]{2}[Tt][0-9]{2}:[0-9]{2}:[0-9]{2})(?:\.([0-9]+))?"
            r"([Zz]|[+-](?:0[0-9]|1[0-9]|2[0-3]):[0-5][0-9])", value
        )
        if match is None:
            raise ValueError("publication and collection times must be RFC 3339 date-time strings")
        # Compare the fraction exactly: datetime otherwise truncates it to microseconds.
        local = datetime.fromisoformat(match[1].upper())
        offset = match[3]
        offset_seconds = 0 if offset.upper() == "Z" else (
            (int(offset[1:3]) * 60 + int(offset[4:6])) * 60 * (1 if offset[0] == "+" else -1))
        seconds = local.toordinal() * 86400 + local.hour * 3600 + local.minute * 60 + local.second - offset_seconds
        return seconds, (match[2] or "").rstrip("0")

    root_members = {"schemaVersion", "kind", "repository", "collectedAtUtc", "complete", "pagesRead", "releases"}
    row_members = {"id", "tag", "publishedAtUtc", "draft", "prerelease", "complete"}
    if not isinstance(inventory, dict) or set(inventory) != root_members:
        return invalid("inventory has missing or unknown members")
    if (inventory["schemaVersion"] != "1.0" or inventory["kind"] != "predecessor-published-release-inventory"
            or inventory["repository"] != "Dennis40816/nvt_fw_combiner" or inventory["complete"] is not True
            or type(inventory["pagesRead"]) is not int or inventory["pagesRead"] < 1
            or not isinstance(inventory["releases"], list)):
        return invalid("inventory identity, completeness or pagination is invalid")
    ids, tags = set(), set()
    try:
        collected = utc_time(inventory["collectedAtUtc"])
        for row in inventory["releases"]:
            if not isinstance(row, dict) or set(row) != row_members:
                return invalid("release has missing or unknown members")
            if (type(row["id"]) is not int or row["id"] < 1 or not isinstance(row["tag"], str)
                    or stable_tag_version(row["tag"]) is None or row["draft"] is not False
                    or row["prerelease"] is not False or row["complete"] is not True):
                return invalid("release is not a complete published stable release")
            if row["id"] in ids or row["tag"] in tags:
                return invalid("duplicate release id or tag")
            if utc_time(row["publishedAtUtc"]) > collected:
                return invalid("release publication is later than inventory collection")
            ids.add(row["id"])
            tags.add(row["tag"])
    except (TypeError, ValueError):
        return invalid("publication or collection time is invalid")
    return []


def published_inventory_identity(payload: bytes, inventory: Mapping[str, Any]) -> dict[str, str]:
    """Bind file bytes and numerically ordered publication facts independently."""
    return {"rawSha256": hashlib.sha256(payload).hexdigest(),
            "factsSha256": canonical_json_sha256({"repository": inventory["repository"],
                "releases": sorted(inventory["releases"], key=lambda row: stable_tag_version(row["tag"]))})}


def deterministic_digest_projection(report: Mapping[str, Any]) -> dict[str, Any]:
    """Remove only the contract's named run-specific members; preserve nulls and order."""
    value = copy.deepcopy(dict(report))
    value.pop("deterministicSha256", None)
    if "environment" in value:
        value["environment"].pop("temporaryRootLength", None)
    for collection in ("scenarios", "routes"):
        for row in value.get(collection, []):
            for name in ("baseline", "candidate"):
                side = row.get(name)
                if side is None:
                    continue
                for process in side.get("processes", []):
                    process.pop("stdoutSha256", None)
                    process.pop("stderrSha256", None)
                    if process.get("report") is not None:
                        process["report"].pop("size", None)
                        process["report"].pop("sha256", None)
    for failures in (value.get("gate", {}).get("failures", []), value.get("failures", [])):
        for failure in failures:
            failure.pop("detail", None)
    if value.get("publishedInventory") is not None:
        value["publishedInventory"].pop("rawSha256", None)
    return value


def deterministic_report_sha256(report: Mapping[str, Any]) -> str:
    return canonical_json_sha256(deterministic_digest_projection(report))


def deterministic_digest_failures(report: Mapping[str, Any]) -> list[Failure]:
    """Builders validate before adding the digest; completed reports verify it here."""
    if "deterministicSha256" in report and report["deterministicSha256"] != deterministic_report_sha256(report):
        return [_failure("REPORT_INVALID", "deterministicSha256", "digest differs from the named projection")]
    return []


def formal_interface_failures(
    contract: Mapping[str, Any], *, formal: bool, amendment: Mapping[str, Any] | None = None,
) -> list[Failure]:
    pending = pending_execution_interfaces(contract, amendment)
    return [_failure("CONTRACT_PENDING", "interfaces", ", ".join(pending))] if formal and pending else []


def comparator_source_failures(expected: Mapping[str, str], observed: Mapping[str, str], *, formal: bool) -> list[Failure]:
    return [_failure("SOURCE_MISMATCH", path, "formal comparator differs from candidate source")
            for path in sorted(set(expected) | set(observed)) if expected.get(path) != observed.get(path)] if formal else []


def rolling_baseline(
    tags: Sequence[RollingTag], candidate_version: str, *, given_tag: str | None, formal: bool,
    published_tags: Sequence[str] | None = None,
) -> tuple[RollingTag | None, list[Failure]]:
    """Select over the complete published inventory, then require that exact local tag.

    The host supplies complete stable release names or None if unavailable.
    Missing or inadmissible local tags never fall back to an older release.
    A diagnostic admits only its given annotated ancestor, without publication.
    """
    pattern = re.compile(r"v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\Z")
    version = pattern.fullmatch("v" + candidate_version)
    eligible = []
    if version:
        candidate = tuple(map(int, version.groups()))
        eligible = [tag for tag in tags if pattern.fullmatch(tag.tag)
                    and tuple(map(int, pattern.fullmatch(tag.tag).groups())) < candidate
                    and tag.object_type == "tag" and tag.ancestor
                    and re.fullmatch(r"[0-9a-f]{40}", tag.tag_object)
                    and re.fullmatch(r"[0-9a-f]{40}", tag.commit)]
    selected = None
    if formal:
        if (version and published_tags and not isinstance(published_tags, (str, bytes))
                and all(isinstance(tag, str) and stable_tag_version(tag) is not None for tag in published_tags)):
            published = [tag for tag in published_tags if stable_tag_version(tag) < candidate]
            if published:
                highest = max(published, key=stable_tag_version)
                matches = [tag for tag in eligible if tag.tag == highest]
                selected = matches[0] if len(matches) == 1 else None
        if given_tag is not None and (selected is None or selected.tag != given_tag):
            selected = None
    else:
        matches = [tag for tag in eligible if tag.tag == given_tag]
        selected = matches[0] if len(matches) == 1 else None
    return selected, ([] if selected is not None else [
        _failure("BASELINE_INVALID", given_tag or "baseline", "no admitted previous stable annotated ancestor baseline")])


def scope_capture_failures(subject: str, expected: Mapping[str, Any], observed: Mapping[str, Any]) -> list[Failure]:
    return [] if _identity(expected) == _identity(observed) else [
        _failure("REPORT_INVALID", subject, "artifact changed after process capture")]


def execution_capacities(evidence: SideProcessEvidence) -> dict[str, int]:
    """Input sizes are captured; output bounds come from bytes or the typed Preview.

    Build is still checked against that same side's Preview before its ranges.
    Preview has no output artifact, so its compiled target extent in `output-image` is its bound;
    a target in another address space never sets it.
    """
    capacities = {reported["addressSpaceId"]: captured["size"]
                  for reported, captured in zip((evidence.context or {}).get("orderedInputs", []), evidence.inputs)}
    capacities[OUTPUT_ADDRESS_SPACE] = (evidence.output or {}).get("size", max(
        (operation["targetRange"]["endExclusive"] for operation in
         (evidence.projection or {}).get("compiledOperations", [])
         if operation.get("targetRange") is not None and operation.get("targetSpaceId") == OUTPUT_ADDRESS_SPACE), default=0))
    return capacities


class RollingOutcome(NamedTuple):
    outcome: str
    failure_code: str | None


def rolling_outcome(
    baseline: Mapping[str, Any], candidate: Mapping[str, Any], evidence: ScopeEvidence,
    failures: Sequence[Failure],
) -> RollingOutcome:
    """Classify only admitted side verdicts and complete scope measurements."""
    if failures or "invalid" in (baseline["status"], candidate["status"]):
        return RollingOutcome("invalid", failures[0].code if failures else "PREDECESSOR_REPORT_INVALID")
    if baseline["status"] == candidate["status"] == "rejected":
        return RollingOutcome("both-reject", None)
    if baseline["status"] == "rejected":
        return RollingOutcome("baseline-rejects", None)
    if candidate["status"] == "rejected":
        return RollingOutcome("candidate-rejects", None)
    return RollingOutcome("different" if any(evidence.get(scope) is not None for scope in SCOPES) else "equal", None)


def declaration_entry_id(kind: str | None, subject: str, declaration: Mapping[str, Any] | None) -> str | None:
    """Projection of an unambiguous binding; declared_change_failures checks reproduction."""
    matches = [entry["id"] for entry in (declaration or {}).get("entries", [])
               if entry["kind"] == kind and subject in entry["routeIds" if kind == "accepted-gap" else "scenarioIds"]]
    return matches[0] if len(matches) == 1 else None


def rolling_coverage(
    ledger: Mapping[str, Any], baseline_ledger: Mapping[str, Any], policy: Mapping[str, Any],
    manifest: Mapping[str, Any], declaration: Mapping[str, Any] | None,
) -> dict[str, Any]:
    """Canonical projection shared by the builder and report coverage validation."""
    universe = universe_routes(policy)
    covered = {row["routeId"] for row in ledger["scenarios"]}
    debt = set(ledger["debtSet"]["routeIds"])
    accepted = {row["routeId"] for row in ledger["acceptedGaps"]}
    pending = set(ledger["pendingAcceptedGaps"]["routeIds"])
    kinds = {row["routeId"]: row["kind"] for row in manifest["routeEvidence"]}
    not_covered = []
    for route_id in sorted(universe - covered):
        reason = "debt-set" if route_id in debt else "accepted-gap" if route_id in accepted else "pending-gap" if route_id in pending else None
        if reason is not None:
            not_covered.append({"routeId": route_id, "reason": reason, "evidenceKind": kinds.get(route_id, "missing")})
    return {"universe": len(universe), "coveredRoutes": len(covered), "scenarios": len(ledger["scenarios"]),
            "debtSetInUniverse": len(debt & universe), "acceptedGaps": len(accepted), "pendingAcceptedGaps": len(pending),
            "notCovered": not_covered,
            "changesSinceBaseline": [{**row, "declarationEntryId": declaration_entry_id(row["kind"], row["subject"], declaration)}
                                     for row in coverage_changes(ledger, baseline_ledger)]}


def scenario_value_failures(scenario: Mapping[str, Any], evidence: ScopeEvidence | None) -> list[Failure]:
    """Values of one rolling scenario result that a schema cannot compare, against its computed evidence."""

    subject = scenario["scenarioId"]
    failures = process_failure_issue_failures(subject, (scenario["baseline"], scenario["candidate"]))
    if scenario["outcome"] == "invalid":
        if scenario["failureCode"] not in EXECUTION_FAILURE_CODES:
            failures.append(_failure("REPORT_INVALID", subject, "invalid scenario without a shared execution failure code"))
        return failures
    baseline, candidate = scenario["baseline"], scenario["candidate"]
    if baseline is None or candidate is None:
        return failures + [_failure("REPORT_INVALID", subject, "scenario is missing a side")]
    if rolling_outcome(baseline, candidate, evidence or {}, []).outcome != scenario["outcome"]:
        failures.append(_failure("REPORT_INVALID", subject, "outcome differs from its sides and computed scopes"))
    failures.extend(scope_evidence_failures(
        subject,
        baseline,
        candidate,
        {"output": scenario["comparison"], "precursor": scenario["precursorComparison"]},
        evidence,
    ))
    differs = any((evidence or {}).get(scope) is not None for scope in SCOPES)
    if scenario["outcome"] == "equal" and differs:
        failures.append(_failure("REPORT_INVALID", subject, "an equal outcome with differing bytes"))
    if scenario["outcome"] == "different" and not differs:
        failures.append(_failure("REPORT_INVALID", subject, "a different outcome without differing bytes"))
    return failures


def artifact_presence_failures(scenario: Mapping[str, Any], ledger_row: Mapping[str, Any]) -> list[Failure]:
    """Each side carries the artifacts its ledger scenario obliges it to build.

    A scenario whose declared CtrlRAM base is `standard-merge` builds a
    precursor on each side before its output, so a side with an output, or
    rejected at `preview` or `build`, carries one, and a side rejected at a
    precursor stage carries none. Any other scenario has no precursor and no
    precursor stage. A failed side is left to its failure code.
    """

    if scenario["outcome"] == "invalid":
        return []
    merge = (ledger_row["ctrlRamBase"] or {}).get("kind") == "standard-merge"
    failures: list[Failure] = []
    for name in ("baseline", "candidate"):
        side = scenario[name]
        if side is None or side["status"] == "invalid":
            continue
        if not merge and side["stoppedAt"] in PRECURSOR_STAGES:
            failures.append(_failure("REPORT_INVALID", scenario["scenarioId"], f"{name} stopped at a precursor stage without a Standard Merge base"))
        built = merge and side["stoppedAt"] not in PRECURSOR_STAGES
        if (side["precursor"] is not None) != built:
            failures.append(
                _failure("REPORT_INVALID", scenario["scenarioId"], f"{name} precursor does not match the ledger's CtrlRAM base and its stage")
            )
    return failures


def rolling_coverage_report_failures(
    report: Mapping[str, Any],
    *,
    ledger: Mapping[str, Any],
    baseline_ledger: Mapping[str, Any],
    policy: Mapping[str, Any],
    manifest: Mapping[str, Any],
) -> list[Failure]:
    """A rolling report runs each ledger scenario exactly once and reports the ledger's coverage."""

    failures: list[Failure] = []
    active = {row["scenarioId"]: row for row in ledger["scenarios"]}
    reported = [row["scenarioId"] for row in report["scenarios"]]
    for scenario_id in _duplicates(reported):
        failures.append(_failure("REPORT_INVALID", scenario_id, "scenario reported more than once"))
    for scenario_id in sorted(set(active) - set(reported)):
        failures.append(_failure("REPORT_INVALID", scenario_id, "ledger scenario missing from the report"))
    for scenario_id in sorted(set(reported) - set(active)):
        failures.append(_failure("REPORT_INVALID", scenario_id, "reported scenario is not in the ledger"))
    for row in report["scenarios"]:
        declared = active.get(row["scenarioId"])
        if declared is not None and (row["routeId"], row["inputRevision"]) != (declared["routeId"], declared["inputRevision"]):
            failures.append(_failure("REPORT_INVALID", row["scenarioId"], "route or input revision differs from the ledger"))
    expected = rolling_coverage(ledger, baseline_ledger, policy, manifest, None)
    expected_counts = {key: expected[key] for key in (
        "universe", "coveredRoutes", "scenarios", "debtSetInUniverse", "acceptedGaps", "pendingAcceptedGaps")}
    coverage = report["coverage"]
    for key, value in expected_counts.items():
        if coverage[key] != value:
            failures.append(_failure("REPORT_INVALID", f"coverage.{key}", f"reports {coverage[key]}, ledger gives {value}"))
    if sorted(coverage["notCovered"], key=lambda row: row["routeId"]) != expected["notCovered"]:
        failures.append(_failure("REPORT_INVALID", "coverage.notCovered", "differs from the ledger's routes that no scenario compares"))
    expected_changes = [(row["kind"], row["subject"]) for row in coverage_changes(ledger, baseline_ledger)]
    if sorted((row["kind"], row["subject"]) for row in coverage["changesSinceBaseline"]) != sorted(expected_changes):
        failures.append(_failure("REPORT_INVALID", "coverage.changesSinceBaseline", "differs from the ledger changes since the baseline"))
    return failures


# ---------------------------------------------------------------------------
# Declaration
# ---------------------------------------------------------------------------


def declaration_failures(
    declaration: Mapping[str, Any],
    *,
    ledger: Mapping[str, Any],
    ledger_sha256: str,
    candidate_version: str,
    baseline_tag: str,
    baseline_tag_object: str,
    changelog: str,
) -> list[Failure]:
    """A declaration names this release, its baseline and ledger, and each id in the CHANGELOG section."""

    failures: list[Failure] = []
    if declaration["candidateVersion"] != candidate_version:
        failures.append(_failure("SOURCE_MISMATCH", "declaration", "declaration is for another version"))
    if declaration["baseline"] != {"tag": baseline_tag, "tagObject": baseline_tag_object}:
        failures.append(_failure("BASELINE_INVALID", "declaration", "declaration names another baseline"))
    if declaration["ledgerSha256"] != ledger_sha256:
        failures.append(_failure("SOURCE_MISMATCH", "declaration", "declaration binds another ledger"))
    known = {row["scenarioId"] for row in ledger["scenarios"]} | {row["scenarioId"] for row in ledger["retiredScenarios"]}
    for entry_id in _duplicates(entry["id"] for entry in declaration["entries"]):
        failures.append(_failure("STALE_DECLARATION", entry_id, "entry id used twice"))
    try:
        notes = render_release_notes(changelog, candidate_version)
    except ValueError as error:
        notes = ""
        failures.append(_failure("RELEASE_NOTE_MISSING", candidate_version, str(error)))
    for entry in declaration["entries"]:
        failures.extend(declaration_disposition_failures(entry))
        for side in (entry.get("expected") or {}).values():
            for code in sorted(set(side["issueCodes"]) & PROCESS_FAILURE_ISSUE_CODES):
                failures.append(_failure("PROCESS_FAILED", entry["id"], f"process-failure issue {code} cannot be declared"))
        match = ENTRY_ID.fullmatch(entry["id"])
        if match is None or match["version"] != candidate_version:
            failures.append(_failure("STALE_DECLARATION", entry["id"], "entry id does not name this version"))
        for scenario_id in entry["scenarioIds"]:
            if scenario_id not in known:
                failures.append(_failure("STALE_DECLARATION", entry["id"], f"unknown scenario {scenario_id}"))
        if notes and re.search(rf"(?<![0-9A-Za-z.-]){re.escape(entry['id'])}(?![0-9])", notes) is None:
            failures.append(_failure("RELEASE_NOTE_MISSING", entry["id"], "id absent from the CHANGELOG section"))
    return failures


def declaration_disposition_failures(entry: Mapping[str, Any]) -> list[Failure]:
    """A declaration cannot approve a rejection or a cause outside the contract's dispositions.

    These admissions accompany reproduction: typed JSON builders supply the
    fields; the validator remains the only owner of an approval decision.
    """
    subject = entry["id"]
    failures: list[Failure] = []
    kind = entry["kind"]
    approval = entry["approval"]
    if (approval["role"] != "firmware-owner"
        or re.fullmatch(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*) board decision [1-9][0-9]*", approval["boardDecision"]) is None):
        failures.append(_failure("STALE_DECLARATION", subject, "entry lacks firmware-owner board-decision approval"))
    issue = entry["knownIssue"]
    if issue is not None and re.fullmatch(r"BUG-[0-9]{8}-[a-z0-9-]+", issue.get("bugId", "")) is None:
        failures.append(_failure("STALE_DECLARATION", subject, "known issue lacks a bug identifier"))
    withdrawal = entry["routeWithdrawal"]
    if type(withdrawal) is not bool:
        failures.append(_failure("STALE_DECLARATION", subject, "withdrawal must be an explicit boolean"))
    if kind == "candidate-rejects" and not ((issue is not None and withdrawal is False) or (issue is None and withdrawal is True)):
        failures.append(_failure("STALE_DECLARATION", subject, "candidate rejection needs exactly one known issue or withdrawal"))
    if kind == "both-reject" and (issue is None or entry["routeWithdrawal"]):
        failures.append(_failure("STALE_DECLARATION", subject, "both rejections need a known issue without withdrawal"))
    if kind not in {"candidate-rejects", "both-reject"} and (issue is not None or entry["routeWithdrawal"]):
        failures.append(_failure("STALE_DECLARATION", subject, "disposition is not allowed for this entry kind"))
    if kind in COVERAGE_ENTRY_KINDS and (entry["expected"] is not None or entry["differences"] is not None):
        failures.append(_failure("STALE_DECLARATION", subject, "coverage disposition cannot declare artifact outcomes"))
    if kind not in set(OUTCOME_ENTRY_KINDS.values()) | COVERAGE_ENTRY_KINDS:
        failures.append(_failure("STALE_DECLARATION", subject, "unknown entry kind"))
    if not entry["routeIds"] or (kind != "accepted-gap" and not entry["scenarioIds"]) or (kind == "accepted-gap" and entry["scenarioIds"]):
        failures.append(_failure("STALE_DECLARATION", subject, "entry does not name its subjects"))
    for scope, difference in (entry["differences"] or {}).items():
        if difference is None:
            continue
        for cause in difference["attribution"]:
            if (cause["mechanism"] not in {"derived-field", "precursor-carried", "stopped-write-preserved-bytes", "writes-different-bytes"}
                or cause["causeVerification"] not in {"not-independently-verified", "supported-by-cited-evidence"}
                or cause["start"] < 0 or cause["start"] >= cause["endExclusive"]
                or not cause["cause"] or (cause["causeVerification"] == "supported-by-cited-evidence" and not cause["evidence"])):
                failures.append(_failure("STALE_DECLARATION", subject, f"{scope} attribution lacks an admitted cause and evidence disposition"))
    return failures


def reproduction_failures(entry: Mapping[str, Any], scenario: Mapping[str, Any], evidence: ScopeEvidence | None) -> list[Failure]:
    """An outcome entry reproduces one scenario exactly: both sides and each compared scope's complete ranges."""

    subject = f"{entry['id']}:{scenario['scenarioId']}"
    if OUTCOME_ENTRY_KINDS.get(scenario["outcome"]) != entry["kind"]:
        return [_failure("STALE_DECLARATION", subject, f"a {entry['kind']} entry for a {scenario['outcome']} outcome")]
    failures = [
        _failure("STALE_DECLARATION", subject, f"{side} outcome is not reproduced")
        for side in ("baseline", "candidate")
        if entry["expected"][side] != declared_side(scenario[side])
    ]
    differences = entry["differences"] or {"output": None, "precursor": None}
    computed = evidence or {}
    for scope in SCOPES:
        declared = differences[scope]
        observed = computed.get(scope)
        if (declared is None) != (observed is None) or (
            declared is not None and [dict(item) for item in declared["ranges"]] != [dict(item) for item in observed]
        ):
            failures.append(_failure("STALE_DECLARATION", subject, f"{scope} difference is not reproduced"))
        if declared is None:
            continue
        totals = range_projection(declared["ranges"])
        assert totals is not None
        if any(declared[key] != totals[key] for key in ("differentByteCount", "rangeCount", "rangeListSha256")):
            failures.append(_failure("STALE_DECLARATION", subject, f"{scope} totals or digest do not match its ranges"))
        failures.extend(range_list_failures(declared["ranges"], f"{subject}:{scope}", "STALE_DECLARATION"))
        ranges = [(item["start"], item["endExclusive"]) for item in declared["ranges"]]
        spans = [(item["start"], item["endExclusive"]) for item in declared["attribution"]]
        overlapping = sum(end - start for start, end in spans) != sum(end - start for start, end in _merged(spans))
        if overlapping or _merged(spans) != _merged(ranges):
            failures.append(_failure("STALE_DECLARATION", subject, f"{scope} attribution does not cover exactly its ranges"))
    return failures


def declared_change_failures(
    report: Mapping[str, Any],
    declaration: Mapping[str, Any] | None,
    evidence: Mapping[str, ScopeEvidence | None],
    *,
    ledger: Mapping[str, Any],
    baseline_ledger: Mapping[str, Any],
    candidate_version: str,
) -> list[Failure]:
    """Every change of the run is declared exactly once, by subject, and every declared subject happened.

    Changes are the outcomes other than `equal` and `invalid`, keyed by
    scenario, and the coverage changes other than added scenarios, keyed by
    scenario or, for an accepted gap, by route. `evidence` maps each scenario
    id to its computed scope evidence.
    """

    entries = list((declaration or {"entries": []})["entries"])
    failures: list[Failure] = []
    scenarios = {row["scenarioId"]: row for row in report["scenarios"]}
    routes = {row["scenarioId"]: row["routeId"] for row in (*baseline_ledger["scenarios"], *ledger["scenarios"])}
    expected: set[tuple[str, str]] = {
        (OUTCOME_ENTRY_KINDS[row["outcome"]], scenario_id)
        for scenario_id, row in scenarios.items()
        if row["outcome"] in OUTCOME_ENTRY_KINDS
    }
    changes = [row for row in coverage_changes(ledger, baseline_ledger) if row["kind"] != "scenario-added"]
    expected |= {(row["kind"], row["subject"]) for row in changes}
    declared: dict[tuple[str, str], list[Mapping[str, Any]]] = {}
    for entry in entries:
        subjects = entry["routeIds"] if entry["kind"] == "accepted-gap" else entry["scenarioIds"]
        for subject in subjects:
            declared.setdefault((entry["kind"], subject), []).append(entry)
        if entry["kind"] != "accepted-gap":
            expected_routes = sorted({routes.get(subject, "") for subject in entry["scenarioIds"]})
            if sorted(entry["routeIds"]) != expected_routes:
                failures.append(_failure("STALE_DECLARATION", entry["id"], "route ids differ from the routes of its scenarios"))
    for key in sorted(expected):
        matching = declared.get(key, [])
        if len(matching) != 1:
            failures.append(_failure("UNDECLARED_CHANGE", key[1], f"{key[0]} is declared by {len(matching)} entries, not one"))
    for key in sorted(set(declared) - expected):
        for entry in declared[key]:
            failures.append(_failure("STALE_DECLARATION", f"{entry['id']}:{key[1]}", f"declares a {key[0]} that did not happen"))
    for scenario_id, row in scenarios.items():
        if row["outcome"] not in OUTCOME_ENTRY_KINDS:
            if row["declarationEntryId"] is not None:
                failures.append(_failure("STALE_DECLARATION", scenario_id, "an entry id on an unchanged or invalid scenario"))
            continue
        matching = declared.get((OUTCOME_ENTRY_KINDS[row["outcome"]], scenario_id), [])
        if len(matching) == 1:
            if row["declarationEntryId"] != matching[0]["id"]:
                failures.append(_failure("STALE_DECLARATION", scenario_id, "report names another entry than the one declaring it"))
            failures.extend(reproduction_failures(matching[0], row, evidence.get(scenario_id)))
    change_ids = {
        (row["kind"], row["subject"]): row["declarationEntryId"] for row in report["coverage"]["changesSinceBaseline"]
    }
    for row in changes:
        matching = declared.get((row["kind"], row["subject"]), [])
        entry_id = matching[0]["id"] if len(matching) == 1 else None
        if change_ids.get((row["kind"], row["subject"])) != entry_id:
            failures.append(_failure("STALE_DECLARATION", row["subject"], f"report names another entry for its {row['kind']}"))
        if entry_id is None or row["kind"] == "input-revision":
            continue
        entry = matching[0]
        ledger_rows = ledger["acceptedGaps"] if row["kind"] == "accepted-gap" else ledger["retiredScenarios"]
        key = "routeId" if row["kind"] == "accepted-gap" else "scenarioId"
        version_key = "approvedInVersion" if row["kind"] == "accepted-gap" else "retiredInVersion"
        ledger_row = next((item for item in ledger_rows if item[key] == row["subject"]), None)
        if ledger_row is None or (ledger_row["declarationEntryId"], ledger_row[version_key], ledger_row["approval"]) != (
            entry["id"],
            candidate_version,
            entry["approval"],
        ):
            failures.append(_failure("STALE_DECLARATION", row["subject"], f"ledger {row['kind']} record does not cite its entry"))
    return failures


# ---------------------------------------------------------------------------
# Source binding
# ---------------------------------------------------------------------------


class SourceAuthority(NamedTuple):
    """What a report must bind, measured by the comparator from Git and the files it applied."""

    candidate_commit: str
    candidate_tree: str
    baseline_tag: str
    baseline_tag_object: str
    baseline_commit: str
    comparator_sha256: str
    contracts: Mapping[str, str]
    ledger_sha256: str | None = None
    declaration_sha256: str | None = None
    amendment_sha256: str | None = None
    published_inventory: Mapping[str, str] | None = None


def source_binding_failures(report: Mapping[str, Any], authority: SourceAuthority) -> list[Failure]:
    """A report binds the exact source, baseline, comparator and applied documents."""

    failures: list[Failure] = []
    candidate = report["candidate"]["executor"]
    if (candidate["commit"], candidate["tree"]) != (authority.candidate_commit, authority.candidate_tree):
        failures.append(_failure("SOURCE_MISMATCH", "candidate", "report does not come from the candidate source"))
    baseline = report["baseline"]
    if (baseline["tag"], baseline["executor"]["tagObject"], baseline["executor"]["commit"]) != (
        authority.baseline_tag,
        authority.baseline_tag_object,
        authority.baseline_commit,
    ):
        failures.append(_failure("BASELINE_INVALID", "baseline", "report names another baseline"))
    if report["comparator"]["scriptSha256"] != authority.comparator_sha256:
        failures.append(_failure("SOURCE_MISMATCH", "comparator", "report comes from another comparator"))
    listed = report["comparator"]["contracts"]
    applied = {row["path"]: row["sha256"] for row in listed}
    if applied != dict(authority.contracts) or len(applied) != len(listed):
        failures.append(_failure("SOURCE_MISMATCH", "contracts", "report binds other contract identities"))
    if report["mode"] == "rolling":
        bound = (("ledgerSha256", authority.ledger_sha256), ("declarationSha256", authority.declaration_sha256))
        if report.get("publishedInventory") != authority.published_inventory:
            failures.append(_failure("SOURCE_MISMATCH", "publishedInventory", "report binds another inventory"))
        if report["formal"] and report.get("publishedInventory") is None:
            failures.append(_failure("BASELINE_INVALID", "publishedInventory", "formal run requires publication inventory"))
    else:
        bound = (("amendmentSha256", authority.amendment_sha256),)
    for member, expected in bound:
        if report[member] != expected:
            failures.append(_failure("SOURCE_MISMATCH", member, f"report binds another {member}"))
    return failures


def rolling_report_failures(
    report: Mapping[str, Any],
    *,
    ledger: Mapping[str, Any],
    baseline_ledger: Mapping[str, Any],
    policy: Mapping[str, Any],
    manifest: Mapping[str, Any],
    case_manifests: Mapping[str, Mapping[str, Any]],
    plan: Mapping[str, Any],
    declaration: Mapping[str, Any] | None,
    changelog: str,
    authority: SourceAuthority,
    evidence: Mapping[str, ScopeEvidence | None],
) -> list[Failure]:
    """Every semantic rule of a rolling report; the gate is clear only when this returns nothing."""

    failures = ledger_failures(ledger, policy=policy, manifest=manifest, case_manifests=case_manifests, plan=plan)
    failures.extend(monotonicity_failures(ledger, baseline_ledger))
    failures.extend(report_of_record_blockers(ledger))
    failures.extend(
        rolling_coverage_report_failures(report, ledger=ledger, baseline_ledger=baseline_ledger, policy=policy, manifest=manifest)
    )
    failures.extend(accepted_gap_scope_failures(ledger, report["candidate"]["version"]))
    active = {row["scenarioId"]: row for row in ledger["scenarios"]}
    for row in report["scenarios"]:
        failures.extend(scenario_value_failures(row, evidence.get(row["scenarioId"])))
        if row["scenarioId"] in active:
            failures.extend(artifact_presence_failures(row, active[row["scenarioId"]]))
        if row["outcome"] == "invalid":
            failures.append(_failure(row["failureCode"].removeprefix("PREDECESSOR_"), row["scenarioId"], "scenario is invalid"))
    if declaration is None and report["formal"]:
        failures.append(_failure("UNDECLARED_CHANGE", "declaration", "no declaration for this release"))
    if declaration is not None:
        failures.extend(
            declaration_failures(
                declaration,
                ledger=ledger,
                ledger_sha256=authority.ledger_sha256 or "",
                candidate_version=report["candidate"]["version"],
                baseline_tag=authority.baseline_tag,
                baseline_tag_object=authority.baseline_tag_object,
                changelog=changelog,
            )
        )
    failures.extend(
        declared_change_failures(
            report,
            declaration,
            evidence,
            ledger=ledger,
            baseline_ledger=baseline_ledger,
            candidate_version=report["candidate"]["version"],
        )
    )
    failures.extend(source_binding_failures(report, authority))
    failures.extend(deterministic_digest_failures(report))
    return failures


def rolling_gate(failures: Sequence[Failure]) -> dict[str, Any]:
    """The rolling gate: clear only without any failure; failures sorted for a deterministic report."""

    ordered = sorted(set(failures), key=lambda failure: (failure.code, failure.subject, failure.detail))
    return {
        "result": "blocked" if ordered else "clear",
        "failures": [{"code": failure.code, "subject": failure.subject, "detail": failure.detail} for failure in ordered],
    }


# ---------------------------------------------------------------------------
# v0.9.16 1.x mode
# ---------------------------------------------------------------------------


class RouteDisposition(NamedTuple):
    """How the v0.9.16 1.x mode proves one plan route, from the plan and the amendment."""

    route_id: str
    capability_fingerprint: str
    proof_kind: str
    row: Mapping[str, Any] | None
    row_source: str | None
    row_member: str | None


def plan_selected_routes(plan: Mapping[str, Any], pinned_policy: Mapping[str, Any]) -> list[dict[str, Any]]:
    """The plan's routes, selected from the policy it pins, in policy order."""

    selection = plan["selection"]
    return [
        route
        for route in pinned_policy["routes"]
        if route["authoring"]["value"] == selection["authoring"]
        and route["publication"]["value"] == selection["publication"]
        and route["workflowId"] in selection["includedWorkflows"]
    ]


def amendment_binding_failures(amendment: Mapping[str, Any], plan: Mapping[str, Any]) -> list[Failure]:
    """The amendment binds the plan without its candidateAuthority member, and the plan's baseline."""

    binding = amendment["plan"]
    observed = {
        "withoutCandidateAuthorityJcsSha256": canonical_json_sha256(
            {key: value for key, value in plan.items() if key != "candidateAuthority"}
        ),
        "canonicalInputAuthorityJcsSha256": canonical_json_sha256(plan["canonicalInputAuthority"]),
        "policySha256": plan["policyBinding"]["sha256"],
        "baselineTagObject": plan["baseline"]["tagObject"],
        "baselinePeeledCommit": plan["baseline"]["peeledCommit"],
    }
    return [
        _failure("AMENDMENT_MISMATCH", key, "the amendment binds another plan")
        for key, value in observed.items()
        if binding[key] != value
    ]


def v0916_policy_capture_failures(plan: Mapping[str, Any], raw_sha256: str) -> list[Failure]:
    return [] if plan["policyBinding"]["sha256"] == raw_sha256 else [
        _failure("INPUT_INVALID", "policy", "policy differs from the plan's pinned authority")]


def v0916_route_dispositions(
    plan: Mapping[str, Any], amendment: Mapping[str, Any], pinned_policy: Mapping[str, Any]
) -> tuple[list[RouteDisposition], list[Failure]]:
    """Each plan route's proof kind and approved row; an ambiguous or unbound row fails closed."""

    routes = plan_selected_routes(plan, pinned_policy)
    fingerprints = {route["routeId"]: route["capabilityFingerprint"] for route in routes}
    missing = set(plan["canonicalInputAuthority"]["currentlyMissingRouteIds"])
    transitive = {row["routeId"]: row for row in plan["transitiveRoutes"]}
    rows: dict[str, list[tuple[str, str, Mapping[str, Any]]]] = {}
    for source, document, member in (
        ("plan", plan, "approvedSemanticCorrections"),
        ("amendment", amendment, "approvedSemanticCorrections"),
        ("amendment", amendment, "baselineNotApplicable"),
    ):
        for row in document[member]:
            rows.setdefault(row["routeId"], []).append((source, member, row))
    failures: list[Failure] = []
    for route_id, listed in sorted(rows.items()):
        if len(listed) > 1 or route_id in transitive:
            failures.append(_failure("AMENDMENT_MISMATCH", route_id, "more than one disposition for one route"))
        for _, _, row in listed:
            if fingerprints.get(route_id) != row["capabilityFingerprint"]:
                failures.append(_failure("AMENDMENT_MISMATCH", route_id, "row does not name a plan route and fingerprint"))
            if route_id in missing:
                failures.append(_failure("AMENDMENT_MISMATCH", route_id, "row names a route without canonical input"))
    dispositions: list[RouteDisposition] = []
    for route in routes:
        route_id = route["routeId"]
        source = member = None
        row: Mapping[str, Any] | None = None
        if route_id in missing:
            proof_kind = "not-covered"
        elif route_id in transitive:
            proof_kind, row, source, member = "tp-prefix-transitive", transitive[route_id], "plan", "transitiveRoutes"
        elif route_id in rows:
            source, member, row = rows[route_id][0]
            proof_kind = (
                "exact-output-with-approved-semantic-correction"
                if member == "approvedSemanticCorrections"
                else "canonical-binding-not-applicable-to-v0916"
            )
        else:
            proof_kind = "exact-output"
        dispositions.append(RouteDisposition(route_id, route["capabilityFingerprint"], proof_kind, row, source, member))
    return dispositions, failures


class TransitiveEvidence(NamedTuple):
    """The actual result of the ADR 0057 `compare_transitive_payloads` for one route.

    `tp_length` is the length the primitive was given. `checks` is the
    mapping it returned when it passed; `failure_code` is the code of the
    `ParityError` it raised otherwise.
    """

    tp_length: int
    checks: Mapping[str, bool] | None
    failure_code: str | None
    failure_checks: Mapping[str, bool] | None = None


def transitive_evidence(
    baseline_full: bytes, candidate_full: bytes, candidate_tp: bytes, candidate_base: bytes, tp_length: int
) -> TransitiveEvidence:
    """Run `compare_transitive_payloads` on bytes the caller read and keep its result or its failure code."""

    try:
        result = compare_transitive_payloads(baseline_full, candidate_full, candidate_tp, candidate_base, tp_length)
    except ParityError as error:
        checks = None
        if error.code in {"PARITY_TP_PREFIX_MISMATCH", "PARITY_TAIL_MUTATED"}:
            checks = dict(zip(TRANSITIVE_CHECKS, (candidate_tp == candidate_full[:tp_length],
                                                candidate_tp == baseline_full[:tp_length],
                                                candidate_full[tp_length:] == candidate_base[tp_length:]), strict=True))
        return TransitiveEvidence(tp_length, None, error.code, checks)
    return TransitiveEvidence(tp_length, {check: result[check] for check in TRANSITIVE_CHECKS}, None)


def _transitive_checks_agree(reported: Mapping[str, Any], evidence: TransitiveEvidence) -> bool:
    """Whether the reported checks are what the primitive's result fixes.

    A pass fixes all three checks. The primitive checks the lengths, then both
    TP prefixes, then the tail, and stops at the first failure: a mutated
    tail fixes both prefix checks true and the tail false, a prefix mismatch
    fixes one prefix check false, and any other failure fixes one check false.
    """

    values = tuple(reported.get(check) for check in TRANSITIVE_CHECKS)
    if evidence.checks is not None:
        return values == tuple(evidence.checks[check] for check in TRANSITIVE_CHECKS)
    if evidence.failure_checks is not None:
        return values == tuple(evidence.failure_checks[check] for check in TRANSITIVE_CHECKS)
    if evidence.failure_code == "PARITY_TAIL_MUTATED":
        return values == (True, True, False)
    if evidence.failure_code == "PARITY_TP_PREFIX_MISMATCH":
        return values[0] is False or values[1] is False
    return False in values


class V0916RouteEvidence(NamedTuple):
    """Computed evidence of one v0.9.16 route.

    `scopes` is the scope evidence of the route's two sides; `transitive` is
    the primitive's result for a transitive route (see `transitive_evidence`).
    """

    scopes: ScopeEvidence
    transitive: TransitiveEvidence | None = None
    correction_reproduced: bool | None = None
    binding: Mapping[str, Any] | None = None


def approved_correction_evidence(before: bytes, after: bytes, row: Mapping[str, Any]) -> bool:
    """Apply the unchanged ADR 0057 correction primitive to this exact row."""
    try:
        compare_approved_semantic_correction_payloads(before, after, row)
    except ParityError:
        return False
    return True


def transitive_projection(row: Mapping[str, Any], computed: TransitiveEvidence) -> dict[str, Any]:
    """Project the primitive's pass or first failure into the report checks."""
    checks = computed.checks or computed.failure_checks
    if checks is None:
        # Invalid lengths admit no byte relation as a passing proof.
        checks = dict.fromkeys(TRANSITIVE_CHECKS, False)
    return {"fullRouteId": row["fullRouteId"], "tpLength": computed.tp_length, **checks}


class V0916RouteVerdict(NamedTuple):
    result: str
    failure_code: str | None
    failures: list[Failure]


def v0916_route_verdict(
    route: Mapping[str, Any], disposition: RouteDisposition, evidence: V0916RouteEvidence | None,
    routes: Mapping[str, Mapping[str, Any]], execution_failures: Sequence[Failure] = (),
) -> V0916RouteVerdict:
    """Classify an acquired route with the same rules that validate its report."""
    subject = disposition.route_id
    if disposition.proof_kind == "not-covered":
        return V0916RouteVerdict("not-covered", None, [])
    failures = [Failure(item.code, subject, item.detail) for item in execution_failures]
    if failures:
        return V0916RouteVerdict("invalid", failures[0].code, failures)
    sides = [route["baseline"], route["candidate"]]
    full = routes.get((disposition.row or {}).get("fullRouteId", ""))
    if disposition.proof_kind == "tp-prefix-transitive":
        if _transitive_blocker(route, full) == "invalid":
            code = (full or {}).get("failureCode") or "PREDECESSOR_REPORT_INVALID"
            return V0916RouteVerdict("invalid", code, [Failure(code, subject, "transitive dependency failed")])
    if any(side is not None and side["status"] == "invalid" for side in sides):
        return V0916RouteVerdict("invalid", "PREDECESSOR_REPORT_INVALID",
                                 [_failure("REPORT_INVALID", subject, "route has an invalid side")])
    tentative = {**route, "result": "consistent", "failureCode": None}
    failures = _v0916_route_failures(tentative, disposition, evidence, routes)
    if disposition.proof_kind == "exact-output-with-approved-semantic-correction" and (
        evidence is None or evidence.correction_reproduced is not True
    ):
        failures.append(_failure("AMENDMENT_MISMATCH", subject, "outputs do not reproduce the exact correction row"))
    if disposition.proof_kind == "tp-prefix-transitive" and _transitive_blocker(route, full) is not None:
        failures = [_failure("UNAPPROVED_DIFFERENCE", subject, "a typed rejection prevents the transitive proof")]
    if failures:
        mismatch = disposition.proof_kind in {"exact-output-with-approved-semantic-correction", "canonical-binding-not-applicable-to-v0916"}
        # Value mismatches are product inconsistency; acquisition/safety was
        # already classified above through the shared execution verdict.
        code = "PREDECESSOR_AMENDMENT_MISMATCH" if mismatch else "PREDECESSOR_UNAPPROVED_DIFFERENCE"
        return V0916RouteVerdict("inconsistent", code, [Failure(code, subject, "route does not reproduce its proof")])
    return V0916RouteVerdict("consistent", None, [])


def _transitive_blocker(route: Mapping[str, Any], full_route: Mapping[str, Any] | None) -> str | None:
    """Why a transitive proof cannot run, or None when the three outputs it reads exist.

    The proof reads the candidate TP output and both outputs of the full
    route. `rejected`: a typed product rejection left one of them missing.
    `invalid`: a side failed otherwise, or the full route has no side.
    """

    sides = [route["candidate"]]
    sides += [None, None] if full_route is None else [full_route["baseline"], full_route["candidate"]]
    if all(side is not None and side["status"] == "output" for side in sides):
        return None
    if any(side is None or side["status"] == "invalid" for side in sides):
        return "invalid"
    return "rejected"


transitive_blocker = _transitive_blocker


def _transitive_route_failures(
    route: Mapping[str, Any],
    row: Mapping[str, Any],
    evidence: V0916RouteEvidence | None,
    full_route: Mapping[str, Any] | None,
) -> list[Failure]:
    """A TP-prefix transitive route against its plan row, its full route and the primitive's result.

    A proof that cannot run is not asked for bytes: the route is `invalid`
    when a side failed and otherwise `inconsistent` with
    `PREDECESSOR_UNAPPROVED_DIFFERENCE`, and it reports and carries no proof.
    A proof that can run needs the primitive's result, run with the plan's
    TP length; a passing proof also fixes the TP output size.
    """

    subject = route["planRouteId"]
    failures: list[Failure] = []
    computed = None if evidence is None else evidence.transitive
    blocker = _transitive_blocker(route, full_route)
    if blocker is not None:
        # An invalid route's code is checked with every invalid route.
        expected = "invalid" if blocker == "invalid" else "inconsistent"
        if route["result"] != expected or (
            expected == "inconsistent" and route["failureCode"] != "PREDECESSOR_UNAPPROVED_DIFFERENCE"
        ):
            failures.append(_failure("REPORT_INVALID", subject, f"a transitive route whose proof cannot run is {expected}"))
        if route["transitive"] is not None:
            failures.append(_failure("REPORT_INVALID", subject, "reports a transitive proof that could not run"))
        if computed is not None:
            failures.append(_failure("REPORT_INVALID", subject, "transitive evidence for a proof that could not run"))
        return failures
    if evidence is not None:
        failures.extend(
            scope_evidence_failures(
                subject, route["baseline"], route["candidate"], {"output": route["comparison"]}, evidence.scopes, ("output",)
            )
        )
    reported = route["transitive"] or {}
    if (reported.get("fullRouteId"), reported.get("tpLength")) != (row.get("fullRouteId"), row.get("tpLength")):
        failures.append(_failure("REPORT_INVALID", subject, "transitive proof names another full route or TP length than the plan"))
    if computed is None:
        failures.append(_failure("REPORT_INVALID", subject, "transitive evidence is missing"))
    else:
        if computed.tp_length != row.get("tpLength"):
            failures.append(_failure("REPORT_INVALID", subject, "transitive evidence was computed with another TP length than the plan"))
        if not _transitive_checks_agree(reported, computed):
            failures.append(_failure("REPORT_INVALID", subject, "transitive checks differ from the computed proof"))
        if computed.checks is not None and len({route["candidate"]["output"]["size"], computed.tp_length, row.get("tpLength")}) != 1:
            failures.append(_failure("REPORT_INVALID", subject, "a passing proof whose TP output size is not the plan and proof TP length"))
    passed = computed is not None and computed.checks is not None and all(computed.checks[check] for check in TRANSITIVE_CHECKS)
    if route["result"] == "consistent" and (not passed or (full_route or {}).get("result") != "consistent"):
        failures.append(_failure("UNAPPROVED_DIFFERENCE", subject, "a consistent transitive route without a passing proof and full route"))
    return failures


def _v0916_route_failures(
    route: Mapping[str, Any],
    disposition: RouteDisposition,
    evidence: V0916RouteEvidence | None,
    routes: Mapping[str, Mapping[str, Any]],
) -> list[Failure]:
    subject = route["planRouteId"]
    failures = process_failure_issue_failures(subject, (route["baseline"], route["candidate"]))
    if route["result"] == "not-covered":
        if disposition.proof_kind != "not-covered" or any(route[member] is not None for member in (
            "baseline", "candidate", "comparison", "dispositionRow", "transitive", "failureCode"
        )) or evidence is not None:
            failures.append(_failure("REPORT_INVALID", subject, "not-covered route was executed or counted as compared"))
        return failures
    if disposition.proof_kind == "not-covered":
        return failures + [_failure("REPORT_INVALID", subject, "not-covered route counted as compared")]
    # A route is invalid when a side fails for a reason that is not a product
    # result, with the codes of the shared execution failures (contract).
    if route["result"] == "invalid" and route["failureCode"] not in EXECUTION_FAILURE_CODES:
        failures.append(_failure("REPORT_INVALID", subject, "an invalid route without a shared execution failure code"))
    if disposition.proof_kind == "tp-prefix-transitive":
        row = disposition.row or {}
        return failures + _transitive_route_failures(route, row, evidence, routes.get(row.get("fullRouteId", "")))
    if any(side is not None and side.get("status") == "invalid" for side in (route["baseline"], route["candidate"])) and route["result"] != "invalid":
        failures.append(_failure("REPORT_INVALID", subject, "a non-transitive route with an invalid side must be invalid"))
    if route["result"] == "invalid":
        return failures
    if disposition.proof_kind == "canonical-binding-not-applicable-to-v0916" and route["result"] == "consistent" and (
        evidence is None or evidence.binding is None
    ):
        failures.append(_failure("AMENDMENT_MISMATCH", subject, "canonical binding evidence is missing"))
    if evidence is None:
        return failures + [_failure("REPORT_INVALID", subject, "computed evidence for a run route is missing")]
    # A v0.9.16 route reports only its output comparison; its consistency is its
    # proof kind's (a precursor enters only through a not-applicable row).
    failures.extend(
        scope_evidence_failures(
            subject, route["baseline"], route["candidate"], {"output": route["comparison"]}, evidence.scopes, ("output",)
        )
    )
    output_differs = evidence.scopes.get("output") is not None
    baseline_output = (route["baseline"] or {}).get("output")
    candidate_output = (route["candidate"] or {}).get("output")
    if disposition.proof_kind == "exact-output" and (route["baseline"] is None or route["candidate"] is None):
        failures.append(_failure("REPORT_INVALID", subject, "an exact-output route is missing a report side"))
    if disposition.proof_kind == "exact-output" and route["result"] == "consistent" and output_differs:
        failures.append(_failure("UNAPPROVED_DIFFERENCE", subject, "a consistent exact-output route with differing bytes"))
    if disposition.proof_kind == "exact-output" and route["result"] == "consistent" and (
        baseline_output is None or candidate_output is None
    ):
        failures.append(_failure("UNAPPROVED_DIFFERENCE", subject, "an exact-output route without both outputs"))
    if (
        disposition.proof_kind == "exact-output"
        and not output_differs
        and baseline_output is not None
        and _identity(baseline_output) == _identity(candidate_output)
        and route["result"] != "consistent"
    ):
        failures.append(_failure("REPORT_INVALID", subject, "an exact-output route with equal outputs must be consistent"))
    if disposition.proof_kind == "exact-output-with-approved-semantic-correction" and route["result"] == "consistent":
        if evidence.correction_reproduced is False:
            failures.append(_failure("AMENDMENT_MISMATCH", subject, "correction primitive did not reproduce its row"))
        row = disposition.row or {}
        observed = {
            "baselineOutput": _identity(baseline_output),
            "candidateOutput": _identity(candidate_output),
            "differentRanges": None if not output_differs else [dict(item) for item in evidence.scopes["output"]],
        }
        observed["differentByteCount"] = None if route["comparison"] is None else route["comparison"]["differentByteCount"]
        for key, value in observed.items():
            if value != row.get(key):
                failures.append(_failure("AMENDMENT_MISMATCH", subject, f"{key} does not reproduce the approved row"))
    if disposition.proof_kind == "canonical-binding-not-applicable-to-v0916" and route["result"] == "consistent":
        row = disposition.row or {}
        baseline, candidate = route["baseline"], route["candidate"]
        if baseline is None or candidate is None or baseline["status"] != "rejected" or candidate["status"] != "output":
            return failures + [_failure("AMENDMENT_MISMATCH", subject, "not-applicable row requires a baseline rejection and candidate output")]
        if evidence.binding is not None and evidence.binding != row["binding"]:
            failures.append(_failure("AMENDMENT_MISMATCH", subject, "canonical binding does not reproduce the approved row"))
        observed_rows = {
            "precursor": (_identity(baseline["precursor"]), _identity(candidate["precursor"])),
            "stage": baseline["stoppedAt"],
            "issueCodes": sorted({issue["code"] for issue in baseline["issues"] if issue["severity"] == "error"}),
            "output": _identity(candidate["output"]),
        }
        expected_rows = {
            "precursor": (row["expectedBaseline"]["precursorOutput"], row["expectedCandidate"]["precursorOutput"]),
            "stage": row["expectedBaseline"]["rejectingStage"],
            "issueCodes": sorted(row["expectedBaseline"]["issueCodes"]),
            "output": row["expectedCandidate"]["output"],
        }
        for key, value in observed_rows.items():
            if value != expected_rows[key]:
                failures.append(_failure("AMENDMENT_MISMATCH", subject, f"{key} does not reproduce the approved row"))
    return failures


def v0916_result(route_results: Sequence[str]) -> str:
    """Any invalid route makes the run invalid; otherwise any inconsistent route makes it inconsistent."""

    if "invalid" in route_results:
        return "invalid"
    if "inconsistent" in route_results:
        return "inconsistent"
    return "consistent"


def v0916_report_failures(
    report: Mapping[str, Any],
    dispositions: Sequence[RouteDisposition],
    evidence: Mapping[str, V0916RouteEvidence | None],
    *, authority: SourceAuthority | None = None, plan: Mapping[str, Any] | None = None,
) -> list[Failure]:
    """A v0.9.16 1.x report covers each plan route once with its proof, row, evidence, summary and result."""

    failures = v0916_milestone_failures(formal=report.get("formal", False), milestone=report.get("milestone"))
    failures.extend(deterministic_digest_failures(report))
    if authority is not None:
        failures.extend(source_binding_failures(report, authority))
    if plan is not None and report["planBinding"] != v0916_plan_binding(plan):
        failures.append(_failure("SOURCE_MISMATCH", "planBinding", "report binds another plan"))
    expected = {row.route_id: row for row in dispositions}
    reported = [route["planRouteId"] for route in report["routes"]]
    for route_id in _duplicates(reported):
        failures.append(_failure("REPORT_INVALID", route_id, "route reported more than once"))
    for route_id in sorted(set(expected) - set(reported)):
        failures.append(_failure("REPORT_INVALID", route_id, "plan route missing from the report"))
    for route_id in sorted(set(reported) - set(expected)):
        failures.append(_failure("REPORT_INVALID", route_id, "reported route is not a plan route"))
    routes = {route["planRouteId"]: route for route in report["routes"]}
    results = {route_id: route["result"] for route_id, route in routes.items()}
    for route in report["routes"]:
        disposition = expected.get(route["planRouteId"])
        if disposition is None:
            continue
        subject = route["planRouteId"]
        if (
            route["planCapabilityFingerprint"] != disposition.capability_fingerprint
            or route["proofKind"] != disposition.proof_kind
        ):
            failures.append(_failure("REPORT_INVALID", subject, "proof kind or fingerprint differs from the plan"))
            continue
        declared_row = {"source": disposition.row_source, "member": disposition.row_member, "routeId": subject}
        if disposition.row_member in {"approvedSemanticCorrections", "baselineNotApplicable"} and (
            route["dispositionRow"] != declared_row
        ):
            failures.append(_failure("REPORT_INVALID", subject, "disposition row differs from the plan and amendment"))
        failures.extend(_v0916_route_failures(route, disposition, evidence.get(subject), routes))
    values = list(results.values())
    summary = {
        "consistent": values.count("consistent"),
        "inconsistent": values.count("inconsistent"),
        "invalid": values.count("invalid"),
        "notCovered": values.count("not-covered"),
    }
    if report["summary"] != summary:
        failures.append(_failure("REPORT_INVALID", "summary", "summary differs from the route results"))
    if report["result"] != v0916_result(values):
        failures.append(_failure("REPORT_INVALID", "result", "result differs from the route results"))
    if report["result"] == "consistent" and not any(value != "not-covered" for value in values):
        failures.append(_failure("REPORT_INVALID", "result", "a consistent result requires at least one compared route"))
    return failures


def v0916_plan_binding(plan: Mapping[str, Any]) -> dict[str, str]:
    """Bind only the historical authority; candidateAuthority is never consumed."""
    return {"path": "docs/contracts/v0916-parity-certification-v1.json",
            "withoutCandidateAuthorityJcsSha256": canonical_json_sha256(
                {key: value for key, value in plan.items() if key != "candidateAuthority"}),
            "canonicalInputAuthorityJcsSha256": canonical_json_sha256(plan["canonicalInputAuthority"])}


def v0916_summary(route_results: Sequence[str]) -> dict[str, int]:
    return {"consistent": route_results.count("consistent"), "inconsistent": route_results.count("inconsistent"),
            "invalid": route_results.count("invalid"), "notCovered": route_results.count("not-covered")}

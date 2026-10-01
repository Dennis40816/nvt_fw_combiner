"""B1 shared execution for both predecessor modes; no mode, report or gate.

Semantic decisions live in predecessor_validation. This layer acquires Git
authority, executes under custody and measures payload-free captures. Runtime
bytes remain local and are never included in a side object or executor identity.
"""

from __future__ import annotations

from contextlib import contextmanager
import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import stat
import subprocess
import tempfile
import threading
import sys
from typing import Any, Callable, ContextManager, Iterator, Mapping, NamedTuple, Protocol, Sequence

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from scripts import predecessor_validation as validation
from scripts.predecessor_report_reader import ReadReport, ReportReaderError, read_cli_report
from scripts.v0916_parity_certification import (
    CapturedExecutionClosure,
    LocalExecutionHost,
    MaterializedCanonicalAuthority,
    ParityError,
    PinnedGitReader,
    admit_case_inputs,
    canonical_json_sha256,
    cli_arguments,
    detached_git_worktree,
    hold_read_only_file_custody,
    load_json_reject_duplicates,
    materialize_execution_closure,
    runtime_closure_inventory,
)


ROOT = Path(__file__).resolve().parents[1]
CONTRACT_PATH = ROOT / "docs/contracts/predecessor-comparison-v1.json"
_ENVIRONMENT_LOCK = threading.RLock()


class ExecutionError(ValueError):
    """Refusal carrying one of the shared contract failure codes."""

    def __init__(self, code: str, detail: str):
        self.code = code
        super().__init__(detail)


class ProcessHost(Protocol):
    """LocalExecutionHost.run contract: bounded, captured, nonzero not raised."""

    def run(self, argv: list[str], cwd: Path) -> subprocess.CompletedProcess[str]: ...


class GitHost(Protocol):
    def list_files(self, commit: str) -> list[str]: ...
    def read_file(self, commit: str, path: str) -> bytes: ...
    def git_head(self, root: Path) -> str: ...
    def git_tag_object(self, ref: str) -> str: ...
    def git_tag_commit(self, tag_object: str) -> str: ...
    def git_tree(self, root: Path) -> str: ...
    def git_tree_for_path(self, root: Path, path: str) -> str: ...
    def git_dirty_paths(self, root: Path) -> list[str]: ...
    def git_ignored_build_paths(self, root: Path) -> list[str]: ...
    def detached_worktree(self, commit: str, temporary_root: Path, name: str) -> ContextManager[Path]: ...


class LocalGitHost:
    """Composition of the existing ADR 0057 adapters, with no second Git reader."""

    def __init__(self, repository: Path):
        self.repository = repository
        self.reader = PinnedGitReader(repository)
        self.host = LocalExecutionHost()

    def list_files(self, commit: str) -> list[str]:
        return self.reader.list_files(commit)

    def read_file(self, commit: str, path: str) -> bytes:
        return self.reader.read_file(commit, path)

    def _git(self, *arguments: str) -> str:
        return subprocess.check_output(["git", *arguments], cwd=self.repository,
                                       text=True, stderr=subprocess.PIPE).strip()

    def git_tag_object(self, ref: str) -> str:
        """Resolve an annotated tag object; a lightweight tag is refused by Git."""
        return self._git("rev-parse", "--verify", f"{ref}^{{tag}}")

    def git_tag_commit(self, tag_object: str) -> str:
        return self._git("rev-parse", "--verify", f"{tag_object}^{{commit}}")

    def git_head(self, root: Path) -> str:
        return self.host.git_head(root)

    def git_tree(self, root: Path) -> str:
        return self.host.git_tree(root)

    def git_tree_for_path(self, root: Path, path: str) -> str:
        return self.host.git_tree_for_path(root, path)

    def git_dirty_paths(self, root: Path) -> list[str]:
        return self.host.git_dirty_paths(root)

    def git_ignored_build_paths(self, root: Path) -> list[str]:
        return self.host.git_ignored_build_paths(root)

    def detached_worktree(self, commit: str, temporary_root: Path, name: str) -> ContextManager[Path]:
        return detached_git_worktree(self.repository, commit, temporary_root, name)


class Executor(NamedTuple):
    identity: dict[str, Any]
    closure: CapturedExecutionClosure
    report_version: str


class BaselineExecutorBuilder(Protocol):
    """Seam for the pending v0.9.16 executor; no recipe is invented here."""

    def build(self, git: GitHost, runner: ProcessRunner, commit: str, record: Mapping[str, Any]) -> Executor: ...


class ContractAdmission(NamedTuple):
    formal: bool
    diagnostic: bool
    pending: list[str]
    contract: Mapping[str, Any]
    compiler_host: Mapping[str, Any]
    baseline_executor: Mapping[str, Any] | None


def admit_execution_contract(
    contract_path: Path = CONTRACT_PATH, *, amendment_path: Path | None = None,
    mode: str, formal: bool,
) -> ContractAdmission:
    """Load interface statuses, refusing formal execution until in effect.

    Rolling never reads the amendment. v0916-1x reads only its baselineExecutor
    status here; applying amendment rows belongs to B2.
    """

    contract = load_json_reject_duplicates(contract_path.read_bytes())
    _refuse(validation.execution_mode_failures(contract, mode))
    amendment = None
    if mode == "v0916-1x":
        path = amendment_path or ROOT / contract["modes"][mode]["amendment"]
        amendment = load_json_reject_duplicates(path.read_bytes())
    return admit_loaded_execution_contract(contract, mode=mode, formal=formal, amendment=amendment)


def admit_loaded_execution_contract(
    contract: Mapping[str, Any], *, mode: str, formal: bool, amendment: Mapping[str, Any] | None = None,
) -> ContractAdmission:
    """Use the same admission for a pinned snapshot and a loaded local contract."""
    _refuse(validation.execution_mode_failures(contract, mode))
    _refuse(validation.formal_interface_failures(contract, formal=formal, amendment=amendment))
    pending = validation.pending_execution_interfaces(contract, amendment)
    return ContractAdmission(formal, not formal, pending, contract, contract["executor"]["compilerHost"],
                             None if amendment is None else amendment["baselineExecutor"])


def local_settings_folder() -> Path:
    """Query Windows' actual LocalApplicationData; never redirect user settings."""

    if os.name != "nt":
        raise ExecutionError("PREDECESSOR_ENVIRONMENT_INVALID", "local settings folder requires Windows or an injected path")
    buffer = ctypes.create_unicode_buffer(32768)
    # CSIDL_LOCAL_APPDATA, SHGFP_TYPE_CURRENT: matches the CLI's SpecialFolder.
    result = ctypes.windll.shell32.SHGetFolderPathW(None, 0x001C, None, 0, buffer)
    if result != 0:
        raise ExecutionError("PREDECESSOR_ENVIRONMENT_INVALID", "cannot resolve LocalApplicationData")
    return Path(buffer.value) / "NvtFwCombiner"


def _sha256(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def _payload_identity(payload: bytes) -> dict[str, Any]:
    return {"size": len(payload), "sha256": _sha256(payload)}


def _file_identity(path: Path) -> dict[str, Any] | None:
    return _payload_identity(path.read_bytes()) if path.exists() else None


def _stream_bytes(value: str | bytes | None) -> bytes:
    return value.encode("utf-8") if isinstance(value, str) else value or b""


def _refuse(failures: Sequence[validation.Failure]) -> None:
    if failures:
        raise ExecutionError(failures[0].code, failures[0].detail)


@contextmanager
def _temporary_environment(directory: Path) -> Iterator[None]:
    """Adapt the existing host without copying its subprocess implementation.

    LocalExecutionHost inherits the process environment; serialize the three
    temporary overrides through invocation and restore them even on failure.
    APPDATA, LOCALAPPDATA and USERPROFILE are never changed.
    """

    with _ENVIRONMENT_LOCK:
        previous = {name: os.environ.get(name) for name in ("TEMP", "TMP", "TMPDIR")}
        try:
            for name in previous:
                os.environ[name] = str(directory)
            yield
        finally:
            for name, value in previous.items():
                if value is None:
                    os.environ.pop(name, None)
                else:
                    os.environ[name] = value


class ProcessCapture(NamedTuple):
    """Internal capture: local paths and typed reader result, no firmware bytes."""

    record: dict[str, Any]
    report: ReadReport | None
    inputs: list[dict[str, Any]]
    output: dict[str, Any] | None
    output_path: Path | None
    stdout: bytes
    stderr: bytes
    failures: list[validation.Failure]
    settings_present: bool
    written_report_identity: dict[str, Any] | None

    def evidence(self) -> validation.SideProcessEvidence:
        return validation.SideProcessEvidence(
            self.record, None if self.report is None else self.report.projection,
            None if self.report is None else self.report.context,
            [] if self.report is None else self.report.issues,
            self.inputs, self.output, self.failures, self.settings_present,
        )


class _Invocation(NamedTuple):
    exit_code: int | None
    timed_out: bool
    stdout: bytes
    stderr: bytes


def _invoke(host: ProcessHost, argv: Sequence[str], work: Path, temporary: Path) -> _Invocation:
    try:
        with _temporary_environment(temporary):
            result = host.run(list(argv), work)
        return _Invocation(result.returncode, False, _stream_bytes(result.stdout), _stream_bytes(result.stderr))
    except subprocess.TimeoutExpired as error:
        return _Invocation(None, True, _stream_bytes(error.output), _stream_bytes(error.stderr))
    except (OSError, subprocess.SubprocessError):
        # No report, issue or stderr is invented from a host exception.
        return _Invocation(-1, False, b"", b"")


class ProcessRunner:
    """One runner for SDK/build and either mode's CLI processes.

    Construct once around the whole run. Call finish (or use the context
    manager) after all processes, including executor builds. The caller owns
    temporary_root cleanup; captured output paths live until then.
    """

    def __init__(
        self, host: ProcessHost, temporary_root: Path, settings_folder: Path, *,
        admission: ContractAdmission,
        custody: Callable[[Sequence[Path]], ContextManager[None]] = hold_read_only_file_custody,
    ):
        self.host = host
        self.temporary_root = temporary_root.resolve()
        self.settings_folder = settings_folder
        self.admission = admission
        self.policy = admission.contract["environment"]
        self.custody = custody
        self.captures: list[ProcessCapture] = []
        self.finished = False
        self.before = self._settings()
        _refuse(self._environment_failures())
        self.temporary_root.mkdir(parents=True, exist_ok=True)

    @property
    def formal(self) -> bool:
        return self.admission.formal

    def _settings(self) -> dict[str, str | None]:
        try:
            return {name: (_sha256((self.settings_folder / name).read_bytes())
                           if (self.settings_folder / name).exists() else None)
                    for name in self.policy["perUserSettingsFiles"]}
        except OSError as error:
            raise ExecutionError("PREDECESSOR_ENVIRONMENT_INVALID", "cannot read per-user settings") from error

    def _environment_failures(self, after: Mapping[str, str | None] | None = None) -> list[validation.Failure]:
        return validation.execution_environment_failures(
            temporary_root_length=len(str(self.temporary_root)), maximum_length=self.policy["temporaryRootMaxLength"],
            formal=self.formal, before=self.before, after=after,
        )

    def finish(self) -> tuple[dict[str, Any], list[validation.Failure]]:
        after = self._settings()
        self.finished = True
        environment = {
            "policy": self.policy["reportOfRecordPolicy"], "temporaryRootLength": len(str(self.temporary_root)),
            "perUserSettings": {name: {"sha256Before": self.before[name], "sha256After": after[name]} for name in self.before},
        }
        return environment, self._environment_failures(after)

    def __enter__(self) -> ProcessRunner:
        return self

    def __exit__(self, exc_type, exc, traceback) -> None:
        _, failures = self.finish()
        _refuse(failures)

    def run(
        self, *, stage: str, argv: Sequence[str], staging_root: Path,
        inputs: Sequence[Mapping[str, Any]], report_path: Path | None = None,
        output_path: Path | None = None, report_version: str = "1x",
        execution_hashes: Mapping[Path, str] | None = None,
    ) -> ProcessCapture:
        if self.finished:
            raise ExecutionError("PREDECESSOR_ENVIRONMENT_INVALID", "run already finished")
        work = staging_root.resolve(strict=True)
        if not work.is_relative_to(self.temporary_root):
            raise ExecutionError("PREDECESSOR_ENVIRONMENT_INVALID", "working directory outside staging root")
        if any(path is not None and (path.exists() or not path.resolve().is_relative_to(work))
               for path in (report_path, output_path)):
            raise ExecutionError("PREDECESSOR_REPORT_INVALID", "capture destination must be fresh and inside staging")
        settings_before = self._settings()
        _refuse(self._environment_failures(settings_before))
        temporary = Path(tempfile.mkdtemp(prefix="tmp-", dir=self.temporary_root))
        paths = [Path(row["path"]) for row in inputs]
        if any(not path.resolve().is_relative_to(work) for path in paths):
            raise ExecutionError("PREDECESSOR_INPUT_INVALID", "process input outside its staging root")
        closure_hashes = execution_hashes or {}
        before: list[dict[str, Any] | None] = []
        after: list[dict[str, Any] | None] = []
        failures: list[validation.Failure] = []
        stdout = stderr = b""
        exit_code: int | None = -1
        timed_out = False
        written: bytes | None = None
        output = None
        try:
            with self.custody([*paths, *closure_hashes]):
                before = [_file_identity(path) for path in paths]
                _refuse(validation.input_capture_failures(inputs, before, before, stage))
                self._check_closure(closure_hashes)
                try:
                    invocation = _invoke(self.host, argv, work, temporary)
                    exit_code, timed_out, stdout, stderr = invocation
                finally:
                    after = [_file_identity(path) for path in paths]
                self._check_closure(closure_hashes)
                try:
                    if report_path is not None and report_path.exists():
                        written = report_path.read_bytes()
                    if output_path is not None:
                        output = _file_identity(output_path)
                except OSError:
                    failures.append(validation.Failure("PREDECESSOR_REPORT_INVALID", stage, "report or output capture failed"))
        except ExecutionError as error:
            failures.append(validation.Failure(error.code, stage, str(error)))
        except (ParityError, OSError):
            failures.append(validation.Failure("PREDECESSOR_INPUT_INVALID", stage, "input custody or capture failed"))
        input_failures = validation.input_capture_failures(inputs, before, after, stage)
        failures.extend(input_failures)
        settings_after = self._settings()
        failures.extend(self._environment_failures(settings_after))
        read = None
        report_identity = None
        if written is not None:
            try:
                read = read_cli_report(load_json_reject_duplicates(written), report_version=report_version)
                report_identity = {**_payload_identity(written), "readerVersion": read.reader_version,
                                   "unknownMembers": read.unknown_members}
            except (ParityError, ReportReaderError, TypeError, ValueError):
                failures.append(validation.Failure("PREDECESSOR_REPORT_INVALID", stage, "written report format invalid"))
        record = {"stage": stage, "exitCode": exit_code, "timedOut": timed_out,
                  "stdoutSha256": _sha256(stdout), "stderrSha256": _sha256(stderr),
                  "inputsUnchanged": not input_failures, "report": report_identity}
        capture = ProcessCapture(record, read, [dict(row) for row in inputs], output, output_path,
                                 stdout, stderr, failures,
                                 any(value is not None for value in (*settings_before.values(), *settings_after.values())),
                                 None if written is None else _payload_identity(written))
        self.captures.append(capture)
        return capture

    @staticmethod
    def _check_closure(hashes: Mapping[Path, str]) -> None:
        try:
            observed = {str(path): (_sha256(path.read_bytes()) if path.exists() else None) for path in hashes}
            _refuse(validation.executor_closure_failures({str(path): value for path, value in hashes.items()}, observed))
        except OSError as error:
            raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "execution closure unavailable") from error


def stage_case_inputs(
    authority: MaterializedCanonicalAuthority, artifacts: Mapping[str, Mapping[str, Any]],
    bindings: Sequence[tuple[str, str] | Mapping[str, Any]], target_root: Path,
) -> list[dict[str, Any]]:
    """Reuse admission and carry bindings into read-only captures under custody.

    The validator always compares artifactId. When a binding gives no
    addressSpaceId (including tuple bindings), only artifactId is compared.
    """

    try:
        captured_bindings = [binding if isinstance(binding, Mapping) else
                             {"artifactId": binding[0], "slotId": binding[1]} for binding in bindings]
        rows = admit_case_inputs(authority, artifacts,
                                [(binding["artifactId"], binding["slotId"]) for binding in captured_bindings],
                                target_root=target_root)
        for row, binding in zip(rows, captured_bindings):
            row["artifactId"] = binding["artifactId"]
            if "addressSpaceId" in binding:
                row["addressSpaceId"] = binding["addressSpaceId"]
            Path(row["path"]).chmod(stat.S_IREAD)
        return rows
    except (ParityError, OSError, KeyError, TypeError, IndexError) as error:
        raise ExecutionError("PREDECESSOR_INPUT_INVALID", "case input admission failed") from error


def _lock_snapshot(root: Path, expected: Mapping[str, bytes]) -> dict[str, bytes | None]:
    return {path: (root / path).read_bytes() if (root / path).exists() else None for path in expected}


def build_1x_executor(
    git: GitHost, runner: ProcessRunner, commit: str, contract: Mapping[str, Any], *, tag_object: str | None = None,
) -> Executor:
    """Build from one exact detached commit and measure the unpinned identity.

    The caller admits the formal/diagnostic contract first. compilerHost is
    returned by that admission with its actual pending status, never fabricated.
    """

    recipe = contract["executor"]
    _refuse(validation.executor_compiler_host_failures(recipe["compilerHost"]))
    try:
        if tag_object is not None:
            _refuse(validation.executor_tag_failures(
                tag_object, git.git_tag_object(tag_object), git.git_tag_commit(tag_object), commit))
        paths = git.list_files(commit)
        locks = {path: git.read_file(commit, path) for path in sorted(paths)
                 if len(PurePosixPath(path).parts) == 3 and PurePosixPath(path).match(recipe["lockFileSet"]["pattern"])}
        lock_inventory = [{"path": path, **_payload_identity(payload)} for path, payload in locks.items()]
        # Reserve a unique parent while leaving the actual worktree destination
        # absent, as the existing worktree owner requires.
        parent = Path(tempfile.mkdtemp(prefix="executor-", dir=runner.temporary_root))
        with git.detached_worktree(commit, parent, "source") as source:
            build_paths = [path.relative_to(source).as_posix() for path in source.rglob("*")
                           if path.name in recipe["forbiddenPreRestorePathSegments"]]
            _refuse(validation.executor_source_failures(
                commit=commit, observed_commit=git.git_head(source), dirty_paths=git.git_dirty_paths(source),
                build_paths=[*git.git_ignored_build_paths(source), *build_paths], tracked_paths=paths,
                forbidden_segments=recipe["forbiddenPreRestorePathSegments"],
            ))
            # Require the SDK source in this Git snapshot. dotnet --version
            # resolves it inside the clean worktree. Only lock files have the
            # contract's stricter blob-byte check (checkout EOLs may differ).
            git.read_file(commit, recipe["sdkSource"])
            _refuse(validation.executor_lock_failures(locks, _lock_snapshot(source, locks), commit))
            sdk_capture = runner.run(stage="build", argv=["dotnet", "--version"], staging_root=source, inputs=[])
            _refuse(sdk_capture.failures)
            _refuse(validation.executor_process_failures(sdk_capture.record))
            sdk = sdk_capture.stdout.decode("utf-8").strip()
            _refuse(validation.executor_sdk_failures(sdk))
            for action in ("restore", "build"):
                arguments = [value.replace("{sourceRoot}", str(source)) for value in recipe[action]["arguments"]]
                capture = runner.run(stage="build", argv=arguments, staging_root=source / recipe[action]["workingDirectory"], inputs=[])
                _refuse(capture.failures)
                _refuse(validation.executor_process_failures(capture.record))
                _refuse(validation.executor_lock_failures(locks, _lock_snapshot(source, locks), commit))
                _refuse(validation.executor_source_failures(
                    commit=commit, observed_commit=git.git_head(source), dirty_paths=git.git_dirty_paths(source),
                    build_paths=[], tracked_paths=[], forbidden_segments=recipe["forbiddenPreRestorePathSegments"],
                ))
            runtime_root = source / recipe["runtimeClosureRoot"]
            cli_relative = (source / recipe["cliAssembly"]).relative_to(runtime_root).as_posix()
            closure = runtime_closure_inventory(runtime_root, cli_relative=cli_relative)
            identity = {
                "authorityTrees": {path: git.git_tree_for_path(source, path) for path in recipe["authorityTrees"]},
                "cliSha256": _sha256(closure.files[cli_relative]), "commit": git.git_head(source),
                "lockFileSetSha256": canonical_json_sha256(lock_inventory), "resolvedSdkVersion": sdk,
                "runtimeClosureSha256": closure.identity_sha256, "tagObject": tag_object, "tree": git.git_tree(source),
            }
            return Executor({member: identity[member] for member in recipe["recordedIdentity"]}, closure, "1x")
    except ExecutionError:
        raise
    except (ParityError, OSError, subprocess.SubprocessError, KeyError, TypeError, ValueError) as error:
        raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "executor acquisition or identity failed") from error


def execute_cli_stage(
    runner: ProcessRunner, executor: Executor, request: Mapping[str, Any],
    authority: MaterializedCanonicalAuthority, artifacts: Mapping[str, Mapping[str, Any]],
    bindings: Sequence[tuple[str, str] | Mapping[str, Any]], *, stage: str, execution_role: str = "candidate",
    precursor: ProcessCapture | None = None,
) -> ProcessCapture:
    """Fresh runtime and admitted input copies for one Preview or Build."""

    work = Path(tempfile.mkdtemp(prefix="cli-", dir=runner.temporary_root))
    rows = stage_case_inputs(authority, artifacts, bindings, work / "inputs")
    if precursor is not None:
        if precursor.output_path is None or precursor.output is None:
            raise ExecutionError("PREDECESSOR_INPUT_INVALID", "precursor capture is missing")
        with runner.custody([precursor.output_path]):
            payload = precursor.output_path.read_bytes()
            _refuse(validation.scope_capture_failures("precursor", precursor.output, _payload_identity(payload)))
            base = work / "inputs" / "precursor.bin"
            base.write_bytes(payload)
            base.chmod(stat.S_IREAD)
        rows.insert(0, {"slotId": "replace-base", "artifactId": "replace-base", "role": "input", "path": str(base),
                        **precursor.output, "order": 0})
        rows = [{**row, "order": order} for order, row in enumerate(rows)]
    try:
        cli, hashes = materialize_execution_closure(executor.closure, work / "runtime")
    except (ParityError, OSError) as error:
        raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "cannot stage executor closure") from error
    report_path, output_path = work / "report.json", work / "output.bin"
    action = stage.removeprefix("precursor-")
    arguments = [str(cli), request["workflowId"], action, "--profile", request["profileId"]]
    arguments.extend(cli_arguments(request, execution_role, [(row, Path(row["path"])) for row in rows]))
    arguments.extend(("--output", str(output_path), "--report", str(report_path)))
    return runner.run(stage=stage, argv=arguments, staging_root=work, inputs=rows,
                      report_path=report_path, output_path=output_path, report_version=executor.report_version,
                      execution_hashes=hashes)


class SideResult(NamedTuple):
    side: dict[str, Any]
    failures: list[validation.Failure]


class ScenarioExecution(NamedTuple):
    result: SideResult
    captures: Sequence[ProcessCapture]


def execute_side_stages(
    runner: ProcessRunner, executor: Executor, request: Mapping[str, Any],
    authority: MaterializedCanonicalAuthority, artifacts: Mapping[str, Mapping[str, Any]],
    bindings: Sequence[Mapping[str, Any]], *, precursor_request: Mapping[str, Any] | None = None,
    precursor_bindings: Sequence[Mapping[str, Any]] = (), execution_role: str = "candidate",
) -> ScenarioExecution:
    """Run each required Preview/Build once, stopping at the shared verdict."""
    stages = []
    if precursor_request is not None:
        stages += [(stage, precursor_request, precursor_bindings) for stage in ("precursor-preview", "precursor-build")]
    stages += [(stage, request, bindings) for stage in ("preview", "build")]
    captures = []
    capacities = {}
    precursor = None
    for stage, stage_request, stage_bindings in stages:
        capture = execute_cli_stage(runner, executor, stage_request, authority, artifacts, stage_bindings,
                                    stage=stage, execution_role=execution_role,
                                    precursor=precursor if not stage.startswith("precursor-") else None)
        captures.append(capture)
        capacities[stage] = validation.execution_capacities(capture.evidence())
        verdict = validation.side_execution_verdict([item.evidence() for item in captures], capacities={},
                                                    capacities_by_stage=capacities, complete=False)
        if verdict.status != "ready":
            break
        if stage == "precursor-build":
            precursor = capture
    return ScenarioExecution(assemble_side_result(captures, capacities={}, capacities_by_stage=capacities), captures)


def assemble_side_result(
    captures: Sequence[ProcessCapture], *, capacities: Mapping[str, int],
    capacities_by_stage: Mapping[str, Mapping[str, int]] | None = None,
) -> SideResult:
    """Project the validator's verdict into exactly the report schema's side."""

    verdict = validation.side_execution_verdict([capture.evidence() for capture in captures], capacities=capacities,
                                               capacities_by_stage=capacities_by_stage)
    issues = [dict(issue) for capture in captures if capture.report is not None for issue in capture.report.issues]
    precursor = next((capture.output for capture in captures if capture.record["stage"] == "precursor-build"), None)
    output = next((capture.output for capture in captures if capture.record["stage"] == "build"), None)
    side = {"status": verdict.status, "stoppedAt": verdict.stopped_at, "issues": issues,
            "precursor": precursor, "output": output if verdict.status == "output" else None,
            "processes": [dict(capture.record) for capture in captures]}
    return SideResult(side, verdict.failures)


class ByteComparison(NamedTuple):
    address_space: str
    baseline: dict[str, Any]
    candidate: dict[str, Any]
    ranges: list[dict[str, int]]
    different_byte_count: int
    range_list_sha256: str

    @property
    def scope_evidence(self) -> Sequence[Mapping[str, int]] | None:
        """Translate equal bytes to the validator's None evidence in one place."""
        return self.ranges or None


def compare_output_bytes(baseline: bytes, candidate: bytes) -> ByteComparison:
    """Measure every offset, including the longer output's entire extra tail."""

    ranges: list[dict[str, int]] = []
    start: int | None = None
    for offset in range(max(len(baseline), len(candidate))):
        different = offset >= min(len(baseline), len(candidate)) or baseline[offset] != candidate[offset]
        if different and start is None:
            start = offset
        elif not different and start is not None:
            ranges.append({"start": start, "endExclusive": offset})
            start = None
    if start is not None:
        ranges.append({"start": start, "endExclusive": max(len(baseline), len(candidate))})
    projection = validation.range_projection(ranges)
    assert projection is not None
    return ByteComparison("output-file-offset", _payload_identity(baseline), _payload_identity(candidate), ranges,
                          projection["differentByteCount"], projection["rangeListSha256"])


def measured_scopes(
    baseline: ScenarioExecution, candidate: ScenarioExecution,
    custody: Callable = hold_read_only_file_custody,
) -> tuple[dict[str, Any], list[validation.Failure]]:
    evidence = {}
    failures = []
    for scope, stage in (("output", "build"), ("precursor", "precursor-build")):
        if any(side.result.side[scope] is None for side in (baseline, candidate)):
            continue
        captures = [next(item for item in side.captures if item.record["stage"] == stage) for side in (baseline, candidate)]
        paths = [capture.output_path for capture in captures]
        with custody(paths):
            payloads = [path.read_bytes() for path in paths]
            for side, payload in zip((baseline, candidate), payloads):
                failures.extend(validation.scope_capture_failures(scope, side.result.side[scope],
                                                                  {"size": len(payload), "sha256": _sha256(payload)}))
        measurement = compare_output_bytes(*payloads)
        evidence[scope] = measurement.scope_evidence
    return evidence, failures


def informational_differences(baseline: ScenarioExecution, candidate: ScenarioExecution) -> list[dict[str, Any]]:
    def facts(side: ScenarioExecution) -> dict[str, str | None]:
        reports = [capture.report for capture in side.captures if capture.report is not None]
        report = reports[-1] if reports else None
        return {"capability-fingerprint": None if report is None else report.projection["compilationFingerprint"],
                "map-id": None if report is None else report.context["mapId"],
                "issue-codes": json.dumps(sorted({issue["code"] for issue in side.result.side["issues"]}), separators=(",", ":")),
                "operation-projection-sha256": None if report is None else canonical_json_sha256(report.projection["compiledOperations"])}
    left, right = facts(baseline), facts(candidate)
    return [{"field": field, "baseline": left[field], "candidate": right[field]}
            for field in sorted(left) if left[field] != right[field]]


def main(argv: Sequence[str] | None = None) -> int:
    """Dispatch the two contract modes to their orchestration modules."""
    arguments = list(sys.argv[1:] if argv is None else argv)
    parser = argparse.ArgumentParser(description="Predecessor comparison")
    modes = parser.add_subparsers(dest="mode", required=True)
    modes.add_parser("rolling", add_help=False, help="compare with the previous stable release")
    modes.add_parser("v0916-1x", add_help=False, help="check the historical v0.9.16 plan at a milestone")
    selected, _ = parser.parse_known_args(arguments)
    if selected.mode == "v0916-1x":
        from scripts.predecessor_v0916 import v0916_main
        return v0916_main(arguments)
    from scripts.predecessor_rolling import rolling_main
    return rolling_main(arguments)


if __name__ == "__main__":
    raise SystemExit(main())

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
import re
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
from scripts.predecessor_pdb_probe import PdbProbeError, probe_compilation_options
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
        self.reader = self.snapshot_reader()
        self.host = LocalExecutionHost()

    def snapshot_reader(self, commit: str | None = None) -> PinnedGitReader:
        """Create a fresh capture; gitlinks have no file payload in this repository."""
        return PinnedGitReader(self.repository, allow_gitlinks=True)

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

    @contextmanager
    def detached_worktree(self, commit: str, temporary_root: Path, name: str) -> Iterator[Path]:
        """A detached worktree whose long Golden paths Git can create, inspect and remove."""
        with _git_long_paths(), detached_git_worktree(self.repository, commit, temporary_root, name) as root:
            yield root


class Executor(NamedTuple):
    identity: dict[str, Any]
    closure: CapturedExecutionClosure
    report_version: str
    external_tools: Mapping[str, bytes]


class BaselineExecutorBuilder(Protocol):
    """Build the v0.9.16 executor from its admitted candidate-snapshot contract."""

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
    """Admit local settings and binding before source or settings acquisition."""

    contract = load_json_reject_duplicates(contract_path.read_bytes())
    _refuse(validation.execution_mode_failures(contract, mode))
    amendment = None
    if mode == "v0916-1x":
        path = amendment_path or ROOT / contract["modes"][mode]["amendment"]
        amendment = load_json_reject_duplicates(path.read_bytes())
        _refuse(validation.formal_interface_failures(contract, formal=formal, amendment=amendment))
        baseline = amendment["baselineExecutor"]
        _refuse(validation.baseline_executor_binding_failures(baseline))
        if baseline.get("status") == "in-effect":
            load_bound_v0916_executor(baseline, lambda path: (ROOT / path).read_bytes(), contract["executor"]["compilerHost"])
    return admit_loaded_execution_contract(contract, mode=mode, formal=formal, amendment=amendment)


def admit_loaded_execution_contract(
    contract: Mapping[str, Any], *, mode: str, formal: bool, amendment: Mapping[str, Any] | None = None,
) -> ContractAdmission:
    """Use the same admission for a pinned snapshot and a loaded local contract."""
    _refuse(validation.execution_mode_failures(contract, mode))
    _refuse(validation.formal_interface_failures(contract, formal=formal, amendment=amendment))
    _refuse(validation.executor_compiler_host_failures(contract["executor"]["compilerHost"]))
    if amendment is not None:
        _refuse(validation.baseline_executor_binding_failures(amendment["baselineExecutor"]))
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


def require_fresh_output(path: Path) -> None:
    _refuse(validation.output_destination_failures(exists=path.exists(), is_symlink=path.is_symlink()))


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


@contextmanager
def _git_long_paths() -> Iterator[None]:
    """Enable `core.longpaths` for the Git commands of this process and its children, then restore.

    The Golden tree has paths of about 200 characters, so a worktree under a temporary root passes the
    Windows limit of 260. Only Git touches those files: the build reads `src`, and every identity comes
    from Git objects. The setting is passed through Git's own environment protocol; no configuration
    file of the repository or the user is changed.
    """

    names = ("GIT_CONFIG_COUNT", "GIT_CONFIG_KEY_", "GIT_CONFIG_VALUE_")
    with _ENVIRONMENT_LOCK:
        try:
            index = int(os.environ.get(names[0], "0"))
        except ValueError:
            index = 0
        index = max(index, 0)
        changed = {names[0]: str(index + 1), f"{names[1]}{index}": "core.longpaths", f"{names[2]}{index}": "true"}
        previous = {name: os.environ.get(name) for name in changed}
        try:
            os.environ.update(changed)
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
    staged_tools: tuple[str, ...]
    temporary_directory: str
    expected_identity: validation.ReportRequestIdentity | None = None

    def evidence(self) -> validation.SideProcessEvidence:
        return validation.SideProcessEvidence(
            self.record, None if self.report is None else self.report.projection,
            None if self.report is None else self.report.context,
            [] if self.report is None else self.report.issues,
            self.inputs, self.output, self.failures, self.settings_present,
            self.staged_tools, self.temporary_directory, self.expected_identity,
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
        tool_hashes: Mapping[Path, str] | None = None,
        expected_identity: validation.ReportRequestIdentity | None = None,
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
        # The staged external tools are under the same custody and hash checks as the runtime closure.
        closure_hashes = {**(execution_hashes or {}), **(tool_hashes or {})}
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
                                 None if written is None else _payload_identity(written),
                                 tuple(str(path) for path in (tool_hashes or {})), str(temporary), expected_identity)
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
    *, request: Mapping[str, Any] | None = None, execution_role: str | None = None,
) -> list[dict[str, Any]]:
    """Reuse admission and carry bindings into read-only captures under custody.

    Golden ids are used only by admission. Expected report ids come from the
    validator and are checked unconditionally along with ordered byte identity.
    """

    try:
        captured_bindings = [binding if isinstance(binding, Mapping) else
                             {"artifactId": binding[0], "slotId": binding[1]} for binding in bindings]
        rows = admit_case_inputs(authority, artifacts,
                                [(binding["artifactId"], binding["slotId"]) for binding in captured_bindings],
                                target_root=target_root)
        for row in rows:
            row.update(validation.report_input_binding(row["slotId"], request=request, execution_role=execution_role))
            Path(row["path"]).chmod(stat.S_IREAD)
        return rows
    except (ParityError, OSError, KeyError, TypeError, IndexError) as error:
        raise ExecutionError("PREDECESSOR_INPUT_INVALID", "case input admission failed") from error


def _lock_snapshot(root: Path, expected: Mapping[str, bytes]) -> dict[str, bytes | None]:
    return {path: (root / path).read_bytes() if (root / path).exists() else None for path in expected}


def _source_inventory(root: Path, build_segments: Sequence[str], package_folder: str) -> dict[str, Any]:
    """Include ignored source files too; only bin/obj products and the restore's package folder are excluded.

    The source's own `NuGet.config` puts the restored packages in a top-level folder of the worktree. They
    are restore products, identified by the lock bytes the caller pins, not source files.
    """
    return {path.relative_to(root).as_posix(): _file_identity(path) for path in root.rglob("*")
            if path.is_file() and not set(path.relative_to(root).parts) & set(build_segments)
            and path.relative_to(root).parts[0] != package_folder}


@contextmanager
def _compiler_environment(compiler_host: Mapping[str, Any]) -> Iterator[None]:
    """Only restore/build inherit the pin; every exceptional path restores it."""
    variables = compiler_host.get("environmentVariables", {})
    with _ENVIRONMENT_LOCK:
        previous = {name: os.environ.get(name) for name in variables}
        try:
            os.environ.update(variables)
            yield
        finally:
            for name, value in previous.items():
                if value is None:
                    os.environ.pop(name, None)
                else:
                    os.environ[name] = value


def _executor_process(runner: ProcessRunner, source: Path, arguments: Sequence[str]) -> ProcessCapture:
    capture = runner.run(stage="build", argv=list(arguments), staging_root=source, inputs=[])
    process_failures = validation.executor_process_failures(capture.record)
    if capture.failures or process_failures:
        print("Executor command failed: " + " ".join(arguments[:3]), file=sys.stderr)
        for name, payload in (("stdout", capture.stdout), ("stderr", capture.stderr)):
            print(f"{name} (last 30 lines):", file=sys.stderr)
            for line in payload.decode("utf-8", errors="replace").splitlines()[-30:]:
                print(line, file=sys.stderr)
    _refuse(capture.failures)
    _refuse(process_failures)
    return capture


def _compiler_preflight(runner: ProcessRunner, source: Path, compiler_host: Mapping[str, Any]) -> None:
    if compiler_host.get("status") != "in-effect":
        return
    required = compiler_host["requiredRuntime"]
    runtimes = _executor_process(runner, source, ["dotnet", "--list-runtimes"]).stdout.decode("utf-8")
    # --list-runtimes lists the selected installation's runtimes but does not
    # report architecture. Query the same dotnet host's Host section for that.
    info = _executor_process(runner, source, ["dotnet", "--info"]).stdout.decode("utf-8")
    host = re.search(r"(?m)^Host:\s*\r?\n((?:[ \t]+[^\n]*\n?)+)", info)
    architectures = [] if host is None else re.findall(r"(?m)^\s+Architecture:\s*(\S+)\s*$", host[1])
    installed = any(re.fullmatch(re.escape(required["framework"]) + r"\s+" + re.escape(required["version"]) + r"\s+\[[^\r\n]+\]", row.strip())
                    for row in runtimes.splitlines())
    if not installed or architectures != [required["architecture"]]:
        raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "required compiler-host runtime or architecture is not installed")


def _compiler_identity(closure: CapturedExecutionClosure, compiler_host: Mapping[str, Any]) -> dict[str, Any]:
    """The CLI deps graph declares project assemblies; filenames do not."""
    deps_path = str(PurePosixPath(closure.cli_relative).with_suffix(".deps.json"))
    deps = load_json_reject_duplicates(closure.files[deps_path])
    target = deps["targets"][deps["runtimeTarget"]["name"]]
    projects = [name for name, value in deps["libraries"].items() if value["type"] == "project"]
    assemblies = []
    for project in projects:
        paths = [path for path in target[project].get("runtime", {}) if path.endswith(".dll")]
        if not paths:
            raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "CLI project has no measured managed assembly")
        assemblies.extend(paths)
    if not assemblies or len(set(assemblies)) != len(assemblies):
        raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "empty or ambiguous first-party CLI project graph")
    options = [probe_compilation_options(closure.files[path]) for path in assemblies]
    runtime_versions = {value.runtime_version for value in options}
    compiler_versions = {value.compiler_version for value in options}
    if runtime_versions != {compiler_host["verification"]["runtimeVersion"]} or len(compiler_versions) != 1:
        raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "mixed or unpinned compiler host in embedded PDBs")
    return {"runtimeVersion": options[0].runtime_version, "compilerVersion": options[0].compiler_version,
            "verifiedAssemblyCount": len(assemblies)}


def _require_artifacts(root: Path, artifacts: Sequence[Mapping[str, Any]]) -> None:
    for row in artifacts:
        if _file_identity(root / row["path"]) != {key: row[key] for key in ("size", "sha256")}:
            raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "artifact pin differs: " + row["path"])


def load_bound_v0916_executor(
    baseline: Mapping[str, Any], read_raw: Callable[[str], bytes], compiler_host: Mapping[str, Any],
) -> Mapping[str, Any] | None:
    """Load only through the binding, using the caller's exact Git snapshot."""
    _refuse(validation.baseline_executor_binding_failures(baseline))
    if baseline.get("status") != "in-effect":
        return None
    try:
        raw = read_raw(baseline["contract"]["path"])
        _refuse(validation.baseline_executor_binding_failures(baseline, raw))
        record = load_json_reject_duplicates(raw)
        _refuse(validation.v0916_executor_contract_failures(record))
        if record["compilerHost"] != compiler_host:
            raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "executor compiler-host contracts differ")
        binding = record["v1Relation"]["contract"]
        previous_raw = read_raw(binding["path"])
        if _payload_identity(previous_raw) != {key: binding[key] for key in ("size", "sha256")}:
            raise ExecutionError("PREDECESSOR_SOURCE_MISMATCH", "v1 relation raw contract binding differs")
        previous = load_json_reject_duplicates(previous_raw)
        if any(record[member] != previous[member] for member in
               ("source", "toolchain", "lockFiles", "externalTools", "build", "cliAssembly", "runtimeClosure")):
            raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "v2 relation does not preserve v1 pins")
        return record
    except ExecutionError:
        raise
    except (ParityError, OSError, KeyError, TypeError, ValueError) as error:
        raise ExecutionError("PREDECESSOR_SOURCE_MISMATCH", "bound baseline executor contract cannot be loaded") from error


def build_1x_executor(
    git: GitHost, runner: ProcessRunner, commit: str, contract: Mapping[str, Any], *, tag_object: str | None = None,
) -> Executor:
    """Build one exact commit with unchanged locks and the admitted host pin."""
    return _build_executor(git, runner, commit, contract["executor"], tag_object=tag_object)


class V0916BaselineExecutorBuilder:
    """Reproduce the complete v2 recipe; v1's terminal consumer is unchanged."""

    def build(self, git: GitHost, runner: ProcessRunner, commit: str, record: Mapping[str, Any]) -> Executor:
        _refuse(validation.v0916_executor_contract_failures(record))
        source = record["source"]
        if commit != source["peeledCommit"]:
            raise ExecutionError("PREDECESSOR_BASELINE_INVALID", "v2 does not name the plan baseline commit")
        try:
            if (git.git_tag_object(source["tag"]), git.git_tag_commit(source["tagObject"])) != (source["tagObject"], commit):
                raise ExecutionError("PREDECESSOR_BASELINE_INVALID", "v2 does not name the annotated baseline tag")
        except subprocess.SubprocessError as error:
            raise ExecutionError("PREDECESSOR_BASELINE_INVALID", "baseline tag cannot be resolved") from error
        recipe = {**runner.admission.contract["executor"], "restore": record["restore"], "build": record["build"],
                  "compilerHost": record["compilerHost"], "cliAssembly": record["cliAssembly"]["path"],
                  "runtimeClosureRoot": record["runtimeClosure"]["root"]}
        return _build_executor(git, runner, commit, recipe, tag_object=source["tagObject"], baseline=record)


def _build_executor(
    git: GitHost, runner: ProcessRunner, commit: str, recipe: Mapping[str, Any], *,
    tag_object: str | None = None, baseline: Mapping[str, Any] | None = None,
) -> Executor:
    """One materializer/process/closure path, with two explicit lock policies."""
    _refuse(validation.executor_compiler_host_failures(recipe["compilerHost"]))
    try:
        if tag_object is not None:
            _refuse(validation.executor_tag_failures(
                tag_object, git.git_tag_object(tag_object), git.git_tag_commit(tag_object), commit))
        paths = git.list_files(commit)
        locks = {path: git.read_file(commit, path) for path in sorted(paths)
                 if len(PurePosixPath(path).parts) == 3 and PurePosixPath(path).match(recipe["lockFileSet"]["pattern"])}
        lock_inventory = [{"path": path, **_payload_identity(payload)} for path, payload in locks.items()]
        tools_tree = recipe["externalToolStaging"]["tree"]
        external_tools = {path: git.read_file(commit, path) for path in sorted(paths)
                          if PurePosixPath(path).parts[:1] == (tools_tree,)}
        if baseline is not None:
            for row in [baseline["toolchain"]["globalJson"], *baseline["lockFiles"], *baseline["externalTools"]]:
                if _payload_identity(git.read_file(commit, row["path"])) != {key: row[key] for key in ("size", "sha256")}:
                    raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "source Git blob pin differs: " + row["path"])
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
            if (source / recipe["restorePackageFolder"]).exists():
                raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "package folder exists before restore")
            # Require the SDK source in this Git snapshot. dotnet --version
            # resolves it inside the clean worktree. Only lock files have the
            # contract's stricter blob-byte check (checkout EOLs may differ).
            git.read_file(commit, recipe["sdkSource"])
            _refuse(validation.executor_lock_failures(locks, _lock_snapshot(source, locks), commit))
            original_files = None
            if baseline is not None:
                original_files = _source_inventory(source, recipe["forbiddenPreRestorePathSegments"],
                                                   recipe["restorePackageFolder"])
                _require_artifacts(source, [baseline["toolchain"]["globalJson"], *baseline["externalTools"]])
                if git.git_tree(source) != baseline["source"]["sourceTree"]:
                    raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "baseline source tree differs")
            sdk_capture = _executor_process(runner, source, ["dotnet", "--version"])
            sdk = sdk_capture.stdout.decode("utf-8").strip()
            _refuse(validation.executor_sdk_failures(sdk))
            if baseline is not None and sdk != baseline["toolchain"]["resolvedSdkVersion"]:
                raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "baseline SDK pin differs")
            _compiler_preflight(runner, source, recipe["compilerHost"])
            for action in ("restore", "build"):
                arguments = [value.replace("{sourceRoot}", str(source)) for value in recipe[action]["arguments"]]
                if action == "build":
                    # Decisions 275/277 approve the runtime property for every
                    # comparator-built program (both baselines and candidate) only.
                    extras = recipe["compilerHost"].get("extraBuildArguments", [])
                    arguments = [value for value in arguments if value not in extras] + list(extras)
                with _compiler_environment(recipe["compilerHost"]):
                    _executor_process(runner, source / recipe[action]["workingDirectory"], arguments)
                dirty = git.git_dirty_paths(source)
                if baseline is None:
                    _refuse(validation.executor_lock_failures(locks, _lock_snapshot(source, locks), commit))
                else:
                    rewrites = {row["path"] for row in baseline["lockFileRewrites"]}
                    _require_artifacts(source, baseline["lockFileRewrites"])
                    _refuse(validation.executor_lock_failures(
                        {path: payload for path, payload in locks.items() if path not in rewrites},
                        _lock_snapshot(source, {path: payload for path, payload in locks.items() if path not in rewrites}), commit))
                    # Raw snapshot catches changes even when Git's EOL filter
                    # hides them; porcelain catches new unauthorized files.
                    current_files = _source_inventory(source, recipe["forbiddenPreRestorePathSegments"],
                                                      recipe["restorePackageFolder"])
                    changed = {path for path in original_files.keys() | current_files.keys()
                               if current_files.get(path) != original_files.get(path)}
                    if changed != rewrites:
                        raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "source file delta differs from seven lock rewrites")
                    if sorted(row.strip() for row in dirty) != sorted("M " + path for path in rewrites):
                        raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "unauthorized source changes after " + action)
                    dirty = []
                    _require_artifacts(source, [baseline["toolchain"]["globalJson"], *baseline["externalTools"]])
                    if git.git_tree(source) != baseline["source"]["sourceTree"]:
                        raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "baseline tree changed after " + action)
                _refuse(validation.executor_source_failures(
                    commit=commit, observed_commit=git.git_head(source), dirty_paths=dirty,
                    build_paths=[], tracked_paths=[], forbidden_segments=recipe["forbiddenPreRestorePathSegments"],
                ))
            runtime_root = source / recipe["runtimeClosureRoot"]
            cli_relative = (source / recipe["cliAssembly"]).relative_to(runtime_root).as_posix()
            closure = runtime_closure_inventory(runtime_root, cli_relative=cli_relative)
            compiler_identity = _compiler_identity(closure, recipe["compilerHost"])
            if baseline is not None:
                _require_artifacts(source, [baseline["cliAssembly"]])
                _require_artifacts(runtime_root, baseline["managedAssemblies"])
                expected = baseline["runtimeClosure"]
                if (closure.file_count, closure.total_size, closure.identity_sha256) != (expected["fileCount"], expected["totalSize"], expected["sha256"]):
                    raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "baseline runtime closure pin differs")
                if compiler_identity["verifiedAssemblyCount"] != len(baseline["managedAssemblies"]):
                    raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "baseline first-party project graph differs")
            identity = {
                "authorityTrees": {path: git.git_tree_for_path(source, path) for path in recipe["authorityTrees"]},
                "cliSha256": _sha256(closure.files[cli_relative]), "commit": git.git_head(source),
                "lockFileSetSha256": canonical_json_sha256(lock_inventory), "resolvedSdkVersion": sdk,
                "runtimeClosureSha256": closure.identity_sha256, "tagObject": tag_object, "tree": git.git_tree(source),
                "compilerHost": compiler_identity,
            }
            return Executor({member: identity[member] for member in recipe["recordedIdentity"]}, closure,
                            "v0916" if baseline else "1x", external_tools)
    except ExecutionError:
        raise
    except (ParityError, PdbProbeError, OSError, subprocess.SubprocessError, KeyError, TypeError, ValueError) as error:
        raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "executor acquisition or identity failed") from error


def stage_external_tools(files: Mapping[str, bytes], staging_root: Path, tree: str) -> dict[Path, str]:
    """Stage the executor commit's external tools where the CLI's own search finds them first.

    A 1.x or v0.9.16 CLI looks for a directory named `external-tools` in its base directory and
    then in each parent. The copy stands beside the staged runtime closure, comes from the Git
    blobs of the executor's commit and joins the closure's custody and hash checks. The directory
    exists even without a tool, so the search never leaves the staging root.
    """
    hashes: dict[Path, str] = {}
    (staging_root / PurePosixPath(tree)).mkdir()
    for relative, payload in files.items():
        target = staging_root / PurePosixPath(relative)
        target.parent.mkdir(parents=True, exist_ok=True)
        with target.open("xb") as stream:
            stream.write(payload)
        target.chmod(stat.S_IREAD)
        hashes[target] = _sha256(payload)
    return hashes


def execute_cli_stage(
    runner: ProcessRunner, executor: Executor, request: Mapping[str, Any],
    authority: MaterializedCanonicalAuthority, artifacts: Mapping[str, Mapping[str, Any]],
    bindings: Sequence[tuple[str, str] | Mapping[str, Any]], *, stage: str, execution_role: str = "candidate",
    precursor: ProcessCapture | None = None,
) -> ProcessCapture:
    """Fresh runtime and admitted input copies for one Preview or Build."""

    work = Path(tempfile.mkdtemp(prefix="cli-", dir=runner.temporary_root))
    rows = stage_case_inputs(authority, artifacts, bindings, work / "inputs", request=request, execution_role=execution_role)
    if precursor is not None:
        if precursor.output_path is None or precursor.output is None:
            raise ExecutionError("PREDECESSOR_INPUT_INVALID", "precursor capture is missing")
        with runner.custody([precursor.output_path]):
            payload = precursor.output_path.read_bytes()
            _refuse(validation.scope_capture_failures("precursor", precursor.output, _payload_identity(payload)))
            base = work / "inputs" / "precursor.bin"
            base.write_bytes(payload)
            base.chmod(stat.S_IREAD)
        rows.insert(0, {"slotId": "replace-base", **validation.report_input_binding("replace-base"), "role": "input", "path": str(base),
                        **precursor.output, "order": 0})
    rows = validation.report_ordered_inputs(request["workflowId"], rows)
    try:
        cli, hashes = materialize_execution_closure(executor.closure, work / "runtime")
        tools = stage_external_tools(executor.external_tools, work,
                                     runner.admission.contract["executor"]["externalToolStaging"]["tree"])
    except (ParityError, OSError) as error:
        raise ExecutionError("PREDECESSOR_EXECUTOR_INVALID", "cannot stage executor closure") from error
    report_path, output_path = work / "report.json", work / "output.bin"
    action = stage.removeprefix("precursor-")
    arguments = [str(cli), request["workflowId"], action, "--profile", request["profileId"]]
    arguments.extend(cli_arguments(request, execution_role, [(row, Path(row["path"])) for row in rows]))
    arguments.extend(("--output", str(output_path), "--report", str(report_path)))
    return runner.run(stage=stage, argv=arguments, staging_root=work, inputs=rows,
                      report_path=report_path, output_path=output_path, report_version=executor.report_version,
                      execution_hashes=hashes, tool_hashes=tools)


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
                                                    capacities_by_stage=capacities, complete=False,
                                                    v0916_executor=executor.report_version == "v0916")
        if verdict.status != "ready":
            break
        if stage == "precursor-build":
            precursor = capture
    return ScenarioExecution(assemble_side_result(captures, capacities={}, capacities_by_stage=capacities,
                                                  v0916_executor=executor.report_version == "v0916"), captures)


def assemble_side_result(
    captures: Sequence[ProcessCapture], *, capacities: Mapping[str, int],
    capacities_by_stage: Mapping[str, Mapping[str, int]] | None = None,
    v0916_executor: bool = False,
) -> SideResult:
    """Project the validator's verdict into exactly the report schema's side."""

    verdict = validation.side_execution_verdict([capture.evidence() for capture in captures], capacities=capacities,
                                               capacities_by_stage=capacities_by_stage, v0916_executor=v0916_executor)
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
    """Dispatch execution modes and the format-only projection of a saved result."""
    arguments = list(sys.argv[1:] if argv is None else argv)
    parser = argparse.ArgumentParser(description="Predecessor comparison")
    modes = parser.add_subparsers(dest="mode", required=True)
    modes.add_parser("rolling", add_help=False, help="compare with the previous stable release")
    modes.add_parser("v0916-1x", add_help=False, help="check the historical v0.9.16 plan at a milestone")
    modes.add_parser("owner-list", add_help=False, help="write a coverage/difference list from a saved result")
    selected, _ = parser.parse_known_args(arguments)
    if selected.mode == "owner-list":
        from scripts.predecessor_report_reader import owner_list_main
        return owner_list_main(arguments[1:])
    if selected.mode == "v0916-1x":
        from scripts.predecessor_v0916 import v0916_main
        return v0916_main(arguments)
    from scripts.predecessor_rolling import rolling_main
    return rolling_main(arguments)


if __name__ == "__main__":
    raise SystemExit(main())

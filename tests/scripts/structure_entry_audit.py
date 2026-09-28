_INITIAL_MAIN = dict(globals())

# This must remain a script bootstrap: capture interpreter-created attributes
# before imports or definitions. A temporary sitecustomize installs the hook
# before inherited customization and every audited repository import.
import collections
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import types


class AuditViolation(AssertionError):
    """A launch or audit transcript differs from the reviewed contract."""


def exit_status(stop):
    value = stop.code
    if value is None:
        return 0
    if isinstance(value, int):
        return value
    print(value, file=sys.stderr)
    return 1


def drop_leading_harness_frames(traceback, codes):
    while traceback is not None and traceback.tb_frame.f_code in codes:
        traceback = traceback.tb_next
    return traceback


def normalized_git(arguments, checkout):
    values = list(arguments)
    if not values or values.pop(0) != "git":
        raise AuditViolation(f"non-git child: {arguments!r}")
    if values[:2] == ["-C", str(checkout)]:
        del values[:2]
    if values[:1] == ["--no-replace-objects"]:
        del values[:1]
    return tuple(values)


def git_arguments(arguments, checkout, expected):
    if isinstance(arguments, str):
        # On Windows CPython emits the already-quoted CreateProcess command.
        # Compare exact generated command lines, never shell-parse them.
        for call in expected:
            for options in ([], ["-C", str(checkout)], ["--no-replace-objects"]):
                candidate = ["git", *options, *call]
                if subprocess.list2cmdline(candidate) == arguments:
                    return tuple(call)
        raise AuditViolation(f"unapproved Git call or non-git child: {arguments}")
    return normalized_git(arguments, checkout)


def calibration_argv():
    # The harmless child must not run inherited site/user customization.
    return [sys.executable, "-I", "-S", "-c", "pass"]


class LaunchGuard:
    def __init__(self, checkout, expected, *, windows=None):
        self.checkout = Path(checkout)
        self.windows = os.name == "nt" if windows is None else windows
        self._broken_native_command = None if self.windows else False
        self.expected = collections.Counter(map(tuple, expected))
        self.seen = collections.Counter()
        self.pending_windows = None
        self.pending_native = None

    @property
    def broken_native_command(self):
        return self._broken_native_command

    def observe(self, item):
        event, arguments = item["event"], item["args"]
        if event == "subprocess.Popen":
            if self.pending_windows is not None or self.pending_native is not None:
                raise AuditViolation("unpaired Windows launch before next Popen")
            if len(arguments) != 3 or self.windows != isinstance(arguments[1], str):
                raise AuditViolation(f"unrecognized Popen arguments: {arguments!r}")
            executable, command, cwd = arguments
            if self.broken_native_command is None:
                if list(arguments) != [None, subprocess.list2cmdline(calibration_argv()), None]:
                    raise AuditViolation(f"unrecognized calibration Popen: {arguments!r}")
                self.pending_windows = list(arguments)
                return
            if executable not in (None, "git", "git.exe"):
                raise AuditViolation(f"non-git child: {command!r}")
            if cwd is not None and os.path.normcase(os.path.abspath(cwd)) != os.path.normcase(str(self.checkout)):
                raise AuditViolation(f"child outside checkout: {cwd}")
            call = git_arguments(command, self.checkout, self.expected)
            self.seen[call] += 1
            if self.seen[call] > self.expected[call]:
                raise AuditViolation(f"unexpected Git call/count: {' '.join(call)}")
            if self.windows:
                self.pending_windows = list(arguments)
        elif event == "_winapi.CreateProcess/arguments":
            if not self.windows or self.pending_windows is None or arguments != self.pending_windows:
                raise AuditViolation(f"unpaired CreateProcess arguments: {arguments!r}")
            self.pending_native = self.pending_windows
            self.pending_windows = None
        elif event == "_winapi.CreateProcess":
            pending = self.pending_native
            if (not self.windows or pending is None or len(arguments) != 3
                    or arguments[0] != pending[0] or arguments[2] != pending[2]):
                raise AuditViolation(f"unpaired _winapi.CreateProcess: {arguments!r}")
            command = arguments[1]
            # Some CPython builds pass PyObject* into a wchar_t* audit slot.
            # Calibrate once from the paired harmless launch, never a version
            # number. A fragment never supplies command authority.
            refcount_fragment = (isinstance(command, str) and len(command) == 1
                                 and 0 < ord(command) < 32)
            if self.broken_native_command is None:
                if command != pending[1] and not refcount_fragment:
                    raise AuditViolation(
                        f"calibration native command has unknown shape: {type(command).__name__} {command!r}")
                self._broken_native_command = command != pending[1]
            if command != pending[1] and not (self.broken_native_command and refcount_fragment):
                raise AuditViolation(f"unrecognized _winapi.CreateProcess command: {arguments!r}")
            self.pending_native = None
        else:
            raise AuditViolation(f"forbidden launch: {event}: {arguments!r}")

    def finish(self):
        if self.pending_windows is not None or self.pending_native is not None:
            raise AuditViolation("unpaired Windows launch at end of transcript")
        if self.broken_native_command is None:
            raise AuditViolation("missing Windows audit calibration")
        if self.seen != self.expected:
            raise AuditViolation(f"Git multiset mismatch: missing={self.expected - self.seen}; extra={self.seen - self.expected}")


def is_launch(event):
    return event in {
        "subprocess.Popen", "_winapi.CreateProcess", "_winapi.CreateProcess/arguments", "os.system", "os.exec",
        "os.spawn", "os.posix_spawn", "os.startfile", "os.startfile/2",
        "os.fork", "os.forkpty", "ctypes.dlopen",
    }


def install_windows_boundary():
    if os.name != "nt":
        return
    import _winapi
    create_process = _winapi.CreateProcess

    def audited_create_process(*args):
        # Positional-only native API: application, command, ..., cwd, startup.
        # Forward the very same tuple; never copy or log its environment.
        if len(args) != 9:
            raise AuditViolation("unrecognized CreateProcess call shape")
        sys.audit("_winapi.CreateProcess/arguments", args[0], args[1], args[7])
        return create_process(*args)

    _winapi.CreateProcess = audited_create_process


def install_audit(expected):
    guard = LaunchGuard(Path.cwd(), expected)
    state = {"events": 0}

    def hook(event, arguments):
        if not is_launch(event):
            return
        # Never record subprocess environments; they can contain credentials.
        payload = list(arguments[:3]) if event in {"subprocess.Popen", "_winapi.CreateProcess", "_winapi.CreateProcess/arguments"} else list(arguments[:2])
        item = {"event": event, "args": payload}
        sys.stderr.flush()
        os.write(2, ("NFC-AUDIT\t" + json.dumps(item, default=os.fsdecode) + "\n").encode())
        state["events"] += 1
        guard.observe(item)

    sys.addaudithook(hook)
    install_windows_boundary()
    if guard.windows:
        subprocess.run(calibration_argv(), stdin=subprocess.DEVNULL,
                       capture_output=True, check=True, timeout=10)
        if guard.broken_native_command is None:
            raise AuditViolation("missing Windows audit calibration events")
    return state


def bootstrap():
    startup = sys.modules.pop("_nfc_structure_audit", None)
    if startup is None or not hasattr(startup, "audit_state"):
        raise AuditViolation("startup audit hook was not installed")
    if sys.modules.get("sitecustomize") is startup.site_shim:
        del sys.modules["sitecustomize"]
    arguments = startup.launch_arguments
    if len(arguments) != 5 or arguments[1] != "--expected" or arguments[3] != "--":
        raise SystemExit("usage: structure_entry_audit.py --expected FILE -- TARGET")
    target = arguments[4]
    target_abs = os.path.abspath(target)
    if Path(target_abs).parent != Path.cwd() / "scripts":
        raise AuditViolation("target must be a script in the checkout")
    state = startup.audit_state
    sys.argv = [target]
    sys.path[0] = os.path.dirname(target_abs)
    module = types.ModuleType("__main__")
    module.__dict__.clear()
    module.__dict__.update(_INITIAL_MAIN)
    module.__file__ = target_abs
    module.__loader__ = type(_INITIAL_MAIN["__loader__"])("__main__", target_abs)
    sys.modules["__main__"] = module
    code = compile(Path(target_abs).read_bytes(), target_abs, "exec", dont_inherit=True)
    harness_codes = frozenset({bootstrap.__code__})
    try:
        exec(code, module.__dict__)
        status = 0
    except SystemExit as stop:
        status = exit_status(stop)
    except BaseException as error:
        tail = drop_leading_harness_frames(error.__traceback__, harness_codes)
        error = error.with_traceback(tail)
        sys.excepthook(type(error), error, tail)
        status = 1
    finally:
        sys.stdout.flush()
        sys.stderr.flush()
        os.write(2, f"NFC-AUDIT-END\t{state['events']}\t{status}\n".encode())
    raise SystemExit(status)


def expected_calls(checkout):
    checkout = Path(checkout).resolve()
    record = json.loads((checkout / "docs/governance/change-records/RELEASE-111-PARITY-AUTHORITY-TRANSFER-09.json").read_text(encoding="utf-8"))
    binding = record["reviewedHead"]
    plan = "docs/contracts/v0916-parity-certification-v1.json"
    source_path = "docs/contracts/v100-candidate-source-executor-v1.json"
    source = json.loads(subprocess.run(
        ["git", "--no-replace-objects", "show", f"{binding}:{source_path}"],
        cwd=checkout, capture_output=True, check=True,
    ).stdout)["source"]
    implementation = source["implementationHead"]
    assert all(re.fullmatch("[0-9a-f]{40}", item) for item in (binding, implementation))
    result = [
        ["ls-files", "-z"],
        ["rev-parse", "--show-toplevel", "--is-inside-work-tree", "--show-object-format"],
        ["ls-tree", "-r", "-t", "-z", "--full-tree", "HEAD"],
        ["ls-files", "-z", "-s", "-v"],
        ["log", "-1", "--format=%H", binding, "--", plan],
        ["rev-list", "--parents", "-n", "1", binding],
        ["rev-parse", "--verify", f"{binding}^{{commit}}"],
        ["show", f"{binding}:{plan}"], ["show", f"{binding}:{source_path}"],
        ["show", f"{implementation}:{plan}"],
        ["diff", "--name-only", "--no-renames", implementation, binding],
    ]
    for commit in (implementation, binding):
        result.extend(["rev-parse", f"{commit}:{path}"] for path in source["authorityTrees"])
        result.append(["show", f"{commit}:docs/contracts/canonical-capability-policy-v1.json"])
    return result


def audit_transcript(stderr, returncode, checkout, expected, reference_stderr=None):
    prefix = re.compile(rb"NFC-AUDIT(?:-END)?\t")
    if reference_stderr is not None and prefix.search(reference_stderr):
        raise AuditViolation("reference stderr contains audit prefix")
    clean, events, sentinel = [], [], None
    for line in stderr.splitlines(keepends=True):
        if not prefix.search(line):
            clean.append(line)
            continue
        if not re.fullmatch(rb"NFC-AUDIT(?:-END)?\t[^\n]*\n", line):
            raise AuditViolation("mid-line or malformed audit prefix")
        if sentinel is not None:
            raise AuditViolation("audit event after sentinel")
        try:
            if line.startswith(b"NFC-AUDIT-END\t"):
                _, count, status = line.rstrip(b"\n").split(b"\t")
                sentinel = (int(count), int(status))
            else:
                events.append(json.loads(line.split(b"\t", 1)[1]))
        except (ValueError, TypeError) as error:
            raise AuditViolation("malformed audit transcript") from error
    if sentinel != (len(events), returncode):
        raise AuditViolation(f"missing/mismatched audit sentinel: {sentinel}, events={len(events)}, exit={returncode}")
    guard = LaunchGuard(Path(checkout).resolve(), expected)
    for item in events:
        guard.observe(item)
    guard.finish()
    stripped = b"".join(clean)
    if reference_stderr is not None and stripped != reference_stderr:
        raise AuditViolation(f"stderr differs: {stripped!r} != {reference_stderr!r}")
    return stripped, events


def entry_environment():
    return os.environ.copy()


def write_site_bootstrap(scratch, harness, contract, target, inherited_path):
    # site imports this shim using normal interpreter flags. Remove its search
    # path and restore the exact inherited environment before loading the real
    # sitecustomize; site itself still runs usercustomize exactly once afterward.
    (scratch / "sitecustomize.py").write_text(f'''import importlib.util
import os
import sys
sys.path.remove({str(scratch)!r})
inherited_path = {inherited_path!r}
if inherited_path is None:
    os.environ.pop("PYTHONPATH", None)
else:
    os.environ["PYTHONPATH"] = inherited_path
spec = importlib.util.spec_from_file_location("_nfc_structure_audit", {str(harness)!r})
audit = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = audit
spec.loader.exec_module(audit)
audit.audit_state = audit.install_audit(audit.json.loads(audit.Path({str(contract)!r}).read_text(encoding="utf-8")))
audit.launch_arguments = sys.argv
sys.argv = [{target!r}]
audit.site_shim = sys.modules.pop("sitecustomize")
try:
    import sitecustomize
except ModuleNotFoundError as error:
    if error.name != "sitecustomize":
        raise
    # Import machinery requires its module until the shim import completes.
    # The script bootstrap removes this placeholder when no original exists.
    sys.modules["sitecustomize"] = audit.site_shim
''', encoding="utf-8")


def entry_path(path):
    path = Path(path).resolve()
    if os.name == "nt":
        # Fixture parent only: keep clone reads, child cwd and cleanup in the
        # same namespace without changing the verifier's session environment.
        from scripts.verify import _windows_file_api_path
        path = Path(_windows_file_api_path(path))
    return path


def create_entry_checkout(source, destination):
    source, destination = Path(source).resolve(), entry_path(destination)
    destination.mkdir(parents=True, exist_ok=True)
    # Git for Windows rejects an extended-length clone destination argument;
    # the native cwd supports it and the relative destination preserves it.
    subprocess.run(["git", "clone", "--shared", "--no-checkout", "-c", "core.longpaths=true", str(source), "."], cwd=destination, check=True, capture_output=True)
    head = subprocess.run(["git", "rev-parse", "HEAD"], cwd=source, check=True, capture_output=True, text=True).stdout.strip()
    subprocess.run(["git", "checkout", "--detach", head], cwd=destination, check=True, capture_output=True)
    return destination


def run_audited(checkout, expected=None, target="scripts/validate_repository.py", *, harness=None, reference=None):
    checkout = Path(checkout).resolve()
    expected = expected_calls(checkout) if expected is None else expected
    with tempfile.TemporaryDirectory(prefix="structure-audit-") as scratch:
        contract = Path(scratch) / "expected-calls.json"
        contract.write_text(json.dumps(expected), encoding="utf-8")
        harness = Path(harness or __file__).resolve()
        environment = entry_environment()
        inherited_path = environment.get("PYTHONPATH")
        write_site_bootstrap(Path(scratch), harness, contract, target, inherited_path)
        environment["PYTHONPATH"] = scratch + (os.pathsep + inherited_path if inherited_path else "")
        result = subprocess.run(
            [sys.executable, str(harness), "--expected", str(contract), "--", target],
            cwd=checkout, env=environment, stdin=subprocess.DEVNULL, capture_output=True,
        )
    clean, events = audit_transcript(result.stderr, result.returncode, checkout, expected, None if reference is None else reference.stderr)
    if reference is not None:
        if result.returncode != reference.returncode or result.stdout != reference.stdout:
            raise AuditViolation("launch status/stdout differ from reference")
    result.audit_stderr, result.audit_events = clean, events
    return result


if __name__ == "__main__":
    bootstrap()

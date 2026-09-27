_INITIAL_MAIN = dict(globals())

# This must remain a script bootstrap: capture interpreter-created attributes
# before imports or definitions, and install the hook before site processing.
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


class LaunchGuard:
    def __init__(self, checkout, expected):
        self.checkout = Path(checkout)
        self.expected = collections.Counter(map(tuple, expected))
        self.seen = collections.Counter()
        self.pending_windows = None

    def observe(self, item):
        event, arguments = item["event"], item["args"]
        if event == "subprocess.Popen":
            executable, command, cwd = arguments
            self.pending_windows = None
            if executable not in (None, "git", "git.exe"):
                raise AuditViolation(f"non-git child: {command!r}")
            if cwd is not None and os.path.normcase(os.path.abspath(cwd)) != os.path.normcase(str(self.checkout)):
                raise AuditViolation(f"child outside checkout: {cwd}")
            call = git_arguments(command, self.checkout, self.expected)
            self.seen[call] += 1
            if self.seen[call] > self.expected[call]:
                raise AuditViolation(f"unexpected Git call/count: {' '.join(call)}")
            self.pending_windows = command if isinstance(command, str) else subprocess.list2cmdline(command)
        elif event == "_winapi.CreateProcess":
            if os.name != "nt" or not self.pending_windows or arguments[1] != self.pending_windows:
                raise AuditViolation(f"unpaired _winapi.CreateProcess: {arguments!r}")
            self.pending_windows = None
        else:
            raise AuditViolation(f"forbidden launch: {event}: {arguments!r}")

    def finish(self):
        if self.seen != self.expected:
            raise AuditViolation(f"Git multiset mismatch: missing={self.expected - self.seen}; extra={self.seen - self.expected}")


def is_launch(event):
    return event in {
        "subprocess.Popen", "_winapi.CreateProcess", "os.system", "os.exec",
        "os.spawn", "os.posix_spawn", "os.startfile", "os.startfile/2",
        "os.fork", "os.forkpty", "ctypes.dlopen",
    }


def bootstrap():
    if len(sys.argv) != 5 or sys.argv[1] != "--expected" or sys.argv[3] != "--":
        raise SystemExit("usage: structure_entry_audit.py --expected FILE -- TARGET")
    expected = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))
    target = sys.argv[4]
    target_abs = os.path.abspath(target)
    checkout = Path.cwd()
    if Path(target_abs).parent != checkout / "scripts":
        raise AuditViolation("target must be a script in the checkout")
    guard = LaunchGuard(checkout, expected)
    events = 0

    def hook(event, arguments):
        nonlocal events
        if not is_launch(event):
            return
        # Never record subprocess environments; they can contain credentials.
        payload = list(arguments[:3]) if event in {"subprocess.Popen", "_winapi.CreateProcess"} else list(arguments[:2])
        item = {"event": event, "args": payload}
        sys.stderr.flush()
        os.write(2, ("NFC-AUDIT\t" + json.dumps(item, default=os.fsdecode) + "\n").encode())
        events += 1
        guard.observe(item)

    sys.addaudithook(hook)
    import site
    site.main()
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
        os.write(2, f"NFC-AUDIT-END\t{events}\t{status}\n".encode())
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
    return {key: value for key, value in os.environ.items() if key != "PYTHONPATH"}


def create_entry_checkout(source, destination):
    source, destination = Path(source).resolve(), Path(destination).resolve()
    subprocess.run(["git", "clone", "--shared", "--no-checkout", "-c", "core.longpaths=true", str(source), str(destination)], check=True, capture_output=True)
    head = subprocess.run(["git", "rev-parse", "HEAD"], cwd=source, check=True, capture_output=True, text=True).stdout.strip()
    subprocess.run(["git", "checkout", "--detach", head], cwd=destination, check=True, capture_output=True)
    return destination


def run_audited(checkout, expected=None, target="scripts/validate_repository.py", *, harness=None, reference=None):
    checkout = Path(checkout).resolve()
    expected = expected_calls(checkout) if expected is None else expected
    with tempfile.TemporaryDirectory(prefix="structure-audit-") as scratch:
        contract = Path(scratch) / "expected-calls.json"
        contract.write_text(json.dumps(expected), encoding="utf-8")
        result = subprocess.run(
            [sys.executable, "-S", str(harness or Path(__file__).resolve()), "--expected", str(contract), "--", target],
            cwd=checkout, env=entry_environment(), stdin=subprocess.DEVNULL, capture_output=True,
        )
    clean, events = audit_transcript(result.stderr, result.returncode, checkout, expected, None if reference is None else reference.stderr)
    if reference is not None:
        if result.returncode != reference.returncode or result.stdout != reference.stdout:
            raise AuditViolation("launch status/stdout differ from reference")
    result.audit_stderr, result.audit_events = clean, events
    return result


if __name__ == "__main__":
    bootstrap()

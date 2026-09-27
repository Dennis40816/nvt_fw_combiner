"""Observe the real structure entry, its launch contract, and caught violations."""

import contextlib
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
from unittest.mock import patch

import pytest

from structure_entry_audit import (
    AuditViolation, LaunchGuard, audit_transcript, create_entry_checkout,
    drop_leading_harness_frames, entry_environment, exit_status, expected_calls,
    run_audited,
)
import structure_entry_audit as audit_module


ROOT = Path(__file__).resolve().parents[2]
HARNESS = Path(__file__).with_name("structure_entry_audit.py")


@pytest.fixture(scope="module")
def entry_checkout():
    # A direct TEMP child keeps the canonical long filenames accessible to
    # Windows interpreters whose manifest does not opt in to long paths.
    with tempfile.TemporaryDirectory(prefix="ea-") as scratch:
        yield create_entry_checkout(ROOT, Path(scratch))


def reference_run(checkout, target):
    return subprocess.run([sys.executable, target], cwd=checkout,
                          env=entry_environment(), stdin=subprocess.DEVNULL, capture_output=True)


PROBE = '''initial_names = sorted(globals())
import ab_merge_fixture_validation
import atexit
import json
import os
import re
import sys
print(json.dumps({
    "argv": sys.argv, "cwd": os.getcwd(), "path": sys.path,
    "executable": sys.executable, "prefix": sys.prefix,
    "flags": re.sub(r"no_site=\\d+,? ?", "", repr(sys.flags)),
    "names": initial_names, "name": __name__, "file": __file__,
    "package": __package__, "spec": __spec__,
    "cached": globals().get("__cached__"),
    "loader": [type(__loader__).__module__, type(__loader__).__qualname__, __loader__.path],
}, sort_keys=True))
atexit.register(lambda: print(json.dumps(sys.argv)))
def deep():
    raise ValueError("deep failure")
def outer():
    deep()
'''


@pytest.mark.parametrize(("ending", "status"), [
    ("raise SystemExit(7)", 7), ("raise SystemExit('message')", 1),
    ("outer()", 1),
    ("try:\n    raise KeyError('cause')\nexcept KeyError as cause:\n    raise ValueError('effect') from cause", 1),
    ("", 0),
])
def test_script_launch_matches_interpreter_namespace_traceback_and_shutdown(entry_checkout, ending, status):
    probe = entry_checkout / "scripts/_launch_probe.py"
    probe.write_text(PROBE + ending + "\n", encoding="utf-8")
    reference = reference_run(entry_checkout, "scripts/_launch_probe.py")
    audited = run_audited(entry_checkout, [], "scripts/_launch_probe.py", reference=reference)
    assert audited.returncode == status
    assert audited.stderr.endswith(f"NFC-AUDIT-END\t0\t{status}\n".encode())


def test_real_structure_entry_has_only_frozen_binding_calls(entry_checkout):
    reference = reference_run(entry_checkout, "scripts/validate_repository.py")
    assert reference.returncode == 0, reference.stdout.decode(errors="replace") + reference.stderr.decode(errors="replace")
    audited = run_audited(entry_checkout, reference=reference)
    assert audited.returncode == 0
    assert len(audited.audit_events) >= len(expected_calls(entry_checkout))


@pytest.mark.parametrize("control", ["unbounded", "system", "python", "import", "foreign", "repeated"])
def test_caught_launch_regressions_remain_visible_to_parent(entry_checkout, control):
    path = entry_checkout / "scripts/validate_repository.py"
    original = path.read_bytes()
    calls = expected_calls(entry_checkout)
    anchored = next(row for row in calls if row[0] == "rev-list")
    parent = subprocess.run(["git", "rev-parse", anchored[-1] + "^"], cwd=entry_checkout,
                            check=True, capture_output=True, text=True).stdout.strip()
    expressions = {
        "unbounded": "subprocess.run(['git', 'rev-list', 'HEAD'])",
        "system": "__import__('os').system('git rev-list HEAD')",
        "python": "subprocess.run([__import__('sys').executable, '-c', \"import subprocess; subprocess.run(['git', 'log', '--oneline'])\"])",
        "import": "subprocess.run(['git', 'rev-list', 'HEAD'])",
        "foreign": f"subprocess.run({['git', *anchored[:-1], parent]!r})",
        "repeated": f"subprocess.run({['git', *anchored]!r}, capture_output=True)",
    }
    caught = f"\ntry:\n    {expressions[control]}\nexcept Exception:\n    pass\n"
    text = original.decode("utf-8")
    if control == "import":
        text = text.replace("import subprocess\n", "import subprocess\n" + caught, 1)
    else:
        marker = "    errors.extend(validate_code_size_policy(ROOT))"
        assert marker in text
        text = text.replace(marker, "\n".join("    " + line for line in caught.splitlines()) + "\n" + marker, 1)
    try:
        path.write_text(text, encoding="utf-8", newline="\n")
        match = {"system": "os.system", "python": "non-git child", "repeated": "count"}.get(control, "rev-list")
        with pytest.raises(AuditViolation, match=match):
            run_audited(entry_checkout, calls)
    finally:
        path.write_bytes(original)


@pytest.mark.parametrize("broken", ["runpy", "path", "argv"])
def test_launch_control_rejects_broken_bootstrap(entry_checkout, tmp_path, broken):
    source = HARNESS.read_text(encoding="utf-8")
    if broken == "runpy":
        source = source.replace('exec(code, module.__dict__)', '__import__("runpy").run_path(target_abs, run_name="__main__")')
    elif broken == "path":
        source = source.replace('sys.path[0] = os.path.dirname(target_abs)', 'pass  # broken sibling import path')
    else:
        source = source.replace('sys.argv = [target]', 'pass  # broken argv')
    broken_harness = tmp_path / "broken_bootstrap.py"
    broken_harness.write_text(source, encoding="utf-8")
    probe = entry_checkout / "scripts/_launch_probe.py"
    probe.write_text(PROBE + "raise SystemExit(7)\n", encoding="utf-8")
    reference = reference_run(entry_checkout, "scripts/_launch_probe.py")
    with pytest.raises(AuditViolation, match="differs|differ"):
        run_audited(entry_checkout, [], "scripts/_launch_probe.py", harness=broken_harness, reference=reference)
    if broken == "argv":
        validator = reference_run(entry_checkout, "scripts/validate_repository.py")
        # The real parser also detects leaked harness arguments with exit 2.
        result = run_audited(entry_checkout, [], harness=broken_harness)
        assert result.returncode == 2
        assert validator.returncode == 0


def traceback_from(function):
    try:
        function()
    except ValueError as error:
        return error.__traceback__
    raise AssertionError("fixture did not raise")


def test_traceback_trimming_uses_only_leading_code_identity():
    def leaf():
        raise ValueError("fixture")
    def middle():
        leaf()
    trace = traceback_from(middle)
    assert drop_leading_harness_frames(trace, {traceback_from.__code__}) is trace.tb_next
    assert drop_leading_harness_frames(trace, {exit_status.__code__}) is trace
    assert drop_leading_harness_frames(trace, {middle.__code__}) is trace


@pytest.mark.parametrize(("value", "status", "stderr"), [
    (None, 0, ""), (0, 0, ""), (7, 7, ""), ("message", 1, "message\n"),
    (("a", "b"), 1, "('a', 'b')\n"),
])
def test_system_exit_retains_interpreter_status_and_message(value, status, stderr):
    output = io.StringIO()
    with contextlib.redirect_stderr(output):
        assert exit_status(SystemExit(value)) == status
    assert output.getvalue() == stderr


def event_line(call):
    return b"NFC-AUDIT\t" + json.dumps({"event": "subprocess.Popen", "args": ["git", ["git", *call], None]}).encode() + b"\n"


def test_stderr_rule_preserves_clean_bytes_and_strips_only_whole_audit_lines(tmp_path):
    for output, expected in [(b"", []), (b"ordinary stderr NFC-AUDIT mention\n", []), (event_line(["ls-files", "-z"]), [["ls-files", "-z"]])]:
        reference = output if output.startswith(b"ordinary") else b""
        raw = output + f"NFC-AUDIT-END\t{len(expected)}\t0\n".encode()
        clean, _ = audit_transcript(raw, 0, tmp_path, expected, reference)
        assert clean == reference


@pytest.mark.parametrize("raw,reference,match", [
    (b"prefix NFC-AUDIT\t{}\nNFC-AUDIT-END\t0\t0\n", b"", "mid-line"),
    (b"NFC-AUDIT-END\t0\t0\n", b"NFC-AUDIT\tref\n", "reference"),
    (b"", b"", "sentinel"),
    (b"NFC-AUDIT-END\t1\t0\n", b"", "sentinel"),
    (b"NFC-AUDIT-END\t0\t1\n", b"", "sentinel"),
    (b"NFC-AUDIT-END\t0\t0\nNFC-AUDIT\t{}\n", b"", "after sentinel"),
])
def test_incomplete_spoofed_or_late_transcripts_fail(tmp_path, raw, reference, match):
    with pytest.raises(AuditViolation, match=match):
        audit_transcript(raw, 0, tmp_path, [], reference)


def test_each_reviewed_multiset_row_is_accepted_once(entry_checkout):
    calls = expected_calls(entry_checkout)
    guard = LaunchGuard(entry_checkout, calls)
    for call in reversed(calls):
        guard.observe({"event": "subprocess.Popen", "args": ["git", ["git", "-C", str(entry_checkout), *call], None]})
    guard.finish()
    with pytest.raises(AuditViolation, match="count"):
        guard.observe({"event": "subprocess.Popen", "args": ["git", ["git", *calls[0]], None]})


@pytest.mark.parametrize("call", [
    ["rev-list", "HEAD"], ["rev-list", "--ancestry-path", "a..b"],
    ["log", "--format=%H", "HEAD", "--", "x"], ["log", "-1", "HEAD"],
    ["diff-tree", "-r", "a"], ["rev-list", "--parents", "-n", "1", "main"],
    ["rev-list", "--parents", "-n", "1", "f" * 40],
])
def test_unbounded_symbolic_and_foreign_history_calls_fail(entry_checkout, call):
    guard = LaunchGuard(entry_checkout, expected_calls(entry_checkout))
    with pytest.raises(AuditViolation, match="Git call"):
        guard.observe({"event": "subprocess.Popen", "args": ["git", ["git", *call], None]})


def test_windows_process_event_requires_exact_preceding_command(tmp_path):
    guard = LaunchGuard(tmp_path, [["ls-files", "-z"]])
    with patch.object(audit_module.os, "name", "nt"):
        with pytest.raises(AuditViolation, match="unpaired"):
            guard.observe({"event": "_winapi.CreateProcess", "args": [None, "git ls-files -z", None]})
        guard.observe({"event": "subprocess.Popen", "args": [None, "git ls-files -z", None]})
        with pytest.raises(AuditViolation, match="unpaired"):
            guard.observe({"event": "_winapi.CreateProcess", "args": [None, "git rev-list HEAD", None]})
        guard.observe({"event": "_winapi.CreateProcess", "args": [None, "git ls-files -z", None]})
        with pytest.raises(AuditViolation, match="unpaired"):
            guard.observe({"event": "_winapi.CreateProcess", "args": [None, "git ls-files -z", None]})
    guard.finish()


@pytest.mark.parametrize("event", ["os.system", "os.exec", "os.spawn", "os.posix_spawn", "os.startfile", "os.fork", "os.forkpty", "ctypes.dlopen"])
def test_other_launch_mechanisms_are_rejected(tmp_path, event):
    guard = LaunchGuard(tmp_path, [])
    assert audit_module.is_launch(event)
    with pytest.raises(AuditViolation, match=event):
        guard.observe({"event": event, "args": []})


def test_frozen_pin_check_precedes_parity_check():
    sys.path.insert(0, str(ROOT / "scripts"))
    import validate_repository as validator
    seen = []
    class StopAfterParity(Exception):
        pass
    def parity(errors):
        seen.append("parity")
        raise StopAfterParity()
    # Other validators remain real; only the two observed calls are replaced.
    with patch.object(validator, "validate_frozen_evidence_pins", side_effect=lambda root, errors: seen.append("pins")), patch.object(validator, "validate_historical_parity_authority", side_effect=parity):
        with pytest.raises(StopAfterParity):
            validator.validate()
    assert seen == ["pins", "parity"]

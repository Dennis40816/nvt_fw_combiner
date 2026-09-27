"""Observe the real structure entry, its launch contract, and caught violations."""

import contextlib
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
from unittest.mock import patch

import pytest

from structure_entry_audit import (
    AuditViolation, LaunchGuard, audit_transcript, create_entry_checkout,
    drop_leading_harness_frames, entry_path, exit_status, expected_calls,
    run_audited,
)
import structure_entry_audit as audit_module


ROOT = Path(__file__).resolve().parents[2]
HARNESS = Path(__file__).with_name("structure_entry_audit.py")


@pytest.fixture(scope="module")
def entry_checkout():
    # The lane's session TEMP is deeper than a direct pytest TEMP. Cleanup
    # needs the same long-path namespace as the checkout and validator.
    with tempfile.TemporaryDirectory(prefix="ea-", dir=entry_path(tempfile.gettempdir())) as scratch:
        yield create_entry_checkout(ROOT, Path(scratch))


def reference_run(checkout, target):
    return subprocess.run([sys.executable, target], cwd=checkout,
                          env=os.environ.copy(), stdin=subprocess.DEVNULL, capture_output=True)


@pytest.fixture
def launch_environment(tmp_path, monkeypatch):
    configure_launch_environment(tmp_path, monkeypatch)


def configure_launch_environment(tmp_path, monkeypatch):
    (tmp_path / "nfc_launch_dependency.py").write_text("value = 'inherited import'\n", encoding="utf-8")
    (tmp_path / "sitecustomize.py").write_text(
        "import sys\nsys.nfc_launch_site = (sys.flags.no_site, sys.argv[:])\n", encoding="utf-8")
    (tmp_path / "usercustomize.py").write_text(
        "import sys\nsys.nfc_launch_user_site = (sys.flags.no_site, sys.argv[:])\n", encoding="utf-8")
    inherited = os.environ.get("PYTHONPATH")
    monkeypatch.setenv("PYTHONPATH", str(tmp_path) + (os.pathsep + inherited if inherited is not None else ""))


PROBE = '''initial_names = sorted(globals())
import ab_merge_fixture_validation
import atexit
import json
import os
import site
import sys
import nfc_launch_dependency
print(json.dumps({
    "argv": sys.argv, "cwd": os.getcwd(), "path": sys.path,
    "executable": sys.executable, "prefix": sys.prefix,
    "flags": repr(sys.flags), "pythonpath": os.environ.get("PYTHONPATH"),
    "lane_environment": {name: os.environ.get(name) for name in (
        "NFC_VERIFY_INTERNAL_LANE", "NFC_TEST_AREA_ROOT", "NFC_TEST_SESSION_ROOT",
        "TEMP", "TMP", "TMPDIR",
    )},
    "inherited_import": nfc_launch_dependency.value,
    "site": sys.nfc_launch_site,
    "user_site": getattr(sys, "nfc_launch_user_site", None),
    "enable_user_site": site.ENABLE_USER_SITE,
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
def test_script_launch_matches_interpreter_namespace_traceback_and_shutdown(entry_checkout, launch_environment, ending, status):
    probe = entry_checkout / "scripts/_launch_probe.py"
    probe.write_text(PROBE + ending + "\n", encoding="utf-8")
    reference = reference_run(entry_checkout, "scripts/_launch_probe.py")
    observed = json.loads(reference.stdout.splitlines()[0])
    assert observed["inherited_import"] == "inherited import"
    assert observed["site"] == [0, ["scripts/_launch_probe.py"]]
    if observed["enable_user_site"]:
        assert observed["user_site"] == observed["site"]
    assert observed["pythonpath"] == os.environ["PYTHONPATH"]
    assert observed["lane_environment"] == {name: os.environ.get(name) for name in (
        "NFC_VERIFY_INTERNAL_LANE", "NFC_TEST_AREA_ROOT", "NFC_TEST_SESSION_ROOT",
        "TEMP", "TMP", "TMPDIR",
    )}
    audited = run_audited(entry_checkout, [], "scripts/_launch_probe.py", reference=reference)
    assert audited.returncode == status
    assert audited.stderr.endswith(f"NFC-AUDIT-END\t{3 if os.name == 'nt' else 0}\t{status}\n".encode())


def test_real_structure_entry_has_only_frozen_binding_calls(entry_checkout):
    reference = reference_run(entry_checkout, "scripts/validate_repository.py")
    assert reference.returncode == 0, reference.stdout.decode(errors="replace") + reference.stderr.decode(errors="replace")
    audited = run_audited(entry_checkout, reference=reference)
    assert audited.returncode == 0
    assert len(audited.audit_events) >= len(expected_calls(entry_checkout))


def test_real_structure_entry_matches_reference_beyond_windows_path_limit(monkeypatch):
    from scripts.verify import _windows_file_api_path

    parent = Path(tempfile.gettempdir())
    cleanup_parent = _windows_file_api_path(parent) if os.name == "nt" else parent
    # Keep cleanup long-path capable even while the old checkout helper fails.
    with tempfile.TemporaryDirectory(prefix="deep-entry-", dir=cleanup_parent) as scratch:
        destination = parent / Path(scratch).name / ("nested-" + "x" * 80) / "r"
        destination.parent.mkdir()
        for name in ("TEMP", "TMP", "TMPDIR"):
            monkeypatch.setenv(name, str(destination.parent))
        monkeypatch.setattr(tempfile, "tempdir", str(destination.parent))
        checkout = create_entry_checkout(ROOT, destination)
        paths = subprocess.check_output(
            ["git", "ls-files", "-z", "testdata/golden/canonical"], cwd=checkout,
        ).decode().rstrip("\0").split("\0")
        assert max(len(str(destination / path)) for path in paths) > 260
        test_real_structure_entry_has_only_frozen_binding_calls(checkout)
        assert all((checkout / path).is_file() for path in paths)
    assert not Path(scratch).exists()


@pytest.mark.parametrize("control", ["unbounded", "system", "python", "import", "no_site", "foreign", "repeated"])
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
        "no_site": "subprocess.run(['git', 'rev-list', 'HEAD'], capture_output=True) if not __import__('sys').flags.no_site else None",
        "foreign": f"subprocess.run({['git', *anchored[:-1], parent]!r})",
        "repeated": f"subprocess.run({['git', *anchored]!r}, capture_output=True)",
    }
    caught = f"\ntry:\n    {expressions[control]}\nexcept Exception:\n    pass\n"
    text = original.decode("utf-8")
    if control in {"import", "no_site"}:
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


@pytest.mark.parametrize("customization", ["sitecustomize", "usercustomize"])
def test_inherited_customization_launch_is_audited_even_when_caught(entry_checkout, tmp_path, monkeypatch, customization):
    monkeypatch.delenv("PYTHONNOUSERSITE", raising=False)
    (tmp_path / f"{customization}.py").write_text(
        "import subprocess\ntry:\n    subprocess.run(['git', 'rev-list', 'HEAD'], capture_output=True)\n"
        "except Exception:\n    pass\n", encoding="utf-8")
    monkeypatch.setenv("PYTHONPATH", str(tmp_path))
    probe = entry_checkout / "scripts/_startup_probe.py"
    probe.write_text("print('startup complete')\n", encoding="utf-8")
    reference = reference_run(entry_checkout, "scripts/_startup_probe.py")
    assert reference.returncode == 0
    assert reference.stdout == (b"startup complete\r\n" if os.name == "nt" else b"startup complete\n")
    with pytest.raises(AuditViolation, match="rev-list"):
        run_audited(entry_checkout, [], "scripts/_startup_probe.py", reference=reference)


@pytest.mark.parametrize("broken", ["runpy", "path", "argv"])
def test_launch_control_rejects_broken_bootstrap(entry_checkout, tmp_path, broken):
    # Topology controls call this test directly with its original signature.
    with pytest.MonkeyPatch.context() as monkeypatch:
        configure_launch_environment(tmp_path, monkeypatch)
        check_broken_bootstrap(entry_checkout, tmp_path, broken)


def check_broken_bootstrap(entry_checkout, tmp_path, broken):
    source = HARNESS.read_text(encoding="utf-8")
    if broken == "runpy":
        source = source.replace('exec(code, module.__dict__)', '__import__("runpy").run_path(target_abs, run_name="__main__")')
    elif broken == "path":
        source = source.replace('sys.path[0] = os.path.dirname(target_abs)', 'pass  # broken sibling import path')
    else:
        source = source.replace('sys.argv = [target]', 'sys.argv = startup.launch_arguments  # broken argv')
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
    command = subprocess.list2cmdline(["git", *call]) if os.name == "nt" else ["git", *call]
    events = [{"event": "subprocess.Popen", "args": [None if os.name == "nt" else "git", command, None]}]
    if os.name == "nt":
        events.extend({"event": name, "args": [None, command, None]} for name in
                      ("_winapi.CreateProcess/arguments", "_winapi.CreateProcess"))
    return b"".join(b"NFC-AUDIT\t" + json.dumps(item).encode() + b"\n" for item in events)


def calibration_events(native_command=None):
    command = subprocess.list2cmdline([sys.executable, "-I", "-S", "-c", "pass"])
    return [
        {"event": "subprocess.Popen", "args": [None, command, None]},
        {"event": "_winapi.CreateProcess/arguments", "args": [None, command, None]},
        {"event": "_winapi.CreateProcess", "args": [None, command if native_command is None else native_command, None]},
    ]


def calibrate(guard, native_command=None):
    for event in calibration_events(native_command):
        guard.observe(event)


@pytest.mark.parametrize("native_command,broken", [(None, False), ("\x02", True), ("\x03", True)])
@pytest.mark.parametrize("version", [(3, 13, 5), (3, 13, 15), (3, 13, 16), (3, 14, 0)])
def test_probe_calibrates_from_observation_independent_of_patch(tmp_path, monkeypatch, native_command, broken, version):
    monkeypatch.setattr(audit_module.sys, "version_info", version)
    guard = LaunchGuard(tmp_path, [], windows=True)
    calibrate(guard, native_command)
    assert guard.broken_native_command is broken
    guard.finish()
    # Calibration is consumed once; it cannot become another launch allowance.
    with pytest.raises(AuditViolation, match="non-git|unapproved"):
        calibrate(guard, native_command)
    assert guard.broken_native_command is broken


@pytest.mark.parametrize("native_command", ["unknown", "", "\0", "\x02\x03", 2])
def test_probe_unknown_native_shape_fails_closed(tmp_path, native_command):
    guard = LaunchGuard(tmp_path, [], windows=True)
    with pytest.raises(AuditViolation, match="calibration.*shape.*" + ("int" if isinstance(native_command, int) else "str")):
        calibrate(guard, native_command)


@pytest.mark.parametrize("failure", ["absent", "missing-popen", "missing-boundary", "missing-native", "duplicate", "wrong-command", "wrong-cwd", "wrong-executable"])
def test_probe_requires_complete_exact_pairing(tmp_path, failure):
    guard = LaunchGuard(tmp_path, [], windows=True)
    events = calibration_events("\x03")
    if failure == "absent":
        events = []
    elif failure.startswith("missing-"):
        del events[{"missing-popen": 0, "missing-boundary": 1, "missing-native": 2}[failure]]
    elif failure == "duplicate":
        events.insert(1, events[0])
    else:
        events[1]["args"][{"wrong-command": 1, "wrong-cwd": 2, "wrong-executable": 0}[failure]] = "unexpected"
    with pytest.raises(AuditViolation):
        for event in events:
            guard.observe(event)
        guard.finish()



def test_stderr_rule_preserves_clean_bytes_and_strips_only_whole_audit_lines(tmp_path):
    for output, expected in [(b"", []), (b"ordinary stderr NFC-AUDIT mention\n", []), (event_line(["ls-files", "-z"]), [["ls-files", "-z"]])]:
        reference = output if output.startswith(b"ordinary") else b""
        if os.name == "nt":
            output = b"".join(b"NFC-AUDIT\t" + json.dumps(item).encode() + b"\n" for item in calibration_events()) + output
        raw = output + f"NFC-AUDIT-END\t{output.count(b"NFC-AUDIT\t")}\t0\n".encode()
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
    guard = LaunchGuard(entry_checkout, calls, windows=False)
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
    guard = LaunchGuard(entry_checkout, expected_calls(entry_checkout), windows=False)
    with pytest.raises(AuditViolation, match="Git call"):
        guard.observe({"event": "subprocess.Popen", "args": ["git", ["git", *call], None]})


@pytest.mark.parametrize("version", [(3, 13, 5), (3, 13, 15)])
@pytest.mark.parametrize("native_command", ["git ls-files -z", "\x02", "\x03"])
@pytest.mark.parametrize("explicit_cwd", [False, True])
def test_windows_launch_pairs_exact_boundary_and_native_event(tmp_path, monkeypatch, version, native_command, explicit_cwd):
    # Both evidenced runtimes use observed events, never a boolean override.
    monkeypatch.setattr(audit_module.sys, "version_info", version)
    checkout = entry_path(tmp_path)
    guard = LaunchGuard(checkout, [["ls-files", "-z"]], windows=True)
    calibrate(guard, None if native_command.startswith("git") else native_command)
    cwd = str(checkout) if explicit_cwd else None
    args = [None, "git ls-files -z", cwd]
    guard.observe({"event": "subprocess.Popen", "args": args})
    guard.observe({"event": "_winapi.CreateProcess/arguments", "args": args})
    guard.observe({"event": "_winapi.CreateProcess", "args": [None, native_command, cwd]})
    guard.finish()
    with pytest.raises(AuditViolation, match="unpaired"):
        guard.observe({"event": "_winapi.CreateProcess", "args": [None, native_command, cwd]})


@pytest.mark.parametrize("version", [(3, 13, 5), (3, 13, 15)])
@pytest.mark.parametrize("native_command", ["\x02", "\x03"])
@pytest.mark.parametrize("source", ["popen", "boundary", "native"])
def test_windows_runtime_compatibility_never_authorizes_unknown_command(tmp_path, monkeypatch, version, native_command, source):
    monkeypatch.setattr(audit_module.sys, "version_info", version)
    guard = LaunchGuard(tmp_path, [["ls-files", "-z"]], windows=True)
    calibrate(guard, native_command)
    approved = [None, "git ls-files -z", None]
    unknown = [None, "git rev-list HEAD", None]
    with pytest.raises(AuditViolation):
        guard.observe({"event": "subprocess.Popen", "args": unknown if source == "popen" else approved})
        guard.observe({"event": "_winapi.CreateProcess/arguments", "args": unknown if source == "boundary" else approved})
        guard.observe({"event": "_winapi.CreateProcess", "args": unknown if source == "native" else [None, native_command, None]})
        guard.finish()


@pytest.mark.parametrize("failure", ["direct", "direct-native", "missing-boundary", "wrong-command", "wrong-executable",
                                      "wrong-cwd", "missing-native", "next-popen", "duplicate-boundary",
                                      "unknown-native", "clean-probe", "native-executable", "native-cwd"])
@pytest.mark.parametrize("version", [(3, 13, 5), (3, 13, 15)])
@pytest.mark.parametrize("native_command", ["\x02", "\x03"])
def test_windows_launch_pairing_fails_closed(tmp_path, monkeypatch, failure, version, native_command):
    monkeypatch.setattr(audit_module.sys, "version_info", version)
    guard = LaunchGuard(tmp_path, [["ls-files", "-z"]], windows=True)
    calibrate(guard, None if failure == "clean-probe" else native_command)
    args = [None, "git ls-files -z", None]
    with pytest.raises(AuditViolation):
        if failure == "direct-native":
            guard.observe({"event": "_winapi.CreateProcess", "args": [None, native_command, None]})
        if failure == "direct":
            guard.observe({"event": "_winapi.CreateProcess/arguments", "args": args})
        guard.observe({"event": "subprocess.Popen", "args": args})
        if failure == "missing-boundary":
            guard.observe({"event": "_winapi.CreateProcess", "args": [None, native_command, None]})
        boundary = list(args)
        if failure == "wrong-command": boundary[1] = "git rev-list HEAD"
        if failure == "wrong-executable": boundary[0] = "python"
        if failure == "wrong-cwd": boundary[2] = "elsewhere"
        guard.observe({"event": "_winapi.CreateProcess/arguments", "args": boundary})
        if failure == "missing-native": guard.finish()
        if failure == "next-popen": guard.observe({"event": "subprocess.Popen", "args": args})
        if failure == "duplicate-boundary":
            guard.observe({"event": "_winapi.CreateProcess/arguments", "args": args})
        native = [None, "unknown" if failure == "unknown-native" else native_command, None]
        if failure == "native-executable": native[0] = "python"
        if failure == "native-cwd": native[2] = "elsewhere"
        guard.observe({"event": "_winapi.CreateProcess", "args": native})
        guard.finish()


@pytest.mark.skipif(os.name != "nt", reason="Windows native audit contract")
def test_real_windows_boundary_records_exact_command_and_rejects_direct_call(entry_checkout):
    probe = entry_checkout / "scripts/_native_probe.py"
    probe.write_text("import subprocess\nsubprocess.run(['git', '--version'], check=True)\n", encoding="utf-8")
    result = run_audited(entry_checkout, [["--version"]], "scripts/_native_probe.py")
    assert result.returncode == 0
    assert [item["event"] for item in result.audit_events] == [
        "subprocess.Popen", "_winapi.CreateProcess/arguments", "_winapi.CreateProcess"] * 2
    assert result.audit_events[:2] == calibration_events()[:2]
    assert result.audit_events[3]["args"] == result.audit_events[4]["args"] == [None, "git --version", None]
    probe.write_text("import _winapi, subprocess\ntry:\n    _winapi.CreateProcess(None, 'git --version', None, None, False, 0, None, None, subprocess.STARTUPINFO())\nexcept Exception:\n    pass\n", encoding="utf-8")
    with pytest.raises(AuditViolation, match="unpaired"):
        run_audited(entry_checkout, [], "scripts/_native_probe.py")


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

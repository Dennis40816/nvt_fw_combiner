"""Behavioral E1/E3 cases and the historical T2a replay; never invoke dotnet."""

from __future__ import annotations

import json
import os
import subprocess
import sys
import tempfile
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
from scripts import split_equivalence as split
from scripts.authority_check import Git

FIXTURES = Path(__file__).parent / "fixtures" / "split-equivalence"


def text(name: str) -> str:
    return (FIXTURES / name).read_text(encoding="utf-8")


@pytest.mark.parametrize("after,missing,added", [
    ("equal.txt", [], []),
    ("localized.txt", [], []),
    ("missing.txt", ['Theory(value: "a.b", count: 1)'], []),
    ("added.txt", [], ["Extra"]),
    ("theory-changed.txt", ['Theory(value: "a.b", count: 1)'], ['Theory(value: "a.b", count: 2)']),
    ("duplicate-missing.txt", ["Same"], []),
])
def test_discovery_keeps_arguments_and_multiplicity(after, missing, added):
    report = split.e1(text("before.txt"), text(after))
    assert report["before_count"] == 4
    assert report["after_count"] == 4 - len(missing) + len(added)
    assert report["missing"] == missing
    assert report["added"] == added
    assert report["passed"] == (not missing and not added)


def test_same_method_in_distinct_classes_is_a_multiset():
    old = split.discovery(text("before.txt"))
    assert [case.class_name for case in old if case.method == "Same"] == ["Old.One", "Old.Two"]
    assert split.e1(text("before.txt"), text("duplicate-missing.txt"))["missing"] == ["Same"]


def test_mapping_checks_each_occurrence_and_accepts_qualified_classes():
    mapping = json.loads(text("mapping.json"))
    report = split.e1(text("before.txt"), text("equal.txt"), mapping)
    assert report["moved-to-unexpected-class"] == [
        {"identity": "Same", "actual": "New.SecondTests", "expected": "FirstTests"}]
    assert not report["passed"]
    before = "Old.One.Check\nOld.One.Theory(value: 1)"
    after = "New.FirstTests.Check\nNew.FirstTests.Theory(value: 1)"
    assert split.e1(before, after, {"Check": "New.FirstTests", "Theory(value: 1)": "FirstTests"})["passed"]


def test_topic_mapping_requires_explicit_method_identities():
    mapping = {"A.Topic.cs": {"class": "FirstTests", "identities": ["Check", "Theory(value: 1)"]}}
    assert split.e1("Old.A.Check\nOld.A.Theory(value: 1)",
                    "New.FirstTests.Check\nNew.FirstTests.Theory(value: 1)", mapping)["passed"]


def test_added_case_with_a_baseline_mapping_is_a_difference():
    report = split.e1("Old.C.Check", "New.C.Check\nNew.C.Extra", {"Check": "C"})
    assert not report["passed"]
    assert report["added"] == ["Extra"]
    assert report["moved-to-unexpected-class"] == [
        {"identity": "Extra", "actual": "New.C", "expected": None}]


@pytest.mark.parametrize("mapping", [[], {"Check": "FirstTests"}, {"UnknownTopic": "FirstTests"},
    {"Check": 1}, {"Topic": {"class": "FirstTests", "identities": ["Check", "Check"]}},
    {"Topic": {"class": "FirstTests", "identities": []}}])
def test_unknown_incomplete_or_ambiguous_mapping_is_input_error(mapping):
    with pytest.raises(ValueError):
        split.e1(text("before.txt"), text("equal.txt"), mapping)


@pytest.mark.parametrize("value", ["", "Localized header only\n", "Header\nnot a test\n",
                                   "Header\nN.C.Valid\ninvalid\n"])
def test_malformed_discovery_fails_closed(value):
    with pytest.raises(ValueError):
        split.discovery(value)


def load_case(case: dict) -> tuple[split.FileDiff, ...]:
    return tuple(split.FileDiff(item["status"], item["old_path"], item["new_path"],
                                split.source_lines(item["before"]), split.source_lines(item["after"]),
                                split.patch_lines(item["patch"])) for item in case["files"])


CASES = json.loads(text("synthetic-diffs.json"))


@pytest.mark.parametrize("case", CASES, ids=lambda case: case["name"])
def test_synthetic_allowed_and_forbidden_changes(case):
    files = load_case(case)
    report = split.e3(files, "Support")
    assert report["passed"] == case["passed"]
    assert report["counts"] == case["counts"]
    assert report["unclassified"] == case["unclassified"]
    assert sum(value for kind, value in report["counts"].items() if kind != "rename") + len(
        report["unclassified"]) == sum(len(file.lines) for file in files)


def test_helpers_require_exact_whitespace_and_occurrence_pairing():
    case = next(case for case in CASES if case["name"] == "helper_move")
    files = load_case(case)
    old, new = files
    changed = tuple(line.replace("return 1;", "return  1;") for line in new.after)
    delta = tuple(split.Line(line.side, line.number, line.text.replace("return 1;", "return  1;"))
                  for line in new.lines)
    report = split.e3((old, split.FileDiff(new.status, new.old_path, new.new_path,
                                         new.before, changed, delta)), "Support")
    assert report["counts"]["helper_move"] == 0
    assert not report["passed"]
    # A single addition cannot justify two removed occurrences.
    duplicate = split.FileDiff(old.status, "Other.cs", "Other.cs", old.before, old.after, old.lines)
    report = split.e3((old, duplicate, new), "Support")
    assert not report["passed"]
    assert {line["file"] for line in report["unclassified"]} == {"Other.cs"}


def test_wrong_support_class_does_not_allow_accessibility_or_moves():
    case = next(case for case in CASES if case["name"] == "support_accessibility")
    assert not split.e3(load_case(case), "OtherSupport")["passed"]


def test_nul_name_list_preserves_unusual_paths():
    assert split.name_list('R100\0a space.cs\0新\tline\nname.cs\0M\0other.cs\0'.encode()) == (
        ("R", "a space.cs", "新\tline\nname.cs"), ("M", "other.cs", "other.cs"))


@pytest.mark.parametrize("data", [b"M\0path", b"R100\0old\0", b"M\0\0", b"C100\0a\0b\0"])
def test_malformed_name_list_is_input_error(data):
    with pytest.raises(ValueError):
        split.name_list(data)


@pytest.mark.parametrize("patch", ["@@ -1,2 +1 @@\n-one\n+two\n",
    "@@ -1 +1 @@\n context\n", "@@ -1 +1 @@\n-one\n@@ -2 +2 @@\n-two\n+three\n"])
def test_malformed_hunks_are_input_errors(patch):
    with pytest.raises(ValueError):
        split.patch_lines(patch)


def test_mode_and_end_of_file_changes_fail_e3():
    for patch in ("old mode 100644\nnew mode 100755\n", "\\ No newline at end of file\n"):
        file = split.FileDiff("M", "A.cs", "A.cs", (), (), split.patch_lines(patch))
        assert not split.e3((file,))["passed"]


@pytest.mark.parametrize("wrapper", ['    private static string Text = @"\n{body}\n";',
                                     '    private static string Text = """\n{body}\n""";',
                                     '/*\n{body}\n*/'])
@pytest.mark.parametrize("before,after", [("using System;", "using Other;"),
    ("public sealed partial class Old", "public sealed partial class New"),
    ("    private static int Value;", "    internal static int Value;")])
def test_class_like_text_inside_literals_and_comments_is_not_allowed(wrapper, before, after):
    prefix = "internal static partial class Support\n{\n"
    source = prefix + wrapper.format(body=before) + "\n}\n"
    new = prefix + wrapper.format(body=after) + "\n}\n"
    file = split.FileDiff("M", "Support.cs", "Support.cs", split.source_lines(source),
                         split.source_lines(new), (split.Line("-", 4, before), split.Line("+", 4, after)))
    report = split.e3((file,), "Support")
    assert not report["passed"]
    assert len(report["unclassified"]) == 2


def test_git_seam_disables_external_transforms_uses_literal_paths_and_timeout(monkeypatch):
    calls = []
    def run(arguments, **options):
        calls.append((arguments, options))
        if "rev-parse" in arguments:
            return subprocess.CompletedProcess(arguments, 0, b"a" * 40 + b"\n", b"")
        if "ls-tree" in arguments:
            return subprocess.CompletedProcess(arguments, 0, "a space/新\0".encode(), b"")
        return subprocess.CompletedProcess(arguments, 0, b"", b"")
    monkeypatch.setattr(subprocess, "run", run)
    assert split.read_diff(Git(ROOT), "base", "head", "a space/新") == ()
    for arguments, options in calls:
        assert isinstance(arguments, list)
        assert arguments[:2] == ["git", "--literal-pathspecs"]
        assert options["timeout"] > 0
        assert not options.get("shell", False)
    arguments = calls[-1][0]
    assert all(option in arguments for option in ("-z", "--find-renames", "--no-ext-diff", "--no-textconv"))
    assert arguments[-2:] == ["--", "a space/新"]


@pytest.mark.parametrize("project", ["missing", "../other", "C:/other", "/other", ""])
def test_unknown_or_non_relative_project_is_input_error(project):
    class EmptyGit:
        def commit(self, value):
            return value
        def run(self, *args):
            return b""
    with pytest.raises(ValueError):
        split.read_diff(EmptyGit(), "a", "b", project)


def test_helper_moves_infer_support_but_do_not_accept_non_csharp_targets():
    files = load_case(next(case for case in CASES if case["name"] == "helper_move"))
    assert split.e3(files)["passed"]
    old, target = files
    non_source = split.FileDiff(target.status, "Support.txt", "Support.txt", target.before,
                                target.after, target.lines)
    assert not split.e3((old, non_source))["passed"]


def test_summary_cannot_claim_a_class_in_an_unchanged_baseline_file_is_new():
    entry = next(case for case in CASES if case["name"] == "class_summary_collection")["files"][0]
    class BaselineGit(Git):
        def commit(self, value):
            return value
        def run(self, *arguments):
            if arguments[0] == "ls-tree":
                return b"old.cs\0unchanged.cs\0" if "-r" in arguments else b"project\0"
            if "--name-status" in arguments:
                return b"R099\0old.cs\0new.cs\0"
            assert arguments[0] == "diff"
            return ("diff --git a/old.cs b/new.cs\n" + entry["patch"]).encode()
        def blob(self, revision, path):
            source = {"old.cs": entry["before"], "new.cs": entry["after"],
                      "unchanged.cs": "public sealed partial class New\n{\n}\n"}[path]
            return "a" * 40, source.encode()
    files = split.read_diff(BaselineGit(ROOT), "base", "head", "project")
    assert files[0].existing_classes == frozenset({"Old", "New"})
    report = split.e3(files)
    assert not report["passed"]
    assert [line["text"] for line in report["unclassified"]] == ["/// <summary>New topic checks.</summary>"]


def test_git_timeout_and_failed_command_are_input_errors(monkeypatch, capsys):
    for failure in (subprocess.TimeoutExpired("git", 1), OSError("git unavailable")):
        def run(*args, **kwargs):
            raise failure
        monkeypatch.setattr(subprocess, "run", run)
        assert split.main(["e3", "--base", "a", "--head", "b", "--project", "tests"]) == 2
        assert "error" in json.loads(capsys.readouterr().out)
    monkeypatch.setattr(subprocess, "run", lambda *args, **kwargs:
                        subprocess.CompletedProcess([], 128, b"", b"unknown revision"))
    assert split.main(["e3", "--base", "a", "--head", "b", "--project", "tests"]) == 2
    assert "unknown revision" in json.loads(capsys.readouterr().out)["error"]


def test_e1_cli_exit_codes_and_json(capsys):
    for after, code in (("equal.txt", 0), ("missing.txt", 1), ("absent.txt", 2)):
        assert split.main(["e1", "--before", str(FIXTURES / "before.txt"),
                           "--after", str(FIXTURES / after)]) == code
        report = json.loads(capsys.readouterr().out)
        assert report["passed"] == (code == 0)
    assert split.main(["e1"]) == 2
    assert "error" in json.loads(capsys.readouterr().out)


def test_e1_cli_rejects_duplicate_json_keys_and_null_mapping(capsys):
    with tempfile.TemporaryDirectory(prefix="split-equivalence-") as directory:
        path = Path(directory) / "mapping.json"
        path.write_text('{"Check":"FirstTests","Check":"SecondTests"}', encoding="utf-8")
        arguments = ["e1", "--before", str(FIXTURES / "before.txt"),
                     "--after", str(FIXTURES / "equal.txt"), "--mapping", str(path)]
        assert split.main(arguments) == 2
        assert "duplicate JSON key" in json.loads(capsys.readouterr().out)["error"]
        path.write_text("null", encoding="utf-8")
        assert split.main(arguments) == 2
        assert "error" in json.loads(capsys.readouterr().out)


def test_e3_cli_returns_difference_as_one(monkeypatch, capsys):
    case = next(case for case in CASES if case["name"] == "changed_assertion")
    monkeypatch.setattr(split, "read_diff", lambda *args: load_case(case))
    assert split.main(["e3", "--base", "a", "--head", "b", "--project", "tests"]) == 1
    assert json.loads(capsys.readouterr().out)["unclassified"] == case["unclassified"]


def test_real_pilot_range_through_git_seam(monkeypatch, capsys):
    # Process environment only: never edit Git config or create another worktree.
    monkeypatch.setenv("GIT_CONFIG_GLOBAL", os.devnull)
    monkeypatch.setenv("GIT_CONFIG_NOSYSTEM", "1")
    expected = json.loads(text("pilot.json"))
    assert split.main(["e3", "--base", expected["base"], "--head", expected["head"],
                       "--project", expected["project"], "--support-class",
                       "RepositoryBoundaryTestSupport"]) == 0
    report = json.loads(capsys.readouterr().out)
    assert report["changed_paths"] == 75
    assert report["counts"] == expected["counts"]
    assert report["unclassified"] == []
    assert sum(count for kind, count in report["counts"].items() if kind != "rename") == 307


def test_direct_script_invocation_works_without_pythonpath():
    environment = dict(os.environ, PYTHONDONTWRITEBYTECODE="1")
    environment.pop("PYTHONPATH", None)
    result = subprocess.run([sys.executable, str(ROOT / "scripts" / "split_equivalence.py"),
                             "e1", "--before", str(FIXTURES / "before.txt"),
                             "--after", str(FIXTURES / "equal.txt")], cwd=ROOT,
                            env=environment, capture_output=True, text=True, timeout=30)
    assert result.returncode == 0, result.stderr
    assert json.loads(result.stdout)["passed"]

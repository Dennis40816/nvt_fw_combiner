"""Behavioral E1/E3 cases and the historical T2a replay; never invoke dotnet."""

from __future__ import annotations

import json
import os
import subprocess
import sys
import tempfile
from dataclasses import replace
from pathlib import Path

import pytest

from split_equivalence_test_support import (
    ROOT, FIXTURES, CASES, split, text, load_case, changed_file, require_split_commits, added_collection_definition,
)
from scripts.authority_check import Git


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


def test_mapping_refuses_ambiguous_old_classes_and_accepts_qualified_pairs():
    mapping = json.loads(text("mapping.json"))
    with pytest.raises(ValueError, match="multiple old classes"):
        split.e1(text("before.txt"), text("equal.txt"), mapping)
    before = "Old.One.Check\nOld.One.Theory(value: 1)"
    after = "Old.FirstTests.Check\nOld.FirstTests.Theory(value: 1)"
    mapping = {method: {"old_class": "Old.One", "new_class": "Old.FirstTests"}
               for method in ("Check", "Theory(value: 1)")}
    report = split.e1(before, after, mapping)
    assert report["passed"]
    assert (report["before_count"], report["after_count"]) == (2, 2)


def test_topic_mapping_requires_explicit_method_identities():
    mapping = {"A.Topic.cs": {"old_class": "Old.A", "new_class": "Old.FirstTests",
                               "identities": ["Check", "Theory(value: 1)"]}}
    report = split.e1("Old.A.Check\nOld.A.Theory(value: 1)",
                      "Old.FirstTests.Check\nOld.FirstTests.Theory(value: 1)", mapping)
    assert report["passed"]
    assert (report["before_count"], report["after_count"]) == (2, 2)


def test_added_case_with_a_baseline_mapping_is_a_difference():
    report = split.e1("Old.C.Check", "Old.New.Check\nOld.New.Extra",
                      {"Check": {"old_class": "Old.C", "new_class": "Old.New"}})
    assert not report["passed"]
    assert report["added"] == ["Extra"]
    assert report["moved-to-unexpected-class"] == [
        {"identity": "Extra", "actual": "Old.New", "expected": None}]
    assert (report["before_count"], report["after_count"]) == (1, 2)


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


@pytest.mark.parametrize("case", CASES, ids=lambda case: case["name"])
def test_synthetic_allowed_and_forbidden_changes(case):
    files = load_case(case)
    report = split.e3(files, "Support")
    assert report["passed"] == case["passed"]
    assert report["counts"] == {**case["counts"], "support_partial_conversion": 0}
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
    assert report["counts"]["helper_move"] == 8
    assert {line["file"] for line in report["unclassified"]} == {"Other.cs"}


def test_wrong_support_class_does_not_allow_accessibility_or_moves():
    case = next(case for case in CASES if case["name"] == "support_accessibility")
    report = split.e3(load_case(case), "OtherSupport")
    assert not report["passed"]
    assert report["counts"]["support_accessibility"] == 0
    assert report["counts"]["class_declaration"] == 0


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
        report = split.e3((file,))
        assert not report["passed"]
        assert not any(report["counts"].values())


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
        if "merge-base" in arguments:
            return subprocess.CompletedProcess(arguments, 0, b"a" * 40 + b"\n", b"")
        if "ls-tree" in arguments:
            return subprocess.CompletedProcess(arguments, 0, "a space/新\0".encode(), b"")
        return subprocess.CompletedProcess(arguments, 0, b"", b"")
    monkeypatch.setattr(subprocess, "run", run)
    with pytest.raises(ValueError, match="no changed path"):
        split.read_diff(Git(ROOT), "base", "head", "a space/新")
    for arguments, options in calls:
        assert isinstance(arguments, list)
        assert arguments[:2] == ["git", "--literal-pathspecs"]
        assert options["timeout"] > 0
        assert not options.get("shell", False)
    arguments = calls[-1][0]
    assert all(option in arguments for option in ("-z", "--find-renames", "--no-ext-diff", "--no-textconv",
                                                  "--ignore-submodules=none"))
    assert arguments[-2:] == ["--", "a space/新"]


@pytest.mark.parametrize("project", ["missing", "../other", "C:/other", "/other", ""])
def test_unknown_or_non_relative_project_is_input_error(project):
    class EmptyGit:
        def commit(self, value):
            return value
        def run(self, *args):
            return b""
        def merge_base(self, first, second):
            return first
    with pytest.raises(ValueError):
        split.read_diff(EmptyGit(), "a", "b", project)


def test_helper_moves_infer_support_but_do_not_accept_non_csharp_targets():
    files = load_case(next(case for case in CASES if case["name"] == "helper_move"))
    report = split.e3(files)
    assert report["passed"]
    assert report["counts"]["helper_move"] == 8
    old, target = files
    non_source = split.FileDiff(target.status, "Support.txt", "Support.txt", target.before,
                                target.after, target.lines)
    report = split.e3((old, non_source))
    assert not report["passed"]
    assert report["counts"]["helper_move"] == 0


def test_summary_cannot_claim_a_class_in_an_unchanged_baseline_file_is_new():
    entry = next(case for case in CASES if case["name"] == "class_summary_collection")["files"][0]
    class BaselineGit(Git):
        def commit(self, value):
            return value
        def merge_base(self, first, second):
            return first
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
    assert report["counts"]["class_declaration"] == 0
    assert {line["text"] for line in report["unclassified"]} == {
        "public sealed partial class Old", "public sealed partial class New",
        "/// <summary>New topic checks.</summary>", "[Collection(nameof(Serial))]"}


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
        if code != 2:
            assert (report["before_count"], report["after_count"]) == (4, 4 if code == 0 else 3)
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
    monkeypatch.setattr(Git, "commit", lambda self, value: "a" * 40)
    assert split.main(["e3", "--base", "a", "--head", "b", "--project", "tests"]) == 1
    report = json.loads(capsys.readouterr().out)
    assert report["unclassified"] == case["unclassified"]
    assert report["counts"] == {**case["counts"], "support_partial_conversion": 0}


def test_real_pilot_range_accepts_the_source_class_collection_and_pins_counts(monkeypatch, capsys):
    # Process environment only: never edit Git config or create another worktree.
    monkeypatch.setenv("GIT_CONFIG_GLOBAL", os.devnull)
    monkeypatch.setenv("GIT_CONFIG_NOSYSTEM", "1")
    expected = json.loads(text("pilot.json"))
    require_split_commits(expected)
    assert split.main(["e3", "--base", expected["base"], "--head", expected["head"],
                       "--project", expected["project"], "--support-class",
                       "RepositoryBoundaryTestSupport"]) == 0
    report = json.loads(capsys.readouterr().out)
    assert report["changed_paths"] == 75
    assert report["base"] == expected["base"]
    assert report["head"] == expected["head"]
    assert len(report["base"]) == len(report["head"]) == 40
    assert report["project"] == expected["project"]
    assert report["support_class"] == "RepositoryBoundaryTestSupport"
    assert report["counts"] == {**expected["counts"], "support_partial_conversion": 0}
    assert report["unclassified"] == expected["unclassified"]
    assert report["passed"]
    assert sum(count for kind, count in report["counts"].items() if kind != "rename") == 307
    assert sum(count for kind, count in report["counts"].items() if kind != "rename") + len(
        report["unclassified"]) == 307


def test_direct_script_invocation_works_without_pythonpath():
    environment = dict(os.environ, PYTHONDONTWRITEBYTECODE="1")
    environment.pop("PYTHONPATH", None)
    result = subprocess.run([sys.executable, str(ROOT / "scripts" / "split_equivalence.py"),
                             "e1", "--before", str(FIXTURES / "before.txt"),
                             "--after", str(FIXTURES / "equal.txt")], cwd=ROOT,
                            env=environment, capture_output=True, text=True, timeout=30)
    assert result.returncode == 0, result.stderr
    report = json.loads(result.stdout)
    assert report["passed"]
    assert (report["before_count"], report["after_count"]) == (4, 4)


@pytest.mark.parametrize("prefix,suffix", [('"""', '"""'), ('@"', '"'),
    ('$@"', '"'), ('@$"', '"'), ('$"""', '"""'), ('$$"""', '"""')])
@pytest.mark.parametrize("before,after", [("", "\n"), ("\n", ""),
    ("\n", "\r\n"), ("\n", "    \n")])
def test_blank_changes_inside_multiline_literals_are_not_mechanical(prefix, suffix, before, after):
    def source(value):
        return f'public sealed partial class Tests\n{{\n    [Fact]\n    public void M()\n    {{\n        var s = {prefix}\n{value}text\n{suffix};\n    }}\n}}\n'
    file = changed_file(source(before), source(after))
    report = split.e3((file,))
    assert not report["passed"]
    assert report["counts"]["blank"] == 0
    assert len(report["unclassified"]) == len(file.lines)


@pytest.mark.parametrize("size", [2, 3])
def test_support_declaration_swaps_cannot_swap_member_bodies(size):
    declarations = [f"    private static int M{number}()" for number in range(size)]
    def source(order, modifier):
        return "internal static partial class Support\n{\n" + "".join(
            declarations[number].replace("private", modifier) + f"\n    {{\n        return {body};\n    }}\n"
            for body, number in enumerate(order)) + "}\n"
    file = changed_file(source(list(range(size)), "private"),
                        source(list(range(1, size)) + [0], "internal"), "Support.cs")
    report = split.e3((file,), "Support")
    assert not report["passed"]
    assert report["counts"]["support_accessibility"] == 0
    assert len(report["unclassified"]) == size * 2


@pytest.mark.parametrize("old,new", [("Old.cs", "Old.cs.txt"), ("Old.txt", "Old.cs")])
def test_rename_requires_csharp_on_both_sides(old, new):
    source = split.source_lines("public sealed partial class Old\n{\n}\n")
    report = split.e3((split.FileDiff("R", old, new, source, source, ()),))
    assert not report["passed"]
    assert report["counts"]["rename"] == 0
    assert len(report["unclassified"]) == 1


@pytest.mark.parametrize("first", ["A.Outer+Inner.M", "custom display name"])
def test_e1_never_discards_an_unparseable_first_test(first, capsys):
    with tempfile.TemporaryDirectory(prefix="split-equivalence-") as directory:
        before, after = Path(directory) / "before.txt", Path(directory) / "after.txt"
        before.write_text(first + "\nA.B.K\n", encoding="utf-8")
        after.write_text("A.B.K\n", encoding="utf-8")
        assert split.main(["e1", "--before", str(before), "--after", str(after)]) == 2
    assert "invalid discovery line" in json.loads(capsys.readouterr().out)["error"]


def test_e1_reports_only_explicit_unindented_headers():
    report = split.e1("Tests:\n    A.Old.M\n", "可用的測試：\n    A.New.M\n")
    assert report["passed"]
    assert (report["before_count"], report["after_count"]) == (1, 1)
    assert report["skipped_headers"] == {"before": ["Tests:"], "after": ["可用的測試："]}
    with pytest.raises(ValueError):
        split.discovery("    Custom:\nA.B.K\n")


@pytest.mark.parametrize("declaration", ["internal static partial class Support",
    "internal sealed partial class New", "public static partial class New",
    "public sealed class New"])
def test_test_class_declaration_cannot_change_modifiers(declaration):
    before = "public sealed partial class Old\n{\n    [Fact]\n    public void M() {}\n}\n"
    file = changed_file(before, before.replace("public sealed partial class Old", declaration))
    report = split.e3((file,), "Support")
    assert not report["passed"]
    assert report["counts"]["class_declaration"] == 0
    assert len(report["unclassified"]) == 2


@pytest.mark.parametrize("prefix", ['@$"', '$"""', '$$"""'])
def test_interpolated_literals_mask_code_like_content(prefix):
    suffix = '"' if prefix == '@$"' else '"""'
    before = f'internal static partial class Support\n{{\n    static string Text = {prefix}\nusing System;\n{suffix};\n}}\n'
    file = changed_file(before, before.replace("using System;", "using Other;"), "Support.cs")
    report = split.e3((file,), "Support")
    assert not report["passed"]
    assert report["counts"]["using"] == 0
    assert len(report["unclassified"]) == 2


@pytest.mark.parametrize("directive", ["using Assert = N.NoOpAssert;",
    "global using Assert = N.NoOpAssert;", "using static N.Evil;",
    "global using static N.Evil;"])
def test_added_aliases_and_unapproved_static_usings_are_unclassified(directive):
    file = changed_file("namespace N;\n", directive + "\nnamespace N;\n")
    report = split.e3((file,), "Support")
    assert not report["passed"]
    assert report["counts"]["using"] == 0
    assert len(report["unclassified"]) == 1


@pytest.mark.parametrize("directive", ["using System;", "global using System;",
    "global using static N.Support;"])
def test_only_added_plain_namespaces_or_the_explicit_support_static_using_pass(directive):
    file = changed_file("namespace N;\n", directive + "\nnamespace N;\n")
    file = replace(file, head_sources=(("Support.cs", split.source_lines(
        "namespace N;\ninternal static partial class Support\n{\n}\n")),))
    report = split.e3((file,), "Support")
    assert report["passed"]
    assert report["counts"]["using"] == 1
    assert sum(report["counts"].values()) == 1
    assert report["unclassified"] == []


def test_static_using_cannot_infer_support_or_match_a_different_qualified_class():
    file = changed_file("namespace N;\n", "global using static N.Support;\nnamespace N;\n")
    for support in (None, "Other.Support"):
        report = split.e3((file,), support)
        assert not report["passed"]
        assert report["counts"]["using"] == 0
        assert len(report["unclassified"]) == 1


@pytest.mark.parametrize("directive,passed", [("using System;", True),
    ("global using System;", True), ("global using static N.Support;", False),
    ("using Assert = N.NoOpAssert;", False), ("using static N.Evil;", False),
    ("using System; Assert.True(false);", False)])
def test_removed_usings_follow_the_same_namespace_allowlist(directive, passed):
    file = changed_file(directive + "\nnamespace N;\n", "namespace N;\n")
    report = split.e3((file,), "Support")
    assert report["passed"] == passed
    assert report["counts"]["using"] == int(passed)
    assert len(report["unclassified"]) == int(not passed)


@pytest.mark.parametrize("before,after", [('[Collection("A")]\n', '[Collection("B")]\n'),
    ('[Collection("A")]\n', ''), ('', '[Collection("A")]\n')])
def test_collection_on_an_unsplit_class_cannot_change(before, after):
    body = "public sealed partial class Tests\n{\n}\n"
    file = changed_file(before + body, after + body)
    report = split.e3((file,))
    assert not report["passed"]
    assert report["counts"]["collection_attribute"] == 0
    assert len(report["unclassified"]) == len(file.lines)


@pytest.mark.parametrize("argument,interface", [('"A", DisableParallelization = true', ''),
    ('"A", DisableParallelization = false', ''), ('"A", Other = 1', ''),
    ('"A"', ' : ICollectionFixture<Fixture>')])
def test_collection_definitions_cannot_introduce_options_or_interfaces(argument, interface):
    source = f'[CollectionDefinition({argument})]\npublic sealed class Serial{interface}\n{{\n}}\n'
    file = changed_file("", source, "Definition.cs")
    file = split.FileDiff("A", file.old_path, file.new_path, (), file.after, file.lines)
    report = split.e3((file,))
    assert not report["passed"]
    assert report["counts"]["collection_definition"] == 0


def test_split_classes_must_share_one_collection():
    before = "public sealed partial class Old\n{\n}\n"
    files = tuple(changed_file(before, f'[Collection("{name}")]\n' + before.replace("Old", name),
                               name + ".cs") for name in ("A", "B"))
    report = split.e3(files)
    assert not report["passed"]
    assert report["counts"]["collection_attribute"] == 0
    assert report["counts"]["class_declaration"] == 4
    assert len(report["unclassified"]) == 2


@pytest.fixture
def renamed_split_file():
    before = "public sealed partial class Old\n{\n}\n"
    file = changed_file(before, '[Collection("A")]\n' + before.replace("Old", "New"),
                        "tests/Project/Old.Topic.cs")
    return split.FileDiff("R", file.old_path, "tests/Project/New.Topic.cs",
                          file.before, file.after, file.lines)


@pytest.mark.parametrize("name,path,attributes,passed,classified,unclassified", [
    ("Old", "tests/Project/Old.cs", '[Collection("A")]\n', True, 2, 0),
    ("Unrelated", "tests/Project/Unrelated.cs", '[Collection("A")]\n', False, 1, 1),
    ("Old", "tests/Project/Old.cs", '[Collection("B")]\n', False, 0, 2),
    ("Old", "tests/Project/Old.cs", '[Collection("A")]\n' * 2, False, 1, 2),
    ("Old", "tests/Project/Old.Topic.cs", '[Collection("A")]\n', False, 1, 1),
    ("Old", "tests/OtherProject/Old.cs", '[Collection("A")]\n', False, 1, 1),
    ("Old", "tests/Project/Old.cs", '[Collection("A", Other = 1)]\n', False, 1, 1),
], ids=["source", "unrelated", "different-collection", "duplicate", "partial-file",
        "other-project", "named-argument"])
def test_source_collection_requires_the_unchanged_root_and_one_shared_attribute(
        renamed_split_file, name, path, attributes, passed, classified, unclassified):
    body = f"public sealed partial class {name}\n{{\n}}\n"
    file = changed_file(body, attributes + body, path)
    report = split.e3((renamed_split_file, file, added_collection_definition("A", "tests/Project/Shared.cs")))
    assert report["passed"] == passed
    assert report["counts"]["collection_attribute"] == classified
    assert len(report["unclassified"]) == unclassified
    assert all(collection["text"].startswith("[Collection(") for collection in report["unclassified"])


@pytest.mark.parametrize("after", ['', '[Collection("A")]\n'])
def test_source_collection_removal_or_replacement_is_unclassified(renamed_split_file, after):
    body = "public sealed partial class Old\n{\n}\n"
    file = changed_file('[Collection("B")]\n' + body, after + body, "tests/Project/Old.cs")
    report = split.e3((renamed_split_file, file))
    assert not report["passed"]
    assert report["counts"]["collection_attribute"] == 1
    assert len(report["unclassified"]) == 1 + bool(after)


@pytest.mark.parametrize("partial_path", ["tests/Project/Old.Other.cs", "tests/Project/Sub/Old.Other.cs"])
def test_source_collection_cannot_be_added_on_two_partial_declarations(renamed_split_file, partial_path):
    body = "public sealed partial class Old\n{\n}\n"
    files = tuple(changed_file(body, '[Collection("A")]\n' + body, path)
                  for path in ("tests/Project/Old.cs", partial_path))
    report = split.e3((renamed_split_file, *files))
    assert not report["passed"]
    assert report["counts"]["collection_attribute"] == 1
    assert len(report["unclassified"]) == 2


@pytest.mark.parametrize("attribute", ['[Theory]', '[Fact(Skip = "reason")]',
    '[Fact]\n    [Trait("Category", "Other")]'])
def test_test_attribute_changes_are_unclassified(attribute):
    before = "public sealed partial class Tests\n{\n    [Fact]\n    public void M() {}\n}\n"
    file = changed_file(before, before.replace("[Fact]", attribute))
    report = split.e3((file,))
    assert not report["passed"]
    assert not any(report["counts"].values())
    assert len(report["unclassified"]) == len(file.lines)


def test_visibility_outside_support_and_line_endings_are_unclassified():
    before = "public sealed partial class Tests\n{\n    private static int M() => 1;\n}\n"
    for after in (before.replace("private", "internal"), before.replace("\n", "\r\n")):
        file = changed_file(before, after)
        report = split.e3((file,), "Support")
        assert not report["passed"]
        assert not any(report["counts"].values())
        assert len(report["unclassified"]) == len(file.lines)


@pytest.mark.parametrize("patch", ["Binary files a/A.cs and b/A.cs differ\n",
    "old mode 100644\nnew mode 100755\n", "\\ No newline at end of file\n"])
def test_binary_mode_and_final_newline_changes_have_no_classification(patch):
    file = split.FileDiff("M", "A.cs", "A.cs", (), (), split.patch_lines(patch))
    report = split.e3((file,))
    assert not report["passed"]
    assert not any(report["counts"].values())
    assert len(report["unclassified"]) == len(file.lines)

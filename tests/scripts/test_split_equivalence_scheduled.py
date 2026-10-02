"""Scheduled split replays, whole-member moves and conservative C# boundaries."""

from __future__ import annotations

import json
import difflib
import tempfile
from dataclasses import replace
from pathlib import Path

import pytest

from split_equivalence_test_support import (
    ROOT, MEMBER_BODIES, SCHEDULED_SPLITS, split,
    text, load_case, changed_file, split_pair, member_move, require_split_commits, added_collection_definition,
)
from scripts.authority_check import Git


@pytest.mark.parametrize("project", ["tests/NvtFwCombiner.Architecture.Tests", "scripts/split_equivalence.py"])
def test_real_git_empty_project_diff_is_input_error(project, capsys):
    assert split.main(["e3", "--base", "HEAD", "--head", "HEAD", "--project", project]) == 2
    assert "no changed path" in json.loads(capsys.readouterr().out)["error"]


def test_empty_diff_is_not_mechanical_evidence():
    with pytest.raises(ValueError, match="no changed path"):
        split.e3(())


def test_real_git_nonancestor_base_is_input_error(capsys):
    pilot = json.loads(text("pilot.json"))
    require_split_commits(pilot)
    assert split.main(["e3", "--base", pilot["head"], "--head", pilot["base"],
                       "--project", pilot["project"]]) == 2
    assert "ancestor" in json.loads(capsys.readouterr().out)["error"]


def test_e1_namespace_root_changes_are_differences():
    with pytest.raises(ValueError, match="invalid mapping"):
        split.e1("ProjA.Tests.Old.M", "ProjB.Tests.New.M", {"M": "New"})
    mapping = {"M": {"old_class": "ProjA.Tests.Old", "new_class": "ProjB.Tests.New"}}
    report = split.e1("ProjA.Tests.Old.M", "ProjB.Tests.New.M", mapping)
    assert not report["passed"]
    assert (report["before_count"], report["after_count"]) == (1, 1)
    assert report["namespace_root_differences"] == [("ProjB", "M")]


def test_e1_mapping_checks_both_fully_qualified_classes():
    mapping = {"M": {"old_class": "N.Old", "new_class": "N.New"}}
    report = split.e1("N.Other.M", "N.New.M", mapping)
    assert not report["passed"]
    assert (report["before_count"], report["after_count"]) == (1, 1)
    assert report["moved-from-unexpected-class"] == [
        {"identity": "M", "actual": "N.Other", "expected": "N.Old"}]


def test_e1_without_header_preserves_counts_and_checks_namespace_roots():
    report = split.e1("N.Old.M\nN.Old.Theory(value: 1)", "N.New.M\nN.New.Theory(value: 1)")
    assert report["passed"]
    assert (report["before_count"], report["after_count"]) == (2, 2)
    assert report["skipped_headers"] == {"before": [], "after": []}
    report = split.e1("ProjA.Tests.Old.M", "ProjB.Tests.New.M")
    assert not report["passed"]
    assert (report["before_count"], report["after_count"]) == (1, 1)
    assert report["namespace_root_differences"] == [("ProjB", "M")]


@pytest.mark.parametrize("mapping", [{"M": {"old_class": "Old", "new_class": "N.New"}},
    {"M": {"old_class": "N.Old", "new_class": "New"}},
    {"topic": {"old_class": "N.Old", "new_class": "N.New", "identities": ["M", "M"]}}])
def test_e1_mapping_requires_qualified_classes_and_unique_identities(mapping):
    with pytest.raises(ValueError):
        split.e1("N.Old.M", "N.New.M", mapping)


def test_e1_mapping_reports_an_unexpected_new_class():
    report = split.e1("N.Old.M", "N.Other.M", {"M": {"old_class": "N.Old", "new_class": "N.New"}})
    assert not report["passed"]
    assert (report["before_count"], report["after_count"]) == (1, 1)
    assert report["moved-from-unexpected-class"] == []
    assert report["moved-to-unexpected-class"] == [
        {"identity": "M", "actual": "N.Other", "expected": "N.New"}]


@pytest.mark.parametrize("prefix", ['$@"', '@$"'])
@pytest.mark.parametrize("addition", ["\n", "using Other;\n"])
def test_interpolation_quote_ambiguity_rejects_every_changed_line(prefix, addition):
    before = f'namespace N;\nclass C\n{{\n    string s = {prefix}a{{Name("x")}}\ntext\n";\n}}\n'
    file = changed_file(before, "using System;\n" + before.replace("text\n", addition + "text\n"))
    report = split.e3((file,))
    assert not report["passed"]
    assert len(report["unclassified"]) == len(file.lines)


def test_unchanged_declaration_moved_across_blanks_is_not_a_split():
    before = '\n\n\npublic sealed partial class Old\n{\n}\n'
    after = '[Collection("Shared")]\npublic sealed partial class Old\n\n\n\n{\n}\n'
    report = split.e3((changed_file(before, after, "Old.cs"),))
    assert not report["passed"]
    assert report["counts"]["class_declaration"] == 0
    assert report["counts"]["collection_attribute"] == 0


def test_declaration_rename_requires_the_same_hunk():
    file = changed_file("public sealed partial class Old\n\n\n\n{\n}\n",
                        "\n\n\npublic sealed partial class New\n{\n}\n")
    report = split.e3((file,))
    assert not report["passed"]
    assert report["counts"]["class_declaration"] == 0


def test_unknown_hunk_identity_cannot_authorize_a_declaration_pair():
    old = "public sealed partial class Old"
    new = "public sealed partial class New"
    file = split.FileDiff("M", "Tests.cs", "Tests.cs", (old, "{", "}"), (new, "{", "}"),
                          (split.Line("-", 1, old), split.Line("+", 1, new)))
    report = split.e3((file,))
    assert not report["passed"]
    assert report["counts"]["class_declaration"] == 0


def test_modified_topic_declaration_identifies_the_source_root():
    report = split.e3((*split_pair(), added_collection_definition()))
    assert report["passed"]
    assert report["counts"]["collection_attribute"] == 2


@pytest.mark.parametrize("source,target,passed", [
    ('[Collection("Shared")]\n', '[Collection("Shared")]\n', True),
    ('[Collection("Shared")]\n', '', False),
    ('', '[Collection("Shared")]\n', False),
    ('', '', True),
    ('[Collection("Legacy")]\n', '[Collection("Shared")]\n', False),
])
def test_collection_consistency_includes_existing_source_attributes(source, target, passed):
    topic, root = split_pair(attribute=target)
    body = "namespace N;\n" + source + "public sealed partial class Old\n{\n}\n"
    root = changed_file(body, body + "\n", "Old.cs")
    report = split.e3((topic, root))
    assert report["passed"] == passed
    assert bool(report["collection_consistency"]) == (not passed)
    if not passed:
        assert {"Old", "New"} <= set(report["collection_consistency"][0]["classes"])


def test_collection_consistency_rejects_one_missing_topic():
    topic, root = split_pair()
    other = split_pair(new="Other", attribute="")[0]
    report = split.e3((topic, root, other))
    assert not report["passed"]
    assert report["collection_consistency"]
    assert "Other" in report["collection_consistency"][0]["classes"]


@pytest.mark.parametrize("attribute", ['[AvaloniaFact]', '[CustomTheory]',
    '[Trait("a", "b"), Fact]', '[Trait("a", "b"), N.AvaloniaFactAttribute]',
    '[global::N.Fact]', '[@AvaloniaFact]', '[Trait("a", "b"),\n        CustomTheory]'])
def test_all_fact_and_theory_attributes_prevent_support_conversion(attribute):
    before = f"public sealed partial class Old\n{{\n    {attribute}\n    public void M() {{}}\n}}\n"
    report = split.e3((changed_file(before, before.replace("public sealed partial class Old",
                      "internal static partial class Support"), "Support.cs"),), "Support")
    assert not report["passed"]
    assert report["counts"]["class_declaration"] == 0


@pytest.mark.parametrize("qualified,passed", [("N.Support", True), ("global::N.Support", True),
    ("Evil.Support", False), ("Support", False)])
def test_static_using_requires_namespace_from_the_support_source(qualified, passed):
    body = "namespace N;\ninternal static partial class Support\n{\n}\n"
    support = changed_file(body, body + "\n", "Support.cs")
    directive = changed_file("namespace N;\n", f"global using static {qualified};\nnamespace N;\n", "Usings.cs")
    assert split.e3((support, directive), "Support")["passed"] == passed


@pytest.mark.parametrize("before,after,code", [("Display:\nN.Old.M\n", "N.New.M\n", 1),
    ("Tests:\nDisplay:\nN.Old.M\n", "Tests:\nN.New.M\n", 1),
    ("Tests:\nN.Old.M\n", "Tests:\nN.New.M\n", 0)])
def test_e1_header_count_mismatch_is_a_cli_difference(capsys, before, after, code):
    with tempfile.TemporaryDirectory(prefix="split-headers-") as directory:
        left, right = Path(directory) / "before.txt", Path(directory) / "after.txt"
        left.write_text(before, encoding="utf-8")
        right.write_text(after, encoding="utf-8")
        assert split.main(["e1", "--before", str(left), "--after", str(right)]) == code
    report = json.loads(capsys.readouterr().out)
    assert report["skipped_headers"]["before"] == before.splitlines()[:-1]


def test_directory_rename_requires_explicit_opt_in():
    body = split.source_lines("namespace N;\npublic sealed partial class Old\n{\n}\n")
    file = split.FileDiff("R", "Old.cs", "Sub/Old.cs", body, body, ())
    assert not split.e3((file,))["passed"]
    assert split.e3((file,), allow_directory_move=True)["passed"]


def test_class_rename_into_an_existing_baseline_class_is_unclassified():
    topic, root = split_pair()
    topic = split.FileDiff(topic.status, topic.old_path, topic.new_path, topic.before,
                           topic.after, topic.lines, frozenset({"New"}))
    report = split.e3((topic, root))
    assert not report["passed"]
    assert report["counts"]["class_declaration"] == 0


def test_two_source_roots_receiving_attributes_are_unclassified():
    report = split.e3((*split_pair(), *split_pair("Other", "Another")))
    assert not report["passed"]
    assert report["counts"]["collection_attribute"] == 0


@pytest.mark.parametrize("body,name,kind", MEMBER_BODIES)
@pytest.mark.parametrize("prefix", ["", "\n", "    // Preserve this context.\n", "\n    /// <summary>Shared member.</summary>\n"])
def test_whole_member_moves_and_their_prefixes_are_reported(body, name, kind, prefix):
    old, new = member_move(body, prefix=prefix)
    report = split.e3((old, new), "Support")
    assert report["passed"], report
    assert report["counts"][kind] == 2 * (len(body.splitlines()) + len(prefix.splitlines()))
    assert report["counts"]["support_file_skeleton"] == 5
    assert report["moves"] == [{"kind": kind, "name": name, "old_file": "Old.cs", "new_file": "Support.cs"}]


@pytest.mark.parametrize("body,name,kind", [case for case in MEMBER_BODIES if case[2] == "support_type_move"])
@pytest.mark.parametrize("prefix", ["", "    // Type context.\n", "\n"])
def test_nested_types_can_move_to_top_level_by_exactly_four_spaces(body, name, kind, prefix):
    old, new = member_move(body, top=True, prefix=prefix, support_path="Support.Doubles.cs")
    report = split.e3((old, new), "Support")
    assert report["passed"], report
    assert report["moves"][0]["name"] == name
    assert report["moves"][0]["new_file"] == "Support.Doubles.cs"


@pytest.mark.parametrize("body,name,kind", MEMBER_BODIES)
@pytest.mark.parametrize("mutation", ["name", "type", "value"])
def test_one_token_changes_in_a_moved_member_fail(body, name, kind, mutation):
    moved = body.replace("private", "internal", 1)
    if mutation == "name":
        moved = moved.replace(name, name + "Other", 1)
    elif mutation == "type":
        moved = moved.replace("string", "object", 1) if "string" in moved else moved.replace("int", "long", 1)
        if moved == body.replace("private", "internal", 1):
            moved = moved.replace("enum", "class", 1)
    else:
        if '"abc"' in moved:
            moved = moved.replace('"abc"', '"abd"', 1)
        elif "1" in moved:
            moved = moved.replace("1", "2", 1)
        elif "Second" in moved:
            moved = moved.replace("Second", "Third", 1)
        else:
            moved = moved.replace(name, name + "Other", 1)
    old, new = member_move(body, new_body=moved)
    report = split.e3((old, new), "Support")
    assert not report["passed"]
    assert report["moves"] == []
    assert report["counts"][kind] == 0


@pytest.mark.parametrize("body,name,kind", MEMBER_BODIES)
@pytest.mark.parametrize("missing", ["addition", "removal"])
def test_unpaired_member_additions_and_removals_fail(body, name, kind, missing):
    old, new = member_move(body)
    report = split.e3((old,) if missing == "addition" else (new,), "Support")
    assert not report["passed"]
    assert report["moves"] == []


@pytest.mark.parametrize("path", ["Other.cs", "Support.txt", "Other.Support.cs", "Support.Topic.Extra.cs"])
def test_type_moves_require_the_support_file_name(path):
    old, new = member_move(MEMBER_BODIES[5][0], top=True, support_path=path)
    report = split.e3((old, new), "Support")
    assert not report["passed"]
    assert report["counts"]["support_type_move"] == 0


@pytest.mark.parametrize("indent", ["", "  ", "        "])
def test_top_level_type_indentation_must_be_uniform(indent):
    body = MEMBER_BODIES[5][0]
    old, new = member_move(body, top=True)
    after = "\n".join(indent + line if "public int" in line else line for line in new.after) + "\n"
    # The valid top-level member has four spaces; all mutations differ on one line.
    if indent == "":
        after = after.replace("    public int", "public int")
    new = replace(changed_file("", after, "Support.cs"), status="A", before=())
    report = split.e3((old, new), "Support")
    assert not report["passed"]
    assert report["moves"] == []


def test_nested_type_accessibility_changes_in_place_are_unclassified():
    body = MEMBER_BODIES[5][0]
    source = "namespace N;\ninternal static class Support\n{\n" + body + "}\n"
    report = split.e3((changed_file(source, source.replace("private", "internal", 1), "Support.cs"),), "Support")
    assert not report["passed"]
    assert report["counts"]["support_type_move"] == 0
    assert report["counts"]["support_accessibility"] == 0


@pytest.mark.parametrize("extra", ["    internal static int Unmatched() => 1;\n",
    "    // Unmatched context.\n", "/// <summary>Extra summary.</summary>\n",
    "    [Obsolete]\n", "namespace Other;\n"])
def test_support_skeleton_cannot_justify_unmatched_lines(extra):
    old, new = member_move(MEMBER_BODIES[0][0])
    after = "\n".join(new.after[:-1]) + "\n" + extra + "}\n"
    new = replace(changed_file("", after, "Support.cs"), status="A", before=())
    report = split.e3((old, new), "Support")
    assert not report["passed"]


def test_changed_comment_above_a_moved_member_is_not_accepted():
    old, new = member_move(MEMBER_BODIES[0][0], prefix="    // Original comment.\n")
    after = "\n".join(new.after).replace("Original comment", "Changed comment") + "\n"
    new = replace(changed_file("", after, "Support.cs"), status="A", before=())
    report = split.e3((old, new), "Support")
    assert not report["passed"]
    assert sum("comment." in line["text"] for line in report["unclassified"]) == 2


@pytest.mark.parametrize("case", SCHEDULED_SPLITS, ids=lambda case: case["name"])
def test_scheduled_split_shapes_pass_through_the_cli_with_pinned_counts(case, monkeypatch, capsys):
    monkeypatch.setattr(split, "read_diff", lambda *args: load_case(case))
    monkeypatch.setattr(Git, "commit", lambda self, value: "a" * 40)
    assert split.main(["e3", "--base", "base", "--head", "head", "--project", "tests",
                       "--support-class", case["support_class"]]) == 0
    report = json.loads(capsys.readouterr().out)
    assert report["counts"] == {**case["counts"], "support_partial_conversion": 0}
    assert report["changed_paths"] == len(case["files"])
    assert report["unclassified"] == report["collection_consistency"] == []
    assert {move["name"] for move in report["moves"]} == (
        {"Hash", "Values", "Read", "Result"} if case["name"].startswith("profile") else
        {"Hash", "Value", "Read", "MemoryStore", "Repository"})


def test_nested_type_move_with_a_changed_member_fails():
    old, new = member_move(MEMBER_BODIES[5][0], top=True)
    after = "\n".join(new.after).replace("public int Value => value;", "public int Value => 0;") + "\n"
    new = replace(changed_file("", after, "Support.cs"), status="A", before=())
    report = split.e3((old, new), "Support")
    assert not report["passed"]
    assert report["moves"] == []


def test_directory_move_opt_in_does_not_waive_other_differences(monkeypatch, capsys):
    file = changed_file("public sealed partial class Old\n{\n}\n", "public sealed partial class Old\n{\n    int Value;\n}\n")
    file = replace(file, status="R", new_path="Sub/Tests.cs")
    monkeypatch.setattr(split, "read_diff", lambda *args: (file,))
    monkeypatch.setattr(Git, "commit", lambda self, value: "a" * 40)
    assert split.main(["e3", "--base", "base", "--head", "head", "--project", "tests",
                       "--allow-directory-move"]) == 1
    assert json.loads(capsys.readouterr().out)["unclassified"][0]["text"] == "    int Value;"


def test_nested_static_type_can_move_to_the_support_file_top_level():
    body = "    private static class Double\n    {\n        internal static int Value => 1;\n    }\n"
    report = split.e3(member_move(body, top=True), "Support")
    assert report["passed"]
    assert report["moves"][0]["name"] == "Double"


def test_support_file_using_inside_a_class_is_unclassified():
    file = replace(changed_file("", "namespace N;\ninternal static class Support\n{\nusing Evil;\n}\n", "Support.cs"), status="A")
    report = split.e3((file,), "Support")
    assert not report["passed"]
    assert report["unclassified"][0]["text"] == "using Evil;"


@pytest.mark.parametrize("separator", ["", "\n", "    // Context.\n", "    /// <summary>Context.</summary>\n"])
def test_annotated_member_moves_cannot_leave_the_attribute_behind(separator):
    body = MEMBER_BODIES[-1][0]
    old, new = member_move(body, prefix=separator)
    before = "\n".join(old.before).replace("{\n" + separator + body, "{\n    [Obsolete]\n" + separator + body) + "\n"
    after = "namespace N;\npublic sealed partial class Old\n{\n    [Obsolete]\n}\n"
    old = changed_file(before, after, "Old.cs")
    report = split.e3((old, new), "Support")
    assert not report["passed"]
    assert report["moves"] == []


def test_member_moves_cannot_change_namespace():
    old, new = member_move(MEMBER_BODIES[0][0])
    after = "\n".join(new.after).replace("namespace N;", "namespace Other;") + "\n"
    new = replace(changed_file("", after, "Support.cs"), status="A", before=())
    report = split.e3((old, new), "Support")
    assert not report["passed"]
    assert report["moves"] == []


@pytest.mark.parametrize("source_collection,static_using,passed", [
    ('[Collection("Shared")]\n', "N.Support", True),
    ('[Collection("Legacy")]\n', "N.Support", False),
    ('[Collection("Shared")]\n', "Evil.Support", False),
])
def test_git_reads_unchanged_source_root_and_support_namespace_at_head(source_collection, static_using, passed):
    before = "namespace N;\npublic sealed partial class Old\n{\n}\n"
    after = before.replace("public sealed partial class Old", '[Collection("Shared")]\npublic sealed partial class New')
    topic = changed_file(before, after, "New.cs")
    using_source = "global using static " + static_using + ";\n"
    using_file = changed_file("", using_source, "Usings.cs")
    root = "namespace N;\n" + source_collection + "public sealed partial class Old\n{\n}\n"
    support = "namespace N;\ninternal static partial class Support\n{\n}\n"
    class SourcesGit(Git):
        def commit(self, value):
            return value
        def merge_base(self, first, second):
            return first
        def run(self, *arguments):
            if arguments[0] == "ls-tree":
                return b"New.cs\0Old.cs\0Support.cs\0Usings.cs\0" if "-r" in arguments else b"project\0"
            if "--name-status" in arguments:
                return b"M\0New.cs\0A\0Usings.cs\0"
            selected = topic if arguments[-1] == "New.cs" else using_file
            # Reuse the real zero-context patch framing, including hunk identities.
            patch = "\n".join(difflib.unified_diff(selected.before, selected.after, n=0, lineterm=""))
            return ("diff --git a/" + selected.old_path + " b/" + selected.new_path + "\n" + patch).encode()
        def blob(self, revision, path):
            sources = {"New.cs": before if revision == "base" else after, "Old.cs": root,
                       "Support.cs": support, "Usings.cs": using_source}
            return "a" * 40, sources[path].encode()
    report = split.e3(split.read_diff(SourcesGit(ROOT), "base", "head", "project"), "Support")
    assert report["passed"] == passed
    if source_collection.startswith('[Collection("Legacy")'):
        assert report["collection_consistency"]


def test_git_checks_all_baseline_class_names_even_without_a_summary():
    before = "namespace N;\npublic sealed partial class Old\n{\n}\n"
    after = before.replace("Old", "New")
    class CollisionGit(Git):
        def commit(self, value):
            return value
        def merge_base(self, first, second):
            return first
        def run(self, *arguments):
            if arguments[0] == "ls-tree":
                return b"Topic.cs\0Existing.cs\0" if "-r" in arguments else b"project\0"
            if "--name-status" in arguments:
                return b"M\0Topic.cs\0"
            return b"diff --git a/Topic.cs b/Topic.cs\n@@ -2 +2 @@\n-public sealed partial class Old\n+public sealed partial class New\n"
        def blob(self, revision, path):
            return "a" * 40, ("namespace N;\npublic class New {}\n" if path == "Existing.cs" else
                              before if revision == "base" else after).encode()
    report = split.e3(split.read_diff(CollisionGit(ROOT), "base", "head", "project"))
    assert not report["passed"]
    assert report["counts"]["class_declaration"] == 0


def test_two_source_namespaces_are_not_one_source_class():
    body = "namespace N;\npublic sealed partial class Old\n{\n}\n"
    first = changed_file(body, body.replace("class Old", "class First"), "First.cs")
    second = changed_file(body.replace("namespace N", "namespace Other"),
                          body.replace("namespace N", "namespace Other").replace("class Old", "class Second"), "Second.cs")
    report = split.e3((first, second))
    assert not report["passed"]
    assert report["counts"]["class_declaration"] == 0


@pytest.mark.parametrize("mutation", [False, True])
@pytest.mark.parametrize("body", [
    "    private sealed class Fake\n    {\n        public int Value => 1;\n    }\n",
    "    private static int Read()\n    {\n        return 1;\n    }\n",
])
def test_reanchored_closing_brace_requires_an_exact_member_body(mutation, body):
    preceding = "    public void Check()\n    {\n        Assert.True(true);\n    }\n"
    source = "namespace N;\npublic sealed partial class Old\n{\n" + preceding
    old = changed_file(source + "\n" + body + "}\n", source + "}\n", "Old.cs")
    # An equally valid zero-context alignment reuses the member's brace for Check.
    patch = "@@ -7,5 +6,0 @@\n" + "\n".join("-" + line for line in old.before[6:11]) + "\n"
    assert old.before[:6] + old.before[11:] == old.after
    old = replace(old, lines=split.patch_lines(patch))
    _, new = member_move(body, prefix="\n")
    if mutation:
        after = "\n".join(new.after).replace("=> 1;", "=> 2;").replace("return 1;", "return 2;") + "\n"
        new = replace(changed_file("", after, "Support.cs"), status="A")
    report = split.e3((old, new), "Support")
    assert report["passed"] == (not mutation)
    assert bool(report["moves"]) == (not mutation)


def test_helpers_in_another_top_level_type_cannot_gain_support_accessibility():
    before = ("namespace N;\ninternal static class Support\n{\n}\n"
              "internal static class Double\n{\n    private static int Read() => 1;\n}\n")
    file = changed_file(before, before.replace("private", "internal"), "Support.cs")
    report = split.e3((file,), "Support")
    assert not report["passed"]
    assert report["counts"]["support_accessibility"] == 0


def test_new_support_skeleton_requires_the_explicit_support_class():
    report = split.e3(member_move(MEMBER_BODIES[0][0]))
    assert not report["passed"]
    assert report["counts"]["support_file_skeleton"] == 0

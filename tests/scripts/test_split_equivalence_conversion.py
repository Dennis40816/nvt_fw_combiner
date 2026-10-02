"""Helper-only partial conversions preserve every body and fail closed."""

from __future__ import annotations

import json
import os
from dataclasses import replace

import pytest

from split_equivalence_test_support import changed_file, require_split_commits, split, split_pair, text


BODY = (
    '    private const string Digest =\n        "private";\n'
    "    private static int Read() => 1;\n"
    "    private sealed class Fake\n    {\n        private int _value;\n    }\n"
)


def converted_file(body=BODY, *, target="internal static partial class Support", path="Old.TestDoubles.cs"):
    before = "namespace N;\npublic sealed partial class Old\n{\n" + body + "}\n"
    after = before.replace("public sealed partial class Old", target)
    after = after.replace("    private ", "    internal ")
    # Only direct declarations change; nested instance members stay byte-identical.
    after = after.replace("        internal ", "        private ")
    return changed_file(before, after, path)


def conversion_report(file, *, support="Support", context=()):
    return split.e3((*split_pair(attribute="")[:1], file, *context), support)


def test_helper_only_partial_converts_in_place_with_static_const_and_nested_members():
    file = converted_file()
    report = conversion_report(file)
    assert report["passed"]
    assert report["counts"]["support_partial_conversion"] == 2
    assert report["counts"]["support_accessibility"] == 6
    assert report["counts"]["class_declaration"] == 2
    assert report["unclassified"] == []


def test_conversion_without_accessibility_changes_still_requires_every_member_to_be_static():
    body = "\n".join(line.replace("private", "internal", 1) if line.startswith("    private ") else line
                     for line in BODY.split("\n"))
    report = conversion_report(converted_file(body))
    assert report["passed"]
    assert report["counts"]["support_partial_conversion"] == 2
    assert report["counts"]["support_accessibility"] == 0


@pytest.mark.parametrize("mutation", ["using", "blank", "comment", "nested-body", "extra-class"])
def test_other_edits_in_a_converted_file_remain_unclassified(mutation):
    file = converted_file()
    before, after = "\n".join(file.before) + "\n", "\n".join(file.after) + "\n"
    if mutation == "using":
        after = "using System;\n" + after
    elif mutation == "blank":
        after = after.replace("{\n", "{\n\n", 1)
    elif mutation == "comment":
        after = after.replace("namespace N;", "namespace N; // edited")
    elif mutation == "nested-body":
        after = after.replace("private int _value;", "private int _different;")
    else:
        after += "public sealed class Other\n{\n}\n"
    report = conversion_report(changed_file(before, after, file.old_path))
    assert not report["passed"]
    assert report["counts"]["support_partial_conversion"] == 0


@pytest.mark.parametrize("attribute", ["Fact", "Theory", "AvaloniaFact", "N.CustomTheoryAttribute"])
def test_converted_partial_with_a_test_attribute_is_rejected(attribute):
    report = conversion_report(converted_file(f"    [{attribute}]\n    private static void Check() {{ }}\n"))
    assert not report["passed"]
    assert report["counts"].get("support_partial_conversion", 0) == 0


@pytest.mark.parametrize("mutation", ["body", "public", "other-class", "non-static", "non-partial"])
def test_non_mechanical_conversion_is_rejected(mutation):
    file = converted_file()
    after = "\n".join(file.after) + "\n"
    if mutation == "body":
        after = after.replace("=> 1;", "=> 2;")
    elif mutation == "public":
        after = after.replace("internal static int Read", "public static int Read")
    elif mutation == "other-class":
        after = after.replace("class Support", "class Other")
    elif mutation == "non-static":
        after = after.replace("internal static partial class", "internal partial class")
    else:
        after = after.replace("static partial class", "static class")
    file = changed_file("\n".join(file.before) + "\n", after, file.old_path)
    report = conversion_report(file)
    assert not report["passed"]
    assert report["counts"].get("support_partial_conversion", 0) == 0
    assert report["unclassified"]


@pytest.mark.parametrize("member", [
    "    private int _instance;\n", "    int _implicitInstance;\n",
    "    internal int _unchangedInstance;\n",
    "    private int Value => 1;\n", "    private void Run() { }\n",
    "    public Old() { }\n", "    [Unknown]\n    private static int Read() => 1;\n",
    "#if MAYBE\n    private static int Read() => 1;\n#endif\n",
])
def test_unproven_static_members_prevent_conversion(member):
    report = conversion_report(converted_file(BODY + member))
    assert not report["passed"]
    assert report["counts"].get("support_partial_conversion", 0) == 0


@pytest.mark.parametrize("context", ["head_sources", "binding_sources"])
def test_every_support_declaration_must_be_partial_including_unchanged_sources(context):
    file = replace(converted_file(), **{context: (("OddName.cs", split.source_lines(
        "namespace N;\ninternal static class Support\n{\n}\n")),)})
    report = conversion_report(file)
    assert not report["passed"]
    assert report["counts"].get("support_partial_conversion", 0) == 0


@pytest.mark.parametrize("variant", ["renamed", "added", "non-partial-base", "wrong-source", "no-support-option"])
def test_conversion_requires_same_existing_path_source_partial_and_explicit_support(variant):
    file = converted_file()
    support = "Support"
    if variant == "renamed":
        file = replace(file, status="R", new_path="Different.cs")
    elif variant == "added":
        file = replace(changed_file("", "\n".join(file.after) + "\n", file.old_path), status="A")
    elif variant in {"non-partial-base", "wrong-source"}:
        before = "\n".join(file.before) + "\n"
        before = before.replace("partial class Old", "class Old" if variant == "non-partial-base" else "partial class Other")
        file = changed_file(before, "\n".join(file.after) + "\n", file.old_path)
    else:
        support = None
    report = conversion_report(file, support=support)
    assert not report["passed"]
    assert report["counts"].get("support_partial_conversion", 0) == 0


def test_real_second_split_classifies_conversions_and_retains_the_state_collision(monkeypatch, capsys):
    monkeypatch.setenv("GIT_CONFIG_GLOBAL", os.devnull)
    monkeypatch.setenv("GIT_CONFIG_NOSYSTEM", "1")
    expected = json.loads(text("version-management-real.json"))
    require_split_commits(expected)
    assert split.main(["e3", "--base", expected["base"], "--head", expected["head"],
                       "--project", expected["project"], "--support-class", expected["support_class"]]) == expected["exit_code"]
    report = json.loads(capsys.readouterr().out)
    for key in ("base", "head", "project", "support_class", "changed_paths", "counts"):
        assert report[key] == expected[key]
    classified = sum(value for kind, value in report["counts"].items() if kind != "rename")
    assert classified + len(report["unclassified"]) == expected["changed_lines"]
    assert report["unclassified"] == expected["unclassified"]
    assert report["collection_consistency"] == report["moves_from_unrelated_classes"] == []

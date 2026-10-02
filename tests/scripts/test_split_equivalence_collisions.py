"""Name binding regressions for both helper moves and in-place conversions."""

from __future__ import annotations

from dataclasses import replace

import pytest

from split_equivalence_test_support import (
    added_collection_definition, changed_file, member_move, split, split_pair,
)
from test_split_equivalence_conversion import converted_file, conversion_report
from test_split_equivalence_followup import read_named_pair


def context_file(member, *, nested=False, name="Old"):
    if nested:
        member = "    private sealed class Nested\n    {\n" + "\n".join(
            "    " + line for line in member.splitlines()) + "\n    }\n"
    source = f"namespace N;\npublic sealed partial class {name}\n{{\n{member}}}\n"
    return changed_file(source, source + "\n", name + ".cs")


def test_converted_overload_cannot_rebind_a_root_invocation():
    converted = converted_file("    private static int Read(int value) => 1;\n")
    root = context_file("    private static int Read(long value) => 2;\n"
                        "    public int Check() => Read(1);\n", name="Old")
    report = conversion_report(converted, context=(root,))
    assert not report["passed"]
    assert report["counts"]["support_partial_conversion"] == 0
    assert report["counts"]["support_accessibility"] == 0
    assert len(report["collisions"]) == 1
    collision = report["collisions"][0]
    assert (collision["name"], collision["declaring_type"], collision["line"]) == ("Read", "Old", 4)
    assert "Read" in collision["reason"] and "Old" in collision["reason"] and "line 4" in collision["reason"]
    assert all(item["collision_ids"] == [collision["id"]]
               for item in report["unclassified"] if item["file"] == converted.new_path)


def test_honest_converted_method_keeps_the_root_invocation_without_a_collision():
    converted = converted_file("    private static int Read(int value) => 1;\n")
    report = conversion_report(converted, context=(context_file("    public int Check() => Read(1);\n"),))
    assert report["passed"]
    assert report["counts"]["support_partial_conversion"] == 2
    assert report["counts"]["support_accessibility"] == 2
    assert report["collisions"] == report["unclassified"] == []


@pytest.mark.parametrize("body,name", [
    ("    internal static int Read(int value) => 1;\n", "Read"),
    ("    private const int Value = 1;\n", "Value"),
    ("    private sealed class Fake\n    {\n    }\n", "Fake"),
])
def test_conversion_checks_every_direct_member_even_without_an_access_edit(body, name):
    report = conversion_report(converted_file(body), context=(context_file(
        f"    public static int {name} = 2;\n"),))
    assert not report["passed"]
    assert report["counts"]["support_partial_conversion"] == 0
    assert report["collisions"][0]["name"] == name


@pytest.mark.parametrize("path", ["move", "convert"])
@pytest.mark.parametrize("kind", ["class", "struct", "record", "record struct", "interface", "enum", "delegate"])
@pytest.mark.parametrize("nested", [False, True])
def test_top_level_types_block_but_unrelated_nested_types_do_not(path, kind, nested):
    body = "    private sealed class Fake\n    {\n    }\n"
    declaration = ("public delegate int Fake();\n" if kind == "delegate" else
                   f"public {kind} Fake\n{{\n}}\n")
    if nested:
        declaration = "public class Other\n{\n" + "\n".join(
            "    " + line for line in declaration.splitlines()) + "\n}\n"
    source = split.source_lines("namespace N;\n" + declaration)
    if path == "convert":
        file = replace(converted_file(body), binding_sources=(("Sibling.cs", source),))
        report = conversion_report(file)
    else:
        old, support = member_move(body)
        old = replace(old, binding_sources=(("Sibling.cs", source),))
        report = split.e3((old, support), "Support")
    assert report["passed"] == nested
    assert bool(report["collisions"]) == (not nested)
    if not nested:
        assert report["collisions"][0]["rule"] == "a"


def test_git_context_collects_unrelated_top_level_types():
    files = read_named_pair(context={"Sibling.cs": "namespace N;\npublic class Fake\n{\n}\n"})
    assert any(path == "Sibling.cs" for file in files for path, _ in file.project_sources)


@pytest.mark.parametrize("definition", ["class Data\n{\n}", "delegate int Data();"])
def test_git_head_dependency_type_evidence_proves_only_non_delegate_nested_storage(definition):
    source = ("using External;\nnamespace N;\npublic sealed partial class Old\n{\n"
              "    private sealed class Store : IStore\n    {\n        public Data Read;\n    }\n}\n")
    dependency = "namespace External;\npublic " + definition + "\npublic interface IStore\n{\n}\n"
    files = read_named_pair(context={"OddName.cs": source}, dependencies={"src/Data.cs": dependency})
    report = split.e3(files, "Support")
    assert report["passed"] == definition.startswith("class")
    assert bool(report["collisions"]) == definition.startswith("delegate")


@pytest.mark.parametrize("moved,remaining,allowed", [
    ("    private static int Read(int value) => 1;\n", "    public int Read { get; }\n", True),
    ("    private static int Read(int value) => 1;\n", "    public int Read;\n", True),
    ("    private static int Read(int value) => 1;\n", "    public const int Read = 2;\n", True),
    ("    private const int Read = 1;\n", "    public int Read(long value) => 2;\n", True),
    ("    private static int Read(int value) => 1;\n", "    public int Read(long value) => 2;\n", False),
    ("    private static int Read(int value) => 1;\n", "    public Func<int, int> Read { get; }\n", False),
    ("    private static int Read(int value) => 1;\n", "    public Action<int> Read;\n", False),
    ("    private static int Read(int value) => 1;\n", "    public Mystery Read;\n", False),
    ("    private const int Read = 1;\n", "    public int Read;\n", False),
    ("    private static readonly int Read = 1;\n", "    public int Read(long value) => 2;\n", False),
    ("    private static readonly int Read = External.@const;\n", "    public int Read(long value) => 2;\n", False),
    ("    private static int Read(int value) => 1;\n", "    public int Read; public int Read(long value) => 2;\n", False),
])
@pytest.mark.parametrize("nested", [False, True])
def test_only_proven_nested_non_invocable_collisions_are_relaxed(moved, remaining, allowed, nested):
    old, support = member_move(moved)
    report = split.e3((old, support, context_file(remaining, nested=nested)), "Support")
    assert report["passed"] == (allowed and nested)
    assert bool(report["collisions"]) == (not (allowed and nested))


@pytest.mark.parametrize("declaration", [
    "    public Reader Read;\n", "    public Reader Read { get; }\n",
    "    public Alias Read;\n", "    public dynamic Read;\n",
])
def test_custom_delegate_alias_or_unknown_nested_member_still_blocks(declaration):
    old, support = member_move("    private static int Read(int value) => 1;\n")
    extra = "namespace N;\npublic delegate int Reader(int value);\n"
    old = replace(old, binding_sources=(("Delegate.cs", split.source_lines(extra)),))
    report = split.e3((old, support, context_file(declaration, nested=True)), "Support")
    assert not report["passed"]
    assert report["collisions"][0]["declaring_type"] == "Old.Nested"


def test_a_global_alias_cannot_turn_delegate_storage_into_a_proven_non_invocable_member():
    old, support = member_move("    private static int Read(int value) => 1;\n")
    sources = (("Aliases.cs", split.source_lines("global using Data = System.Func<int, int>;\n")),
               ("Data.cs", split.source_lines("namespace N;\npublic class Data\n{\n}\n")))
    old = replace(old, project_sources=sources)
    report = split.e3((old, support, context_file("    public Data Read;\n", nested=True)), "Support")
    assert not report["passed"]
    assert report["collisions"][0]["kind"] == "unknown"


@pytest.mark.parametrize("imported_kind", ["missing", "delegate"])
def test_an_unrelated_namespace_class_cannot_prove_an_imported_type_non_delegate(imported_kind):
    old, support = member_move("    private static int Read(int value) => 1;\n")
    sources = [("Data.cs", split.source_lines("namespace Other;\npublic class Data\n{\n}\n"))]
    if imported_kind == "delegate":
        sources.append(("Delegate.cs", split.source_lines("namespace External;\npublic delegate int Data(int value);\n")))
    old = replace(old, project_sources=tuple(sources))
    nested = context_file("    public Data Read;\n", nested=True)
    nested = replace(nested, before=("using External;", *nested.before), after=("using External;", *nested.after),
                     lines=tuple(replace(line, number=line.number + 1) for line in nested.lines))
    report = split.e3((old, support, nested), "Support")
    assert not report["passed"]
    assert report["collisions"][0]["kind"] in {"unknown", "delegate"}


@pytest.mark.parametrize("directive", ["using External;", "global using External;"])
def test_only_a_visible_class_definition_proves_a_custom_nested_property_non_invocable(directive):
    old, support = member_move("    private static int Read(int value) => 1;\n")
    source = split.source_lines("namespace External;\npublic class Data\n{\n}\n")
    old = replace(old, project_sources=(("Data.cs", source),))
    nested = context_file("    public Data Read;\n", nested=True)
    nested = replace(nested, before=(directive, *nested.before), after=(directive, *nested.after),
                     lines=tuple(replace(line, number=line.number + 1) for line in nested.lines))
    assert split.e3((old, support, nested), "Support")["passed"]


def test_unknown_base_type_lookup_cannot_prove_an_imported_property_non_delegate():
    old, support = member_move("    private static int Read(int value) => 1;\n")
    old = replace(old, project_sources=(("Data.cs", split.source_lines("namespace N;\npublic class Data\n{\n}\n")),))
    nested = context_file("    public Data Read;\n", nested=True)
    before = "\n".join(nested.before).replace("class Nested", "class Nested : ExternalBase") + "\n"
    nested = changed_file(before, before + "\n", "Old.Other.cs")
    report = split.e3((old, support, nested), "Support")
    assert not report["passed"]
    assert report["collisions"][0]["kind"] == "unknown"


@pytest.mark.parametrize("base", ["External.IStore", "global::External.IStore"])
def test_qualified_unknown_base_cannot_borrow_an_unrelated_interface_fact(base):
    old, support = member_move("    private static int Read(int value) => 1;\n")
    source = "namespace N;\npublic class Data\n{\n}\npublic interface IStore\n{\n}\n"
    old = replace(old, project_sources=(("Data.cs", split.source_lines(source)),))
    nested = context_file("    public Data Read;\n", nested=True)
    before = "\n".join(nested.before).replace("class Nested", "class Nested : " + base) + "\n"
    report = split.e3((old, support, changed_file(before, before + "\n", "Old.Other.cs")), "Support")
    assert not report["passed"]
    assert report["collisions"][0]["kind"] == "unknown"


@pytest.mark.parametrize("directive", ["using", "global using"])
def test_base_alias_cannot_borrow_an_unrelated_interface_fact(directive):
    old, support = member_move("    private static int Read(int value) => 1;\n")
    source = "namespace N;\npublic class Data\n{\n}\npublic interface IStore\n{\n}\n"
    old = replace(old, project_sources=(("Data.cs", split.source_lines(source)),))
    nested = context_file("    public Data Read;\n", nested=True)
    before = directive + " IStore = External.BaseClass;\n" + "\n".join(nested.before).replace(
        "class Nested", "class Nested : IStore") + "\n"
    report = split.e3((old, support, changed_file(before, before + "\n", "Old.Other.cs")), "Support")
    assert not report["passed"]
    assert report["collisions"][0]["kind"] == "unknown"


def test_generic_parameters_do_not_inherit_an_unrelated_non_delegate_type_fact():
    old, support = member_move("    private static int Read(int value) => 1;\n")
    nested = context_file("    public Data Read;\n", nested=True)
    before = "\n".join(nested.before).replace("class Nested", "class Nested<Data>") + "\n"
    nested = changed_file(before, before + "\n", "Old.Generic.cs")
    old = replace(old, project_sources=(("Data.cs", split.source_lines("namespace N;\npublic class Data\n{\n}\n")),))
    report = split.e3((old, support, nested), "Support")
    assert not report["passed"]
    assert report["collisions"][0]["kind"] == "unknown"


def test_an_oddly_named_support_partial_keeps_the_old_member_collision_rejection():
    old, support = member_move("    private static int Read(int value) => 1;\n")
    source = "namespace N;\ninternal static partial class Support\n{\n    internal static int Read(long value) => 2;\n}\n"
    old = replace(old, binding_sources=(("OddName.cs", split.source_lines(source)),))
    report = split.e3((old, support), "Support")
    assert not report["passed"]
    assert report["collisions"][0]["file"] == "OddName.cs"


def test_collision_summary_deduplicates_lines_and_overloads_but_keeps_each_declaration():
    body = "    private static int Read(int value) => 1;\n    private static int Read(short value) => 1;\n"
    old, support = member_move(body)
    support = replace(support, after=tuple(line.replace("private static", "internal static")
                                          for line in support.after))
    support = replace(changed_file("", "\n".join(support.after) + "\n", "Support.cs"), status="A")
    report = split.e3((old, support, context_file(
        "    public int Read(long value) => 2;\n    public int Read(double value) => 3;\n")), "Support")
    assert not report["passed"]
    assert [(item["name"], item["line"]) for item in report["collisions"]] == [("Read", 4), ("Read", 5)]
    assert report["counts"]["helper_move"] == 0
    assert all(item["collision_ids"] == [0, 1] for item in report["unclassified"]
               if item["file"] in {"Old.cs", "Support.cs"})


@pytest.mark.parametrize("attribute", ['[Collection("Shared")]', '[Collection(nameof(Shared))]'])
@pytest.mark.parametrize("declaration", ["public sealed class", "public class", "internal class"])
@pytest.mark.parametrize("comment", ["", " // note", "\n/* note */", "\n/// <summary>Sibling.</summary>"])
def test_new_collection_definition_cannot_capture_an_implicit_sibling_collection(attribute, declaration, comment):
    topic, root = split_pair()
    sibling = split.source_lines(f"namespace N;\n{attribute}{comment}\n{declaration} Unrelated\n{{\n}}\n")
    topic = replace(topic, base_sources=(("Unrelated.cs", sibling),))
    report = split.e3((topic, root, added_collection_definition()))
    assert not report["passed"]
    assert report["counts"]["collection_definition"] == 0
    assert any(item.get("reason") == "new definition captures a base collection outside the split"
               for item in report["collection_consistency"])


@pytest.mark.parametrize("attribute", [
    '[Trait("Category", "Other"), Collection("Shared")]',
    '[Collection("Shared"), Trait("Category", "Other")]',
    '[global::Xunit.CollectionAttribute(nameof(Shared))]',
])
@pytest.mark.parametrize("declaration", ["public class", "public record", "public record class"])
def test_grouped_qualified_and_record_class_memberships_also_block_new_definitions(attribute, declaration):
    topic, root = split_pair()
    sibling = split.source_lines(f"namespace N;\n{attribute}\n{declaration} Other\n{{\n}}\n")
    topic = replace(topic, base_sources=(("Other.cs", sibling),))
    report = split.e3((topic, root, added_collection_definition()))
    assert not report["passed"]
    assert report["counts"]["collection_definition"] == 0


@pytest.mark.parametrize("argument", [
    '"Shared" /* note */', '/* note */ "Shared"', 'nameof(Shared) /* note */',
    '"Shared" // note\n', '/* note */ nameof(Shared)',
    'SharedName', '"\\u0053hared"',
])
def test_commented_or_unclassified_base_collection_arguments_cannot_bypass_capture(argument):
    topic, root = split_pair()
    sibling = split.source_lines(f"namespace N;\n[Collection({argument})]\npublic class Other\n{{\n}}\n")
    topic = replace(topic, base_sources=(("Other.cs", sibling),))
    report = split.e3((topic, root, added_collection_definition()))
    assert not report["passed"]
    assert report["counts"]["collection_definition"] == 0

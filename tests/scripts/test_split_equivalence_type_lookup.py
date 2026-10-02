"""Commander-approved lookup boundary for moved and converted type names."""

from dataclasses import replace

import pytest

from split_equivalence_test_support import changed_file, member_move, split
from scripts.split_equivalence_bindings import TypeLookup, declarations
from test_split_equivalence_conversion import converted_file, conversion_report


BODY = "    private sealed class Fake\n    {\n    }\n"


def report_with_context(path, source, *, changed=False, before=None):
    context = (("Context.cs", split.source_lines(source)),)
    extra = (changed_file(source, source + "\n", "Context.cs"),) if changed else ()
    if path == "convert":
        file = replace(converted_file(BODY), project_sources=context)
        if before:
            file = replace(file, base_sources=(("Before.cs", split.source_lines(before)),))
        return conversion_report(file, context=extra)
    old, support = member_move(BODY)
    old = replace(old, project_sources=context)
    if before:
        old = replace(old, base_sources=(("Before.cs", split.source_lines(before)),))
    return split.e3((old, support, *extra), "Support")


@pytest.mark.parametrize("path", ["move", "convert"])
@pytest.mark.parametrize("owner,rule", [("Old", "b"), ("Support", "b"), ("Other", None)])
def test_nested_type_lookup_depends_on_its_declaring_type(path, owner, rule):
    source = f"namespace N;\npublic partial class {owner}\n{{\n" + BODY + "}\n"
    report = report_with_context(path, source)
    assert report["passed"] == (rule is None)
    assert [item["rule"] for item in report["collisions"]] == ([] if rule is None else [rule])


@pytest.mark.parametrize("path", ["move", "convert"])
@pytest.mark.parametrize("header", ["public class", "public sealed partial class"])
def test_nested_type_in_changed_unrelated_file_blocks_conservatively(path, header):
    source = f"namespace N;\n{header} Other\n{{\n" + BODY + "}\n"
    report = report_with_context(path, source, changed=True)
    assert not report["passed"]
    assert report["collisions"][0]["rule"] == "c"


def test_unrelated_declaring_type_in_the_root_file_is_rule_c():
    declaration = "public sealed partial class Other\n{\n" + BODY + "}\n"
    old, support = member_move(BODY)
    before = "\n".join(old.before) + "\n" + declaration
    after = "\n".join(old.after) + "\n" + declaration
    old = changed_file(before, after, old.old_path)
    report = split.e3((old, support), "Support")
    assert not report["passed"]
    assert report["collisions"][0]["rule"] == "c"


def test_nested_type_in_lexically_enclosing_type_blocks():
    source = ("namespace N;\npublic class Outer\n{\n" + BODY +
              "    public partial class Old\n    {\n    }\n}\n")
    lines = split.source_lines(source)
    lookup = TypeLookup(({"Context.cs": lines},), {("N", "Outer.Old")}, ())
    item = next(item for item in declarations(split.code_lines(lines)) if item.name == "Fake")
    assert lookup.rule("Context.cs", item, lines) == "b"


@pytest.mark.parametrize("path", ["move", "convert"])
def test_an_unrelated_nested_class_with_the_root_name_is_not_the_split_root(path):
    source = ("namespace N;\npublic class Outer\n{\n" + BODY +
              "    public class Old\n    {\n    }\n}\n")
    assert report_with_context(path, source)["passed"]


@pytest.mark.parametrize("path", ["move", "convert"])
@pytest.mark.parametrize("owner", ["Old", "Support"])
def test_unrelated_namespace_does_not_turn_a_namesake_into_a_split_or_support_class(path, owner):
    source = f"namespace Other;\npublic class {owner}\n{{\n" + BODY + "}\n"
    assert report_with_context(path, source)["passed"]


@pytest.mark.parametrize("path", ["move", "convert"])
def test_top_level_declaration_before_the_move_also_blocks(path):
    before = "namespace Other;\npublic class Fake\n{\n}\n"
    report = report_with_context(path, "namespace N;\n", before=before)
    assert not report["passed"]
    assert report["collisions"][0]["rule"] == "a"


@pytest.mark.parametrize("path", ["move", "convert"])
def test_resolved_base_does_not_borrow_an_unrelated_namespaces_nested_type(path):
    source = "namespace Other;\npublic class Base\n{\n" + BODY + "}\n"
    before = "namespace N;\npublic partial class Old : Base\n{\n}\npublic class Base\n{\n}\n"
    assert report_with_context(path, source, before=before)["passed"]


@pytest.mark.parametrize("path", ["move", "convert"])
@pytest.mark.parametrize("directive", ["using", "global using"])
def test_alias_to_a_project_base_cannot_hide_its_nested_name(path, directive):
    source = (f"{directive} Alias = N.Base<int>;\nnamespace N;\n"
              "public partial class Old : Alias\n{\n}\npublic class Base<T>\n{\n" + BODY + "}\n")
    report = report_with_context(path, source)
    assert not report["passed"]
    assert report["collisions"][0]["rule"] == "b"


@pytest.mark.parametrize("path", ["move", "convert"])
def test_base_name_containing_where_is_not_a_generic_constraint(path):
    source = ("namespace N;\npublic partial class Old : SomewhereBase\n{\n}\n"
              "public class SomewhereBase\n{\n" + BODY + "}\n")
    report = report_with_context(path, source)
    assert not report["passed"]
    assert report["collisions"][0]["rule"] == "b"


@pytest.mark.parametrize("path", ["move", "convert"])
@pytest.mark.parametrize("base", ["Base", "Base<int>", "global::N.Base", "External.Base<int>"])
@pytest.mark.parametrize("owner", ["Old", "Support", "New"])
def test_nested_type_in_a_named_base_list_type_blocks(path, base, owner):
    source = (f"namespace N;\npublic partial class {owner} : {base}\n{{\n}}\n"
              "public class Base<T>\n{\n" + BODY + "}\n")
    report = report_with_context(path, source)
    # New is a split class on the converted path only.
    blocked = owner != "New" or path == "convert"
    assert report["passed"] == (not blocked)
    assert [item["rule"] for item in report["collisions"]] == (["b"] if blocked else [])


@pytest.mark.parametrize("path", ["move", "convert"])
@pytest.mark.parametrize("base", ["ExternalBase", "ExternalBase<int>"])
def test_unresolved_base_alone_does_not_block_an_unrelated_nested_type(path, base):
    source = (f"namespace N;\npublic partial class Old : {base}\n{{\n}}\n"
              "public class Other\n{\n" + BODY + "}\n")
    report = report_with_context(path, source)
    assert report["passed"]
    assert report["collisions"] == []


@pytest.mark.parametrize("path", ["move", "convert"])
def test_base_list_before_conversion_or_move_also_participates(path):
    before = "namespace N;\npublic partial class Old : Base\n{\n}\n"
    source = "namespace N;\npublic class Base\n{\n" + BODY + "}\n"
    report = report_with_context(path, source, before=before)
    assert not report["passed"]
    assert report["collisions"][0]["rule"] == "b"


@pytest.mark.parametrize("path", ["move", "convert"])
@pytest.mark.parametrize("source", [
    "namespace N\n{\npublic class Other\n{\n" + BODY + "}\n}\n",
    "namespace N;\npublic class Other { private class Fake { } }\n",
    "namespace N;\npublic class Other\n{\n    public delegate\n        int Fake();\n}\n",
    "namespace N;\npublic class Other\n{\n" + BODY,
])
def test_unclassifiable_nested_declaration_blocks(path, source):
    # Block-scoped namespace identity is outside the inventory's proven subset.
    report = report_with_context(path, source)
    assert not report["passed"]
    assert report["collisions"][0]["rule"] == "d"

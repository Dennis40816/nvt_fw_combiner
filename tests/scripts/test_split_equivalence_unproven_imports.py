"""Unproven split scopes retain imports and aliases without relaxing E3."""

import pytest

from test_split_equivalence_name_guards import guarded_report


OTHER = "namespace N;\npublic class Other\n{\n    public class Fake {}\n}\n"
REAL = "namespace N;\npublic class Real {}\n"


def scoped_source(directive, owner="Old", shape="block"):
    declaration = f"public sealed partial class {owner} {{}}"
    if shape == "nested":
        return f"namespace Outer {{ namespace N {{ {directive} {declaration} }} }}\n"
    if shape == "multiline":
        return f"namespace N\n{{\n    {directive}\n    {declaration}\n}}\n"
    return f"namespace N {{ {directive} {declaration} }}\n"


def import_report(mode, source, revision):
    sources = (("Old.Extra.cs", source), ("Other.cs", OTHER), ("Real.cs", REAL))
    return guarded_report(mode, **{"project" if revision == "head" else "base": sources})


def assert_rule_d(report, declaring_type):
    assert not report["passed"]
    assert any(item["file"] == "Old.Extra.cs" and item["name"] == "Fake"
               and item["rule"] == "d" and item["declaring_type"] == declaring_type
               for item in report["collisions"])
    assert any(item.get("collision_ids") for item in report["unclassified"])


@pytest.mark.parametrize("mode", ["move", "convert"])
@pytest.mark.parametrize("revision", ["head", "base"])
@pytest.mark.parametrize("owner", ["Old", "Support"])
def test_block_scoped_split_static_import_cannot_hide_a_project_nested_type(mode, revision, owner):
    # Exact single-line Old.Extra.cs reproduction; Other.Fake is unchanged.
    source = scoped_source("using static N.Other;", owner)
    assert_rule_d(import_report(mode, source, revision), "<using static N.Other>")


@pytest.mark.parametrize("mode", ["move", "convert"])
@pytest.mark.parametrize("revision", ["head", "base"])
@pytest.mark.parametrize("shape", ["block", "multiline", "nested"])
@pytest.mark.parametrize("directive,owner", [
    ("using Fake = N.Real;", "<using alias>"),
    ("extern alias Fake;", "<extern alias>"),
    ("using static N.Other;", "<using static N.Other>"),
])
def test_unproven_split_namespace_keeps_alias_and_static_import_guards(mode, revision, shape, directive, owner):
    assert_rule_d(import_report(mode, scoped_source(directive, shape=shape), revision), owner)


@pytest.mark.parametrize("mode", ["move", "convert"])
@pytest.mark.parametrize("revision", ["head", "base"])
@pytest.mark.parametrize("directive", ["extern alias Fake;", "extern\n alias\n @Fake;"])
def test_extern_alias_named_like_a_moved_type_blocks_even_in_a_proven_scope(mode, revision, directive):
    source = directive + "\nnamespace N;\npublic sealed partial class Old {}\n"
    assert_rule_d(import_report(mode, source, revision), "<extern alias>")


@pytest.mark.parametrize("mode", ["move", "convert"])
@pytest.mark.parametrize("directive", ["", "using System;", "using Different = N.Real;", "extern alias Different;"])
def test_block_scope_without_a_relevant_import_passes_but_a_matching_alias_blocks(mode, directive):
    source = scoped_source(directive)
    report = import_report(mode, source, "head")
    assert report["passed"]
    assert report["collisions"] == report["unclassified"] == []
    # Pair the acceptance control with a rejection: this test is red without
    # the fix too, while proving an unproven namespace alone is not a blocker.
    unsafe = source.replace("public sealed", "using Fake = N.Real; public sealed")
    assert_rule_d(import_report(mode, unsafe, "head"), "<using alias>")


@pytest.mark.parametrize("revision", ["head", "base"])
def test_unproven_renamed_split_class_also_keeps_its_imports(revision):
    assert_rule_d(import_report("convert", scoped_source("using Fake = N.Real;", "New"), revision),
                  "<using alias>")


@pytest.mark.parametrize("mode", ["move", "convert"])
@pytest.mark.parametrize("owner", ["Old", "Support"])
@pytest.mark.parametrize("directive,scope", [
    ("using static N.Other;", "<using static N.Other>"),
    ("using Fake = N.Real;", "<using alias>"),
])
def test_split_class_hidden_behind_another_header_retains_imports(mode, owner, directive, scope):
    source = f"namespace N {{ public class Unrelated {{}} {directive} public partial class {owner} {{}} }}\n"
    assert_rule_d(import_report(mode, source, "head"), scope)

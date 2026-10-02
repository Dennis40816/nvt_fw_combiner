"""Delta-review regressions exercise the public E3 classification boundary."""

from dataclasses import replace

import pytest

from split_equivalence_test_support import changed_file, member_move, split, split_pair
from test_split_equivalence_conversion import converted_file, conversion_report


TYPE_BODY = "    private sealed class Fake\n    {\n    }\n"
METHOD_BODY = "    private static int Read(long value) => 2;\n"


def guarded_report(mode, body=TYPE_BODY, *, directive="", project=(), base=(), support_body=""):
    topic = split_pair(attribute="")[:1]
    if mode == "convert":
        file = converted_file(body)
        support = changed_file("namespace N;\ninternal static partial class Support\n{\n" +
                               support_body + "}\n", "namespace N;\ninternal static partial class Support\n{\n" +
                               support_body + "}\n", "Support.cs")
    else:
        file, support = member_move(body)
        if support_body:
            before = "namespace N;\ninternal static class Support\n{\n" + support_body + "}\n"
            after = before[:-2] + body.replace("private", "internal", 1) + "}\n"
            support = changed_file(before, after, "Support.cs")
    if directive and mode == "convert":
        root = topic[0]
        topic = (changed_file(directive + "\n" + "\n".join(root.before) + "\n",
                              directive + "\n" + "\n".join(root.after) + "\n", root.old_path),)
    elif directive:
        file = changed_file(directive + "\n" + "\n".join(file.before) + "\n",
                            directive + "\n" + "\n".join(file.after) + "\n", file.old_path)
    file = replace(file, project_sources=tuple((p, split.source_lines(s)) for p, s in project),
                   base_sources=tuple((p, split.source_lines(s)) for p, s in base))
    return (split.e3((*topic, file, support), "Support") if mode == "convert" else
            split.e3((file, support), "Support"))


@pytest.mark.parametrize("mode", ["move", "convert"])
@pytest.mark.parametrize("scope", ["N", "Different"])
@pytest.mark.parametrize("global_import", [False, True])
@pytest.mark.parametrize("revision", ["head", "base"])
def test_static_import_from_unchanged_project_type_blocks(mode, scope, global_import, revision):
    source = f"namespace {scope};\npublic class Other\n{{\n" + TYPE_BODY + "}\n"
    project = [("Other.cs", source)]
    directive = f"using static {scope}.Other;"
    if global_import:
        project.append(("Imports.cs", "global " + directive + "\n"))
        directive = ""
    report = guarded_report(mode, directive=directive, **{"project" if revision == "head" else "base": project})
    assert not report["passed"]
    assert any(c["declaring_type"] == "Other" and c["rule"] == "b" for c in report["collisions"])


@pytest.mark.parametrize("mode", ["move", "convert"])
@pytest.mark.parametrize("target", ["External.Other", "System.Unknown", "Xunit.Unknown", "Missing.Support"])
@pytest.mark.parametrize("global_import", [False, True])
def test_unresolved_static_import_blocks_without_a_blanket_framework_exception(mode, target, global_import):
    directive = f"using static {target};"
    project = (("Imports.cs", "global " + directive + "\n"),) if global_import else ()
    report = guarded_report(mode, directive="" if global_import else directive, project=project)
    assert not report["passed"]
    assert any(c["rule"] == "d" for c in report["collisions"])


@pytest.mark.parametrize("mode", ["move", "convert"])
@pytest.mark.parametrize("global_import", [False, True])
def test_static_import_of_project_type_without_the_moved_name_passes(mode, global_import):
    directive = "using static N.Other;"
    project = [("Other.cs", "namespace N;\npublic class Other\n{\n    public class Different {}\n}\n")]
    if global_import:
        project.append(("Imports.cs", "global " + directive + "\n"))
    assert guarded_report(mode, directive="" if global_import else directive, project=project)["passed"]


@pytest.mark.parametrize("mode", ["move", "convert"])
@pytest.mark.parametrize("global_alias", [False, True])
@pytest.mark.parametrize("revision", ["head", "base"])
@pytest.mark.parametrize("name,blocked", [("Fake", True), ("Different", False)])
def test_type_alias_collision_is_scoped_and_includes_unchanged_global_aliases(mode, global_alias, revision, name, blocked):
    directive = f"using {name} = N.Real;"
    sources = [("Real.cs", "namespace N;\npublic class Real {}\n")]
    if global_alias:
        sources.append(("Imports.cs", "global " + directive + "\n"))
    report = guarded_report(mode, directive="" if global_alias else directive,
                            **{"project" if revision == "head" else "base": sources})
    assert report["passed"] == (not blocked)
    assert bool(report["collisions"]) == blocked


@pytest.mark.parametrize("mode", ["move", "convert"])
@pytest.mark.parametrize("body,existing,name", [
    (METHOD_BODY, "    internal static int Read(int value) => 1;\n", "Read"),
    ("    private const int Value = 2;\n", "    internal const int Value = 1;\n", "Value"),
    (METHOD_BODY, "    internal sealed class Read\n    {\n    }\n", "Read"),
    ("    private const int Value = 2;\n", "    internal sealed class Value\n    {\n    }\n", "Value"),
])
def test_existing_support_member_from_another_origin_blocks(mode, body, existing, name):
    report = guarded_report(mode, body, support_body=existing)
    assert not report["passed"]
    assert any(c["name"] == name and c["declaring_type"] == "Support" for c in report["collisions"])


@pytest.mark.parametrize("mode", ["move", "convert"])
def test_support_name_present_only_in_the_moved_set_passes(mode):
    assert guarded_report(mode, METHOD_BODY)["passed"]


@pytest.mark.parametrize("mode", ["move", "convert"])
@pytest.mark.parametrize("globally", [False, True])
@pytest.mark.parametrize("kind", ["static", "alias"])
def test_name_import_after_another_directive_on_the_same_line_blocks(mode, globally, kind):
    directive = "using static N.Other;" if kind == "static" else "using Fake = N.Real;"
    sources = [("Other.cs", "namespace N;\npublic class Other\n{\n" + TYPE_BODY + "}\n")]
    if globally:
        sources.append(("Imports.cs", "global using System; global " + directive + "\n"))
    report = guarded_report(mode, directive="" if globally else "using System; " + directive, project=sources)
    assert not report["passed"]
    assert report["collisions"]


def test_moving_long_overload_cannot_rebind_the_retained_byte_call_to_existing_int_overload():
    old, moved = member_move(METHOD_BODY)
    use = "    public int Use(byte b) => Read(b);\n"
    old = changed_file("\n".join(old.before[:-1]) + "\n" + use + "}\n",
                       "\n".join(old.after[:-1]) + "\n" + use + "}\n", old.old_path)
    before = "namespace N;\ninternal static class Support\n{\n    internal static int Read(int value) => 1;\n}\n"
    after = before.replace("}\n", METHOD_BODY.replace("private", "internal", 1) + "}\n")
    report = split.e3((old, changed_file(before, after, moved.new_path)), "Support")
    assert not report["passed"]
    assert any(c["name"] == "Read" and c["declaring_type"] == "Support" and c["line"] == 4
               for c in report["collisions"])


@pytest.mark.parametrize("mode", ["move", "convert"])
@pytest.mark.parametrize("declaration", [
    "    private sealed record Rec(Func<int> Read);\n",
    "    private sealed record Rec(\n        Func<int> Read);\n",
    "    private class Pc(Func<int> Read)\n    {\n    }\n",
    "    private class Pc(\n        Func<int> Read)\n    {\n    }\n",
    "    private class Nested\n    {\n        public unsafe delegate*<int> Read;\n    }\n",
    "    private class Nested\n    {\n        public unsafe delegate* unmanaged[Cdecl]<int> Read;\n    }\n",
])
def test_nested_delegate_parameters_and_function_pointers_block(mode, declaration):
    context = changed_file("namespace N;\npublic partial class Old\n{\n" + declaration + "}\n",
                           "namespace N;\npublic partial class Old\n{\n" + declaration + "}\n", "Old.Other.cs")
    # Non-type guards intentionally inventory split partials, not unrelated project members.
    file = converted_file(METHOD_BODY) if mode == "convert" else member_move(METHOD_BODY)[0]
    file = replace(file, binding_sources=((context.new_path, context.after),))
    report = conversion_report(file) if mode == "convert" else split.e3((file, member_move(METHOD_BODY)[1]), "Support")
    assert not report["passed"]
    assert any(c["name"] == "Read" for c in report["collisions"])


@pytest.mark.parametrize("mode", ["move", "convert"])
@pytest.mark.parametrize("revision", ["head", "base"])
def test_changed_partial_of_unrelated_declaring_type_blocks_its_unchanged_nested_type(mode, revision):
    source = "namespace N;\npublic partial class Other\n{\n" + TYPE_BODY + "}\n"
    partial = changed_file("namespace N;\npublic partial class Other\n{\n}\n",
                           "namespace N;\npublic partial class Other\n{\n\n}\n", "Other.Part.cs")
    file = converted_file(TYPE_BODY) if mode == "convert" else member_move(TYPE_BODY)[0]
    file = replace(file, **{"project_sources" if revision == "head" else "base_sources":
                           (("Other.cs", split.source_lines(source)),)})
    report = (conversion_report(file, context=(partial,)) if mode == "convert" else
              split.e3((file, member_move(TYPE_BODY)[1], partial), "Support"))
    assert not report["passed"]
    assert any(c["declaring_type"] == "Other" and c["rule"] == "c" for c in report["collisions"])

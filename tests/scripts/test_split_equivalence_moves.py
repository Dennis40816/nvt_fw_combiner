"""Complete-member boundaries and byte identity of the supported C# subset."""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
from scripts import split_equivalence as split
from scripts import split_equivalence_moves as moves


@pytest.mark.parametrize("declaration,name", [
    ("private static int Read(int value = 1) => value;", "Read"),
    ("private static string Root => Path.GetFullPath(\"root\");", "Root"),
    ("private static (int Value, string Name) Pair() => (1, \"n\");", "Pair"),
    ("private static partial Regex Pattern();", "Pattern"),
    ("private static T Parse<T>(string text) => default!;", "Parse"),
    ("private const string Name = \"private\";", "Name"),
    ("private static readonly int Value = 1;", "Value"),
])
def test_member_names_and_complete_spans(declaration, name):
    source = split.source_lines("public sealed partial class C\n{\n    " + declaration + "\n}\n")
    members = moves.member_spans(source, split.code_lines(source))
    assert len(members) == 1
    assert (members[0].start, members[0].end, members[0].name) == (3, 3, name)


@pytest.mark.parametrize("body", [
    "    private static readonly int[] Values =\n    {\n        1,\n    };\n",
    "    private static int Read()\n    {\n        return 1;\n    }\n",
    '    private static int Read(string? text = "default")\n    {\n        return 1;\n    }\n',
    "    private sealed class Fake\n    {\n        private static int Hidden() => 1;\n    }\n",
    "    private sealed record Fake(\n        int Value);\n",
])
def test_member_extraction_stops_at_the_complete_outer_member(body):
    source = split.source_lines("public sealed partial class C\n{\n" + body + "    private const int Next = 2;\n}\n")
    members = moves.member_spans(source, split.code_lines(source))
    assert len(members) == 2
    assert members[0].end == 2 + len(body.splitlines())
    assert members[1].name == "Next"


@pytest.mark.parametrize("body", ["    private static int Read(] => 1;\n",
    "    private static int Read()\n    {\n", "    private const int Value = (1;\n",
    "    private static int Read() { return 1; } Extra();\n"])
def test_incomplete_or_ambiguous_members_have_no_span(body):
    source = split.source_lines("public sealed partial class C\n{\n" + body)
    assert moves.member_spans(source, split.code_lines(source)) == ()


def test_accessibility_normalization_changes_only_the_declaration_keyword():
    body = ('    private const string Name = "private";',)
    member = moves.Member(1, 1, "Name", "support_member_move")
    assert moves.canonical_body(body, member, True) == ('    internal const string Name = "private";',)
    assert moves.canonical_body(body, member, False) is None


def test_each_added_member_is_consumed_once():
    member = moves.Member(1, 1, "Name", "support_member_move")
    body = ("    internal const int Name = 1;",)
    removed = [moves.Candidate(0, member, body, (0,)), moves.Candidate(1, member, body, (0,))]
    added = [moves.Candidate(2, member, body, (0,))]
    assert moves.pair_moves(removed, added) == ((removed[0], added[0]),)
    assert moves.pair_moves(removed, [removed[0]]) == ((removed[1], removed[0]),)
    assert moves.pair_moves([removed[0]], [removed[0]]) == ()

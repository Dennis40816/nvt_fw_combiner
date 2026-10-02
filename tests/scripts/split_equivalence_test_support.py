"""Shared source construction and recorded evidence for E1/E3 tests."""

from __future__ import annotations

import json
import difflib
import sys
from dataclasses import replace
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
from scripts import split_equivalence as split
from scripts.authority_check import AuthorityError, Git

FIXTURES = Path(__file__).parent / "fixtures" / "split-equivalence"


def text(name: str) -> str:
    return (FIXTURES / name).read_text(encoding="utf-8")


def require_split_commits(expected):
    for revision in (expected["base"], expected["head"]):
        try:
            Git(ROOT).run("rev-parse", "--verify", "--quiet", "--end-of-options", revision + "^{commit}")
        except AuthorityError as error:
            if str(error) != "git rev-parse --verify failed: 1":
                raise
            pytest.skip(f"split commit {revision} is absent from this clone")


def load_case(case: dict) -> tuple[split.FileDiff, ...]:
    return tuple(split.FileDiff(item["status"], item["old_path"], item["new_path"],
                                split.source_lines(item["before"]), split.source_lines(item["after"]),
                                split.patch_lines(item["patch"]),
                                head_sources=tuple((path, split.source_lines(source)) for path, source in
                                                   item.get("head_sources", {}).items())) for item in case["files"])


CASES = json.loads(text("synthetic-diffs.json"))


def changed_file(before: str, after: str, path: str = "Tests.cs") -> split.FileDiff:
    patch = "\n".join(difflib.unified_diff(split.source_lines(before),
                                          split.source_lines(after), n=0, lineterm=""))
    return split.FileDiff("M", path, path, split.source_lines(before),
                          split.source_lines(after), split.patch_lines(patch))


def split_pair(old="Old", new="New", attribute='[Collection("Shared")]\n'):
    body = f"namespace N;\npublic sealed partial class {old}\n{{\n}}\n"
    topic = changed_file(body, body.replace(f"public sealed partial class {old}",
                                            attribute + f"public sealed partial class {new}"), new + ".cs")
    root = changed_file(body, body.replace("public sealed", '[Collection("Shared")]\npublic sealed'), old + ".cs")
    return topic, root


def added_collection_definition(name="Shared", path="Shared.cs"):
    source = f'namespace N;\n[CollectionDefinition("{name}")]\npublic sealed class Shared\n{{\n}}\n'
    return replace(changed_file("", source, path), status="A")


MEMBER_BODIES = [
    ('    private const string Hash =\n        "abc";\n', "Hash", "support_member_move"),
    ('    private static readonly int Value = 1;\n', "Value", "support_member_move"),
    ('    private static readonly int[] Values =\n    {\n        1,\n        2,\n    };\n', "Values", "support_member_move"),
    ('    private static readonly Options Settings = new()\n    {\n        Value = 1,\n    };\n', "Settings", "support_member_move"),
    ('    private sealed record Result(\n        int Value,\n        string Name);\n', "Result", "support_type_move"),
    ('    private sealed class Fake(int value) : IFake\n    {\n        public int Value => value;\n    }\n', "Fake", "support_type_move"),
    ('    private struct Point\n    {\n        public int X;\n    }\n', "Point", "support_type_move"),
    ('    private enum Mode\n    {\n        First,\n        Second,\n    }\n', "Mode", "support_type_move"),
    ('    private interface IFake\n    {\n        int Read();\n    }\n', "IFake", "support_type_move"),
    ('    private delegate int Reader(\n        int value);\n', "Reader", "support_type_move"),
    ('    private static int Read()\n    {\n        return 1;\n    }\n', "Read", "helper_move"),
    ('    private static int ReadOptional(int value = 1)\n    {\n        return value;\n    }\n', "ReadOptional", "helper_move"),
]


def member_move(body, *, top=False, support_path="Support.cs", new_body=None, prefix=""):
    source = "namespace N;\npublic sealed partial class Old\n{\n"
    old = changed_file(source + prefix + body + "}\n", source + "}\n", "Old.cs")
    moved = prefix + body.replace("private", "internal", 1)
    if top:
        moved = "\n".join(line[4:] if line.startswith("    ") else line for line in moved.split("\n"))
    if new_body is not None:
        moved = new_body
    target = "using System;\nnamespace N;\n/// <summary>Shared helpers.</summary>\ninternal static class Support\n{\n"
    after = target + ("}\n" + moved if top else moved + "}\n")
    new = replace(changed_file("", after, support_path), status="A", before=())
    return old, new


SCHEDULED_SPLITS = json.loads(text("scheduled-splits.json"))

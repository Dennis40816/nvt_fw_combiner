"""Conservative classification of the first actual scheduled split."""

from __future__ import annotations

import difflib
import json
import os
from dataclasses import replace

import pytest

from split_equivalence_test_support import ROOT, changed_file, member_move, split, split_pair, text
from scripts.authority_check import AuthorityError, Git


def read_named_pair(*, mutation=False, new_path="New.Topic.cs", declaration="New", extra="",
                    ambiguous=False, metadata=""):
    body = "namespace N;\npublic sealed partial class Old\n{\n    [Fact]\n    public void Check() => Assert.Equal(1, Read());\n"
    before = body + "    private static int Read() => 1;\n}\n"
    after = body.replace("class Old", "class " + declaration) + "}\n" + extra
    if mutation:
        after = after.replace("Assert.Equal(1", "Assert.Equal(2")
    support = "namespace N;\ninternal static class Support\n{\n    internal static int Read() => 1;\n}\n"
    sources = {"Old.Topic.cs": before, new_path: after, "Support.cs": support}
    names = [("D", "Old.Topic.cs"), ("A", new_path), ("A", "Support.cs")]
    if ambiguous:
        sources["Another.Topic.cs"] = after.replace("class New", "class Another")
        names.append(("A", "Another.Topic.cs"))
    blobs = {str(index + 1) * 40: source for index, source in enumerate(sources.values())}
    ids = dict(zip(sources, blobs))

    class PairGit(Git):
        def commit(self, revision):
            return revision

        def merge_base(self, base, head):
            return base

        def blob(self, revision, path):
            return ids[path], sources[path].encode()

        def run(self, *arguments):
            if arguments[0] == "ls-tree":
                paths = ["Old.Topic.cs"] if "base" in arguments else list(sources)
                return ("\0".join(paths) + "\0").encode()
            if "--name-status" in arguments:
                return "".join(status + "\0" + path + "\0" for status, path in names).encode()
            if "--" in arguments:
                path = arguments[-1]
                old, new = (sources[path], "") if path == "Old.Topic.cs" else ("", sources[path])
            else:
                old, new = (blobs[oid] for oid in arguments[-2:])
            patch = "\n".join(difflib.unified_diff(split.source_lines(old), split.source_lines(new),
                                                  n=0, lineterm=""))
            return ("diff --git a/source b/source\n" + metadata + patch).encode()

    return split.read_diff(PairGit(ROOT), "base", "head", ".")


@pytest.mark.parametrize("mutation", [False, True])
def test_named_split_pairs_below_git_similarity_still_require_identical_test_bodies(mutation):
    files = read_named_pair(mutation=mutation)
    assert len(files) == 2
    pair = next(file for file in files if file.status == "R")
    assert (pair.old_path, pair.new_path) == ("Old.Topic.cs", "New.Topic.cs")
    report = split.e3(files, "Support")
    assert report["passed"] == (not mutation)
    assert report["counts"]["rename"] == 1
    if mutation:
        assert {line["text"] for line in report["unclassified"]} == {
            "    public void Check() => Assert.Equal(1, Read());",
            "    public void Check() => Assert.Equal(2, Read());"}


@pytest.mark.parametrize("options", [
    {"new_path": "Sub/New.Topic.cs"}, {"new_path": "New.Other.cs"},
    {"declaration": "Wrong"}, {"extra": "public sealed partial class Extra\n{\n}\n"},
    {"ambiguous": True},
])
def test_named_split_pairing_rejects_directory_topic_declaration_and_ambiguity(options):
    files = read_named_pair(**options)
    assert all(file.status != "R" for file in files)
    assert not split.e3(files, "Support")["passed"]


@pytest.mark.parametrize("metadata", ["new file mode 100755\n", "\\ No newline at end of file\n"])
def test_named_pairing_preserves_non_text_rejections(metadata):
    assert not split.e3(read_named_pair(metadata=metadata), "Support")["passed"]


@pytest.mark.parametrize("extra", ["", "    public void Extra() {}\n", "    // Extra context.\n"])
def test_deleted_source_partial_requires_every_non_skeleton_line_to_move(extra):
    body = "    private static int Read() => 1;\n"
    old, support = member_move(body)
    before = "using System;\n" + "\n".join(old.before[:-1]) + "\n" + extra + "}\n"
    deleted = replace(changed_file(before, "", "Old.Helpers.cs"), status="D")
    report = split.e3((*split_pair(attribute="")[:1], deleted, support), "Support")
    assert report["passed"] == (not extra)
    assert report["counts"].get("emptied_partial_removed", 0) == (5 if not extra else 0)
    if extra:
        assert any(line["text"] == extra.rstrip("\n") for line in report["unclassified"])


@pytest.mark.parametrize("name,namespace", [("Unrelated", "N"), ("Old", "Other")])
def test_deleted_partial_must_belong_to_the_identified_source_class(name, namespace):
    old, support = member_move("    private static int Read() => 1;\n")
    before = "\n".join(old.before).replace("class Old", "class " + name).replace("namespace N", "namespace " + namespace) + "\n"
    deleted = replace(changed_file(before, "", name + ".Helpers.cs"), status="D")
    assert not split.e3((*split_pair(attribute="")[:1], deleted, support), "Support")["passed"]


def require_split_commits(expected):
    for revision in (expected["base"], expected["head"]):
        try:
            Git(ROOT).run("rev-parse", "--verify", "--quiet", "--end-of-options", revision + "^{commit}")
        except AuthorityError as error:
            if str(error) != "git rev-parse --verify failed: 1":
                raise
            pytest.skip(f"real scheduled split commit {revision} is absent from this clone (local-only range)")


@pytest.mark.parametrize("error,exception", [
    ("git rev-parse --verify failed: 1", pytest.skip.Exception),
    ("git rev-parse failed: timeout", AuthorityError),
])
def test_real_split_skips_only_missing_commits_not_git_environment_failures(monkeypatch, error, exception):
    def fail(*args):
        raise AuthorityError(error)
    monkeypatch.setattr(Git, "run", fail)
    with pytest.raises(exception):
        require_split_commits({"base": "a" * 40, "head": "b" * 40})


def test_unclosed_deleted_partial_is_not_an_empty_skeleton():
    old, support = member_move("    private static int Read() => 1;\n")
    before = "\n".join(old.before[:-1]) + "\n"
    deleted = replace(changed_file(before, "", "Old.Helpers.cs"), status="D")
    report = split.e3((*split_pair(attribute="")[:1], deleted, support), "Support")
    assert not report["passed"]
    assert report["counts"]["emptied_partial_removed"] == 0


def test_real_scheduled_split_pins_path_line_and_kind_counts(monkeypatch, capsys):
    monkeypatch.setenv("GIT_CONFIG_GLOBAL", os.devnull)
    monkeypatch.setenv("GIT_CONFIG_NOSYSTEM", "1")
    expected = json.loads(text("profile-contract-real.json"))
    require_split_commits(expected)
    assert split.main(["e3", "--base", expected["base"], "--head", expected["head"],
                       "--project", expected["project"], "--support-class", expected["support_class"]]) == 0
    report = json.loads(capsys.readouterr().out)
    for key in ("base", "head", "project", "support_class"):
        assert report[key] == expected[key]
    assert report["changed_paths"] == expected["changed_paths"]
    assert report["counts"] == expected["counts"]
    assert sum(value for kind, value in report["counts"].items() if kind != "rename") == expected["changed_lines"]
    assert report["unclassified"] == report["collection_consistency"] == []

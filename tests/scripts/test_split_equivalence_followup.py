"""Conservative classification of the first actual scheduled split."""

from __future__ import annotations

import difflib
import json
import os
from dataclasses import replace

import pytest

from split_equivalence_test_support import ROOT, changed_file, member_move, require_split_commits, split, split_pair, text
from scripts.authority_check import AuthorityError, Git


def read_named_pair(*, mutation=False, new_path="New.Topic.cs", declaration="New", extra="",
                    ambiguous=False, metadata="", context=None, dependencies=None):
    body = "namespace N;\npublic sealed partial class Old\n{\n    [Fact]\n    public void Check() => Assert.Equal(1, Read());\n"
    before = body + "    private static int Read() => 1;\n}\n"
    after = body.replace("class Old", "class " + declaration) + "}\n" + extra
    if mutation:
        after = after.replace("Assert.Equal(1", "Assert.Equal(2")
    support = "namespace N;\ninternal static class Support\n{\n    internal static int Read() => 1;\n}\n"
    sources = {"Old.Topic.cs": before, new_path: after, "Support.cs": support}
    names = [("D", "Old.Topic.cs"), ("A", new_path), ("A", "Support.cs")]
    sources.update(context or {})
    sources.update(dependencies or {})
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
            if arguments[0] == "grep":
                assert arguments[-3:] == ("head", "--", "src")
                return "".join("head:" + path + "\n" for path in (dependencies or {})).encode()
            if arguments[0] == "ls-tree":
                paths = (["Old.Topic.cs"] if "base" in arguments else [path for status, path in names if status == "A"])
                paths += list(context or {})
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


@pytest.mark.parametrize("error,exception", [
    ("git rev-parse --verify failed: 1", pytest.skip.Exception),
    ("git rev-parse failed: timeout", AuthorityError),
])
def test_real_split_skips_only_missing_commits_not_git_environment_failures(monkeypatch, error, exception):
    def fail(*args):
        raise AuthorityError(error)
    monkeypatch.setattr(Git, "run", fail)
    with pytest.raises(exception, match="a" * 40 if exception is pytest.skip.Exception else "timeout"):
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
    assert report["counts"] == {**expected["counts"], "support_partial_conversion": 0}
    assert sum(value for kind, value in report["counts"].items() if kind != "rename") == expected["changed_lines"]
    assert report["unclassified"] == report["collection_consistency"] == []


def test_split_cannot_join_an_existing_unrelated_collection():
    topic, root = split_pair()
    unrelated = 'namespace N;\n[Collection("Shared")]\npublic sealed class Unrelated\n{\n}\n'
    topic = replace(topic, head_sources=(("Unrelated.cs", split.source_lines(unrelated)),))
    report = split.e3((topic, root))
    assert not report["passed"]
    failure = report["collection_consistency"][0]
    assert set(failure["classes"]) == {"Old", "New"}
    assert failure["collection"] == '[Collection("Shared")]'


def test_split_can_keep_the_source_classes_original_collection():
    before = 'namespace N;\n[Collection("Shared")]\npublic sealed partial class Old\n{\n}\n'
    topic = changed_file(before, before.replace("class Old", "class New"), "New.cs")
    root = changed_file(before, before + "\n", "Old.cs")
    report = split.e3((topic, root))
    assert report["passed"]
    assert report["collection_consistency"] == []


def test_split_can_join_a_collection_defined_by_this_diff():
    definition = 'namespace N;\n[CollectionDefinition("Shared")]\npublic sealed class Shared\n{\n}\n'
    added = replace(changed_file("", definition, "Shared.cs"), status="A")
    assert split.e3((*split_pair(), added))["passed"]


def test_a_different_namespaces_source_name_cannot_authorize_collection_membership():
    unrelated = 'namespace Other;\n[Collection("Shared")]\npublic sealed partial class Old\n{\n}\n'
    topic = replace(split_pair()[0], head_sources=(("Unrelated.cs", split.source_lines(unrelated)),),
                    base_sources=(("Unrelated.cs", split.source_lines(unrelated)),))
    report = split.e3((topic,))
    assert not report["passed"]
    assert any(item.get("collection") == '[Collection("Shared")]' for item in report["collection_consistency"])


@pytest.mark.parametrize("remaining", [
    "    private static int Read(long value) => 2;\n",
    "    public int Read(long value) => 2;\n",
    "    [Obsolete]\n    internal static int Read(long value) => 2;\n",
    "    public static int Read { get; }\n",
    "    public static int\n        Read(long value) => 2;\n",
    "    [Obsolete] public static int Read(long value) => 2;\n",
])
@pytest.mark.parametrize("location", ["root", "topic", "touched", "unchanged-partial"])
def test_moved_helper_with_a_same_named_head_member_is_unclassified(remaining, location):
    body = "    private static int Read(int value) => 1;\n"
    old, support = member_move(body)
    files = [old, support]
    if location == "root":
        before = "\n".join(old.before[:-1]) + "\n" + remaining + "}\n"
        after = "\n".join(old.after[:-1]) + "\n" + remaining + "}\n"
        files[0] = changed_file(before, after, "Old.cs")
        path = "Old.cs"
    else:
        path = {"topic": "New.cs", "touched": "Unrelated.cs", "unchanged-partial": "OddName.cs"}[location]
        name = "Old" if location == "unchanged-partial" else "New" if location == "topic" else "Unrelated"
        source = "namespace N;\npublic sealed partial class " + name + "\n{\n" + remaining + "}\n"
        if location == "unchanged-partial":
            files[0] = replace(old, head_sources=((path, split.source_lines(source)),))
        else:
            files.append(changed_file(source, source + "\n", path))
    report = split.e3(files, "Support")
    assert not report["passed"]
    assert any(item["name"] == "Read" and item["file"] == path
               for item in report["collisions"])
    assert any(item.get("collision_ids") for item in report["unclassified"])
    assert report["counts"]["helper_move"] == 0


def test_same_named_overloads_moved_together_into_support_are_allowed():
    body = "    private static int Read(int value) => 1;\n    private static int Read(long value) => 2;\n"
    old, support = member_move(body)
    after = "\n".join(support.after).replace("private static", "internal static") + "\n"
    support = replace(changed_file("", after, "Support.cs"), status="A")
    report = split.e3((old, support), "Support")
    assert report["passed"]
    assert len(report["moves"]) == 2


def test_moving_the_wider_overload_also_rejects_a_remaining_narrower_overload():
    moved = "    private static int Read(long value) => 1;\n"
    remaining = "    private static int Read(int value) => 2;\n"
    old, support = member_move(moved)
    before = "\n".join(old.before[:-1]) + "\n" + remaining + "}\n"
    after = "\n".join(old.after[:-1]) + "\n" + remaining + "}\n"
    report = split.e3((changed_file(before, after, "Old.cs"), support), "Support")
    assert not report["passed"]
    assert any("same-named member remains in Old.cs" in item.get("reason", "")
               for item in report["collisions"])


def test_git_finds_remaining_members_in_a_partial_with_an_unrelated_filename():
    source = "namespace N;\npublic sealed partial class Old\n{\n    private static int Read(long value) => 2;\n}\n"
    report = split.e3(read_named_pair(context={"OddName.cs": source}), "Support")
    assert not report["passed"]
    assert any("same-named member remains in OddName.cs" in item.get("reason", "")
               for item in report["collisions"])


def test_extra_binding_context_does_not_relax_the_existing_collection_rejection():
    topic = split_pair()[0]
    root = 'namespace N;\n[Collection("Shared")]\npublic sealed partial class Old\n{\n}\n'
    topic = replace(topic, binding_sources=(("OddName.cs", split.source_lines(root)),),
                    base_sources=(("OddName.cs", split.source_lines(root)),))
    report = split.e3((topic,))
    assert not report["passed"]
    assert report["collection_consistency"]


def test_an_existing_other_class_inside_the_support_file_is_not_exempt():
    old, support = member_move("    private static int Read(int value) => 1;\n")
    other = "internal sealed class Other\n{\n    public int Read(long value) => 2;\n}\n"
    support = changed_file("namespace N;\ninternal static class Support\n{\n}\n" + other,
                           "\n".join(support.after) + "\n" + other, "Support.cs")
    report = split.e3((old, support), "Support")
    assert not report["passed"]
    assert any("same-named member remains in Support.cs" in item.get("reason", "")
               for item in report["collisions"])


@pytest.mark.parametrize("origin,passed", [("Old", True), ("Unrelated", False)])
def test_helper_move_reports_and_rejects_an_unrelated_origin_class(origin, passed):
    old, support = member_move("    private static int Read() => 1;\n")
    before = "\n".join(old.before).replace("class Old", "class " + origin) + "\n"
    after = "\n".join(old.after).replace("class Old", "class " + origin) + "\n"
    old = changed_file(before, after, origin + ".Helpers.cs")
    report = split.e3((*split_pair(attribute="")[:1], old, support), "Support")
    assert report["passed"] == passed
    assert bool(report["moves_from_unrelated_classes"]) == (not passed)
    if not passed:
        assert report["moves_from_unrelated_classes"][0]["old_class"] == "Unrelated"
        assert report["moves_from_unrelated_classes"][0]["old_file"] == "Unrelated.Helpers.cs"


def test_raw_dotnet_discovery_preamble_explains_how_to_prepare_input():
    with pytest.raises(ValueError, match="strip the preamble.*keep the test names"):
        split.discovery("Test run for tests.dll (.NETCoreApp,Version=v10.0)\nThe following Tests are available:\nN.Old.Check\n")
    assert split.e1("The following Tests are available:\nN.Old.Check",
                    "The following Tests are available:\nN.New.Check")["passed"]


@pytest.mark.parametrize("command,options", [
    ("e1", {"--before": "Base discovery text", "--after": "Head discovery text",
            "--mapping": "JSON mapping of identities"}),
    ("e3", {"--base": "Base Git commit", "--head": "Head Git commit",
            "--project": "Repository-relative project path", "--support-class": "Simple name",
            "--allow-directory-move": "Allow C# renames across directories"}),
])
def test_command_help_explains_limits_and_every_argument(command, options, capsys):
    with pytest.raises(SystemExit) as result:
        split.main([command, "--help"])
    assert result.value.code == 0
    help_text = " ".join(capsys.readouterr().out.split())
    for limitation in ("neither E2 nor E4 to E7", "Unchanged lines are not examined", "constructor",
                       "outside --project", "no name-binding check", "without --mapping"):
        assert limitation in help_text
    for option, description in options.items():
        assert option in help_text
        assert description in help_text


def test_missing_head_commit_skip_names_the_missing_revision(monkeypatch):
    revisions = {"base": "a" * 40, "head": "b" * 40}
    def run(self, *args):
        if args[-1] == revisions["head"] + "^{commit}":
            raise AuthorityError("git rev-parse --verify failed: 1")
        return b""
    monkeypatch.setattr(Git, "run", run)
    with pytest.raises(pytest.skip.Exception, match=revisions["head"]):
        require_split_commits(revisions)

"""Frozen evidence is proved from Git objects, index entries and raw disk bytes."""
from __future__ import annotations

import json
import os
import shutil
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
import pytest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))
import validate_repository as validator

DIRECTORIES = (
    "docs/governance/change-records",
    "docs/governance/external-authority-attestations",
    "docs/governance/waivers",
)
CHECKPOINT = "docs/governance/trusted-initial-capability-checkpoint.v1.json"
PIN_FILE = "docs/governance/frozen-evidence-pins.json"


def git(root, *args):
    env = {key: value for key, value in os.environ.items() if not key.upper().startswith("GIT_")}
    return subprocess.check_output(["git", *args], cwd=root, env=env, stderr=subprocess.PIPE).decode().strip()


def write_pins(root, base=None):
    """Pins staged evidence; callers commit it with the pin document."""
    tree = git(root, "write-tree")
    data = {
        "schemaVersion": 1,
        "authority": "docs/adr/0080-governance-reset.md#evidence-and-admission",
        "frozenAtBase": base or git(root, "rev-parse", "HEAD"),
        "pins": [
            {"path": path, "type": kind, "id": git(root, "rev-parse", f"{tree}:{path}")}
            for path, kind in [*((path, "tree") for path in DIRECTORIES), (CHECKPOINT, "blob")]
        ],
    }
    (root / PIN_FILE).write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8", newline="\n")


class FrozenEvidencePinTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        git(self.root, "init", "-q")
        git(self.root, "config", "user.name", "Fixture")
        git(self.root, "config", "user.email", "fixture@example.invalid")
        git(self.root, "config", "core.autocrlf", "false")
        (self.root / ".gitattributes").write_text("*.json text eol=lf\n*.md text eol=lf\n", encoding="utf-8")
        for directory in DIRECTORIES:
            (self.root / directory).mkdir(parents=True)
            (self.root / directory / "record.json").write_bytes(b'{"evidence": true}\n')
        (self.root / CHECKPOINT).write_bytes(b'{}\n')
        git(self.root, "add", ".")
        git(self.root, "commit", "-qm", "Evidence")
        write_pins(self.root)
        git(self.root, "add", ".")
        git(self.root, "commit", "-qm", "Freeze")
        self.record = DIRECTORIES[0] + "/record.json"

    def errors(self):
        errors = []
        validator.validate_frozen_evidence_pins(self.root, errors)
        return errors

    def reject(self, path):
        self.assertTrue(any(path in error for error in self.errors()), self.errors())

    def test_clean_snapshot_passes(self):
        self.assertEqual([], self.errors())

    def test_raw_change_fails_even_when_git_ignores_it(self):
        git(self.root, "update-index", "--assume-unchanged", self.record)
        (self.root / self.record).write_bytes(b'changed\n')
        self.reject(self.record)

    def test_changed_then_restored_history_passes(self):
        original = (self.root / self.record).read_bytes()
        (self.root / self.record).write_bytes(b'changed\n')
        git(self.root, "commit", "-qam", "Change")
        (self.root / self.record).write_bytes(original)
        git(self.root, "commit", "-qam", "Restore")
        self.assertEqual([], self.errors())

    def test_proof_processes_have_closed_git_environment(self):
        run = subprocess.run
        calls = []

        def observe(argv, **kwargs):
            calls.append((argv, kwargs))
            return run(argv, **kwargs)

        with mock.patch.dict(os.environ, {"GIT_DIR": "missing", "GIT_INDEX_FILE": "missing",
                                        "GIT_CONFIG_COUNT": "1", "GIT_CONFIG_KEY_0": "alias.foo",
                                        "GIT_CONFIG_VALUE_0": "bar", "GIT_WORK_TREE": "missing",
                                        "GIT_OBJECT_DIRECTORY": "missing"}):
            with mock.patch.object(validator.subprocess, "run", side_effect=observe):
                self.assertEqual([], self.errors())
        self.assertEqual(3, len(calls))
        for argv, kwargs in calls:
            self.assertEqual(["git", "--no-replace-objects"], argv[:2])
            self.assertEqual({"GIT_NO_REPLACE_OBJECTS": "1", "GIT_OPTIONAL_LOCKS": "0",
                              "GIT_LITERAL_PATHSPECS": "1"},
                             {k: v for k, v in kwargs["env"].items() if k.upper().startswith("GIT_")})

    def test_replacement_object_cannot_hide_changed_head(self):
        frozen = git(self.root, "rev-parse", "HEAD")
        (self.root / self.record).write_bytes(b'changed\n')
        git(self.root, "commit", "-qam", "Change")
        git(self.root, "replace", "HEAD", frozen)
        self.reject(DIRECTORIES[0])

    def test_alternate_index_cannot_hide_staged_change(self):
        crafted = self.root / "clean-index"
        shutil.copyfile(self.root / ".git/index", crafted)
        original = (self.root / self.record).read_bytes()
        (self.root / self.record).write_bytes(b'changed\n')
        git(self.root, "add", self.record)
        (self.root / self.record).write_bytes(original)
        with mock.patch.dict(os.environ, {"GIT_INDEX_FILE": str(crafted)}):
            self.reject(self.record)

    def test_alternate_repository_cannot_hide_changed_head(self):
        crafted = self.root / "clean-repository"
        git(self.root, "clone", "-q", "--shared", str(self.root), str(crafted))
        original = (self.root / self.record).read_bytes()
        (self.root / self.record).write_bytes(b'changed\n')
        git(self.root, "commit", "-qam", "Change")
        (self.root / self.record).write_bytes(original)
        with mock.patch.dict(os.environ, {"GIT_DIR": str(crafted / ".git"), "GIT_WORK_TREE": str(crafted)}):
            self.reject(DIRECTORIES[0])

    @unittest.skipUnless(os.name == "nt", "Windows checkout contract")
    def test_autocrlf_checkout_preserves_raw_blob_bytes(self):
        git(self.root, "config", "core.autocrlf", "true")
        (self.root / self.record).unlink()
        git(self.root, "checkout", "--", self.record)
        self.assertEqual([], self.errors())


@pytest.fixture
def snapshot():
    case = FrozenEvidencePinTests()
    case.setUp()
    yield case
    case.doCleanups()


@pytest.mark.parametrize("change", ["add", "change", "delete", "rename-out", "rename-in",
                                    "record-mode", "checkpoint-mode", "checkpoint-blob"])
def test_committed_mutations_fail(snapshot, change):
    root, record = snapshot.root, snapshot.record
    path = DIRECTORIES[0]
    if change == "add":
        (root / path / "extra.json").write_bytes(b'{}')
    elif change == "change":
        (root / record).write_bytes(b'changed')
    elif change == "delete":
        (root / record).unlink()
    elif change == "rename-out":
        git(root, "mv", record, "outside.json")
    elif change == "rename-in":
        (root / "outside.json").write_bytes(b'{}')
        git(root, "add", "outside.json")
        git(root, "commit", "-qm", "Outside")
        git(root, "mv", "outside.json", path + "/incoming.json")
    elif change.endswith("mode"):
        target = CHECKPOINT if change.startswith("checkpoint") else record
        git(root, "update-index", "--chmod=+x", target)
        path = target if target == CHECKPOINT else path
    else:
        path = CHECKPOINT
        (root / path).write_bytes(b'changed')
    if not change.endswith("mode"):
        git(root, "add", "-A")
    git(root, "commit", "-qm", "Mutation")
    snapshot.reject(path)


@pytest.mark.parametrize("change", ["staged", "intent", "assume", "skip", "sparse",
                                    "unmerged", "gitlink", "symlink"])
def test_index_mutations_fail(snapshot, change):
    root, path = snapshot.root, snapshot.record
    if change in {"staged", "intent"}:
        if change == "intent":
            path = DIRECTORIES[0] + "/extra.json"
        (root / path).write_bytes(b'changed')
        git(root, "add", *(["-N"] if change == "intent" else []), path)
    elif change in {"assume", "skip"}:
        git(root, "update-index", "--assume-unchanged" if change == "assume" else "--skip-worktree", path)
        (root / path).unlink()
    elif change == "sparse":
        git(root, "sparse-checkout", "set", "--no-cone", "/*", "!/docs/governance/change-records/")
    elif change == "unmerged":
        oid = git(root, "rev-parse", "HEAD:" + path)
        git(root, "update-index", "--force-remove", path)
        subprocess.run(["git", "update-index", "--index-info"], cwd=root,
                       input=f"100644 {oid} 1\t{path}\n".encode(), check=True)
    else:
        oid = git(root, "rev-parse", "HEAD" if change == "gitlink" else "HEAD:" + path)
        git(root, "update-index", "--cacheinfo", "160000" if change == "gitlink" else "120000", oid, path)
    snapshot.reject(path)


@pytest.mark.parametrize("change", ["unstaged", "crlf", "untracked", "ignored", "directory", "missing"])
def test_disk_mutations_fail(snapshot, change):
    root, path = snapshot.root, snapshot.record
    if change == "unstaged":
        (root / path).write_bytes(b'changed')
    elif change == "crlf":
        (root / path).write_bytes((root / path).read_bytes().replace(b'\n', b'\r\n'))
    elif change in {"untracked", "ignored", "directory"}:
        path = DIRECTORIES[0] + "/extra"
        if change == "directory":
            (root / path).mkdir()
        else:
            (root / path).write_bytes(b'extra')
            if change == "ignored":
                (root / ".git/info/exclude").write_text("extra\n", encoding="utf-8")
    else:
        (root / path).unlink()
    snapshot.reject(path)


@pytest.mark.parametrize("path", ["docs", "docs/governance", *DIRECTORIES, CHECKPOINT,
                                  DIRECTORIES[0] + "/record.json"])
def test_case_only_disk_rename_fails(snapshot, path):
    original = snapshot.root / path
    intermediate = original.with_name(original.name + ".temporary")
    original.rename(intermediate)
    intermediate.rename(original.with_name(original.name.upper()))
    snapshot.reject(path)


def test_case_alias_with_ignorecase_fails(snapshot):
    path = snapshot.root / snapshot.record
    alias = path.with_name(path.name.upper())
    if alias.exists():
        pytest.skip("case-insensitive filesystem cannot hold both spellings")
    git(snapshot.root, "config", "core.ignorecase", "true")
    alias.write_bytes(path.read_bytes())
    snapshot.reject(DIRECTORIES[0])


@pytest.mark.parametrize("path", ["docs", "docs/governance", DIRECTORIES[0], CHECKPOINT,
                                  DIRECTORIES[0] + "/record.json"])
def test_symlink_components_fail(snapshot, path):
    target = snapshot.root / path
    saved = snapshot.root / "saved-evidence"
    is_dir = target.is_dir()
    target.rename(saved)
    try:
        target.symlink_to(saved, target_is_directory=is_dir)
    except OSError as error:
        pytest.skip(f"symlink creation unavailable: {error.winerror if os.name == 'nt' else error.errno}")
    snapshot.reject(path)
    target.unlink()


@pytest.mark.skipif(os.name != "nt", reason="Windows junction contract")
@pytest.mark.parametrize("path", ["docs", "docs/governance", *DIRECTORIES])
def test_junction_components_fail(snapshot, path):
    target = snapshot.root / path
    saved = snapshot.root / "saved-evidence"
    target.rename(saved)
    subprocess.run(["cmd", "/c", "mklink", "/J", str(target), str(saved)],
                   check=True, capture_output=True)
    snapshot.reject(path)
    os.rmdir(target)


@pytest.mark.skipif(os.name == "nt", reason="POSIX execute permission contract")
@pytest.mark.parametrize("path", [CHECKPOINT, DIRECTORIES[0] + "/record.json"])
def test_execute_bit_fails(snapshot, path):
    (snapshot.root / path).chmod(0o755)
    snapshot.reject(path)


@pytest.mark.parametrize("change", ["duplicate", "extra", "missing", "retyped", "reordered", "short",
                                    "boolean-version", "base"])
def test_pin_document_is_closed(snapshot, change):
    path = snapshot.root / PIN_FILE
    document = json.loads(path.read_text(encoding="utf-8"))
    if change == "duplicate":
        path.write_text(path.read_text(encoding="utf-8").replace('"schemaVersion": 1', '"schemaVersion": 1, "schemaVersion": 1'), encoding="utf-8")
    else:
        if change == "extra": document["extra"] = True
        elif change == "missing": document["pins"].pop()
        elif change == "retyped": document["pins"][0]["type"] = "blob"
        elif change == "reordered": document["pins"].reverse()
        elif change == "short": document["pins"][0]["id"] = "123"
        elif change == "boolean-version": document["schemaVersion"] = True
        else: document["frozenAtBase"] = "HEAD"
        path.write_text(json.dumps(document), encoding="utf-8")
    assert snapshot.errors()


def test_non_repository_fails(snapshot):
    git_dir = snapshot.root / ".git"
    git_dir.rename(snapshot.root / "saved-git")
    assert snapshot.errors()


def test_sha256_repository_fails(snapshot, tmp_path):
    git(tmp_path, "init", "--object-format=sha256", "-q")
    (tmp_path / PIN_FILE).parent.mkdir(parents=True)
    (tmp_path / PIN_FILE).write_bytes((snapshot.root / PIN_FILE).read_bytes())
    errors = []
    validator.validate_frozen_evidence_pins(tmp_path, errors)
    assert any("SHA-1" in error for error in errors)


@pytest.mark.parametrize("reply", [b'bad', b'\xff\0'])
def test_malformed_git_reply_fails(snapshot, reply):
    with mock.patch.object(validator.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, reply)):
        assert snapshot.errors()

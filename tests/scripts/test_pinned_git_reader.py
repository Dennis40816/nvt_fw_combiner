"""The pinned Git reader on a real temporary repository that carries a submodule gitlink."""

from __future__ import annotations

import subprocess
import tempfile
import unittest
from pathlib import Path

from scripts import v0916_parity_certification as parity


GITLINK_COMMIT = "6" * 40
GITLINK_PATH = "third-party/dependency"


def git(root: Path, *arguments: str) -> str:
    return subprocess.check_output(
        ["git", "-c", "user.name=test", "-c", "user.email=test@example.invalid",
         "-c", "core.autocrlf=false", "-c", "commit.gpgsign=false", *arguments],
        cwd=root, text=True, stderr=subprocess.PIPE,
    ).strip()


class PinnedGitReaderGitlinkTests(unittest.TestCase):
    def setUp(self) -> None:
        self.scratch = tempfile.TemporaryDirectory()
        self.root = Path(self.scratch.name)
        git(self.root, "init", "--quiet")
        (self.root / "docs").mkdir()
        (self.root / "docs" / "note.txt").write_bytes(b"synthetic\n")
        git(self.root, "add", "docs/note.txt")
        git(self.root, "commit", "--quiet", "-m", "plain")
        self.plain = git(self.root, "rev-parse", "HEAD")
        git(self.root, "update-index", "--add", "--cacheinfo", f"160000,{GITLINK_COMMIT},{GITLINK_PATH}")
        git(self.root, "commit", "--quiet", "-m", "gitlink")
        self.with_gitlink = git(self.root, "rev-parse", "HEAD")

    def tearDown(self) -> None:
        self.scratch.cleanup()

    def test_terminal_reader_still_refuses_a_gitlink(self) -> None:
        reader = parity.PinnedGitReader(self.root)
        self.assertEqual(["docs/note.txt"], reader.list_files(self.plain))
        with self.assertRaises(parity.ParityError) as raised:
            reader.list_files(self.with_gitlink)
        self.assertEqual("PARITY_AUTHORITY_MISMATCH", raised.exception.code)
        self.assertEqual({}, dict(reader.gitlinks))

    def test_opt_in_sets_the_gitlink_aside_and_never_lists_it_as_a_file(self) -> None:
        reader = parity.PinnedGitReader(self.root, allow_gitlinks=True)
        self.assertEqual(["docs/note.txt"], reader.list_files(self.with_gitlink))
        self.assertEqual({GITLINK_PATH: GITLINK_COMMIT}, dict(reader.gitlinks))
        self.assertEqual(b"synthetic\n", reader.read_file(self.with_gitlink, "docs/note.txt"))
        for read in (lambda: reader.entry(GITLINK_PATH), lambda: reader.read_file(self.with_gitlink, GITLINK_PATH)):
            with self.assertRaises(parity.ParityError) as raised:
                read()
            self.assertEqual("PARITY_AUTHORITY_MISMATCH", raised.exception.code)
        # A later capture without a gitlink leaves none behind.
        self.assertEqual(["docs/note.txt"], reader.list_files(self.plain))
        self.assertEqual({}, dict(reader.gitlinks))


if __name__ == "__main__":
    unittest.main()

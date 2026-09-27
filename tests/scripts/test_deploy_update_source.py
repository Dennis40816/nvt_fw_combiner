"""Behavior tests for the Windows update-source deployment wrapper."""

from __future__ import annotations

import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import textwrap
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "deploy-update-source.ps1"
POWERSHELL = shutil.which("pwsh") or shutil.which("powershell")
TEST_AREA = Path(os.environ["NFC_TEST_AREA_ROOT"])
WORK_ROOT = TEST_AREA / "work"


class DeployUpdateSourceTests(unittest.TestCase):
    package = b"immutable-release-package\x00\xff"
    version = "1.0.8"
    published_at = "2026-09-01T00:00:00Z"
    body = "NVT FW Combiner 1.0.8\n\nExact release notes."

    @classmethod
    def setUpClass(cls) -> None:
        if POWERSHELL is None:
            raise unittest.SkipTest("PowerShell is required")
        WORK_ROOT.mkdir(parents=True, exist_ok=True)

    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="deploy-update-source-", dir=WORK_ROOT
        )
        self.root = Path(self.temporary.name)
        self.source = self.root / "source"
        self.packages = self.source / "packages"
        self.packages.mkdir(parents=True)
        self.tools = self.root / "tools"
        self.tools.mkdir()
        self.release_package = self.root / "release.zip"
        self.release_package.write_bytes(self.package)
        self.metadata_path = self.root / "release.json"
        self.gh_log = self.root / "gh.jsonl"
        self.python_log = self.root / "python.jsonl"
        self._write_metadata()
        self._write_fake_tools()

    def tearDown(self) -> None:
        self.temporary.cleanup()

    @property
    def package_name(self) -> str:
        return f"NvtFwCombiner-v{self.version}-win-x64.zip"

    def _write_metadata(self, **overrides: object) -> None:
        document: dict[str, object] = {
            "tagName": f"v{self.version}",
            "isDraft": False,
            "isPrerelease": False,
            "body": self.body,
            "assets": [
                {
                    "name": self.package_name,
                    "size": len(self.package),
                    "digest": "sha256:" + hashlib.sha256(self.package).hexdigest(),
                }
            ],
        }
        document.update(overrides)
        self.metadata_path.write_text(json.dumps(document), encoding="utf-8")

    def _write_fake_tools(self) -> None:
        fake_gh = self.tools / "fake_gh.py"
        fake_gh.write_text(
            textwrap.dedent(
                """
                import json
                import os
                import shutil
                import sys
                from pathlib import Path

                args = sys.argv[1:]
                with Path(os.environ["FAKE_GH_LOG"]).open("a", encoding="utf-8") as log:
                    log.write(json.dumps(args) + "\\n")
                if args[:2] == ["auth", "status"]:
                    raise SystemExit(int(os.environ.get("FAKE_GH_AUTH_EXIT", "0")))
                if args[:2] == ["release", "view"]:
                    print(Path(os.environ["FAKE_GH_METADATA"]).read_text(encoding="utf-8"))
                    raise SystemExit(int(os.environ.get("FAKE_GH_VIEW_EXIT", "0")))
                if args[:2] == ["release", "download"]:
                    if os.environ.get("FAKE_GH_SWAP_SOURCE"):
                        source = Path(os.environ["FAKE_GH_SWAP_SOURCE"])
                        backup = source.with_name(source.name + ".original")
                        source.rename(backup)
                        (source / "packages").mkdir(parents=True)
                    destination = Path(args[args.index("--dir") + 1])
                    package = Path(os.environ["FAKE_GH_PACKAGE"])
                    if os.environ.get("FAKE_GH_REPLACE_TEMP"):
                        backup = destination.with_name(destination.name + ".attacker")
                        try:
                            destination.rename(backup)
                        except OSError:
                            Path(os.environ["FAKE_TEMP_ATTACK_LOG"]).write_text(
                                "blocked", encoding="utf-8"
                            )
                            raise SystemExit(44)
                        destination.mkdir()
                        Path(os.environ["FAKE_TEMP_ATTACK_LOG"]).write_text(
                            "replaced", encoding="utf-8"
                        )
                    shutil.copyfile(package, destination / os.environ["FAKE_PACKAGE_NAME"])
                    if os.environ.get("FAKE_GH_UNKNOWN_ENTRY"):
                        (destination / "unexpected.txt").write_text(
                            "unexpected", encoding="utf-8"
                        )
                    raise SystemExit(int(os.environ.get("FAKE_GH_DOWNLOAD_EXIT", "0")))
                raise SystemExit(97)
                """
            ).lstrip(),
            encoding="utf-8",
        )
        fake_python = self.tools / "fake_python.py"
        fake_python.write_text(
            textwrap.dedent(
                """
                import json
                import os
                import sys
                from pathlib import Path

                args = sys.argv[1:]
                note_arg = args[args.index("--release-notes-file") + 1]
                notes_path = Path(note_arg.split("=", 1)[1])
                source = Path(args[args.index("--source-root") + 1])
                policies = [
                    args[index + 1]
                    for index, value in enumerate(args)
                    if value == "--notification-policy"
                ]
                record = {
                    "args": args,
                    "notes": notes_path.read_text(encoding="utf-8"),
                    "packageReadable": (
                        source / "packages" / os.environ["FAKE_PACKAGE_NAME"]
                    ).read_bytes().hex(),
                    "policies": policies,
                }
                with Path(os.environ["FAKE_PYTHON_LOG"]).open(
                    "a", encoding="utf-8"
                ) as log:
                    log.write(json.dumps(record) + "\\n")
                required = os.environ.get("FAKE_REQUIRED_POLICIES")
                if required and policies != required.split("|"):
                    raise SystemExit(12)
                exit_code = int(os.environ.get("FAKE_PYTHON_EXIT", "0"))
                if exit_code == 0:
                    schema = args[args.index("--catalog-schema-version") + 1]
                    (source / f"update-catalog.v{schema}.json").write_text(
                        "{}", encoding="utf-8"
                    )
                raise SystemExit(exit_code)
                """
            ).lstrip(),
            encoding="utf-8",
        )
        interpreter = str(Path(sys.executable).resolve())
        (self.tools / "gh.cmd").write_text(
            f'@"{interpreter}" "%~dp0fake_gh.py" %*\r\n', encoding="utf-8"
        )
        (self.tools / "python.cmd").write_text(
            f'@"{interpreter}" "%~dp0fake_python.py" %*\r\n', encoding="utf-8"
        )

    def _environment(self, **overrides: str) -> dict[str, str]:
        environment = os.environ.copy()
        environment.update(
            {
                "PATH": str(self.tools) + os.pathsep + environment["PATH"],
                "TEMP": str(TEST_AREA / "temp"),
                "TMP": str(TEST_AREA / "temp"),
                "TMPDIR": str(TEST_AREA / "temp"),
                "FAKE_GH_METADATA": str(self.metadata_path),
                "FAKE_GH_PACKAGE": str(self.release_package),
                "FAKE_GH_LOG": str(self.gh_log),
                "FAKE_PYTHON_LOG": str(self.python_log),
                "FAKE_PACKAGE_NAME": self.package_name,
                "FAKE_TEMP_ATTACK_LOG": str(self.root / "temp-attack.txt"),
            }
        )
        environment.update(overrides)
        return environment

    def _run(
        self,
        *extra: str,
        environment: dict[str, str] | None = None,
        source: Path | str | None = None,
    ) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                str(POWERSHELL),
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                str(SCRIPT),
                "-Version",
                self.version,
                "-CatalogPublishedAtUtc",
                self.published_at,
                "-SourceRoot",
                str(source or self.source),
                "-Confirm:$false",
                *extra,
            ],
            cwd=ROOT,
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            env=environment or self._environment(),
        )

    def _gh_calls(self) -> list[list[str]]:
        if not self.gh_log.exists():
            return []
        return [json.loads(line) for line in self.gh_log.read_text().splitlines()]

    def _python_calls(self) -> list[dict[str, object]]:
        if not self.python_log.exists():
            return []
        return [json.loads(line) for line in self.python_log.read_text().splitlines()]

    def _assert_no_transients(self) -> None:
        names = {entry.name for entry in self.source.iterdir()} | {
            entry.name for entry in self.packages.iterdir()
        }
        self.assertFalse(
            any("custody" in name or name.endswith(".staging") for name in names), names
        )

    def test_what_if_downloads_and_validates_without_source_mutation(self) -> None:
        before = sorted(path.relative_to(self.source) for path in self.source.rglob("*"))
        result = self._run("-WhatIf")
        self.assertEqual(0, result.returncode, result.stderr)
        after = sorted(path.relative_to(self.source) for path in self.source.rglob("*"))
        self.assertEqual(before, after)
        self.assertEqual([], self._python_calls())
        self.assertEqual("auth", self._gh_calls()[0][0])
        self.assertEqual(
            [
                "release",
                "view",
                "v1.0.8",
                "--repo",
                "Dennis40816/nvt_fw_combiner",
                "--json",
                "tagName,isDraft,isPrerelease,body,assets",
            ],
            self._gh_calls()[1],
        )
        self.assertIn("Dennis40816/nvt_fw_combiner", self._gh_calls()[2])

    def test_new_package_is_admitted_and_existing_publisher_receives_exact_inputs(self) -> None:
        result = self._run()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(self.package, (self.packages / self.package_name).read_bytes())
        calls = self._python_calls()
        self.assertEqual(1, len(calls))
        self.assertEqual(self.body, calls[0]["notes"])
        self.assertEqual(self.package.hex(), calls[0]["packageReadable"])
        args = calls[0]["args"]
        self.assertEqual(str(ROOT / "scripts" / "create_update_catalog.py"), args[0])
        self.assertIn(f"{self.version}={self.published_at}", args)
        self.assertEqual([], calls[0]["policies"])
        self.assertTrue((self.source / "update-catalog.v1.json").is_file())
        self._assert_no_transients()

    def test_invalid_release_metadata_fails_before_download_or_source_mutation(self) -> None:
        invalid_documents = (
            {"tagName": "v1.0.9"},
            {"isDraft": True},
            {"isPrerelease": True},
            {"body": ""},
            {
                "assets": [
                    {
                        "name": self.package_name,
                        "size": len(self.package),
                        "digest": "sha256:" + hashlib.sha256(self.package).hexdigest(),
                    },
                    {
                        "name": self.package_name,
                        "size": len(self.package),
                        "digest": "sha256:" + hashlib.sha256(self.package).hexdigest(),
                    },
                ]
            },
            {
                "assets": [
                    {
                        "name": self.package_name,
                        "size": 0,
                        "digest": "sha256:" + "0" * 64,
                    }
                ]
            },
            {
                "assets": [
                    {
                        "name": self.package_name,
                        "size": len(self.package),
                        "digest": "sha256:" + "A" * 64,
                    }
                ]
            },
        )
        for overrides in invalid_documents:
            with self.subTest(overrides=overrides):
                self._write_metadata(**overrides)
                self.gh_log.unlink(missing_ok=True)
                result = self._run()
                self.assertNotEqual(0, result.returncode)
                self.assertEqual([], self._python_calls())
                self.assertFalse((self.packages / self.package_name).exists())
                self.assertFalse(
                    any(call[:2] == ["release", "download"] for call in self._gh_calls())
                )

    def test_downloaded_byte_mismatch_fails_without_source_mutation(self) -> None:
        self.release_package.write_bytes(b"different")
        result = self._run()
        self.assertNotEqual(0, result.returncode)
        self.assertIn("Downloaded package bytes", result.stderr)
        self.assertFalse((self.packages / self.package_name).exists())
        self.assertEqual([], self._python_calls())

    def test_existing_exact_package_is_reused_without_overwrite(self) -> None:
        destination = self.packages / self.package_name
        destination.write_bytes(self.package)
        before = destination.stat()
        result = self._run()
        self.assertEqual(0, result.returncode, result.stderr)
        after = destination.stat()
        self.assertEqual(before.st_ino, after.st_ino)
        self.assertEqual(before.st_mtime_ns, after.st_mtime_ns)
        self.assertEqual(self.package, destination.read_bytes())
        self._assert_no_transients()

    def test_existing_mismatching_package_is_never_overwritten(self) -> None:
        destination = self.packages / self.package_name
        destination.write_bytes(b"older-conflicting-bytes")
        result = self._run()
        self.assertNotEqual(0, result.returncode)
        self.assertEqual(b"older-conflicting-bytes", destination.read_bytes())
        self.assertEqual([], self._python_calls())
        self._assert_no_transients()

    def test_helper_failure_retains_admitted_package_as_unreferenced_orphan(self) -> None:
        result = self._run(environment=self._environment(FAKE_PYTHON_EXIT="23"))
        self.assertNotEqual(0, result.returncode)
        self.assertIn("Retain and report", result.stderr)
        self.assertEqual(self.package, (self.packages / self.package_name).read_bytes())
        self.assertFalse((self.source / "update-catalog.v1.json").exists())
        self._assert_no_transients()

    def test_v1_rejects_notification_policy_before_any_external_or_source_action(self) -> None:
        result = self._run("-NotificationPolicy", "1.0.8=notify")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("does not accept", result.stderr)
        self.assertEqual([], self._gh_calls())
        self.assertFalse((self.packages / self.package_name).exists())

    def test_v2_forwards_complete_explicit_policies_without_guessing(self) -> None:
        old_name = "NvtFwCombiner-v1.0.7-win-x64.zip"
        (self.packages / old_name).write_bytes(b"old")
        policies = ["1.0.7=notify", "1.0.8=manual-only"]
        command = (
            f"& '{SCRIPT}' -Version '{self.version}' "
            f"-CatalogPublishedAtUtc '{self.published_at}' "
            f"-SourceRoot '{self.source}' -CatalogSchemaVersion 2 "
            "-NotificationPolicy @('1.0.7=notify','1.0.8=manual-only') -Confirm:$false"
        )
        result = subprocess.run(
            [str(POWERSHELL), "-NoProfile", "-Command", command],
            cwd=ROOT,
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            env=self._environment(FAKE_REQUIRED_POLICIES="|".join(policies)),
        )
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(policies, self._python_calls()[0]["policies"])
        self.assertTrue((self.source / "update-catalog.v2.json").exists())

    def test_source_identity_swap_during_download_is_rejected(self) -> None:
        result = self._run(
            environment=self._environment(FAKE_GH_SWAP_SOURCE=str(self.source))
        )
        self.assertNotEqual(0, result.returncode)
        self.assertIn("identity changed", result.stderr)
        self.assertEqual([], self._python_calls())
        replacement = self.source
        original = self.source.with_name(self.source.name + ".original")
        (replacement / "packages").rmdir()
        replacement.rmdir()
        original.rename(replacement)

    def test_reparse_point_in_source_chain_is_rejected(self) -> None:
        target = self.root / "target"
        (target / "packages").mkdir(parents=True)
        link = self.root / "linked-source"
        try:
            os.symlink(target, link, target_is_directory=True)
        except OSError as exception:
            self.skipTest(f"directory symlink unavailable: {exception}")
        try:
            result = self._run(source=link)
            self.assertNotEqual(0, result.returncode)
            self.assertIn("reparse point", result.stderr)
            self.assertEqual([], self._gh_calls())
        finally:
            link.unlink(missing_ok=True)

    def test_locked_destination_refuses_stable_package_custody(self) -> None:
        destination = self.packages / self.package_name
        destination.write_bytes(self.package)
        locker = subprocess.Popen(
            [
                str(POWERSHELL),
                "-NoProfile",
                "-Command",
                (
                    "$s=[IO.File]::Open('"
                    + str(destination).replace("'", "''")
                    + "',[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None);"
                    + "[Console]::Out.WriteLine('ready');[Console]::Out.Flush();"
                    + "[Console]::In.ReadLine()|Out-Null;$s.Dispose()"
                ),
            ],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
        try:
            self.assertEqual("ready", locker.stdout.readline().strip())
            result = self._run()
            self.assertNotEqual(0, result.returncode)
            self.assertEqual([], self._python_calls())
        finally:
            locker.communicate("done\n", timeout=10)
        self.assertEqual(self.package, destination.read_bytes())

    def test_directory_that_refuses_custody_file_creation_fails_before_staging(self) -> None:
        icacls = shutil.which("icacls")
        if icacls is None:
            self.skipTest("icacls is required for the directory-custody refusal test")
        sid_result = subprocess.run(
            [
                str(POWERSHELL),
                "-NoProfile",
                "-Command",
                "[Security.Principal.WindowsIdentity]::GetCurrent().User.Value",
            ],
            check=True,
            capture_output=True,
            text=True,
            encoding="utf-8",
        )
        sid = sid_result.stdout.strip()
        deny = subprocess.run(
            [icacls, str(self.source), "/deny", f"*{sid}:(WD)", "/q"],
            check=False,
            capture_output=True,
            text=True,
        )
        if deny.returncode != 0:
            self.skipTest(f"could not install temporary create-file denial: {deny.stderr}")
        try:
            result = self._run()
            self.assertNotEqual(0, result.returncode)
            self.assertEqual([], self._python_calls())
            self.assertFalse((self.packages / self.package_name).exists())
            self.assertFalse(any("custody" in path.name for path in self.source.iterdir()))
        finally:
            restore = subprocess.run(
                [icacls, str(self.source), "/remove:d", f"*{sid}", "/q"],
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(0, restore.returncode, restore.stderr)

    def test_drive_and_unc_share_roots_are_rejected_before_external_calls(self) -> None:
        for source_root in (self.source.anchor, "\\\\server\\share\\"):
            with self.subTest(source_root=source_root):
                self.gh_log.unlink(missing_ok=True)
                result = self._run(source=source_root)
                self.assertNotEqual(0, result.returncode)
                self.assertIn("must be below", result.stderr)
                self.assertEqual([], self._gh_calls())

    def test_temp_root_replacement_attempt_is_blocked_without_final_admission(self) -> None:
        result = self._run(environment=self._environment(FAKE_GH_REPLACE_TEMP="1"))
        self.assertNotEqual(0, result.returncode)
        self.assertEqual("blocked", (self.root / "temp-attack.txt").read_text())
        self.assertFalse((self.packages / self.package_name).exists())

    def test_unknown_temp_entry_preserves_exact_root_and_reports_it(self) -> None:
        result = self._run(environment=self._environment(FAKE_GH_UNKNOWN_ENTRY="1"))
        self.assertNotEqual(0, result.returncode)
        self.assertIn("preserved for inspection", result.stderr)
        download_call = next(
            call for call in self._gh_calls() if call[:2] == ["release", "download"]
        )
        temp_root = Path(download_call[download_call.index("--dir") + 1])
        self.assertTrue(temp_root.is_dir())
        self.assertEqual(
            {self.package_name, "unexpected.txt"},
            {entry.name for entry in temp_root.iterdir()},
        )
        (temp_root / self.package_name).unlink()
        (temp_root / "unexpected.txt").unlink()
        temp_root.rmdir()

    def test_mutation_after_first_hash_check_is_rejected_by_second_hash_check(
        self,
    ) -> None:
        """Deterministically prove the post-hash attack window from
        BUG-20260927-deploy-post-hash-attack-test-race: a mutation landing
        strictly after the script's first hash check on the just-downloaded
        package (the "Downloaded package bytes..." check) and strictly
        before its second hash check just before staging (the "verified
        downloaded package" check) must still be rejected, and nothing may
        be admitted.

        This uses a PowerShell script-debugger breakpoint
        (Set-PSBreakpoint -Action) as a test-only synchronization seam --
        no attacker subprocess, no lock polling, no sleeps, no timing
        threshold, and no change to deploy-update-source.ps1. The
        breakpoint is set on the exact source line that runs immediately
        after the first hash stream has been read and disposed; its action
        runs synchronously, inside the very PowerShell process executing
        the script, before that line resumes, and mutates the downloaded
        file's bytes in place. The mutation therefore always lands in the
        intended window, every run: the assertion on the target line's
        exact text below fails loudly (instead of silently no-op'ing the
        breakpoint) if the script is ever restructured there.

        Because the first check already holds the hash and length it
        computed in memory before the mutation, it cannot see the change;
        only the second check, which re-reads the file from disk, can
        catch it. A deterministic failure here is proof that the second
        check does that job.
        """
        target_line = 443
        expected_line_text = "$DownloadedIdentity = $DownloadedEvidence.Identity"
        script_lines = SCRIPT.read_text(encoding="utf-8").splitlines()
        actual_line_text = script_lines[target_line - 1].strip()
        self.assertEqual(
            expected_line_text,
            actual_line_text,
            "deploy-update-source.ps1 moved the post-first-hash-check line; "
            "update target_line/expected_line_text in this test to match "
            "whatever line now runs immediately after the first hash "
            "stream ($DownloadedStream) is disposed.",
        )

        breakpoint_log = self.root / "breakpoint-fired.txt"
        script_literal = str(SCRIPT).replace("'", "''")
        source_literal = str(self.source).replace("'", "''")
        breakpoint_log_literal = str(breakpoint_log).replace("'", "''")
        command = textwrap.dedent(
            f"""
            $ErrorActionPreference = 'Stop'
            Set-PSBreakpoint -Script '{script_literal}' -Line {target_line} -Action {{
                $path = Get-Variable -Name DownloadedPath -ValueOnly -ErrorAction Stop
                [IO.File]::WriteAllBytes(
                    $path,
                    [Text.Encoding]::ASCII.GetBytes('post-hash-mutation'))
                Add-Content -LiteralPath '{breakpoint_log_literal}' -Value 'fired'
            }} | Out-Null
            & '{script_literal}' -Version '{self.version}' -CatalogPublishedAtUtc '{self.published_at}' -SourceRoot '{source_literal}' -Confirm:$false
            """
        )
        result = subprocess.run(
            [
                str(POWERSHELL),
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-Command",
                command,
            ],
            cwd=ROOT,
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            env=self._environment(),
        )
        self.assertTrue(
            breakpoint_log.exists(),
            "the synchronization breakpoint never fired -- the post-hash "
            "attack window was not exercised at all:\n" + result.stderr,
        )
        self.assertNotEqual(0, result.returncode, result.stderr)
        self.assertIn("verified downloaded package", result.stderr)
        self.assertEqual([], self._python_calls())
        self.assertFalse((self.packages / self.package_name).exists())


if __name__ == "__main__":
    unittest.main()

"""Tree identity and pester-free assembly/refusal policy tests."""
from contextlib import contextmanager
from copy import deepcopy
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

from scripts.golden import golden_pin
from scripts.golden.golden_layout import copy_pinned_tree

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "scripts/golden"
PRIVATE_NAME = "SYNTHETIC_PRIVATE_NAME_39BD.bin"


class GoldenPinTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.work = Path(self.temporary.name)
        self.root = self.work / "repository"
        self.golden = self.root / "testdata/golden"
        self.references = self.root / "docs/references"
        self.golden.mkdir(parents=True)
        self.references.mkdir(parents=True)
        (self.golden / PRIVATE_NAME).write_bytes(b"synthetic-1")
        (self.golden / "z.txt").write_bytes(b"synthetic-2")
        (self.references / "unicode-é.txt").write_bytes(b"synthetic-3")
        self.document = json.loads((ROOT / "scripts/ci/test-baseline.json").read_text())
        self.pin = self.document["pin"]
        self.pin.update(golden_pin.compute(self.root, self.pin["paths"]))
        self.pin["assetsCommit"] = "a" * 40
        self.baseline = self.work / "baseline.json"
        self.save_baseline()
        self.environment = os.environ.copy()
        for key in ("GITHUB_EVENT_NAME", "GITHUB_ACTOR", "GITHUB_EVENT_PATH", "GITHUB_REPOSITORY",
                    "NFC_ASSETS_KEY_FILE", "RUNNER_TEMP"):
            self.environment.pop(key, None)

    def save_baseline(self):
        self.baseline.write_text(json.dumps(self.document, indent=2) + "\n", encoding="utf-8")

    def remove_generated_copy(self, directory):
        if not directory.exists():
            return
        golden_pin.reject_links(directory)
        target, boundary = directory.resolve(strict=True), self.work.resolve(strict=True)
        if target == boundary or not target.is_relative_to(boundary):
            raise ValueError("Generated test copy cleanup failed.")
        shutil.rmtree(golden_pin.filesystem_path(target))

    def powershell(self, script, *arguments, environment=None):
        result = subprocess.run(["pwsh", "-NoProfile", "-File", str(SCRIPTS / script), *map(str, arguments)],
                                capture_output=True, text=True, check=False,
                                env=self.environment if environment is None else environment)
        for value in (PRIVATE_NAME, str(self.work), "synthetic-1"):
            self.assertNotIn(value, result.stdout + result.stderr)
        return result

    def test_hash_definition_and_utf8_ordinal_order_independent_of_enumeration(self):
        lines = []
        for relative, content in sorted((
            (f"testdata/golden/{PRIVATE_NAME}", b"synthetic-1"),
            ("testdata/golden/z.txt", b"synthetic-2"),
            ("docs/references/unicode-é.txt", b"synthetic-3")), key=lambda item: item[0].encode("utf-8")):
            lines.append(f"{hashlib.sha256(content).hexdigest()} {len(content)} {relative}\n".encode("utf-8"))
        expected = dict(fileCount=3, treeSha256=hashlib.sha256(b"".join(lines)).hexdigest())
        self.assertEqual(expected, golden_pin.compute(self.root, self.pin["paths"]))
        original_scandir = os.scandir

        @contextmanager
        def reversed_entries(path):
            with original_scandir(path) as entries:
                yield iter(reversed(list(entries)))

        with patch.object(golden_pin.os, "scandir", side_effect=reversed_entries):
            self.assertEqual(expected, golden_pin.compute(self.root, tuple(reversed(self.pin["paths"]))))

    def test_copy_and_verify_support_long_destination_paths(self):
        destination = self.work.joinpath(*(["long-destination-segment"] * 12))
        self.addCleanup(self.remove_generated_copy, self.work / "long-destination-segment")
        self.assertEqual(golden_pin.compute(self.root, self.pin["paths"]),
                         copy_pinned_tree(self.root, destination, self.pin))
        self.assertEqual(golden_pin.compute(self.root, self.pin["paths"]),
                         golden_pin.compute(destination, self.pin["paths"]))

    def test_default_root_entrypoints_normalize_parent_segments(self):
        result = self.powershell("assemble-golden.ps1", "-Mode", "InTree")
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        destination = self.work / "default-export"
        self.addCleanup(self.remove_generated_copy, destination)
        result = self.powershell("export-assets-layout.ps1", "-Destination", destination)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        real_pin = golden_pin.load_pin(golden_pin.DEFAULT_BASELINE)
        self.assertEqual({key: real_pin[key] for key in ("fileCount", "treeSha256")},
                         golden_pin.verify(destination / real_pin["assetsPath"], real_pin))

    def test_external_coverlet_cache_is_available_inside_snapshot_boundary(self):
        from scripts.golden import run_full
        cache = self.work / "package-cache"
        version = run_full.verifier.load_baseline()["collection"]["dotnet"]["version"]
        adapter = cache / "coverlet.collector" / version / "build/netstandard2.0"
        adapter.mkdir(parents=True)
        for name in ("coverlet.collector.dll", "coverlet.collector.deps.json"):
            (adapter / name).write_bytes(b"synthetic-adapter")
        snapshot = self.work / "snapshot"
        run_full.seed_coverlet_adapter(cache, snapshot)
        actual = run_full.verifier.resolve_coverlet_adapter_path(snapshot)
        self.assertEqual(adapter.name, actual.name)
        self.assertEqual((adapter / "coverlet.collector.dll").read_bytes(),
                         (actual / "coverlet.collector.dll").read_bytes())

    def test_change_remove_add_and_rename_each_fail_without_names(self):
        original = golden_pin.compute(self.root, self.pin["paths"])
        for change in ("byte", "removed", "added", "renamed"):
            target = self.work / change
            shutil.copytree(self.root, target)
            file = target / "testdata/golden" / PRIVATE_NAME
            if change == "byte":
                payload = file.read_bytes()
                file.write_bytes(bytes([payload[0] ^ 1]) + payload[1:])
            elif change == "removed":
                file.unlink()
            elif change == "added":
                (file.parent / "extra.txt").write_bytes(b"synthetic-extra")
            else:
                file.rename(file.with_name("renamed.bin"))
            self.assertNotEqual(original, golden_pin.compute(target, self.pin["paths"]))
            with self.assertRaisesRegex(ValueError, r"^Golden pin verification failed\.$") as caught:
                golden_pin.verify(target, self.pin)
            self.assertNotIn(PRIVATE_NAME, str(caught.exception))

    def test_path_escape_and_nonregular_entry_are_rejected(self):
        for paths in (("../outside",), ("/absolute",), ("testdata/golden", "testdata/golden/nested")):
            with self.assertRaisesRegex(ValueError, r"^Golden tree is missing or invalid\.$"):
                golden_pin.compute(self.root, paths)
        entry = unittest.mock.Mock()
        entry.stat.return_value.st_mode = 0
        entry.stat.return_value.st_file_attributes = 0
        entry.is_symlink.return_value = False

        @contextmanager
        def nonregular_entries(path):
            yield iter([entry])

        with patch.object(golden_pin.os, "scandir", side_effect=nonregular_entries):
            with self.assertRaisesRegex(ValueError, r"^Golden tree is missing or invalid\.$"):
                golden_pin.compute(self.root, self.pin["paths"])
        entry.is_symlink.return_value = True
        with patch.object(golden_pin.os, "scandir", side_effect=nonregular_entries):
            with self.assertRaisesRegex(ValueError, r"^Golden tree is missing or invalid\.$"):
                golden_pin.compute(self.root, self.pin["paths"])

    def test_write_pin_changes_only_two_tokens_and_preserves_layout(self):
        self.pin["fileCount"] = 0
        self.pin["treeSha256"] = "0" * 64
        original = (json.dumps(self.document, indent=4) + "\n").replace("\n", "\r\n").encode("utf-8")
        self.baseline.write_bytes(original)
        result = golden_pin.write_pin(self.root, self.baseline)
        expected = original.replace(b'"fileCount": 0', f'"fileCount": {result["fileCount"]}'.encode())
        expected = expected.replace(('"treeSha256": "' + "0" * 64 + '"').encode(),
                                    f'"treeSha256": "{result["treeSha256"]}"'.encode())
        self.assertEqual(expected, self.baseline.read_bytes())
        after = json.loads(self.baseline.read_bytes())
        before = deepcopy(self.document)
        before["pin"].update(result)
        self.assertEqual(before, after)

    def test_real_public_tree_matches_committed_pin(self):
        pin = golden_pin.load_pin(ROOT / "scripts/ci/test-baseline.json")
        self.assertEqual({key: pin[key] for key in ("fileCount", "treeSha256")}, golden_pin.verify(ROOT, pin))
        self.assertEqual("0" * 40, pin["assetsCommit"])

    def test_export_and_from_directory_equal_pin_and_corrupt_sources_fail(self):
        export = self.work / "export"
        result = self.powershell("export-assets-layout.ps1", "-Root", self.root,
                                 "-Destination", export, "-BaselinePath", self.baseline)
        self.assertEqual(0, result.returncode, result.stderr)
        destination = self.work / "assembled"
        result = self.powershell("assemble-golden.ps1", "-Mode", "FromDirectory", "-Source", export,
                                 "-Root", destination, "-BaselinePath", self.baseline)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(golden_pin.compute(self.root, self.pin["paths"]),
                         golden_pin.compute(destination, self.pin["paths"]))
        for change in ("byte", "removed"):
            damaged = self.work / ("damaged-" + change)
            shutil.copytree(export, damaged)
            file = damaged / self.pin["assetsPath"] / "testdata/golden" / PRIVATE_NAME
            if change == "byte":
                file.write_bytes(b"X" + file.read_bytes()[1:])
            else:
                file.unlink()
            absent_destination = self.work / ("rejected-" + change)
            result = self.powershell("assemble-golden.ps1", "-Mode", "FromDirectory", "-Source", damaged,
                                     "-Root", absent_destination, "-BaselinePath", self.baseline)
            self.assertEqual(1, result.returncode)
            self.assertEqual("Golden assembly failed.\n", result.stderr)
            self.assertFalse(absent_destination.exists())

    def assert_powershell_refusal(self, *, mode, expected, environment=None, source=None):
        """Plain PowerShell assertions in a child script; no Pester or clone call."""
        assertion = self.work / "assert-refusal.ps1"
        assertion.write_text('''param([string]$Assembly, [string]$Baseline, [string]$Mode,
    [string]$Expected, [string]$Source)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$arguments = @('-NoProfile', '-File', $Assembly, '-Mode', $Mode, '-BaselinePath', $Baseline)
if ($Source) { $arguments += @('-Source', $Source) }
$output = @(& pwsh @arguments 2>&1)
if ($LASTEXITCODE -eq 0) { throw 'Unexpected successful assembly.' }
$text = ($output | ForEach-Object { [string]$_ }) -join "`n"
if ($text.Trim() -cne $Expected) { throw 'Unexpected refusal message.' }
[Console]::WriteLine('Refusal assertions passed.')
''', encoding="utf-8")
        result = subprocess.run(["pwsh", "-NoProfile", "-File", str(assertion), "-Assembly",
                                 str(SCRIPTS / "assemble-golden.ps1"), "-Baseline", str(self.baseline),
                                 "-Mode", mode, "-Expected", expected, "-Source", str(source or "")],
                                capture_output=True, text=True, check=False,
                                env=self.environment if environment is None else environment)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual("Refusal assertions passed.\n", result.stdout)

    def test_zero_commit_refused_in_both_private_modes_and_intree_allowed(self):
        self.pin["assetsCommit"] = "0" * 40
        self.save_baseline()
        for mode in ("Clone", "FromDirectory"):
            self.assert_powershell_refusal(mode=mode, expected=golden_pin.UNBOUND_PIN,
                                           source=self.work if mode == "FromDirectory" else None)
        result = self.powershell("assemble-golden.ps1", "-Root", self.root, "-BaselinePath", self.baseline)
        self.assertEqual(0, result.returncode, result.stderr)

    def test_missing_key_invalid_arguments_and_fork_refused_before_clone(self):
        self.assert_powershell_refusal(mode="Clone", expected="Assets key variable is missing.")
        self.assert_powershell_refusal(mode="InvalidMode", expected="Golden assembly arguments are invalid.")
        self.assert_powershell_refusal(mode="FromDirectory", expected="Golden assembly arguments are invalid.")
        event = self.work / "fork-event.json"
        event.write_text(json.dumps({"pull_request": {"head": {"repo": {"full_name": "fork/repository"}},
                                                      "user": {"login": "contributor"}}}))
        environment = dict(self.environment, GITHUB_EVENT_NAME="pull_request", GITHUB_EVENT_PATH=str(event),
                           GITHUB_REPOSITORY="upstream/repository")
        self.assert_powershell_refusal(mode="Clone", expected="unverified, cannot merge", environment=environment)
        environment = dict(self.environment, GITHUB_ACTOR="dependabot[bot]")
        self.assert_powershell_refusal(mode="Clone", expected="unverified, cannot merge", environment=environment)

    def test_argument_errors_do_not_echo_the_offending_argument(self):
        private_name = "SYNTHETIC_PRIVATE_CASE_74D9.bin"
        for script in ("scripts/golden/golden_pin.py", "scripts/golden/golden_layout.py",
                       "scripts/golden/run_full.py", "scripts/ci/check_test_shard.py",
                       "scripts/ci/update_test_baseline.py"):
            with self.subTest(script=script):
                result = subprocess.run([sys.executable, "-B", script, "--no-such-option", private_name],
                                        cwd=ROOT, capture_output=True, text=True, check=False)
                self.assertEqual(2, result.returncode)
                self.assertNotIn(private_name, result.stdout + result.stderr)
                self.assertNotIn("no-such-option", result.stdout + result.stderr)

    def test_full_run_missing_test_area_is_fixed(self):
        environment = self.environment.copy()
        environment.pop("NFC_TEST_AREA_ROOT", None)
        result = self.powershell("run-full.ps1", environment=environment)
        self.assertEqual(1, result.returncode)
        self.assertEqual("NFC_TEST_AREA_ROOT is not set.\n", result.stderr)


if __name__ == "__main__":
    unittest.main()

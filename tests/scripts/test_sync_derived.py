"""Behavioral tests for the one local derived-file synchronization writer."""

import hashlib
import io
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest.mock import patch

from scripts import sync_derived as sync


class DerivedSyncTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        (self.root / "source.txt").write_bytes(b"new\n")
        (self.root / "target.txt").write_bytes(b"old\n")
        self.provider = sync.Provider(
            "copy",
            ("source.txt", "target.txt"),
            ("target.txt",),
            lambda before: {"target.txt": before["source.txt"]},
        )
        environment = patch.dict(os.environ, {"CI": "", "GITHUB_ACTIONS": ""})
        environment.start()
        self.addCleanup(environment.stop)

    def run_sync(self, providers=None, write=False):
        with redirect_stdout(io.StringIO()):
            return sync.synchronize(
                self.root, providers or [self.provider], write=write
            )

    def test_check_is_read_only_and_write_is_idempotent(self):
        self.assertEqual(1, self.run_sync())
        self.assertEqual(b"old\n", (self.root / "target.txt").read_bytes())
        self.assertEqual(0, self.run_sync(write=True))
        self.assertEqual(b"new\n", (self.root / "target.txt").read_bytes())
        with patch.object(
            sync.os, "replace", side_effect=AssertionError("no-op must not write")
        ):
            self.assertEqual(0, self.run_sync(write=True))
        self.assertEqual(0, self.run_sync())

    def test_every_provider_plans_before_any_write(self):
        def fail(_):
            raise RuntimeError("invalid second projection")

        provider = sync.Provider("bad", ("source.txt",), (), fail)
        with self.assertRaisesRegex(RuntimeError, "invalid second"):
            self.run_sync([self.provider, provider], write=True)
        self.assertEqual(b"old\n", (self.root / "target.txt").read_bytes())

    def test_conflicting_owners_and_undeclared_outputs_are_rejected(self):
        with self.assertRaises(sync.SyncError):
            self.run_sync([self.provider, self.provider], write=True)
        invalid = sync.Provider("invalid", self.provider.inputs, (), self.provider.plan)
        with self.assertRaises(sync.SyncError):
            self.run_sync([invalid], write=True)
        self.assertEqual(b"old\n", (self.root / "target.txt").read_bytes())

    def test_case_aliases_are_rejected_before_either_writer_runs(self):
        alias = sync.Provider(
            "alias",
            ("source.txt", "TARGET.txt"),
            ("TARGET.txt",),
            lambda before: {"TARGET.txt": before["source.txt"]},
        )
        with self.assertRaises(sync.SyncError):
            self.run_sync([self.provider, alias], write=True)
        self.assertEqual(b"old\n", (self.root / "target.txt").read_bytes())

    def test_escaping_path_is_rejected_before_any_write(self):
        for path in (
            "../outside.txt",
            "/absolute.txt",
            "C:/absolute.txt",
            "folder\\file.txt",
        ):
            with self.subTest(path=path), self.assertRaises(sync.SyncError):
                provider = sync.Provider(
                    "escape", (path,), (path,), lambda _: {path: b"bad"}
                )
                self.run_sync([provider], write=True)

    def test_source_race_is_rejected_before_any_write(self):
        def racing_plan(before):
            (self.root / "source.txt").write_bytes(b"concurrent edit\n")
            return {"target.txt": before["source.txt"]}

        provider = sync.Provider(
            "race", self.provider.inputs, self.provider.outputs, racing_plan
        )
        with self.assertRaises(sync.SyncError):
            self.run_sync([provider], write=True)
        self.assertEqual(b"old\n", (self.root / "target.txt").read_bytes())

    def test_ci_write_is_rejected_but_check_still_reports_drift(self):
        with patch.dict(os.environ, {"CI": "true"}):
            with self.assertRaises(sync.SyncError):
                self.run_sync(write=True)
            self.assertEqual(1, self.run_sync())

    def test_nonconverging_provider_cannot_claim_success(self):
        provider = sync.Provider(
            "append",
            self.provider.inputs,
            self.provider.outputs,
            lambda before: {"target.txt": before["target.txt"] + b"x"},
        )
        with self.assertRaises(sync.SyncError):
            self.run_sync([provider], write=True)
        self.assertEqual(b"old\n", (self.root / "target.txt").read_bytes())

    def test_reparse_input_is_rejected_without_reading_or_writing_payloads(self):
        with patch.object(Path, "is_symlink", return_value=True):
            with self.assertRaisesRegex(sync.SyncError, "reparse"):
                self.run_sync(write=True)
        self.assertEqual(b"old\n", (self.root / "target.txt").read_bytes())

    def test_formal_structure_gate_checks_derived_files_before_expensive_validation(
        self,
    ):
        from scripts import verify

        with patch.object(verify, "run") as run:
            verify.verify_structure()
        self.assertEqual("scripts/sync_derived.py", run.call_args_list[0].args[0][1])
        with patch.object(
            verify, "run", side_effect=RuntimeError("derived drift")
        ) as run:
            with self.assertRaisesRegex(RuntimeError, "derived drift"):
                verify.verify_structure()
            self.assertEqual(1, run.call_count)

    def test_cli_write_requires_explicit_provider_selection(self):
        with self.assertRaisesRegex(sync.SyncError, "--only"):
            sync.main(["--write", "--repository", str(self.root)])

    def test_fixed_registry_has_no_arbitrary_target_or_publisher(self):
        self.assertEqual(
            {
                "v0916-workflow-contract",
                "ci-template-mirror",
                "reviewed-source-pins",
                "release-version-headers",
            },
            {provider.name for provider in sync.default_providers()},
        )

    def test_ci_template_is_an_exact_non_executable_mirror(self):
        provider = next(
            p for p in sync.default_providers() if p.name == "ci-template-mirror"
        )
        for relative in provider.inputs:
            path = self.root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(
                b"source\r\n" if relative.startswith(".github/") else b"stale\n"
            )
        self.assertEqual(0, self.run_sync([provider], write=True))
        self.assertEqual(b"source\r\n", (self.root / provider.outputs[0]).read_bytes())


class ReleaseVersionHeadersTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.original = {
            "VERSION": b"1.1.3\n",
            "SPEC.md": (
                "# Specification\r\n> 文件版本：`1.1.2`\r\n"
                "> 文件狀態：`1.1.2 historical candidate`\r\n"
                "> 基準日期：`2026-09-04`\r\n"
            ).encode(),
            "docs/references/verification-report.md": (
                b"# Report\nSpecification package version: `1.1.2`\n"
                b"Historical 1.1.2 result. Approval remains for 1.1.2.\n"
            ),
        }
        for relative, raw in self.original.items():
            self.write(relative, raw)
        environment = patch.dict(os.environ, {"CI": "", "GITHUB_ACTIONS": ""})
        environment.start()
        self.addCleanup(environment.stop)

    def write(self, relative, raw):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)

    def run_cli(self, *arguments):
        with redirect_stdout(io.StringIO()):
            return sync.main(
                [
                    "--repository",
                    str(self.root),
                    "--only",
                    "release-version-headers",
                    *arguments,
                ]
            )

    def test_stale_check_explicit_write_preserves_all_other_bytes_and_is_idempotent(
        self,
    ):
        self.assertEqual(1, self.run_cli())
        for path, raw in self.original.items():
            self.assertEqual(raw, (self.root / path).read_bytes())
        self.assertEqual(0, self.run_cli("--write"))
        for path, raw in self.original.items():
            expected = (
                raw if path == "VERSION" else raw.replace(b"`1.1.2`", b"`1.1.3`", 1)
            )
            self.assertEqual(expected, (self.root / path).read_bytes())
        self.assertEqual(0, self.run_cli())
        with patch.object(
            sync.os, "replace", side_effect=AssertionError("no-op wrote")
        ):
            self.assertEqual(0, self.run_cli("--write"))

    def test_invalid_versions_and_headers_fail_before_any_write(self):
        for path, raw in self.original.items():
            replacements = (
                (b"1.01.3\n", b"v1.1.3\n", b"1.1.3-rc.1\n", b"bad\n")
                if path == "VERSION"
                else (
                    b"# Missing header\n",
                    raw + raw,
                    raw.replace(b"`1.1.2`", b"`bad`", 1),
                    raw.replace(b"`1.1.2`", b"1.1.2", 1),
                )
            )
            for invalid in replacements:
                with self.subTest(path=path, invalid=invalid):
                    self.write(path, invalid)
                    with patch.object(sync.os, "replace") as replace:
                        with self.assertRaises(sync.SyncError):
                            self.run_cli("--write")
                        replace.assert_not_called()
                    for other, original in self.original.items():
                        self.assertEqual(
                            invalid if other == path else original,
                            (self.root / other).read_bytes(),
                        )
                    self.write(path, raw)

    def test_version_provider_remains_read_only_in_ci(self):
        with patch.dict(os.environ, {"GITHUB_ACTIONS": "true"}):
            self.assertEqual(1, self.run_cli())
            with self.assertRaisesRegex(sync.SyncError, "cannot write in CI"):
                self.run_cli("--write")
        for path, raw in self.original.items():
            self.assertEqual(raw, (self.root / path).read_bytes())

    def version_validation_fixture(self):
        repository = Path(__file__).resolve().parents[2]
        with patch.object(sys, "path", [str(repository / "scripts"), *sys.path]):
            import validate_repository as validator

        for path in (
            "Directory.Build.props",
            "LICENSE",
            "global.json",
            "scripts/install-dotnet.ps1",
            "scripts/install-dotnet.sh",
        ):
            self.write(path, (repository / path).read_bytes())
        self.write("SPEC.md", "> 文件版本：`1.1.3`\n".encode())
        self.write(
            "docs/references/verification-report.md",
            b"Specification package version: `1.1.3`\n",
        )
        self.write("CHANGELOG.md", b"## [1.1.3] - Unreleased\n")
        self.write(
            "docs/governance/development-tags.md", b"# Historical tags\n- v1.1.2\n"
        )
        return validator

    def test_current_version_does_not_require_a_fictitious_historical_tag_node(self):
        validator = self.version_validation_fixture()
        errors = []
        with patch.object(validator, "ROOT", self.root):
            validator.validate_version_license_and_sdk(errors)
        self.assertEqual([], errors)

    def test_removing_tag_text_gate_preserves_real_version_and_sdk_gates(self):
        validator = self.version_validation_fixture()
        for path, invalid, expected in (
            ("VERSION", b"bad\n", "invalid VERSION value"),
            ("SPEC.md", b"stale\n", "SPEC.md document version disagree"),
            (
                "docs/references/verification-report.md",
                b"stale\n",
                "verification-report version disagree",
            ),
            ("CHANGELOG.md", b"stale\n", "no changelog section"),
            ("global.json", b'{"sdk":{"version":"9.0.100"}}', "stable .NET 10 SDK"),
            (
                "Directory.Build.props",
                b"<Project />",
                "internal project-reference version stable",
            ),
        ):
            with self.subTest(path=path):
                original = (self.root / path).read_bytes()
                self.write(path, invalid)
                errors = []
                with patch.object(validator, "ROOT", self.root):
                    validator.validate_version_license_and_sdk(errors)
                self.assertTrue(any(expected in error for error in errors), errors)
                self.write(path, original)


class ReviewedSourcePinsTests(unittest.TestCase):
    def setUp(self):
        self.provider = next(
            p for p in sync.default_providers() if p.name == "reviewed-source-pins"
        )
        root = Path(__file__).resolve().parents[2]
        self.before = {
            relative: (root / relative).read_bytes()
            for relative in self.provider.inputs
        }
        self.allowlist = "testdata/golden/release-canonical-v1.json"
        self.before[self.allowlist] = (root / self.allowlist).read_bytes()
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        environment = patch.dict(os.environ, {"CI": "", "GITHUB_ACTIONS": ""})
        environment.start()
        self.addCleanup(environment.stop)

    def write_fixture(self, before):
        for relative, raw in before.items():
            path = self.root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)

    def run_sync(self, *, write=False):
        with redirect_stdout(io.StringIO()):
            return sync.synchronize(self.root, [self.provider], write=write)

    def test_current_pins_are_clean(self):
        self.assertEqual(
            {p: self.before[p] for p in self.provider.outputs},
            self.provider.plan(self.before),
        )

    def test_three_source_changes_update_all_four_consumers_without_other_rewrites(
        self,
    ):
        changed = dict(self.before)
        sources = (
            "docs/contracts/canonical-capability-policy-v1.json",
            "profiles/built-in/package-trust-index.json",
            self.allowlist,
        )
        for source in sources:
            changed[source] += b"\n"
        planned = self.provider.plan(changed)
        for target in self.provider.outputs:
            expected = self.before[target]
            for source in sources:
                old_digest = hashlib.sha256(self.before[source]).hexdigest().encode()
                new_digest = hashlib.sha256(changed[source]).hexdigest().encode()
                expected = expected.replace(old_digest, new_digest)
            self.assertEqual(expected, planned[target], target)
        self.assertEqual(set(self.provider.outputs), set(planned))

    def test_missing_named_binding_is_rejected_instead_of_global_hash_replacement(self):
        changed = dict(self.before)
        path = "scripts/package.ps1"
        changed[path] = changed[path].replace(
            b"role = 'capabilityPolicy'", b"role = 'otherPolicy'", 1
        )
        with self.assertRaises(RuntimeError):
            self.provider.plan(changed)

    def test_allowlist_is_a_source_never_an_output_and_only_two_scalars_are_written(
        self,
    ):
        self.assertEqual(
            (
                "docs/contracts/canonical-capability-policy-v1.json",
                "profiles/built-in/package-trust-index.json",
                self.allowlist,
                *self.provider.outputs,
            ),
            self.provider.inputs,
        )
        self.assertEqual(
            (
                "src/NvtFwCombiner.Infrastructure/Capabilities/BuiltInCanonicalCapabilityPolicy.cs",
                "scripts/package.ps1",
                "scripts/smoke-release.ps1",
                "tests/scripts/test_release_package_policy.py",
            ),
            self.provider.outputs,
        )
        changed = dict(self.before)
        changed[self.allowlist] += b"\n"
        old_digest = hashlib.sha256(self.before[self.allowlist]).hexdigest().encode()
        new_digest = hashlib.sha256(changed[self.allowlist]).hexdigest().encode()
        expected = dict(changed)
        for path, name in (
            ("scripts/package.ps1", b"ApprovedCanonicalGoldenReleaseAllowlistSha256"),
            ("scripts/smoke-release.ps1", b"ApprovedCanonicalGoldenAllowlistSha256"),
        ):
            changed[path] += b"\n# Unrelated digest must stay: " + old_digest + b"\n"
            expected[path] = changed[path].replace(
                b"$" + name + b" = '" + old_digest + b"'",
                b"$" + name + b" = '" + new_digest + b"'",
                1,
            )
        golden = "testdata/golden/canonical/synthetic-reference.bin"
        changed[golden] = expected[golden] = b"synthetic unchanged Golden bytes\x00\xff"
        self.write_fixture(changed)
        self.assertEqual(1, self.run_sync())
        for path, raw in changed.items():
            self.assertEqual(raw, (self.root / path).read_bytes(), path)
        with patch.object(sync.os, "replace", wraps=os.replace) as replace:
            self.assertEqual(0, self.run_sync(write=True))
            self.assertEqual(
                {
                    self.root / "scripts/package.ps1",
                    self.root / "scripts/smoke-release.ps1",
                },
                {call.args[1] for call in replace.call_args_list},
            )
            self.assertEqual(2, replace.call_count)
        for path, raw in expected.items():
            self.assertEqual(raw, (self.root / path).read_bytes(), path)
        with patch.object(
            sync.os, "replace", side_effect=AssertionError("no-op wrote")
        ):
            self.assertEqual(0, self.run_sync(write=True))

    def test_missing_or_duplicate_allowlist_bindings_fail_before_any_write(self):
        for path, source, name in (
            (
                "scripts/package.ps1",
                b"$CanonicalGoldenReleaseAllowlistPath = Join-Path $RepoRoot 'testdata/golden/release-canonical-v1.json'",
                b"ApprovedCanonicalGoldenReleaseAllowlistSha256",
            ),
            (
                "scripts/smoke-release.ps1",
                b"$ApprovedCanonicalGoldenAllowlistPath = Join-Path $PSScriptRoot '../testdata/golden/release-canonical-v1.json'",
                b"ApprovedCanonicalGoldenAllowlistSha256",
            ),
        ):
            digest = hashlib.sha256(self.before[self.allowlist]).hexdigest().encode()
            scalar = b"$" + name + b" = '" + digest + b"'"
            for invalid in (
                self.before[path].replace(
                    source, source.replace(b"release-canonical-v1", b"foreign"), 1
                ),
                self.before[path].replace(
                    scalar, scalar.replace(name, b"OtherSha256"), 1
                ),
                self.before[path] + b"\n" + source + b"\n" + scalar + b"\n",
                self.before[path] + b"\n" + scalar + b"\n",
                self.before[path] + b"\n" + scalar.lower() + b"\n",
            ):
                with self.subTest(path=path, invalid=invalid[-160:]):
                    changed = dict(self.before)
                    changed[self.allowlist] += b"\n"
                    changed[path] = invalid
                    self.write_fixture(changed)
                    with patch.object(sync.os, "replace") as replace:
                        with self.assertRaisesRegex(RuntimeError, "binding|pin"):
                            self.run_sync(write=True)
                        replace.assert_not_called()
                    for relative, raw in changed.items():
                        self.assertEqual(
                            raw, (self.root / relative).read_bytes(), relative
                        )


if __name__ == "__main__":
    unittest.main()

"""Regression coverage for operator-editable update-source Registry hotfixes."""

from __future__ import annotations

import hashlib
import importlib.util
import json
import os
import subprocess
import sys
import tempfile
import textwrap
import unittest
from pathlib import Path
from unittest import mock

from scripts import update_source_registry_policy as REGISTRY_POLICY


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "edit_update_source_registry.py"
SPEC = importlib.util.spec_from_file_location("edit_update_source_registry", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
EDITOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(EDITOR)


def _registry(revision: int = 7) -> bytes:
    return (
        json.dumps(
            {
                "schemaVersion": 1,
                "registryId": "nvt-fw-combiner-production",
                "registryRevision": revision,
                "publishedAtUtc": "2026-08-27T00:00:00Z",
                "catalogPublication": {
                    "latestVersion": "1.0.0",
                    "catalogSchemaVersion": 1,
                    "catalogSha256": "0" * 64,
                },
                "entries": [
                    {
                        "status": "latest",
                        "catalogPath": (
                            r"G:\AUTO\projects\NVT_FW_Combiner\update-catalog.v1.json"
                        ),
                    }
                ],
            },
            indent=2,
        )
        + "\n"
    ).encode("utf-8")


class UpdateSourceRegistryWirePolicyTests(unittest.TestCase):
    def _document(self) -> dict[str, object]:
        document = json.loads(_registry())
        self.assertIsInstance(document, dict)
        return document

    def test_registry_rejects_noncanonical_or_invalid_publication_times(self) -> None:
        invalid_values = (
            "2026-08-27T00:00Z",
            "2026-08-27 00:00:00Z",
            "2026-08-27T00:00:00+00:00",
            "2026-08-27T00:00:00.12345678Z",
            "2026-02-30T00:00:00Z",
            " 2026-08-27T00:00:00Z",
        )
        for value in invalid_values:
            with self.subTest(value=value):
                document = self._document()
                document["publishedAtUtc"] = value
                with self.assertRaisesRegex(ValueError, "canonical UTC"):
                    EDITOR._validate_document(document)

    def test_registry_rejects_empty_or_noncanonical_catalog_identity(self) -> None:
        invalid_publications = (
            {"latestVersion": "", "catalogSchemaVersion": 1, "catalogSha256": "0" * 64},
            {"latestVersion": "01.0.0", "catalogSchemaVersion": 1, "catalogSha256": "0" * 64},
            {"latestVersion": "1.0.0", "catalogSchemaVersion": 1, "catalogSha256": "A" * 64},
            {"latestVersion": "1.0.0", "catalogSchemaVersion": True, "catalogSha256": "0" * 64},
        )
        for publication in invalid_publications:
            with self.subTest(publication=publication):
                document = self._document()
                document["catalogPublication"] = publication
                with self.assertRaises(ValueError):
                    EDITOR._validate_document(document)

    def test_explicit_empty_hotfix_metadata_is_rejected_not_inherited(self) -> None:
        document = self._document()
        proposed = EDITOR._updated_publication(document, "", None, "")
        self.assertEqual("", proposed["latestVersion"])
        self.assertEqual("", proposed["catalogSha256"])
        with self.assertRaises(ValueError):
            EDITOR._validate_proposed_metadata(document, "", proposed)


@unittest.skipUnless(os.name == "nt", "Registry publisher is Windows-only")
class UpdateSourceRegistryEditorTests(unittest.TestCase):
    """Keeps Registry routing mutable without weakening immutable package identity."""

    def test_hotfix_advances_revision_and_preserves_declared_order(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            registry = Path(temporary) / "update-source-registry.json"
            registry.write_bytes(_registry())
            security = EDITOR._security_descriptor_identity(registry)

            previous, current = EDITOR.update_registry(
                registry,
                r"\\server\share\latest\update-catalog.v1.json",
                [
                    r"G:\AUTO\available-a\update-catalog.v1.json",
                    r"G:\AUTO\available-b\update-catalog.v1.json",
                ],
                [r"G:\AUTO\deprecated\update-catalog.v1.json"],
                expected_revision=7,
                dry_run=False,
            )

            document = json.loads(registry.read_text(encoding="utf-8"))
            self.assertEqual((7, 8), (previous, current))
            self.assertEqual(8, document["registryRevision"])
            self.assertEqual(
                ["latest", "available", "available", "deprecated"],
                [entry["status"] for entry in document["entries"]],
            )
            self.assertNotIn("checksum", document)
            self.assertNotIn("digest", document)
            self.assertNotIn("sha256", document)
            self.assertEqual(security, EDITOR._security_descriptor_identity(registry))

    def test_authoritative_repair_jumps_stale_replica_to_exact_newer_bytes(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            stale = root / "stale-registry.json"
            authoritative = root / "authoritative-registry.json"
            stale.write_bytes(_registry(7))
            authoritative.write_bytes(_registry(9))
            stale_security = EDITOR._security_descriptor_identity(stale)

            previous, current = EDITOR.repair_registry(
                stale,
                authoritative,
                expected_revision=7,
                dry_run=False,
            )

            self.assertEqual((7, 9), (previous, current))
            self.assertEqual(authoritative.read_bytes(), stale.read_bytes())
            self.assertEqual(
                hashlib.sha256(authoritative.read_bytes()).digest(),
                hashlib.sha256(stale.read_bytes()).digest(),
            )
            self.assertEqual(stale_security, EDITOR._security_descriptor_identity(stale))

    def test_authoritative_repair_rejects_non_newer_or_wrong_identity(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            stale = root / "stale-registry.json"
            authoritative = root / "authoritative-registry.json"
            stale.write_bytes(_registry(7))
            authoritative.write_bytes(_registry(7))
            original = stale.read_bytes()

            with self.assertRaisesRegex(ValueError, "must be newer"):
                EDITOR.repair_registry(
                    stale,
                    authoritative,
                    expected_revision=7,
                    dry_run=False,
                )
            changed_identity = json.loads(_registry(9))
            changed_identity["registryId"] = "different-authority"
            authoritative.write_text(json.dumps(changed_identity), encoding="utf-8")
            with self.assertRaises(ValueError):
                EDITOR.repair_registry(
                    stale,
                    authoritative,
                    expected_revision=7,
                    dry_run=False,
                )

            self.assertEqual(original, stale.read_bytes())

    def test_registry_hotfix_does_not_modify_package_catalog_or_manifest(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            registry = root / "update-source-registry.json"
            registry.write_bytes(_registry())
            immutable = {
                root / "packages" / "NvtFwCombiner-v1.0.0-win-x64.zip": b"zip",
                root / "update-catalog.v1.json": b"catalog",
                root / "RELEASE-MANIFEST.json": b"manifest",
            }
            for path, content in immutable.items():
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(content)
            before = {
                path: hashlib.sha256(path.read_bytes()).hexdigest()
                for path in immutable
            }

            EDITOR.update_registry(
                registry,
                r"G:\AUTO\projects\NVT_FW_Combiner-hotfix\update-catalog.v1.json",
                [],
                [r"G:\AUTO\projects\NVT_FW_Combiner\update-catalog.v1.json"],
                expected_revision=7,
                dry_run=False,
            )

            after = {
                path: hashlib.sha256(path.read_bytes()).hexdigest()
                for path in immutable
            }
            self.assertEqual(before, after)

    def test_catalog_assertion_hotfix_advances_revision_without_changing_path(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            registry = Path(temporary) / "update-source-registry.json"
            registry.write_bytes(_registry())

            previous, current = EDITOR.update_registry(
                registry,
                r"G:\AUTO\projects\NVT_FW_Combiner\update-catalog.v1.json",
                [],
                [],
                expected_revision=7,
                dry_run=False,
                latest_version="1.0.1",
                catalog_schema_version=1,
                catalog_sha256="a" * 64,
                published_at_utc="2026-08-28T00:00:00Z",
            )

            document = json.loads(registry.read_text(encoding="utf-8"))
            self.assertEqual((7, 8), (previous, current))
            self.assertEqual("1.0.1", document["catalogPublication"]["latestVersion"])
            self.assertEqual("a" * 64, document["catalogPublication"]["catalogSha256"])
            self.assertEqual("2026-08-28T00:00:00Z", document["publishedAtUtc"])

    def test_invalid_or_duplicate_path_leaves_registry_unchanged(self) -> None:
        cases = (
            ("relative", [], []),
            (r"G:\AUTO\latest", [r"g:\auto\LATEST"], []),
            (r"G:\AUTO\catalog-a.json", [r"G:\AUTO\catalog-b.json"], []),
            ("G:\\AUTO\\latest\\", [], []),
            (r"\\?\G:\AUTO\latest", [], []),
        )
        for latest, available, deprecated in cases:
            with self.subTest(latest=latest):
                with tempfile.TemporaryDirectory() as temporary:
                    registry = Path(temporary) / "update-source-registry.json"
                    original = _registry()
                    registry.write_bytes(original)

                    with self.assertRaises(ValueError):
                        EDITOR.update_registry(
                            registry,
                            latest,
                            available,
                            deprecated,
                            expected_revision=7,
                            dry_run=False,
                        )

                    self.assertEqual(original, registry.read_bytes())

    def test_windows_catalog_path_normalization_matches_runtime_reader_contract(self) -> None:
        accepted = (
            r"\\server\share\update-catalog.v1.json",
            r"\\server\share\latest\catalog.json",
            r"G:\update-catalog.v1.json",
            r"G:\AUTO\latest\catalog.json",
        )
        rejected = (
            "\\\\server\\share\\catalog.json\\",
            "G:\\AUTO\\catalog.json\\",
            r"\\?\G:\AUTO\catalog.json",
            r"G:\AUTO\..\latest\catalog.json",
        )

        for value in accepted:
            with self.subTest(value=value, expected="accepted"):
                self.assertEqual(
                    value,
                    REGISTRY_POLICY.normalize_windows_catalog_path(value),
                )
        for value in rejected:
            with self.subTest(value=value, expected="rejected"):
                with self.assertRaises(ValueError):
                    REGISTRY_POLICY.normalize_windows_catalog_path(value)

    def test_stale_expected_revision_and_noop_leave_registry_unchanged(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            registry = Path(temporary) / "update-source-registry.json"
            original = _registry()
            registry.write_bytes(original)

            with self.assertRaisesRegex(ValueError, "expected 6, found 7"):
                EDITOR.update_registry(
                    registry,
                    r"G:\AUTO\new",
                    [],
                    [],
                    expected_revision=6,
                    dry_run=False,
                )
            self.assertEqual(original, registry.read_bytes())

            with self.assertRaisesRegex(ValueError, "did not change"):
                EDITOR.update_registry(
                    registry,
                    r"g:\auto\PROJECTS\nvt_fw_combiner\UPDATE-CATALOG.V1.JSON",
                    [],
                    [],
                    expected_revision=7,
                    dry_run=False,
                )
            self.assertEqual(original, registry.read_bytes())

            with self.assertRaisesRegex(ValueError, "did not change"):
                EDITOR.update_registry(
                    registry,
                    r"G:\AUTO\projects\NVT_FW_Combiner\update-catalog.v1.json",
                    [],
                    [],
                    expected_revision=7,
                    dry_run=False,
                )
            self.assertEqual(original, registry.read_bytes())

    def test_dry_run_validates_without_writing(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            registry = Path(temporary) / "update-source-registry.json"
            original = _registry()
            registry.write_bytes(original)

            revisions = EDITOR.update_registry(
                registry,
                r"G:\AUTO\new",
                [],
                [],
                expected_revision=7,
                dry_run=True,
            )

            self.assertEqual((7, 8), revisions)
            self.assertEqual(original, registry.read_bytes())

    def test_write_requires_expected_revision(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            registry = Path(temporary) / "update-source-registry.json"
            original = _registry()
            registry.write_bytes(original)

            with self.assertRaisesRegex(ValueError, "expected-revision is required"):
                EDITOR.update_registry(
                    registry,
                    r"G:\AUTO\new",
                    [],
                    [],
                    expected_revision=None,
                    dry_run=False,
                )

            self.assertEqual(original, registry.read_bytes())

    def test_two_publishers_cannot_emit_different_content_at_one_revision(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            registry = Path(temporary) / "update-source-registry.json"
            registry.write_bytes(_registry())
            command = [
                sys.executable,
                str(SCRIPT),
                "--registry",
                str(registry),
                "--expected-revision",
                "7",
                "--latest",
            ]
            publishers = [
                subprocess.Popen(
                    [*command, latest],
                    stdout=subprocess.PIPE,
                    stderr=subprocess.PIPE,
                    text=True,
                )
                for latest in (r"G:\AUTO\writer-a", r"G:\AUTO\writer-b")
            ]
            results = [publisher.communicate(timeout=30) for publisher in publishers]
            return_codes = [publisher.returncode for publisher in publishers]

            self.assertEqual(1, return_codes.count(0), results)
            self.assertEqual(1, sum(code != 0 for code in return_codes), results)
            document = json.loads(registry.read_text(encoding="utf-8"))
            self.assertEqual(8, document["registryRevision"])
            self.assertIn(
                document["entries"][0]["catalogPath"],
                {r"G:\AUTO\writer-a", r"G:\AUTO\writer-b"},
            )
            self.assertFalse(
                registry.with_name(f".{registry.name}.publisher.lock").exists()
            )

    def test_hardened_windows_security_descriptor_survives_publish(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            registry = Path(temporary) / "update-source-registry.json"
            registry.write_bytes(_registry())
            subprocess.run(
                ["icacls", str(registry), "/inheritance:d"],
                check=True,
                capture_output=True,
                text=True,
            )
            before = EDITOR._security_descriptor_identity(registry)

            EDITOR.update_registry(
                registry,
                r"G:\AUTO\hardened",
                [],
                [],
                expected_revision=7,
                dry_run=False,
            )

            self.assertEqual(before, EDITOR._security_descriptor_identity(registry))

    def test_ancestor_junction_is_rejected_without_touching_target(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            target = root / "target"
            junction = root / "junction"
            target.mkdir()
            subprocess.run(
                ["cmd", "/c", "mklink", "/J", str(junction), str(target)],
                check=True,
                capture_output=True,
                text=True,
            )
            registry = junction / "update-source-registry.json"
            target_registry = target / registry.name
            original = _registry()
            target_registry.write_bytes(original)
            try:
                with self.assertRaisesRegex(ValueError, "reparse point"):
                    EDITOR.update_registry(
                        registry,
                        r"G:\AUTO\redirected",
                        [],
                        [],
                        expected_revision=7,
                        dry_run=False,
                    )
                self.assertEqual(original, target_registry.read_bytes())
            finally:
                os.rmdir(junction)

    def test_acl_or_replace_failure_preserves_original_and_cleans_staging(self) -> None:
        for owner, failure in (
            ("_apply_security_descriptor", OSError("ACL failure")),
            ("_replace_file", OSError("replace failure")),
        ):
            with self.subTest(owner=owner):
                with tempfile.TemporaryDirectory() as temporary:
                    registry = Path(temporary) / "update-source-registry.json"
                    original = _registry()
                    registry.write_bytes(original)
                    security = EDITOR._security_descriptor_identity(registry)

                    with mock.patch.object(EDITOR, owner, side_effect=failure):
                        with self.assertRaises(OSError):
                            EDITOR.update_registry(
                                registry,
                                r"G:\AUTO\failed",
                                [],
                                [],
                                expected_revision=7,
                                dry_run=False,
                            )

                    self.assertEqual(original, registry.read_bytes())
                    self.assertEqual(security, EDITOR._security_descriptor_identity(registry))
                    self.assertEqual(
                        [],
                        list(Path(temporary).glob(f".{registry.name}.*")),
                    )

    def test_post_replace_verification_exception_restores_original(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            registry = Path(temporary) / "update-source-registry.json"
            original = _registry()
            registry.write_bytes(original)
            security = EDITOR._security_descriptor_identity(registry)
            original_identity = EDITOR._security_descriptor_identity
            calls = 0

            def fail_once_after_replace(path: Path) -> str:
                nonlocal calls
                calls += 1
                if calls == 3:
                    raise OSError("post-replace ACL inspection failed")
                return original_identity(path)

            with mock.patch.object(
                EDITOR,
                "_security_descriptor_identity",
                side_effect=fail_once_after_replace,
            ):
                with self.assertRaisesRegex(RuntimeError, "original restored"):
                    EDITOR.update_registry(
                        registry,
                        r"G:\AUTO\failed-verification",
                        [],
                        [],
                        expected_revision=7,
                        dry_run=False,
                    )

            self.assertEqual(original, registry.read_bytes())
            self.assertEqual(security, EDITOR._security_descriptor_identity(registry))
            self.assertEqual([], list(Path(temporary).glob(f".{registry.name}.*")))

    def test_post_replace_mismatch_uses_actual_restore_path(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            registry = Path(temporary) / "update-source-registry.json"
            original = _registry()
            registry.write_bytes(original)
            security = EDITOR._security_descriptor_identity(registry)
            real_replace = EDITOR._replace_file
            calls = 0

            def corrupt_first_publication(destination: Path, replacement: Path) -> None:
                nonlocal calls
                calls += 1
                real_replace(destination, replacement)
                if calls == 1:
                    destination.write_bytes(b"corrupt")

            with mock.patch.object(
                EDITOR,
                "_replace_file",
                side_effect=corrupt_first_publication,
            ):
                with self.assertRaisesRegex(RuntimeError, "original restored"):
                    EDITOR.update_registry(
                        registry,
                        r"G:\AUTO\mismatch",
                        [],
                        [],
                        expected_revision=7,
                        dry_run=False,
                    )

            self.assertEqual(2, calls)
            self.assertEqual(original, registry.read_bytes())
            self.assertEqual(security, EDITOR._security_descriptor_identity(registry))

    def test_forced_process_termination_releases_lock_and_next_run_cleans_stage(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            registry = Path(temporary) / "update-source-registry.json"
            registry.write_bytes(_registry())
            child = textwrap.dedent(
                f"""
                import importlib.util
                import sys
                import time
                from pathlib import Path
                spec = importlib.util.spec_from_file_location('editor', {str(SCRIPT)!r})
                module = importlib.util.module_from_spec(spec)
                spec.loader.exec_module(module)
                def stop_after_stage(destination, replacement):
                    print('STAGED', flush=True)
                    time.sleep(300)
                module._replace_file = stop_after_stage
                module.update_registry(
                    Path(sys.argv[1]),
                    r'G:\\AUTO\\crashed',
                    [],
                    [],
                    expected_revision=7,
                    dry_run=False,
                )
                """
            )
            publisher = subprocess.Popen(
                [sys.executable, "-c", child, str(registry)],
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
            )
            try:
                assert publisher.stdout is not None
                self.assertEqual("STAGED", publisher.stdout.readline().strip())
                lock = registry.with_name(f".{registry.name}.publisher.lock")
                self.assertTrue(lock.exists())
                self.assertEqual(
                    1,
                    len(list(Path(temporary).glob(f".{registry.name}.*.tmp"))),
                )
                publisher.kill()
                publisher.communicate(timeout=30)
                self.assertFalse(lock.exists())

                previous, current = EDITOR.update_registry(
                    registry,
                    r"G:\AUTO\after-crash",
                    [],
                    [],
                    expected_revision=7,
                    dry_run=False,
                )
                self.assertEqual((7, 8), (previous, current))
                self.assertEqual([], list(Path(temporary).glob(f".{registry.name}.*")))
            finally:
                if publisher.poll() is None:
                    publisher.kill()
                publisher.communicate(timeout=30)


if __name__ == "__main__":
    unittest.main()

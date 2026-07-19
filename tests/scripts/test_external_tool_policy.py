"""Behavioral tests for repository-only external-tool manifest pinning."""

from __future__ import annotations

import hashlib
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path, PurePosixPath

SCRIPTS = Path(__file__).resolve().parents[2] / "scripts"
sys.path.insert(0, str(SCRIPTS))

from external_tool_policy import (  # noqa: E402
    APPROVED_EXTERNAL_TOOL_PACKAGE_PATHS,
    APPROVED_EXTERNAL_TOOL_REPOSITORY_PATHS,
    validate_external_tool_catalog,
    validate_repository_external_tool_manifests,
)

ROOT = Path(__file__).resolve().parents[2]


class ExternalToolPolicyTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)
        self.package = PurePosixPath("external-tools/example/1.0.0")
        self.payload_path = self.root / self.package / "Tool.exe"
        self.payload_path.parent.mkdir(parents=True)
        self.payload_path.write_bytes(b"owner supplied tool")
        self.manifest_path = self.package / "package-manifest.json"
        (self.root / self.manifest_path).write_text(
            json.dumps(
                {
                    "files": [
                        {
                            "path": "Tool.exe",
                            "size": self.payload_path.stat().st_size,
                            "sha256": hashlib.sha256(
                                self.payload_path.read_bytes()
                            ).hexdigest(),
                        }
                    ]
                }
            ),
            encoding="utf-8",
        )
        self.repository_paths = {self.manifest_path, self.package / "Tool.exe"}

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    def validate(self) -> list[str]:
        errors: list[str] = []
        validate_repository_external_tool_manifests(
            self.root, self.repository_paths, errors
        )
        return errors

    def test_accepts_manifest_pinned_repository_payload(self) -> None:
        self.assertEqual([], self.validate())

    def test_rejects_payload_that_no_longer_matches_hash_and_size(self) -> None:
        self.payload_path.write_bytes(b"modified")

        errors = self.validate()

        self.assertTrue(any("size mismatch" in error for error in errors))
        self.assertTrue(any("SHA-256 mismatch" in error for error in errors))

    def test_rejects_approved_payload_missing_from_manifest(self) -> None:
        extra_path = self.package / "Tool.exe.config"
        (self.root / extra_path).write_text("configuration", encoding="utf-8")
        self.repository_paths.add(extra_path)

        errors = self.validate()

        self.assertTrue(any("manifest inventory mismatch" in error for error in errors))


class ExternalToolCatalogTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)
        shutil.copytree(ROOT / "external-tools", self.root / "external-tools")
        shutil.copytree(ROOT / "tools/crc-worker", self.root / "tools/crc-worker")

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    def validate(self) -> list[str]:
        errors: list[str] = []
        validate_external_tool_catalog(
            self.root,
            APPROVED_EXTERNAL_TOOL_REPOSITORY_PATHS,
            APPROVED_EXTERNAL_TOOL_PACKAGE_PATHS,
            errors,
        )
        return errors

    def catalog(self) -> dict:
        return json.loads(
            (self.root / "external-tools/catalog.json").read_text(encoding="utf-8")
        )

    def write_catalog(self, catalog: dict) -> None:
        (self.root / "external-tools/catalog.json").write_text(
            json.dumps(catalog), encoding="utf-8"
        )

    def update_catalog_repository_pin(self, catalog: dict, relative_path: str) -> None:
        payload_path = self.root / relative_path
        entry = next(
            entry
            for entry in catalog["repositoryFiles"]
            if entry["path"] == relative_path
        )
        payload = payload_path.read_bytes()
        entry["size"] = len(payload)
        entry["sha256"] = hashlib.sha256(payload).hexdigest()

    def test_accepts_pinned_repository_package_and_runtime_inventory(self) -> None:
        self.assertEqual([], self.validate())

    def test_rejects_catalog_pinned_repository_file_drift(self) -> None:
        (self.root / "external-tools/README.md").write_text(
            "changed external-tool policy", encoding="utf-8"
        )

        errors = self.validate()

        self.assertTrue(any("catalog size mismatch" in error for error in errors))
        self.assertTrue(any("catalog SHA-256 mismatch" in error for error in errors))

    def test_rejects_root_executable_in_release_inventory(self) -> None:
        catalog = self.catalog()
        catalog["releasePackagePaths"].append("CRCWorker.exe")
        self.write_catalog(catalog)

        errors = self.validate()

        self.assertTrue(
            any(
                "executable cannot be packaged at the release root" in error
                for error in errors
            )
        )

    def test_rejects_diff_nf_merge_runtime_promotion(self) -> None:
        catalog = self.catalog()
        next(tool for tool in catalog["tools"] if tool["logicalId"] == "diff-nf-merge")[
            "runtimeStatus"
        ] = "registered"
        self.write_catalog(catalog)

        errors = self.validate()

        self.assertTrue(
            any("diff-nf-merge runtimeStatus mismatch" in error for error in errors)
        )

    def test_rejects_coordinated_diff_nf_merge_runtime_promotion(self) -> None:
        manifest_relative = (
            "external-tools/diff-nf-merge/1.0.0/package-manifest.json"
        )
        manifest_path = self.root / manifest_relative
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        manifest["runtimeIntegrationStatus"] = "registered"
        manifest["releasePackageStatus"] = "included"
        manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
        catalog = self.catalog()
        tool = next(
            tool for tool in catalog["tools"] if tool["logicalId"] == "diff-nf-merge"
        )
        tool["runtimeStatus"] = "registered"
        tool["releaseStatus"] = "included"
        self.update_catalog_repository_pin(catalog, manifest_relative)
        self.write_catalog(catalog)

        errors = self.validate()

        self.assertTrue(
            any(
                "DiffNFMerge package manifest runtimeIntegrationStatus mismatch" in error
                for error in errors
            )
        )
        self.assertTrue(
            any("diff-nf-merge runtimeStatus mismatch" in error for error in errors)
        )

    def test_rejects_coordinated_legacy_combiner_hash_drift(self) -> None:
        manifest_relative = (
            "external-tools/legacy-combiner/1.13.0/manifest.json"
        )
        manifest_path = self.root / manifest_relative
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        manifest["sha256"] = "0" * 64
        manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
        catalog = self.catalog()
        next(
            tool for tool in catalog["tools"] if tool["logicalId"] == "legacy-combiner"
        )["executableSha256"] = "0" * 64
        self.update_catalog_repository_pin(catalog, manifest_relative)
        self.write_catalog(catalog)

        errors = self.validate()

        self.assertTrue(
            any("legacy Combiner manifest sha256 mismatch" in error for error in errors)
        )
        self.assertTrue(
            any("manifest SHA-256 does not match actual payload" in error for error in errors)
        )

    def test_rejects_crc_worker_source_protocol_drift(self) -> None:
        protocol_path = (
            self.root / "tools/crc-worker/src/nfc_crc_worker/protocol.py"
        )
        protocol_path.write_text(
            protocol_path.read_text(encoding="utf-8").replace(
                'PROTOCOL_VERSION = "1.0"', 'PROTOCOL_VERSION = "2.0"'
            ),
            encoding="utf-8",
        )

        errors = self.validate()

        self.assertTrue(
            any("missing 'PROTOCOL_VERSION = \"1.0\"'" in error for error in errors)
        )


if __name__ == "__main__":
    unittest.main()

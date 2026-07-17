"""Behavioral tests for repository-only external-tool manifest pinning."""

from __future__ import annotations

import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path, PurePosixPath

SCRIPTS = Path(__file__).resolve().parents[2] / "scripts"
sys.path.insert(0, str(SCRIPTS))

from validate_repository import validate_repository_external_tool_manifests


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


if __name__ == "__main__":
    unittest.main()

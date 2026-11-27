# Copyright (c) 2026 Dennis Liu. All rights reserved.
"""Offline evaluation of the build-owned version identity and assembly metadata."""

from __future__ import annotations

import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from xml.sax.saxutils import escape


ROOT = Path(__file__).resolve().parents[2]


class BuildVersionIdentityTests(unittest.TestCase):
    def test_build_emits_one_stable_core_and_preserves_display_identity(self):
        for product_version in ("1.3.0-g.0", "1.3.0"):
            with self.subTest(product_version=product_version), tempfile.TemporaryDirectory() as temp:
                driver = Path(temp) / "version.proj"
                driver.write_text(
                    f'<Project><Import Project="{escape(str(ROOT / "Directory.Build.props"))}" />'
                    '<Target Name="Build" /></Project>', encoding="utf-8",
                )
                result = subprocess.run(
                    ["dotnet", "build", str(driver), "--no-restore", "--nologo",
                     "--verbosity", "quiet", "-m:1", "-nr:false",
                     f"-p:ProductVersion={product_version}",
                     "-getProperty:VersionCore,InformationalVersion", "-getItem:AssemblyMetadata"],
                    cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace",
                    timeout=120, check=False,
                )
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                evaluated = json.loads(result.stdout)
                self.assertEqual({"VersionCore": "1.3.0", "InformationalVersion": product_version},
                                 evaluated["Properties"])
                self.assertEqual([("VersionCore", "1.3.0")],
                                 [(item["Identity"], item["Value"])
                                  for item in evaluated["Items"]["AssemblyMetadata"]])


if __name__ == "__main__":
    unittest.main()

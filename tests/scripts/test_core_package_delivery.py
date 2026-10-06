"""The Core package source and its mapping in NuGet.config."""

from __future__ import annotations

from pathlib import Path
import unittest
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[2]


class CorePackageDeliveryTests(unittest.TestCase):
    def test_sources_keep_protocol_and_repository_package_cache(self) -> None:
        config = ET.parse(ROOT / "NuGet.config")
        self.assertIsNotNone(config.find("./packageSources/clear"))
        self.assertEqual(
            [node.attrib for node in config.findall("./packageSources/add")],
            [
                {"key": "nuget.org", "value": "https://api.nuget.org/v3/index.json", "protocolVersion": "3"},
                {"key": "core-packages", "value": "artifacts/core-packages"},
            ],
        )
        self.assertEqual(config.find("./config/add[@key='globalPackagesFolder']").get("value"), ".packages")
        self.assertEqual(
            {
                source.get("key"): [node.get("pattern") for node in source.findall("package")]
                for source in config.findall("./packageSourceMapping/packageSource")
            },
            {"nuget.org": ["*"], "core-packages": ["Nvt.Core", "Nvt.Core.*"]},
        )

    def test_bootstrap_scripts_fetch_core_packages_before_restore(self) -> None:
        for name in ("bootstrap.ps1", "bootstrap.sh"):
            with self.subTest(script=name):
                source = (ROOT / "scripts" / name).read_text(encoding="utf-8")
                fetch = source.find("fetch_core_packages.py")
                restore = source.find("dotnet restore")
                self.assertGreaterEqual(fetch, 0, "the bootstrap script must fetch Core packages")
                self.assertLess(fetch, restore, "the fetch must come before the restore")
        powershell = (ROOT / "scripts" / "bootstrap.ps1").read_text(encoding="utf-8")
        self.assertIn("if ($LASTEXITCODE -ne 0) { throw 'Core package download or verification failed", powershell)
        self.assertIn("set -euo pipefail", (ROOT / "scripts" / "bootstrap.sh").read_text(encoding="utf-8"))

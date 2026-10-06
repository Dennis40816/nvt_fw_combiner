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

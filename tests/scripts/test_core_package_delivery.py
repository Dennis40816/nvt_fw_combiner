"""Core package source isolation through the SDK's NuGet mapping API."""

from __future__ import annotations

import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[2]
PWSH = shutil.which("pwsh")
DOTNET = shutil.which("dotnet")


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

    @unittest.skipUnless(PWSH and DOTNET, "PowerShell 7 and .NET SDK are required for NuGet mapping")
    def test_nuget_resolves_core_only_locally_and_other_packages_only_from_nuget_org(self) -> None:
        package_ids = ["Nvt.Core", "Nvt.Core.Avalonia", "Nvt.Core.Future", "nvt.core.lowercase",
                       "Nvt.CoreExtra", "Nvt.Other", "Avalonia", "xunit.v3", "Other.Package"]
        with tempfile.TemporaryDirectory() as temporary:
            script = Path(temporary) / "source-mapping.ps1"
            script.write_text(
                "param([string]$DotNet, [string]$RepoRoot, [string]$PackageIds)\n"
                "$ErrorActionPreference = 'Stop'\n"
                "$version = (& $DotNet --version).Trim()\n"
                "if ($LASTEXITCODE -ne 0) { throw 'SDK discovery failed' }\n"
                "$sdk = Join-Path (Split-Path $DotNet -Parent) ('sdk/' + $version)\n"
                "foreach ($name in @('NuGet.Common', 'NuGet.Configuration')) {\n"
                "    [Reflection.Assembly]::LoadFrom((Join-Path $sdk ($name + '.dll'))) | Out-Null\n}\n"
                "[xml]$config = Get-Content -LiteralPath (Join-Path $RepoRoot 'NuGet.config') -Raw\n"
                "$patterns = [Collections.Generic.Dictionary[string, Collections.Generic.IReadOnlyList[string]]]::new()\n"
                "foreach ($source in $config.configuration.packageSourceMapping.packageSource) {\n"
                "    $patterns.Add($source.key, [string[]]@($source.package | ForEach-Object { $_.pattern }))\n}\n"
                "$mapping = [NuGet.Configuration.PackageSourceMapping]::new($patterns)\n"
                "$result = [ordered]@{}\n"
                "foreach ($id in (ConvertFrom-Json $PackageIds)) {\n"
                "    $result[$id] = @($mapping.GetConfiguredPackageSources($id))\n}\n"
                "$result | ConvertTo-Json -Compress\n", encoding="utf-8",
            )
            result = subprocess.run(
                [PWSH, "-NoProfile", "-NonInteractive", "-File", str(script),
                 "-DotNet", DOTNET, "-RepoRoot", str(ROOT), "-PackageIds", json.dumps(package_ids)],
                cwd=ROOT, capture_output=True, text=True, encoding="utf-8", check=False, timeout=60,
            )
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        resolved = json.loads(result.stdout)
        for package_id in package_ids:
            with self.subTest(package_id=package_id):
                self.assertEqual(
                    resolved[package_id],
                    ["core-packages"] if package_id.lower() == "nvt.core" or package_id.lower().startswith("nvt.core.")
                    else ["nuget.org"],
                )

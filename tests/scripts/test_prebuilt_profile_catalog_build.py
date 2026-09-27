"""Offline behavior of the shared materializer task and precompile identity."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
TARGET = ROOT / "eng/profile-bundle-materializer/NvtFwCombiner.ProfileBundleMaterializer.targets"


class AdmissionBuildTests(unittest.TestCase):
    def invoke_task(self, index: Path, source: Path) -> tuple[subprocess.CompletedProcess, list[str]]:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            driver = root / "driver.proj"
            output = root / "bundles.txt"
            driver.write_text(f'''<Project>
<Import Project="{escape(str(TARGET))}" />
<Target Name="Probe">
<LoadProfileBundleTrustIndex TrustIndexPath="{escape(str(index))}"
 TrustIndexSchemaPath="{escape(str(ROOT / 'docs/contracts/profile-bundle-package-trust-index-v1.schema.json'))}"
 BuiltInProfileSourceRoot="{escape(str(source))}"
 ProfileContractRoot="{escape(str(ROOT / 'docs/contracts'))}">
<Output TaskParameter="Bundles" ItemName="Bundle" />
</LoadProfileBundleTrustIndex>
<WriteLinesToFile File="{escape(str(output))}" Lines="@(Bundle->'%(Identity)|%(HasCompositionProfileSchema)|%(HasFirmwareFamilySchema)|%(CanonicalFirmwareFamilySource)|%(CanonicalFirmwareFamilyDestination)')" Overwrite="true" />
</Target></Project>''', encoding="utf-8")
            result = subprocess.run(["dotnet", "msbuild", str(driver), "-t:Probe", "-nologo"],
                                    cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=90)
            return result, output.read_text(encoding="utf-8-sig").splitlines() if output.exists() else []

    def test_existing_materializer_enumerates_all_reviewed_bundles_and_metadata(self):
        source = ROOT / "profiles/built-in"
        result, lines = self.invoke_task(source / "package-trust-index.json", source)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        index = json.loads((source / "package-trust-index.json").read_text(encoding="utf-8"))
        expected = []
        for bundle in index["bundles"]:
            manifest = json.loads((source / bundle["bundleDirectory"] / "profile-bundle.json").read_text(encoding="utf-8"))
            paths = {entry["path"] for entry in manifest["entries"]}
            canonical = bundle["materialization"].get("canonicalFirmwareFamily", {})
            expected.append("|".join([bundle["bundleDirectory"],
                str("schemas/composition-profile-v2.schema.json" in paths).lower(),
                str("schemas/firmware-family-v1.schema.json" in paths).lower(),
                canonical.get("source", ""), canonical.get("destination", "")]))
        self.assertEqual(sorted(expected), sorted(lines))

    def test_existing_materializer_rejects_duplicate_index_property(self):
        with tempfile.TemporaryDirectory() as temp:
            source = ROOT / "profiles/built-in"
            index = Path(temp) / "index.json"
            original = (source / "package-trust-index.json").read_text(encoding="utf-8")
            index.write_text(original.replace("{", '{"schemaVersion":"1.0",', 1), encoding="utf-8")
            result, lines = self.invoke_task(index, source)
            self.assertNotEqual(0, result.returncode)
            self.assertEqual([], lines)
            self.assertIn("schemaVersion", result.stdout)


if __name__ == "__main__":
    unittest.main()

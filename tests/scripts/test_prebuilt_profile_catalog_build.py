"""Offline behavior of the shared materializer task and precompile identity."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
TARGET = ROOT / "eng/profile-bundle-materializer/NvtFwCombiner.ProfileBundleMaterializer.targets"


class AdmissionBuildTests(unittest.TestCase):
    def invoke_task(self, index: Path, source: Path, identity: bool = False) -> tuple[subprocess.CompletedProcess, list[str]]:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            driver = root / "driver.proj"
            output = root / "bundles.txt"
            identity_outputs = ('<Output TaskParameter="TrustIndexSha256" PropertyName="IndexHash" />'
                                '<Output TaskParameter="ManifestSetSha256" PropertyName="ManifestHash" />') if identity else ""
            identity_lines = ";$(IndexHash);$(ManifestHash)" if identity else ""
            driver.write_text(f'''<Project>
<Import Project="{escape(str(TARGET))}" />
<Target Name="Probe">
<LoadProfileBundleTrustIndex TrustIndexPath="{escape(str(index))}"
 TrustIndexSchemaPath="{escape(str(ROOT / 'docs/contracts/profile-bundle-package-trust-index-v1.schema.json'))}"
 BuiltInProfileSourceRoot="{escape(str(source))}"
 ProfileContractRoot="{escape(str(ROOT / 'docs/contracts'))}">
<Output TaskParameter="Bundles" ItemName="Bundle" />{identity_outputs}
</LoadProfileBundleTrustIndex>
<WriteLinesToFile File="{escape(str(output))}" Lines="@(Bundle->'%(Identity)|%(HasCompositionProfileSchema)|%(HasFirmwareFamilySchema)|%(CanonicalFirmwareFamilySource)|%(CanonicalFirmwareFamilyDestination)'){identity_lines}" Overwrite="true" />
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

    def test_build_task_exact_capture_digests_equal_independent_calculation(self):
        source = ROOT / "profiles/built-in"
        result, lines = self.invoke_task(source / "package-trust-index.json", source, identity=True)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        index = (source / "package-trust-index.json").read_bytes()
        manifests = [{"bundleDirectory": b["bundleDirectory"], "manifestSha256": hashlib.sha256(
            (source / b["bundleDirectory"] / "profile-bundle.json").read_bytes()).hexdigest()}
            for b in sorted(json.loads(index)["bundles"], key=lambda b: b["bundleDirectory"])]
        self.assertEqual([hashlib.sha256(index).hexdigest(), hashlib.sha256(
            json.dumps(manifests, separators=(",", ":")).encode()).hexdigest()], lines[-2:])

    def test_task_identity_detects_exact_bytes_and_sorts_manifest_set(self):
        with tempfile.TemporaryDirectory() as temp:
            source = Path(temp) / "built-in"
            shutil.copytree(ROOT / "profiles/built-in", source)
            index = source / "package-trust-index.json"
            result, baseline = self.invoke_task(index, source, identity=True)
            self.assertEqual(0, result.returncode, result.stdout)
            index.write_bytes(index.read_bytes() + b" ")
            result, whitespace = self.invoke_task(index, source, identity=True)
            self.assertEqual(0, result.returncode, result.stdout)
            self.assertNotEqual(baseline[-2], whitespace[-2])
            self.assertEqual(baseline[-1], whitespace[-1])
            document = json.loads(index.read_bytes())
            document["bundles"].reverse()
            index.write_text(json.dumps(document), encoding="utf-8")
            result, reordered = self.invoke_task(index, source, identity=True)
            self.assertEqual(0, result.returncode, result.stdout)
            self.assertEqual(baseline[-1], reordered[-1])
            manifest = source / document["bundles"][0]["bundleDirectory"] / "profile-bundle.json"
            manifest.write_bytes(manifest.read_bytes() + b" ")
            result, changed = self.invoke_task(index, source, identity=True)
            self.assertEqual(0, result.returncode, result.stdout)
            self.assertEqual(reordered[-2], changed[-2])
            self.assertNotEqual(reordered[-1], changed[-1])
            removed = document["bundles"].pop(0)
            index.write_text(json.dumps(document), encoding="utf-8")
            result, subset = self.invoke_task(index, source, identity=True)
            self.assertEqual(0, result.returncode, result.stdout)
            self.assertNotEqual(changed[-1], subset[-1])
            document["bundles"].append(removed)
            index.write_text(json.dumps(document), encoding="utf-8")
            result, restored = self.invoke_task(index, source, identity=True)
            self.assertEqual(0, result.returncode, result.stdout)
            self.assertEqual(changed[-1], restored[-1])


if __name__ == "__main__":
    unittest.main()

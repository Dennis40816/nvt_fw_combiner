"""Offline behavior of the shared materializer task and precompile identity."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
TARGET = ROOT / "eng/profile-bundle-materializer/NvtFwCombiner.ProfileBundleMaterializer.targets"


class CatalogOutputLifecycleTests(unittest.TestCase):
    """Real SDK graph, offline restored closure, and isolated reviewed inputs."""

    def test_clean_incremental_and_failed_generation_never_copy_a_stale_pack(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for directory in ("src", "eng", "profiles", "docs/contracts"):
                shutil.copytree(ROOT / directory, root / directory,
                                ignore=shutil.ignore_patterns("bin", "obj", "AGENTS.md"))
            for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props",
                         "global.json", "VERSION", ".editorconfig"):
                shutil.copy2(ROOT / name, root / name)
            project = root / "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj"
            cache = Path(os.environ.get("NUGET_PACKAGES", str(Path.home() / ".nuget/packages")))

            def run(*args):
                return subprocess.run(["dotnet", *map(str, args)], cwd=root, capture_output=True,
                                      text=True, encoding="utf-8", errors="replace", timeout=240)

            for dependency in [*root.glob("src/*/*.csproj"), *root.glob("eng/*/*.csproj")]:
                lock = json.loads(dependency.with_name("packages.lock.json").read_bytes())
                rid = ["-r", "win-x64"] if "net10.0/win-x64" in lock["dependencies"] else []
                restored = run("restore", dependency, "--no-dependencies", "--locked-mode", "--source", cache,
                               "-p:NuGetAudit=false", *rid)
                self.assertEqual(0, restored.returncode, restored.stdout + restored.stderr)
            pack = project.parent / "bin/Debug/net10.0/profiles/built-in/prebuilt-profile-catalog.pack"
            for attempt in range(2):
                built = run("build", project, "--no-restore", "-nologo")
                self.assertEqual(0, built.returncode, built.stdout + built.stderr)
                self.assertTrue(pack.is_file(), f"attempt {attempt}: missing exact pack Content")
                self.assertEqual(b"NFCPBCAT", pack.read_bytes()[:8])
                if attempt == 0:
                    first = pack.read_bytes()
                    pack.write_bytes(b"stale output must be regenerated")
                else:
                    self.assertEqual(first, pack.read_bytes())
            output = project.parent / "bin/Debug/net10.0"
            self.assertFalse(list(output.glob("*PrebuiltProfileCatalogGenerator*")))
            deps = (output / "NvtFwCombiner.Bootstrap.deps.json").read_text(encoding="utf-8")
            self.assertNotIn("PrebuiltProfileCatalogGenerator", deps)
            # An unlisted input must rerun actual runtime admission, even after a green build.
            index = json.loads((root / "profiles/built-in/package-trust-index.json").read_bytes())
            extra = root / "profiles/built-in" / index["bundles"][0]["bundleDirectory"] / "extra.json"
            extra.write_text("{}", encoding="utf-8")
            failed = run("build", project, "--no-restore", "-nologo")
            self.assertNotEqual(0, failed.returncode, failed.stdout + failed.stderr)
            self.assertFalse(pack.exists(), "failed admission retained a copyable old pack")
            self.assertFalse(list((project.parent / "obj").rglob("*.pack")))
            extra.unlink()
            # Publish uses the actual Desktop graph and the same executable-only flags as packaging.
            desktop = root / "src/NvtFwCombiner.Desktop/NvtFwCombiner.Desktop.csproj"
            restored = run("restore", desktop, "-r", "win-x64", "--source", cache,
                           "-p:NuGetAudit=false", "-p:PublishReadyToRun=true")
            self.assertEqual(0, restored.returncode, restored.stdout + restored.stderr)
            published = root / "published"
            result = run("publish", desktop, "-c", "Release", "-r", "win-x64", "--no-restore",
                         "--self-contained", "true", "-o", published,
                         "-p:PublishSingleFile=true", "-p:EnableCompressionInSingleFile=true",
                         "-p:PublishReadyToRun=true", "-p:PublishReadyToRunComposite=true")
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            packs = list(published.rglob("*.pack"))
            self.assertEqual([published / "profiles/built-in/prebuilt-profile-catalog.pack"], packs)
            self.assertEqual(first, packs[0].read_bytes())
            self.assertFalse(list(published.rglob("*PrebuiltProfileCatalogGenerator*")))
            self.assertTrue((published / "NvtFwCombiner.Desktop.exe").is_file())


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

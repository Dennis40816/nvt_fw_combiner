from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
import zipfile
from pathlib import Path

from scripts.create_managed_installation_lab import build_managed_root


class ManagedInstallationLabTests(unittest.TestCase):
    def test_creates_immutable_bootstrap_and_version_scoped_launcher(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source"
            bootstrap = root / "NvtFwCombiner.Bootstrap.exe"
            bootstrap.write_bytes(b"immutable-bootstrap")
            self.create_source(source)
            output = root / "managed"

            build_managed_root(output, source, bootstrap)

            self.assertEqual(b"immutable-bootstrap", (output / bootstrap.name).read_bytes())
            self.assertFalse((output / "NvtFwCombiner.Launcher.exe").exists())
            self.assertTrue(
                (output / "versions" / "1.0.0" / "launcher" / "NvtFwCombiner.Launcher.exe").is_file()
            )
            state = json.loads((output / "version-manager.seed.v1.json").read_text("utf-8"))
            self.assertEqual("1.0.0", state["activeVersion"])

    def test_rejects_launcher_identity_mismatch(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source"
            bootstrap = root / "NvtFwCombiner.Bootstrap.exe"
            bootstrap.write_bytes(b"immutable-bootstrap")
            self.create_source(source, wrong_launcher_hash=True)

            with self.assertRaisesRegex(ValueError, "seed launcher differs"):
                build_managed_root(root / "managed", source, bootstrap, "1.0.0")

    @staticmethod
    def create_source(source: Path, wrong_launcher_hash: bool = False) -> None:
        source.mkdir()
        packages = source / "packages"
        packages.mkdir()
        version = "1.0.0"
        launcher = b"managed-launcher"
        launcher_hash = hashlib.sha256(launcher).hexdigest()
        declared_hash = "0" * 64 if wrong_launcher_hash else launcher_hash
        manifest = {
            "schemaVersion": "1.2",
            "product": "NVT FW Combiner",
            "version": version,
            "versionManagementProtocolVersion": 1,
            "launcher": {
                "launcherVersion": version,
                "protocolVersion": 1,
                "executableRelativePath": "launcher/NvtFwCombiner.Launcher.exe",
                "size": len(launcher),
                "sha256": declared_hash,
            },
            "files": [
                {
                    "path": "launcher/NvtFwCombiner.Launcher.exe",
                    "size": len(launcher),
                    "sha256": declared_hash,
                    "role": "launcher",
                }
            ],
        }
        manifest_bytes = json.dumps(manifest, separators=(",", ":")).encode()
        package = packages / f"NvtFwCombiner-v{version}-win-x64.zip"
        prefix = f"NvtFwCombiner-v{version}-win-x64"
        with zipfile.ZipFile(package, "w") as archive:
            archive.writestr(f"{prefix}/NvtFwCombiner.exe", b"application")
            archive.writestr(f"{prefix}/launcher/NvtFwCombiner.Launcher.exe", launcher)
            archive.writestr(f"{prefix}/RELEASE-MANIFEST.json", manifest_bytes)
        package_bytes = package.read_bytes()
        catalog = {
            "schemaVersion": 1,
            "product": "NVT FW Combiner",
            "runtimeIdentifier": "win-x64",
            "versions": [
                {
                    "version": version,
                    "packagePath": f"packages/{package.name}",
                    "packageSize": len(package_bytes),
                    "packageSha256": hashlib.sha256(package_bytes).hexdigest(),
                    "releaseManifestSha256": hashlib.sha256(manifest_bytes).hexdigest(),
                }
            ],
        }
        (source / "update-catalog.v1.json").write_text(json.dumps(catalog), encoding="utf-8")


if __name__ == "__main__":
    unittest.main()

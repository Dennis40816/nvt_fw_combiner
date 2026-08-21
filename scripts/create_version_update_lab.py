#!/usr/bin/env python3
"""Create release-safe local update packages for the managed-version lab."""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import tempfile
import zipfile
from pathlib import Path


PRODUCT = "NVT FW Combiner"
RID = "win-x64"
VERSIONS = ("0.10.5", "0.10.6")
ZIP_TIMESTAMP = (2026, 8, 21, 0, 0, 0)
CATALOG_NAME = "update-catalog.v1.json"


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _json_bytes(value: object) -> bytes:
    return json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8")


def _probe_payload(probe_root: Path) -> dict[str, bytes]:
    executable = probe_root / "NvtFwCombiner.ReadyProbe.exe"
    if not executable.is_file():
        raise FileNotFoundError(f"ready probe has not been built: {executable}")
    files: dict[str, bytes] = {
        "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe": b"MZ-synthetic-worker",
        "THIRD-PARTY-NOTICES.txt": b"Synthetic local version-management lab only.\n",
        "LICENSE.txt": b"MIT\n",
        "README.txt": b"Synthetic local version-management lab payload.\n",
    }
    for source in sorted(probe_root.iterdir(), key=lambda path: path.name.casefold()):
        if not source.is_file() or source.suffix.casefold() == ".pdb":
            continue
        target = "NvtFwCombiner.exe" if source.name.casefold() == executable.name.casefold() else source.name
        files[target] = source.read_bytes()
    return files


def _role(path: str) -> str:
    return {
        "NvtFwCombiner.exe": "application",
        "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe": "crcWorker",
        "THIRD-PARTY-NOTICES.txt": "notices",
        "LICENSE.txt": "license",
        "README.txt": "readme",
    }.get(path, "externalTool")


def _manifest(version: str, files: dict[str, bytes]) -> bytes:
    return _json_bytes(
        {
            "schemaVersion": "1.1",
            "product": PRODUCT,
            "version": version,
            "sourceCommit": "a" * 40,
            "sourceTag": f"v{version}",
            "runtimeIdentifier": RID,
            "licenseSpdx": "MIT",
            "workerProtocolVersions": ["1.0"],
            "approvedProcessorIds": [],
            "processorBundleSha256": "b" * 64,
            "embeddedProfileCatalogSha256": "c" * 64,
            "embeddedSchemaBundleSha256": "d" * 64,
            "files": [
                {
                    "path": path,
                    "size": len(files[path]),
                    "sha256": _sha256(files[path]),
                    "role": _role(path),
                }
                for path in sorted(files)
            ],
            "sbomAsset": f"NvtFwCombiner-v{version}-win-x64.spdx.json",
            "provenanceAsset": f"NvtFwCombiner-v{version}-win-x64.provenance.json",
        }
    )


def _write_zip(path: Path, version: str, files: dict[str, bytes], manifest: bytes) -> None:
    root = f"NvtFwCombiner-v{version}-win-x64"
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for relative, data in [("RELEASE-MANIFEST.json", manifest), *sorted(files.items())]:
            entry = zipfile.ZipInfo(f"{root}/{relative}", ZIP_TIMESTAMP)
            entry.compress_type = zipfile.ZIP_DEFLATED
            entry.external_attr = 0o100644 << 16
            archive.writestr(entry, data)


def build_lab(output_root: Path, probe_root: Path) -> None:
    """Atomically create one source folder; an existing destination is never overwritten."""
    output_root = output_root.resolve()
    if output_root.exists():
        raise FileExistsError(f"refusing to overwrite existing lab: {output_root}")
    output_root.parent.mkdir(parents=True, exist_ok=True)
    staging = Path(tempfile.mkdtemp(prefix=f".{output_root.name}-", dir=output_root.parent))
    try:
        packages = staging / "packages"
        packages.mkdir()
        catalog_versions: list[dict[str, object]] = []
        files = _probe_payload(probe_root.resolve())
        for version in VERSIONS:
            manifest = _manifest(version, files)
            relative = f"packages/NvtFwCombiner-v{version}-win-x64.zip"
            package = staging / relative
            _write_zip(package, version, files, manifest)
            package_bytes = package.read_bytes()
            catalog_versions.append(
                {
                    "version": version,
                    "publishedAt": "2026-08-21T00:00:00Z",
                    "packagePath": relative,
                    "packageSize": len(package_bytes),
                    "packageSha256": _sha256(package_bytes),
                    "releaseManifestSha256": _sha256(manifest),
                    "releaseNotes": f"Synthetic local upgrade lab {version}",
                }
            )
        (staging / CATALOG_NAME).write_bytes(
            _json_bytes(
                {
                    "schemaVersion": 1,
                    "product": PRODUCT,
                    "runtimeIdentifier": RID,
                    "versions": catalog_versions,
                }
            )
        )
        staging.rename(output_root)
    except BaseException:
        shutil.rmtree(staging, ignore_errors=True)
        raise


def main() -> int:
    repository = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output",
        type=Path,
        default=repository / "artifacts" / "version-update-source-lab",
    )
    parser.add_argument(
        "--probe-root",
        type=Path,
        default=repository
        / "tests"
        / "NvtFwCombiner.Infrastructure.Tests"
        / "bin"
        / "Debug"
        / "net10.0"
        / "ready-probe",
    )
    args = parser.parse_args()
    build_lab(args.output, args.probe_root)
    print(args.output.resolve())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

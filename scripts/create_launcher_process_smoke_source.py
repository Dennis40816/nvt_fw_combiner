#!/usr/bin/env python3
"""Create a deterministic two-version launcher process smoke source from published executables."""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import re
import shutil
import zipfile
from dataclasses import dataclass
from pathlib import Path

import create_update_catalog
from update_source_registry_policy import validate_registry_document


PRODUCT = "NVT FW Combiner"
VERSIONS = ("0.10.5", "0.10.6")
ZIP_TIMESTAMP = (2026, 8, 26, 0, 0, 0)
SMOKE_PUBLISHED_AT = "2026-08-31T00:00:00Z"


@dataclass(frozen=True)
class SingleSourceResult:
    """Exact local Registry and Catalog paths created around one published ZIP."""

    catalog_path: str
    registry_path: str


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _json_bytes(value: object) -> bytes:
    return json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8")


def _role(path: str) -> str:
    return {
        "NvtFwCombiner.exe": "application",
        "launcher/NvtFwCombiner.Launcher.exe": "launcher",
        "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe": "externalTool",
        "THIRD-PARTY-NOTICES.txt": "notices",
        "LICENSE.txt": "license",
        "README.txt": "readme",
        "docs/contracts/canonical-capability-policy-v1.json": "capabilityPolicy",
        "profiles/built-in/catalog.json": "builtInProfile",
    }[path]


def _package_files(app: bytes, launcher: bytes) -> dict[str, bytes]:
    return {
        "NvtFwCombiner.exe": app,
        "launcher/NvtFwCombiner.Launcher.exe": launcher,
        "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe": b"MZ-smoke-worker",
        "THIRD-PARTY-NOTICES.txt": b"Synthetic launcher process smoke only.\n",
        "LICENSE.txt": b"MIT\n",
        "README.txt": b"Synthetic launcher process smoke only.\n",
        "docs/contracts/canonical-capability-policy-v1.json": b"{}\n",
        "profiles/built-in/catalog.json": b"{}\n",
    }


def _manifest(version: str, files: dict[str, bytes]) -> bytes:
    launcher = files["launcher/NvtFwCombiner.Launcher.exe"]
    return _json_bytes(
        {
            "schemaVersion": "1.2",
            "product": PRODUCT,
            "version": version,
            "sourceCommit": "a" * 40,
            "sourceTag": f"v{version}",
            "runtimeIdentifier": "win-x64",
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
            "versionManagementProtocolVersion": 1,
            "launcher": {
                "launcherVersion": version,
                "protocolVersion": 1,
                "executableRelativePath": "launcher/NvtFwCombiner.Launcher.exe",
                "size": len(launcher),
                "sha256": _sha256(launcher),
            },
        }
    )


def _write_package(path: Path, version: str, files: dict[str, bytes], manifest: bytes) -> None:
    prefix = f"NvtFwCombiner-v{version}-win-x64/"
    checksums = {
        **{name: _sha256(data) for name, data in files.items()},
        "RELEASE-MANIFEST.json": _sha256(manifest),
    }
    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for name, data in [
            ("RELEASE-MANIFEST.json", manifest),
            ("SHA256SUMS.txt", "".join(f"{digest}  {name}\n" for name, digest in sorted(checksums.items())).encode()),
            *sorted(files.items()),
        ]:
            entry = zipfile.ZipInfo(prefix + name, ZIP_TIMESTAMP)
            entry.compress_type = zipfile.ZIP_DEFLATED
            entry.external_attr = 0o100644 << 16
            archive.writestr(entry, data)


def build_source(output: Path, app: Path, stable_launcher: Path, failing_launcher: Path) -> None:
    output.mkdir(parents=True)
    packages = output / "packages"
    packages.mkdir()
    versions: list[dict[str, object]] = []
    app_bytes = app.read_bytes()
    for version, launcher_path in zip(VERSIONS, (stable_launcher, failing_launcher), strict=True):
        files = _package_files(app_bytes, launcher_path.read_bytes())
        manifest = _manifest(version, files)
        relative = f"packages/NvtFwCombiner-v{version}-win-x64.zip"
        package = output / relative
        _write_package(package, version, files, manifest)
        package_bytes = package.read_bytes()
        versions.append(
            {
                "version": version,
                "publishedAt": "2026-08-26T00:00:00Z",
                "packagePath": relative,
                "packageSize": len(package_bytes),
                "packageSha256": _sha256(package_bytes),
                "releaseManifestSha256": _sha256(manifest),
                "releaseNotes": f"Synthetic launcher process smoke {version}",
            }
        )
    (output / "update-catalog.v1.json").write_bytes(
        _json_bytes(
            {
                "schemaVersion": 1,
                "product": PRODUCT,
                "runtimeIdentifier": "win-x64",
                "versions": versions,
            }
        )
    )


def build_single_source(output: Path, package: Path, version: str) -> SingleSourceResult:
    """Create one deterministic local source while preserving the exact published ZIP bytes."""

    if re.fullmatch(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)", version) is None:
        raise ValueError("version must be a stable three-component numeric version")
    package = package.resolve(strict=True)
    if not package.is_file():
        raise ValueError("published package must be an ordinary file")
    relative_package = f"packages/NvtFwCombiner-v{version}-win-x64.zip"
    output.mkdir(parents=True)
    try:
        packages = output / "packages"
        packages.mkdir()
        destination = output / relative_package
        with package.open("rb") as source, destination.open("xb") as target:
            shutil.copyfileobj(source, target)
        catalog_path = create_update_catalog.build_catalog(
            output,
            {version: SMOKE_PUBLISHED_AT},
            {version: f"Local Distribution Launcher E2E smoke {version}"},
        )
        catalog_bytes = catalog_path.read_bytes()
        registry_path = (output / "update-source-registry.json").resolve()
        registry_document = {
            "schemaVersion": 1,
            "registryId": "nvt-fw-combiner-production",
            "registryRevision": 1,
            "publishedAtUtc": SMOKE_PUBLISHED_AT,
            "catalogPublication": {
                "latestVersion": version,
                "catalogSchemaVersion": 1,
                "catalogSha256": _sha256(catalog_bytes),
            },
            "entries": [{"status": "latest", "catalogPath": str(catalog_path)}],
        }
        validate_registry_document(registry_document, "local smoke")
        registry_path.write_bytes(_json_bytes(registry_document))
        return SingleSourceResult(str(catalog_path), str(registry_path))
    except BaseException:
        shutil.rmtree(output)
        raise


def install_candidate(repository: Path, source: Path, managed_root: Path, state_path: Path) -> None:
    module_path = repository / "scripts" / "create_managed_installation_lab.py"
    spec = importlib.util.spec_from_file_location("create_managed_installation_lab", module_path)
    if spec is None or spec.loader is None:
        raise RuntimeError("managed installation lab owner could not be loaded")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    entry = module._catalog_entry(source, "0.10.6")
    identity, manifest_hash = module._extract_seed(source, managed_root, entry)
    state = json.loads(state_path.read_text(encoding="utf-8"))
    state["activeVersion"] = "0.10.6"
    state["lastKnownGoodVersion"] = "0.10.5"
    state["admissions"].append(
        {
            "version": "0.10.6",
            "admissionIdentity": identity,
            "releaseManifestSha256": manifest_hash,
        }
    )
    state_path.write_bytes(_json_bytes(state))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    create = subparsers.add_parser("create")
    create.add_argument("--output", type=Path, required=True)
    create.add_argument("--app", type=Path, required=True)
    create.add_argument("--stable-launcher", type=Path, required=True)
    create.add_argument("--failing-launcher", type=Path, required=True)
    create_single = subparsers.add_parser("create-single")
    create_single.add_argument("--output", type=Path, required=True)
    create_single.add_argument("--package", type=Path, required=True)
    create_single.add_argument("--version", required=True)
    install = subparsers.add_parser("install-candidate")
    install.add_argument("--repository", type=Path, required=True)
    install.add_argument("--source", type=Path, required=True)
    install.add_argument("--managed-root", type=Path, required=True)
    install.add_argument("--state-path", type=Path, required=True)
    args = parser.parse_args()
    if args.command == "create":
        build_source(args.output, args.app, args.stable_launcher, args.failing_launcher)
    elif args.command == "create-single":
        build_single_source(args.output, args.package, args.version)
    else:
        install_candidate(args.repository, args.source, args.managed_root, args.state_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
"""Rebuild one update-catalog.v1.json from the exact release ZIPs in packages/."""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import os
import re
import tempfile
import zipfile
from datetime import datetime
from pathlib import Path


CATALOG_NAME = "update-catalog.v1.json"
PRODUCT = "NVT FW Combiner"
RID = "win-x64"
PACKAGE_PATTERN = re.compile(
    r"^NvtFwCombiner-v(?P<version>0|[1-9][0-9]*)\."
    r"(?P<minor>0|[1-9][0-9]*)\.(?P<patch>0|[1-9][0-9]*)-win-x64\.zip$"
)
PUBLISHED_AT_PATTERN = re.compile(
    r"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}"
    r"(?:\.[0-9]{1,7})?Z$"
)
SEMVER_PATTERN = re.compile(r"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$")
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
ROOT_KEYS = {"schemaVersion", "product", "runtimeIdentifier", "versions"}
ENTRY_KEYS = {
    "version",
    "publishedAt",
    "packagePath",
    "packageSize",
    "packageSha256",
    "releaseManifestSha256",
    "releaseNotes",
}


def _maximum_package_bytes(version: str) -> int:
    return 134_217_728 if version == "1.0.0" else 80_000_000


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _strict_object(pairs: list[tuple[str, object]]) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"JSON object repeats property {key}")
        result[key] = value
    return result


def _load_strict_json(data: bytes, description: str) -> object:
    try:
        return json.loads(
            data.decode("utf-8"),
            object_pairs_hook=_strict_object,
            parse_constant=lambda value: (_ for _ in ()).throw(
                ValueError(f"non-finite JSON value {value}")
            ),
        )
    except (UnicodeDecodeError, json.JSONDecodeError, ValueError) as exception:
        raise ValueError(
            f"{description} is not strict UTF-8 JSON: {exception}"
        ) from exception


def _validate_published_at(value: str, version: str) -> None:
    if not PUBLISHED_AT_PATTERN.fullmatch(value):
        raise ValueError(f"publishedAt for {version} is not canonical UTC: {value}")
    try:
        _ = datetime.strptime(value[:19], "%Y-%m-%dT%H:%M:%S")
    except ValueError as exception:
        raise ValueError(
            f"publishedAt for {version} is not canonical UTC: {value}"
        ) from exception


def _reject_link(path: Path, description: str) -> None:
    is_junction = getattr(path, "is_junction", lambda: False)
    if path.is_symlink() or is_junction():
        raise ValueError(f"{description} cannot be a symbolic link or junction: {path}")


def _parse_assignments(values: list[str], option: str) -> dict[str, str]:
    result: dict[str, str] = {}
    for value in values:
        version, separator, assigned = value.partition("=")
        if not separator or not version or not assigned:
            raise ValueError(f"{option} requires VERSION=VALUE: {value}")
        if version in result:
            raise ValueError(f"{option} repeats version {version}")
        result[version] = assigned
    return result


def _existing_metadata(source_root: Path) -> dict[str, dict[str, object]]:
    catalog_path = source_root / CATALOG_NAME
    if not catalog_path.is_file():
        return {}
    raw = catalog_path.read_bytes()
    if len(raw) > 1_048_576:
        raise ValueError("existing update catalog exceeds 1 MiB")
    document = _load_strict_json(raw, "existing update catalog")
    if (
        not isinstance(document, dict)
        or set(document) != ROOT_KEYS
        or document.get("schemaVersion") != 1
        or document.get("product") != PRODUCT
        or document.get("runtimeIdentifier") != RID
        or not isinstance(document.get("versions"), list)
    ):
        raise ValueError("existing update catalog identity is invalid")
    versions = document["versions"]
    if not 1 <= len(versions) <= 128:
        raise ValueError("existing update catalog must contain 1 through 128 versions")
    entries: dict[str, dict[str, object]] = {}
    for entry in versions:
        if not isinstance(entry, dict) or set(entry) != ENTRY_KEYS:
            raise ValueError("existing update catalog entry shape is invalid")
        version = entry["version"]
        published_at = entry["publishedAt"]
        package_path = entry["packagePath"]
        package_size = entry["packageSize"]
        package_sha256 = entry["packageSha256"]
        manifest_sha256 = entry["releaseManifestSha256"]
        release_notes = entry["releaseNotes"]
        if not isinstance(version, str) or not SEMVER_PATTERN.fullmatch(version):
            raise ValueError("existing update catalog version is invalid")
        if not isinstance(published_at, str):
            raise ValueError(f"existing publishedAt for {version} is invalid")
        _validate_published_at(published_at, version)
        if package_path != f"packages/NvtFwCombiner-v{version}-win-x64.zip":
            raise ValueError(f"existing packagePath for {version} is not canonical")
        if (
            isinstance(package_size, bool)
            or not isinstance(package_size, int)
            or not 1 <= package_size <= _maximum_package_bytes(version)
        ):
            raise ValueError(f"existing packageSize for {version} is invalid")
        if not isinstance(package_sha256, str) or not SHA256_PATTERN.fullmatch(
            package_sha256
        ):
            raise ValueError(f"existing packageSha256 for {version} is invalid")
        if not isinstance(manifest_sha256, str) or not SHA256_PATTERN.fullmatch(
            manifest_sha256
        ):
            raise ValueError(f"existing releaseManifestSha256 for {version} is invalid")
        if (
            not isinstance(release_notes, str)
            or len(release_notes.encode("utf-8")) > 65_536
        ):
            raise ValueError(f"existing releaseNotes for {version} are invalid")
        if version in entries:
            raise ValueError(f"existing update catalog repeats version {version}")
        entries[version] = entry
    return entries


def _reject_existing_metadata_drift(
    existing_entries: dict[str, dict[str, object]],
    published_at_overrides: dict[str, str],
    release_notes_overrides: dict[str, str],
) -> None:
    for version, published_at in published_at_overrides.items():
        existing = existing_entries.get(version)
        if existing is not None and published_at != existing["publishedAt"]:
            raise ValueError(
                f"existing stable publishedAt changed for version {version}"
            )
    for version, release_notes in release_notes_overrides.items():
        existing = existing_entries.get(version)
        if existing is not None and release_notes != existing["releaseNotes"]:
            raise ValueError(
                f"existing stable releaseNotes changed for version {version}"
            )


def _package_entry(
    source_root: Path,
    package: Path,
    published_at: str,
    release_notes: str,
) -> dict[str, object]:
    match = PACKAGE_PATTERN.fullmatch(package.name)
    if match is None:
        raise ValueError(f"release ZIP name is not canonical: {package.name}")
    version = ".".join(
        (match.group("version"), match.group("minor"), match.group("patch"))
    )
    _validate_published_at(published_at, version)
    if len(release_notes.encode("utf-8")) > 65_536:
        raise ValueError(f"release notes for {version} exceed 65,536 UTF-8 bytes")

    package_bytes = package.read_bytes()
    if not 1 <= len(package_bytes) <= _maximum_package_bytes(version):
        raise ValueError(
            f"release ZIP size is outside the catalog bound: {package.name}"
        )
    package_root = f"NvtFwCombiner-v{version}-win-x64/"
    manifest_name = f"{package_root}RELEASE-MANIFEST.json"
    with zipfile.ZipFile(io.BytesIO(package_bytes)) as archive:
        files = [item.filename for item in archive.infolist() if not item.is_dir()]
        if manifest_name not in files or any(
            not name.startswith(package_root) for name in files
        ):
            raise ValueError(
                f"release ZIP has an unexpected root or manifest path: {package.name}"
            )
        manifest = archive.read(manifest_name)
    manifest_document = _load_strict_json(manifest, "release manifest")
    if (
        not isinstance(manifest_document, dict)
        or manifest_document.get("product") != PRODUCT
        or manifest_document.get("version") != version
        or manifest_document.get("runtimeIdentifier") != RID
    ):
        raise ValueError(
            f"release manifest identity differs from ZIP name: {package.name}"
        )

    package_relative = package.relative_to(source_root).as_posix()
    return {
        "version": version,
        "publishedAt": published_at,
        "packagePath": package_relative,
        "packageSize": len(package_bytes),
        "packageSha256": _sha256(package_bytes),
        "releaseManifestSha256": _sha256(manifest),
        "releaseNotes": release_notes,
    }


def build_catalog(
    source_root: Path,
    published_at_overrides: dict[str, str],
    release_notes_overrides: dict[str, str],
) -> Path:
    """Atomically rebuild the catalog from every canonical ZIP directly under packages/."""
    source_root = source_root.absolute()
    _reject_link(source_root, "update source root")
    source_root = source_root.resolve(strict=True)
    packages_root = source_root / "packages"
    if not packages_root.is_dir():
        raise FileNotFoundError(
            f"update source packages directory is missing: {packages_root}"
        )
    _reject_link(packages_root, "update source packages directory")

    existing_entries = _existing_metadata(source_root)
    _reject_existing_metadata_drift(
        existing_entries,
        published_at_overrides,
        release_notes_overrides,
    )
    packages = sorted(
        packages_root.glob("*.zip"), key=lambda path: path.name.casefold()
    )
    if not packages:
        raise ValueError("update source packages directory contains no release ZIP")
    if len(packages) > 128:
        raise ValueError("update source must contain 1 through 128 release ZIPs")

    entries: list[dict[str, object]] = []
    seen_versions: set[str] = set()
    for package in packages:
        _reject_link(package, "release ZIP")
        match = PACKAGE_PATTERN.fullmatch(package.name)
        if match is None:
            raise ValueError(f"release ZIP name is not canonical: {package.name}")
        version = ".".join(
            (match.group("version"), match.group("minor"), match.group("patch"))
        )
        if version in seen_versions:
            raise ValueError(f"packages directory repeats version {version}")
        seen_versions.add(version)
        existing = existing_entries.get(version)
        published_at = published_at_overrides.get(
            version, None if existing is None else str(existing["publishedAt"])
        )
        release_notes = release_notes_overrides.get(
            version, None if existing is None else str(existing["releaseNotes"])
        )
        if published_at is None or release_notes is None:
            raise ValueError(
                f"new package {version} requires --published-at and --release-notes-file"
            )
        entry = _package_entry(source_root, package, published_at, release_notes)
        if existing is not None and any(
            entry[field] != existing[field]
            for field in ("packageSize", "packageSha256", "releaseManifestSha256")
        ):
            raise ValueError(
                f"existing stable package identity changed for version {version}"
            )
        entries.append(entry)

    unknown_overrides = (
        published_at_overrides.keys() | release_notes_overrides.keys()
    ) - seen_versions
    if unknown_overrides:
        raise ValueError(
            "metadata was supplied for packages that are not present: "
            + ", ".join(sorted(unknown_overrides))
        )
    entries.sort(
        key=lambda entry: tuple(int(part) for part in str(entry["version"]).split("."))
    )
    document = {
        "schemaVersion": 1,
        "product": PRODUCT,
        "runtimeIdentifier": RID,
        "versions": entries,
    }
    encoded = json.dumps(document, ensure_ascii=False, separators=(",", ":")).encode(
        "utf-8"
    )
    if len(encoded) > 1_048_576:
        raise ValueError("generated update catalog exceeds 1 MiB")

    destination = source_root / CATALOG_NAME
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{CATALOG_NAME}.",
        suffix=".tmp",
        dir=source_root,
    )
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(encoded)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary_name, destination)
    except BaseException:
        try:
            os.unlink(temporary_name)
        except FileNotFoundError:
            pass
        raise
    return destination


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument(
        "--published-at",
        action="append",
        default=[],
        metavar="VERSION=UTC",
        help="Required for each package not already present in the current catalog.",
    )
    parser.add_argument(
        "--release-notes-file",
        action="append",
        default=[],
        metavar="VERSION=PATH",
        help="UTF-8 notes file required for each package not already in the catalog.",
    )
    args = parser.parse_args()
    published = _parse_assignments(args.published_at, "--published-at")
    note_paths = _parse_assignments(args.release_notes_file, "--release-notes-file")
    notes = {
        version: Path(path).read_text(encoding="utf-8")
        for version, path in note_paths.items()
    }
    destination = build_catalog(args.source_root, published, notes)
    print(destination)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

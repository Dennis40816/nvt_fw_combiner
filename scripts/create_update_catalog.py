#!/usr/bin/env python3
"""Rebuild one strict update Catalog from the exact release ZIPs in packages/."""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import os
import re
import tempfile
import zipfile
from contextlib import contextmanager
from collections.abc import Iterator
from datetime import datetime
from pathlib import Path

try:
    from update_source_registry_policy import (
        load_strict_registry_json as _load_strict_registry_json,
        validate_canonical_utc as _validate_registry_published_at,
        validate_registry_document as _validate_registry_document,
        validate_registry_entries as _shared_validate_registry_entries,
        validate_registry_revision as _validate_registry_revision,
        validate_registry_template as _shared_validate_registry_template,
    )
except ModuleNotFoundError:  # importlib-based repository tests load through scripts.*
    from scripts.update_source_registry_policy import (
        load_strict_registry_json as _load_strict_registry_json,
        validate_canonical_utc as _validate_registry_published_at,
        validate_registry_document as _validate_registry_document,
        validate_registry_entries as _shared_validate_registry_entries,
        validate_registry_revision as _validate_registry_revision,
        validate_registry_template as _shared_validate_registry_template,
    )


CATALOG_NAME = "update-catalog.v1.json"
CATALOG_V2_NAME = "update-catalog.v2.json"
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
V2_ENTRY_KEYS = ENTRY_KEYS | {"notificationPolicy"}
NOTIFICATION_POLICIES = {"manual-only", "notify"}
PUBLICATION_LOCK_NAME = ".update-source.publisher.lock"
MAXIMUM_PACKAGE_BYTES = 134_217_728


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


def _catalog_name(schema_version: int) -> str:
    if schema_version == 1:
        return CATALOG_NAME
    if schema_version == 2:
        return CATALOG_V2_NAME
    raise ValueError(f"catalog schema version is unsupported: {schema_version}")


def _existing_metadata(
    source_root: Path,
    schema_version: int = 1,
) -> dict[str, dict[str, object]]:
    catalog_path = source_root / _catalog_name(schema_version)
    expected_schema_version = schema_version
    if schema_version == 2 and not catalog_path.is_file():
        if catalog_path.exists() or catalog_path.is_symlink():
            raise ValueError("existing Catalog v2 is not a regular file")
        catalog_path = source_root / CATALOG_NAME
        expected_schema_version = 1
    if not catalog_path.is_file():
        return {}
    raw = catalog_path.read_bytes()
    return _validated_catalog_entries(
        raw,
        "existing",
        expected_schema_version=expected_schema_version,
    )


def _validated_catalog_entries(
    raw: bytes,
    label: str,
    expected_schema_version: int = 1,
) -> dict[str, dict[str, object]]:
    _ = _catalog_name(expected_schema_version)
    if len(raw) > 1_048_576:
        raise ValueError(f"{label} update catalog exceeds 1 MiB")
    document = _load_strict_json(raw, f"{label} update catalog")
    if (
        not isinstance(document, dict)
        or set(document) != ROOT_KEYS
        or document.get("schemaVersion") != expected_schema_version
        or document.get("product") != PRODUCT
        or document.get("runtimeIdentifier") != RID
        or not isinstance(document.get("versions"), list)
    ):
        raise ValueError(f"{label} update catalog identity is invalid")
    versions = document["versions"]
    if not 1 <= len(versions) <= 128:
        raise ValueError(
            f"{label} update catalog must contain 1 through 128 versions"
        )
    entries: dict[str, dict[str, object]] = {}
    for entry in versions:
        expected_entry_keys = (
            ENTRY_KEYS if expected_schema_version == 1 else V2_ENTRY_KEYS
        )
        if not isinstance(entry, dict) or set(entry) != expected_entry_keys:
            raise ValueError(f"{label} update catalog entry shape is invalid")
        version = entry["version"]
        published_at = entry["publishedAt"]
        package_path = entry["packagePath"]
        package_size = entry["packageSize"]
        package_sha256 = entry["packageSha256"]
        manifest_sha256 = entry["releaseManifestSha256"]
        release_notes = entry["releaseNotes"]
        notification_policy = entry.get("notificationPolicy")
        if not isinstance(version, str) or not SEMVER_PATTERN.fullmatch(version):
            raise ValueError(f"{label} update catalog version is invalid")
        if not isinstance(published_at, str):
            raise ValueError(f"{label} publishedAt for {version} is invalid")
        _validate_published_at(published_at, version)
        if package_path != f"packages/NvtFwCombiner-v{version}-win-x64.zip":
            raise ValueError(f"{label} packagePath for {version} is not canonical")
        if (
            isinstance(package_size, bool)
            or not isinstance(package_size, int)
            or not 1 <= package_size <= MAXIMUM_PACKAGE_BYTES
        ):
            raise ValueError(f"{label} packageSize for {version} is invalid")
        if not isinstance(package_sha256, str) or not SHA256_PATTERN.fullmatch(
            package_sha256
        ):
            raise ValueError(f"{label} packageSha256 for {version} is invalid")
        if not isinstance(manifest_sha256, str) or not SHA256_PATTERN.fullmatch(
            manifest_sha256
        ):
            raise ValueError(
                f"{label} releaseManifestSha256 for {version} is invalid"
            )
        if (
            not isinstance(release_notes, str)
            or len(release_notes.encode("utf-8")) > 65_536
        ):
            raise ValueError(f"{label} releaseNotes for {version} are invalid")
        if expected_schema_version == 2 and notification_policy not in NOTIFICATION_POLICIES:
            raise ValueError(
                f"{label} notificationPolicy for {version} is invalid"
            )
        if version in entries:
            raise ValueError(f"{label} update catalog repeats version {version}")
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
    if not 1 <= len(package_bytes) <= MAXIMUM_PACKAGE_BYTES:
        raise ValueError(
            f"release ZIP size is outside the catalog bound: {package.name}"
        )
    manifest = _read_release_manifest(package_bytes, package.name, version)
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


def _read_release_manifest(
    package_bytes: bytes,
    package_name: str,
    version: str,
) -> bytes:
    package_root = f"NvtFwCombiner-v{version}-win-x64/"
    manifest_name = f"{package_root}RELEASE-MANIFEST.json"
    with zipfile.ZipFile(io.BytesIO(package_bytes)) as archive:
        files = [item.filename for item in archive.infolist() if not item.is_dir()]
        if manifest_name not in files or any(
            not name.startswith(package_root) for name in files
        ):
            raise ValueError(
                f"release ZIP has an unexpected root or manifest path: {package_name}"
            )
        return archive.read(manifest_name)


def _validate_manifest_copy_destination(source_root: Path, destination: Path) -> Path:
    source_root = source_root.absolute().resolve(strict=True)
    destination = destination.absolute()
    if destination.parent.resolve(strict=True) != source_root:
        raise ValueError("release manifest copy must be a direct child of source root")
    if destination.name != "RELEASE-MANIFEST.json":
        raise ValueError("release manifest copy must be named RELEASE-MANIFEST.json")
    if destination.exists():
        raise FileExistsError(f"refusing to replace release manifest copy: {destination}")
    return destination


def _remove_owned_publication(
    destination: Path,
    published: bytes,
    description: str,
) -> None:
    try:
        current = destination.read_bytes()
    except FileNotFoundError as exception:
        raise RuntimeError(
            f"{description} disappeared before rollback cleanup"
        ) from exception
    if current != published:
        raise RuntimeError(
            f"{description} changed before rollback cleanup; preserving current bytes"
        )
    destination.unlink()


def _prepare_release_manifest(
    source_root: Path,
    version: str,
    destination: Path,
    catalog_schema_version: int = 1,
    catalog_bytes: bytes | None = None,
) -> tuple[Path, bytes]:
    """Validate and return one Catalog-bound manifest without publishing it."""
    source_root = source_root.absolute()
    _reject_link(source_root, "update source root")
    source_root = source_root.resolve(strict=True)
    destination = _validate_manifest_copy_destination(source_root, destination)

    entries = (
        _existing_metadata(source_root, catalog_schema_version)
        if catalog_bytes is None
        else _validated_catalog_entries(
            catalog_bytes,
            "prepared",
            expected_schema_version=catalog_schema_version,
        )
    )
    entry = entries.get(version)
    if entry is None:
        raise ValueError(f"release manifest copy version is not in catalog: {version}")
    packages_root = source_root / "packages"
    if not packages_root.is_dir():
        raise FileNotFoundError(
            f"update source packages directory is missing: {packages_root}"
        )
    _reject_link(packages_root, "update source packages directory")
    package = source_root / str(entry["packagePath"])
    _reject_link(package, "release ZIP")
    package_bytes = package.read_bytes()
    if _sha256(package_bytes) != entry["packageSha256"]:
        raise ValueError(f"release package changed after catalog generation: {version}")
    manifest = _read_release_manifest(package_bytes, package.name, version)
    if _sha256(manifest) != entry["releaseManifestSha256"]:
        raise ValueError(f"release manifest changed after catalog generation: {version}")
    return destination, manifest


def _publish_release_manifest(
    source_root: Path,
    version: str,
    destination: Path,
    catalog_schema_version: int = 1,
) -> tuple[Path, bytes]:
    """Publish one Catalog-bound manifest and return its exact owned bytes."""
    destination, manifest = _prepare_release_manifest(
        source_root,
        version,
        destination,
        catalog_schema_version,
    )
    return _publish_prepared_release_manifest(destination, manifest)


def _publish_prepared_release_manifest(
    destination: Path,
    manifest: bytes,
) -> tuple[Path, bytes]:
    """Create one previously validated manifest without recomputing output."""

    descriptor = os.open(
        destination,
        os.O_WRONLY | os.O_CREAT | os.O_EXCL,
        0o600,
    )
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(manifest)
            stream.flush()
            os.fsync(stream.fileno())
    except BaseException:
        try:
            _remove_owned_publication(
                destination,
                manifest,
                "published release manifest",
            )
        except (FileNotFoundError, RuntimeError):
            pass
        raise
    return destination, manifest


def copy_release_manifest(
    source_root: Path,
    version: str,
    destination: Path,
    catalog_schema_version: int = 1,
) -> Path:
    """Copy one catalog-bound inner release manifest for operator inspection."""
    published, _ = _publish_release_manifest(
        source_root,
        version,
        destination,
        catalog_schema_version,
    )
    return published


@contextmanager
def _publication_lock(source_root: Path) -> Iterator[None]:
    lock_path = source_root / PUBLICATION_LOCK_NAME
    temporary_flag = getattr(os, "O_TEMPORARY", 0)
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    flags |= getattr(os, "O_NOINHERIT", 0)
    flags |= temporary_flag
    try:
        descriptor = os.open(lock_path, flags, 0o600)
    except FileExistsError as exception:
        raise RuntimeError(
            f"another update-source publisher owns {lock_path}"
        ) from exception
    try:
        with os.fdopen(descriptor, "w", encoding="ascii", newline="\n") as stream:
            stream.write(f"pid={os.getpid()}\n")
            stream.flush()
            os.fsync(stream.fileno())
            yield
    finally:
        if temporary_flag == 0:
            lock_path.unlink(missing_ok=True)


def _prepare_catalog_unlocked(
    source_root: Path,
    published_at_overrides: dict[str, str],
    release_notes_overrides: dict[str, str],
    catalog_schema_version: int = 1,
    notification_policies: dict[str, str] | None = None,
) -> tuple[Path, bytes]:
    """Validate and encode a Catalog without publishing any bytes."""
    source_root = source_root.absolute()
    _reject_link(source_root, "update source root")
    source_root = source_root.resolve(strict=True)
    packages_root = source_root / "packages"
    if not packages_root.is_dir():
        raise FileNotFoundError(
            f"update source packages directory is missing: {packages_root}"
        )
    _reject_link(packages_root, "update source packages directory")

    catalog_name = _catalog_name(catalog_schema_version)
    policies = {} if notification_policies is None else dict(notification_policies)
    if catalog_schema_version == 1 and policies:
        raise ValueError("Catalog v1 does not accept notification-policy assignments")
    if catalog_schema_version == 2:
        invalid_policies = {
            version: policy
            for version, policy in policies.items()
            if policy not in NOTIFICATION_POLICIES
        }
        if invalid_policies:
            version = sorted(invalid_policies)[0]
            raise ValueError(
                f"notificationPolicy for {version} is invalid: {invalid_policies[version]}"
            )

    existing_entries = _existing_metadata(source_root, catalog_schema_version)
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

    package_versions: set[str] = set()
    for package in packages:
        match = PACKAGE_PATTERN.fullmatch(package.name)
        if match is None:
            raise ValueError(f"release ZIP name is not canonical: {package.name}")
        version = ".".join(
            (match.group("version"), match.group("minor"), match.group("patch"))
        )
        if version in package_versions:
            raise ValueError(f"packages directory repeats version {version}")
        package_versions.add(version)
    if catalog_schema_version == 2 and policies.keys() != package_versions:
        missing = package_versions - policies.keys()
        extra = policies.keys() - package_versions
        details: list[str] = []
        if missing:
            details.append("missing " + ", ".join(sorted(missing)))
        if extra:
            details.append("extra " + ", ".join(sorted(extra)))
        raise ValueError(
            "Catalog v2 requires exactly one notification policy per version: "
            + "; ".join(details)
        )

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
        if catalog_schema_version == 2:
            entry["notificationPolicy"] = policies[version]
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
        "schemaVersion": catalog_schema_version,
        "product": PRODUCT,
        "runtimeIdentifier": RID,
        "versions": entries,
    }
    encoded = json.dumps(document, ensure_ascii=False, separators=(",", ":")).encode(
        "utf-8"
    )
    if len(encoded) > 1_048_576:
        raise ValueError("generated update catalog exceeds 1 MiB")

    return source_root / catalog_name, encoded


def _publish_catalog_unlocked(destination: Path, encoded: bytes) -> Path:
    """Atomically replace one already prepared Catalog under the publisher lock."""
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{destination.name}.",
        suffix=".tmp",
        dir=destination.parent,
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


def _build_catalog_unlocked(
    source_root: Path,
    published_at_overrides: dict[str, str],
    release_notes_overrides: dict[str, str],
    catalog_schema_version: int = 1,
    notification_policies: dict[str, str] | None = None,
) -> Path:
    """Build a Catalog while the caller owns the update-source publication lock."""
    destination, encoded = _prepare_catalog_unlocked(
        source_root,
        published_at_overrides,
        release_notes_overrides,
        catalog_schema_version,
        notification_policies,
    )
    return _publish_catalog_unlocked(destination, encoded)


def build_catalog(
    source_root: Path,
    published_at_overrides: dict[str, str],
    release_notes_overrides: dict[str, str],
    catalog_schema_version: int = 1,
    notification_policies: dict[str, str] | None = None,
) -> Path:
    """Atomically rebuild the Catalog under the sole publisher lock."""
    source_root_argument = source_root.absolute()
    _reject_link(source_root_argument, "update source root")
    resolved_source_root = source_root_argument.resolve(strict=True)
    with _publication_lock(resolved_source_root):
        return _build_catalog_unlocked(
            resolved_source_root,
            published_at_overrides,
            release_notes_overrides,
            catalog_schema_version,
            notification_policies,
        )


def _validate_registry_entries(entries: object, label: str) -> None:
    _shared_validate_registry_entries(entries, label)


def _validate_registry_template(template: object) -> dict[str, object]:
    return _shared_validate_registry_template(template)


def _validate_rendered_registry(document: object) -> dict[str, object]:
    _validate_registry_document(document, "rendered")
    assert isinstance(document, dict)
    return document


def _preflight_registry_render(
    template_path: Path,
    destination: Path,
    revision: int,
    published_at: str,
) -> dict[str, object]:
    template_bytes = template_path.read_bytes()
    template = _validate_registry_template(
        _load_strict_registry_json(template_bytes)
    )
    _validate_registry_revision(revision)
    _validate_registry_published_at(published_at, "Registry publishedAtUtc")
    if destination.exists():
        raise FileExistsError(f"refusing to replace rendered Registry: {destination}")
    return template


def _prepare_registry_render(
    template_path: Path,
    catalog_bytes: bytes,
    destination: Path,
    revision: int,
    published_at: str,
) -> bytes:
    """Validate and encode a Registry without publishing any bytes."""
    template = _preflight_registry_render(
        template_path,
        destination,
        revision,
        published_at,
    )

    catalog_document = _load_strict_json(catalog_bytes, "rendered update catalog")
    if not isinstance(catalog_document, dict):
        raise ValueError("rendered update catalog identity is invalid")
    catalog_schema_version = catalog_document.get("schemaVersion")
    if catalog_schema_version not in (1, 2):
        raise ValueError("rendered update catalog schema version is invalid")
    catalog_entries = _validated_catalog_entries(
        catalog_bytes,
        "rendered",
        expected_schema_version=catalog_schema_version,
    )
    latest = max(
        catalog_entries,
        key=lambda version: tuple(int(part) for part in version.split(".")),
    )

    rendered = dict(template)
    rendered["registryRevision"] = revision
    rendered["publishedAtUtc"] = published_at
    rendered["catalogPublication"] = {
        "latestVersion": latest,
        "catalogSchemaVersion": catalog_schema_version,
        "catalogSha256": _sha256(catalog_bytes),
    }
    _validate_rendered_registry(rendered)
    encoded = json.dumps(rendered, ensure_ascii=False, separators=(",", ":")).encode(
        "utf-8"
    )
    _validate_rendered_registry(_load_strict_registry_json(encoded))
    return encoded


def render_registry(
    template_path: Path,
    catalog_path: Path,
    destination: Path,
    revision: int,
    published_at: str,
) -> Path:
    """Render one deployable Registry bound to exact validated Catalog bytes."""
    encoded = _prepare_registry_render(
        template_path,
        catalog_path.read_bytes(),
        destination,
        revision,
        published_at,
    )
    return _publish_prepared_registry(destination, encoded)


def _publish_prepared_registry(destination: Path, encoded: bytes) -> Path:
    """Create and verify one previously validated Registry output."""
    descriptor = os.open(destination, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(encoded)
            stream.flush()
            os.fsync(stream.fileno())
    except BaseException:
        try:
            _remove_owned_publication(destination, encoded, "rendered Registry")
        except (FileNotFoundError, RuntimeError):
            pass
        raise
    try:
        written = destination.read_bytes()
        if written != encoded:
            raise ValueError("rendered Registry changed during publication")
    except BaseException:
        try:
            _remove_owned_publication(destination, encoded, "rendered Registry")
        except (FileNotFoundError, RuntimeError):
            pass
        raise
    return destination


def _restore_catalog_after_combined_failure(
    source_root: Path,
    original: bytes | None,
    published: bytes,
    catalog_name: str = CATALOG_NAME,
) -> None:
    destination = source_root / catalog_name
    try:
        current = destination.read_bytes()
    except FileNotFoundError as exception:
        raise RuntimeError(
            "generated Catalog disappeared before combined rollback"
        ) from exception
    if current != published:
        raise RuntimeError(
            "generated Catalog changed before combined rollback; preserving current bytes"
        )
    if original is None:
        destination.unlink()
        return
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{catalog_name}.restore.",
        suffix=".tmp",
        dir=source_root,
    )
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(original)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary_name, destination)
    except BaseException:
        try:
            os.unlink(temporary_name)
        except FileNotFoundError:
            pass
        raise


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument(
        "--catalog-schema-version",
        type=int,
        choices=(1, 2),
        default=1,
        help="Catalog schema to publish; default 1 preserves the existing v1 output.",
    )
    parser.add_argument(
        "--notification-policy",
        action="append",
        default=[],
        metavar="VERSION=manual-only|notify",
        help="Required exactly once per aggregate version for Catalog v2.",
    )
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
    parser.add_argument(
        "--manifest-copy",
        action="append",
        default=[],
        metavar="VERSION=PATH",
        help="Copy one catalog-bound inner manifest to one direct source-root child.",
    )
    parser.add_argument("--registry-template", type=Path)
    parser.add_argument("--registry-output", type=Path)
    parser.add_argument("--registry-revision", type=int)
    parser.add_argument("--registry-published-at")
    args = parser.parse_args()
    published = _parse_assignments(args.published_at, "--published-at")
    note_paths = _parse_assignments(args.release_notes_file, "--release-notes-file")
    notification_policies = _parse_assignments(
        args.notification_policy,
        "--notification-policy",
    )
    if len(args.manifest_copy) > 1:
        raise ValueError("--manifest-copy may be supplied at most once")
    manifest_copies = _parse_assignments(args.manifest_copy, "--manifest-copy")
    for manifest_path in manifest_copies.values():
        _validate_manifest_copy_destination(args.source_root, Path(manifest_path))
    notes = {
        version: Path(path).read_text(encoding="utf-8")
        for version, path in note_paths.items()
    }
    registry_arguments = (
        args.registry_template,
        args.registry_output,
        args.registry_revision,
        args.registry_published_at,
    )
    if any(value is not None for value in registry_arguments):
        if any(value is None for value in registry_arguments):
            raise ValueError("all Registry rendering options must be supplied together")
        _preflight_registry_render(
            args.registry_template,
            args.registry_output,
            args.registry_revision,
            args.registry_published_at,
        )
    source_root_argument = args.source_root.absolute()
    _reject_link(source_root_argument, "update source root")
    source_root = source_root_argument.resolve(strict=True)
    combined_handoff = bool(
        manifest_copies or any(value is not None for value in registry_arguments)
    )
    if not combined_handoff:
        destination = build_catalog(
            source_root,
            published,
            notes,
            args.catalog_schema_version,
            notification_policies,
        )
        print(destination)
        return 0

    with _publication_lock(source_root):
        catalog_name = _catalog_name(args.catalog_schema_version)
        catalog_path = source_root / catalog_name
        original_catalog = (
            catalog_path.read_bytes() if catalog_path.is_file() else None
        )
        created_manifest_copies: dict[Path, bytes] = {}
        published_catalog: bytes | None = None
        try:
            destination, prepared_catalog = _prepare_catalog_unlocked(
                source_root,
                published,
                notes,
                args.catalog_schema_version,
                notification_policies,
            )
            prepared_manifest_copies: dict[Path, bytes] = {}
            for version, manifest_path in manifest_copies.items():
                prepared_manifest, manifest_bytes = _prepare_release_manifest(
                    source_root,
                    version,
                    Path(manifest_path),
                    args.catalog_schema_version,
                    prepared_catalog,
                )
                prepared_manifest_copies[prepared_manifest] = manifest_bytes
            prepared_registry: bytes | None = None
            if all(value is not None for value in registry_arguments):
                prepared_registry = _prepare_registry_render(
                    args.registry_template,
                    prepared_catalog,
                    args.registry_output,
                    args.registry_revision,
                    args.registry_published_at,
                )

            _ = _publish_catalog_unlocked(destination, prepared_catalog)
            published_catalog = prepared_catalog
            for prepared_manifest, manifest_bytes in prepared_manifest_copies.items():
                published_manifest, published_manifest_bytes = (
                    _publish_prepared_release_manifest(
                        prepared_manifest,
                        manifest_bytes,
                    )
                )
                created_manifest_copies[published_manifest] = published_manifest_bytes
            if all(value is not None for value in registry_arguments):
                _ = _publish_prepared_registry(
                    args.registry_output,
                    prepared_registry,
                )
        except BaseException as failure:
            rollback_failures: list[str] = []
            for manifest_path, manifest_bytes in created_manifest_copies.items():
                try:
                    _remove_owned_publication(
                        manifest_path,
                        manifest_bytes,
                        "published release manifest",
                    )
                except BaseException as rollback_failure:
                    rollback_failures.append(str(rollback_failure))
            if published_catalog is not None:
                try:
                    _restore_catalog_after_combined_failure(
                        source_root,
                        original_catalog,
                        published_catalog,
                        catalog_name,
                    )
                except BaseException as rollback_failure:
                    rollback_failures.append(str(rollback_failure))
            if rollback_failures:
                raise RuntimeError(
                    "combined update-source rollback failed: "
                    + "; ".join(rollback_failures)
                ) from failure
            raise
    print(destination)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

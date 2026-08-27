#!/usr/bin/env python3
"""Canonical publisher-side wire policy for update-source Registry JSON."""

from __future__ import annotations

import datetime
import json
import ntpath
import re


MAXIMUM_REGISTRY_BYTES = 64 * 1024
MAXIMUM_ENTRIES = 16
MAXIMUM_REVISION = 9_223_372_036_854_775_807
PRODUCTION_REGISTRY_ID = "nvt-fw-combiner-production"
ROOT_KEYS = {
    "schemaVersion",
    "registryId",
    "registryRevision",
    "publishedAtUtc",
    "catalogPublication",
    "entries",
}
CATALOG_PUBLICATION_KEYS = {
    "latestVersion",
    "catalogSchemaVersion",
    "catalogSha256",
}
ENTRY_KEYS = {"status", "catalogPath"}
STATUSES = {"latest", "available", "deprecated"}
SEMVER_PATTERN = re.compile(r"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$")
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
CANONICAL_UTC_PATTERN = re.compile(
    r"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}"
    r"(?:\.[0-9]{1,7})?Z$"
)


def _strict_object(pairs: list[tuple[str, object]]) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"JSON object repeats property {key}")
        result[key] = value
    return result


def load_strict_registry_json(raw: bytes) -> object:
    if not 1 <= len(raw) <= MAXIMUM_REGISTRY_BYTES:
        raise ValueError("registry must contain 1 through 65,536 UTF-8 bytes")
    try:
        return json.loads(
            raw.decode("utf-8"),
            object_pairs_hook=_strict_object,
            parse_constant=lambda value: (_ for _ in ()).throw(
                ValueError(f"non-finite JSON value {value}")
            ),
        )
    except (UnicodeDecodeError, json.JSONDecodeError, ValueError) as exception:
        raise ValueError(
            f"registry is not strict UTF-8 JSON: {exception}"
        ) from exception


def validate_canonical_utc(value: object, label: str) -> str:
    if not isinstance(value, str) or not CANONICAL_UTC_PATTERN.fullmatch(value):
        raise ValueError(f"{label} is not canonical UTC: {value}")
    try:
        datetime.datetime.strptime(value[:19], "%Y-%m-%dT%H:%M:%S")
    except ValueError as exception:
        raise ValueError(f"{label} is not canonical UTC: {value}") from exception
    return value


def validate_registry_revision(value: object, label: str = "Registry revision") -> int:
    if (
        isinstance(value, bool)
        or not isinstance(value, int)
        or not 1 <= value <= MAXIMUM_REVISION
    ):
        raise ValueError(f"{label} must be a positive Int64 integer")
    return value


def normalize_windows_catalog_path(value: object) -> str:
    if not isinstance(value, str) or not 3 <= len(value) <= 1024:
        raise ValueError(
            "Registry catalogPath must be a 3 through 1,024 character string"
        )
    if value.startswith(("\\\\?\\", "\\\\.\\")) or "/" in value:
        raise ValueError(
            f"Registry catalogPath is not an ordinary Windows path: {value}"
        )
    drive, tail = ntpath.splitdrive(value)
    drive_qualified = len(drive) == 2 and drive[1] == ":" and tail.startswith("\\")
    unc_parts = tuple(part for part in drive[2:].split("\\") if part)
    unc_qualified = (
        drive.startswith("\\\\")
        and len(unc_parts) == 2
        and (not tail or tail.startswith("\\"))
    )
    if not (drive_qualified or unc_qualified):
        raise ValueError(f"Registry catalogPath is not fully qualified: {value}")
    root_length = 3 if drive_qualified else len(drive)
    if ":" in value[root_length:]:
        raise ValueError(
            f"Registry catalogPath contains an alternate data stream: {value}"
        )
    normalized = ntpath.normpath(value)
    if normalized.endswith("\\") and not (drive_qualified and len(normalized) == 3):
        normalized = normalized[:-1]
    if ntpath.normcase(normalized) != ntpath.normcase(value):
        raise ValueError(f"Registry catalogPath must already be normalized: {value}")
    if not ntpath.basename(normalized):
        raise ValueError(f"registry catalogPath must name a file: {value}")
    return normalized


def validate_registry_entries(
    entries: object,
    label: str = "registry",
) -> tuple[dict[str, str], ...]:
    if not isinstance(entries, list) or not 1 <= len(entries) <= MAXIMUM_ENTRIES:
        raise ValueError(f"{label} Registry must contain 1 through 16 entries")
    projected: list[dict[str, str]] = []
    unique_paths: set[str] = set()
    unique_roots: set[str] = set()
    latest_count = 0
    for entry in entries:
        if not isinstance(entry, dict) or set(entry) != ENTRY_KEYS:
            raise ValueError(f"{label} Registry entry shape is invalid")
        status = entry.get("status")
        if status not in STATUSES:
            raise ValueError(f"{label} Registry entry status is invalid: {status}")
        path = normalize_windows_catalog_path(entry.get("catalogPath"))
        identity = ntpath.normcase(path)
        root_identity = ntpath.normcase(ntpath.dirname(path))
        if identity in unique_paths or root_identity in unique_roots:
            raise ValueError(
                f"{label} Registry catalogPath or source root is unsafe or duplicated"
            )
        unique_paths.add(identity)
        unique_roots.add(root_identity)
        latest_count += status == "latest"
        projected.append({"status": status, "catalogPath": path})
    if latest_count != 1:
        raise ValueError(f"{label} Registry must contain exactly one latest entry")
    return tuple(projected)


def validate_registry_document(
    document: object,
    label: str = "registry",
) -> tuple[int, tuple[dict[str, str], ...]]:
    if not isinstance(document, dict) or set(document) != ROOT_KEYS:
        raise ValueError(f"{label} Registry root shape is invalid")
    schema_version = document.get("schemaVersion")
    if isinstance(schema_version, bool) or schema_version != 1:
        raise ValueError(f"{label} Registry schemaVersion must be 1")
    if document.get("registryId") != PRODUCTION_REGISTRY_ID:
        raise ValueError(f"{label} Registry registryId is invalid")
    revision = validate_registry_revision(
        document.get("registryRevision"),
        f"{label} Registry registryRevision",
    )
    validate_canonical_utc(
        document.get("publishedAtUtc"), f"{label} Registry publishedAtUtc"
    )
    publication = document.get("catalogPublication")
    if (
        not isinstance(publication, dict)
        or set(publication) != CATALOG_PUBLICATION_KEYS
    ):
        raise ValueError(f"{label} Registry catalogPublication shape is invalid")
    latest_version = publication.get("latestVersion")
    if not isinstance(latest_version, str) or not SEMVER_PATTERN.fullmatch(
        latest_version
    ):
        raise ValueError(
            f"{label} Registry catalogPublication latestVersion is invalid"
        )
    catalog_schema = publication.get("catalogSchemaVersion")
    if (
        isinstance(catalog_schema, bool)
        or not isinstance(catalog_schema, int)
        or catalog_schema < 1
    ):
        raise ValueError(
            f"{label} Registry catalogPublication catalogSchemaVersion is invalid"
        )
    catalog_sha256 = publication.get("catalogSha256")
    if not isinstance(catalog_sha256, str) or not SHA256_PATTERN.fullmatch(
        catalog_sha256
    ):
        raise ValueError(
            f"{label} Registry catalogPublication catalogSha256 is invalid"
        )
    return revision, validate_registry_entries(document.get("entries"), label)


def validate_registry_template(template: object) -> dict[str, object]:
    if (
        not isinstance(template, dict)
        or set(template) != ROOT_KEYS
        or isinstance(template.get("schemaVersion"), bool)
        or template.get("schemaVersion") != 1
        or isinstance(template.get("registryRevision"), bool)
        or template.get("registryRevision") != 0
        or template.get("publishedAtUtc") != "__PUBLISHED_AT_UTC__"
        or template.get("registryId") != PRODUCTION_REGISTRY_ID
    ):
        raise ValueError("Registry template shape, authority, or sentinels are invalid")
    publication = template.get("catalogPublication")
    if (
        not isinstance(publication, dict)
        or set(publication) != CATALOG_PUBLICATION_KEYS
        or publication.get("latestVersion") != "__LATEST_VERSION__"
        or isinstance(publication.get("catalogSchemaVersion"), bool)
        or publication.get("catalogSchemaVersion") != 1
        or publication.get("catalogSha256") != "__CATALOG_SHA256__"
    ):
        raise ValueError("Registry Catalog-publication template is invalid")
    validate_registry_entries(template.get("entries"), "template")
    return template

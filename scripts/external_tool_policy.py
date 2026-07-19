"""Closed repository and release inventories for external tool payloads."""

import hashlib
import json
import tomllib
from pathlib import Path, PurePosixPath


ALLOWED_EXTERNAL_TOOL_BINARY_PAYLOADS = {
    PurePosixPath("external-tools/diff-nf-merge/1.0.0/CommandLine.dll"),
    PurePosixPath("external-tools/diff-nf-merge/1.0.0/DiffNFMerge.exe"),
    PurePosixPath("external-tools/legacy-combiner/1.13.0/Combiner.exe"),
}
APPROVED_REPOSITORY_EXTERNAL_TOOL_PACKAGE_PATHS = {
    PurePosixPath("external-tools/README.md"),
    PurePosixPath("external-tools/legacy-combiner/README.md"),
    PurePosixPath("external-tools/legacy-combiner/1.13.0/Combiner.exe"),
    PurePosixPath("external-tools/legacy-combiner/1.13.0/manifest.json"),
}
APPROVED_EXTERNAL_TOOL_PACKAGE_PATHS = (
    APPROVED_REPOSITORY_EXTERNAL_TOOL_PACKAGE_PATHS
    | {
        PurePosixPath("external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe"),
    }
)
APPROVED_EXTERNAL_TOOL_REPOSITORY_PATHS = (
    APPROVED_REPOSITORY_EXTERNAL_TOOL_PACKAGE_PATHS
    | {
        PurePosixPath("external-tools/catalog.json"),
        PurePosixPath("external-tools/diff-nf-merge/README.md"),
        PurePosixPath("external-tools/diff-nf-merge/1.0.0/CommandLine.dll"),
        PurePosixPath("external-tools/diff-nf-merge/1.0.0/DiffNFMerge.exe"),
        PurePosixPath("external-tools/diff-nf-merge/1.0.0/DiffNFMerge.exe.config"),
        PurePosixPath(
            "external-tools/diff-nf-merge/1.0.0/LICENSE.CommandLineParser.md"
        ),
        PurePosixPath("external-tools/diff-nf-merge/1.0.0/package-manifest.json"),
    }
)

EXTERNAL_TOOL_CATALOG_PATH = PurePosixPath("external-tools/catalog.json")


def _read_json_object(path: Path, label: str, errors: list[str]) -> dict | None:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        errors.append(f"invalid {label} {path}: {error}")
        return None
    if not isinstance(value, dict):
        errors.append(f"{label} must be an object: {path}")
        return None
    return value


def _catalog_paths(
    values: object, field: str, errors: list[str]
) -> set[PurePosixPath] | None:
    if not isinstance(values, list) or not all(
        isinstance(value, str) for value in values
    ):
        errors.append(f"external-tool catalog {field} must be an array of paths")
        return None
    paths = {PurePosixPath(value) for value in values}
    if len(paths) != len(values):
        errors.append(f"external-tool catalog {field} contains duplicate paths")
    for path in paths:
        if path.is_absolute() or ".." in path.parts:
            errors.append(
                f"external-tool catalog {field} path escapes the repository: {path}"
            )
    return paths


def _require_fields(
    label: str, actual: dict, expected: dict[str, object], errors: list[str]
) -> None:
    for field, expected_value in expected.items():
        if actual.get(field) != expected_value:
            errors.append(
                f"{label} {field} mismatch: "
                f"expected {expected_value!r}, actual {actual.get(field)!r}"
            )


def _require_tool_fields(
    tool: dict, expected: dict[str, object], errors: list[str]
) -> None:
    _require_fields(f"external-tool catalog {expected['logicalId']}", tool, expected, errors)


def validate_external_tool_catalog(
    root: Path,
    repository_paths: set[PurePosixPath],
    package_paths: set[PurePosixPath],
    errors: list[str],
) -> None:
    """Verify repository identity, runtime status, and release deployment remain aligned."""
    catalog = _read_json_object(
        root / EXTERNAL_TOOL_CATALOG_PATH, "external-tool catalog", errors
    )
    if catalog is None:
        return
    if catalog.get("schemaVersion") != "1.0":
        errors.append("external-tool catalog schemaVersion must be 1.0")

    repository_entries = catalog.get("repositoryFiles")
    declared_repository_paths: set[PurePosixPath] = set()
    repository_hashes: dict[PurePosixPath, str] = {}
    if not isinstance(repository_entries, list):
        errors.append("external-tool catalog repositoryFiles must be an array")
    else:
        for entry in repository_entries:
            if not isinstance(entry, dict) or not isinstance(entry.get("path"), str):
                errors.append(
                    "external-tool catalog has an invalid repositoryFiles entry"
                )
                continue
            path = PurePosixPath(entry["path"])
            if (
                path.is_absolute()
                or ".." in path.parts
                or path == EXTERNAL_TOOL_CATALOG_PATH
            ):
                errors.append(
                    f"external-tool catalog has an invalid repository path: {path}"
                )
                continue
            if path in declared_repository_paths:
                errors.append(f"external-tool catalog repeats repository path: {path}")
                continue
            declared_repository_paths.add(path)
            try:
                payload = (root / path).read_bytes()
            except OSError as error:
                errors.append(
                    f"cannot read catalog-pinned external-tool file {path}: {error}"
                )
                continue
            if entry.get("size") != len(payload):
                errors.append(
                    f"external-tool catalog size mismatch for {path}: "
                    f"expected {entry.get('size')}, actual {len(payload)}"
                )
            actual_sha256 = hashlib.sha256(payload).hexdigest()
            repository_hashes[path] = actual_sha256
            if entry.get("sha256") != actual_sha256:
                errors.append(
                    f"external-tool catalog SHA-256 mismatch for {path}: "
                    f"expected {entry.get('sha256')}, actual {actual_sha256}"
                )

    expected_repository_paths = repository_paths - {EXTERNAL_TOOL_CATALOG_PATH}
    if declared_repository_paths != expected_repository_paths:
        errors.append(
            "external-tool catalog repository inventory mismatch: "
            f"expected {', '.join(str(path) for path in sorted(expected_repository_paths))}; "
            f"declared {', '.join(str(path) for path in sorted(declared_repository_paths))}"
        )

    declared_package_paths = _catalog_paths(
        catalog.get("releasePackagePaths"), "releasePackagePaths", errors
    )
    if declared_package_paths is not None:
        if declared_package_paths != package_paths:
            errors.append(
                "external-tool catalog release package inventory mismatch: "
                f"expected {', '.join(str(path) for path in sorted(package_paths))}; "
                f"declared {', '.join(str(path) for path in sorted(declared_package_paths))}"
            )
        for path in declared_package_paths:
            if path.suffix.lower() in {".exe", ".dll"} and len(path.parts) < 3:
                errors.append(
                    f"external-tool executable cannot be packaged at the release root: {path}"
                )

    tool_entries = catalog.get("tools")
    tools: dict[str, dict] = {}
    if not isinstance(tool_entries, list):
        errors.append("external-tool catalog tools must be an array")
    else:
        for entry in tool_entries:
            if not isinstance(entry, dict) or not isinstance(
                entry.get("logicalId"), str
            ):
                errors.append("external-tool catalog has an invalid tool entry")
                continue
            logical_id = entry["logicalId"]
            if logical_id in tools:
                errors.append(f"external-tool catalog repeats logicalId: {logical_id}")
            tools[logical_id] = entry
    if set(tools) != {"legacy-combiner", "diff-nf-merge", "crc-worker"}:
        errors.append(
            "external-tool catalog logical inventory must be "
            "crc-worker, diff-nf-merge, legacy-combiner"
        )
        return

    legacy_manifest_path = PurePosixPath(
        "external-tools/legacy-combiner/1.13.0/manifest.json"
    )
    legacy_manifest = _read_json_object(
        root / legacy_manifest_path, "legacy Combiner manifest", errors
    )
    if legacy_manifest is not None:
        legacy_executable_path = PurePosixPath(
            "external-tools/legacy-combiner/1.13.0/Combiner.exe"
        )
        _require_fields(
            "legacy Combiner manifest",
            legacy_manifest,
            {
                "schemaVersion": "1.0",
                "toolBindingId": "legacy-combiner-1.13.0",
                "toolId": "legacy-combiner",
                "toolVersion": "1.13.0",
                "platform": "win-x64",
                "executableName": "Combiner.exe",
                "sha256": "ed6b58289cc780f73d36b831f5424cef44ad93187ba7518d36df6a77ad0c76bf",
                "adapterId": "legacy-combiner-postbuild-v1",
                "inputMode": "in-place",
                "argumentTemplate": ["{staging.runDir}"],
                "workingDirectoryPolicy": "staging-directory",
                "timeoutSeconds": 30,
                "allowedExtraOutputFiles": [],
            },
            errors,
        )
        _require_tool_fields(
            tools["legacy-combiner"],
            {
                "logicalId": "legacy-combiner",
                "version": "1.13.0",
                "platform": "win-x64",
                "artifactOrigin": "repository",
                "runtimeStatus": "registered",
                "releaseStatus": "included",
                "manifestPath": str(legacy_manifest_path),
                "executablePath": str(legacy_executable_path),
                "executableSha256": "ed6b58289cc780f73d36b831f5424cef44ad93187ba7518d36df6a77ad0c76bf",
                "commandAuthority": "application-invocation-profile-catalog",
                "argvStatus": "registered-profile-specific",
                "workingDirectoryStatus": "host-created-staging-directory",
                "readAuthority": "host-created-staging-copy",
                "writeAuthority": "profile-declared-host-verified-ranges",
            },
            errors,
        )
        actual_sha256 = repository_hashes.get(legacy_executable_path)
        for authority, declared_sha256 in (
            ("manifest", legacy_manifest.get("sha256")),
            ("catalog tool", tools["legacy-combiner"].get("executableSha256")),
        ):
            if actual_sha256 is not None and declared_sha256 != actual_sha256:
                errors.append(
                    f"legacy Combiner {authority} SHA-256 does not match actual payload: "
                    f"expected {actual_sha256}, actual {declared_sha256}"
                )

    diff_manifest_path = PurePosixPath(
        "external-tools/diff-nf-merge/1.0.0/package-manifest.json"
    )
    diff_manifest = _read_json_object(
        root / diff_manifest_path, "DiffNFMerge package manifest", errors
    )
    if diff_manifest is not None:
        diff_executable_path = PurePosixPath(
            "external-tools/diff-nf-merge/1.0.0/DiffNFMerge.exe"
        )
        _require_fields(
            "DiffNFMerge package manifest",
            diff_manifest,
            {
                "schemaVersion": "1.0",
                "packageId": "diff-nf-merge",
                "toolVersion": "1.0.0.0",
                "productVersion": "1.0.0+90ac4292c65f94fd123098f8174f3e2cf68d0d41",
                "platform": "windows",
                "requiredRuntime": ".NET Framework 4.6",
                "runtimeIntegrationStatus": "not-registered",
                "releasePackageStatus": "excluded",
                "ownerReviewRequired": True,
                "inputContractStatus": "unverified; deferred to the v0.12.x integration",
            },
            errors,
        )
        _require_tool_fields(
            tools["diff-nf-merge"],
            {
                "logicalId": "diff-nf-merge",
                "version": "1.0.0.0",
                "platform": "windows",
                "artifactOrigin": "repository",
                "runtimeStatus": "not-registered",
                "releaseStatus": "excluded",
                "manifestPath": str(diff_manifest_path),
                "executablePath": str(diff_executable_path),
                "executableSha256": "f611af7e315d46341e15cd7140eb3962f6ac05d337121e5554022ef5e69a2bbe",
                "commandAuthority": "unverified",
                "argvStatus": "unverified",
                "workingDirectoryStatus": "unverified",
                "readAuthority": "unverified",
                "writeAuthority": "unverified",
                "inputContractStatus": "deferred-v0.12.x",
            },
            errors,
        )
        diff_executable = next(
            (
                entry
                for entry in diff_manifest.get("files", [])
                if isinstance(entry, dict) and entry.get("role") == "executable"
            ),
            {},
        )
        actual_sha256 = repository_hashes.get(diff_executable_path)
        for authority, declared_sha256 in (
            ("package manifest", diff_executable.get("sha256")),
            ("catalog tool", tools["diff-nf-merge"].get("executableSha256")),
        ):
            if actual_sha256 is not None and declared_sha256 != actual_sha256:
                errors.append(
                    f"DiffNFMerge {authority} SHA-256 does not match actual payload: "
                    f"expected {actual_sha256}, actual {declared_sha256}"
                )

    crc_worker_path = PurePosixPath("external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe")
    _require_tool_fields(
        tools["crc-worker"],
        {
            "logicalId": "crc-worker",
            "version": "0.1.0",
            "platform": "win-x64",
            "artifactOrigin": "generated",
            "runtimeStatus": "package-contract-only",
            "releaseStatus": "included",
            "sourcePath": "tools/crc-worker",
            "packagePath": str(crc_worker_path),
            "protocolVersions": ["1.0"],
            "argvStatus": "not-applicable-json-stdio",
            "workingDirectoryStatus": "not-authoritative",
            "readAuthority": "stdin-json-payload-max-4-mib",
            "writeAuthority": "stdout-json-only-no-filesystem-mutation",
            "transformStatus": "not-routed",
        },
        errors,
    )
    if crc_worker_path not in package_paths:
        errors.append(
            "CRC Worker generated package path is absent from the release allowlist"
        )
    if any(
        path.parts[:2] == ("external-tools", "crc-worker") for path in repository_paths
    ):
        errors.append(
            "CRC Worker executable must be generated, not stored in the repository"
        )
    crc_worker_source = root / "tools/crc-worker"
    if not crc_worker_source.is_dir():
        errors.append("CRC Worker catalog sourcePath does not exist")
    else:
        try:
            pyproject = tomllib.loads(
                (crc_worker_source / "pyproject.toml").read_text(encoding="utf-8")
            )
        except (OSError, tomllib.TOMLDecodeError) as error:
            errors.append(f"invalid CRC Worker pyproject.toml: {error}")
        else:
            project = pyproject.get("project")
            if not isinstance(project, dict):
                errors.append("CRC Worker pyproject.toml has no project table")
            else:
                _require_fields(
                    "CRC Worker source project",
                    project,
                    {"name": "nfc-crc-worker", "version": "0.1.0"},
                    errors,
                )
        for relative_path, required_literals in {
            "src/nfc_crc_worker/__init__.py": ('__version__ = "0.1.0"',),
            "src/nfc_crc_worker/protocol.py": (
                'PROTOCOL_VERSION = "1.0"',
                'OPERATION_CALCULATE = "calculate"',
                "MAX_DECODED_PAYLOAD_BYTES = 4 * 1024 * 1024",
            ),
        }.items():
            try:
                source = (crc_worker_source / relative_path).read_text(encoding="utf-8")
            except OSError as error:
                errors.append(f"cannot read CRC Worker source contract {relative_path}: {error}")
                continue
            for literal in required_literals:
                if literal not in source:
                    errors.append(
                        f"CRC Worker source contract {relative_path} is missing {literal!r}"
                    )
    if any(
        path.parts[:2] == ("external-tools", "diff-nf-merge") for path in package_paths
    ):
        errors.append(
            "DiffNFMerge is repository-only and cannot enter the release allowlist"
        )


def validate_repository_external_tool_manifests(
    root: Path, repository_paths: set[PurePosixPath], errors: list[str]
) -> None:
    """Verify every repository-only external-tool package against its pinned manifest."""
    manifest_paths = sorted(
        path for path in repository_paths if path.name == "package-manifest.json"
    )
    for manifest_path in manifest_paths:
        try:
            manifest = json.loads((root / manifest_path).read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as error:
            errors.append(
                f"invalid external-tool package manifest {manifest_path}: {error}"
            )
            continue

        if not isinstance(manifest, dict):
            errors.append(
                f"external-tool package manifest must be an object: {manifest_path}"
            )
            continue
        entries = manifest.get("files")
        if not isinstance(entries, list):
            errors.append(
                f"external-tool package manifest has no files array: {manifest_path}"
            )
            continue

        declared_paths: set[PurePosixPath] = set()
        for entry in entries:
            if not isinstance(entry, dict) or not isinstance(entry.get("path"), str):
                errors.append(
                    f"external-tool package manifest has an invalid file entry: {manifest_path}"
                )
                continue
            relative_path = PurePosixPath(entry["path"])
            if relative_path.is_absolute() or ".." in relative_path.parts:
                errors.append(
                    f"external-tool package manifest path escapes its package: {manifest_path}: {relative_path}"
                )
                continue

            repository_path = manifest_path.parent / relative_path
            declared_paths.add(repository_path)
            payload_path = root / repository_path
            try:
                payload = payload_path.read_bytes()
            except OSError as error:
                errors.append(
                    f"cannot read external-tool payload {repository_path}: {error}"
                )
                continue

            expected_size = entry.get("size")
            expected_sha256 = entry.get("sha256")
            if expected_size != len(payload):
                errors.append(
                    f"external-tool payload size mismatch for {repository_path}: "
                    f"expected {expected_size}, actual {len(payload)}"
                )
            actual_sha256 = hashlib.sha256(payload).hexdigest()
            if expected_sha256 != actual_sha256:
                errors.append(
                    f"external-tool payload SHA-256 mismatch for {repository_path}: "
                    f"expected {expected_sha256}, actual {actual_sha256}"
                )

        expected_paths = {
            path
            for path in repository_paths
            if path.parent == manifest_path.parent and path != manifest_path
        }
        if declared_paths != expected_paths:
            errors.append(
                f"external-tool package manifest inventory mismatch for {manifest_path}: "
                f"expected {', '.join(str(path) for path in sorted(expected_paths))}; "
                f"declared {', '.join(str(path) for path in sorted(declared_paths))}"
            )

"""Closed repository and release inventories for external tool payloads."""

import hashlib
import json
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
APPROVED_EXTERNAL_TOOL_PACKAGE_PATHS = APPROVED_REPOSITORY_EXTERNAL_TOOL_PACKAGE_PATHS | {
    PurePosixPath("external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe"),
}
APPROVED_EXTERNAL_TOOL_REPOSITORY_PATHS = APPROVED_REPOSITORY_EXTERNAL_TOOL_PACKAGE_PATHS | {
    PurePosixPath("external-tools/diff-nf-merge/README.md"),
    PurePosixPath("external-tools/diff-nf-merge/1.0.0/CommandLine.dll"),
    PurePosixPath("external-tools/diff-nf-merge/1.0.0/DiffNFMerge.exe"),
    PurePosixPath("external-tools/diff-nf-merge/1.0.0/DiffNFMerge.exe.config"),
    PurePosixPath("external-tools/diff-nf-merge/1.0.0/LICENSE.CommandLineParser.md"),
    PurePosixPath("external-tools/diff-nf-merge/1.0.0/package-manifest.json"),
}


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
            errors.append(f"invalid external-tool package manifest {manifest_path}: {error}")
            continue

        if not isinstance(manifest, dict):
            errors.append(f"external-tool package manifest must be an object: {manifest_path}")
            continue
        entries = manifest.get("files")
        if not isinstance(entries, list):
            errors.append(f"external-tool package manifest has no files array: {manifest_path}")
            continue

        declared_paths: set[PurePosixPath] = set()
        for entry in entries:
            if not isinstance(entry, dict) or not isinstance(entry.get("path"), str):
                errors.append(f"external-tool package manifest has an invalid file entry: {manifest_path}")
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
                errors.append(f"cannot read external-tool payload {repository_path}: {error}")
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

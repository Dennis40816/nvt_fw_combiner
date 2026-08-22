#!/usr/bin/env python3
"""Create a relocatable managed-root folder from a verified synthetic update source."""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import tempfile
import zipfile
from pathlib import Path, PurePosixPath


CATALOG_NAME = "update-catalog.v1.json"
SEED_NAME = "version-manager.seed.v1.json"
ADMISSION_NAME = ".managed-admission.v1.json"
LAUNCHER_NAME = "NvtFwCombiner.Launcher.exe"


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _json_bytes(value: object) -> bytes:
    return json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8")


def _safe_relative(path: str) -> bool:
    pure = PurePosixPath(path)
    return (
        bool(path)
        and not pure.is_absolute()
        and "\\" not in path
        and ":" not in path
        and all(part not in {"", ".", ".."} for part in pure.parts)
    )


def _catalog_entry(source_root: Path, seed_version: str) -> dict[str, object]:
    catalog = json.loads((source_root / CATALOG_NAME).read_text(encoding="utf-8"))
    if (
        catalog.get("schemaVersion") != 1
        or catalog.get("product") != "NVT FW Combiner"
        or catalog.get("runtimeIdentifier") != "win-x64"
    ):
        raise ValueError("update catalog identity is invalid")
    matches = [entry for entry in catalog.get("versions", []) if entry.get("version") == seed_version]
    if len(matches) != 1:
        raise ValueError(f"catalog must contain exactly one seed version {seed_version}")
    return matches[0]


def _extract_seed(source_root: Path, staging: Path, entry: dict[str, object]) -> tuple[str, str]:
    version = str(entry["version"])
    package_relative = str(entry["packagePath"])
    if not _safe_relative(package_relative):
        raise ValueError("catalog package path is unsafe")
    package = source_root / Path(*PurePosixPath(package_relative).parts)
    package_bytes = package.read_bytes()
    if len(package_bytes) != int(entry["packageSize"]) or _sha256(package_bytes) != entry["packageSha256"]:
        raise ValueError("seed package differs from catalog admission")

    prefix = f"NvtFwCombiner-v{version}-win-x64/"
    version_root = staging / "versions" / version
    version_root.mkdir(parents=True)
    manifest: bytes | None = None
    with zipfile.ZipFile(package) as archive:
        seen: set[str] = set()
        for info in archive.infolist():
            if info.is_dir():
                continue
            if not info.filename.startswith(prefix):
                raise ValueError("seed archive has an unexpected root")
            relative = info.filename[len(prefix) :]
            if not _safe_relative(relative) or relative.casefold() in seen:
                raise ValueError("seed archive contains an unsafe or duplicate path")
            seen.add(relative.casefold())
            data = archive.read(info)
            target = version_root.joinpath(*PurePosixPath(relative).parts)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
            if relative == "RELEASE-MANIFEST.json":
                manifest = data
    if manifest is None or not (version_root / "NvtFwCombiner.exe").is_file():
        raise ValueError("seed payload is incomplete")
    manifest_hash = _sha256(manifest)
    if manifest_hash != entry["releaseManifestSha256"]:
        raise ValueError("seed release manifest differs from catalog admission")

    identity = "|".join(
        (
            version,
            package_relative,
            str(entry["packageSize"]),
            str(entry["packageSha256"]),
            manifest_hash,
        )
    )
    admission = {
        "version": version,
        "admissionIdentity": identity,
        "releaseManifestSha256": manifest_hash,
    }
    (version_root / ADMISSION_NAME).write_bytes(_json_bytes(admission))
    return identity, manifest_hash


def build_managed_root(
    output_root: Path,
    source_root: Path,
    launcher_path: Path,
    seed_version: str = "0.10.5",
) -> None:
    """Atomically creates one managed-root folder and never overwrites a destination."""
    output_root = output_root.resolve()
    source_root = source_root.resolve()
    launcher_path = launcher_path.resolve()
    if output_root.exists():
        raise FileExistsError(f"refusing to overwrite existing managed lab: {output_root}")
    if not launcher_path.is_file():
        raise FileNotFoundError(f"stable launcher has not been published: {launcher_path}")
    output_root.parent.mkdir(parents=True, exist_ok=True)
    staging = Path(tempfile.mkdtemp(prefix=f".{output_root.name}-", dir=output_root.parent))
    try:
        entry = _catalog_entry(source_root, seed_version)
        identity, manifest_hash = _extract_seed(source_root, staging, entry)
        shutil.copyfile(launcher_path, staging / LAUNCHER_NAME)
        seed = {
            "schemaVersion": 1,
            "updateSource": None,
            "activeVersion": seed_version,
            "lastKnownGoodVersion": seed_version,
            "admissions": [
                {
                    "version": seed_version,
                    "admissionIdentity": identity,
                    "releaseManifestSha256": manifest_hash,
                }
            ],
            "pendingActivation": None,
            "failedActivationVersion": None,
            "retentionReviewDue": False,
        }
        (staging / SEED_NAME).write_bytes(_json_bytes(seed))
        staging.rename(output_root)
    except BaseException:
        shutil.rmtree(staging, ignore_errors=True)
        raise


def main() -> int:
    repository = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=repository / "artifacts" / "version-update-source-lab")
    parser.add_argument("--launcher", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=repository / "artifacts" / "managed-installation-lab")
    parser.add_argument("--seed-version", default="0.10.5")
    args = parser.parse_args()
    build_managed_root(args.output, args.source, args.launcher, args.seed_version)
    print(args.output.resolve())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

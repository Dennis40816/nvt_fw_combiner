from __future__ import annotations

import hashlib
import importlib.util
import json
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "create_version_update_lab.py"
CATALOG_SCRIPT = ROOT / "scripts" / "create_update_catalog.py"


def _load_module():
    spec = importlib.util.spec_from_file_location("create_version_update_lab", SCRIPT)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def _load_catalog_module():
    spec = importlib.util.spec_from_file_location(
        "create_update_catalog", CATALOG_SCRIPT
    )
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def _build_update_source(tmp_path: Path, name: str = "NvtFwCombiner") -> Path:
    probe = tmp_path / "probe"
    probe.mkdir()
    (probe / "NvtFwCombiner.ReadyProbe.exe").write_bytes(b"MZ-probe")
    source = tmp_path / name
    _load_module().build_lab(source, probe)
    return source


def _run_catalog_cli(*arguments: object) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, str(CATALOG_SCRIPT), *(str(value) for value in arguments)],
        cwd=ROOT,
        capture_output=True,
        text=True,
        encoding="utf-8",
        check=False,
    )


def test_release_catalog_package_size_ceiling_is_version_scoped() -> None:
    catalog_builder = _load_catalog_module()

    assert catalog_builder._maximum_package_bytes("1.0.0") == 134_217_728
    assert catalog_builder._maximum_package_bytes("1.0.1") == 134_217_728
    assert catalog_builder._maximum_package_bytes("1.0.2") == 80_000_000
    assert catalog_builder._maximum_package_bytes("0.10.6") == 80_000_000


def test_lab_catalog_and_packages_remain_valid_after_folder_move(
    tmp_path: Path,
) -> None:
    probe = tmp_path / "probe"
    probe.mkdir()
    (probe / "NvtFwCombiner.ReadyProbe.exe").write_bytes(b"MZ-probe")
    (probe / "NvtFwCombiner.ReadyProbe.dll").write_bytes(b"probe-dll")
    source = tmp_path / "version-update-source-lab"
    relocated = tmp_path / "relocated" / "version-update-source-lab"
    module = _load_module()

    module.build_lab(source, probe)
    shutil.move(source, relocated)
    catalog = json.loads(
        (relocated / "update-catalog.v1.json").read_text(encoding="utf-8")
    )

    assert [entry["version"] for entry in catalog["versions"]] == ["0.10.5", "0.10.6"]
    for entry in catalog["versions"]:
        package = relocated / entry["packagePath"]
        package_bytes = package.read_bytes()
        assert len(package_bytes) == entry["packageSize"]
        assert hashlib.sha256(package_bytes).hexdigest() == entry["packageSha256"]
        with zipfile.ZipFile(package) as archive:
            root = f"NvtFwCombiner-v{entry['version']}-win-x64"
            manifest = archive.read(f"{root}/RELEASE-MANIFEST.json")
            manifest_document = json.loads(manifest)
            checksums = archive.read(f"{root}/SHA256SUMS.txt").decode("utf-8")
            assert (
                hashlib.sha256(manifest).hexdigest() == entry["releaseManifestSha256"]
            )
            assert f"{root}/NvtFwCombiner.exe" in archive.namelist()
            assert {file["role"] for file in manifest_document["files"]} >= {
                "application",
                "externalTool",
                "capabilityPolicy",
                "builtInProfile",
            }
            expected_checksums = {
                file["path"]: file["sha256"] for file in manifest_document["files"]
            }
            expected_checksums["RELEASE-MANIFEST.json"] = hashlib.sha256(
                manifest
            ).hexdigest()
            actual_checksums = {line[66:]: line[:64] for line in checksums.splitlines()}
            assert actual_checksums == expected_checksums


def test_lab_builder_refuses_to_overwrite_existing_destination(tmp_path: Path) -> None:
    probe = tmp_path / "probe"
    probe.mkdir()
    (probe / "NvtFwCombiner.ReadyProbe.exe").write_bytes(b"MZ-probe")
    source = tmp_path / "version-update-source-lab"
    module = _load_module()
    module.build_lab(source, probe)

    try:
        module.build_lab(source, probe)
    except FileExistsError:
        pass
    else:
        raise AssertionError("existing local lab must not be overwritten")


def test_release_catalog_is_rebuilt_from_two_exact_package_archives(
    tmp_path: Path,
) -> None:
    probe = tmp_path / "probe"
    probe.mkdir()
    (probe / "NvtFwCombiner.ReadyProbe.exe").write_bytes(b"MZ-probe")
    source = tmp_path / "NvtFwCombiner"
    lab = _load_module()
    lab.build_lab(source, probe)
    (source / "update-catalog.v1.json").unlink()
    catalog_builder = _load_catalog_module()

    catalog_builder.build_catalog(
        source,
        {
            "0.10.5": "2026-08-21T00:00:00Z",
            "0.10.6": "2026-08-24T00:00:00Z",
        },
        {
            "0.10.5": "Release 0.10.5",
            "0.10.6": "Release 0.10.6",
        },
    )

    catalog = json.loads(
        (source / "update-catalog.v1.json").read_text(encoding="utf-8")
    )
    assert [entry["version"] for entry in catalog["versions"]] == ["0.10.5", "0.10.6"]
    for entry in catalog["versions"]:
        package = source / entry["packagePath"]
        package_bytes = package.read_bytes()
        assert entry["packagePath"] == (
            f"packages/NvtFwCombiner-v{entry['version']}-win-x64.zip"
        )
        assert entry["packageSize"] == len(package_bytes)
        assert entry["packageSha256"] == hashlib.sha256(package_bytes).hexdigest()
        with zipfile.ZipFile(package) as archive:
            manifest = archive.read(
                f"NvtFwCombiner-v{entry['version']}-win-x64/RELEASE-MANIFEST.json"
            )
        assert entry["releaseManifestSha256"] == hashlib.sha256(manifest).hexdigest()


def test_release_manifest_copy_is_exact_catalog_bound_inner_manifest(
    tmp_path: Path,
) -> None:
    source = _build_update_source(tmp_path)
    catalog = json.loads(
        (source / "update-catalog.v1.json").read_text(encoding="utf-8")
    )
    selected = catalog["versions"][1]
    destination = source / "RELEASE-MANIFEST.json"

    _load_catalog_module().copy_release_manifest(
        source,
        selected["version"],
        destination,
    )

    copied = destination.read_bytes()
    package = source / selected["packagePath"]
    with zipfile.ZipFile(package) as archive:
        expected = archive.read(
            f"NvtFwCombiner-v{selected['version']}-win-x64/RELEASE-MANIFEST.json"
        )
    assert copied == expected
    assert hashlib.sha256(copied).hexdigest() == selected["releaseManifestSha256"]


def test_release_manifest_copy_refuses_existing_destination(tmp_path: Path) -> None:
    source = _build_update_source(tmp_path)
    destination = source / "RELEASE-MANIFEST.json"
    destination.write_bytes(b"operator-owned")

    with pytest.raises(FileExistsError, match="refusing to replace"):
        _load_catalog_module().copy_release_manifest(
            source,
            "0.10.6",
            destination,
        )

    assert destination.read_bytes() == b"operator-owned"


def test_release_manifest_copy_never_overwrites_concurrent_destination(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    source = _build_update_source(tmp_path)
    destination = source / "RELEASE-MANIFEST.json"
    catalog_builder = _load_catalog_module()
    real_open = catalog_builder.os.open

    def open_after_competitor(path: Path, flags: int, mode: int) -> int:
        competitor = real_open(
            destination,
            catalog_builder.os.O_WRONLY
            | catalog_builder.os.O_CREAT
            | catalog_builder.os.O_EXCL,
            0o600,
        )
        try:
            catalog_builder.os.write(competitor, b"competitor-owned")
        finally:
            catalog_builder.os.close(competitor)
        return real_open(path, flags, mode)

    monkeypatch.setattr(catalog_builder.os, "open", open_after_competitor)

    with pytest.raises(FileExistsError):
        catalog_builder.copy_release_manifest(source, "0.10.6", destination)

    assert destination.read_bytes() == b"competitor-owned"


def test_release_manifest_copy_rechecks_package_identity(tmp_path: Path) -> None:
    source = _build_update_source(tmp_path)
    catalog = json.loads(
        (source / "update-catalog.v1.json").read_text(encoding="utf-8")
    )
    selected = catalog["versions"][1]
    package = source / selected["packagePath"]
    package.write_bytes(package.read_bytes() + b"changed")

    with pytest.raises(ValueError, match="changed after catalog generation"):
        _load_catalog_module().copy_release_manifest(
            source,
            selected["version"],
            source / "RELEASE-MANIFEST.json",
        )

    assert not (source / "RELEASE-MANIFEST.json").exists()


def test_release_catalog_rejects_changed_stable_package_without_rewriting_catalog(
    tmp_path: Path,
) -> None:
    probe = tmp_path / "probe"
    probe.mkdir()
    (probe / "NvtFwCombiner.ReadyProbe.exe").write_bytes(b"MZ-probe")
    source = tmp_path / "NvtFwCombiner"
    lab = _load_module()
    lab.build_lab(source, probe)
    catalog_builder = _load_catalog_module()
    catalog_path = source / "update-catalog.v1.json"
    original_bytes = catalog_path.read_bytes()
    original = json.loads(original_bytes)
    package = source / original["versions"][1]["packagePath"]
    package.write_bytes(package.read_bytes() + b"changed")

    with pytest.raises(ValueError, match="stable package identity changed"):
        catalog_builder.build_catalog(source, {}, {})

    assert catalog_path.read_bytes() == original_bytes


def test_release_catalog_accepts_exact_existing_metadata_overrides(
    tmp_path: Path,
) -> None:
    source = _build_update_source(tmp_path)
    catalog_path = source / "update-catalog.v1.json"
    original = json.loads(catalog_path.read_text(encoding="utf-8"))
    metadata = {entry["version"]: entry for entry in original["versions"]}

    _load_catalog_module().build_catalog(
        source,
        {"0.10.5": metadata["0.10.5"]["publishedAt"]},
        {"0.10.6": metadata["0.10.6"]["releaseNotes"]},
    )

    rebuilt = json.loads(catalog_path.read_text(encoding="utf-8"))
    assert rebuilt == original


@pytest.mark.parametrize(
    ("published", "notes", "message"),
    [
        ({"0.10.5": "2026-08-22T00:00:00Z"}, {}, "publishedAt"),
        ({}, {"0.10.5": "Changed release notes"}, "releaseNotes"),
    ],
)
def test_release_catalog_rejects_existing_metadata_drift_without_rewriting_catalog(
    tmp_path: Path,
    published: dict[str, str],
    notes: dict[str, str],
    message: str,
) -> None:
    source = _build_update_source(tmp_path)
    catalog_path = source / "update-catalog.v1.json"
    original_bytes = catalog_path.read_bytes()

    with pytest.raises(ValueError, match=message):
        _load_catalog_module().build_catalog(source, published, notes)

    assert catalog_path.read_bytes() == original_bytes


def test_release_catalog_cli_builds_two_packages_with_spaced_utf8_note_paths(
    tmp_path: Path,
) -> None:
    source = _build_update_source(tmp_path, "update source files")
    (source / "update-catalog.v1.json").unlink()
    notes_root = tmp_path / "release notes files"
    notes_root.mkdir()
    notes = {
        "0.10.5": "Release 0.10.5 — 穩定版\n",
        "0.10.6": "Release 0.10.6 — 更新版\n第二行",
    }
    note_paths: dict[str, Path] = {}
    for version, content in notes.items():
        path = notes_root / f"version {version} notes.txt"
        path.write_text(content, encoding="utf-8")
        note_paths[version] = path

    result = _run_catalog_cli(
        "--source-root",
        source,
        "--published-at",
        "0.10.5=2026-08-21T00:00:00Z",
        "--published-at",
        "0.10.6=2026-08-24T00:00:00Z",
        "--release-notes-file",
        f"0.10.5={note_paths['0.10.5']}",
        "--release-notes-file",
        f"0.10.6={note_paths['0.10.6']}",
    )

    assert result.returncode == 0, result.stderr
    catalog_path = source / "update-catalog.v1.json"
    assert Path(result.stdout.strip()) == catalog_path
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    assert [entry["version"] for entry in catalog["versions"]] == ["0.10.5", "0.10.6"]
    assert {
        entry["version"]: entry["releaseNotes"] for entry in catalog["versions"]
    } == notes


def test_release_catalog_cli_missing_metadata_fails_without_creating_catalog(
    tmp_path: Path,
) -> None:
    source = _build_update_source(tmp_path)
    catalog_path = source / "update-catalog.v1.json"
    catalog_path.unlink()

    result = _run_catalog_cli("--source-root", source)

    assert result.returncode != 0
    assert "requires --published-at and --release-notes-file" in result.stderr
    assert not catalog_path.exists()


def test_release_catalog_cli_rejects_manifest_copy_before_catalog_write(
    tmp_path: Path,
) -> None:
    source = _build_update_source(tmp_path)
    catalog_path = source / "update-catalog.v1.json"
    catalog_path.unlink()

    result = _run_catalog_cli(
        "--source-root",
        source,
        "--manifest-copy",
        "malformed",
    )

    assert result.returncode != 0
    assert "--manifest-copy requires VERSION=VALUE" in result.stderr
    assert not catalog_path.exists()


def test_release_catalog_cli_rejects_multiple_manifest_copies_before_catalog_write(
    tmp_path: Path,
) -> None:
    source = _build_update_source(tmp_path)
    catalog_path = source / "update-catalog.v1.json"
    catalog_path.unlink()

    result = _run_catalog_cli(
        "--source-root",
        source,
        "--manifest-copy",
        f"0.10.5={source / 'RELEASE-MANIFEST.json'}",
        "--manifest-copy",
        f"0.10.6={source / 'RELEASE-MANIFEST.json'}",
    )

    assert result.returncode != 0
    assert "--manifest-copy may be supplied at most once" in result.stderr
    assert not catalog_path.exists()
    assert not (source / "RELEASE-MANIFEST.json").exists()


def test_release_catalog_cli_metadata_drift_fails_without_rewriting_catalog(
    tmp_path: Path,
) -> None:
    source = _build_update_source(tmp_path)
    catalog_path = source / "update-catalog.v1.json"
    original_bytes = catalog_path.read_bytes()
    notes_path = tmp_path / "changed release notes.txt"
    notes_path.write_text("Changed release notes", encoding="utf-8")

    result = _run_catalog_cli(
        "--source-root",
        source,
        "--release-notes-file",
        f"0.10.5={notes_path}",
    )

    assert result.returncode != 0
    assert "releaseNotes" in result.stderr
    assert catalog_path.read_bytes() == original_bytes


def test_release_catalog_cli_package_drift_fails_without_rewriting_catalog(
    tmp_path: Path,
) -> None:
    source = _build_update_source(tmp_path)
    catalog_path = source / "update-catalog.v1.json"
    original_bytes = catalog_path.read_bytes()
    original = json.loads(original_bytes)
    package = source / original["versions"][0]["packagePath"]
    package.write_bytes(package.read_bytes() + b"changed")

    result = _run_catalog_cli("--source-root", source)

    assert result.returncode != 0
    assert "stable package identity changed" in result.stderr
    assert catalog_path.read_bytes() == original_bytes


def test_release_catalog_rejects_calendar_invalid_publication_time(
    tmp_path: Path,
) -> None:
    probe = tmp_path / "probe"
    probe.mkdir()
    (probe / "NvtFwCombiner.ReadyProbe.exe").write_bytes(b"MZ-probe")
    source = tmp_path / "NvtFwCombiner"
    _load_module().build_lab(source, probe)
    (source / "update-catalog.v1.json").unlink()

    with pytest.raises(ValueError, match="canonical UTC"):
        _load_catalog_module().build_catalog(
            source,
            {
                "0.10.5": "2026-99-99T99:99:99Z",
                "0.10.6": "2026-08-24T00:00:00Z",
            },
            {"0.10.5": "Release 0.10.5", "0.10.6": "Release 0.10.6"},
        )


def test_release_catalog_refuses_oversized_existing_version_set(tmp_path: Path) -> None:
    source = tmp_path / "NvtFwCombiner"
    (source / "packages").mkdir(parents=True)
    entries = [
        {
            "version": f"1.0.{patch}",
            "publishedAt": "2026-08-24T00:00:00Z",
            "packagePath": f"packages/NvtFwCombiner-v1.0.{patch}-win-x64.zip",
            "packageSize": 1,
            "packageSha256": "0" * 64,
            "releaseManifestSha256": "0" * 64,
            "releaseNotes": "Release",
        }
        for patch in range(129)
    ]
    (source / "update-catalog.v1.json").write_text(
        json.dumps(
            {
                "schemaVersion": 1,
                "product": "NVT FW Combiner",
                "runtimeIdentifier": "win-x64",
                "versions": entries,
            }
        ),
        encoding="utf-8",
    )

    with pytest.raises(ValueError, match="1 through 128"):
        _load_catalog_module().build_catalog(source, {}, {})


def test_release_catalog_refuses_malformed_existing_metadata(tmp_path: Path) -> None:
    probe = tmp_path / "probe"
    probe.mkdir()
    (probe / "NvtFwCombiner.ReadyProbe.exe").write_bytes(b"MZ-probe")
    source = tmp_path / "NvtFwCombiner"
    _load_module().build_lab(source, probe)
    catalog_path = source / "update-catalog.v1.json"
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    catalog["versions"][0]["packagePath"] = "../outside.zip"
    catalog_path.write_text(json.dumps(catalog), encoding="utf-8")

    with pytest.raises(ValueError, match="packagePath.*not canonical"):
        _load_catalog_module().build_catalog(source, {}, {})


def test_release_catalog_refuses_duplicate_json_properties(tmp_path: Path) -> None:
    source = tmp_path / "NvtFwCombiner"
    (source / "packages").mkdir(parents=True)
    (source / "update-catalog.v1.json").write_text(
        '{"schemaVersion":1,"schemaVersion":1,"product":"NVT FW Combiner",'
        '"runtimeIdentifier":"win-x64","versions":[]}',
        encoding="utf-8",
    )

    with pytest.raises(ValueError, match="repeats property schemaVersion"):
        _load_catalog_module().build_catalog(source, {}, {})

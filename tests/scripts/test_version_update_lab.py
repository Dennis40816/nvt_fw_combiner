from __future__ import annotations

import hashlib
import importlib.util
import json
import shutil
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "create_version_update_lab.py"


def _load_module():
    spec = importlib.util.spec_from_file_location("create_version_update_lab", SCRIPT)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def test_lab_catalog_and_packages_remain_valid_after_folder_move(tmp_path: Path) -> None:
    probe = tmp_path / "probe"
    probe.mkdir()
    (probe / "NvtFwCombiner.ReadyProbe.exe").write_bytes(b"MZ-probe")
    (probe / "NvtFwCombiner.ReadyProbe.dll").write_bytes(b"probe-dll")
    source = tmp_path / "version-update-source-lab"
    relocated = tmp_path / "relocated" / "version-update-source-lab"
    module = _load_module()

    module.build_lab(source, probe)
    shutil.move(source, relocated)
    catalog = json.loads((relocated / "update-catalog.v1.json").read_text(encoding="utf-8"))

    assert [entry["version"] for entry in catalog["versions"]] == ["0.10.5", "0.10.6"]
    for entry in catalog["versions"]:
        package = relocated / entry["packagePath"]
        package_bytes = package.read_bytes()
        assert len(package_bytes) == entry["packageSize"]
        assert hashlib.sha256(package_bytes).hexdigest() == entry["packageSha256"]
        with zipfile.ZipFile(package) as archive:
            root = f"NvtFwCombiner-v{entry['version']}-win-x64"
            manifest = archive.read(f"{root}/RELEASE-MANIFEST.json")
            assert hashlib.sha256(manifest).hexdigest() == entry["releaseManifestSha256"]
            assert f"{root}/NvtFwCombiner.exe" in archive.namelist()


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

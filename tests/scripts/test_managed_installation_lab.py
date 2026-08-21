from __future__ import annotations

import importlib.util
import json
import shutil
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
UPDATE_SCRIPT = ROOT / "scripts" / "create_version_update_lab.py"
MANAGED_SCRIPT = ROOT / "scripts" / "create_managed_installation_lab.py"


def _load(path: Path, name: str):
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def test_managed_root_seed_is_content_bound_and_source_location_independent(tmp_path: Path) -> None:
    probe = tmp_path / "probe"
    probe.mkdir()
    (probe / "NvtFwCombiner.ReadyProbe.exe").write_bytes(b"MZ-probe")
    source = tmp_path / "source"
    relocated = tmp_path / "relocated" / "source"
    launcher = tmp_path / "launcher.exe"
    launcher.write_bytes(b"MZ-launcher")
    update = _load(UPDATE_SCRIPT, "create_update_lab")
    managed = _load(MANAGED_SCRIPT, "create_managed_lab")
    update.build_lab(source, probe)
    shutil.move(source, relocated)
    output = tmp_path / "managed-root"

    managed.build_managed_root(output, relocated, launcher)

    seed = json.loads((output / managed.SEED_NAME).read_text(encoding="utf-8"))
    admission = seed["admissions"][0]
    stored_admission = json.loads(
        (output / "versions" / "0.10.5" / managed.ADMISSION_NAME).read_text(encoding="utf-8")
    )
    assert seed["activeVersion"] == seed["lastKnownGoodVersion"] == "0.10.5"
    assert seed["updateSource"] is None
    assert admission == stored_admission
    assert str(relocated) not in admission["admissionIdentity"]
    assert (output / managed.LAUNCHER_NAME).read_bytes() == b"MZ-launcher"
    assert (output / "versions" / "0.10.5" / "NvtFwCombiner.exe").is_file()


def test_managed_root_builder_rejects_tampered_seed_package(tmp_path: Path) -> None:
    probe = tmp_path / "probe"
    probe.mkdir()
    (probe / "NvtFwCombiner.ReadyProbe.exe").write_bytes(b"MZ-probe")
    source = tmp_path / "source"
    launcher = tmp_path / "launcher.exe"
    launcher.write_bytes(b"MZ-launcher")
    update = _load(UPDATE_SCRIPT, "create_update_lab_tamper")
    managed = _load(MANAGED_SCRIPT, "create_managed_lab_tamper")
    update.build_lab(source, probe)
    catalog = json.loads((source / update.CATALOG_NAME).read_text(encoding="utf-8"))
    package = source / catalog["versions"][0]["packagePath"]
    package.write_bytes(package.read_bytes() + b"tampered")

    try:
        managed.build_managed_root(tmp_path / "managed-root", source, launcher)
    except ValueError:
        pass
    else:
        raise AssertionError("tampered seed package must fail closed")

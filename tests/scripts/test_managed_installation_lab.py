from __future__ import annotations

import json
import shutil
from pathlib import Path

from scripts.create_managed_installation_lab import (
    ADMISSION_NAME,
    BOOTSTRAP_NAME,
    SEED_NAME,
    build_managed_root,
)
from tests.scripts import test_create_managed_installation_lab as _current_lab_support


def _create_current_source(source: Path) -> None:
    _current_lab_support.ManagedInstallationLabTests.create_source(source)


def test_managed_root_seed_is_content_bound_and_source_location_independent(tmp_path: Path) -> None:
    source = tmp_path / "source"
    relocated = tmp_path / "relocated" / "source"
    bootstrap = tmp_path / BOOTSTRAP_NAME
    bootstrap.write_bytes(b"MZ-bootstrap")
    _create_current_source(source)
    shutil.move(source, relocated)
    output = tmp_path / "managed-root"

    build_managed_root(output, relocated, bootstrap)

    seed = json.loads((output / SEED_NAME).read_text(encoding="utf-8"))
    admission = seed["admissions"][0]
    stored_admission = json.loads(
        (output / "versions" / "1.0.0" / ADMISSION_NAME).read_text(encoding="utf-8")
    )
    assert seed["activeVersion"] == seed["lastKnownGoodVersion"] == "1.0.0"
    assert seed["updateSource"] is None
    assert admission == stored_admission
    assert str(relocated) not in admission["admissionIdentity"]
    assert (output / BOOTSTRAP_NAME).read_bytes() == b"MZ-bootstrap"
    assert (output / "versions" / "1.0.0" / "NvtFwCombiner.exe").is_file()
    assert (
        output
        / "versions"
        / "1.0.0"
        / "launcher"
        / "NvtFwCombiner.Launcher.exe"
    ).is_file()


def test_managed_root_builder_rejects_tampered_seed_package(tmp_path: Path) -> None:
    source = tmp_path / "source"
    bootstrap = tmp_path / BOOTSTRAP_NAME
    bootstrap.write_bytes(b"MZ-bootstrap")
    _create_current_source(source)
    catalog = json.loads((source / "update-catalog.v1.json").read_text(encoding="utf-8"))
    package = source / catalog["versions"][0]["packagePath"]
    package.write_bytes(package.read_bytes() + b"tampered")

    try:
        build_managed_root(tmp_path / "managed-root", source, bootstrap)
    except ValueError:
        pass
    else:
        raise AssertionError("tampered seed package must fail closed")

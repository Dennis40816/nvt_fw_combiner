"""Confidential reference intake keeps source material out of public proposals."""

from __future__ import annotations

import importlib.util
import json
from pathlib import Path

import pytest


SCRIPT = Path(__file__).resolve().parents[2] / "scripts" / "intake_ic_reference.py"
SPEC = importlib.util.spec_from_file_location("intake_ic_reference", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
intake = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(intake)


@pytest.mark.parametrize(
    ("name", "kind"),
    [
        ("flashmap.xlsx", "flash-map"),
        ("memory_mmap.h", "mmap-header"),
        ("postbuild.bat", "postbuild-script"),
        ("flash_header.xlsx", "flash-header"),
        ("fwconfig.c", "firmware-source"),
    ],
)
def test_confidential_reference_proposes_private_storage_and_public_hash_only(
    tmp_path: Path, name: str, kind: str
) -> None:
    source = tmp_path / name
    source.write_bytes(b"synthetic reference fixture")

    artifact = intake.make_artifact(source, tmp_path, tmp_path / "staged", True, "nt-example")
    fragment = intake.build_confidential_manifest_fragment([artifact])

    assert artifact["proposedPrivateDestination"].startswith("nfc/references/docs/references/")
    assert "proposedTrackedDestination" not in artifact
    assert len(fragment) == 1
    assert fragment[0]["kind"] == kind
    assert fragment[0]["sizeBytes"] == source.stat().st_size
    assert fragment[0]["sha256"] == artifact["sha256"]
    assert fragment[0]["id"].startswith(f"{kind}-nt-example-")
    assert name not in json.dumps(fragment)
    assert "path" not in fragment[0]


def test_supporting_document_is_not_a_confidential_manifest_entry(tmp_path: Path) -> None:
    source = tmp_path / "notes.md"
    source.write_text("synthetic", encoding="utf-8")

    artifact = intake.make_artifact(source, tmp_path, tmp_path / "staged", True, "nt-example")

    assert artifact["category"] == "supporting-reference"
    assert intake.build_confidential_manifest_fragment([artifact]) == []
    assert "proposedPrivateDestination" not in artifact


@pytest.mark.parametrize("name", ["sheet.xlsx", "header.h"])
def test_unrecognized_source_format_requires_classification(tmp_path: Path, name: str) -> None:
    source = tmp_path / name
    source.write_bytes(b"synthetic reference fixture")

    artifact = intake.make_artifact(source, tmp_path, tmp_path / "staged", True, "nt-example")

    assert artifact["category"] == "unclassified"
    assert "proposedPrivateDestination" not in artifact
    assert intake.build_confidential_manifest_fragment([artifact]) == []


def test_identical_private_sources_share_one_public_identity(tmp_path: Path) -> None:
    artifacts = []
    for name in ("flashmap-first.xlsx", "flashmap-second.xlsx"):
        source = tmp_path / name
        source.write_bytes(b"same synthetic reference")
        artifacts.append(intake.make_artifact(source, tmp_path, tmp_path / "staged", True, "nt-example"))

    fragment = intake.build_confidential_manifest_fragment(artifacts)

    assert len(fragment) == 1
    assert fragment[0]["sha256"] == artifacts[0]["sha256"]
    assert len(artifacts) == 2


def test_conflicting_identity_is_rejected_without_private_names(tmp_path: Path) -> None:
    source = tmp_path / "flashmap.xlsx"
    source.write_bytes(b"synthetic")
    artifact = intake.make_artifact(source, tmp_path, tmp_path / "staged", True, "nt-example")
    conflicting = {**artifact, "sha256": "0" * 64}

    with pytest.raises(ValueError, match="conflicting public identities"):
        intake.build_confidential_manifest_fragment([artifact, conflicting])

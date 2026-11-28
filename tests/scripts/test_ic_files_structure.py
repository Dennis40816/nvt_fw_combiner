"""Exercise the imported structure C validator through its public command."""

from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys

import pytest


REPOSITORY = Path(__file__).resolve().parents[2]
SCRIPT = REPOSITORY / "scripts/check_ic_files.py"
IC_FILES = REPOSITORY / "profiles/ic-files"


def snapshot(root: Path) -> dict[str, tuple[bytes, int]]:
    return {
        path.relative_to(root).as_posix(): (path.read_bytes(), path.stat().st_mtime_ns)
        for path in root.rglob("*")
        if path.is_file()
    }


def run_checker(root: Path, *arguments: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, "-B", str(SCRIPT), "--root", str(root), *arguments],
        capture_output=True,
        text=True,
        encoding="utf-8",
        check=False,
    )


@pytest.mark.parametrize("arguments", [(), ("--force-fallback",)])
def test_repository_ic_files_pass_both_engines_without_writes(
    arguments: tuple[str, ...],
) -> None:
    before = snapshot(IC_FILES)
    result = run_checker(IC_FILES, *arguments)
    assert result.returncode == 0, result.stdout + result.stderr
    assert "60 documents / 5 document types" in result.stdout
    assert "175 rule/layout pairs" in result.stdout
    assert "94 canonical routeId values once; 38 exact expected IDs" in result.stdout
    assert "PASS negative controls:" in result.stdout
    engine = (
        "jsonschema Draft 2020-12"
        if not arguments and importlib.util.find_spec("jsonschema") is not None
        else "fallback schema evaluator"
    )
    assert "Engine: " + engine in result.stdout
    assert snapshot(IC_FILES) == before


@pytest.mark.parametrize("arguments", [(), ("--force-fallback",)])
@pytest.mark.parametrize("fault", ["schema", "nested-schema", "reference"])
def test_invalid_imported_file_fails_without_writes(
    tmp_path: Path, arguments: tuple[str, ...], fault: str
) -> None:
    root = Path(shutil.copytree(IC_FILES, tmp_path / "ic-files"))
    if fault == "schema":
        path = root / "definitions/dpcmi.yaml"
        path.write_bytes(path.read_bytes() + b"unexpected: true\n")
        reason = "schema rejected"
    elif fault == "nested-schema":
        path = root / "definitions/nested/invalid.yml"
        path.parent.mkdir()
        path.write_text(
            'schema-version: 1\ndefinition: "extra"\nkind: 7\n', encoding="utf-8"
        )
        reason = "schema rejected"
    else:
        path = root / "ic/nt51917.yaml"
        body = path.read_text(encoding="utf-8")
        assert 'file: "parts/nt51917.facts.yaml"' in body
        path.write_text(
            body.replace('file: "parts/nt51917.facts.yaml"', 'file: "../missing.yaml"'),
            encoding="utf-8",
        )
        reason = "Dangling or escaping include"
    before = snapshot(root)
    result = run_checker(root, *arguments)
    assert result.returncode == 1, result.stdout + result.stderr
    assert reason in result.stderr
    assert snapshot(root) == before


def test_checker_and_formatter_work_in_an_isolated_clone_without_site_packages(
    tmp_path: Path,
) -> None:
    clone = tmp_path / "clone"
    for name in (
        "check_ic_files.py",
        "ic_files.py",
        "ic_files_refs.json",
        "ic_files_structure_refs.json",
        "ic_files_routes.json",
    ):
        destination = clone / "scripts" / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(REPOSITORY / "scripts" / name, destination)
    shutil.copytree(IC_FILES, clone / "profiles/ic-files")
    refs = json.loads(
        (clone / "scripts/ic_files_structure_refs.json").read_text(encoding="utf-8")
    )
    for relative in (
        "docs/contracts/canonical-capability-policy-v1.json",
        "docs/references/g0-output-baseline/recipes.json",
        *refs["generalMergeProfiles"].values(),
    ):
        destination = clone / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(REPOSITORY / relative, destination)
    before = snapshot(clone)
    for command in (
        ["scripts/check_ic_files.py", "--root", "profiles/ic-files"],
        ["scripts/ic_files.py", "fmt", "--root", "profiles/ic-files", "--check"],
    ):
        result = subprocess.run(
            [sys.executable, "-S", "-B", *command],
            cwd=clone,
            capture_output=True,
            text=True,
            encoding="utf-8",
            check=False,
        )
        assert result.returncode == 0, result.stdout + result.stderr
    assert snapshot(clone) == before

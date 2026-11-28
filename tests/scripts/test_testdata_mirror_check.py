"""The nvt-testdata mirror must match the repository copy file for file."""

import json
from pathlib import Path

import pytest

from scripts import testdata_mirror_check as MODULE


PIN = {
    "schemaVersion": 1,
    "repository": "example-owner/example-data",
    "commit": "0123456789abcdef0123456789abcdef01234567",
    "root": "nfc",
}


def write(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


@pytest.fixture
def trees(tmp_path):
    """Create a synthetic repository copy and an equal mirror."""
    repository_root = tmp_path / "repository"
    repository = MODULE.repository_copy(repository_root)
    mirror = tmp_path / "mirror" / "nfc"
    for root in (repository, mirror):
        write(root / "README.md", b"synthetic\n")
        write(root / "golden" / "case" / "input.dat", bytes(range(16)))
    write(repository / MODULE.PIN_FILE, json.dumps(PIN).encode("utf-8"))
    return repository_root, repository, mirror


def run(monkeypatch, repository_root, mirror, capsys):
    monkeypatch.setenv("NFC_TESTDATA_ROOT", str(mirror))
    status = MODULE.check_mirror(repository_root)
    return status, capsys.readouterr()


def test_equal_trees_pass(monkeypatch, trees, capsys):
    repository_root, _, mirror = trees
    status, output = run(monkeypatch, repository_root, mirror, capsys)
    assert status == 0
    assert "matches" in output.out
    assert "(2 files)" in output.out


def test_missing_file_fails(monkeypatch, trees, capsys):
    repository_root, _, mirror = trees
    (mirror / "README.md").unlink()
    status, output = run(monkeypatch, repository_root, mirror, capsys)
    assert status == 1
    assert "missing: 1\n  README.md" in output.err


def test_extra_file_fails(monkeypatch, trees, capsys):
    repository_root, _, mirror = trees
    write(mirror / "golden" / "extra.dat", b"x")
    status, output = run(monkeypatch, repository_root, mirror, capsys)
    assert status == 1
    assert "extra: 1\n  golden/extra.dat" in output.err


def test_changed_bytes_fail(monkeypatch, trees, capsys):
    repository_root, _, mirror = trees
    write(mirror / "golden" / "case" / "input.dat", bytes(range(1, 17)))
    status, output = run(monkeypatch, repository_root, mirror, capsys)
    assert status == 1
    assert "changed: 1\n  golden/case/input.dat" in output.err


def test_report_is_bounded(monkeypatch, trees, capsys):
    repository_root, _, mirror = trees
    for index in range(MODULE.REPORT_LIMIT + 5):
        write(mirror / "extra" / f"{index:03}.dat", b"x")
    status, output = run(monkeypatch, repository_root, mirror, capsys)
    assert status == 1
    assert f"extra: {MODULE.REPORT_LIMIT + 5}" in output.err
    assert "  ... 5 more" in output.err
    assert "extra/024.dat" not in output.err


def test_pin_file_is_excluded_on_both_sides(monkeypatch, trees, capsys):
    repository_root, _, mirror = trees
    status, _ = run(monkeypatch, repository_root, mirror, capsys)
    assert status == 0
    write(mirror / MODULE.PIN_FILE, b"different pin")
    status, _ = run(monkeypatch, repository_root, mirror, capsys)
    assert status == 0


def test_unset_root_fails_instead_of_comparing_the_copy(monkeypatch, trees):
    repository_root, _, _ = trees
    monkeypatch.delenv("NFC_TESTDATA_ROOT", raising=False)
    with pytest.raises(ValueError, match="NFC_TESTDATA_ROOT"):
        MODULE.check_mirror(repository_root)


@pytest.mark.parametrize("configured", ["", " ", "relative/example"])
def test_invalid_root_fails(monkeypatch, trees, configured):
    repository_root, _, _ = trees
    monkeypatch.setenv("NFC_TESTDATA_ROOT", configured)
    with pytest.raises(ValueError, match="NFC_TESTDATA_ROOT"):
        MODULE.check_mirror(repository_root)


def test_repository_copy_is_not_a_mirror(monkeypatch, trees):
    repository_root, repository, _ = trees
    monkeypatch.setenv("NFC_TESTDATA_ROOT", str(repository))
    with pytest.raises(ValueError, match="repository copy"):
        MODULE.check_mirror(repository_root)


def test_main_reports_errors_with_exit_one(monkeypatch, capsys):
    monkeypatch.delenv("NFC_TESTDATA_ROOT", raising=False)
    assert MODULE.main([]) == 1
    assert "NFC_TESTDATA_ROOT" in capsys.readouterr().err


def test_pin_outputs_are_validated(tmp_path, trees):
    repository_root, repository, _ = trees
    output = tmp_path / "github-output"
    MODULE.write_pin_outputs(output, repository_root)
    assert output.read_text(encoding="utf-8") == (
        f"repository={PIN['repository']}\ncommit={PIN['commit']}\nroot={PIN['root']}\n"
    )
    for key, value in (("commit", "abc123"), ("root", ".."), ("repository", "x"), ("schemaVersion", 2)):
        write(repository / MODULE.PIN_FILE, json.dumps({**PIN, key: value}).encode("utf-8"))
        with pytest.raises(ValueError, match="nvt-testdata.json"):
            MODULE.read_pin(repository_root)


def test_repository_pin_is_valid():
    pin = MODULE.read_pin()
    assert pin["repository"] == "Dennis40816/nvt-testdata"
    assert pin["root"] == "nfc"

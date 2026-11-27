"""The test-data root is explicit when configured and fails closed."""

from pathlib import Path

import pytest

from scripts.testdata_paths import repository_path, resolve_testdata_root


ROOT = Path(__file__).resolve().parents[2]


def test_unset_root_uses_repository_copy(monkeypatch):
    monkeypatch.delenv("NFC_TESTDATA_ROOT", raising=False)
    assert resolve_testdata_root() == ROOT / "testdata"
    assert repository_path("testdata/golden/canonical/manifest.json") == (
        ROOT / "testdata/golden/canonical/manifest.json"
    )


def test_existing_absolute_root_overrides_repository(monkeypatch):
    configured = ROOT / "tests"
    monkeypatch.setenv("NFC_TESTDATA_ROOT", str(configured))
    assert resolve_testdata_root() == configured
    assert repository_path("testdata/golden/canonical/manifest.json") == (
        configured / "golden/canonical/manifest.json"
    )
    assert repository_path("CONTRIBUTING.md") == ROOT / "CONTRIBUTING.md"


@pytest.mark.parametrize("configured", [
    "", " ", "relative/example", str(ROOT / "missing-example-testdata"),
    str(ROOT / "CONTRIBUTING.md"),
])
def test_invalid_configured_root_fails_without_fallback(monkeypatch, configured):
    monkeypatch.setenv("NFC_TESTDATA_ROOT", configured)
    with pytest.raises(ValueError, match="NFC_TESTDATA_ROOT"):
        resolve_testdata_root()

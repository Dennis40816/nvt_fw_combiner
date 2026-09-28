"""Regression checks for user-profile path disclosure in tracked text."""

from pathlib import Path
import sys

import pytest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))
from validate_repository import validate_private_user_profile_paths  # noqa: E402


def test_rejects_named_windows_user_profile_paths(tmp_path: Path) -> None:
    document = tmp_path / "new.md"
    document.write_text("Reference: C:/Users/" + "developer" + "/Desktop/image.png\n")
    errors: list[str] = []

    validate_private_user_profile_paths([document], errors, root=tmp_path)

    assert errors == ["private user-profile path in new.md:1"]


def test_allows_placeholder_accounts_and_other_path_roots(tmp_path: Path) -> None:
    document = tmp_path / "new.md"
    document.write_text(
        "C:/Users/owner/example.png\n"
        "C:\\\\Users\\\\operator\\\\example.png\n"
        "D:/NvtFwCombiner-TestArea/evidence\n"
    )
    errors: list[str] = []

    validate_private_user_profile_paths([document], errors, root=tmp_path)

    assert errors == []


def test_rejects_private_path_in_extensionless_tracked_text(tmp_path: Path) -> None:
    document = tmp_path / "CODEOWNERS"
    document.write_text("# C:/Users/" + "developer" + "/Desktop/reference\n", encoding="utf-8")
    errors: list[str] = []

    validate_private_user_profile_paths([document], errors, root=tmp_path)

    assert errors == ["private user-profile path in CODEOWNERS:1"]


def test_rejects_private_path_in_svg_tracked_text(tmp_path: Path) -> None:
    document = tmp_path / "reference.svg"
    document.write_text("<!-- C:/Users/" + "developer" + "/Desktop/reference -->\n", encoding="utf-8")
    errors: list[str] = []

    validate_private_user_profile_paths([document], errors, root=tmp_path)

    assert errors == ["private user-profile path in reference.svg:1"]


def test_skips_binary_content_with_embedded_null(tmp_path: Path) -> None:
    document = tmp_path / "payload.bin"
    document.write_bytes(b"\0C:/Users/developer/Desktop/reference\0")
    errors: list[str] = []

    validate_private_user_profile_paths([document], errors, root=tmp_path)

    assert errors == []


@pytest.mark.parametrize("relative", [
    "docs/governance/change-records/DOC-HYGIENE-1113-PRIVATE-PATHS-01.json",
    "docs/governance/change-records/LAUNCHER-106-UI-01.json",
    "docs/governance/change-records/UI-114-MEMORY-CARDS-31.json",
    "docs/governance/waivers/REL-110-FULL-VERIFY-OWNER-WAIVER-01.md",
])
def test_historical_evidence_allows_only_existing_occurrences(
    tmp_path: Path, relative: str
) -> None:
    document = tmp_path / relative
    document.parent.mkdir(parents=True)
    original = (ROOT / relative).read_bytes()
    document.write_bytes(original)
    errors: list[str] = []
    validate_private_user_profile_paths([document], errors, root=tmp_path)
    assert errors == []

    document.write_bytes(original + b"\nC:/Users/developer/Desktop/new-reference\n")
    validate_private_user_profile_paths([document], errors, root=tmp_path)

    assert len(errors) == 1
    assert errors[0].startswith(f"private user-profile path in {relative}:")

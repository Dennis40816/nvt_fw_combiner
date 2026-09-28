"""Regression checks for user-profile path disclosure in tracked text."""

from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "scripts"))
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

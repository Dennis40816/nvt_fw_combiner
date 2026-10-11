"""Closed golden-tree identity, with privacy-safe diagnostics."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import stat
import sys

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_BASELINE = ROOT / "scripts/ci/test-baseline.json"
DEFAULT_PATHS = ("testdata/golden", "docs/references")
INVALID_TREE = "Golden tree is missing or invalid."
PIN_MISMATCH = "Golden pin verification failed."
UNBOUND_PIN = "pin is not bound to a private commit"


def filesystem_path(path: Path) -> Path:
    """Use Windows extended paths for IO, keeping repository-relative identities unchanged."""
    # Extended Windows paths do not interpret parent segments. Normalize lexically,
    # never resolve: resolving would hide symlink/reparse evidence from reject_links.
    path = Path(os.path.normpath(path.absolute()))
    value = str(path)
    if os.name == "nt" and not value.startswith("\\\\?\\"):
        value = "\\\\?\\UNC\\" + value[2:] if value.startswith("\\\\") else "\\\\?\\" + value
        return Path(value)
    return path


def safe_relative(value: str) -> PurePosixPath:
    path = PurePosixPath(value)
    if (not value or "\\" in value or ":" in value or path.is_absolute()
            or any(p in ("", ".", "..") for p in value.split("/"))
            or "\n" in value or "\r" in value):
        raise ValueError(INVALID_TREE)
    return path


def reject_links(path: Path) -> None:
    """Reject symlinks and Windows junctions, including existing ancestors."""
    path = filesystem_path(path)
    for component in reversed((path, *path.parents)):
        try:
            status = component.lstat()
        except FileNotFoundError:
            continue
        if (stat.S_ISLNK(status.st_mode) or
                getattr(status, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT):
            raise ValueError(INVALID_TREE)


def regular_files(root: Path, paths: list[str] | tuple[str, ...]) -> list[Path]:
    root = filesystem_path(root)
    reject_links(root)
    selected = [safe_relative(value) for value in paths]
    if not selected or len(set(selected)) != len(selected):
        raise ValueError(INVALID_TREE)
    for index, path in enumerate(selected):
        if any(path in other.parents or other in path.parents for other in selected[index + 1:]):
            raise ValueError(INVALID_TREE)
    files = []
    for relative in selected:
        target = root.joinpath(*relative.parts)
        reject_links(target)
        if not stat.S_ISDIR(target.lstat().st_mode):
            raise ValueError(INVALID_TREE)
        pending = [target]
        while pending:
            directory = pending.pop()
            with os.scandir(directory) as entries:
                for entry in entries:
                    status = entry.stat(follow_symlinks=False)
                    if (entry.is_symlink() or getattr(status, "st_file_attributes", 0)
                            & stat.FILE_ATTRIBUTE_REPARSE_POINT):
                        raise ValueError(INVALID_TREE)
                    if stat.S_ISDIR(status.st_mode):
                        pending.append(Path(entry.path))
                    elif stat.S_ISREG(status.st_mode):
                        files.append(Path(entry.path))
                    else:
                        raise ValueError(INVALID_TREE)
    return sorted(files, key=lambda path: path.relative_to(root).as_posix().encode("utf-8"))


def compute(root: Path | str, paths: list[str] | tuple[str, ...]) -> dict:
    """SHA256 of UTF-8 ordinal path-sorted '<sha> <size> <path>\\n' lines."""
    try:
        root = filesystem_path(Path(root))
        files = regular_files(root, paths)
        tree = hashlib.sha256()
        for path in files:
            content = hashlib.sha256()
            size = 0
            with path.open("rb") as stream:
                while chunk := stream.read(1024 * 1024):
                    size += len(chunk)
                    content.update(chunk)
            relative = path.relative_to(root).as_posix()
            safe_relative(relative)
            tree.update(f"{content.hexdigest()} {size} {relative}\n".encode("utf-8"))
        return dict(fileCount=len(files), treeSha256=tree.hexdigest())
    except (OSError, ValueError, UnicodeError):
        raise ValueError(INVALID_TREE) from None


def verify(root: Path | str, pin: dict) -> dict:
    try:
        observed = compute(root, pin["paths"])
        if observed != {key: pin[key] for key in ("fileCount", "treeSha256")}:
            raise ValueError(PIN_MISMATCH)
        return observed
    except (OSError, ValueError, KeyError, TypeError):
        raise ValueError(PIN_MISMATCH) from None


def load_pin(baseline: Path) -> dict:
    try:
        pin = json.loads(baseline.read_text(encoding="utf-8-sig"))["pin"]
        if (pin["assetsRepo"] != "Dennis40816/nvt-private-assets"
                or pin["assetsPath"] != "nfc/golden-assembly"
                or pin["paths"] != list(DEFAULT_PATHS)
                or re.fullmatch(r"[0-9a-f]{40}", pin["assetsCommit"]) is None
                or type(pin["fileCount"]) is not int or pin["fileCount"] < 0
                or re.fullmatch(r"[0-9a-f]{64}", pin["treeSha256"]) is None):
            raise ValueError(PIN_MISMATCH)
        return pin
    except (OSError, ValueError, KeyError, TypeError):
        raise ValueError(PIN_MISMATCH) from None


def require_bound_pin(pin: dict) -> None:
    if pin["assetsCommit"] == "0" * 40:
        raise ValueError(UNBOUND_PIN)


def write_pin(root: Path, baseline: Path) -> dict:
    """Replace exactly fileCount and treeSha256 tokens, preserving all other bytes."""
    try:
        pin = load_pin(baseline)
        observed = compute(root, pin["paths"])
        payload = baseline.read_bytes()
        text = payload.decode("utf-8-sig")
        match = re.search(r'"pin"\s*:\s*', text)
        if match is None or len(re.findall(r'"pin"\s*:', text)) != 1:
            raise ValueError(PIN_MISMATCH)
        _, length = json.JSONDecoder().raw_decode(text[match.end():])
        start, end = match.end(), match.end() + length
        block = text[start:end]
        for key, value in observed.items():
            token = str(value) if key == "fileCount" else json.dumps(value)
            pattern = rf'("{key}"\s*:\s*)(?:[0-9]+|"[0-9a-f]{{64}}")'
            block, changed = re.subn(pattern, lambda m: m[1] + token, block)
            if changed != 1:
                raise ValueError(PIN_MISMATCH)
        updated = (text[:start] + block + text[end:]).encode("utf-8")
        if payload.startswith(b"\xef\xbb\xbf"):
            updated = b"\xef\xbb\xbf" + updated
        baseline.write_bytes(updated)
        return observed
    except (OSError, ValueError, TypeError):
        raise ValueError("Golden pin could not be written.") from None


def pin_message(pin: dict) -> str:
    return f"[golden-pin] fileCount {pin['fileCount']} treeSha256 {pin['treeSha256'][:12]}"


class FixedArgumentParser(argparse.ArgumentParser):
    """Argument errors print one fixed sentence. The default text echoes the offending argument,
    which can be a private file name."""

    def error(self, message: str):  # noqa: ARG002 - the message is deliberately not printed
        self.exit(2, "Invalid arguments.\n")


def main() -> int:
    parser = FixedArgumentParser(description=__doc__, allow_abbrev=False)
    parser.add_argument("command", choices=("compute", "verify", "write-pin"))
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--baseline", type=Path, default=DEFAULT_BASELINE)
    parser.add_argument("--paths", nargs="+", default=list(DEFAULT_PATHS))
    args = parser.parse_args()
    try:
        if args.command == "compute":
            observed = compute(args.root, args.paths)
        elif args.command == "verify":
            observed = verify(args.root, load_pin(args.baseline))
        else:
            observed = write_pin(args.root, args.baseline)
        print(pin_message(observed))
        return 0
    except ValueError as error:
        print(str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())

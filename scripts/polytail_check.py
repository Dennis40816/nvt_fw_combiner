"""Fast repository anti-slop checks used by the Polytail skill and CI."""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCAN_ROOTS = (ROOT / "src", ROOT / "tools", ROOT / "scripts")
SKIP_PARTS = {
    "refcode",
    ".git",
    ".dotnet",
    "bin",
    "obj",
    "dist",
    "artifacts",
    "__pycache__",
}
SOURCE_SUFFIXES = {".cs", ".py", ".ps1", ".sh"}
PLACEHOLDER_PATTERN = re.compile(
    r"\b(?:TODO|FIXME|HACK|NotImplementedException|NotImplementedError)\b",
    re.IGNORECASE,
)
STALE_TOKENS = {
    "customMappings": "use explicitMappings",
    "custom-merge": "use general-merge",
    "replace-ae": "use display/tp-hw/tp-fw/general replace experiences",
    "replace-arbitrary": "use general-replace",
}


def main() -> int:
    errors: list[str] = []
    warnings: list[str] = []
    for root in SCAN_ROOTS:
        for path in root.rglob("*"):
            if not path.is_file() or path.suffix.lower() not in SOURCE_SUFFIXES:
                continue
            rel = path.relative_to(ROOT)
            if any(part in SKIP_PARTS for part in rel.parts):
                continue
            text = path.read_text(encoding="utf-8")
            if path.name != "polytail_check.py" and PLACEHOLDER_PATTERN.search(text):
                errors.append(
                    f"placeholder marker in production/tooling source: {rel.as_posix()}"
                )
            if path.name not in {"polytail_check.py", "validate_repository.py"}:
                for token, guidance in STALE_TOKENS.items():
                    if token in text:
                        errors.append(
                            f"stale token {token!r} in {rel.as_posix()}: {guidance}"
                        )
            line_count = text.count("\n") + 1
            if path.suffix.lower() in {".cs", ".py"} and line_count > 800:
                warnings.append(
                    f"code-size review oversized source file ({line_count} lines): {rel.as_posix()}"
                )
            if "refcode" in text and path.suffix.lower() == ".cs":
                errors.append(f"C# source must not reference refcode: {rel.as_posix()}")

    if errors:
        for error in sorted(set(errors)):
            print(f"POLYTAIL: {error}", file=sys.stderr)
        return 1
    for warning in sorted(set(warnings)):
        print(f"POLYTAIL WARNING: {warning}", file=sys.stderr)
    print("Polytail fast checks passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

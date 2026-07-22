"""Render one complete stable release section from the repository changelog."""

from __future__ import annotations

import argparse
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_CHANGELOG = ROOT / "CHANGELOG.md"
STABLE_VERSION = re.compile(r"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$")
SECTION_HEADING = re.compile(r"^## \[(?P<version>[^]]+)](?:\s+-\s+.*)?$", re.MULTILINE)
REQUIRED_SECTIONS = (
    "### Summary",
    "### Product changes",
    "### Security",
    "### Known issues",
    "### Upgrade and rollback",
    "### Downloads and integrity",
)
REQUIRED_FEATURE_FIELDS = (
    "- Before → After:",
    "- Affected:",
    "- Support status:",
    "- Compatibility:",
    "- Verification:",
    "- Limitations:",
)
INCOMPLETE_RELEASE_TEXT = re.compile(
    r"\b(?:T.D|T.DO|FIX.E|PLACE.OLDER)\b",
    re.IGNORECASE,
)
PRIVATE_DISCLOSURE = re.compile(
    r"(?:owner-handoff|testdata[/\\]golden|\.7z\b|(?:password|secret|token)\s*[:=]\s*\S+)",
    re.IGNORECASE,
)


def render_release_notes(changelog: str, version: str) -> str:
    """Return the exact stable-version changelog section as GitHub Release notes."""

    if STABLE_VERSION.fullmatch(version) is None:
        raise ValueError(
            f"version must be stable SemVer without a v prefix: {version!r}"
        )

    matches = list(SECTION_HEADING.finditer(changelog))
    matching = [match for match in matches if match.group("version") == version]
    if len(matching) != 1:
        raise ValueError(
            f"CHANGELOG.md must contain exactly one section for [{version}]; found {len(matching)}"
        )

    start_match = matching[0]
    next_heading = next(
        (match for match in matches if match.start() > start_match.start()), None
    )
    end = next_heading.start() if next_heading is not None else len(changelog)
    body = changelog[start_match.end() : end].strip()
    if not body:
        raise ValueError(f"CHANGELOG.md section [{version}] is empty")

    if INCOMPLETE_RELEASE_TEXT.search(body):
        raise ValueError(
            f"CHANGELOG.md section [{version}] contains an incomplete release token"
        )
    if PRIVATE_DISCLOSURE.search(body):
        raise ValueError(
            f"CHANGELOG.md section [{version}] contains private or secret-like disclosure"
        )

    subsection_matches = list(re.finditer(r"^### (?P<title>\S.*)$", body, re.MULTILINE))
    section_positions: list[tuple[str, int]] = []
    for heading in REQUIRED_SECTIONS:
        matches_for_heading = [
            match for match in subsection_matches if match.group(0) == heading
        ]
        if not matches_for_heading:
            raise ValueError(f"CHANGELOG.md section [{version}] is missing {heading}")
        if len(matches_for_heading) != 1:
            raise ValueError(
                f"CHANGELOG.md section [{version}] must contain exactly one {heading}; "
                f"found {len(matches_for_heading)}"
            )
        section_positions.append((heading, matches_for_heading[0].start()))
    if [position for _, position in section_positions] != sorted(
        position for _, position in section_positions
    ):
        raise ValueError(
            f"CHANGELOG.md section [{version}] required sections are out of order"
        )

    product_start = body.index("### Product changes") + len("### Product changes")
    product_end = body.index("### Security", product_start)
    product_changes = body[product_start:product_end].strip()
    feature_matches = list(
        re.finditer(r"^#### (?P<title>\S.*)$", product_changes, re.MULTILINE)
    )
    if not feature_matches:
        raise ValueError(
            f"CHANGELOG.md section [{version}] has no structured product change"
        )
    for index, feature_match in enumerate(feature_matches):
        end = (
            feature_matches[index + 1].start()
            if index + 1 < len(feature_matches)
            else len(product_changes)
        )
        feature = product_changes[feature_match.end() : end].strip()
        for field in REQUIRED_FEATURE_FIELDS:
            field_match = re.search(rf"^{re.escape(field)}\s*\S", feature, re.MULTILINE)
            if field_match is None:
                raise ValueError(
                    f"CHANGELOG.md feature '{feature_match.group('title')}' is missing non-empty {field}"
                )

    for index, (heading, position) in enumerate(section_positions):
        end = (
            section_positions[index + 1][1]
            if index + 1 < len(section_positions)
            else len(body)
        )
        if not body[position + len(heading) : end].strip():
            raise ValueError(f"CHANGELOG.md section [{version}] has empty {heading}")

    return f"# NVT FW Combiner v{version}\n\n{body}\n"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", required=True)
    parser.add_argument("--changelog", type=Path, default=DEFAULT_CHANGELOG)
    parser.add_argument("--output", type=Path, required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    changelog_path = args.changelog.resolve()
    output_path = args.output.resolve()
    notes = render_release_notes(
        changelog_path.read_text(encoding="utf-8"), args.version
    )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(notes, encoding="utf-8", newline="\n")
    print(f"Rendered release notes for v{args.version}: {output_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

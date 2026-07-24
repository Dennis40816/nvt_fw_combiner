"""Parse the closed YAML schema used by repository skill metadata."""

from __future__ import annotations

import json
import re
from pathlib import Path


def parse_skill_metadata(
    metadata_path: Path, repository_root: Path, errors: list[str]
) -> dict[str, dict[str, str | bool]] | None:
    """Return closed skill metadata or append a structural YAML error."""

    metadata: dict[str, dict[str, str | bool]] = {"interface": {}, "policy": {}}
    seen_sections: set[str] = set()
    current_section: str | None = None
    is_valid = True

    for line_number, raw_line in enumerate(
        metadata_path.read_text(encoding="utf-8").splitlines(), start=1
    ):
        if not raw_line.strip():
            continue
        if "\t" in raw_line:
            errors.append(
                f"skill metadata is not valid YAML at {metadata_path.relative_to(repository_root)}:{line_number}"
            )
            is_valid = False
            continue
        section_match = re.fullmatch(r"([a-z][a-z_]*):\s*", raw_line)
        if section_match is not None:
            section = section_match.group(1)
            if section not in metadata or section in seen_sections:
                errors.append(
                    f"skill metadata is not valid YAML at {metadata_path.relative_to(repository_root)}:{line_number}"
                )
                is_valid = False
                current_section = None
                continue
            seen_sections.add(section)
            current_section = section
            continue

        field_match = re.fullmatch(r"  ([a-z][a-z_]*):\s*(.+)", raw_line)
        if field_match is None or current_section is None:
            errors.append(
                f"skill metadata is not valid YAML at {metadata_path.relative_to(repository_root)}:{line_number}"
            )
            is_valid = False
            continue

        field, raw_value = field_match.groups()
        section = metadata[current_section]
        if field in section:
            errors.append(
                f"skill metadata is not valid YAML at {metadata_path.relative_to(repository_root)}:{line_number}"
            )
            is_valid = False
            continue
        if current_section == "interface":
            try:
                value = json.loads(raw_value)
            except json.JSONDecodeError:
                errors.append(
                    f"skill metadata is not valid YAML at {metadata_path.relative_to(repository_root)}:{line_number}"
                )
                is_valid = False
                continue
            if not isinstance(value, str):
                errors.append(
                    f"skill metadata is not valid YAML at {metadata_path.relative_to(repository_root)}:{line_number}"
                )
                is_valid = False
                continue
        elif raw_value in {"true", "false"}:
            value = raw_value == "true"
        else:
            errors.append(
                f"skill metadata is not valid YAML at {metadata_path.relative_to(repository_root)}:{line_number}"
            )
            is_valid = False
            continue
        section[field] = value

    if not is_valid:
        return None
    return metadata

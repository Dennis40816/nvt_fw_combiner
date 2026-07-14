"""Stage owner-provided IC reference evidence into a reviewed handoff area."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import sys
from collections import Counter
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

from ic_reference_candidate_intake import WORKFLOWS, stage_manifest_request

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT_ROOT = ROOT / "testdata" / "golden" / "owner-handoff"

SKIP_DIRECTORY_NAMES = {
    ".git",
    ".hg",
    ".svn",
    "__pycache__",
    ".pytest_cache",
    ".ruff_cache",
    ".mypy_cache",
    ".venv",
    "venv",
    "intake",
    "bin",
    "obj",
}

FIRMWARE_EXTENSIONS = {".bin", ".hex", ".fw", ".dat", ".mot", ".s19", ".srec"}
EXECUTABLE_EXTENSIONS = {".exe", ".dll"}
ARCHIVE_EXTENSIONS = {".7z", ".zip", ".rar", ".tar", ".gz", ".tgz"}
SCRIPT_EXTENSIONS = {".bat", ".cmd", ".ps1", ".sh"}
SOURCE_EXTENSIONS = {".c", ".cc", ".cpp", ".h", ".hpp", ".py", ".cs", ".asm", ".s"}
DOCUMENT_EXTENSIONS = {
    ".csv",
    ".doc",
    ".docx",
    ".h",
    ".ini",
    ".json",
    ".log",
    ".md",
    ".pdf",
    ".txt",
    ".xls",
    ".xlsx",
    ".yaml",
    ".yml",
}

CATEGORY_FOLDERS = {
    "archive": "archives",
    "combiner-source-reference": "references/combiner-source",
    "external-tool-binary": "tool-binaries",
    "firmware-payload": "payloads",
    "flash-header-reference": "references/flash-header",
    "flashmap-reference": "references/flashmap",
    "mmap-header": "references/mmap",
    "postbuild-script": "references/postbuild",
    "supporting-reference": "references/supporting",
    "unclassified": "unclassified",
}

CATEGORY_DESCRIPTIONS = {
    "archive": "Source archive for owner review; unpack and classify before promotion.",
    "combiner-source-reference": "Combiner source/reference code. Promote only after legal and firmware-owner approval.",
    "external-tool-binary": "External tool binary. Do not commit until an external-tools manifest is approved.",
    "firmware-payload": "Private firmware/golden payload. Do not commit unless owner-approved golden fixture rules are met.",
    "flash-header-reference": "Flash header evidence used to confirm protected/header ranges.",
    "flashmap-reference": "Flash-map workbook or exported table used to confirm regions and ranges.",
    "mmap-header": "Legacy mmap.h/header evidence used to confirm addresses.",
    "postbuild-script": "Legacy postbuild command evidence used to derive structured combiner invocations.",
    "supporting-reference": "Supporting text/table/log evidence.",
    "unclassified": "File could not be classified. Review manually before use.",
}

TRACKED_DESTINATIONS = {
    "combiner-source-reference": "docs/references/ic-flashmap/combiner-source/{name}",
    "flash-header-reference": "docs/references/ic-flashmap/flash-header/{name}",
    "flashmap-reference": "docs/references/ic-flashmap/{name}",
    "mmap-header": "docs/references/ic-flashmap/mmap/{name}",
    "postbuild-script": "docs/references/ic-flashmap/postbuild/{name}",
    "supporting-reference": "docs/references/ic-flashmap/supporting/{name}",
}

PRIVATE_COMMIT_POLICY = (
    "Keep in owner-handoff/private storage. Commit only after owner approval, "
    "manifested size/SHA-256, provenance, and required firmware review."
)

REFERENCE_COMMIT_POLICY = (
    "May be promoted to docs/references only after owner confirms the file contains "
    "no firmware payload, secret, or private customer data."
)

MODE_REQUIREMENTS: dict[str, tuple[tuple[str, ...], ...]] = {
    "reference-only": (
        ("flashmap-reference",),
        ("mmap-header",),
        ("flash-header-reference",),
    ),
    "standard-merge": (
        ("flashmap-reference",),
        ("mmap-header",),
        ("firmware-payload",),
    ),
    "dp-replace": (
        ("flashmap-reference",),
        ("mmap-header",),
        ("flash-header-reference",),
        ("firmware-payload",),
    ),
    "ctrlram-replace": (
        ("flashmap-reference",),
        ("mmap-header",),
        ("postbuild-script",),
        ("combiner-source-reference", "external-tool-binary"),
        ("firmware-payload",),
    ),
    "general-replace": (
        ("flashmap-reference",),
        ("mmap-header",),
        ("flash-header-reference",),
        ("postbuild-script",),
        ("combiner-source-reference", "external-tool-binary"),
        ("firmware-payload",),
    ),
}

ROLE_HINTS = (
    ("expected-output", ("expected", "golden", "output", "flashcode", "flash_code")),
    ("base-image", ("base", "reference")),
    ("dp-input", ("dp", "display")),
    ("tp-input", ("tp", "touch")),
    ("ctrlram-input", ("ctrlram", "ctrl_ram", "ctrl-ram", "cram")),
    ("ld-input", ("ld", "localdimming", "local_dimming", "local-dimming")),
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    input_source = parser.add_mutually_exclusive_group(required=True)
    input_source.add_argument("--source", type=Path, help="Folder containing owner-provided files.")
    input_source.add_argument(
        "--request",
        type=Path,
        help="Manifest-driven candidate intake request; requires --source-root and --output-dir.",
    )
    parser.add_argument("--source-root", type=Path, help="Root containing files declared by --request.")
    parser.add_argument("--output-dir", type=Path, help="New, caller-selected candidate staging directory.")
    parser.add_argument("--ic", help="IC id, for example NT51950 or 51950.")
    parser.add_argument("--mode", choices=WORKFLOWS)
    parser.add_argument("--case", help="Optional case name, for example single, cascade, or dp-0x40000.")
    parser.add_argument("--owner", help="Owner/provenance label recorded in the manifest.")
    parser.add_argument("--source-ref", help="Free-form source note, archive name, ticket, or transfer id.")
    parser.add_argument("--output-root", type=Path)
    parser.add_argument("--run-id", help="Deterministic run id. Defaults to current UTC timestamp.")
    parser.add_argument("--dry-run", action="store_true", help="Print the manifest without copying files.")
    return parser.parse_args()


def normalize_ic(value: str) -> tuple[str, str]:
    compact = re.sub(r"[\s_-]+", "", value.strip().upper())
    if compact.isdigit():
        compact = f"NT{compact}"
    if not compact:
        raise ValueError("IC id cannot be empty")
    slug = sanitize_component(compact)
    return compact, slug


def sanitize_component(value: str) -> str:
    slug = re.sub(r"[^A-Za-z0-9.]+", "-", value.strip()).strip("-").lower()
    if not slug:
        raise ValueError(f"cannot derive safe path component from {value!r}")
    return slug


def now_run_id() -> str:
    return datetime.now(UTC).strftime("%Y%m%dT%H%M%SZ")


def resolve_existing_directory(path: Path) -> Path:
    resolved = path.expanduser().resolve()
    if not resolved.is_dir():
        raise FileNotFoundError(f"source directory does not exist: {path}")
    return resolved


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return path.resolve().as_posix()


def iter_source_files(source: Path) -> list[Path]:
    files: list[Path] = []
    for path in source.rglob("*"):
        if any(part in SKIP_DIRECTORY_NAMES for part in path.relative_to(source).parts):
            continue
        if path.is_file():
            files.append(path)
    return sorted(files, key=lambda item: item.relative_to(source).as_posix().lower())


def classify(path: Path) -> str:
    suffix = path.suffix.lower()
    text = normalize_for_matching(path)

    if suffix in FIRMWARE_EXTENSIONS:
        return "firmware-payload"
    if suffix in EXECUTABLE_EXTENSIONS:
        return "external-tool-binary"
    if suffix in ARCHIVE_EXTENSIONS:
        return "archive"
    if contains_any(text, ("postbuild", "post-build", "post_build")) and suffix in SCRIPT_EXTENSIONS | DOCUMENT_EXTENSIONS:
        return "postbuild-script"
    if contains_any(text, ("mmap", "memory-map", "memory_map")) and suffix in SOURCE_EXTENSIONS | DOCUMENT_EXTENSIONS:
        return "mmap-header"
    if contains_any(text, ("flashmap", "flash-map", "flash_map", "ic-flashmap", "ic_flashmap")):
        return "flashmap-reference"
    if contains_any(text, ("flash-header", "flash_header", "flash header", "tp-header", "tp_header")):
        return "flash-header-reference"
    if "combiner" in text and suffix in SOURCE_EXTENSIONS | SCRIPT_EXTENSIONS | DOCUMENT_EXTENSIONS:
        return "combiner-source-reference"
    if suffix in DOCUMENT_EXTENSIONS:
        return "supporting-reference"
    return "unclassified"


def normalize_for_matching(path: Path) -> str:
    return " ".join(part.lower() for part in path.parts)


def contains_any(text: str, needles: tuple[str, ...]) -> bool:
    return any(needle in text for needle in needles)


def infer_payload_role(path: Path, category: str) -> str | None:
    if category != "firmware-payload":
        return None
    text = normalize_for_matching(path)
    for role, hints in ROLE_HINTS:
        if contains_any(text, hints):
            return role
    return "firmware-payload"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def category_folder(category: str) -> Path:
    return Path(*CATEGORY_FOLDERS[category].split("/"))


def build_output_dir(output_root: Path, mode: str, ic_slug: str, case: str | None, run_id: str) -> Path:
    parts = [output_root, Path(mode), Path(ic_slug)]
    if case is not None:
        parts.append(Path(sanitize_component(case)))
    parts.extend((Path("intake"), Path(sanitize_component(run_id))))
    path = parts[0]
    for part in parts[1:]:
        path /= part
    return path.resolve()


def make_artifact(
    source: Path,
    source_root: Path,
    output_dir: Path,
    dry_run: bool,
) -> dict[str, Any]:
    relative = source.relative_to(source_root)
    category = classify(source)
    staged = output_dir / category_folder(category) / relative
    artifact: dict[str, Any] = {
        "relativeSourcePath": relative.as_posix(),
        "stagedPath": display_path(staged),
        "category": category,
        "description": CATEGORY_DESCRIPTIONS[category],
        "size": source.stat().st_size,
        "sha256": sha256(source),
        "commitPolicy": commit_policy(category),
    }

    role = infer_payload_role(source, category)
    if role is not None:
        artifact["payloadRoleHint"] = role

    proposed = TRACKED_DESTINATIONS.get(category)
    if proposed is not None:
        artifact["proposedTrackedDestination"] = proposed.format(name=source.name)

    if not dry_run:
        staged.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, staged)

    return artifact


def commit_policy(category: str) -> str:
    if category in {"firmware-payload", "external-tool-binary", "archive"}:
        return PRIVATE_COMMIT_POLICY
    if category == "unclassified":
        return "Review manually. Do not commit or use for implementation until classified."
    return REFERENCE_COMMIT_POLICY


def build_manifest(
    *,
    source_dir: Path,
    output_dir: Path,
    ic: str,
    mode: str,
    case: str | None,
    owner: str,
    source_ref: str,
    run_id: str,
    artifacts: list[dict[str, Any]],
) -> dict[str, Any]:
    found_categories = Counter(artifact["category"] for artifact in artifacts)
    found_roles = Counter(artifact.get("payloadRoleHint") for artifact in artifacts if artifact.get("payloadRoleHint"))
    missing = missing_requirements(mode, found_categories)
    return {
        "manifestVersion": "1.0",
        "manifestKind": "ic-reference-handoff",
        "generatedAtUtc": datetime.now(UTC).isoformat(timespec="seconds").replace("+00:00", "Z"),
        "runId": run_id,
        "ic": ic,
        "mode": mode,
        "case": case,
        "owner": owner,
        "sourceRef": source_ref,
        "sourceDirectory": display_path(source_dir),
        "outputDirectory": display_path(output_dir),
        "policy": {
            "defaultStorage": "testdata/golden/owner-handoff; ignored by Git by default",
            "promotion": (
                "Promotion to docs/references, external-tools, or testdata/golden requires a separate "
                "reviewed change with provenance, SHA-256 manifest entries, and firmware-owner approval "
                "where firmware semantics or golden bytes are affected."
            ),
            "csharpChanges": "Not performed by this intake script.",
        },
        "coverage": {
            "artifactCount": len(artifacts),
            "foundCategories": dict(sorted(found_categories.items())),
            "payloadRoleHints": dict(sorted(found_roles.items())),
            "missingDocumentFamilies": missing,
        },
        "artifacts": artifacts,
        "sourceManifestFragment": build_source_manifest_fragment(artifacts),
        "nextActions": next_actions(mode, missing),
    }


def missing_requirements(mode: str, found_categories: Counter[str]) -> list[str]:
    missing: list[str] = []
    for alternatives in MODE_REQUIREMENTS[mode]:
        if any(found_categories[category] > 0 for category in alternatives):
            continue
        labels = " or ".join(alternatives)
        missing.append(f"Missing {labels}")
    return missing


def build_source_manifest_fragment(artifacts: list[dict[str, Any]]) -> list[dict[str, Any]]:
    fragment: list[dict[str, Any]] = []
    for artifact in artifacts:
        category = artifact["category"]
        if category not in TRACKED_DESTINATIONS:
            continue
        fragment.append(
            {
                "path": artifact["proposedTrackedDestination"],
                "sourcePath": artifact["relativeSourcePath"],
                "category": category,
                "size": artifact["size"],
                "sha256": artifact["sha256"],
                "approvalRequired": True,
            }
        )
    return fragment


def next_actions(mode: str, missing: list[str]) -> list[str]:
    actions = [
        "Review unclassified files and rename or split the source folder if important files were not recognized.",
        "Confirm every range from flash-map, flash header, mmap.h, and postbuild evidence in half-open [start, end) form.",
        "Promote only approved reference documents to docs/references/ic-flashmap and update SOURCE_MANIFEST.json.",
        "Promote firmware payloads only through a workflow-specific golden manifest with sizes, SHA-256, provenance, and owner approval.",
    ]
    if mode in {"ctrlram-replace", "general-replace"}:
        actions.append(
            "Derive structured combiner commands from postbuild evidence; do not run BAT/CMD directly from production code."
        )
    if missing:
        actions.append("Collect the missing document families before claiming the IC/mode reference is complete.")
    return actions


def write_json(path: Path, data: dict[str, Any]) -> None:
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def write_next_steps(path: Path, manifest: dict[str, Any]) -> None:
    category_counts = manifest["coverage"]["foundCategories"]
    role_counts = manifest["coverage"]["payloadRoleHints"]
    missing = manifest["coverage"]["missingDocumentFamilies"]
    lines = [
        f"# {manifest['ic']} {manifest['mode']} Reference Intake",
        "",
        f"- Run id: `{manifest['runId']}`",
        f"- Source: `{manifest['sourceDirectory']}`",
        f"- Output: `{manifest['outputDirectory']}`",
        f"- Owner/source: `{manifest['owner']}` / `{manifest['sourceRef'] or 'not specified'}`",
        "",
        "## Classified Artifacts",
        "",
        "| Category | Count | Meaning |",
        "| --- | ---: | --- |",
    ]
    for category, count in sorted(category_counts.items()):
        lines.append(f"| `{category}` | {count} | {CATEGORY_DESCRIPTIONS[category]} |")

    lines.extend(["", "## Payload Role Hints", ""])
    if role_counts:
        for role, count in sorted(role_counts.items()):
            lines.append(f"- `{role}`: {count}")
    else:
        lines.append("- No firmware payload role hints were detected.")

    lines.extend(["", "## Missing / Needs Owner Input", ""])
    if missing:
        for item in missing:
            lines.append(f"- {item}")
    else:
        lines.append("- No required document family is obviously missing for this mode.")

    lines.extend(
        [
            "",
            "## Promotion Checklist",
            "",
            "- Confirm source/provenance, owner approval, and confidentiality class.",
            "- Convert all legacy inclusive ranges into half-open `[start, end)` ranges.",
            "- Update `docs/references/ic-flashmap/SOURCE_MANIFEST.json` for promoted reference documents.",
            "- Update workflow-specific golden manifests only for owner-approved firmware payloads.",
            "- Update profile/catalog/C# code in a separate reviewed implementation change.",
            "",
            "## Verification",
            "",
            "```text",
            "python scripts/verify.py --structure-only",
            "```",
        ]
    )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_ai_prompt(path: Path, manifest: dict[str, Any]) -> None:
    missing = manifest["coverage"]["missingDocumentFamilies"]
    lines = [
        f"# AI Follow-up Prompt: {manifest['ic']} {manifest['mode']}",
        "",
        "Use the staged reference intake output below to prepare the next reviewed change.",
        "",
        f"- Intake output: `{manifest['outputDirectory']}`",
        f"- Manifest: `{manifest['outputDirectory']}/handoff_manifest.json`",
        f"- IC: `{manifest['ic']}`",
        f"- Mode: `{manifest['mode']}`",
        f"- Case: `{manifest['case'] or 'none'}`",
        "",
        "Constraints:",
        "",
        "- Do not commit private firmware BINs, archives, tool binaries, or generated outputs.",
        "- Do not implement one-off firmware semantics in UI/CLI scripts.",
        "- Promote reference docs only with manifest provenance and owner approval.",
        "- Treat range, CRC/header, postbuild, processor write range, and golden promotion as firmware-owner-gated.",
        "",
        "Missing items to ask the owner for:",
    ]
    if missing:
        lines.extend(f"- {item}" for item in missing)
    else:
        lines.append("- None obvious from the file-family scan; still verify roles and provenance manually.")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def validate_output_location(output_dir: Path, dry_run: bool) -> None:
    if not dry_run and output_dir.exists():
        raise FileExistsError(f"output directory already exists; choose another --run-id: {display_path(output_dir)}")


def main() -> int:
    args = parse_args()
    try:
        if args.request is not None:
            return stage_manifest_request(args)
        if args.source is None:
            raise ValueError("--source is required when --request is not used")
        if args.source_root is not None or args.output_dir is not None:
            raise ValueError("--source-root and --output-dir are available only with --request")
        if args.ic is None:
            raise ValueError("--ic is required when --source is used")
        source_dir = resolve_existing_directory(args.source)
        ic, ic_slug = normalize_ic(args.ic)
        mode = args.mode or "reference-only"
        run_id = args.run_id or now_run_id()
        output_root = (args.output_root or DEFAULT_OUTPUT_ROOT).expanduser().resolve()
        output_dir = build_output_dir(output_root, mode, ic_slug, args.case, run_id)
        validate_output_location(output_dir, args.dry_run)

        files = iter_source_files(source_dir)
        artifacts = [make_artifact(path, source_dir, output_dir, args.dry_run) for path in files]
        manifest = build_manifest(
            source_dir=source_dir,
            output_dir=output_dir,
            ic=ic,
            mode=mode,
            case=args.case,
            owner=args.owner or "owner",
            source_ref=args.source_ref or "",
            run_id=run_id,
            artifacts=artifacts,
        )

        if args.dry_run:
            print(json.dumps(manifest, indent=2, ensure_ascii=False))
        else:
            output_dir.mkdir(parents=True, exist_ok=True)
            write_json(output_dir / "handoff_manifest.json", manifest)
            write_next_steps(output_dir / "NEXT_STEPS.md", manifest)
            write_ai_prompt(output_dir / "AI_PROMPT.md", manifest)
            print(f"Staged {len(artifacts)} artifact(s) in {display_path(output_dir)}")
            missing = manifest["coverage"]["missingDocumentFamilies"]
            if missing:
                print("Missing document families:")
                for item in missing:
                    print(f"- {item}")
            else:
                print("No required document family is obviously missing for this mode.")
        return 0
    except (FileExistsError, FileNotFoundError, OSError, ValueError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

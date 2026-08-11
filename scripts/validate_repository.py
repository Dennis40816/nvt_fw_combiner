"""Validate repository structure, contracts, policies, and reference provenance."""

from __future__ import annotations

import ast
import hashlib
import json
import re
import subprocess
import sys
import tomllib
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Any, Iterable
from urllib.parse import unquote

from ab_merge_fixture_validation import validate_ab_merge_golden_fixtures
from canonical_golden_validation import (
    validate_canonical_golden,
    validate_standard_merge_release_allowlist,
)
from code_size_policy import (
    is_physical_source_file,
    review_code_size_policy,
    validate_code_size_policy,
)
from coverage_configuration_policy import (
    is_approved_package_analyzer,
    is_approved_sdk_analyzer,
    validate_coverage_collector_pin,
    validate_coverage_exclusion_policy,
    validate_evaluated_test_coverage_collector,
    validate_restored_test_coverage_collector_version,
)
from coverage_policy import load_baseline
from diagnostic_golden_validation import validate_diagnostic_golden_separation
from external_tool_policy import (
    ALLOWED_EXTERNAL_TOOL_BINARY_PAYLOADS,
    APPROVED_EXTERNAL_TOOL_PACKAGE_PATHS,
    APPROVED_EXTERNAL_TOOL_REPOSITORY_PATHS,
    validate_external_tool_catalog,
    validate_repository_external_tool_manifests,
)
from repository_contract_validation import validate_v2_contract_model
from skill_metadata_validation import (
    parse_skill_metadata,
    validate_skill_metadata_fields,
)

ROOT = Path(__file__).resolve().parents[1]
REQUIRED_FILES = {
    "README.md",
    "LICENSE",
    "AGENTS.md",
    "SPEC.md",
    "CHANGELOG.md",
    "VERSION",
    "global.json",
    "Directory.Build.props",
    "Directory.Build.targets",
    "Directory.Packages.props",
    "NuGet.config",
    "NvtFwCombiner.slnx",
    "THIRD_PARTY_NOTICES.md",
    ".codex/config.toml",
    ".github/CODEOWNERS",
    ".github/dependabot.yml",
    ".github/pull_request_template.md",
    ".github/workflows/ci.yml",
    ".github/workflows/release.yml",
    ".agents/skills/manifest.json",
    "scripts/bootstrap.ps1",
    "scripts/bootstrap.sh",
    "scripts/install-dotnet.ps1",
    "scripts/install-dotnet.sh",
    "scripts/package.ps1",
    "scripts/polytail_check.py",
    "scripts/publish-github.ps1",
    "scripts/publish-github.sh",
    "scripts/validate_repository.py",
    "scripts/canonical_golden_validation.py",
    "scripts/code_size_policy.py",
    "scripts/coverage_policy.py",
    "scripts/diagnostic_golden_validation.py",
    "scripts/external_tool_policy.py",
    "scripts/repository_contract_validation.py",
    "scripts/verify_ctrlram_replace_fixture.py",
    "scripts/verify.py",
    "external-tools/README.md",
    "external-tools/catalog.json",
    "external-tools/legacy-combiner/README.md",
    "external-tools/legacy-combiner/1.13.0/manifest.json",
    "testdata/golden/canonical/manifest.json",
    "testdata/golden/release-standard-merge-v1.json",
    "testdata/diagnostics/golden-evidence/README.md",
    "testdata/diagnostics/golden-evidence/manifest.json",
    "docs/adr/0003-unified-composition-engine.md",
    "docs/adr/0004-orthogonal-experience-access-policy.md",
    "docs/adr/0005-replace-personas-and-general-mapping.md",
    "docs/adr/0006-external-combiner-tool-runner.md",
    "docs/adr/0007-dev0-contract-scope-and-region-model.md",
    "docs/adr/0015-canonical-firmware-map-and-compiled-composition.md",
    "docs/architecture/canonical-variable-model.md",
    "docs/architecture/experience-and-access-policy.md",
    "docs/architecture/external-combiner-tool-runner.md",
    "docs/architecture/integrity-processing-matrix.md",
    "docs/architecture/operation-order-and-overlap-policy.md",
    "docs/architecture/region-model.md",
    "docs/architecture/saved-rule-promotion.md",
    "docs/architecture/terminal-log-and-diagnostics.md",
    "docs/contracts/composition-profile-v1.schema.json",
    "docs/contracts/coverage-baseline-v1.json",
    "docs/contracts/coverage-baseline-v1.md",
    "docs/contracts/canonical-golden-manifest-v1.md",
    "docs/contracts/canonical-capability-policy-v1.json",
    "docs/contracts/canonical-capability-policy-v1.md",
    "docs/contracts/canonical-capability-policy-v1.schema.json",
    "docs/contracts/composition-profile-v2.md",
    "docs/contracts/composition-profile-v2.schema.json",
    "docs/contracts/composition-request-v1.schema.json",
    "docs/contracts/composition-report-v1.schema.json",
    "docs/contracts/crc-worker-v1.schema.json",
    "docs/contracts/external-combiner-tool-manifest-v1.schema.json",
    "docs/contracts/firmware-evidence-manifest-v1.md",
    "docs/contracts/firmware-evidence-manifest-v1.schema.json",
    "docs/contracts/firmware-family-v1.md",
    "docs/contracts/firmware-family-v1.schema.json",
    "docs/contracts/profile-bundle-v1.md",
    "docs/contracts/profile-bundle-v1.schema.json",
    "docs/contracts/region-v1.schema.json",
    "docs/contracts/release-manifest-v1.schema.json",
    "docs/contracts/saved-composition-rule-v1.schema.json",
    "docs/contracts/saved-composition-rule-v2.md",
    "docs/contracts/saved-composition-rule-v2.schema.json",
    "docs/governance/agent-skill-inventory.md",
    "docs/governance/agent-skill-routing.md",
    "docs/governance/development-execution-workflow.md",
    "docs/governance/development-tags.md",
    "docs/policies/polytail.md",
    "docs/references/verification-report.md",
    "docs/specs/dev0-contract-scope.md",
    "docs/ui/0.1.1-demo-interface-plan.md",
    "docs/ui/diagnostics-and-terminal-wireframe.md",
    "docs/ui/information-architecture.md",
    "docs/ui/merge-replace-wireframes.md",
    "docs/ui/viewmodel-boundaries.md",
    "refcode/README.md",
    "refcode/REFERENCE_MANIFEST.json",
    "refcode/gen_flash_bin_v2/SOURCE_MANIFEST.json",
    "refcode/ab_code_combiner/SOURCE_MANIFEST.json",
    "tools/crc-worker/pyproject.toml",
}


@dataclass(frozen=True)
class EvaluatedProjectItems:
    """Restored MSBuild items plus the SDK root that supplied implicit analyzers."""

    items: dict[str, list[dict[str, Any]]]
    msbuild_sdks_path: Path


EXPECTED_PROJECTS = {
    "src/NvtFwCombiner.Domain/NvtFwCombiner.Domain.csproj",
    "src/NvtFwCombiner.Contracts/NvtFwCombiner.Contracts.csproj",
    "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj",
    "src/NvtFwCombiner.Profiles/NvtFwCombiner.Profiles.csproj",
    "src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj",
    "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj",
    "src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj",
    "src/NvtFwCombiner.Desktop/NvtFwCombiner.Desktop.csproj",
    "src/NvtFwCombiner.Presentation.Avalonia/NvtFwCombiner.Presentation.Avalonia.csproj",
    "tests/NvtFwCombiner.Domain.Tests/NvtFwCombiner.Domain.Tests.csproj",
    "tests/NvtFwCombiner.Application.Tests/NvtFwCombiner.Application.Tests.csproj",
    "tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj",
    "tests/NvtFwCombiner.ProfileContract.Tests/NvtFwCombiner.ProfileContract.Tests.csproj",
    "tests/NvtFwCombiner.GoldenRegression.Tests/NvtFwCombiner.GoldenRegression.Tests.csproj",
    "tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj",
    "tests/NvtFwCombiner.Architecture.Tests/NvtFwCombiner.Architecture.Tests.csproj",
    "tests/NvtFwCombiner.TestSupport/NvtFwCombiner.TestSupport.csproj",
    "tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj",
}

EXPECTED_PROJECT_REFERENCES = {
    "src/NvtFwCombiner.Domain/NvtFwCombiner.Domain.csproj": set(),
    "src/NvtFwCombiner.Contracts/NvtFwCombiner.Contracts.csproj": set(),
    "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj": {
        "src/NvtFwCombiner.Domain/NvtFwCombiner.Domain.csproj",
        "src/NvtFwCombiner.Contracts/NvtFwCombiner.Contracts.csproj",
    },
    "src/NvtFwCombiner.Profiles/NvtFwCombiner.Profiles.csproj": {
        "src/NvtFwCombiner.Domain/NvtFwCombiner.Domain.csproj",
        "src/NvtFwCombiner.Contracts/NvtFwCombiner.Contracts.csproj",
    },
    "src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj": {
        "src/NvtFwCombiner.Domain/NvtFwCombiner.Domain.csproj",
        "src/NvtFwCombiner.Contracts/NvtFwCombiner.Contracts.csproj",
        "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj",
        "src/NvtFwCombiner.Profiles/NvtFwCombiner.Profiles.csproj",
    },
    "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj": {
        "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj",
        "src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj",
    },
    "src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj": {
        "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj"
    },
    "src/NvtFwCombiner.Desktop/NvtFwCombiner.Desktop.csproj": {
        "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj",
        "src/NvtFwCombiner.Presentation.Avalonia/NvtFwCombiner.Presentation.Avalonia.csproj",
    },
    "src/NvtFwCombiner.Presentation.Avalonia/NvtFwCombiner.Presentation.Avalonia.csproj": {
        "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj",
    },
    "tests/NvtFwCombiner.Domain.Tests/NvtFwCombiner.Domain.Tests.csproj": {
        "src/NvtFwCombiner.Domain/NvtFwCombiner.Domain.csproj"
    },
    "tests/NvtFwCombiner.Application.Tests/NvtFwCombiner.Application.Tests.csproj": {
        "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj",
        "src/NvtFwCombiner.Profiles/NvtFwCombiner.Profiles.csproj",
        "src/NvtFwCombiner.Contracts/NvtFwCombiner.Contracts.csproj",
        "tests/NvtFwCombiner.TestSupport/NvtFwCombiner.TestSupport.csproj",
    },
    "tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj": {
        "src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj",
        "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj",
        "src/NvtFwCombiner.Contracts/NvtFwCombiner.Contracts.csproj",
        "src/NvtFwCombiner.Domain/NvtFwCombiner.Domain.csproj",
        "tests/NvtFwCombiner.TestSupport/NvtFwCombiner.TestSupport.csproj",
    },
    "tests/NvtFwCombiner.ProfileContract.Tests/NvtFwCombiner.ProfileContract.Tests.csproj": {
        "src/NvtFwCombiner.Profiles/NvtFwCombiner.Profiles.csproj"
    },
    "tests/NvtFwCombiner.GoldenRegression.Tests/NvtFwCombiner.GoldenRegression.Tests.csproj": {
        "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj",
        "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj",
        "src/NvtFwCombiner.Domain/NvtFwCombiner.Domain.csproj",
        "src/NvtFwCombiner.Profiles/NvtFwCombiner.Profiles.csproj",
        "tests/NvtFwCombiner.TestSupport/NvtFwCombiner.TestSupport.csproj",
    },
    "tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj": {
        "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj",
        "src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj",
        "tests/NvtFwCombiner.TestSupport/NvtFwCombiner.TestSupport.csproj",
    },
    "tests/NvtFwCombiner.Architecture.Tests/NvtFwCombiner.Architecture.Tests.csproj": set(),
    "tests/NvtFwCombiner.TestSupport/NvtFwCombiner.TestSupport.csproj": {
        "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj",
        "src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj",
    },
    "tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj": {
        "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj",
        "src/NvtFwCombiner.Presentation.Avalonia/NvtFwCombiner.Presentation.Avalonia.csproj",
        "tests/NvtFwCombiner.TestSupport/NvtFwCombiner.TestSupport.csproj",
    },
}
EXPECTED_REFCODE_SNAPSHOTS = {"gen_flash_bin_v2", "ab_code_combiner"}
FORBIDDEN_SUFFIXES = {
    ".bin",
    ".exe",
    ".dll",
    ".pdb",
    ".pfx",
    ".p12",
    ".pem",
    ".key",
    ".pyc",
}
ALLOWED_GOLDEN_BIN_ROOTS = {
    PurePosixPath("testdata/golden/canonical"),
    PurePosixPath("testdata/golden/ctrlram-replace/fixtures"),
}
ALLOWED_EXECUTABLE_PAYLOADS = ALLOWED_EXTERNAL_TOOL_BINARY_PAYLOADS
FORBIDDEN_DIRECTORY_NAMES = {
    "__pycache__",
    ".pytest_cache",
    ".mypy_cache",
    ".ruff_cache",
    ".venv",
    "venv",
    "artifacts",
    "release",
    "bin",
    "obj",
}
FORBIDDEN_REFCODE_SUFFIXES = {".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs"}
SNAPSHOT_CODE_SUFFIXES = {".py", ".json", ".txt", ".bat"}
XML_SUFFIXES = {".csproj", ".props", ".targets", ".slnx", ".axaml", ".manifest"}
DOTNET_INSTALL_SCRIPTS_COMMIT = "cbd31355adcf0c63eaeff601fb2eaa5fd0778f2b"
FULL_ACTION_PIN = re.compile(r"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[0-9a-f]{40}$")
SEMVER = re.compile(
    r"(?:0|[1-9][0-9]*)(?:\.(?:0|[1-9][0-9]*)){2}(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?"
)


def _git_tracked_paths() -> list[Path] | None:
    if not (ROOT / ".git").exists():
        return None
    completed = subprocess.run(
        ["git", "ls-files", "-z"],
        cwd=ROOT,
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    if completed.returncode != 0:
        return None
    return [
        ROOT / item.decode("utf-8") for item in completed.stdout.split(b"\0") if item
    ]


def repository_files() -> list[Path]:
    tracked = _git_tracked_paths()
    if tracked is not None:
        return [path for path in tracked if path.is_file()]
    files: list[Path] = []
    for path in ROOT.rglob("*"):
        if not path.is_file():
            continue
        relative = path.relative_to(ROOT)
        if ".git" in relative.parts or any(
            part in FORBIDDEN_DIRECTORY_NAMES for part in relative.parts
        ):
            continue
        files.append(path)
    return files


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def git_blob_sha1(path: Path) -> str:
    content = path.read_bytes()
    digest = hashlib.sha1(usedforsecurity=False)
    digest.update(f"blob {len(content)}\0".encode("ascii"))
    digest.update(content)
    return digest.hexdigest()


def load_json(path: Path, errors: list[str]) -> Any | None:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        errors.append(f"invalid JSON {path.relative_to(ROOT)}: {exc}")
        return None


def validate_required_files(errors: list[str]) -> None:
    for relative in sorted(REQUIRED_FILES):
        if not (ROOT / relative).is_file():
            errors.append(f"missing required file: {relative}")


def validate_forbidden_tracked_content(
    files: Iterable[Path], errors: list[str]
) -> None:
    for path in files:
        relative = path.relative_to(ROOT)
        if any(part in FORBIDDEN_DIRECTORY_NAMES for part in relative.parts):
            errors.append(f"generated/cache path is tracked: {relative}")
        if path.suffix.lower() in FORBIDDEN_SUFFIXES and not is_allowed_binary_payload(
            relative
        ):
            errors.append(
                f"forbidden payload/generated/secret-like file is tracked: {relative}"
            )


def is_allowed_binary_payload(relative: Path) -> bool:
    normalized = PurePosixPath(relative.as_posix())
    return is_allowed_golden_bin(relative) or normalized in ALLOWED_EXECUTABLE_PAYLOADS


def is_allowed_golden_bin(relative: Path) -> bool:
    normalized = PurePosixPath(relative.as_posix())
    return normalized.suffix.lower() == ".bin" and any(
        normalized == root or root in normalized.parents
        for root in ALLOWED_GOLDEN_BIN_ROOTS
    )


def validate_structured_files(files: Iterable[Path], errors: list[str]) -> None:
    for path in files:
        suffix = path.suffix.lower()
        if suffix == ".json":
            document = load_json(path, errors)
            if document is not None and path.name.endswith(".schema.json"):
                try:
                    from jsonschema import Draft202012Validator
                except ImportError:
                    continue
                try:
                    Draft202012Validator.check_schema(document)
                except Exception as exc:
                    errors.append(
                        f"invalid JSON Schema {path.relative_to(ROOT)}: {exc}"
                    )
        elif suffix == ".toml":
            try:
                tomllib.loads(path.read_text(encoding="utf-8"))
            except (OSError, UnicodeDecodeError, tomllib.TOMLDecodeError) as exc:
                errors.append(f"invalid TOML {path.relative_to(ROOT)}: {exc}")
        elif suffix in XML_SUFFIXES:
            try:
                ET.parse(path)
            except (OSError, ET.ParseError) as exc:
                errors.append(f"invalid XML {path.relative_to(ROOT)}: {exc}")


def validate_canonical_capability_policy_contract(errors: list[str]) -> None:
    policy_path = ROOT / "docs/contracts/canonical-capability-policy-v1.json"
    schema_path = ROOT / "docs/contracts/canonical-capability-policy-v1.schema.json"
    policy = load_json(policy_path, errors)
    schema = load_json(schema_path, errors)
    if policy is None or schema is None:
        return
    try:
        from jsonschema import Draft202012Validator
    except ImportError:
        # Keep the dependency policy aligned with validate_structured_files: clean
        # repository verification does not require the optional jsonschema package.
        # The schema and instance are still parsed as JSON here, while focused .NET
        # contract tests validate the runtime publication-policy semantics.
        return
    for finding in Draft202012Validator(schema).iter_errors(policy):
        location = "/".join(str(part) for part in finding.absolute_path) or "<root>"
        errors.append(f"canonical capability policy schema error at {location}: {finding.message}")


def validate_python_syntax(files: Iterable[Path], errors: list[str]) -> None:
    for path in files:
        if path.suffix.lower() != ".py":
            continue
        try:
            ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
        except (OSError, UnicodeDecodeError, SyntaxError) as exc:
            errors.append(f"invalid Python {path.relative_to(ROOT)}: {exc}")


def validate_markdown_links(files: Iterable[Path], errors: list[str]) -> None:
    link_pattern = re.compile(r"(?<!!)\[[^\]]*\]\(([^)]+)\)")
    for path in files:
        if path.suffix.lower() != ".md":
            continue
        for raw_target in link_pattern.findall(path.read_text(encoding="utf-8")):
            target = raw_target.strip()
            if target.startswith("<") and ">" in target:
                target = target[1 : target.index(">")]
            elif ' "' in target:
                target = target.split(' "', 1)[0]
            target = unquote(target.split("#", 1)[0].split("?", 1)[0])
            if not target or target.startswith(
                ("http://", "https://", "mailto:", "sandbox:")
            ):
                continue
            candidate = (path.parent / target).resolve()
            try:
                candidate.relative_to(ROOT.resolve())
            except ValueError:
                errors.append(
                    f"Markdown link escapes repository in {path.relative_to(ROOT)}: {target}"
                )
                continue
            if not candidate.exists():
                errors.append(
                    f"broken local Markdown link in {path.relative_to(ROOT)}: {target}"
                )


def load_skill_manifest(errors: list[str]) -> list[dict[str, Any]]:
    manifest_path = ROOT / ".agents" / "skills" / "manifest.json"
    document = load_json(manifest_path, errors)
    if not isinstance(document, dict) or document.get("schemaVersion") != 1:
        errors.append("skill manifest must be an object with schemaVersion 1")
        return []
    skills = document.get("skills")
    if not isinstance(skills, list):
        errors.append("skill manifest skills must be an array")
        return []

    required_fields = {
        "name",
        "status",
        "scope",
        "invocation",
        "authority",
        "owner",
        "replaces",
    }
    result: list[dict[str, Any]] = []
    names: set[str] = set()
    for index, entry in enumerate(skills):
        label = f"skill manifest skills[{index}]"
        if not isinstance(entry, dict) or set(entry) != required_fields:
            errors.append(f"{label} must contain exactly {sorted(required_fields)}")
            continue
        name = entry.get("name")
        if (
            not isinstance(name, str)
            or re.fullmatch(r"[a-z0-9]+(?:-[a-z0-9]+)*", name) is None
        ):
            errors.append(f"{label}.name must be lowercase hyphen-case")
            continue
        if name in names:
            errors.append(f"skill manifest contains duplicate name: {name}")
            continue
        names.add(name)
        if entry.get("status") != "active":
            errors.append(f"{label}.status must be active")
        if entry.get("scope") != "repo":
            errors.append(f"{label}.scope must be repo")
        if entry.get("invocation") not in {"implicit", "explicit"}:
            errors.append(f"{label}.invocation must be implicit or explicit")
        for field in ("authority", "owner"):
            if not isinstance(entry.get(field), str) or not entry[field].strip():
                errors.append(f"{label}.{field} must be a non-empty string")
        replaces = entry.get("replaces")
        if (
            not isinstance(replaces, list)
            or any(not isinstance(value, str) or not value for value in replaces)
            or len(replaces) != len(set(replaces))
        ):
            errors.append(f"{label}.replaces must be an array of unique names")
        result.append(entry)

    if [entry["name"] for entry in result] != sorted(names):
        errors.append("skill manifest entries must be sorted by name")
    return result


def render_skill_inventory(entries: list[dict[str, Any]]) -> str:
    lines = [
        "# Agent Skill Inventory",
        "",
        "Status: Generated from `.agents/skills/manifest.json`; do not edit the table manually.",
        "",
        "Repository validation checks this table, every active skill directory,",
        "frontmatter, Codex metadata, and invocation policy against the manifest.",
        "Removed generic workflows remain available from Git history or user-level",
        "skills; they are not repository authority.",
        "",
        "| Skill | Invocation | Authority | Replaces |",
        "| --- | --- | --- | --- |",
    ]
    for entry in entries:
        replaces = ", ".join(entry["replaces"]) or "—"
        lines.append(
            f"| `{entry['name']}` | {entry['invocation']} | "
            f"{entry['authority']} | {replaces} |"
        )
    return "\n".join(lines) + "\n"


def validate_skills(errors: list[str]) -> None:
    manifest_entries = load_skill_manifest(errors)
    expected_skills = {entry["name"] for entry in manifest_entries}
    explicit_skills = {
        entry["name"] for entry in manifest_entries if entry["invocation"] == "explicit"
    }
    found: set[str] = set()
    for path in sorted((ROOT / ".agents" / "skills").glob("*/SKILL.md")):
        text = path.read_text(encoding="utf-8")
        if not text.startswith("---\n"):
            errors.append(
                f"skill frontmatter must start at byte zero: {path.relative_to(ROOT)}"
            )
            continue
        parts = text.split("---\n", 2)
        if len(parts) < 3:
            errors.append(f"skill frontmatter is not closed: {path.relative_to(ROOT)}")
            continue
        header = parts[1]
        header_keys: list[str] = []
        for line in header.splitlines():
            if not line.strip():
                continue
            key_match = re.match(r"^([A-Za-z0-9_-]+):", line)
            if key_match is None:
                errors.append(
                    f"invalid skill frontmatter line in {path.relative_to(ROOT)}: {line}"
                )
                continue
            header_keys.append(key_match.group(1))
        if header_keys != ["name", "description"]:
            errors.append(
                "skill frontmatter keys must be exactly name, description in "
                f"{path.relative_to(ROOT)}: got {header_keys}"
            )
        name_match = re.search(r"(?m)^name:\s*([^\s]+)\s*$", header)
        description_match = re.search(r"(?m)^description:\s*(.+?)\s*$", header)
        if name_match is None or description_match is None:
            errors.append(
                f"skill requires name and description: {path.relative_to(ROOT)}"
            )
            continue
        name = name_match.group(1)
        found.add(name)
        if name != path.parent.name:
            errors.append(f"skill name/directory mismatch: {path.relative_to(ROOT)}")
        if len(description_match.group(1).strip()) < 20:
            errors.append(f"skill description is too vague: {path.relative_to(ROOT)}")
        metadata_path = path.parent / "agents" / "openai.yaml"
        if not metadata_path.is_file():
            errors.append(
                f"skill requires agents/openai.yaml: {path.relative_to(ROOT)}"
            )
            continue
        metadata = parse_skill_metadata(metadata_path, ROOT, errors)
        if metadata is None:
            continue
        validate_skill_metadata_fields(metadata, metadata_path, ROOT, name, errors)
        implicit_disabled = metadata["policy"].get("allow_implicit_invocation") is False
        if name in explicit_skills and not implicit_disabled:
            errors.append(
                "explicit skill must set allow_implicit_invocation: false: "
                f"{metadata_path.relative_to(ROOT)}"
            )
        if name not in explicit_skills and implicit_disabled:
            errors.append(
                "implicit skill must not disable implicit invocation: "
                f"{metadata_path.relative_to(ROOT)}"
            )
    if found != expected_skills:
        errors.append(
            f"repository skills must match manifest {sorted(expected_skills)}, got {sorted(found)}"
        )
    inventory_path = ROOT / "docs" / "governance" / "agent-skill-inventory.md"
    if inventory_path.is_file():
        expected_inventory = render_skill_inventory(manifest_entries)
        if inventory_path.read_text(encoding="utf-8") != expected_inventory:
            errors.append(
                "agent-skill-inventory.md must be rendered from manifest.json"
            )


def validate_source_manifest(manifest_path: Path, errors: list[str]) -> None:
    document = load_json(manifest_path, errors)
    if not isinstance(document, dict):
        return
    included = document.get("included")
    if not isinstance(included, list) or not included:
        errors.append(
            f"source manifest has no included entries: {manifest_path.relative_to(ROOT)}"
        )
        return
    snapshot_root = manifest_path.parent.resolve()
    listed_paths: set[str] = set()
    for index, entry in enumerate(included):
        if not isinstance(entry, dict):
            errors.append(
                f"invalid included[{index}] in {manifest_path.relative_to(ROOT)}"
            )
            continue
        relative = entry.get("path")
        expected_hash = entry.get("sha256")
        if not isinstance(relative, str) or not isinstance(expected_hash, str):
            errors.append(
                f"invalid path/hash in {manifest_path.relative_to(ROOT)} included[{index}]"
            )
            continue
        pure_path = PurePosixPath(relative)
        if pure_path.is_absolute() or ".." in pure_path.parts:
            errors.append(
                f"unsafe source-manifest path: {manifest_path.parent.name}/{relative}"
            )
            continue
        candidate = (manifest_path.parent / Path(*pure_path.parts)).resolve()
        if snapshot_root not in candidate.parents:
            errors.append(f"source-manifest path escapes snapshot: {relative}")
            continue
        listed_paths.add(pure_path.as_posix())
        if not candidate.is_file():
            errors.append(
                f"source-manifest file missing: {candidate.relative_to(ROOT)}"
            )
            continue
        if sha256(candidate) != expected_hash.lower():
            errors.append(f"reference hash drift: {candidate.relative_to(ROOT)}")
        repository_blob_sha = entry.get("repositoryBlobSha")
        if (
            repository_blob_sha is not None
            and git_blob_sha1(candidate) != repository_blob_sha
        ):
            errors.append(f"Git blob provenance drift: {candidate.relative_to(ROOT)}")
    for candidate in manifest_path.parent.rglob("*"):
        if not candidate.is_file() or candidate.name in {
            "SOURCE_MANIFEST.json",
            "README.md",
            "README.txt",
        }:
            continue
        relative = candidate.relative_to(manifest_path.parent).as_posix()
        if (
            candidate.suffix.lower() in SNAPSHOT_CODE_SUFFIXES
            and relative not in listed_paths
        ):
            errors.append(
                f"unlisted reference source file: {candidate.relative_to(ROOT)}"
            )


def validate_ctrlram_replace_golden_fixtures(errors: list[str]) -> None:
    manifest_path = ROOT / "testdata/golden/ctrlram-replace/manifest.json"
    from verify_ctrlram_replace_fixture import verify_fixture_manifest

    try:
        verify_fixture_manifest(manifest_path)
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        errors.append(f"ctrlram-replace golden manifest contract invalid: {exc}")
    manifest = load_json(manifest_path, errors)
    if not isinstance(manifest, dict):
        return

    if manifest.get("payloadClass") != "owner-approved-golden-firmware":
        errors.append(
            "ctrlram-replace golden manifest must declare owner-approved-golden-firmware payloadClass"
        )
    if manifest.get("binaryPayloadsIncluded") is not True:
        errors.append(
            "ctrlram-replace golden manifest must explicitly include binaryPayloadsIncluded=true"
        )

    golden_root = manifest_path.parent
    declared_bins: set[PurePosixPath] = set()
    cases = manifest.get("cases")
    if not isinstance(cases, list) or not cases:
        errors.append("ctrlram-replace golden manifest must contain cases")
        return

    for index, item in enumerate(cases):
        if not isinstance(item, dict):
            errors.append(f"invalid ctrlram-replace golden case[{index}]")
            continue

        base = item.get("base")
        if isinstance(base, dict):
            relative = validate_golden_manifest_entry(
                golden_root, base, errors, require_bin=True, label="ctrlram-replace"
            )
            if relative is not None:
                declared_bins.add(relative)
        else:
            errors.append(f"ctrlram-replace golden case[{index}] has no base")

        replacement_inputs = item.get("replacementInputs")
        if not isinstance(replacement_inputs, list) or not replacement_inputs:
            errors.append(
                f"ctrlram-replace golden case[{index}] has no replacementInputs"
            )
            continue

        for replacement_index, replacement in enumerate(replacement_inputs):
            if not isinstance(replacement, dict):
                errors.append(
                    f"invalid ctrlram-replace golden case[{index}].replacementInputs[{replacement_index}]"
                )
                continue
            file_entry = replacement.get("file")
            if isinstance(file_entry, dict):
                relative = validate_golden_manifest_entry(
                    golden_root,
                    file_entry,
                    errors,
                    require_bin=True,
                    label="ctrlram-replace",
                )
                if relative is not None:
                    declared_bins.add(relative)
            else:
                errors.append(
                    f"ctrlram-replace golden case[{index}].replacementInputs[{replacement_index}] has no file"
                )

        expected = item.get("expectedOutput")
        if isinstance(expected, dict):
            relative = validate_golden_manifest_entry(
                golden_root,
                expected,
                errors,
                require_bin=True,
                label="ctrlram-replace",
            )
            if relative is not None:
                declared_bins.add(relative)

    actual_bins = {
        PurePosixPath(path.relative_to(golden_root).as_posix())
        for path in golden_root.rglob("*.bin")
        if path.is_file()
        and not path.is_relative_to(golden_root / "fixtures/20260717")
        and not path.is_relative_to(golden_root / "fixtures/20260718")
    }
    if actual_bins != declared_bins:
        errors.append(
            "ctrlram-replace golden BIN manifest drift: "
            f"expected={sorted(path.as_posix() for path in declared_bins)} "
            f"actual={sorted(path.as_posix() for path in actual_bins)}"
        )


def validate_golden_manifest_entry(
    golden_root: Path,
    entry: dict[str, Any],
    errors: list[str],
    *,
    require_bin: bool,
    label: str,
) -> PurePosixPath | None:
    relative_text = entry.get("path")
    expected_size = entry.get("size")
    expected_hash = entry.get("sha256")
    if (
        not isinstance(relative_text, str)
        or not isinstance(expected_size, int)
        or not isinstance(expected_hash, str)
    ):
        errors.append(f"invalid {label} golden manifest file entry")
        return None

    relative = PurePosixPath(relative_text)
    if relative.is_absolute() or ".." in relative.parts:
        errors.append(f"unsafe {label} golden manifest path: {relative_text}")
        return None
    if require_bin and relative.suffix.lower() != ".bin":
        errors.append(f"{label} golden payload is not a BIN file: {relative_text}")
        return None

    candidate = (golden_root / Path(*relative.parts)).resolve()
    try:
        candidate.relative_to(golden_root.resolve())
    except ValueError:
        errors.append(
            f"{label} golden manifest path escapes fixture root: {relative_text}"
        )
        return None
    if not candidate.is_file():
        errors.append(
            f"{label} golden manifest file missing: {candidate.relative_to(ROOT)}"
        )
        return relative
    if candidate.stat().st_size != expected_size:
        errors.append(f"{label} golden size drift: {candidate.relative_to(ROOT)}")
    if sha256(candidate) != expected_hash.lower():
        errors.append(f"{label} golden hash drift: {candidate.relative_to(ROOT)}")
    return relative


def validate_refcode(errors: list[str]) -> None:
    refcode_root = ROOT / "refcode"
    snapshot_dirs = {path.name for path in refcode_root.iterdir() if path.is_dir()}
    if snapshot_dirs != EXPECTED_REFCODE_SNAPSHOTS:
        errors.append(
            f"refcode top-level snapshots must be exactly {sorted(EXPECTED_REFCODE_SNAPSHOTS)}, got {sorted(snapshot_dirs)}"
        )
    for path in refcode_root.rglob("*"):
        if path.is_file() and path.suffix.lower() in FORBIDDEN_REFCODE_SUFFIXES:
            errors.append(
                f"TypeScript/JavaScript is forbidden in refcode: {path.relative_to(ROOT)}"
            )
    manifest = load_json(refcode_root / "REFERENCE_MANIFEST.json", errors)
    if isinstance(manifest, dict):
        policy = manifest.get("policy")
        allowed = (
            policy.get("allowedTopLevelCodeSnapshots")
            if isinstance(policy, dict)
            else None
        )
        if not isinstance(allowed, list) or set(allowed) != EXPECTED_REFCODE_SNAPSHOTS:
            errors.append(
                "REFERENCE_MANIFEST allowedTopLevelCodeSnapshots is inconsistent"
            )
        if (
            not isinstance(policy, dict)
            or policy.get("typescriptSnapshotAllowed") is not False
        ):
            errors.append(
                "REFERENCE_MANIFEST must explicitly forbid TypeScript snapshots"
            )
    for snapshot in EXPECTED_REFCODE_SNAPSHOTS:
        validate_source_manifest(
            refcode_root / snapshot / "SOURCE_MANIFEST.json", errors
        )


def validate_version_license_and_sdk(errors: list[str]) -> None:
    version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    if SEMVER.fullmatch(version) is None:
        errors.append(f"invalid VERSION value: {version!r}")
    spec = (ROOT / "SPEC.md").read_text(encoding="utf-8")
    report = (ROOT / "docs/references/verification-report.md").read_text(
        encoding="utf-8"
    )
    changelog = (ROOT / "CHANGELOG.md").read_text(encoding="utf-8")
    tags = (ROOT / "docs/governance/development-tags.md").read_text(encoding="utf-8")
    build_props = (ROOT / "Directory.Build.props").read_text(encoding="utf-8")
    if f"文件版本：`{version}`" not in spec:
        errors.append("VERSION and SPEC.md document version disagree")
    if f"Specification package version: `{version}`" not in report:
        errors.append("VERSION and verification-report version disagree")
    if f"## [{version}]" not in changelog:
        errors.append("VERSION has no changelog section")
    if f"v{version}" not in tags:
        errors.append("VERSION has no development-tag node")
    has_repository_version_file = (
        "<RepositoryVersionFile>$(MSBuildThisFileDirectory)VERSION</RepositoryVersionFile>"
        in build_props
    )
    has_version_file_read = "ReadAllText('$(RepositoryVersionFile)')" in build_props
    if not has_repository_version_file or not has_version_file_read:
        errors.append(
            "Directory.Build.props must derive product version metadata from VERSION"
        )
    if not (ROOT / "LICENSE").read_text(encoding="utf-8").startswith("MIT License"):
        errors.append("root LICENSE is not the MIT License")
    global_json = load_json(ROOT / "global.json", errors)
    sdk_version = (
        global_json.get("sdk", {}).get("version")
        if isinstance(global_json, dict)
        else None
    )
    if (
        not isinstance(sdk_version, str)
        or re.fullmatch(r"10\.0\.[0-9]+", sdk_version) is None
    ):
        errors.append(f"global.json must pin a stable .NET 10 SDK, got {sdk_version!r}")
    for installer in ("scripts/install-dotnet.ps1", "scripts/install-dotnet.sh"):
        text = (ROOT / installer).read_text(encoding="utf-8")
        if "global.json" not in text or "dotnet-install" not in text:
            errors.append(
                f"{installer} must derive the SDK from global.json and use dotnet-install"
            )
        if DOTNET_INSTALL_SCRIPTS_COMMIT not in text:
            errors.append(
                f"{installer} must pin the approved dotnet/install-scripts commit"
            )
        if "raw.githubusercontent.com/dotnet/install-scripts" not in text:
            errors.append(
                f"{installer} must download from the official dotnet/install-scripts repository"
            )
        if "<auto>" not in text:
            errors.append(
                f"{installer} must document wrapper auto architecture handling"
            )


def normalize_project_reference(project: Path, include: str) -> str:
    return (
        (project.parent / include.replace("\\", "/"))
        .resolve()
        .relative_to(ROOT.resolve())
        .as_posix()
    )


def is_solution_test_project(relative: str) -> bool:
    """Classify solution test projects without trusting mutable project properties."""

    path = PurePosixPath(relative)
    return (
        bool(path.parts) and path.parts[0] == "tests" and path.stem.endswith(".Tests")
    )


def validate_production_source_ownership(
    relative: str, project_root: ET.Element, errors: list[str]
) -> None:
    """Keep production source physical, owned, and inside the measured tree."""

    if not relative.startswith("src/"):
        return
    for element in project_root.iter("Compile"):
        include = element.attrib.get("Include")
        if include:
            errors.append(
                "production project must not add an explicit Compile include "
                f"outside its owned source tree: {relative} -> {include}"
            )
    for element in project_root.iter("Analyzer"):
        include = element.attrib.get("Include", "<implicit>")
        errors.append(
            "production project must not add a source-generating analyzer without "
            f"an explicit architecture decision: {relative} -> {include}"
        )


def evaluate_project_items(
    project_path: Path, errors: list[str]
) -> EvaluatedProjectItems | None:
    """Read evaluated source and package items, including imported package targets."""

    assets_file = project_path.parent / "obj" / "project.assets.json"
    relative = project_path.relative_to(ROOT).as_posix()
    if not assets_file.is_file():
        errors.append(
            f"repository MSBuild evaluation requires restored assets: {relative}"
        )
        return None
    executable_name = "dotnet.exe" if sys.platform == "win32" else "dotnet"
    repository_dotnet = ROOT / ".dotnet" / executable_name
    dotnet = str(repository_dotnet) if repository_dotnet.is_file() else "dotnet"
    try:
        result = subprocess.run(
            [
                dotnet,
                "msbuild",
                str(project_path),
                "-nologo",
                "-property:Configuration=Release",
                "-target:ResolveLockFileAnalyzers",
                "-getProperty:MSBuildSDKsPath",
                "-getItem:Compile",
                "-getItem:Analyzer",
                "-getItem:PackageReference",
            ],
            cwd=ROOT,
            capture_output=True,
            text=True,
            check=False,
        )
    except OSError as exc:
        errors.append(
            f"could not start repository MSBuild evaluation for {relative}: {exc}"
        )
        return None
    if result.returncode != 0:
        detail = result.stderr.strip() or result.stdout.strip()
        errors.append(
            f"could not evaluate repository MSBuild items for {relative}: {detail}"
        )
        return None
    try:
        document = json.loads(result.stdout)
    except json.JSONDecodeError as exc:
        errors.append(
            "could not parse evaluated repository MSBuild items for "
            f"{relative}: {exc.msg}"
        )
        return None
    items = document.get("Items") if isinstance(document, dict) else None
    if not isinstance(items, dict):
        errors.append(f"evaluated repository MSBuild items are invalid for {relative}")
        return None
    properties = document.get("Properties")
    msbuild_sdks_path = (
        properties.get("MSBuildSDKsPath") if isinstance(properties, dict) else None
    )
    if not isinstance(msbuild_sdks_path, str) or not msbuild_sdks_path:
        errors.append(
            f"evaluated repository MSBuild SDK path is invalid for {relative}"
        )
        return None
    typed_items: dict[str, list[dict[str, Any]]] = {}
    for kind in ("Compile", "Analyzer", "PackageReference"):
        value = items.get(kind)
        if not isinstance(value, list) or not all(
            isinstance(item, dict) for item in value
        ):
            errors.append(
                f"evaluated repository MSBuild {kind} items are invalid for {relative}"
            )
            return None
        typed_items[kind] = value
    return EvaluatedProjectItems(typed_items, Path(msbuild_sdks_path))


def validate_evaluated_production_source_ownership(
    relative: str,
    project_directory: Path,
    items: dict[str, list[dict[str, Any]]],
    msbuild_sdks_path: Path,
    errors: list[str],
    repository_root: Path = ROOT,
) -> None:
    """Reject imported or packaged sources that escape the owned production tree."""

    if not relative.startswith("src/"):
        return
    owned_directory = project_directory.resolve()
    for compile_item in items["Compile"]:
        full_path = compile_item.get("FullPath")
        if not isinstance(full_path, str):
            errors.append(
                f"production project has an invalid evaluated Compile item: {relative}"
            )
            continue
        source_path = Path(full_path)
        if not is_physical_source_file(
            source_path,
            owned_directory,
            frozenset({".cs"}),
        ):
            errors.append(
                "production project must compile only physical C# inside its measured "
                f"source tree: {relative} -> {full_path}"
            )
    for analyzer in items["Analyzer"]:
        if is_approved_sdk_analyzer(
            analyzer, msbuild_sdks_path
        ) or is_approved_package_analyzer(analyzer, repository_root):
            continue
        identity = analyzer.get("Identity", "<implicit>")
        errors.append(
            "production project must not add an evaluated analyzer without an "
            f"explicit architecture decision: {relative} -> {identity}"
        )


def validate_evaluated_nonproduction_source_ownership(
    relative: str,
    items: dict[str, list[dict[str, Any]]],
    repository_root: Path,
    errors: list[str],
) -> None:
    """Reject duplicate compilation of measured production sources."""

    if relative.startswith("src/"):
        return
    production_root = (repository_root / "src").resolve()
    for compile_item in items["Compile"]:
        full_path = compile_item.get("FullPath")
        if not isinstance(full_path, str):
            errors.append(
                f"non-production project has an invalid evaluated Compile item: {relative}"
            )
            continue
        try:
            Path(full_path).resolve().relative_to(production_root)
        except ValueError:
            continue
        errors.append(
            "non-production project must not compile a duplicate production source: "
            f"{relative} -> {full_path}"
        )


def validate_restored_project_contracts(errors: list[str]) -> None:
    """Evaluate source ownership and test collectors after the .NET owner restores."""

    try:
        baseline = load_baseline(ROOT / "docs/contracts/coverage-baseline-v1.json")
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        errors.append(f"coverage baseline validation failed: {exc}")
        return
    collector = baseline["collection"]["dotnet"]["collector"]
    collector_version = baseline["collection"]["dotnet"]["version"]
    for relative in sorted(EXPECTED_PROJECT_REFERENCES):
        project_path = ROOT / relative
        is_test_project = is_solution_test_project(relative)
        evaluated = evaluate_project_items(project_path, errors)
        if evaluated is None:
            continue
        if relative.startswith("src/"):
            validate_evaluated_production_source_ownership(
                relative,
                project_path.parent,
                evaluated.items,
                evaluated.msbuild_sdks_path,
                errors,
                ROOT,
            )
        else:
            validate_evaluated_nonproduction_source_ownership(
                relative,
                evaluated.items,
                ROOT,
                errors,
            )
        if is_test_project:
            validate_evaluated_test_coverage_collector(
                relative,
                evaluated.items,
                collector,
                ROOT,
                errors,
            )
            validate_restored_test_coverage_collector_version(
                relative,
                project_path.parent / "obj" / "project.assets.json",
                collector,
                collector_version,
                errors,
            )


def validate_solution_and_dependencies(errors: list[str]) -> None:
    solution_root = ET.parse(ROOT / "NvtFwCombiner.slnx").getroot()
    solution_projects = {
        element.attrib["Path"].replace("\\", "/")
        for element in solution_root.findall("Project")
    }
    if solution_projects != EXPECTED_PROJECTS:
        errors.append(
            f"solution projects must be exactly {sorted(EXPECTED_PROJECTS)}, got {sorted(solution_projects)}"
        )
    for relative, expected_references in EXPECTED_PROJECT_REFERENCES.items():
        project_path = ROOT / relative
        root = ET.parse(project_path).getroot()
        actual = {
            normalize_project_reference(project_path, element.attrib["Include"])
            for element in root.iter("ProjectReference")
        }
        if actual != expected_references:
            errors.append(
                f"project reference drift in {relative}: expected={sorted(expected_references)} actual={sorted(actual)}"
            )
        for element in root.iter():
            include = element.attrib.get("Include", "")
            if "refcode" in include.lower():
                errors.append(
                    f"production/test project includes refcode: {relative} -> {include}"
                )
        validate_production_source_ownership(relative, root, errors)


def validate_contract_model(errors: list[str]) -> None:
    profile = load_json(
        ROOT / "docs/contracts/composition-profile-v1.schema.json", errors
    )
    report = load_json(
        ROOT / "docs/contracts/composition-report-v1.schema.json", errors
    )
    if isinstance(profile, dict):
        required = set(profile.get("required", []))
        for key in {"compositionKind", "experience", "image", "regions", "operations"}:
            if key not in required:
                errors.append(f"profile schema does not require canonical field: {key}")
        if "workflowFamily" in json.dumps(profile):
            errors.append(
                "profile schema must not contain closed workflowFamily semantics"
            )
    if isinstance(report, dict):
        required = set(report.get("required", []))
        for key in {
            "compositionKind",
            "experience",
            "imageInitialization",
            "mutations",
        }:
            if key not in required:
                errors.append(f"report schema does not require canonical field: {key}")
    validate_v2_contract_model(ROOT, load_json, errors)
    spec = (ROOT / "SPEC.md").read_text(encoding="utf-8")
    for term in {
        "dp-replace",
        "ctrlram-replace",
        "general-replace",
        "general-merge",
        "`unknown` 絕不等同 `none`",
        "host-created staging copy",
        "legacy `combiner.exe`",
    }:
        if term not in spec:
            errors.append(f"SPEC.md is missing required architecture term: {term}")


def validate_action_pins_in(path: Path, errors: list[str]) -> None:
    text = path.read_text(encoding="utf-8")
    for line_number, line in enumerate(text.splitlines(), 1):
        match = re.search(r"\buses:\s*([^\s#]+)", line)
        if match is None:
            continue
        reference = match.group(1).strip("'\"")
        if reference.startswith("./"):
            continue
        if FULL_ACTION_PIN.fullmatch(reference) is None:
            errors.append(
                f"third-party action is not pinned to a full SHA in {path.relative_to(ROOT)}:{line_number}: {reference}"
            )
    if "pull_request_target" in text:
        errors.append(f"pull_request_target is forbidden: {path.relative_to(ROOT)}")


def validate_workflows(errors: list[str]) -> None:
    for base in (ROOT / ".github/workflows", ROOT / "docs/ci/workflow-templates"):
        if base.is_dir():
            for path in sorted(base.glob("*.yml")):
                validate_action_pins_in(path, errors)
    ci = (ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8")
    for name in ("policy / polytail", "python-worker / verify", "dotnet / build-test"):
        if f"name: {name}" not in ci:
            errors.append(f"CI is missing required check name: {name}")
    if "scripts/install-dotnet.ps1" not in ci:
        errors.append("CI must exercise the repository .NET installer")
    if "python scripts/verify.py --skip-python --skip-structure" not in ci:
        errors.append("CI dotnet job must run the canonical .NET verifier")
    verifier = (ROOT / "scripts/verify.py").read_text(encoding="utf-8")
    required_dotnet_coverage_markers = (
        '            "test",',
        "            str(SOLUTION),",
        '            "--no-restore",\n            "--collect:XPlat Code Coverage",',
        '"--collect:XPlat Code Coverage",',
        '"--results-directory",',
    )
    if any(marker not in verifier for marker in required_dotnet_coverage_markers):
        errors.append(
            "canonical verifier must run the full .NET solution test suite "
            "with coverage collection"
        )
    if verifier.count('"--evaluated-source-ownership-only"') != 1:
        errors.append(
            "canonical .NET verifier must own exactly one restored source-ownership check"
        )
    dotnet_job = ci[ci.index("  dotnet:") :] if "  dotnet:" in ci else ""
    if "fetch-depth: 0" not in dotnet_job:
        errors.append("CI dotnet job must fetch the fixed coverage baseline revision")
    main_package = (ROOT / ".github/workflows/main-package.yml").read_text(
        encoding="utf-8"
    )
    if (
        "python ./scripts/verify.py --all" in main_package
        and "fetch-depth: 0" not in main_package
    ):
        errors.append(
            "main package workflow must fetch the fixed coverage baseline revision"
        )
    for marker in (
        "name: python-coverage",
        "path: artifacts/coverage/python/",
        "name: dotnet-coverage",
        "path: artifacts/coverage/dotnet/",
    ):
        if marker not in ci:
            errors.append(f"CI is missing coverage evidence marker: {marker}")
    if "verify_ctrlram_replace_fixture.py" not in verifier:
        errors.append(
            "canonical verifier must include the CtrlRAM Replace fixture gate"
        )
    release = (ROOT / ".github/workflows/release.yml").read_text(encoding="utf-8")
    required_release_markers = (
        "workflow_dispatch",
        "Exact reviewed release-branch head",
        "Final reviewed pull request",
        "NFC_WORKFLOW_REF -ne 'refs/heads/main'",
        "NFC_RELEASE_SOURCE_BRANCH -notin @('main', '0.9.17', '0.9.18', '0.9.19')",
        "'0.9.17' = '0.9.17'",
        "'0.9.18' = '0.9.18'",
        "'0.9.19' = '0.9.19'",
        "$approvedMaintenanceVersions[$env:NFC_SOURCE_BRANCH] -ne $version",
        "$env:NFC_RELEASE_POLICY validate-context",
        "$env:NFC_RELEASE_POLICY validate-promotion-source",
        "environment: release",
        "Create or verify immutable annotated tag",
    )
    if any(marker not in release for marker in required_release_markers):
        errors.append(
            "release workflow must use protected-main authority, an explicit release-source allowlist, and a protected human environment gate"
        )
    if "\n  promote:" in release and "\n  published-smoke:" in release:
        promote = release.split("\n  promote:", maxsplit=1)[1].split(
            "\n  published-smoke:", maxsplit=1
        )[0]
        if "Checkout prepared source" in promote or "smoke-release.ps1" in promote:
            errors.append(
                "release write-token job must not check out or execute release-source code"
            )
    else:
        errors.append(
            "release workflow must isolate published package smoke in a read-only job"
        )
    if (
        "Smoke published package without a GitHub token" not in release
        or "(Test-Path Env:GH_TOKEN) -or (Test-Path Env:GITHUB_TOKEN)" not in release
    ):
        errors.append(
            "published package smoke must fail closed when a GitHub token is exposed"
        )
    if "push:" in release and "tags:" in release:
        errors.append(
            "development tags must not automatically trigger the stable release workflow"
        )
    if "scripts/package.ps1" not in release:
        errors.append("release workflow does not call the closed-allowlist packager")


def validate_packaging_policy(files: Iterable[Path], errors: list[str]) -> None:
    tracked_external_tools = {
        PurePosixPath(path.relative_to(ROOT).as_posix())
        for path in files
        if path.relative_to(ROOT).parts[:1] == ("external-tools",)
    }
    if tracked_external_tools != APPROVED_EXTERNAL_TOOL_REPOSITORY_PATHS:
        errors.append(
            "tracked external-tools files differ from the approved repository inventory: "
            f"{', '.join(str(path) for path in sorted(tracked_external_tools))}"
        )

    validate_repository_external_tool_manifests(
        ROOT, APPROVED_EXTERNAL_TOOL_REPOSITORY_PATHS, errors
    )
    validate_external_tool_catalog(
        ROOT,
        APPROVED_EXTERNAL_TOOL_REPOSITORY_PATHS,
        APPROVED_EXTERNAL_TOOL_PACKAGE_PATHS,
        errors,
    )
    for script_name in ("package.ps1", "smoke-release.ps1"):
        text = (ROOT / "scripts" / script_name).read_text(encoding="utf-8")
        match = re.search(
            r"\$ApprovedExternalToolPackagePaths\s*=\s*@\((.*?)\)\s*\|\s*Sort-Object",
            text,
            flags=re.DOTALL,
        )
        if match is None:
            errors.append(
                f"{script_name} must declare a fixed ApprovedExternalToolPackagePaths allowlist"
            )
            continue

        declared_paths = {
            PurePosixPath(path) for path in re.findall(r"'([^']+)'", match.group(1))
        }
        if declared_paths != APPROVED_EXTERNAL_TOOL_PACKAGE_PATHS:
            errors.append(
                f"{script_name} external tool allowlist differs from the approved package paths: "
                f"{', '.join(str(path) for path in sorted(declared_paths))}"
            )

def validate_agent_files(errors: list[str]) -> None:
    if (ROOT / "AGENTS.md").stat().st_size > 16 * 1024:
        errors.append("root AGENTS.md exceeds 16 KiB")
    for relative in {
        "profiles/AGENTS.md",
        "testdata/golden/AGENTS.md",
        "src/NvtFwCombiner.Domain/AGENTS.md",
        "src/NvtFwCombiner.Application/AGENTS.md",
        "src/NvtFwCombiner.Infrastructure/AGENTS.md",
        "src/NvtFwCombiner.Profiles/AGENTS.md",
        "src/NvtFwCombiner.Presentation.Avalonia/AGENTS.md",
        "tools/crc-worker/AGENTS.md",
        "refcode/AGENTS.md",
    }:
        if not (ROOT / relative).is_file():
            errors.append(f"missing scoped AGENTS.md: {relative}")

    config = tomllib.loads(
        (ROOT / ".codex" / "config.toml").read_text(encoding="utf-8")
    )
    if config.get("agents") != {
        "enabled": True,
        "max_concurrent_threads_per_session": 3,
    }:
        errors.append(
            ".codex/config.toml must use only the approved global agents keys"
        )

    agents_root = ROOT / ".codex" / "agents"
    expected_agent_files = {
        "architect.toml",
        "evidence_reviewer.toml",
        "implementer.toml",
        "reviewer.toml",
    }
    found_agent_files = {path.name for path in agents_root.glob("*.toml")}
    if found_agent_files != expected_agent_files:
        errors.append(
            "standalone Codex agents must be exactly "
            f"{sorted(expected_agent_files)}, got {sorted(found_agent_files)}"
        )
    for name in expected_agent_files & found_agent_files:
        document = tomllib.loads((agents_root / name).read_text(encoding="utf-8"))
        for field in ("name", "description", "developer_instructions"):
            if not isinstance(document.get(field), str) or not document[field].strip():
                errors.append(f".codex/agents/{name} requires non-empty {field}")
        if document.get("agents") != {"enabled": False}:
            errors.append(f".codex/agents/{name} must disable nested agents")
    for read_only in ("architect.toml", "evidence_reviewer.toml", "reviewer.toml"):
        if read_only in found_agent_files:
            document = tomllib.loads(
                (agents_root / read_only).read_text(encoding="utf-8")
            )
            if document.get("sandbox_mode") != "read-only":
                errors.append(f".codex/agents/{read_only} must be read-only")


def validate() -> list[str]:
    errors: list[str] = []
    errors.extend(validate_code_size_policy(ROOT))
    files = repository_files()
    validate_required_files(errors)
    validate_forbidden_tracked_content(files, errors)
    validate_coverage_exclusion_policy(ROOT, files, errors)
    validate_structured_files(files, errors)
    validate_canonical_capability_policy_contract(errors)
    validate_python_syntax(files, errors)
    validate_markdown_links(files, errors)
    validate_ab_merge_golden_fixtures(
        ROOT, load_json, validate_golden_manifest_entry, errors
    )
    validate_canonical_golden(ROOT, errors)
    validate_diagnostic_golden_separation(ROOT, errors, files)
    validate_standard_merge_release_allowlist(ROOT, errors)
    validate_ctrlram_replace_golden_fixtures(errors)
    validate_skills(errors)
    validate_refcode(errors)
    validate_version_license_and_sdk(errors)
    validate_solution_and_dependencies(errors)
    validate_contract_model(errors)
    baseline: dict[str, Any] | None = None
    try:
        baseline = load_baseline(ROOT / "docs/contracts/coverage-baseline-v1.json")
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        errors.append(f"coverage baseline validation failed: {exc}")
    if baseline is not None:
        validate_coverage_collector_pin(baseline, errors, ROOT)
    validate_workflows(errors)
    validate_packaging_policy(files, errors)
    validate_agent_files(errors)
    return sorted(set(errors))


def main(arguments: list[str] | None = None) -> int:
    arguments = sys.argv[1:] if arguments is None else arguments
    if arguments:
        if arguments != ["--evaluated-source-ownership-only"]:
            print(
                "ERROR: unsupported repository validation arguments",
                file=sys.stderr,
            )
            return 2
        errors: list[str] = []
        validate_restored_project_contracts(errors)
        if errors:
            for error in sorted(set(errors)):
                print(f"ERROR: {error}", file=sys.stderr)
            return 1
        print(
            "Restored source ownership and test coverage collector validation passed."
        )
        return 0

    for finding in review_code_size_policy(ROOT):
        print(f"WARNING: {finding}", file=sys.stderr)
    errors = validate()
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 1
    print("Repository structure validation passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

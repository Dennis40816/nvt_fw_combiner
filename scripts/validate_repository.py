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

import yaml

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
from v0916_parity_certification import (
    GitAuthorityReader,
    ParityError,
    validate_repository_parity_authority_transfer,
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
    "scripts/package-distribution-launcher.ps1",
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
    "docs/adr/0054-finalize-capability-reuse-records.md",
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
    "docs/governance/capability-reuse-record.md",
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
    "src/NvtFwCombiner.VersionManagement.Application/NvtFwCombiner.VersionManagement.Application.csproj",
    "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj",
    "src/NvtFwCombiner.Profiles/NvtFwCombiner.Profiles.csproj",
    "src/NvtFwCombiner.Platform/NvtFwCombiner.Platform.csproj",
    "src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj",
    "src/NvtFwCombiner.VersionManagement.Infrastructure/NvtFwCombiner.VersionManagement.Infrastructure.csproj",
    "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj",
    "src/NvtFwCombiner.DistributionLauncher/NvtFwCombiner.DistributionLauncher.csproj",
    "src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj",
    "src/NvtFwCombiner.Desktop/NvtFwCombiner.Desktop.csproj",
    "src/NvtFwCombiner.Launcher/NvtFwCombiner.Launcher.csproj",
    "src/NvtFwCombiner.LauncherBootstrap/NvtFwCombiner.LauncherBootstrap.csproj",
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
    "tests/NvtFwCombiner.ReadyProbe/NvtFwCombiner.ReadyProbe.csproj",
}

EXPECTED_PROJECT_REFERENCES = {
    "src/NvtFwCombiner.Domain/NvtFwCombiner.Domain.csproj": set(),
    "src/NvtFwCombiner.Contracts/NvtFwCombiner.Contracts.csproj": set(),
    "src/NvtFwCombiner.VersionManagement.Application/NvtFwCombiner.VersionManagement.Application.csproj": {
        "src/NvtFwCombiner.Contracts/NvtFwCombiner.Contracts.csproj",
    },
    "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj": {
        "src/NvtFwCombiner.Domain/NvtFwCombiner.Domain.csproj",
        "src/NvtFwCombiner.Contracts/NvtFwCombiner.Contracts.csproj",
    },
    "src/NvtFwCombiner.Profiles/NvtFwCombiner.Profiles.csproj": {
        "src/NvtFwCombiner.Domain/NvtFwCombiner.Domain.csproj",
        "src/NvtFwCombiner.Contracts/NvtFwCombiner.Contracts.csproj",
    },
    "src/NvtFwCombiner.Platform/NvtFwCombiner.Platform.csproj": set(),
    "src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj": {
        "src/NvtFwCombiner.Domain/NvtFwCombiner.Domain.csproj",
        "src/NvtFwCombiner.Contracts/NvtFwCombiner.Contracts.csproj",
        "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj",
        "src/NvtFwCombiner.Profiles/NvtFwCombiner.Profiles.csproj",
        "src/NvtFwCombiner.Platform/NvtFwCombiner.Platform.csproj",
    },
    "src/NvtFwCombiner.VersionManagement.Infrastructure/NvtFwCombiner.VersionManagement.Infrastructure.csproj": {
        "src/NvtFwCombiner.Contracts/NvtFwCombiner.Contracts.csproj",
        "src/NvtFwCombiner.Platform/NvtFwCombiner.Platform.csproj",
        "src/NvtFwCombiner.VersionManagement.Application/NvtFwCombiner.VersionManagement.Application.csproj",
    },
    "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj": {
        "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj",
        "src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj",
        "src/NvtFwCombiner.VersionManagement.Application/NvtFwCombiner.VersionManagement.Application.csproj",
        "src/NvtFwCombiner.VersionManagement.Infrastructure/NvtFwCombiner.VersionManagement.Infrastructure.csproj",
    },
    "src/NvtFwCombiner.DistributionLauncher/NvtFwCombiner.DistributionLauncher.csproj": {
        "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj",
        "src/NvtFwCombiner.VersionManagement.Application/NvtFwCombiner.VersionManagement.Application.csproj",
    },
    "src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj": {
        "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj",
        "src/NvtFwCombiner.VersionManagement.Application/NvtFwCombiner.VersionManagement.Application.csproj",
    },
    "src/NvtFwCombiner.Desktop/NvtFwCombiner.Desktop.csproj": {
        "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj",
        "src/NvtFwCombiner.Presentation.Avalonia/NvtFwCombiner.Presentation.Avalonia.csproj",
        "src/NvtFwCombiner.VersionManagement.Application/NvtFwCombiner.VersionManagement.Application.csproj",
        "src/NvtFwCombiner.VersionManagement.Infrastructure/NvtFwCombiner.VersionManagement.Infrastructure.csproj",
    },
    "src/NvtFwCombiner.Launcher/NvtFwCombiner.Launcher.csproj": {
        "src/NvtFwCombiner.VersionManagement.Application/NvtFwCombiner.VersionManagement.Application.csproj",
        "src/NvtFwCombiner.VersionManagement.Infrastructure/NvtFwCombiner.VersionManagement.Infrastructure.csproj",
    },
    "src/NvtFwCombiner.LauncherBootstrap/NvtFwCombiner.LauncherBootstrap.csproj": {
        "src/NvtFwCombiner.VersionManagement.Application/NvtFwCombiner.VersionManagement.Application.csproj",
        "src/NvtFwCombiner.VersionManagement.Infrastructure/NvtFwCombiner.VersionManagement.Infrastructure.csproj",
    },
    "src/NvtFwCombiner.Presentation.Avalonia/NvtFwCombiner.Presentation.Avalonia.csproj": {
        "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj",
        "src/NvtFwCombiner.VersionManagement.Application/NvtFwCombiner.VersionManagement.Application.csproj",
    },
    "tests/NvtFwCombiner.Domain.Tests/NvtFwCombiner.Domain.Tests.csproj": {
        "src/NvtFwCombiner.Domain/NvtFwCombiner.Domain.csproj"
    },
    "tests/NvtFwCombiner.Application.Tests/NvtFwCombiner.Application.Tests.csproj": {
        "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj",
        "src/NvtFwCombiner.VersionManagement.Application/NvtFwCombiner.VersionManagement.Application.csproj",
        "src/NvtFwCombiner.Profiles/NvtFwCombiner.Profiles.csproj",
        "src/NvtFwCombiner.Contracts/NvtFwCombiner.Contracts.csproj",
        "tests/NvtFwCombiner.TestSupport/NvtFwCombiner.TestSupport.csproj",
    },
    "tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj": {
        "src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj",
        "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj",
        "src/NvtFwCombiner.LauncherBootstrap/NvtFwCombiner.LauncherBootstrap.csproj",
        "src/NvtFwCombiner.VersionManagement.Application/NvtFwCombiner.VersionManagement.Application.csproj",
        "src/NvtFwCombiner.VersionManagement.Infrastructure/NvtFwCombiner.VersionManagement.Infrastructure.csproj",
        "src/NvtFwCombiner.Contracts/NvtFwCombiner.Contracts.csproj",
        "src/NvtFwCombiner.Domain/NvtFwCombiner.Domain.csproj",
        "tests/NvtFwCombiner.ReadyProbe/NvtFwCombiner.ReadyProbe.csproj",
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
        "src/NvtFwCombiner.DistributionLauncher/NvtFwCombiner.DistributionLauncher.csproj",
        "src/NvtFwCombiner.VersionManagement.Application/NvtFwCombiner.VersionManagement.Application.csproj",
        "tests/NvtFwCombiner.TestSupport/NvtFwCombiner.TestSupport.csproj",
    },
    "tests/NvtFwCombiner.Architecture.Tests/NvtFwCombiner.Architecture.Tests.csproj": set(),
    "tests/NvtFwCombiner.TestSupport/NvtFwCombiner.TestSupport.csproj": {
        "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj",
        "src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj",
        "src/NvtFwCombiner.VersionManagement.Application/NvtFwCombiner.VersionManagement.Application.csproj",
    },
    "tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj": {
        "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj",
        "src/NvtFwCombiner.DistributionLauncher/NvtFwCombiner.DistributionLauncher.csproj",
        "src/NvtFwCombiner.Presentation.Avalonia/NvtFwCombiner.Presentation.Avalonia.csproj",
        "src/NvtFwCombiner.VersionManagement.Application/NvtFwCombiner.VersionManagement.Application.csproj",
        "tests/NvtFwCombiner.TestSupport/NvtFwCombiner.TestSupport.csproj",
    },
    "tests/NvtFwCombiner.ReadyProbe/NvtFwCombiner.ReadyProbe.csproj": {
        "src/NvtFwCombiner.VersionManagement.Application/NvtFwCombiner.VersionManagement.Application.csproj",
        "src/NvtFwCombiner.VersionManagement.Infrastructure/NvtFwCombiner.VersionManagement.Infrastructure.csproj",
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
        errors.append(
            f"canonical capability policy schema error at {location}: {finding.message}"
        )


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
    has_product_version_read = (
        "<ProductVersion>$([System.IO.File]::ReadAllText('$(RepositoryVersionFile)').Trim())</ProductVersion>"
        in build_props
    )
    has_stable_project_version = all(
        marker in build_props
        for marker in (
            "<InternalProjectVersion>1.0.0</InternalProjectVersion>",
            "<VersionPrefix>$(InternalProjectVersion)</VersionPrefix>",
            "<Version>$(InternalProjectVersion)</Version>",
            "<PackageVersion>$(InternalProjectVersion)</PackageVersion>",
            "<VersionCore>$([System.Text.RegularExpressions.Regex]::Replace('$(ProductVersion)'",
            "<InformationalVersion>$(ProductVersion)</InformationalVersion>",
        )
    )
    if (
        not has_repository_version_file
        or not has_product_version_read
        or not has_stable_project_version
    ):
        errors.append(
            "Directory.Build.props must derive product metadata from VERSION while "
            "keeping the internal project-reference version stable at 1.0.0"
        )
    for lock_path in sorted((ROOT / "src").rglob("packages.lock.json")) + sorted(
        (ROOT / "tests").rglob("packages.lock.json")
    ):
        lock = load_json(lock_path, errors)
        dependency_targets = lock.get("dependencies") if isinstance(lock, dict) else None
        if not isinstance(dependency_targets, dict):
            continue
        for target in dependency_targets.values():
            if not isinstance(target, dict):
                continue
            for dependency in target.values():
                if not isinstance(dependency, dict) or dependency.get("type") != "Project":
                    continue
                project_dependencies = dependency.get("dependencies", {})
                if not isinstance(project_dependencies, dict):
                    continue
                for name, constraint in project_dependencies.items():
                    if name.startswith("NvtFwCombiner.") and constraint != "[1.0.0, )":
                        errors.append(
                            f"{lock_path.relative_to(ROOT).as_posix()} has unstable "
                            f"project-reference constraint {name}={constraint!r}"
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


def _validate_windows_only_ci_topology(path: Path, errors: list[str]) -> None:
    path_label = (
        path.relative_to(ROOT).as_posix() if path.is_relative_to(ROOT) else path.name
    )
    expected_jobs = {
        "structure",
        "python-worker",
        "dotnet-build",
        "dotnet-test",
        "dotnet",
    }
    try:
        workflow = yaml.safe_load(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, yaml.YAMLError) as error:
        errors.append(f"cannot parse CI workflow {path_label}: {error}")
        return

    if not isinstance(workflow, dict) or not isinstance(workflow.get("jobs"), dict):
        errors.append(f"CI workflow must declare a parsed jobs mapping: {path_label}")
        return

    jobs = workflow["jobs"]
    if set(jobs) != expected_jobs:
        errors.append(
            f"CI workflow must retain exactly these jobs in {path_label}: "
            f"{', '.join(sorted(expected_jobs))}"
        )
        return

    for job_name in sorted(expected_jobs):
        job = jobs[job_name]
        runner = job.get("runs-on") if isinstance(job, dict) else None
        if not isinstance(runner, str) or runner != "windows-latest":
            errors.append(
                f"CI job {job_name} must use the scalar windows-latest runner in "
                f"{path_label}"
            )


def validate_workflows(errors: list[str]) -> None:
    for base in (ROOT / ".github/workflows", ROOT / "docs/ci/workflow-templates"):
        if base.is_dir():
            for path in sorted(base.glob("*.yml")):
                validate_action_pins_in(path, errors)
    _validate_windows_only_ci_topology(ROOT / ".github/workflows/ci.yml", errors)
    _validate_windows_only_ci_topology(
        ROOT / "docs/ci/workflow-templates/ci.yml", errors
    )
    ci = (ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8")
    for name in ("policy / polytail", "python-worker / verify", "dotnet / build-test"):
        if f"name: {name}" not in ci:
            errors.append(f"CI is missing required check name: {name}")
    if "scripts/install-dotnet.ps1" not in ci:
        errors.append("CI must exercise the repository .NET installer")
    required_ci_dotnet_modes = (
        "python scripts/verify.py --ci-dotnet-build",
        "python scripts/verify.py --ci-dotnet-test-shard ${{ matrix.shard }}",
        ("python scripts/verify.py --ci-dotnet-finalize artifacts/ci-dotnet-downloads"),
    )
    if any(mode not in ci for mode in required_ci_dotnet_modes):
        errors.append("CI .NET producers and finalizer must use the canonical verifier")
    required_ci_dotnet_topology = (
        "  dotnet-build:",
        "  dotnet-test:",
        "fail-fast: false",
        "shard: [bootstrap, ui, core]",
        "  dotnet:\n    name: dotnet / build-test\n    needs: [dotnet-build, dotnet-test]",
        "if: >-\n      always() &&",
        "pattern: dotnet-*-evidence",
        "path: artifacts/ci-dotnet-upload/",
        "path: artifacts/ci-dotnet-downloads/",
    )
    if any(marker not in ci for marker in required_ci_dotnet_topology):
        errors.append(
            "CI .NET topology must retain one build producer, three closed test "
            "shards, and the always-run stable finalizer"
        )
    if "merge-multiple: true" in ci:
        errors.append(
            "CI must preserve separate producer artifact roots until finalization"
        )
    if ".csproj" in ci:
        errors.append("CI workflow must not duplicate the canonical .NET project map")
    verifier = (ROOT / "scripts/verify.py").read_text(encoding="utf-8")
    required_dotnet_coverage_markers = (
        "CI_DOTNET_SHARDS",
        "def ci_dotnet_test_command(",
        '        "--no-restore",',
        '"--collect:XPlat Code Coverage",',
        '"--results-directory",',
        '"trx;LogFileName=test-results.trx",',
        "def finalize_ci_dotnet_evidence(",
    )
    if any(marker not in verifier for marker in required_dotnet_coverage_markers):
        errors.append(
            "canonical verifier must own the closed .NET shard map, unfiltered "
            "coverage/TRX collection, and evidence finalization"
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


CAPABILITY_REUSE_RECORD_FIELDS = {
    "schemaVersion",
    "taskId",
    "capability",
    "integrationBase",
    "risk",
    "kind",
    "state",
    "mutablePaths",
    "implementationOwner",
    "searchEvidence",
    "semanticOwner",
    "terminalContract",
    "disposition",
    "designReview",
    "implementationHead",
    "reviewedHead",
    "pathStateDigest",
    "finalReview",
}
CAPABILITY_REUSE_RISKS = {"R0", "R1", "R2", "R3"}
CAPABILITY_REUSE_KINDS = {"behavior", "refactor", "ui", "release", "governance"}
CAPABILITY_REUSE_STATES = {"design-active", "final-complete", "blocked"}
CAPABILITY_REUSE_DISPOSITIONS = {"reuse", "extend-owner", "reject-duplicate"}
CAPABILITY_REUSE_DESIGN_REVIEW_OUTCOMES = {
    "not-required",
    "approved",
    "findings-incorporated",
    "blocked",
}
CAPABILITY_REUSE_FINAL_REVIEW_OUTCOMES = {
    "pending",
    "approved",
    "findings-incorporated",
}
CAPABILITY_REUSE_TASK_ID = re.compile(r"[A-Z][A-Z0-9]*(?:-[A-Z0-9]+)+")
CAPABILITY_REUSE_CHANGE_RECORD_ROOT = PurePosixPath(
    "docs/governance/change-records"
)
CAPABILITY_REUSE_TRUSTED_CHECKPOINT_PATH = PurePosixPath(
    "docs/governance/trusted-initial-capability-checkpoint.v1.json"
)
CAPABILITY_REUSE_EXTERNAL_AUTHORITY_ROOT = PurePosixPath(
    "docs/governance/external-authority-attestations"
)
CAPABILITY_REUSE_TRUSTED_CHECKPOINT_FIELDS = {
    "schemaVersion",
    "checkpointId",
    "reviewedHead",
    "reviewedTree",
    "ownerDecisionRef",
    "legacyRecords",
    "openR3Authorities",
}
CAPABILITY_REUSE_LEGACY_RECORD_FIELDS = {
    "taskId",
    "path",
    "risk",
    "state",
    "contentSha256",
}
CAPABILITY_REUSE_OPEN_AUTHORITY_FIELDS = {
    "taskId",
    "authorityType",
    "status",
}
CAPABILITY_REUSE_EXTERNAL_ATTESTATION_FIELDS = {
    "schemaVersion",
    "taskId",
    "authorityType",
    "reviewedHead",
    "decision",
    "reviewer",
    "evidence",
}
CAPABILITY_REUSE_AUTHORITY_TYPES = {"firmware-owner", "release-owner"}
CAPABILITY_REUSE_INITIAL_FIRMWARE_OWNER_TASKS = {"FORMAL-SUPPORT-01"}
CAPABILITY_REUSE_RISK_LEVELS = {"R0": 0, "R1": 1, "R2": 2, "R3": 3}
CAPABILITY_REUSE_FINALIZED_FIELDS = {
    "state",
    "implementationHead",
    "reviewedHead",
    "pathStateDigest",
    "finalReview",
}
CAPABILITY_REUSE_R3_SCRIPTS = {
    "scripts/ab_merge_fixture_validation.py",
    "scripts/canonical_golden_validation.py",
    "scripts/create_candidate_ic_intake.py",
    "scripts/create_ctrlram_universal_sentinel.py",
    "scripts/create_update_catalog.py",
    "scripts/update_source_registry_policy.py",
    "scripts/diagnostic_golden_validation.py",
    "scripts/edit_update_source_registry.py",
    "scripts/external_tool_policy.py",
    "scripts/intake_ic_reference.py",
    "scripts/package.ps1",
    "scripts/package-distribution-launcher.ps1",
    "scripts/publish-github.ps1",
    "scripts/publish-github.sh",
    "scripts/release_promotion_policy.py",
    "scripts/render_release_notes.py",
    "scripts/sign-release.ps1",
    "scripts/sign-release.sh",
    "scripts/sign_release.py",
    "scripts/signing_policy.py",
    "scripts/smoke-release.ps1",
}


@dataclass(frozen=True)
class _CommittedCapabilityRecord:
    revision: str
    content: bytes
    value: dict[str, Any]


@dataclass(frozen=True)
class _CapabilityRecordHistory:
    first_active: _CommittedCapabilityRecord | None
    first_final: _CommittedCapabilityRecord | None
    latest_unfinalized_active: _CommittedCapabilityRecord | None
    admitted_field_violation: str | None


@dataclass(frozen=True)
class _TrustedCapabilityCheckpoint:
    activation_commit: str
    reviewed_head: str
    retired_records: dict[str, str]
    reserved_task_ids: frozenset[str]
    open_r3_authorities: dict[str, str]


def _is_capability_reuse_governed_path(relative: str) -> bool:
    path = PurePosixPath(relative)
    parts = path.parts
    if parts[:3] == CAPABILITY_REUSE_CHANGE_RECORD_ROOT.parts:
        return False
    if path == CAPABILITY_REUSE_TRUSTED_CHECKPOINT_PATH:
        return False
    if (
        path.parent == CAPABILITY_REUSE_EXTERNAL_AUTHORITY_ROOT
        and path.suffix == ".json"
    ):
        return False
    if path.name == "AGENTS.md":
        return True
    if parts[:2] == (".agents", "skills"):
        return True
    if parts[:2] in {
        ("docs", "governance"),
        ("docs", "policies"),
        ("docs", "adr"),
        ("docs", "specs"),
        ("docs", "contracts"),
        (".github", "workflows"),
        ("testdata", "golden"),
        ("tools", "crc-worker"),
    }:
        if parts[:2] == ("tools", "crc-worker") and any(
            part in {"tests", "cache", ".cache", ".pytest_cache", "__pycache__"}
            for part in parts[2:]
        ):
            return False
        return True
    if relative == "docs/ci/release-package.md":
        return True
    if parts and parts[0] in {"src", "profiles", "eng"}:
        return True
    return (
        len(parts) == 2
        and parts[0] == "scripts"
        and path.suffix.casefold() in {".py", ".ps1", ".sh"}
    )


def _capability_reuse_minimum_risk(relative: str) -> str:
    path = PurePosixPath(relative)
    parts = path.parts
    if path.name == "AGENTS.md":
        return "R2"
    if parts[:2] == (".agents", "skills"):
        return "R2"
    if parts[:2] in {
        ("docs", "governance"),
        ("docs", "policies"),
        ("docs", "adr"),
        ("docs", "specs"),
        ("docs", "contracts"),
    } or relative == "docs/ci/release-package.md":
        return "R2"
    if parts[:2] in {
        (".github", "workflows"),
        ("testdata", "golden"),
        ("tools", "crc-worker"),
    }:
        return "R3"
    if parts and parts[0] == "profiles":
        return "R3"
    if relative in CAPABILITY_REUSE_R3_SCRIPTS:
        return "R3"
    if parts and parts[0] in {"scripts", "eng"}:
        return "R2"
    return "R1"


def _git_paths(root: Path, arguments: list[str]) -> tuple[set[str], str | None]:
    result = subprocess.run(
        ["git", *arguments],
        cwd=root,
        check=False,
        capture_output=True,
    )
    if result.returncode != 0:
        detail = result.stderr.decode("utf-8", errors="replace").strip()
        return set(), detail or f"git exited {result.returncode}"
    return {
        value.decode("utf-8", errors="strict")
        for value in result.stdout.split(b"\0")
        if value
    }, None


def _parse_git_name_status(output: bytes) -> set[str]:
    values = [value.decode("utf-8", errors="strict") for value in output.split(b"\0") if value]
    paths: set[str] = set()
    index = 0
    while index < len(values):
        status = values[index]
        index += 1
        if not status or status[0] not in "ACDMRT":
            raise ValueError(f"unsupported git name-status entry: {status!r}")
        path_count = 2 if status[0] in {"R", "C"} else 1
        if index + path_count > len(values):
            raise ValueError(f"truncated git name-status entry: {status!r}")
        paths.update(values[index : index + path_count])
        index += path_count
    return paths


def _git_changed_paths(root: Path, integration_base: str) -> tuple[set[str], str | None]:
    result = subprocess.run(
        [
            "git",
            "diff",
            "--name-status",
            "-z",
            "--find-renames",
            integration_base,
            "--",
        ],
        cwd=root,
        check=False,
        capture_output=True,
    )
    if result.returncode != 0:
        detail = result.stderr.decode("utf-8", errors="replace").strip()
        return set(), detail or f"git exited {result.returncode}"
    try:
        return _parse_git_name_status(result.stdout), None
    except (UnicodeDecodeError, ValueError) as exc:
        return set(), str(exc)


def _git_revision_changed_paths(
    root: Path,
    older: str,
    newer: str,
) -> tuple[set[str], str | None]:
    result = subprocess.run(
        [
            "git",
            "diff",
            "--name-status",
            "-z",
            "--find-renames",
            older,
            newer,
            "--",
        ],
        cwd=root,
        check=False,
        capture_output=True,
    )
    if result.returncode != 0:
        detail = result.stderr.decode("utf-8", errors="replace").strip()
        return set(), detail or f"git exited {result.returncode}"
    try:
        return _parse_git_name_status(result.stdout), None
    except (UnicodeDecodeError, ValueError) as exc:
        return set(), str(exc)


def _git_object(root: Path, arguments: list[str]) -> tuple[bytes, str | None]:
    result = subprocess.run(
        ["git", *arguments],
        cwd=root,
        check=False,
        capture_output=True,
    )
    if result.returncode != 0:
        detail = result.stderr.decode("utf-8", errors="replace").strip()
        return b"", detail or f"git exited {result.returncode}"
    return result.stdout, None


def _capability_path_state_digest(
    root: Path,
    revision: str,
    paths: Iterable[str],
) -> tuple[str | None, str | None]:
    """Hash committed Git path states without depending on checkout EOL conversion."""
    digest = hashlib.sha256()
    digest.update(b"nfc-capability-path-state-v1\0")
    for relative in sorted(paths):
        encoded_path = relative.encode("utf-8")
        digest.update(len(encoded_path).to_bytes(4, "big"))
        digest.update(encoded_path)
        tree_entry, tree_error = _git_object(
            root,
            ["ls-tree", "-z", revision, "--", relative],
        )
        if tree_error is not None:
            return None, tree_error
        if not tree_entry:
            digest.update(b"\0deleted\0")
            continue
        entries = [entry for entry in tree_entry.split(b"\0") if entry]
        if len(entries) != 1 or b"\t" not in entries[0]:
            return None, f"expected one exact Git tree entry for {relative}"
        metadata, actual_path = entries[0].split(b"\t", 1)
        values = metadata.split(b" ")
        if len(values) != 3 or actual_path.decode("utf-8", errors="strict") != relative:
            return None, f"invalid Git tree entry for {relative}"
        mode, object_type, object_id = values
        if object_type != b"blob":
            return None, f"capability mutable path is not a Git blob: {relative}"
        blob, blob_error = _git_object(root, ["cat-file", "blob", object_id.decode("ascii")])
        if blob_error is not None:
            return None, blob_error
        digest.update(b"\0present\0")
        digest.update(mode)
        digest.update(b"\0")
        digest.update(len(blob).to_bytes(8, "big"))
        digest.update(blob)
    return digest.hexdigest(), None


def _historical_final_records(
    root: Path,
) -> tuple[dict[str, _CapabilityRecordHistory], set[str], str | None]:
    """Return committed active/final snapshots for every historical task path."""
    output, error = _git_object(
        root,
        [
            "log",
            "--format=",
            "--name-only",
            "--diff-filter=ADMR",
            "--",
            CAPABILITY_REUSE_CHANGE_RECORD_ROOT.as_posix(),
        ],
    )
    if error is not None:
        return {}, set(), error
    candidates = {
        value.decode("utf-8", errors="strict")
        for value in output.splitlines()
        if value
    }
    history: dict[str, _CapabilityRecordHistory] = {}
    invalid_nested_paths: set[str] = set()
    for relative in sorted(candidates):
        path = PurePosixPath(relative)
        if path.suffix != ".json":
            continue
        if path.parent != CAPABILITY_REUSE_CHANGE_RECORD_ROOT:
            invalid_nested_paths.add(relative)
            continue
        revisions, revision_error = _git_object(
            root,
            ["rev-list", "--reverse", "HEAD", "--", relative],
        )
        if revision_error is not None:
            return {}, invalid_nested_paths, revision_error
        first_active: _CommittedCapabilityRecord | None = None
        first_final: _CommittedCapabilityRecord | None = None
        latest_active: _CommittedCapabilityRecord | None = None
        admitted_field_violation: str | None = None
        for revision_value in revisions.splitlines():
            revision = revision_value.decode("ascii")
            content, content_error = _git_object(
                root,
                ["show", f"{revision}:{relative}"],
            )
            if content_error is not None:
                continue
            try:
                value = json.loads(content.decode("utf-8"))
            except (UnicodeDecodeError, json.JSONDecodeError):
                continue
            if not isinstance(value, dict) or value.get("schemaVersion") != 2:
                continue
            committed = _CommittedCapabilityRecord(revision, content, value)
            if value.get("state") == "design-active":
                if first_active is None:
                    first_active = committed
                elif any(
                    first_active.value.get(field) != value.get(field)
                    for field in CAPABILITY_REUSE_RECORD_FIELDS - CAPABILITY_REUSE_FINALIZED_FIELDS
                ):
                    admitted_field_violation = revision
                latest_active = committed
            elif value.get("state") == "final-complete":
                if first_active is not None and any(
                    first_active.value.get(field) != value.get(field)
                    for field in CAPABILITY_REUSE_RECORD_FIELDS - CAPABILITY_REUSE_FINALIZED_FIELDS
                ):
                    admitted_field_violation = revision
                first_final = committed
                break
        if first_final is not None or latest_active is not None:
            history[relative] = _CapabilityRecordHistory(
                first_active=first_active,
                first_final=first_final,
                latest_unfinalized_active=None if first_final is not None else latest_active,
                admitted_field_violation=admitted_field_violation,
            )
    return history, invalid_nested_paths, None


def _git_index_blob(root: Path, relative: str) -> tuple[bytes | None, str | None]:
    output, error = _git_object(root, ["ls-files", "--stage", "-z", "--", relative])
    if error is not None:
        return None, error
    entries = [entry for entry in output.split(b"\0") if entry]
    if not entries:
        return None, "record is not present in the Git index"
    if len(entries) != 1 or b"\t" not in entries[0]:
        return None, "record has multiple or malformed Git index entries"
    metadata, actual_path = entries[0].split(b"\t", 1)
    values = metadata.split(b" ")
    if len(values) != 3 or actual_path.decode("utf-8", errors="strict") != relative:
        return None, "record has a malformed Git index entry"
    mode, object_id, stage = values
    if stage != b"0":
        return None, "record has an unresolved Git index stage"
    if object_id == b"0" * len(object_id):
        return None, "record is intent-to-add and has no indexed blob"
    in_head = subprocess.run(
        ["git", "cat-file", "-e", f"HEAD:{relative}"],
        cwd=root,
        check=False,
        capture_output=True,
    )
    if in_head.returncode != 0:
        staged_paths, staged_error = _git_paths(
            root,
            ["diff", "--cached", "--name-only", "-z", "--", relative],
        )
        if staged_error is not None:
            return None, staged_error
        if relative not in staged_paths:
            return None, "record is intent-to-add and has no indexed candidate blob"
    if mode not in {b"100644", b"100755"}:
        return None, f"record has unsupported Git index mode {mode.decode('ascii')}"
    content, content_error = _git_object(
        root,
        ["cat-file", "blob", object_id.decode("ascii")],
    )
    if content_error is not None:
        return None, content_error
    return content, None


def _record_changed_in_commits_after(
    root: Path,
    relative: str,
    revision: str,
) -> tuple[bool, str | None]:
    revisions, revisions_error = _git_object(
        root,
        ["rev-list", "--ancestry-path", f"{revision}..HEAD"],
    )
    if revisions_error is not None:
        return False, revisions_error
    for candidate in revisions.splitlines():
        changed, changed_error = _git_object(
            root,
            [
                "diff-tree",
                "--no-commit-id",
                "--name-status",
                "-z",
                "-r",
                "-m",
                "--find-renames",
                candidate.decode("ascii"),
                "--",
            ],
        )
        if changed_error is not None:
            return False, changed_error
        try:
            if relative in _parse_git_name_status(changed):
                redundant, redundant_error = _is_tree_transparent_containment_merge(
                    root,
                    candidate.decode("ascii"),
                )
                if redundant_error is not None:
                    return False, redundant_error
                if redundant:
                    continue
                return True, None
        except (UnicodeDecodeError, ValueError) as exc:
            return False, str(exc)
    return False, None


def _is_tree_transparent_containment_merge(
    root: Path,
    candidate: str,
) -> tuple[bool, str | None]:
    """Recognize one redundant merge edge without trusting or pruning its ancestry."""
    parent_output, parent_error = _git_object(
        root,
        ["rev-list", "--parents", "-n", "1", candidate],
    )
    if parent_error is not None:
        return False, parent_error
    try:
        revisions = parent_output.decode("ascii").split()
    except UnicodeDecodeError as exc:
        return False, str(exc)
    if len(revisions) != 3 or revisions[0] != candidate:
        return False, None
    first_parent, second_parent = revisions[1:]
    tree_output, tree_error = _git_object(
        root,
        [
            "rev-parse",
            f"{candidate}^{{tree}}",
            f"{first_parent}^{{tree}}",
            f"{second_parent}^{{tree}}",
        ],
    )
    if tree_error is not None:
        return False, tree_error
    try:
        trees = tree_output.decode("ascii").split()
    except UnicodeDecodeError as exc:
        return False, str(exc)
    if len(trees) != 3:
        return False, "git returned an incomplete merge-tree identity"
    candidate_tree, first_tree, second_tree = trees
    matches_first = candidate_tree == first_tree
    matches_second = candidate_tree == second_tree
    if matches_first == matches_second:
        return False, None
    equivalent_parent = first_parent if matches_first else second_parent
    other_parent = second_parent if matches_first else first_parent
    ancestry = subprocess.run(
        ["git", "merge-base", "--is-ancestor", other_parent, equivalent_parent],
        cwd=root,
        check=False,
        capture_output=True,
    )
    if ancestry.returncode == 0:
        return True, None
    if ancestry.returncode == 1:
        return False, None
    detail = ancestry.stderr.decode("utf-8", errors="replace").strip()
    return False, detail or f"git exited {ancestry.returncode}"


def _load_trusted_capability_checkpoint(
    root: Path,
    errors: list[str],
) -> _TrustedCapabilityCheckpoint | None:
    """Load and audit the one-time owner-approved legacy-record activation."""
    relative = CAPABILITY_REUSE_TRUSTED_CHECKPOINT_PATH.as_posix()
    additions, additions_error = _git_object(
        root,
        ["log", "--reverse", "--format=%H", "--diff-filter=A", "--", relative],
    )
    if additions_error is not None:
        errors.append(f"trusted capability checkpoint history could not be read: {additions_error}")
        return None
    addition_commits = [value.decode("ascii") for value in additions.splitlines() if value]
    if not addition_commits:
        index_content, index_error = _git_index_blob(root, relative)
        if index_content is not None or (root / CAPABILITY_REUSE_TRUSTED_CHECKPOINT_PATH).exists():
            errors.append("trusted capability checkpoint activation manifest must be committed")
        elif index_error not in {None, "record is not present in the Git index"}:
            errors.append(f"trusted capability checkpoint index could not be read: {index_error}")
        return None
    if len(addition_commits) != 1:
        errors.append("trusted capability checkpoint activation manifest must be added exactly once")
        return None

    activation_commit = addition_commits[0]
    manifest_content, manifest_error = _git_object(
        root,
        ["show", f"{activation_commit}:{relative}"],
    )
    if manifest_error is not None:
        errors.append(f"trusted capability checkpoint manifest could not be read: {manifest_error}")
        return None
    current_content, current_error = _git_index_blob(root, relative)
    if current_error is not None or current_content is None:
        errors.append("trusted capability checkpoint activation manifest was deleted")
    elif current_content != manifest_content:
        errors.append("trusted capability checkpoint activation manifest is immutable")
    else:
        try:
            worktree_content = (root / CAPABILITY_REUSE_TRUSTED_CHECKPOINT_PATH).read_bytes()
        except OSError as exc:
            errors.append(f"trusted capability checkpoint index/worktree mismatch: {exc}")
        else:
            if worktree_content != manifest_content:
                errors.append("trusted capability checkpoint index/worktree content differs")
    changed, changed_error = _record_changed_in_commits_after(root, relative, activation_commit)
    if changed_error is not None:
        errors.append(f"trusted capability checkpoint history could not be audited: {changed_error}")
    elif changed:
        errors.append("trusted capability checkpoint activation manifest changed after activation")

    try:
        manifest = json.loads(manifest_content.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        errors.append(f"invalid trusted capability checkpoint JSON: {exc}")
        return None
    if not isinstance(manifest, dict):
        errors.append("trusted capability checkpoint manifest must be a JSON object")
        return None
    if set(manifest) != CAPABILITY_REUSE_TRUSTED_CHECKPOINT_FIELDS:
        errors.append("trusted capability checkpoint fields differ from v1")
        return None
    if manifest.get("schemaVersion") != 1:
        errors.append("trusted capability checkpoint requires schemaVersion 1")
    if manifest.get("checkpointId") != "CAPABILITY-REUSE-INITIAL-100":
        errors.append("trusted capability checkpoint has an unexpected checkpointId")
    reviewed_head = manifest.get("reviewedHead")
    reviewed_tree = manifest.get("reviewedTree")
    if not isinstance(reviewed_head, str) or re.fullmatch(r"[0-9a-f]{40}", reviewed_head) is None:
        errors.append("trusted capability checkpoint reviewedHead must be a full lowercase SHA")
        return None
    if not isinstance(reviewed_tree, str) or re.fullmatch(r"[0-9a-f]{40}", reviewed_tree) is None:
        errors.append("trusted capability checkpoint reviewedTree must be a full lowercase SHA")
        return None
    owner_decision = manifest.get("ownerDecisionRef")
    if not isinstance(owner_decision, str) or not owner_decision.strip():
        errors.append("trusted capability checkpoint requires a non-empty ownerDecisionRef")

    parents, parents_error = _git_object(
        root,
        ["rev-list", "--parents", "-n", "1", activation_commit],
    )
    parent_values = parents.decode("ascii").split() if parents_error is None else []
    if parent_values != [activation_commit, reviewed_head]:
        errors.append("trusted capability checkpoint activation must directly follow reviewedHead")
    actual_tree, tree_error = _git_object(root, ["rev-parse", f"{reviewed_head}^{{tree}}"])
    if tree_error is not None or actual_tree.decode("ascii").strip() != reviewed_tree:
        errors.append("trusted capability checkpoint reviewedTree differs from reviewedHead")

    legacy_records = manifest.get("legacyRecords")
    if not isinstance(legacy_records, list) or not legacy_records:
        errors.append("trusted capability checkpoint requires a non-empty legacyRecords list")
        return None
    retired_records: dict[str, str] = {}
    legacy_values_by_task: dict[str, dict[str, Any]] = {}
    listed_paths: list[str] = []
    for entry in legacy_records:
        if not isinstance(entry, dict) or set(entry) != CAPABILITY_REUSE_LEGACY_RECORD_FIELDS:
            errors.append("trusted capability checkpoint legacy record fields differ from v1")
            continue
        task_id = entry.get("taskId")
        record_path = entry.get("path")
        risk = entry.get("risk")
        state = entry.get("state")
        content_sha = entry.get("contentSha256")
        if not isinstance(task_id, str) or CAPABILITY_REUSE_TASK_ID.fullmatch(task_id) is None:
            errors.append("trusted capability checkpoint contains an invalid legacy taskId")
            continue
        candidate = PurePosixPath(str(record_path))
        if (
            not isinstance(record_path, str)
            or candidate.parent != CAPABILITY_REUSE_CHANGE_RECORD_ROOT
            or candidate.suffix != ".json"
            or candidate.stem != task_id
        ):
            errors.append(f"trusted capability checkpoint contains an invalid legacy path: {record_path}")
            continue
        if risk not in CAPABILITY_REUSE_RISKS or state not in CAPABILITY_REUSE_STATES:
            errors.append(f"trusted capability checkpoint contains invalid legacy facts: {task_id}")
            continue
        if not isinstance(content_sha, str) or re.fullmatch(r"[0-9a-f]{64}", content_sha) is None:
            errors.append(f"trusted capability checkpoint contains an invalid content SHA: {task_id}")
            continue
        if record_path in retired_records or task_id in legacy_values_by_task:
            errors.append(f"trusted capability checkpoint legacy inventory contains a duplicate: {task_id}")
            continue
        content, content_error = _git_object(root, ["show", f"{reviewed_head}:{record_path}"])
        if content_error is not None:
            errors.append(f"trusted capability checkpoint legacy record is absent at reviewedHead: {task_id}")
            continue
        if hashlib.sha256(content).hexdigest() != content_sha:
            errors.append(f"trusted capability checkpoint legacy content SHA differs: {task_id}")
        try:
            record = json.loads(content.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError):
            errors.append(f"trusted capability checkpoint legacy record is invalid JSON: {task_id}")
            continue
        if not isinstance(record, dict) or any(
            record.get(field) != entry.get(field)
            for field in ("taskId", "risk", "state")
        ):
            errors.append(f"trusted capability checkpoint legacy facts differ from reviewed bytes: {task_id}")
            continue
        retired_records[record_path] = task_id
        legacy_values_by_task[task_id] = record
        listed_paths.append(record_path)
    if listed_paths != sorted(listed_paths):
        errors.append("trusted capability checkpoint legacyRecords must be sorted by path")

    tree_paths, tree_paths_error = _git_object(
        root,
        [
            "ls-tree",
            "-r",
            "--name-only",
            "-z",
            reviewed_head,
            "--",
            CAPABILITY_REUSE_CHANGE_RECORD_ROOT.as_posix(),
        ],
    )
    if tree_paths_error is not None:
        errors.append(f"trusted capability checkpoint legacy inventory could not be read: {tree_paths_error}")
    else:
        exact_legacy_paths = {
            value.decode("utf-8", errors="strict")
            for value in tree_paths.split(b"\0")
            if value
            and PurePosixPath(value.decode("utf-8", errors="strict")).parent
            == CAPABILITY_REUSE_CHANGE_RECORD_ROOT
            and PurePosixPath(value.decode("utf-8", errors="strict")).suffix == ".json"
        }
        if set(retired_records) != exact_legacy_paths:
            errors.append("trusted capability checkpoint does not exactly inventory reviewed legacy records")

    open_authorities = manifest.get("openR3Authorities")
    if not isinstance(open_authorities, list):
        errors.append("trusted capability checkpoint openR3Authorities must be a list")
        open_authorities = []
    authority_by_task: dict[str, str] = {}
    authority_order: list[str] = []
    for entry in open_authorities:
        if not isinstance(entry, dict) or set(entry) != CAPABILITY_REUSE_OPEN_AUTHORITY_FIELDS:
            errors.append("trusted capability checkpoint R3 authority fields differ from v1")
            continue
        task_id = entry.get("taskId")
        authority_type = entry.get("authorityType")
        if (
            not isinstance(task_id, str)
            or authority_type not in CAPABILITY_REUSE_AUTHORITY_TYPES
            or entry.get("status") != "pending"
            or task_id in authority_by_task
        ):
            errors.append("trusted capability checkpoint contains an invalid R3 authority")
            continue
        authority_by_task[task_id] = authority_type
        authority_order.append(task_id)
    if authority_order != sorted(authority_order):
        errors.append("trusted capability checkpoint openR3Authorities must be sorted by taskId")
    expected_r3 = {
        task_id
        for task_id, record in legacy_values_by_task.items()
        if record.get("risk") == "R3"
    }
    if set(authority_by_task) != expected_r3:
        errors.append("trusted capability checkpoint does not exactly list every legacy R3 authority")
    for task_id, authority_type in authority_by_task.items():
        expected_type = (
            "firmware-owner"
            if task_id in CAPABILITY_REUSE_INITIAL_FIRMWARE_OWNER_TASKS
            else "release-owner"
        )
        if authority_type != expected_type:
            errors.append(f"trusted capability checkpoint R3 authority type differs: {task_id}")

    expected_activation_paths = {relative, *retired_records}
    activation_paths, activation_error = _git_revision_changed_paths(
        root,
        reviewed_head,
        activation_commit,
    )
    if activation_error is not None or activation_paths != expected_activation_paths:
        errors.append("trusted capability checkpoint activation changes paths outside its exact manifest and legacy deletions")
    before_manifest, before_manifest_error = _git_object(root, ["show", f"{reviewed_head}:{relative}"])
    if before_manifest_error is None or before_manifest:
        errors.append("trusted capability checkpoint manifest already existed at reviewedHead")
    for record_path, task_id in retired_records.items():
        after_record, after_error = _git_object(root, ["show", f"{activation_commit}:{record_path}"])
        if after_error is None or after_record:
            errors.append(f"trusted capability checkpoint did not delete legacy record: {task_id}")
        current_record, current_record_error = _git_object(root, ["show", f"HEAD:{record_path}"])
        if current_record_error is None or current_record or (root / PurePosixPath(record_path)).exists():
            errors.append(f"trusted capability checkpoint retired legacy record was restored: {task_id}")
        record_changed, record_changed_error = _record_changed_in_commits_after(
            root,
            record_path,
            activation_commit,
        )
        if record_changed_error is not None:
            errors.append(f"trusted capability checkpoint retired history could not be read: {task_id}")
        elif record_changed:
            errors.append(f"trusted capability checkpoint retired legacy record changed after activation: {task_id}")

    return _TrustedCapabilityCheckpoint(
        activation_commit=activation_commit,
        reviewed_head=reviewed_head,
        retired_records=retired_records,
        reserved_task_ids=frozenset(legacy_values_by_task),
        open_r3_authorities=authority_by_task,
    )


def _validate_external_authority_attestations(
    root: Path,
    required: dict[str, str | None],
    errors: list[str],
) -> dict[str, str]:
    """Validate immutable exact-head evidence commits for required R3 owners."""
    root_relative = CAPABILITY_REUSE_EXTERNAL_AUTHORITY_ROOT.as_posix()
    indexed_paths, index_error = _git_paths(root, ["ls-files", "-z", "--", root_relative])
    untracked_paths, untracked_error = _git_paths(
        root,
        ["ls-files", "--others", "--exclude-standard", "-z", "--", root_relative],
    )
    if index_error is not None or untracked_error is not None:
        errors.append(
            "external authority attestation inventory could not be read: "
            f"{index_error or untracked_error}"
        )
        return {}
    inventory = indexed_paths | untracked_paths
    invalid_paths = {
        relative
        for relative in inventory
        if not (
            PurePosixPath(relative).parent == CAPABILITY_REUSE_EXTERNAL_AUTHORITY_ROOT
            and PurePosixPath(relative).suffix == ".json"
        )
    }
    for relative in sorted(invalid_paths):
        errors.append(f"external authority attestation must be a direct JSON child: {relative}")
    paths = inventory - invalid_paths
    expected_paths = {
        f"{root_relative}/{task_id}.json"
        for task_id in required
    }
    for missing in sorted(expected_paths - paths):
        errors.append(f"external R3 authority remains pending: {PurePosixPath(missing).stem}")
    for extra in sorted(paths - expected_paths):
        errors.append(f"external authority attestation has no current R3 requirement: {extra}")
    if not paths:
        return {}
    if paths != expected_paths:
        errors.append("external authority attestations must close the exact current R3 set in one batch")

    approved: dict[str, str] = {}
    evidence_batches: dict[tuple[str, str], set[str]] = {}
    for relative in sorted(paths & expected_paths):
        content, content_error = _git_index_blob(root, relative)
        if content_error is not None or content is None:
            errors.append(f"external authority attestation must have one committed indexed blob: {relative}")
            continue
        try:
            worktree_content = (root / PurePosixPath(relative)).read_bytes()
        except OSError as exc:
            errors.append(f"external authority attestation index/worktree mismatch: {relative}: {exc}")
            continue
        if worktree_content != content:
            errors.append(f"external authority attestation index/worktree content differs: {relative}")
            continue
        try:
            value = json.loads(content.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            errors.append(f"invalid external authority attestation JSON {relative}: {exc}")
            continue
        if not isinstance(value, dict) or set(value) != CAPABILITY_REUSE_EXTERNAL_ATTESTATION_FIELDS:
            errors.append(f"external authority attestation fields differ from v1: {relative}")
            continue
        task_id = value.get("taskId")
        authority_type = value.get("authorityType")
        reviewed_head = value.get("reviewedHead")
        if (
            value.get("schemaVersion") != 1
            or task_id != PurePosixPath(relative).stem
            or task_id not in required
            or authority_type not in CAPABILITY_REUSE_AUTHORITY_TYPES
            or (required.get(str(task_id)) is not None and authority_type != required.get(str(task_id)))
            or not isinstance(reviewed_head, str)
            or re.fullmatch(r"[0-9a-f]{40}", reviewed_head) is None
            or value.get("decision") != "approved"
            or not isinstance(value.get("reviewer"), str)
            or not value.get("reviewer", "").strip()
            or not isinstance(value.get("evidence"), str)
            or not value.get("evidence", "").strip()
        ):
            errors.append(f"external authority attestation has invalid typed evidence: {relative}")
            continue
        additions, additions_error = _git_object(
            root,
            ["log", "--reverse", "--format=%H", "--diff-filter=A", "--", relative],
        )
        candidates = [item.decode("ascii") for item in additions.splitlines() if item]
        if additions_error is not None or len(candidates) != 1:
            errors.append(f"external authority attestation must be added exactly once: {relative}")
            continue
        addition_commit = candidates[0]
        committed, committed_error = _git_object(root, ["show", f"{addition_commit}:{relative}"])
        changed, changed_error = _record_changed_in_commits_after(root, relative, addition_commit)
        if committed_error is not None or committed != content or changed_error is not None or changed:
            errors.append(f"external authority attestation is immutable after commit: {relative}")
            continue
        evidence_batches.setdefault((addition_commit, reviewed_head), set()).add(relative)
        approved[str(task_id)] = reviewed_head

    if approved and set(approved) != set(required):
        errors.append("external authority attestation batch does not approve every required R3 task")
    for (evidence_commit, reviewed_head), batch_paths in evidence_batches.items():
        parents, parents_error = _git_object(
            root,
            ["rev-list", "--parents", "-n", "1", evidence_commit],
        )
        parent_values = parents.decode("ascii").split() if parents_error is None else []
        if parent_values != [evidence_commit, reviewed_head]:
            errors.append("external authority evidence commit must directly follow reviewedHead")
        evidence_paths, evidence_error = _git_revision_changed_paths(
            root,
            reviewed_head,
            evidence_commit,
        )
        if evidence_error is not None or evidence_paths != batch_paths:
            errors.append("external authority evidence commit may add only its exact attestation batch")
    return approved


def _validate_capability_reuse_record(
    path: Path,
    record: object,
    errors: list[str],
) -> dict[str, Any] | None:
    relative = path.as_posix()
    if not isinstance(record, dict):
        errors.append(f"capability-reuse record must be a JSON object: {relative}")
        return None
    if record.get("schemaVersion") != 2:
        errors.append(f"capability-reuse record requires schemaVersion 2: {relative}")
        return None
    fields = set(record)
    if fields != CAPABILITY_REUSE_RECORD_FIELDS:
        errors.append(
            f"capability-reuse record fields differ from v2 in {relative}: "
            f"missing={sorted(CAPABILITY_REUSE_RECORD_FIELDS - fields)}, "
            f"extra={sorted(fields - CAPABILITY_REUSE_RECORD_FIELDS)}"
        )
        return None
    for field in (
        "taskId",
        "capability",
        "integrationBase",
        "implementationOwner",
        "semanticOwner",
        "terminalContract",
    ):
        if not isinstance(record[field], str) or not record[field].strip():
            errors.append(f"capability-reuse record requires non-empty {field}: {relative}")
    task_id = record["taskId"]
    if not isinstance(task_id, str) or CAPABILITY_REUSE_TASK_ID.fullmatch(task_id) is None:
        errors.append(f"capability-reuse taskId has invalid format: {relative}")
    if path.stem != task_id:
        errors.append(
            f"capability-reuse filename must equal taskId: {relative}: {task_id}"
        )
    if not re.fullmatch(r"[0-9a-f]{40}", str(record["integrationBase"])):
        errors.append(f"capability-reuse integrationBase must be a full lowercase SHA: {relative}")
    for field, allowed in (
        ("risk", CAPABILITY_REUSE_RISKS),
        ("kind", CAPABILITY_REUSE_KINDS),
        ("state", CAPABILITY_REUSE_STATES),
        ("disposition", CAPABILITY_REUSE_DISPOSITIONS),
    ):
        if record[field] not in allowed:
            errors.append(
                f"capability-reuse {field} must be one of {sorted(allowed)}: {relative}"
            )
    search_evidence = record["searchEvidence"]
    if not isinstance(search_evidence, list) or not search_evidence or not all(
        isinstance(value, str) and value.strip() for value in search_evidence
    ):
        errors.append(f"capability-reuse searchEvidence must be a non-empty string list: {relative}")

    mutable_paths = record["mutablePaths"]
    if not isinstance(mutable_paths, list) or not all(isinstance(value, str) for value in mutable_paths):
        errors.append(f"capability-reuse mutablePaths must be a string list: {relative}")
        mutable_paths = []
    normalized_paths: list[str] = []
    for value in mutable_paths:
        candidate = PurePosixPath(value)
        if (
            not value
            or "\\" in value
            or candidate.is_absolute()
            or any(part in {"", ".", ".."} for part in candidate.parts)
            or any(token in value for token in "*?[")
            or candidate.as_posix() != value
        ):
            errors.append(f"capability-reuse mutable path must be exact and repo-relative: {relative}: {value}")
            continue
        normalized_paths.append(value)
    if len(normalized_paths) != len(set(normalized_paths)):
        errors.append(f"capability-reuse mutablePaths contains duplicates: {relative}")
    record["mutablePaths"] = normalized_paths
    state = record["state"]
    if state == "blocked" and normalized_paths:
        errors.append(f"blocked capability-reuse record requires empty mutablePaths: {relative}")
    if state in {"design-active", "final-complete"} and not normalized_paths:
        errors.append(f"{state} capability-reuse record requires mutablePaths: {relative}")
    if record["disposition"] == "reject-duplicate" and state != "blocked":
        errors.append(f"reject-duplicate capability-reuse record must be blocked: {relative}")
    governed_mutable_paths = [
        value for value in normalized_paths if _is_capability_reuse_governed_path(value)
    ]
    if governed_mutable_paths and record["risk"] in CAPABILITY_REUSE_RISK_LEVELS:
        minimum_risk = max(
            (_capability_reuse_minimum_risk(value) for value in governed_mutable_paths),
            key=CAPABILITY_REUSE_RISK_LEVELS.__getitem__,
        )
        if (
            CAPABILITY_REUSE_RISK_LEVELS[record["risk"]]
            < CAPABILITY_REUSE_RISK_LEVELS[minimum_risk]
        ):
            errors.append(
                f"capability-reuse risk is below path minimum {minimum_risk}: "
                f"{relative}: {record['risk']}"
            )

    review = record["designReview"]
    if not isinstance(review, dict) or set(review) != {"reviewer", "outcome", "evidence"}:
        errors.append(f"capability-reuse designReview differs from v2: {relative}")
        return record
    outcome = review.get("outcome")
    if outcome not in CAPABILITY_REUSE_DESIGN_REVIEW_OUTCOMES:
        errors.append(
            f"capability-reuse design review outcome must be one of "
            f"{sorted(CAPABILITY_REUSE_DESIGN_REVIEW_OUTCOMES)}: {relative}"
        )
    reviewer = review.get("reviewer")
    evidence = review.get("evidence")
    requires_independent_review = record["risk"] in {"R2", "R3"}
    if requires_independent_review:
        if not isinstance(reviewer, str) or not reviewer.strip():
            errors.append(f"R2/R3 capability-reuse record requires a design reviewer: {relative}")
        elif reviewer.strip().casefold() == record["implementationOwner"].strip().casefold():
            errors.append(f"R2/R3 capability-reuse design reviewer must be independent: {relative}")
        if not isinstance(evidence, str) or not evidence.strip():
            errors.append(f"R2/R3 capability-reuse record requires design-review evidence: {relative}")
    elif reviewer is not None:
        errors.append(f"R0/R1 capability-reuse design reviewer must be null: {relative}")
    if state == "blocked":
        if outcome != "blocked":
            errors.append(f"blocked capability-reuse record requires blocked design review: {relative}")
    elif requires_independent_review:
        if outcome not in {"approved", "findings-incorporated"}:
            errors.append(f"R2/R3 admitted record requires an admitted design review: {relative}")
    elif outcome != "not-required":
        errors.append(f"R0/R1 capability-reuse design review must be not-required: {relative}")
    final_review = record["finalReview"]
    if not isinstance(final_review, dict) or set(final_review) != {
        "reviewer",
        "outcome",
        "evidence",
    }:
        errors.append(f"capability-reuse finalReview differs from v2: {relative}")
        return record
    final_outcome = final_review.get("outcome")
    if final_outcome not in CAPABILITY_REUSE_FINAL_REVIEW_OUTCOMES:
        errors.append(
            f"capability-reuse final review outcome must be one of "
            f"{sorted(CAPABILITY_REUSE_FINAL_REVIEW_OUTCOMES)}: {relative}"
        )
    final_reviewer = final_review.get("reviewer")
    final_evidence = final_review.get("evidence")
    head_fields = ("implementationHead", "reviewedHead")
    if state == "final-complete":
        for field in head_fields:
            if re.fullmatch(r"[0-9a-f]{40}", str(record[field])) is None:
                errors.append(
                    f"final-complete capability-reuse record requires full lowercase {field}: {relative}"
                )
        if record["implementationHead"] != record["reviewedHead"]:
            errors.append(
                f"final-complete capability-reuse implementationHead must equal reviewedHead: {relative}"
            )
        if re.fullmatch(r"[0-9a-f]{64}", str(record["pathStateDigest"])) is None:
            errors.append(
                f"final-complete capability-reuse record requires SHA-256 pathStateDigest: {relative}"
            )
        if not isinstance(final_reviewer, str) or not final_reviewer.strip():
            errors.append(f"final-complete capability-reuse record requires a final reviewer: {relative}")
        elif final_reviewer.strip().casefold() == record["implementationOwner"].strip().casefold():
            errors.append(
                f"final-complete capability-reuse final reviewer must be independent: {relative}"
            )
        if final_outcome not in {"approved", "findings-incorporated"}:
            errors.append(f"final-complete capability-reuse record requires an admitted final review: {relative}")
        if not isinstance(final_evidence, str) or not final_evidence.strip():
            errors.append(f"final-complete capability-reuse record requires final-review evidence: {relative}")
    else:
        for field in (*head_fields, "pathStateDigest"):
            if record[field] is not None:
                errors.append(f"{state} capability-reuse record requires null {field}: {relative}")
        if final_reviewer is not None or final_outcome != "pending" or final_evidence != "":
            errors.append(f"{state} capability-reuse final review must remain pending: {relative}")
    return record


def validate_capability_reuse_governance(
    root: Path,
    errors: list[str],
    *,
    trusted_initial_base: str | None = None,
) -> None:
    capability_error_start = len(errors)
    shallow = subprocess.run(
        ["git", "rev-parse", "--is-shallow-repository"],
        cwd=root,
        check=False,
        capture_output=True,
        text=True,
    )
    if shallow.returncode != 0 or shallow.stdout.strip() not in {"true", "false"}:
        errors.append(
            "capability-reuse history availability could not be read: "
            f"{shallow.stderr.strip() or f'git exited {shallow.returncode}'}"
        )
        return
    if shallow.stdout.strip() == "true":
        errors.append(
            "capability-reuse validation requires complete Git history; shallow repositories fail closed"
        )
        return

    records_root = root / "docs" / "governance" / "change-records"
    all_index_paths, index_paths_error = _git_paths(
        root,
        [
            "ls-files",
            "-z",
            "--",
            CAPABILITY_REUSE_CHANGE_RECORD_ROOT.as_posix(),
        ],
    )
    if index_paths_error is not None:
        errors.append(f"capability-reuse Git index could not be read: {index_paths_error}")
        return
    all_worktree_paths = {
        path.relative_to(root).as_posix()
        for path in records_root.rglob("*.json")
    } if records_root.is_dir() else set()
    invalid_nested_paths = {
        relative
        for relative in all_index_paths | all_worktree_paths
        if PurePosixPath(relative).suffix == ".json"
        and PurePosixPath(relative).parent != CAPABILITY_REUSE_CHANGE_RECORD_ROOT
    }
    for relative in sorted(invalid_nested_paths):
        errors.append(
            f"capability-reuse record parent must be exactly "
            f"{CAPABILITY_REUSE_CHANGE_RECORD_ROOT.as_posix()}: {relative}"
        )
    index_paths = {
        relative
        for relative in all_index_paths
        if PurePosixPath(relative).parent == CAPABILITY_REUSE_CHANGE_RECORD_ROOT
        and PurePosixPath(relative).suffix == ".json"
    }
    worktree_paths = {
        relative
        for relative in all_worktree_paths
        if PurePosixPath(relative).parent == CAPABILITY_REUSE_CHANGE_RECORD_ROOT
    }
    current_relatives = sorted(index_paths | worktree_paths)

    indexed_content: dict[str, bytes] = {}
    records_by_relative: dict[str, dict[str, Any]] = {}
    current_snapshot_failed = False
    for relative in current_relatives:
        path = root / PurePosixPath(relative)
        ignored = subprocess.run(
            ["git", "check-ignore", "-q", "--", relative],
            cwd=root,
            check=False,
            capture_output=True,
        )
        if ignored.returncode == 0:
            errors.append(f"capability-reuse record is ignored and cannot open the gate: {relative}")
            current_snapshot_failed = True
            continue
        if ignored.returncode != 1:
            detail = ignored.stderr.decode("utf-8", errors="replace").strip()
            errors.append(
                f"capability-reuse record tracking state could not be read: {relative}: "
                f"{detail or f'git exited {ignored.returncode}'}"
            )
            current_snapshot_failed = True
            continue
        content, content_error = _git_index_blob(root, relative)
        if content_error is not None or content is None:
            if content_error == "record is not present in the Git index":
                errors.append(f"capability-reuse record must be tracked in Git: {relative}")
            else:
                errors.append(
                    f"capability-reuse record must have one real staged Git blob: "
                    f"{relative}: {content_error or 'missing blob'}"
                )
            current_snapshot_failed = True
            continue
        indexed_content[relative] = content
        try:
            worktree_content = path.read_bytes()
        except OSError as exc:
            errors.append(f"capability-reuse index/worktree mismatch: {relative}: {exc}")
            current_snapshot_failed = True
            continue
        if worktree_content != content:
            errors.append(f"capability-reuse index/worktree content differs: {relative}")
            current_snapshot_failed = True
        try:
            value = json.loads(content.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            errors.append(f"invalid capability-reuse JSON {relative}: {exc}")
            continue
        validated = _validate_capability_reuse_record(PurePosixPath(relative), value, errors)
        if validated is not None:
            records_by_relative[relative] = validated

    task_ids = [record.get("taskId") for record in records_by_relative.values()]
    duplicate_task_ids = {
        value for value in task_ids if isinstance(value, str) and task_ids.count(value) > 1
    }
    for task_id in sorted(duplicate_task_ids):
        errors.append(f"capability-reuse taskId must be unique: {task_id}")

    for record in records_by_relative.values():
        if record.get("state") != "design-active":
            continue
        task_id = str(record.get("taskId", "<invalid>"))
        for relative in record.get("mutablePaths", []):
            if not _is_capability_reuse_governed_path(relative):
                errors.append(
                    f"current capability-reuse mutable path is not governed: "
                    f"{task_id}: {relative}"
                )

    if current_snapshot_failed or len(errors) != capability_error_start:
        return

    history, historical_nested_paths, history_error = _historical_final_records(root)
    if history_error is not None:
        errors.append(f"capability-reuse record history could not be read: {history_error}")
        return
    for relative in sorted(historical_nested_paths):
        if relative not in invalid_nested_paths:
            errors.append(
                f"capability-reuse record parent must be exactly "
                f"{CAPABILITY_REUSE_CHANGE_RECORD_ROOT.as_posix()}: {relative}"
            )
    trusted_checkpoint = _load_trusted_capability_checkpoint(root, errors)
    retired_record_paths = (
        set(trusted_checkpoint.retired_records)
        if trusted_checkpoint is not None
        else set()
    )
    reserved_task_ids = (
        trusted_checkpoint.reserved_task_ids
        if trusted_checkpoint is not None
        else frozenset()
    )

    for relative, record_history in history.items():
        if relative in retired_record_paths:
            continue
        if record_history.admitted_field_violation is not None:
            errors.append(
                f"committed capability-reuse record changed immutable admitted fields: "
                f"{relative}: {record_history.admitted_field_violation}"
            )
        first_final = record_history.first_final
        if first_final is not None:
            if relative not in indexed_content:
                errors.append(f"final-complete capability-reuse record was deleted: {relative}")
            elif indexed_content[relative] != first_final.content:
                errors.append(f"final-complete capability-reuse record is immutable after commit: {relative}")
            changed, changed_error = _record_changed_in_commits_after(
                root,
                relative,
                first_final.revision,
            )
            if changed_error is not None:
                errors.append(
                    f"final-complete capability-reuse history could not be audited: "
                    f"{relative}: {changed_error}"
                )
            elif changed:
                errors.append(
                    f"final-complete capability-reuse record changed in commit history: {relative}"
                )
        active = record_history.latest_unfinalized_active
        if active is not None:
            current = records_by_relative.get(relative)
            if current is None or current.get("state") == "blocked":
                errors.append(
                    f"committed design-active capability-reuse record cannot be deleted or blocked: {relative}"
                )
            elif record_history.first_active is not None and any(
                record_history.first_active.value.get(field) != current.get(field)
                for field in CAPABILITY_REUSE_RECORD_FIELDS - CAPABILITY_REUSE_FINALIZED_FIELDS
            ):
                errors.append(
                    f"capability-reuse record changed immutable admitted fields: {relative}"
                )

    for task_id in sorted(
        value
        for value in task_ids
        if isinstance(value, str) and value in reserved_task_ids
    ):
        errors.append(f"retired capability-reuse taskId cannot be reused: {task_id}")

    derived_initial_base = (
        trusted_checkpoint.activation_commit
        if trusted_checkpoint is not None
        else trusted_initial_base
    )
    if trusted_checkpoint is not None and trusted_initial_base is not None:
        if trusted_initial_base != trusted_checkpoint.activation_commit:
            errors.append("supplied trusted initial base differs from the activated checkpoint")
            return
    if derived_initial_base is not None:
        if re.fullmatch(r"[0-9a-f]{40}", derived_initial_base) is None:
            errors.append("trusted initial capability-reuse base must be a full lowercase SHA")
            return
        trusted_commit = subprocess.run(
            ["git", "merge-base", "--is-ancestor", derived_initial_base, "HEAD"],
            cwd=root,
            check=False,
            capture_output=True,
        )
        if trusted_commit.returncode != 0:
            errors.append("trusted initial capability-reuse base is not on current HEAD ancestry")
            return

    final_groups: dict[str, list[tuple[str, dict[str, Any]]]] = {}
    for relative, record_history in history.items():
        if relative in retired_record_paths:
            continue
        if record_history.first_final is not None:
            final_groups.setdefault(record_history.first_final.revision, []).append(
                (relative, record_history.first_final.value)
            )
    required_external_authorities: dict[str, str | None] = (
        dict(trusted_checkpoint.open_r3_authorities)
        if trusted_checkpoint is not None
        else {}
    )
    for group in final_groups.values():
        for _, record in group:
            if record.get("risk") == "R3":
                required_external_authorities.setdefault(str(record.get("taskId")), None)
    current_r3_final_records = [
        record
        for record in records_by_relative.values()
        if record.get("state") == "final-complete"
        and record.get("risk") == "R3"
        and str(record.get("taskId")) not in reserved_task_ids
    ]
    for record in current_r3_final_records:
        required_external_authorities.setdefault(str(record.get("taskId")), None)
    approved_external_authorities = _validate_external_authority_attestations(
        root,
        required_external_authorities,
        errors,
    )
    for record in current_r3_final_records:
        task_id = str(record.get("taskId"))
        if task_id not in approved_external_authorities:
            errors.append(
                f"R3 final-complete capability-reuse record remains pending external "
                f"owner authority: {task_id}"
            )
    revision_order, revision_order_error = _git_object(root, ["rev-list", "--reverse", "HEAD"])
    if revision_order_error is not None:
        errors.append(f"capability-reuse commit order could not be read: {revision_order_error}")
        return
    ordered_final_commits = [
        value.decode("ascii")
        for value in revision_order.splitlines()
        if value.decode("ascii") in final_groups
    ]
    checkpoint = derived_initial_base
    if final_groups and checkpoint is None:
        errors.append(
            "capability-reuse trusted initial evidence checkpoint is pending owner authority"
        )
    for evidence_commit in ordered_final_commits:
        group = final_groups[evidence_commit]
        batch_error_count = len(errors)
        if any(
            relative not in records_by_relative
            or records_by_relative[relative].get("state") != "final-complete"
            for relative, _ in group
        ):
            errors.append(
                f"final capability-reuse batch contains an invalid current record: {evidence_commit}"
            )
        missing_r3_authority = sorted(
            str(record.get("taskId"))
            for _, record in group
            if record.get("risk") == "R3"
            and approved_external_authorities.get(str(record.get("taskId")))
            != evidence_commit
        )
        if missing_r3_authority:
            errors.append(
                f"R3 final capability-reuse batch remains pending external owner authority: "
                f"{evidence_commit}: {missing_r3_authority}"
            )
        reviewed_heads = {record.get("reviewedHead") for _, record in group}
        bases = {record.get("integrationBase") for _, record in group}
        if checkpoint is None or bases != {checkpoint}:
            errors.append(
                f"final capability-reuse batch does not bind the latest evidence checkpoint: {evidence_commit}"
            )
        if len(reviewed_heads) != 1:
            errors.append(f"final capability-reuse batch must bind one reviewedHead: {evidence_commit}")
            continue
        reviewed_head = next(iter(reviewed_heads))
        if not isinstance(reviewed_head, str) or re.fullmatch(r"[0-9a-f]{40}", reviewed_head) is None:
            continue
        parents, parents_error = _git_object(
            root,
            ["rev-list", "--parents", "-n", "1", evidence_commit],
        )
        parent_values = parents.decode("ascii").split() if parents_error is None else []
        if parent_values != [evidence_commit, reviewed_head]:
            errors.append(
                f"first final evidence commit must be the direct child of reviewedHead: {evidence_commit}"
            )
        evidence_changes, evidence_error = _git_revision_changed_paths(
            root,
            reviewed_head,
            evidence_commit,
        )
        if evidence_error is not None:
            errors.append(f"final evidence diff could not be read: {evidence_commit}: {evidence_error}")
        elif any(_is_capability_reuse_governed_path(path) for path in evidence_changes):
            errors.append(
                f"final evidence commit changes governed paths after reviewedHead: {evidence_commit}"
            )
        batch_coverage: dict[str, list[str]] = {}
        for relative, record in group:
            task_id = str(record.get("taskId", "<invalid>"))
            design_content, design_error = _git_object(
                root,
                ["show", f"{reviewed_head}:{relative}"],
            )
            try:
                design_record = json.loads(design_content.decode("utf-8")) if design_error is None else None
            except (UnicodeDecodeError, json.JSONDecodeError):
                design_record = None
            if not isinstance(design_record, dict) or design_record.get("state") != "design-active":
                errors.append(
                    f"final record requires its design-active predecessor at reviewedHead: {task_id}"
                )
                continue
            invariant_fields = CAPABILITY_REUSE_RECORD_FIELDS - CAPABILITY_REUSE_FINALIZED_FIELDS
            if any(design_record.get(field) != record.get(field) for field in invariant_fields):
                errors.append(f"final record changed admitted design fields: {task_id}")
            if any(
                design_record.get(field) is not None
                for field in ("implementationHead", "reviewedHead", "pathStateDigest")
            ) or design_record.get("finalReview") != {
                "reviewer": None,
                "outcome": "pending",
                "evidence": "",
            }:
                errors.append(f"design-active predecessor contains premature final evidence: {task_id}")
            expected_digest, digest_error = _capability_path_state_digest(
                root,
                reviewed_head,
                record.get("mutablePaths", []),
            )
            if digest_error is not None or expected_digest != record.get("pathStateDigest"):
                errors.append(f"final record pathStateDigest differs from reviewed Git state: {task_id}")
            for mutable_path in record.get("mutablePaths", []):
                if _is_capability_reuse_governed_path(mutable_path):
                    batch_coverage.setdefault(mutable_path, []).append(task_id)
        if checkpoint is not None:
            batch_changes, batch_diff_error = _git_revision_changed_paths(
                root,
                checkpoint,
                reviewed_head,
            )
            if batch_diff_error is not None:
                errors.append(f"final batch governed diff could not be read: {batch_diff_error}")
            else:
                governed_batch_changes = {
                    path for path in batch_changes if _is_capability_reuse_governed_path(path)
                }
                if set(batch_coverage) != governed_batch_changes or any(
                    len(owners) != 1 for owners in batch_coverage.values()
                ):
                    errors.append(
                        f"final capability-reuse batch does not exactly cover its governed diff: {evidence_commit}"
                    )
        if len(errors) == batch_error_count:
            checkpoint = evidence_commit

    current_records = [
        record
        for relative, record in records_by_relative.items()
        if record.get("state") == "design-active"
        or (
            record.get("state") == "final-complete"
            and history.get(relative, _CapabilityRecordHistory(None, None, None, None)).first_final is None
        )
    ]
    if checkpoint is None:
        errors.append(
            "capability-reuse trusted initial evidence checkpoint is pending owner authority"
        )
        return
    bases = {
        record.get("integrationBase")
        for record in current_records
    }
    if current_records and bases != {checkpoint}:
        errors.append(
            f"current capability-reuse records must bind latest evidence checkpoint {checkpoint}"
        )

    for record in current_records:
        task_id = str(record.get("taskId", "<invalid>"))
        relative = f"{CAPABILITY_REUSE_CHANGE_RECORD_ROOT.as_posix()}/{task_id}.json"
        if record.get("state") == "design-active":
            committed, committed_error = _git_object(root, ["show", f"HEAD:{relative}"])
            if committed_error is None and committed == indexed_content.get(relative):
                errors.append(
                    f"design-active capability-reuse record cannot remain committed or be reused: {relative}"
                )
        elif record.get("state") == "final-complete":
            reviewed_head = subprocess.run(
                ["git", "rev-parse", "HEAD"],
                cwd=root,
                check=False,
                capture_output=True,
                text=True,
            ).stdout.strip()
            if record.get("reviewedHead") != reviewed_head:
                errors.append(f"uncommitted final record must bind current HEAD: {task_id}")
                continue
            design_content, design_error = _git_object(
                root,
                ["show", f"{reviewed_head}:{relative}"],
            )
            try:
                design_record = json.loads(design_content.decode("utf-8")) if design_error is None else None
            except (UnicodeDecodeError, json.JSONDecodeError):
                design_record = None
            if not isinstance(design_record, dict) or design_record.get("state") != "design-active":
                errors.append(f"final record requires its design-active predecessor at reviewedHead: {task_id}")
                continue
            invariant_fields = CAPABILITY_REUSE_RECORD_FIELDS - CAPABILITY_REUSE_FINALIZED_FIELDS
            if any(design_record.get(field) != record.get(field) for field in invariant_fields):
                errors.append(f"final record changed admitted design fields: {task_id}")
                continue
            if any(
                design_record.get(field) is not None
                for field in ("implementationHead", "reviewedHead", "pathStateDigest")
            ) or design_record.get("finalReview") != {
                "reviewer": None,
                "outcome": "pending",
                "evidence": "",
            }:
                errors.append(f"design-active predecessor contains premature final evidence: {task_id}")
                continue
            expected_digest, digest_error = _capability_path_state_digest(
                root,
                reviewed_head,
                record.get("mutablePaths", []),
            )
            if digest_error is not None or expected_digest != record.get("pathStateDigest"):
                errors.append(f"final record pathStateDigest differs from reviewed Git state: {task_id}")

    if any(record.get("state") == "final-complete" for record in current_records):
        post_review_changes, post_review_error = _git_changed_paths(root, "HEAD")
        post_review_untracked, post_review_untracked_error = _git_paths(
            root,
            ["ls-files", "--others", "--exclude-standard", "-z"],
        )
        if post_review_error is not None or post_review_untracked_error is not None:
            errors.append(
                "uncommitted final post-review diff could not be read: "
                f"{post_review_error or post_review_untracked_error}"
            )
        elif any(
            _is_capability_reuse_governed_path(path)
            for path in post_review_changes | post_review_untracked
        ):
            errors.append("uncommitted final records cannot cover governed changes after reviewedHead")

    tracked, tracked_error = _git_changed_paths(root, checkpoint)
    untracked, untracked_error = _git_paths(
        root,
        ["ls-files", "--others", "--exclude-standard", "-z"],
    )
    if tracked_error is not None or untracked_error is not None:
        errors.append(
            "capability-reuse governed checkpoint diff could not be read: "
            f"{tracked_error or untracked_error}"
        )
        return
    governed_changes = {
        value for value in tracked | untracked if _is_capability_reuse_governed_path(value)
    }
    coverage: dict[str, list[str]] = {}
    for record in current_records:
        task_id = str(record.get("taskId", "<invalid>"))
        for relative in record.get("mutablePaths", []):
            if not _is_capability_reuse_governed_path(relative):
                errors.append(f"current capability-reuse mutable path is not governed: {task_id}: {relative}")
                continue
            coverage.setdefault(relative, []).append(task_id)
            if relative not in governed_changes:
                errors.append(
                    f"current capability-reuse path is not in the current governed diff from checkpoint: "
                    f"{task_id}: {relative}"
                )
    for relative in sorted(governed_changes):
        owners = coverage.get(relative, [])
        if not owners:
            errors.append(
                f"governed changed path lacks a design-active/current-final "
                f"capability-reuse record: {relative}"
            )
        elif len(owners) > 1:
            errors.append(
                f"governed changed path has duplicate capability-reuse coverage: {relative}: {sorted(owners)}"
            )


def validate_agent_files(errors: list[str]) -> None:
    root_agents = ROOT / "AGENTS.md"
    if root_agents.is_file() and root_agents.stat().st_size > 16 * 1024:
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

    validate_capability_reuse_governance(ROOT, errors)

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
    if (ROOT / "docs/contracts/v0916-parity-certification-v1.json").is_file():
        try:
            git = GitAuthorityReader(ROOT)
            binding_head = git.last_change(
                "HEAD", "docs/contracts/v0916-parity-certification-v1.json"
            )
            if binding_head is None:
                raise ParityError("PARITY_AUTHORITY_MISMATCH")
            validate_repository_parity_authority_transfer(
                ROOT,
                head=binding_head,
                reader=git,
            )
        except ParityError as exc:
            errors.append(
                "v0.9.16 parity Git authority transfer failed: " f"{exc.code}"
            )
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

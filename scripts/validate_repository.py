"""Validate repository structure, contracts, policies, and reference provenance."""

from __future__ import annotations

import ast
import functools
import hashlib
import json
import os
import re
import stat
import subprocess
import sys
import tomllib
import unicodedata
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Any, Callable, Iterable
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
    "docs/governance/frozen-evidence-pins.json",
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


PREBUILT_CATALOG_GENERATOR = "eng/prebuilt-profile-catalog/NvtFwCombiner.PrebuiltProfileCatalogGenerator.csproj"
CATALOG_PROBE = "tests/NvtFwCombiner.CatalogProbe/NvtFwCombiner.CatalogProbe.csproj"

EXPECTED_PROJECTS = {
    PREBUILT_CATALOG_GENERATOR,
    CATALOG_PROBE,
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
    CATALOG_PROBE: {
        "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj",
        "src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj",
        PREBUILT_CATALOG_GENERATOR,
    },
    PREBUILT_CATALOG_GENERATOR: {
        "src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj",
    },
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
        PREBUILT_CATALOG_GENERATOR,
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
        PREBUILT_CATALOG_GENERATOR,
        "src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj",
        "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj",
        "src/NvtFwCombiner.Launcher/NvtFwCombiner.Launcher.csproj",
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
        CATALOG_PROBE,
        "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj",
        "src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj",
        "src/NvtFwCombiner.DistributionLauncher/NvtFwCombiner.DistributionLauncher.csproj",
        "src/NvtFwCombiner.VersionManagement.Application/NvtFwCombiner.VersionManagement.Application.csproj",
        "src/NvtFwCombiner.VersionManagement.Infrastructure/NvtFwCombiner.VersionManagement.Infrastructure.csproj",
        "tests/NvtFwCombiner.TestSupport/NvtFwCombiner.TestSupport.csproj",
    },
    "tests/NvtFwCombiner.Architecture.Tests/NvtFwCombiner.Architecture.Tests.csproj": {
        "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj",
        "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj",
        "src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj",
        "src/NvtFwCombiner.Contracts/NvtFwCombiner.Contracts.csproj",
        "src/NvtFwCombiner.Desktop/NvtFwCombiner.Desktop.csproj",
        "src/NvtFwCombiner.DistributionLauncher/NvtFwCombiner.DistributionLauncher.csproj",
        "src/NvtFwCombiner.Domain/NvtFwCombiner.Domain.csproj",
        "src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj",
        "src/NvtFwCombiner.Launcher/NvtFwCombiner.Launcher.csproj",
        "src/NvtFwCombiner.LauncherBootstrap/NvtFwCombiner.LauncherBootstrap.csproj",
        "src/NvtFwCombiner.Platform/NvtFwCombiner.Platform.csproj",
        "src/NvtFwCombiner.Presentation.Avalonia/NvtFwCombiner.Presentation.Avalonia.csproj",
        "src/NvtFwCombiner.Profiles/NvtFwCombiner.Profiles.csproj",
        "src/NvtFwCombiner.VersionManagement.Application/NvtFwCombiner.VersionManagement.Application.csproj",
        "src/NvtFwCombiner.VersionManagement.Infrastructure/NvtFwCombiner.VersionManagement.Infrastructure.csproj",
    },
    "tests/NvtFwCombiner.TestSupport/NvtFwCombiner.TestSupport.csproj": {
        "src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj",
        "src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj",
        "src/NvtFwCombiner.VersionManagement.Application/NvtFwCombiner.VersionManagement.Application.csproj",
    },
    "tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj": {
        "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj",
        "src/NvtFwCombiner.Desktop/NvtFwCombiner.Desktop.csproj",
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
USER_PROFILE_PATH = re.compile(
    r"(?i)(?<![A-Za-z0-9])[A-Za-z]:[\\/]+Users[\\/]+(?P<account>[^\\/\s\"'<>:]+)"
)
USER_PROFILE_PLACEHOLDERS = {"owner", "operator", "user", "username", "example", "public", "default", "fake"}
HISTORICAL_PRIVATE_PATH_EVIDENCE = {
    # SHA-256 of each existing source line, with its allowed private-path occurrence count.
    "docs/governance/change-records/DOC-HYGIENE-1113-PRIVATE-PATHS-01.json": {},
    "docs/governance/change-records/LAUNCHER-106-UI-01.json": {
        "9517b8a37942ff2a900a872a3ea2bc69bc0a3204e1ca5ecbf1c31b7f622c3e30": 1,
    },
    "docs/governance/change-records/UI-114-MEMORY-CARDS-31.json": {
        "e31d9bc74001f29d6788d642642ecd427ed5b89629b586df6d59722119d2a32f": 1,
    },
    "docs/governance/waivers/REL-110-FULL-VERIFY-OWNER-WAIVER-01.md": {
        "cb5fe6493ce33d4a19a050e3e2c5b7cfea8be44b62ce1f09dc4dc0989ce73f99": 2,
        "6480bfbb05c9afd98d5e60d5dbd21011376cd8632799545d446b7a655d126c8d": 1,
        "a330944505b62d34ca060af58ddc5c79dc8bcae72bf280923138ad9bdbb83e5c": 2,
    },
}


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


def repository_files(tracked: list[Path] | None = None) -> list[Path]:
    if tracked is None:
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


def validate_private_user_profile_paths(
    files: Iterable[Path], errors: list[str], *, root: Path = ROOT
) -> None:
    """Reject new private account paths while preserving exact historical occurrences."""
    for path in files:
        relative = path.relative_to(root).as_posix()
        remaining_evidence = HISTORICAL_PRIVATE_PATH_EVIDENCE.get(relative, {}).copy()
        try:
            content = path.read_bytes()
            if b"\0" in content:
                continue
            lines = content.decode("utf-8").splitlines()
        except (OSError, UnicodeDecodeError):
            continue
        for line_number, line in enumerate(lines, 1):
            private_count = sum(
                match.group("account").casefold() not in USER_PROFILE_PLACEHOLDERS
                for match in USER_PROFILE_PATH.finditer(line)
            )
            if private_count == 0:
                continue
            line_hash = hashlib.sha256(line.encode("utf-8")).hexdigest()
            allowed_count = remaining_evidence.get(line_hash, 0)
            if private_count > allowed_count:
                errors.append(f"private user-profile path in {relative}:{line_number}")
            else:
                remaining_evidence[line_hash] = allowed_count - private_count


PUBLIC_CONTENT_POLICY = "docs/governance/public-content-policy.json"
# The policy file holds only SHA-256 values, so plain restricted words stay out of the tree. The hashes are not
# secret: a dictionary reverses short words. This digest pins the whole file, so any change to the restricted
# words, the workbook pattern or the legacy allowance is a change to this validator (R3) and shows in review.
PUBLIC_CONTENT_POLICY_SHA256 = "8dea056bca36f48599bc88e635efbde737273737b4c5ad603ecb775412b47ba1"
_PUBLIC_WORD = re.compile(r"[A-Za-z0-9]+")
_PUBLIC_CAMEL_WORD = re.compile(r"[A-Z]+(?![a-z])|[A-Z]?[a-z]+|[0-9]+")
_PUBLIC_HEX_BLOB = re.compile(r"[0-9a-fA-F]{20,}")
_PUBLIC_LINE_BREAK = re.compile(r"\r\n|\r|\n")
# Filler code points that are not format characters or combining marks are removed as well: the Hangul fillers
# and the blank Braille pattern. They are written as numbers so no invisible character sits in this file.
_PUBLIC_FILLER_CHARS = frozenset(map(chr, (0x115F, 0x1160, 0x2800, 0x3164, 0xFFA0)))
_PUBLIC_IGNORED_CATEGORIES = frozenset({"Cf", "Mn", "Me"})
_PUBLIC_JOINER_GAP = re.compile(r"[_.\-]+")
_PUBLIC_MAX_JOINED_PARTS = 4
_PUBLIC_HEX64 = re.compile(r"[0-9a-f]{64}")
_PUBLIC_BINARY_SUFFIXES = frozenset(
    {
        ".7z", ".bin", ".bmp", ".dll", ".docx", ".exe", ".gif", ".gz", ".ico", ".jpeg", ".jpg", ".nupkg", ".otf",
        ".pdf", ".png", ".pptx", ".ttf", ".webp", ".woff", ".woff2", ".xls", ".xlsb", ".xlsm", ".xlsx", ".zip",
    }
)
_PUBLIC_POLICY_KEYS = {
    "schemaVersion", "kind", "workbookNamePattern", "restrictedTokenSha256", "legacyPathNames", "legacyLines",
}


@functools.lru_cache(maxsize=1 << 18)
def public_content_sha256(text: str) -> str:
    # Cached: a tree has a small vocabulary and the same word is hashed many times.
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def public_content_policy_digest(document: dict[str, Any]) -> str:
    """Digest of the canonical JSON form of the policy document."""
    return public_content_sha256(json.dumps(document, sort_keys=True, separators=(",", ":"), ensure_ascii=True))


def _reject_duplicate_policy_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    keys = [key for key, _ in pairs]
    if len(set(keys)) != len(keys):
        raise ValueError("duplicate key")
    return dict(pairs)


def load_public_content_policy(
    root: Path, errors: list[str], *, expected_digest: str = PUBLIC_CONTENT_POLICY_SHA256
) -> dict[str, Any] | None:
    """Load the policy; every failure is an error, never an exception."""
    path = root / PUBLIC_CONTENT_POLICY
    if not path.is_file():
        errors.append(f"missing public content policy: {PUBLIC_CONTENT_POLICY}")
        return None
    try:
        document = json.loads(
            path.read_text(encoding="utf-8"), object_pairs_hook=_reject_duplicate_policy_keys
        )
    except (OSError, ValueError) as exc:
        errors.append(f"invalid JSON {PUBLIC_CONTENT_POLICY}: {exc}")
        return None
    words = document.get("restrictedTokenSha256") if isinstance(document, dict) else None
    names = document.get("legacyPathNames") if isinstance(document, dict) else None
    lines = document.get("legacyLines") if isinstance(document, dict) else None
    valid = (
        isinstance(document, dict)
        and set(document) == _PUBLIC_POLICY_KEYS
        and document["schemaVersion"] == "1.0"
        and document["kind"] == "public-content-policy"
        and isinstance(document["workbookNamePattern"], str)
        and isinstance(words, list)
        and all(isinstance(item, str) and _PUBLIC_HEX64.fullmatch(item) for item in words)
        and words == sorted(set(words))
        and isinstance(names, dict)
        and all(
            _PUBLIC_HEX64.fullmatch(key) and isinstance(value, int) and not isinstance(value, bool) and value > 0
            for key, value in names.items()
        )
        and isinstance(lines, dict)
        and all(
            _PUBLIC_HEX64.fullmatch(key)
            and isinstance(value, dict)
            and value
            and all(
                _PUBLIC_HEX64.fullmatch(line_key)
                and isinstance(count, int)
                and not isinstance(count, bool)
                and count > 0
                for line_key, count in value.items()
            )
            for key, value in lines.items()
        )
    )
    if not valid:
        errors.append(f"invalid public content policy shape: {PUBLIC_CONTENT_POLICY}")
        return None
    try:
        re.compile(document["workbookNamePattern"])
    except re.error as exc:
        errors.append(f"invalid workbook name pattern in {PUBLIC_CONTENT_POLICY}: {exc}")
        return None
    actual_digest = public_content_policy_digest(document)
    if actual_digest != expected_digest:
        errors.append(
            f"{PUBLIC_CONTENT_POLICY} differs from the digest pinned in the validator; "
            f"review the change, then set PUBLIC_CONTENT_POLICY_SHA256 to {actual_digest}"
        )
        return None
    return document


def public_content_fold(text: str) -> str:
    """NFKD-fold text for matching and drop format characters, combining marks and filler characters."""
    if text.isascii():
        return text  # NFKD changes nothing in ASCII, and ASCII has no format or combining characters
    return "".join(
        char
        for char in unicodedata.normalize("NFKD", text)
        if unicodedata.category(char) not in _PUBLIC_IGNORED_CATEGORIES and char not in _PUBLIC_FILLER_CHARS
    )


@functools.lru_cache(maxsize=1 << 18)
def _public_token_parts(raw: str) -> tuple[str, tuple[str, ...]]:
    """The folded whole token and its folded camel-case parts; a single-case hex blob is not split."""
    is_hex_blob = _PUBLIC_HEX_BLOB.fullmatch(raw) is not None and raw in (raw.lower(), raw.upper())
    parts = [raw] if is_hex_blob else _PUBLIC_CAMEL_WORD.findall(raw)
    return raw.casefold(), tuple(part.casefold() for part in parts)


@functools.lru_cache(maxsize=8)
def _public_restricted_check(word_hashes: frozenset[str]) -> Callable[[str], bool]:
    """A memoized test "is this word restricted" for one set of word hashes."""

    @functools.lru_cache(maxsize=1 << 18)
    def is_restricted(word: str) -> bool:
        return word.isascii() and public_content_sha256(word) in word_hashes

    return is_restricted


def public_content_match_count(text: str, workbook_name: re.Pattern[str], word_hashes: set[str]) -> int:
    """Count distinct hits in one string: workbook names plus restricted words.

    Words are compared by SHA-256 of the lower-cased word, never by text. A token is split at camel-case
    boundaries and each part is tried alone and joined with up to three following parts when only ``_``, ``.``
    or ``-`` lies between tokens or when camel-case parts touch. Only single-case hexadecimal blobs of 20
    characters or more are tried whole without splitting, so hashes do not match by chance. Text is folded with
    NFKD first and format characters, combining marks and filler characters are dropped. Known limits: a word
    split over two lines, look-alike letters from another script, and words that are not ASCII are not found.
    """
    text = public_content_fold(text)
    hits = {match.group(0).casefold() for match in workbook_name.finditer(text)}
    is_restricted = _public_restricted_check(frozenset(word_hashes))
    window: list[tuple[str, bool]] = []  # recent parts of this line with "joined to the previous part"
    previous_end = -1
    for token in _PUBLIC_WORD.finditer(text):
        raw = token.group()
        start = token.start()
        joined = False
        if previous_end >= 0 and start > previous_end and text[previous_end] in "_.-":
            joined = _PUBLIC_JOINER_GAP.fullmatch(text, previous_end, start) is not None
        previous_end = token.end()
        whole, parts = _public_token_parts(raw)
        if is_restricted(whole):
            hits.add(whole)
        for index, part in enumerate(parts):
            joinable = joined if index == 0 else True
            if is_restricted(part):
                hits.add(part)
            combined = part
            link = joinable
            back = len(window) - 1
            while link and back >= 0 and len(window) - back < _PUBLIC_MAX_JOINED_PARTS:
                combined = window[back][0] + combined
                if is_restricted(combined):
                    hits.add(combined)
                link = window[back][1]
                back -= 1
            window.append((part, joinable))
    return len(hits)


def public_content_split_lines(text: str) -> list[str]:
    """Split on CR, LF and CRLF only, so line numbers agree with common editors."""
    lines = _PUBLIC_LINE_BREAK.split(text)
    if lines and lines[-1] == "":
        lines.pop()
    return lines


def public_content_text_lines(path: Path) -> tuple[list[str] | None, str | None]:
    """Decode a tracked file as text.

    Returns ``(lines, None)`` for text, ``(None, None)`` for a known binary format, and ``(None, reason)`` when a
    file that should be text cannot be read, so the caller reports it instead of skipping it.
    """
    if path.suffix.lower() in _PUBLIC_BINARY_SUFFIXES:
        return None, None
    try:
        content = path.read_bytes()
    except OSError:
        return None, "unreadable file"
    try:
        if content.startswith((b"\xff\xfe\x00\x00", b"\x00\x00\xfe\xff")):
            try:
                return public_content_split_lines(content.decode("utf-32")), None
            except UnicodeDecodeError:
                pass  # a UTF-16 file may start with a NUL character
        if content.startswith((b"\xff\xfe", b"\xfe\xff")):
            text = content.decode("utf-16")
            if "\0" in text[1:]:  # a damaged UTF-32 file read as UTF-16 shows NUL characters between letters
                return None, "undecodable UTF-16 or UTF-32 file"
            return public_content_split_lines(text), None
    except UnicodeDecodeError:
        return None, "undecodable UTF-16 or UTF-32 file"
    if b"\0" in content:
        return None, (
            "NUL byte in a file with an unlisted binary suffix; "
            "for a real binary format add the suffix to _PUBLIC_BINARY_SUFFIXES with governance-owner review"
        )
    try:
        return public_content_split_lines(content.decode("utf-8-sig")), None
    except UnicodeDecodeError:
        return public_content_split_lines(content.decode("latin-1")), None


def validate_public_content_names(
    files: Iterable[Path],
    errors: list[str],
    *,
    root: Path = ROOT,
    policy: dict[str, Any] | None = None,
) -> None:
    """Reject new workbook file names and restricted words in public file names and text.

    Old occurrences are allowed only by hash: a path name by its path hash, a line by the hash of its file path
    and of the whole line. An allowance that no longer matches is an error, so a cleaned line must leave the
    policy. The policy file itself is pinned by ``PUBLIC_CONTENT_POLICY_SHA256``.
    """
    if policy is None:
        policy = load_public_content_policy(root, errors)
        if policy is None:
            return
    workbook_name = re.compile(policy["workbookNamePattern"], re.IGNORECASE)
    word_hashes = set(policy["restrictedTokenSha256"])
    legacy_names: dict[str, int] = policy["legacyPathNames"]
    legacy_lines: dict[str, dict[str, int]] = policy["legacyLines"]
    seen_names: set[str] = set()
    seen_paths: set[str] = set()
    short_line_counts: dict[str, int] = {}  # short lines repeat across files (braces, usings, blanks)
    for path in files:
        relative = path.relative_to(root).as_posix()
        if relative == PUBLIC_CONTENT_POLICY:
            continue
        path_hash = public_content_sha256(relative)
        name_hits = public_content_match_count(relative, workbook_name, word_hashes)
        if name_hits:
            allowed_names = legacy_names.get(path_hash, 0)
            if name_hits > allowed_names:
                errors.append(f"restricted public name in file name {relative}")
            elif name_hits < allowed_names:
                errors.append(f"stale public content file name allowance {path_hash[:12]}")
            seen_names.add(path_hash)
        text_lines, problem = public_content_text_lines(path)
        allowances = legacy_lines.get(path_hash)
        seen_paths.add(path_hash)
        if problem:
            errors.append(f"cannot scan public content ({problem}): {relative}")
        if text_lines is None:
            if allowances and not problem:
                errors.append(f"unscannable file has a public content allowance: {relative}")
            continue
        remaining = dict(allowances or {})
        for line_number, text in enumerate(text_lines, 1):
            found = short_line_counts.get(text) if len(text) <= 120 else None
            if found is None:
                found = public_content_match_count(text, workbook_name, word_hashes)
                if len(text) <= 120:
                    short_line_counts[text] = found
            if found == 0:
                continue
            line_hash = public_content_sha256(text)
            allowed = remaining.get(line_hash, 0)
            if found > allowed:
                errors.append(f"restricted public name in {relative}:{line_number}")
            else:
                remaining[line_hash] = allowed - found
        stale = sorted(key[:12] for key, count in remaining.items() if count)
        if stale:
            errors.append(
                f"stale public content allowance in {relative} (line hashes {', '.join(stale)}); "
                f"remove the cleaned lines from {PUBLIC_CONTENT_POLICY} and delete the file key when no line is left"
            )
    for path_hash in sorted(set(legacy_lines) - seen_paths):
        errors.append(f"stale public content allowance for a removed file {path_hash[:12]}")
    for path_hash in sorted(set(legacy_names) - seen_names):
        errors.append(f"stale public content file name allowance {path_hash[:12]}")


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
    build_props = (ROOT / "Directory.Build.props").read_text(encoding="utf-8")
    if f"文件版本：`{version}`" not in spec:
        errors.append("VERSION and SPEC.md document version disagree")
    if f"Specification package version: `{version}`" not in report:
        errors.append("VERSION and verification-report version disagree")
    if f"## [{version}]" not in changelog:
        errors.append("VERSION has no changelog section")
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


VENDORED_CORE_SOURCE = "Vendor/Core/UiEventRunner.cs"
VENDORED_CORE_SOURCE_CONSUMERS = frozenset(
    {
        "src/NvtFwCombiner.Presentation.Avalonia/NvtFwCombiner.Presentation.Avalonia.csproj",
        "src/NvtFwCombiner.DistributionLauncher/NvtFwCombiner.DistributionLauncher.csproj",
    }
)


def is_approved_vendored_core_include(relative: str, include: str) -> bool:
    """Accept the one byte-pinned Core source file in its two named consumers.

    The pin itself is checked by the UiSmoke source-consumption tests; this rule only keeps the
    exception exact, so any other file outside the measured tree is still rejected.
    """

    if relative not in VENDORED_CORE_SOURCE_CONSUMERS or any(token in include for token in ("$(", "@(", "%", ";", "*", "?")):
        return False
    target = PurePosixPath(
        os.path.normpath(
            (PurePosixPath(relative).parent / include.replace(chr(92), "/")).as_posix()
        ).replace(chr(92), "/")
    )
    return target.as_posix() == VENDORED_CORE_SOURCE


def validate_production_source_ownership(
    relative: str, project_root: ET.Element, errors: list[str]
) -> None:
    """Keep production source physical, owned, and inside the measured tree."""

    if not relative.startswith("src/"):
        return
    for element in project_root.iter("Compile"):
        include = element.attrib.get("Include")
        if include and not is_approved_vendored_core_include(relative, include):
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
        if relative in VENDORED_CORE_SOURCE_CONSUMERS and source_path.resolve() == (
            repository_root / VENDORED_CORE_SOURCE
        ).resolve():
            continue
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


def validate_prebuilt_generator_references(relative: str, root: ET.Element, errors: list[str]) -> None:
    """Allow only the declared build-only host edge and the two test consumers."""
    for reference in root.iter("ProjectReference"):
        target = normalize_project_reference(ROOT / relative, reference.attrib["Include"])
        if target != PREBUILT_CATALOG_GENERATOR:
            continue
        if relative in {CATALOG_PROBE, "tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj"}:
            continue
        if (relative != "src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj"
                or reference.get("ReferenceOutputAssembly") != "false"
                or reference.get("PrivateAssets") != "all"):
            errors.append(f"generator reference must remain the declared build-only dependency: {relative}")


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
        validate_prebuilt_generator_references(relative, root, errors)


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
        "repository-scripts",
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


def validate_ci_dotnet_verifier_contract(verifier: str, errors: list[str]) -> None:
    """Require the sole exact-assembly CI evidence owner and reject its predecessor."""

    required_dotnet_coverage_markers = (
        "CI_DOTNET_SHARDS",
        "def local_dotnet_vstest_command(",
        '        "--no-restore",',
        '"--Collect:XPlat Code Coverage",',
        'f"--ResultsDirectory:{results_directory}",',
        '"--Logger:trx;LogFileName=test-results.trx",',
        "adapter_path = resolve_coverlet_adapter_path(ROOT)",
        "def finalize_ci_dotnet_evidence(",
    )
    if any(marker not in verifier for marker in required_dotnet_coverage_markers):
        errors.append(
            "canonical verifier must own the closed .NET shard map, unfiltered "
            "exact-assembly coverage/TRX collection, and evidence finalization"
        )
    obsolete_project_execution_markers = (
        "def ci_dotnet_test_command(",
        '"--collect:XPlat Code Coverage",',
        '"--results-directory",',
    )
    if any(marker in verifier for marker in obsolete_project_execution_markers):
        errors.append(
            "canonical verifier must not restore the obsolete project-level "
            ".NET test execution owner"
        )
    if verifier.count('"--evaluated-source-ownership-only"') != 1:
        errors.append(
            "canonical .NET verifier must own exactly one restored source-ownership check"
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
    validate_ci_dotnet_verifier_contract(verifier, errors)
    dotnet_job = ci[ci.index("  dotnet:") :] if "  dotnet:" in ci else ""
    if "fetch-depth: 0" not in dotnet_job:
        errors.append("CI dotnet job must fetch the fixed coverage baseline revision")
    rehearsal = (ROOT / ".github/workflows/release-rehearsal.yml").read_text(
        encoding="utf-8"
    )
    if "fetch-depth: 0" not in rehearsal:
        errors.append(
            "release rehearsal workflow must fetch the fixed coverage baseline revision"
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
        "Derive release request from merge commit",
        "NFC_AUTOMATIC: ${{ github.event_name == 'workflow_run' }}",
        "if ($env:NFC_AUTOMATIC -eq 'true') { '--automatic' }",
        "collect-release-request",
        "--version $version --github-output $env:GITHUB_OUTPUT @automaticArgument",
        "--github-output $env:GITHUB_OUTPUT",
        "Collect and validate final PR review/check evidence",
        "collect-review-snapshot",
        "--pull-request $env:NFC_PULL_REQUEST",
        "--workflow-ref $env:NFC_WORKFLOW_REF",
        "$mainSha -ne $env:NFC_WORKFLOW_SHA",
        "$sourceSha = $mainSha",
        "source-branch=main",
        "validate-release-floor",
        "release-eligibility:",
        "NFC_WORKFLOW_REF: ${{ github.ref }}",
        "$env:NFC_RELEASE_POLICY validate-promotion-source",
        "environment: release",
        "Create or verify immutable annotated tag",
    )
    if any(marker not in release for marker in required_release_markers):
        errors.append(
            "release workflow must use protected-main source and authority, a release floor, eligibility gate, and a protected human environment gate"
        )
    try:
        release_workflow = yaml.safe_load(release)
    except yaml.YAMLError as error:
        errors.append(f"cannot parse release workflow: {error}")
        release_workflow = {}
    if not isinstance(release_workflow, dict):
        release_workflow = {}
    events = release_workflow.get("on", release_workflow.get(True, {}))
    if not isinstance(events, dict) or events.get("workflow_run") != {
        "workflows": ["ci"],
        "types": ["completed"],
        "branches": ["main"],
    }:
        errors.append("release workflow must trigger on completed main CI runs")
    dispatch = events.get("workflow_dispatch") if isinstance(events, dict) else None
    inputs = dispatch.get("inputs") if isinstance(dispatch, dict) else None
    dry_run = inputs.get("dry_run") if isinstance(inputs, dict) else None
    if (
        not isinstance(inputs, dict)
        or set(inputs) != {"dry_run"}
        or not isinstance(dry_run, dict)
        or dry_run.get("required") is not True
        or dry_run.get("default") is not False
        or dry_run.get("type") != "boolean"
    ):
        errors.append(
            "release manual dispatch must have only the dry_run fallback input"
        )
    jobs = release_workflow.get("jobs")
    candidate = jobs.get("candidate") if isinstance(jobs, dict) else None
    gate = candidate.get("if") if isinstance(candidate, dict) else None
    if not isinstance(gate, str):
        gate = ""
    expected_gate = (
        "${{ github.ref == 'refs/heads/main' && (github.event_name == 'workflow_dispatch' || "
        "(github.event.workflow_run.conclusion == 'success' && "
        "github.event.workflow_run.event == 'push' && "
        "github.event.workflow_run.head_branch == 'main' && "
        "github.event.workflow_run.head_sha == github.sha && "
        "github.sha == github.workflow_sha)) }}"
    )
    if gate != expected_gate:
        errors.append(
            "release candidate trigger must require successful exact-main push CI"
        )
    if (
        "owner_self_approval_exception" in release
        or "NFC_OWNER_SELF_APPROVAL_EXCEPTION" in release
        or "--owner-self-approval-exception" in release
    ):
        errors.append("automatic release must not use a self-approval exception")
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


FROZEN_EVIDENCE_PIN_FILE = "docs/governance/frozen-evidence-pins.json"
FROZEN_EVIDENCE_PATHS = (
    ("docs/governance/change-records", "tree"),
    ("docs/governance/external-authority-attestations", "tree"),
    ("docs/governance/waivers", "tree"),
    ("docs/governance/trusted-initial-capability-checkpoint.v1.json", "blob"),
)
FROZEN_CHECKPOINT_MODE = "100644"


def validate_frozen_evidence_pins(root: Path, errors: list[str]) -> None:
    """Prove the frozen snapshot at HEAD, in the index and on disk without history."""
    def require(condition: bool, path: str, reason: str) -> None:
        if not condition:
            raise ValueError(f"{path}: {reason}")

    def unique_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            require(key not in result, FROZEN_EVIDENCE_PIN_FILE, f"duplicate key {key}")
            result[key] = value
        return result

    environment = {key: value for key, value in os.environ.items()
                   if not key.upper().startswith("GIT_")}
    environment.update(GIT_NO_REPLACE_OBJECTS="1", GIT_OPTIONAL_LOCKS="0",
                       GIT_LITERAL_PATHSPECS="1")

    def git(*arguments: str) -> str:
        result = subprocess.run(
            ["git", "--no-replace-objects", *arguments], cwd=root, env=environment,
            capture_output=True, check=True,
        )
        return result.stdout.decode("utf-8", errors="strict")

    def frozen_path(path: str) -> bool:
        folded = path.casefold()
        return any(folded == name.casefold() or folded.startswith(name.casefold() + "/")
                   for name, _ in FROZEN_EVIDENCE_PATHS)

    def entry_stat(entry: os.DirEntry[str], relative: str, directory: bool) -> None:
        info = entry.stat(follow_symlinks=False)
        require(not entry.is_symlink() and not (
            getattr(info, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT
        ), relative, "symlink or reparse point")
        require(stat.S_ISDIR(info.st_mode) if directory else stat.S_ISREG(info.st_mode),
                relative, "unexpected filesystem type")
        if not directory and os.name != "nt":
            require(not info.st_mode & 0o111, relative, "executable file")

    def exact_entry(relative: str) -> os.DirEntry[str]:
        parent = root
        components = relative.split("/")
        for offset, component in enumerate(components):
            with os.scandir(parent) as entries:
                matches = [entry for entry in entries if entry.name.casefold() == component.casefold()]
            current = "/".join(components[:offset + 1])
            require(len(matches) == 1 and matches[0].name == component,
                    current, "missing, case-renamed or case-aliased component")
            entry = matches[0]
            directory = offset < len(components) - 1 or relative != FROZEN_EVIDENCE_PATHS[-1][0]
            entry_stat(entry, current, directory)
            parent = Path(entry.path)
        return entry

    try:
        document = json.loads((root / FROZEN_EVIDENCE_PIN_FILE).read_text(encoding="utf-8"),
                              object_pairs_hook=unique_object)
        require(isinstance(document, dict) and set(document) == {
            "schemaVersion", "authority", "frozenAtBase", "pins"
        }, FROZEN_EVIDENCE_PIN_FILE, "invalid document keys")
        require(type(document["schemaVersion"]) is int and document["schemaVersion"] == 1
                and document["authority"] == "docs/adr/0080-governance-reset.md#evidence-and-admission",
                FROZEN_EVIDENCE_PIN_FILE, "invalid schema or authority")
        require(isinstance(document["frozenAtBase"], str) and
                re.fullmatch(r"[0-9a-f]{40}", document["frozenAtBase"]) is not None,
                FROZEN_EVIDENCE_PIN_FILE, "invalid frozenAtBase")
        pins = document["pins"]
        require(isinstance(pins, list) and len(pins) == 4, FROZEN_EVIDENCE_PIN_FILE, "invalid pin count")
        for pin, (path, kind) in zip(pins, FROZEN_EVIDENCE_PATHS):
            require(isinstance(pin, dict) and set(pin) == {"path", "type", "id"}
                    and pin["path"] == path and pin["type"] == kind
                    and isinstance(pin["id"], str) and re.fullmatch(r"[0-9a-f]{40}", pin["id"]) is not None,
                    path, "invalid frozen evidence pin")
        repository = git("rev-parse", "--show-toplevel", "--is-inside-work-tree", "--show-object-format").splitlines()
        require(len(repository) == 3 and os.path.samefile(repository[0], root)
                and repository[1:] == ["true", "sha1"], FROZEN_EVIDENCE_PIN_FILE, "not the expected SHA-1 work tree")
        tree: dict[str, tuple[str, str, str]] = {}
        raw_tree = git("ls-tree", "-r", "-t", "-z", "--full-tree", "HEAD")
        require(raw_tree.endswith("\0"), FROZEN_EVIDENCE_PIN_FILE, "malformed tree response")
        for row in raw_tree[:-1].split("\0"):
            metadata, path = row.split("\t", 1)
            mode, kind, oid = metadata.split(" ")
            require(path not in tree and re.fullmatch(r"[0-9a-f]{40}", oid) is not None,
                    path, "malformed tree entry")
            tree[path] = (mode, kind, oid)
        expected: dict[str, str] = {}
        for pin in pins:
            path, kind, oid = pin["path"], pin["type"], pin["id"]
            mode = "040000" if kind == "tree" else FROZEN_CHECKPOINT_MODE
            actual = tree.get(path)
            require(actual == (mode, kind, oid), path,
                    f"HEAD differs from frozen pin: pinned={mode} {kind} {oid}; "
                    f"actual={' '.join(actual) if actual is not None else 'missing'}")
            if kind == "blob":
                expected[path] = oid
        for path, (mode, kind, oid) in tree.items():
            if not frozen_path(path):
                continue
            roots = [name for name, _ in FROZEN_EVIDENCE_PATHS]
            require(path in roots or any(path.startswith(name + "/") for name in roots[:-1]),
                    path, "case alias in HEAD")
            if path not in roots:
                require((mode, kind) == ("100644", "blob"), path, "non-regular frozen tree entry")
                expected[path] = oid
        folded: set[str] = set()
        for path in expected:
            require(path.casefold() not in folded, path, "case alias in HEAD")
            folded.add(path.casefold())
        found: set[str] = set()
        raw_index = git("ls-files", "-z", "-s", "-v")
        require(raw_index.endswith("\0"), FROZEN_EVIDENCE_PIN_FILE, "malformed index response")
        for row in raw_index[:-1].split("\0"):
            metadata, path = row.split("\t", 1)
            tag, mode, oid, stage = metadata.split(" ")
            if not frozen_path(path.rstrip("/")):
                continue
            require(path in expected and path not in found and tag == "H" and mode == "100644"
                    and stage == "0" and oid == expected[path], path, "index differs from frozen snapshot")
            found.add(path)
        missing = set(expected) - found
        require(not missing, sorted(missing)[0] if missing else "index", "missing frozen index entry")
        disk: dict[str, Path] = {}
        for relative, kind in FROZEN_EVIDENCE_PATHS:
            entry = exact_entry(relative)
            if kind == "blob":
                disk[relative] = Path(entry.path)
                continue
            with os.scandir(entry.path) as children:
                seen: set[str] = set()
                for child in children:
                    path = relative + "/" + child.name
                    require(child.name.casefold() not in seen, path, "case-aliased sibling")
                    seen.add(child.name.casefold())
                    canonical = next((name for name in expected if name.casefold() == path.casefold()), path)
                    require(path in expected, canonical, f"extra or case-renamed frozen entry: {path}")
                    entry_stat(child, path, False)
                    disk[path] = Path(child.path)
        missing = set(expected) - set(disk)
        require(not missing, sorted(missing)[0] if missing else "worktree", "missing frozen file")
        for path, file in disk.items():
            require(git_blob_sha1(file) == expected[path], path, "raw bytes differ from frozen snapshot")
    except (OSError, ValueError, TypeError, KeyError, subprocess.SubprocessError) as error:
        errors.append(f"frozen evidence pins: {error}")


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


def validate_historical_parity_authority(errors: list[str]) -> None:
    if (ROOT / "docs/contracts/v0916-parity-certification-v1.json").is_file():
        try:
            # Frozen evidence pins validate this immutable record before this adapter.
            # Current workflow projections may change the plan after its frozen H2.
            binding_record = load_json(
                ROOT / "docs/governance/change-records/"
                "RELEASE-111-PARITY-AUTHORITY-TRANSFER-09.json",
                errors,
            )
            binding_head = (
                binding_record.get("reviewedHead")
                if isinstance(binding_record, dict)
                else None
            )
            if (
                not isinstance(binding_head, str)
                or re.fullmatch(r"[0-9a-f]{40}", binding_head) is None
            ):
                raise ParityError("PARITY_AUTHORITY_MISMATCH")
            git = GitAuthorityReader(ROOT)
            validate_repository_parity_authority_transfer(
                ROOT,
                head=binding_head,
                reader=git,
            )
        except ParityError as exc:
            errors.append(
                "v0.9.16 parity Git authority transfer failed: " f"{exc.code}"
            )


def validate_claude_projections(tracked: list[Path], errors: list[str]) -> None:
    from sync_derived import claude_projection_provider

    expected = set(claude_projection_provider(ROOT).outputs)
    actual = {
        path.relative_to(ROOT).as_posix() for path in tracked
        if path.relative_to(ROOT).as_posix().startswith((".claude/skills/", ".claude/agents/"))
    }
    if actual != expected:
        errors.append(f"tracked Claude projections must match expected set: missing {sorted(expected - actual)}, stale {sorted(actual - expected)}")


def validate() -> list[str]:
    errors: list[str] = []
    errors.extend(validate_code_size_policy(ROOT))
    tracked = _git_tracked_paths()
    files = repository_files(tracked)
    validate_claude_projections(tracked if tracked is not None else files, errors)
    validate_required_files(errors)
    validate_forbidden_tracked_content(files, errors)
    validate_private_user_profile_paths(files, errors)
    validate_public_content_names(files, errors)
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
    validate_frozen_evidence_pins(ROOT, errors)
    validate_historical_parity_authority(errors)
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

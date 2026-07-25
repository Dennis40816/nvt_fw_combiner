"""Validate coverage collector and denominator-integrity configuration."""

from __future__ import annotations

import json
import re
import tomllib
import xml.etree.ElementTree as element_tree
from pathlib import Path, PurePosixPath
from typing import Any, Iterable

APPROVED_SDK_ANALYZER_PATHS = frozenset(
    path.casefold()
    for path in (
        "analyzers/Microsoft.CodeAnalysis.NetAnalyzers.dll",
        "analyzers/Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll",
        "codestyle/cs/Microsoft.CodeAnalysis.CodeStyle.dll",
        "codestyle/cs/Microsoft.CodeAnalysis.CodeStyle.Fixes.dll",
        "codestyle/cs/Microsoft.CodeAnalysis.CSharp.CodeStyle.dll",
        "codestyle/cs/Microsoft.CodeAnalysis.CSharp.CodeStyle.Fixes.dll",
    )
)
APPROVED_PACKAGE_ANALYZERS = frozenset(
    (
        package.casefold(),
        version,
        relative_path.casefold(),
    )
    for package, version, relative_path in (
        (
            "Avalonia",
            "12.0.5",
            "analyzers/dotnet/cs/Avalonia.Analyzers.CSharp.dll",
        ),
        (
            "Avalonia",
            "12.0.5",
            "analyzers/dotnet/cs/Avalonia.Analyzers.CodeFixes.CSharp.dll",
        ),
        (
            "Avalonia",
            "12.0.5",
            "analyzers/dotnet/cs/Avalonia.Analyzers.VisualBasic.dll",
        ),
        (
            "Avalonia",
            "12.0.5",
            "analyzers/dotnet/cs/Avalonia.Generators.dll",
        ),
        (
            "CommunityToolkit.Mvvm",
            "8.4.2",
            "analyzers/dotnet/roslyn5.0/cs/CommunityToolkit.Mvvm.CodeFixers.dll",
        ),
        (
            "CommunityToolkit.Mvvm",
            "8.4.2",
            "analyzers/dotnet/roslyn5.0/cs/CommunityToolkit.Mvvm.SourceGenerators.dll",
        ),
        (
            "Humanizer.Core",
            "3.0.1",
            "analyzers/dotnet/roslyn3.8/cs/Humanizer.Analyzers.dll",
        ),
    )
)
SDK_ANALYZER_TARGET_PATH = ("targets/Microsoft.NET.Sdk.Analyzers.targets").casefold()
APPROVED_COVERLET_FORMAT_SETTING = (
    "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration."
    "Format=json,cobertura"
)


def validate_coverage_collector_pin(
    baseline: dict[str, Any], errors: list[str], root: Path
) -> None:
    """Keep the baseline's coverage collector identities reproducible in CI."""

    dotnet_collection = baseline["collection"]["dotnet"]
    collector = dotnet_collection["collector"]
    expected_version = dotnet_collection["version"]
    collector_package_versions = [
        element
        for element in element_tree.parse(root / "Directory.Packages.props")
        .getroot()
        .iter("PackageVersion")
        if any(
            element.attrib.get(attribute, "").casefold() == collector.casefold()
            for attribute in ("Include", "Update")
        )
    ]
    if (
        len(collector_package_versions) != 1
        or collector_package_versions[0].attrib.get("Include", "").casefold()
        != collector.casefold()
        or collector_package_versions[0].attrib.get("Version") != expected_version
    ):
        errors.append(
            "Directory.Packages.props must exactly pin the baseline .NET "
            f"coverage collector: {collector} {expected_version}"
        )

    build_root = element_tree.parse(root / "Directory.Build.props").getroot()
    collector_references: list[tuple[element_tree.Element, element_tree.Element]] = []
    for item_group in build_root.iter("ItemGroup"):
        for reference in item_group.findall("PackageReference"):
            if reference.attrib.get("Include", "").casefold() == collector.casefold():
                collector_references.append((item_group, reference))
    valid_reference = len(collector_references) == 1
    if valid_reference:
        item_group, reference = collector_references[0]
        valid_reference = (
            item_group.attrib.get("Condition", "").replace(" ", "")
            == "'$(IsTestProject)'=='true'"
            and reference.attrib.get("PrivateAssets") == "all"
            and "Version" not in reference.attrib
            and "VersionOverride" not in reference.attrib
            and "IncludeAssets" not in reference.attrib
            and "ExcludeAssets" not in reference.attrib
        )
    if not valid_reference:
        errors.append(
            "Directory.Build.props must provide exactly one test-only baseline "
            f"coverage collector reference with PrivateAssets=all: {collector}"
        )

    python_collection = baseline["collection"]["python"]
    pyproject = tomllib.loads(
        (root / "tools/crc-worker/pyproject.toml").read_text(encoding="utf-8")
    )
    development_dependencies = (
        pyproject.get("project", {}).get("optional-dependencies", {}).get("dev")
    )
    expected = {
        f"coverage=={python_collection['coveragePyVersion']}",
        f"pytest-cov=={python_collection['pytestCovVersion']}",
    }
    collector_dependencies = (
        {
            dependency
            for dependency in development_dependencies
            if isinstance(dependency, str)
            and dependency.casefold().startswith(("coverage", "pytest-cov"))
        }
        if isinstance(development_dependencies, list)
        else set()
    )
    if collector_dependencies != expected:
        errors.append(
            "tools/crc-worker/pyproject.toml must exactly pin the approved "
            "Python coverage collector versions"
        )


def validate_coverage_exclusion_policy(
    root: Path, files: Iterable[Path | str], errors: list[str]
) -> None:
    """Reject unapproved ways to shrink production coverage denominators."""

    msbuild_properties = {
        name.casefold(): name
        for name in (
            "Include",
            "Exclude",
            "ExcludeByFile",
            "ExcludeByAttribute",
            "RunSettingsFilePath",
            "SkipAutoProps",
        )
    }
    invocation_pattern = re.compile(
        r"(?:^|[\s\"'])(?:-p:|/p:|--property:)"
        r"(?:Include|Exclude|ExcludeByFile|ExcludeByAttribute|SkipAutoProps)\s*=",
        re.IGNORECASE,
    )
    for item in files:
        source_path = Path(item)
        if source_path.is_absolute():
            try:
                relative = source_path.resolve().relative_to(root.resolve()).as_posix()
            except ValueError:
                continue
        else:
            relative = str(item).replace("\\", "/")
            source_path = root / Path(*PurePosixPath(relative).parts)
        path = PurePosixPath(relative)
        if path.suffix.casefold() == ".runsettings":
            errors.append(
                "coverage runsettings require an explicit reviewed coverage-contract "
                f"exception: {relative}"
            )
            continue
        inspect_source = (
            path.suffix.casefold() == ".cs"
            and bool(path.parts)
            and path.parts[0] == "src"
        )
        inspect_msbuild = path.suffix.casefold() in {".csproj", ".props", ".targets"}
        inspect_invocation = relative == "scripts/verify.py" or relative.startswith(
            ".github/workflows/"
        )
        if not inspect_source and not inspect_msbuild and not inspect_invocation:
            continue
        text = source_path.read_text(encoding="utf-8-sig")
        if inspect_source and "ExcludeFromCodeCoverage" in text:
            errors.append(
                "production coverage exclusion requires an explicit reviewed "
                f"coverage-contract exception: {relative} -> ExcludeFromCodeCoverage"
            )
        if inspect_msbuild:
            try:
                document = element_tree.fromstring(text)
            except element_tree.ParseError:
                document = None
            for property_group in () if document is None else document.iter():
                if property_group.tag.rsplit("}", 1)[-1] != "PropertyGroup":
                    continue
                for element in property_group:
                    name = element.tag.rsplit("}", 1)[-1]
                    normalized_name = name.casefold()
                    value = (element.text or "").strip()
                    if (
                        normalized_name in msbuild_properties
                        and value
                        and not (
                            normalized_name == "skipautoprops"
                            and value.casefold() == "false"
                        )
                    ):
                        errors.append(
                            "coverage filter configuration requires an explicit reviewed "
                            f"coverage-contract exception: {relative} -> "
                            f"{msbuild_properties[normalized_name]}"
                        )
        inspected_invocation = text.replace(APPROVED_COVERLET_FORMAT_SETTING, "")
        if inspect_invocation and (
            "--settings" in inspected_invocation
            or "DataCollectionRunSettings" in inspected_invocation
            or invocation_pattern.search(inspected_invocation)
        ):
            errors.append(
                "coverage invocation filter requires an explicit reviewed "
                f"coverage-contract exception: {relative}"
            )


def is_approved_sdk_analyzer(analyzer: dict[str, Any], msbuild_sdks_path: Path) -> bool:
    """Accept only the selected SDK's known built-in analyzer payloads."""

    full_path = analyzer.get("FullPath")
    defining_project = analyzer.get("DefiningProjectFullPath")
    if (
        analyzer.get("IsImplicitlyDefined") != "true"
        or not isinstance(full_path, str)
        or not isinstance(defining_project, str)
    ):
        return False
    try:
        sdk_root = (msbuild_sdks_path / "Microsoft.NET.Sdk").resolve(strict=True)
        analyzer_path = Path(full_path).resolve(strict=True)
        target_path = Path(defining_project).resolve(strict=True)
        analyzer_relative = analyzer_path.relative_to(sdk_root).as_posix().casefold()
        target_relative = target_path.relative_to(sdk_root).as_posix().casefold()
    except (OSError, RuntimeError, ValueError):
        return False
    return (
        analyzer_path.is_file()
        and target_path.is_file()
        and analyzer_relative in APPROVED_SDK_ANALYZER_PATHS
        and target_relative == SDK_ANALYZER_TARGET_PATH
    )


def is_approved_package_analyzer(
    analyzer: dict[str, Any], repository_root: Path
) -> bool:
    """Accept only the exact analyzer assets already owned by pinned packages."""

    package_id = analyzer.get("NuGetPackageId")
    package_version = analyzer.get("NuGetPackageVersion")
    full_path = analyzer.get("FullPath")
    if not all(
        isinstance(value, str) and value
        for value in (package_id, package_version, full_path)
    ):
        return False
    try:
        resolved_repository_root = repository_root.resolve(strict=True)
        package_root = (
            repository_root / ".packages" / package_id.casefold() / package_version
        ).resolve(strict=True)
        package_root.relative_to(resolved_repository_root)
        analyzer_path = Path(full_path).resolve(strict=True)
        relative_path = analyzer_path.relative_to(package_root).as_posix().casefold()
    except (OSError, RuntimeError, ValueError):
        return False
    return (
        analyzer_path.is_file()
        and (package_id.casefold(), package_version, relative_path)
        in APPROVED_PACKAGE_ANALYZERS
    )


def validate_evaluated_test_coverage_collector(
    relative: str,
    items: dict[str, list[dict[str, Any]]],
    collector: str,
    repository_root: Path,
    errors: list[str],
) -> None:
    """Require each restored test project to receive the central collector once."""

    references = [
        reference
        for reference in items["PackageReference"]
        if str(reference.get("Identity", "")).casefold() == collector.casefold()
    ]
    expected_source = (repository_root / "Directory.Build.props").resolve()
    valid = len(references) == 1
    if valid:
        reference = references[0]
        defining_project = reference.get("DefiningProjectFullPath")
        try:
            is_central = (
                isinstance(defining_project, str)
                and Path(defining_project).resolve() == expected_source
            )
        except OSError:
            is_central = False
        valid = (
            reference.get("PrivateAssets") == "all"
            and is_central
            and not reference.get("Version")
            and not reference.get("VersionOverride")
            and not reference.get("IncludeAssets")
            and not reference.get("ExcludeAssets")
        )
    if not valid:
        errors.append(
            "test project must receive exactly one centrally defined coverage collector "
            f"with PrivateAssets=all: {relative} -> {collector}"
        )


def validate_restored_test_coverage_collector_version(
    relative: str,
    assets_file: Path,
    collector: str,
    expected_version: str,
    errors: list[str],
) -> None:
    """Require restored assets to contain exactly the approved collector version."""

    try:
        document = json.loads(assets_file.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        errors.append(f"could not read restored package assets for {relative}: {exc}")
        return
    libraries = document.get("libraries") if isinstance(document, dict) else None
    if not isinstance(libraries, dict):
        errors.append(f"restored package assets are invalid for {relative}")
        return
    resolved_versions = [
        identity.partition("/")[2]
        for identity in libraries
        if isinstance(identity, str)
        and identity.partition("/")[0].casefold() == collector.casefold()
    ]
    if resolved_versions != [expected_version]:
        errors.append(
            "test project must resolve the baseline coverage collector version: "
            f"{relative} -> {collector} {expected_version}, got {resolved_versions}"
        )

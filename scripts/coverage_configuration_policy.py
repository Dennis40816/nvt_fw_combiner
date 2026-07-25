"""Validate coverage collector and denominator-integrity configuration."""

from __future__ import annotations

import re
import tomllib
import xml.etree.ElementTree as element_tree
from pathlib import Path, PurePosixPath
from typing import Any, Iterable


def validate_coverage_collector_pin(
    baseline: dict[str, Any], errors: list[str], root: Path
) -> None:
    """Keep the baseline's coverage collector identities reproducible in CI."""

    dotnet_collection = baseline["collection"]["dotnet"]
    collector = dotnet_collection["collector"]
    expected_version = dotnet_collection["version"]
    package_versions = [
        element
        for element in element_tree.parse(root / "Directory.Packages.props")
        .getroot()
        .iter("PackageVersion")
        if element.attrib.get("Include", "").casefold() == collector.casefold()
    ]
    if (
        len(package_versions) != 1
        or package_versions[0].attrib.get("Version") != expected_version
    ):
        errors.append(
            "Directory.Packages.props must exactly pin the baseline .NET "
            f"coverage collector: {collector} {expected_version}"
        )

    build_root = element_tree.parse(root / "Directory.Build.props").getroot()
    collector_references: list[
        tuple[element_tree.Element, element_tree.Element]
    ] = []
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
            for property_group in (() if document is None else document.iter()):
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
        if inspect_invocation and (
            "--settings" in text
            or "DataCollectionRunSettings" in text
            or invocation_pattern.search(text)
        ):
            errors.append(
                "coverage invocation filter requires an explicit reviewed "
                f"coverage-contract exception: {relative}"
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

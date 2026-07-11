"""Focused contract invariants used by the canonical repository validator."""

from __future__ import annotations

import re
from pathlib import Path
from typing import Any, Callable

LoadJson = Callable[[Path, list[str]], Any | None]


def _variant_with_const(
    definition: dict[str, Any], property_name: str, expected: str
) -> dict[str, Any]:
    for variant in definition.get("oneOf", []):
        actual = variant.get("properties", {}).get(property_name, {}).get("const")
        if actual == expected:
            return variant
    return {}


def validate_v2_contract_model(
    root: Path, load_json: LoadJson, errors: list[str]
) -> None:
    """Validate locked cross-schema rules that JSON meta-schema checks cannot express."""
    family = load_json(root / "docs/contracts/firmware-family-v1.schema.json", errors)
    bundle = load_json(root / "docs/contracts/profile-bundle-v1.schema.json", errors)
    evidence = load_json(
        root / "docs/contracts/firmware-evidence-manifest-v1.schema.json", errors
    )
    if isinstance(family, dict):
        _validate_firmware_family(family, errors)
    if isinstance(bundle, dict):
        _validate_profile_bundle(bundle, errors)
    if isinstance(evidence, dict):
        _validate_evidence_manifest(evidence, errors)


def _validate_firmware_family(family: dict[str, Any], errors: list[str]) -> None:
    required = set(family.get("required", []))
    canonical = {"regionSets", "metadataSets", "imageMaps", "factAliases", "evidenceRefs"}
    for key in canonical - required:
        errors.append(f"firmware-family schema does not require canonical field: {key}")

    root_properties = family.get("properties", {})
    for forbidden in {"operations", "processorStages", "promotion"} & set(root_properties):
        errors.append(f"firmware-family schema must not grant workflow policy: {forbidden}")

    definitions = family.get("$defs", {})
    image_map = definitions.get("imageMap", {})
    map_required = set(image_map.get("required", []))
    map_safety = {"coveragePolicy", "applicability", "regionSetIds", "metadataSetIds"}
    for key in map_safety - map_required:
        errors.append(f"firmware-family imageMap does not require safety field: {key}")
    coverage = image_map.get("properties", {}).get("coveragePolicy", {}).get("const")
    if coverage != "complete-with-explicit-gaps":
        errors.append("firmware-family imageMap must require complete explicit gap coverage")

    applicability = definitions.get("applicability", {})
    if "capacityBytes" not in set(applicability.get("required", [])):
        errors.append("firmware-family map applicability must require one exact capacityBytes")

    region_properties = definitions.get("region", {}).get("properties", {})
    owner_values = set(region_properties.get("owner", {}).get("enum", []))
    kind_values = set(region_properties.get("kind", {}).get("enum", []))
    if "customer" not in owner_values:
        errors.append("firmware-family region owner must classify customer information")
    for kind in {"ctrlram", "customer-information", "reserved", "unmapped"} - kind_values:
        errors.append(f"firmware-family region kind is missing physical classification: {kind}")

    addressed_required = set(definitions.get("addressedRange", {}).get("required", []))
    if "addressSpaceId" not in addressed_required:
        errors.append("firmware-family absolute/search ranges must name an address space")

    marker_selection = definitions.get("markerSelection", {})
    selection_kinds = {
        variant.get("properties", {}).get("kind", {}).get("const")
        for variant in marker_selection.get("oneOf", [])
    }
    if selection_kinds != {"unique", "terminal-match"}:
        errors.append("firmware-family marker selection must be unique or terminal-match")
    terminal = _variant_with_const(marker_selection, "kind", "terminal-match")
    terminal_required = set(terminal.get("required", []))
    if not {"terminal", "expectedMatchCount"}.issubset(terminal_required):
        errors.append("firmware-family terminal marker selection must require direction and count")

    locators = definitions.get("locator", {})
    absolute = _variant_with_const(locators, "kind", "absolute-range")
    marker = _variant_with_const(locators, "kind", "marker-relative")
    absolute_ref = absolute.get("properties", {}).get("range", {}).get("$ref")
    marker_ref = marker.get("properties", {}).get("searchRange", {}).get("$ref")
    if absolute_ref != "#/$defs/addressedRange" or marker_ref != "#/$defs/addressedRange":
        errors.append("firmware-family absolute and marker search ranges must be addressed")

    structure = definitions.get("metadataStructure", {})
    conditionals = structure.get("allOf", [])
    assertion_minimum = 0
    if conditionals:
        assertion_minimum = (
            conditionals[0]
            .get("then", {})
            .get("properties", {})
            .get("assertions", {})
            .get("minItems", 0)
        )
    if assertion_minimum < 1:
        errors.append("firmware-family marker structures must require an assertion")

    alias = definitions.get("aliasApplicability", {})
    alias_properties = set(alias.get("properties", {}))
    alias_required = set(alias.get("required", []))
    discriminators = {"capacityBytes", "commonFirmwareCategoryIds", "metadataPredicates"}
    for key in discriminators - alias_properties:
        errors.append(f"firmware-family alias applicability is missing map discriminator: {key}")
    if "capacityBytes" not in alias_required:
        errors.append("firmware-family alias applicability must require exact capacityBytes")


def _validate_profile_bundle(bundle: dict[str, Any], errors: list[str]) -> None:
    required = set(bundle.get("required", []))
    for key in {"contentHash", "trustAnchorBindingId", "entries"} - required:
        errors.append(f"profile-bundle schema does not require trust field: {key}")

    algorithm = bundle.get("properties", {}).get("hashAlgorithm", {}).get("const")
    if algorithm != "sha256-rfc8785-entry-array-v1":
        errors.append("profile-bundle content hash must use locked RFC 8785 encoding")

    entry = bundle.get("$defs", {}).get("entry", {})
    path_pattern = entry.get("properties", {}).get("path", {}).get("pattern")
    if not isinstance(path_pattern, str):
        errors.append("profile-bundle entry path must declare a closed pattern")
    else:
        unsafe_paths = {"families/../profiles/a.json", "families//a.json", "families/./a.json"}
        for unsafe_path in unsafe_paths:
            if re.fullmatch(path_pattern, unsafe_path):
                errors.append(f"profile-bundle entry path permits traversal shape: {unsafe_path}")
    if len(entry.get("allOf", [])) != 5:
        errors.append("profile-bundle entry kind must select its canonical directory")


def _validate_evidence_manifest(evidence: dict[str, Any], errors: list[str]) -> None:
    required = set(evidence.get("required", []))
    for key in {"sourceArtifacts", "facts", "reviews"} - required:
        errors.append(f"firmware-evidence schema does not require canonical field: {key}")
    if "promotionAssessments" in evidence.get("properties", {}):
        errors.append("firmware-evidence schema must not own profile promotion state")

    source = evidence.get("$defs", {}).get("sourceArtifact", {})
    path_pattern = source.get("properties", {}).get("repositoryPath", {}).get("pattern")
    if isinstance(path_pattern, str) and re.fullmatch(path_pattern, "docs/../private.bin"):
        errors.append("firmware-evidence repository path permits traversal")

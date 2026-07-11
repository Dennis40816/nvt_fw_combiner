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


def _discriminator_values(definition: dict[str, Any], property_name: str) -> set[str]:
    values: set[str] = set()
    for variant in definition.get("oneOf", []):
        schemas = [variant, *variant.get("allOf", [])]
        for schema in schemas:
            property_schema = schema.get("properties", {}).get(property_name, {})
            if isinstance(property_schema.get("const"), str):
                values.add(property_schema["const"])
            values.update(property_schema.get("enum", []))
    return values


def _contains_const(node: Any, expected: str) -> bool:
    if isinstance(node, dict):
        return node.get("const") == expected or any(
            _contains_const(value, expected) for value in node.values()
        )
    if isinstance(node, list):
        return any(_contains_const(value, expected) for value in node)
    return False


def validate_v2_contract_model(
    root: Path, load_json: LoadJson, errors: list[str]
) -> None:
    """Validate locked cross-schema rules that JSON meta-schema checks cannot express."""
    family = load_json(root / "docs/contracts/firmware-family-v1.schema.json", errors)
    bundle = load_json(root / "docs/contracts/profile-bundle-v1.schema.json", errors)
    evidence = load_json(
        root / "docs/contracts/firmware-evidence-manifest-v1.schema.json", errors
    )
    profile = load_json(root / "docs/contracts/composition-profile-v2.schema.json", errors)
    saved_rule = load_json(
        root / "docs/contracts/saved-composition-rule-v2.schema.json", errors
    )
    if isinstance(family, dict):
        _validate_firmware_family(family, errors)
    if isinstance(bundle, dict):
        _validate_profile_bundle(bundle, errors)
    if isinstance(evidence, dict):
        _validate_evidence_manifest(evidence, errors)
    if isinstance(profile, dict):
        _validate_composition_profile(profile, errors)
    if isinstance(saved_rule, dict):
        _validate_saved_rule(saved_rule, errors)


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


def _validate_composition_profile(profile: dict[str, Any], errors: list[str]) -> None:
    required = set(profile.get("required", []))
    canonical = {
        "promotion",
        "mapBinding",
        "inputSlots",
        "spaces",
        "views",
        "metadataBindings",
        "regionAccessRules",
        "operations",
        "validations",
        "processorStages",
        "output",
    }
    for key in canonical - required:
        errors.append(f"composition-profile-v2 does not require canonical field: {key}")
    root_properties = profile.get("properties", {})
    for forbidden in {"regions", "image", "addressSpaces"} & set(root_properties):
        errors.append(f"composition-profile-v2 must not duplicate physical map field: {forbidden}")

    definitions = profile.get("$defs", {})
    map_required = set(definitions.get("mapBinding", {}).get("required", []))
    for key in {"familyContentHash", "mapIds", "requiredRegionIds"} - map_required:
        errors.append(f"composition-profile-v2 map binding does not require: {key}")

    input_slot = definitions.get("inputSlot", {})
    if "artifactClass" not in set(input_slot.get("required", [])):
        errors.append("composition-profile-v2 input slots must require artifactClass")
    length_kinds = _discriminator_values(definitions.get("lengthRule", {}), "kind")
    firmware_length_kinds = {
        "exact-resolved-map-capacity",
        "normal-dp-extract-with-warning",
        "tp-maximum-256k",
    }
    for kind in firmware_length_kinds - length_kinds:
        errors.append(f"composition-profile-v2 is missing firmware length policy: {kind}")
    tp_length = _variant_with_const(definitions.get("lengthRule", {}), "kind", "tp-maximum-256k")
    tp_maximum = tp_length.get("properties", {}).get("maximumBytes", {}).get("const")
    if tp_maximum != 262144:
        errors.append("composition-profile-v2 TP maximum must be exactly 256 KiB")

    space_kinds = _discriminator_values(definitions.get("space", {}), "kind")
    if space_kinds != {"input-artifact", "work-buffer", "output-image"}:
        errors.append("composition-profile-v2 spaces must separate immutable input/work/output")
    initializer_kinds = _discriminator_values(definitions.get("initializer", {}), "kind")
    if initializer_kinds != {"blank", "clone"}:
        errors.append("composition-profile-v2 mutable initializers must be blank or clone")
    clone = _variant_with_const(definitions.get("initializer", {}), "kind", "clone")
    clone_required = set(clone.get("required", []))
    if "sourceSlotId" not in clone_required or "sourceSpaceId" in clone_required:
        errors.append("composition-profile-v2 clone must reference an immutable source slot")
    spaces = profile.get("properties", {}).get("spaces", {})
    if spaces.get("maxContains") != 1:
        errors.append("composition-profile-v2 must allow exactly one output-image space")
    if "spaceId" in definitions.get("output", {}).get("properties", {}):
        errors.append("composition-profile-v2 output naming must not select another space")

    root_conditions = profile.get("allOf", [])
    pad_condition = next(
        (item for item in root_conditions if _contains_const(item.get("if", {}), "pad-shorter")),
        {},
    )
    pad_then = pad_condition.get("then", {}).get("properties", {})
    if pad_then.get("processorStages", {}).get("maxItems") != 0:
        errors.append("composition-profile-v2 padding must forbid processor stages")
    truncate_condition = next(
        (
            item
            for item in root_conditions
            if _contains_const(item.get("if", {}), "truncate-ctrlram")
        ),
        {},
    )
    if not _contains_const(truncate_condition.get("then", {}), "ctrlram-replace"):
        errors.append("composition-profile-v2 truncation must require CtrlRAM Replace")

    operation_kinds = _discriminator_values(definitions.get("operation", {}), "kind")
    expected_operations = {
        "copy-range",
        "replace-range",
        "fill-range",
        "patch-scalar",
        "transform-scalar",
        "run-processor",
    }
    if operation_kinds != expected_operations:
        errors.append("composition-profile-v2 operation algebra differs from the locked primitives")

    validation_kinds = _discriminator_values(definitions.get("validation", {}), "kind")
    required_validations = {
        "metadata-equality", "pid-sanity", "reject-metadata-byte-pattern",
    }
    for kind in required_validations - validation_kinds:
        errors.append(f"composition-profile-v2 is missing metadata validation kind: {kind}")

    processor = definitions.get("processorStage", {})
    processor_kinds = _discriminator_values(processor, "kind")
    if processor_kinds != {"crc-worker-v1", "legacy-combiner-v1"}:
        errors.append("composition-profile-v2 processor stages must use the closed v1 union")
    for variant in processor.get("oneOf", []):
        properties = variant.get("properties", {})
        required = set(variant.get("required", []))
        for key in {"authority", "purpose", "integrityDisposition", "failurePolicy"} - required:
            errors.append(f"composition-profile-v2 processor stage does not require: {key}")
        for forbidden in {"parameters", "path", "command", "arguments", "script"} & set(properties):
            errors.append(f"composition-profile-v2 processor stage permits unsafe field: {forbidden}")
    crc = _variant_with_const(processor, "kind", "crc-worker-v1")
    crc_properties = crc.get("properties", {})
    if crc_properties.get("authority", {}).get("const") != "calculate":
        errors.append("composition-profile-v2 CRC worker must have calculate authority")
    if crc_properties.get("allowedWriteViewIds", {}).get("maxItems") != 0:
        errors.append("composition-profile-v2 CRC worker must not have write authority")
    legacy = _variant_with_const(processor, "kind", "legacy-combiner-v1")
    legacy_properties = legacy.get("properties", {})
    if legacy_properties.get("authority", {}).get("const") != "transform":
        errors.append("composition-profile-v2 legacy combiner must have transform authority")
    if legacy_properties.get("allowedWriteViewIds", {}).get("minItems") != 1:
        errors.append("composition-profile-v2 legacy combiner must require a write view")
    none_condition = next(
        (item for item in legacy.get("allOf", []) if _contains_const(item.get("if", {}), "none")),
        {},
    )
    if not _contains_const(none_condition.get("then", {}), "relocation"):
        errors.append("composition-profile-v2 integrity none must require relocation purpose")


def _validate_saved_rule(saved_rule: dict[str, Any], errors: list[str]) -> None:
    required = set(saved_rule.get("required", []))
    canonical = {
        "parentBinding",
        "promotion",
        "mappingFragments",
        "accessEnvelope",
        "validationRuleIds",
        "processorStageIds",
        "evidenceRefs",
    }
    for key in canonical - required:
        errors.append(f"saved-composition-rule-v2 does not require canonical field: {key}")

    root_properties = saved_rule.get("properties", {})
    for forbidden in {"processorStages", "output", "operations"} & set(root_properties):
        errors.append(f"saved-composition-rule-v2 must not redefine parent policy: {forbidden}")

    definitions = saved_rule.get("$defs", {})
    parent_required = set(definitions.get("parentBinding", {}).get("required", []))
    parent_hashes = {"bundleContentHash", "profileContentHash", "familyContentHash"}
    for key in parent_hashes - parent_required:
        errors.append(f"saved-composition-rule-v2 parent binding does not require hash: {key}")
    for key in {"familyId", "familyVersion", "mapId"} - parent_required:
        errors.append(f"saved-composition-rule-v2 parent binding does not require identity: {key}")
    if "mapContentHash" in definitions.get("parentBinding", {}).get("properties", {}):
        errors.append("saved-composition-rule-v2 must not require an unverifiable map hash")

    mapping = definitions.get("mappingFragment", {}).get("properties", {})
    for key in {"targetRegionId", "targetOffset", "sourceRange"} - set(mapping):
        errors.append(f"saved-composition-rule-v2 mapping is missing constrained field: {key}")
    for forbidden in {"targetAddressSpaceId", "targetRange", "processorInvocation"} & set(mapping):
        errors.append(f"saved-composition-rule-v2 mapping permits unsafe field: {forbidden}")

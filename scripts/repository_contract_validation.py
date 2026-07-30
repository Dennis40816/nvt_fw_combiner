"""Focused contract invariants used by the canonical repository validator."""

from __future__ import annotations

import copy
import re
from pathlib import Path
from typing import Any, Callable

LoadJson = Callable[[Path, list[str]], Any | None]
HEX_BYTES_PATTERN = r"^(?:[0-9a-f]{2})+(?![\s\S])"
PARTIAL_MASK_PATTERN = (
    r"^(?!0+(?![\s\S]))(?!f+(?![\s\S]))(?:[0-9a-f]{2})+(?![\s\S])"
)


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
    if isinstance(family, dict) and isinstance(profile, dict):
        _validate_contract_guard_self_tests(family, profile, errors)


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
    structure_required = set(structure.get("required", []))
    if "artifactBindingId" not in structure_required:
        errors.append("firmware-family metadata structures must require an artifact binding")
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

    predicate = definitions.get("metadataPredicate", {})
    predicate_required = set(predicate.get("required", []))
    if "metadataStructureId" not in predicate_required:
        errors.append("firmware-family metadata predicates must require a structure id")

    metadata_field = definitions.get("metadataField", {})
    field_variants = metadata_field.get("oneOf", [])
    field_encodings = {
        variant.get("properties", {}).get("encoding", {}).get("const")
        for variant in field_variants
    }
    expected_encodings = {
        "bytes",
        "printable-ascii",
        "unsigned-integer",
        "signed-integer",
    }
    if len(field_variants) != len(expected_encodings) or field_encodings != expected_encodings:
        errors.append("firmware-family metadata fields must use the closed typed encoding set")
    common_required = {"fieldId", "offset", "widthBytes", "encoding"}
    common_properties = common_required
    for encoding in expected_encodings:
        variant = _variant_with_const(metadata_field, "encoding", encoding)
        properties = set(variant.get("properties", {}))
        required = set(variant.get("required", []))
        if variant.get("additionalProperties") is not False:
            errors.append(f"firmware-family metadata field variant must be closed: {encoding}")
        if not common_required.issubset(required):
            errors.append(
                f"firmware-family metadata field is missing required identity: {encoding}"
            )
        property_definitions = variant.get("properties", {})
        if property_definitions.get("fieldId", {}).get("$ref") != "#/$defs/id":
            errors.append(f"firmware-family metadata field id must use canonical ids: {encoding}")
        if property_definitions.get("offset") != {"type": "integer", "minimum": 0}:
            errors.append(f"firmware-family metadata field offset must be nonnegative: {encoding}")
        width = property_definitions.get("widthBytes", {})
        if encoding in {"unsigned-integer", "signed-integer"}:
            expected_properties = common_properties | {"byteOrder"}
            if encoding == "unsigned-integer":
                expected_properties |= {"bitSlice"}
            if properties != expected_properties:
                errors.append(
                    f"firmware-family integer metadata properties must be closed: {encoding}"
                )
            if required != common_required | {"byteOrder"}:
                errors.append(
                    f"firmware-family integer metadata required fields changed: {encoding}"
                )
            if width != {"type": "integer", "minimum": 1, "maximum": 4}:
                errors.append(
                    f"firmware-family integer metadata width must be one to four bytes: {encoding}"
                )
            byte_orders = set(
                variant.get("properties", {}).get("byteOrder", {}).get("enum", [])
            )
            if byte_orders != {"little", "big"}:
                errors.append(
                    f"firmware-family integer metadata byte order must be little or big: {encoding}"
                )
        else:
            if properties != common_properties or required != common_required:
                errors.append(
                    f"firmware-family byte/text metadata properties must be closed: {encoding}"
                )
            if width != {"type": "integer", "minimum": 1}:
                errors.append(
                    f"firmware-family byte/text metadata width must be positive: {encoding}"
                )
    unsigned = _variant_with_const(metadata_field, "encoding", "unsigned-integer")
    unsigned_slice_ref = unsigned.get("properties", {}).get("bitSlice", {}).get("$ref")
    if unsigned_slice_ref != "#/$defs/bitSlice":
        errors.append("firmware-family unsigned metadata must permit checked bit slices")
    signed = _variant_with_const(metadata_field, "encoding", "signed-integer")
    if "bitSlice" in signed.get("properties", {}):
        errors.append("firmware-family signed metadata cannot declare bit slices")

    bit_slice_properties = definitions.get("bitSlice", {}).get("properties", {})
    bit_slice = definitions.get("bitSlice", {})
    if bit_slice.get("additionalProperties") is not False:
        errors.append("firmware-family metadata bit slices must be closed")
    if set(bit_slice.get("required", [])) != {"leastSignificantBit", "bitCount"}:
        errors.append("firmware-family metadata bit slices must require start and count")
    if set(bit_slice_properties) != {"leastSignificantBit", "bitCount"}:
        errors.append("firmware-family metadata bit slices have unexpected properties")
    if (
        bit_slice_properties.get("leastSignificantBit")
        != {"type": "integer", "minimum": 0, "maximum": 31}
        or bit_slice_properties.get("bitCount")
        != {"type": "integer", "minimum": 1, "maximum": 32}
    ):
        errors.append("firmware-family metadata bit slices must stay inside a 32-bit carrier")

    scalar_variants = definitions.get("scalar", {}).get("oneOf", [])
    scalar_types = {variant.get("type") for variant in scalar_variants}
    integer_scalars = [variant for variant in scalar_variants if variant.get("type") == "integer"]
    string_scalars = [variant for variant in scalar_variants if variant.get("type") == "string"]
    if (
        len(scalar_variants) != 2
        or scalar_types != {"integer", "string"}
        or integer_scalars != [{"type": "integer"}]
        or string_scalars != [{"type": "string", "minLength": 1}]
    ):
        errors.append(
            "firmware-family predicate scalars must be contextual integer or string values"
        )

    assertions = definitions.get("byteAssertion", {}).get("oneOf", [])
    exact_assertions = [
        variant
        for variant in assertions
        if "maskHex" not in variant.get("properties", {})
    ]
    masked_assertions = [
        variant
        for variant in assertions
        if "maskHex" in variant.get("properties", {})
    ]
    assertion_common = {"offset", "expectedHex"}
    if (
        len(assertions) != 2
        or len(exact_assertions) != 1
        or len(masked_assertions) != 1
    ):
        errors.append("firmware-family byte assertions must separate exact and partial masks")
    else:
        exact = exact_assertions[0]
        masked = masked_assertions[0]
        if (
            exact.get("additionalProperties") is not False
            or set(exact.get("required", [])) != assertion_common
            or set(exact.get("properties", {})) != assertion_common
            or exact.get("properties", {}).get("offset")
            != {"type": "integer", "minimum": 0}
            or exact.get("properties", {}).get("expectedHex", {}).get("$ref")
            != "#/$defs/hexBytes"
        ):
            errors.append("firmware-family exact byte assertions must omit masks")
        if (
            masked.get("additionalProperties") is not False
            or set(masked.get("required", [])) != assertion_common | {"maskHex"}
            or set(masked.get("properties", {})) != assertion_common | {"maskHex"}
            or masked.get("properties", {}).get("offset")
            != {"type": "integer", "minimum": 0}
            or masked.get("properties", {}).get("expectedHex", {}).get("$ref")
            != "#/$defs/hexBytes"
            or masked.get("properties", {}).get("maskHex", {}).get("type") != "string"
            or masked.get("properties", {}).get("maskHex", {}).get("pattern")
            != PARTIAL_MASK_PATTERN
        ):
            errors.append(
                "firmware-family partial assertion masks must be closed and nontrivial"
            )

    hex_bytes_pattern = definitions.get("hexBytes", {}).get("pattern")
    if hex_bytes_pattern != HEX_BYTES_PATTERN:
        errors.append("firmware-family hex bytes must use canonical true-end matching")
    else:
        _validate_pattern_vectors(
            "firmware-family hex bytes",
            hex_bytes_pattern,
            accepted=("00", "0f", "00ff"),
            rejected=("", "0", "0F", "0f\n", "0f\r", "0f\r\n", "0f\u2028", "0f\u2029"),
            errors=errors,
        )
    if masked_assertions:
        mask_pattern = (
            masked_assertions[0].get("properties", {}).get("maskHex", {}).get("pattern")
        )
        if mask_pattern == PARTIAL_MASK_PATTERN:
            _validate_pattern_vectors(
                "firmware-family partial mask",
                mask_pattern,
                accepted=("0f", "f0", "00ff", "ff00"),
                rejected=(
                    "00",
                    "ff",
                    "0000",
                    "ffff",
                    "0F",
                    "0f\n",
                    "0f\r",
                    "0f\r\n",
                    "0f\u2028",
                    "0f\u2029",
                ),
                errors=errors,
            )

    alias = definitions.get("aliasApplicability", {})
    alias_properties = set(alias.get("properties", {}))
    alias_required = set(alias.get("required", []))
    discriminators = {"capacityBytes", "commonFirmwareCategoryIds", "metadataPredicates"}
    for key in discriminators - alias_properties:
        errors.append(f"firmware-family alias applicability is missing map discriminator: {key}")
    if "capacityBytes" not in alias_required:
        errors.append("firmware-family alias applicability must require exact capacityBytes")


def _validate_contract_guard_self_tests(
    family: dict[str, Any],
    profile: dict[str, Any],
    errors: list[str],
) -> None:
    """Prove the focused schema guards reject representative contract weakening."""

    family_probes: list[tuple[str, dict[str, Any]]] = []

    mutated = copy.deepcopy(family)
    unsigned = _variant_with_const(
        mutated["$defs"]["metadataField"], "encoding", "unsigned-integer"
    )
    unsigned["required"].remove("widthBytes")
    family_probes.append(("unsigned required width", mutated))

    mutated = copy.deepcopy(family)
    unsigned = _variant_with_const(
        mutated["$defs"]["metadataField"], "encoding", "unsigned-integer"
    )
    unsigned["properties"]["widthBytes"]["minimum"] = 0
    family_probes.append(("positive unsigned width", mutated))

    mutated = copy.deepcopy(family)
    mutated["$defs"]["bitSlice"]["additionalProperties"] = True
    family_probes.append(("closed bit slice", mutated))

    mutated = copy.deepcopy(family)
    mutated["$defs"]["bitSlice"]["required"].remove("bitCount")
    family_probes.append(("required bit count", mutated))

    mutated = copy.deepcopy(family)
    mutated["$defs"]["bitSlice"]["properties"]["bitCount"]["minimum"] = 0
    family_probes.append(("positive bit count", mutated))

    mutated = copy.deepcopy(family)
    metadata_field = mutated["$defs"]["metadataField"]
    metadata_field["oneOf"].append(
        copy.deepcopy(_variant_with_const(metadata_field, "encoding", "bytes"))
    )
    family_probes.append(("unique field variants", mutated))

    mutated = copy.deepcopy(family)
    string_scalar = next(
        variant
        for variant in mutated["$defs"]["scalar"]["oneOf"]
        if variant.get("type") == "string"
    )
    string_scalar["minLength"] = 0
    family_probes.append(("nonempty family scalar text", mutated))

    mutated = copy.deepcopy(family)
    exact_assertion = next(
        variant
        for variant in mutated["$defs"]["byteAssertion"]["oneOf"]
        if "maskHex" not in variant["properties"]
    )
    exact_assertion["properties"]["maskHex"] = {"$ref": "#/$defs/hexBytes"}
    family_probes.append(("exact assertion mask omission", mutated))

    mutated = copy.deepcopy(family)
    masked_assertion = next(
        variant
        for variant in mutated["$defs"]["byteAssertion"]["oneOf"]
        if "maskHex" in variant["properties"]
    )
    masked_assertion["properties"]["maskHex"]["pattern"] = "^(?:[0-9a-f]{2})+$"
    family_probes.append(("nontrivial partial assertion mask", mutated))

    for name, document in family_probes:
        _assert_guard_rejects(name, document, _validate_firmware_family, errors)

    mutated_profile = copy.deepcopy(profile)
    profile_string_scalar = next(
        variant
        for variant in mutated_profile["$defs"]["scalar"]["oneOf"]
        if variant.get("type") == "string"
    )
    profile_string_scalar["minLength"] = 0
    _assert_guard_rejects(
        "nonempty profile scalar text",
        mutated_profile,
        _validate_composition_profile,
        errors,
    )


def _assert_guard_rejects(
    name: str,
    document: dict[str, Any],
    validator: Callable[[dict[str, Any], list[str]], None],
    errors: list[str],
) -> None:
    probe_errors: list[str] = []
    validator(document, probe_errors)
    if not probe_errors:
        errors.append(f"repository contract guard self-test did not reject: {name}")


def _validate_pattern_vectors(
    name: str,
    pattern: str,
    *,
    accepted: tuple[str, ...],
    rejected: tuple[str, ...],
    errors: list[str],
) -> None:
    for value in accepted:
        if re.search(pattern, value) is None:
            errors.append(f"{name} rejected canonical vector: {value!r}")
    for value in rejected:
        if re.search(pattern, value) is not None:
            errors.append(f"{name} accepted noncanonical vector: {value!r}")


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
    if definitions.get("hexBytes", {}).get("pattern") != HEX_BYTES_PATTERN:
        errors.append("composition-profile hex bytes must use canonical true-end matching")
    scalar_variants = definitions.get("scalar", {}).get("oneOf", [])
    scalar_types = {variant.get("type") for variant in scalar_variants}
    integer_scalars = [variant for variant in scalar_variants if variant.get("type") == "integer"]
    string_scalars = [variant for variant in scalar_variants if variant.get("type") == "string"]
    if (
        len(scalar_variants) != 2
        or scalar_types != {"integer", "string"}
        or integer_scalars != [{"type": "integer"}]
        or string_scalars != [{"type": "string", "minLength": 1}]
    ):
        errors.append(
            "composition-profile metadata scalars must align with typed firmware fields"
        )
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
    if "imageInitialization" not in root_properties:
        errors.append("saved-composition-rule-v2 is missing General Merge image initialization")

    definitions = saved_rule.get("$defs", {})
    initializer = definitions.get("generalMergeInitialization", {})
    initializer_required = set(initializer.get("required", []))
    if {"kind", "capacity"} - initializer_required:
        errors.append(
            "saved-composition-rule-v2 General Merge initializer must require kind and capacity"
        )
    fill = initializer.get("properties", {}).get("fillByte", {})
    if (
        fill.get("minimum") != 0
        or fill.get("maximum") != 255
        or fill.get("default") != 0
    ):
        errors.append(
            "saved-composition-rule-v2 fill byte must cover 0..255 with default 0"
        )
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

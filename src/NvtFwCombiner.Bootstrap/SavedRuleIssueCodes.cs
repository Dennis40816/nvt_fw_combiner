namespace NvtFwCombiner.Bootstrap;

/// <summary>Stable saved-rule validation issue codes used by Bootstrap CLI contracts.</summary>
public static class SavedRuleIssueCodes
{
    /// <summary>Saved-rule JSON file was not found.</summary>
    public const string FileNotFound = "saved-rule.file-not-found";

    /// <summary>Saved-rule JSON could not be parsed.</summary>
    public const string JsonInvalid = "saved-rule.json.invalid";

    /// <summary>Saved-rule JSON could not be read from storage.</summary>
    public const string FileReadFailed = "saved-rule.file-read-failed";

    /// <summary>Saved-rule JSON root is not an object.</summary>
    public const string RootInvalid = "saved-rule.root.invalid";

    /// <summary>Saved-rule schema version is unsupported.</summary>
    public const string SchemaVersionUnsupported = "saved-rule.schema-version.unsupported";

    /// <summary>Saved Rule v2 General Merge initializer is required.</summary>
    public const string InitializerRequired = "saved-rule.initializer.required";

    /// <summary>Saved Rule v2 initializer is forbidden for General Replace.</summary>
    public const string InitializerForbidden = "saved-rule.initializer.forbidden";

    /// <summary>Saved Rule v2 initializer object or kind is invalid.</summary>
    public const string InitializerInvalid = "saved-rule.initializer.invalid";

    /// <summary>Saved Rule v2 General Merge capacity is invalid or unsupported.</summary>
    public const string InitializerCapacityInvalid =
        "saved-rule.initializer.capacity-invalid";

    /// <summary>Saved Rule v2 General Merge fill is outside one byte.</summary>
    public const string InitializerFillByteInvalid =
        "saved-rule.initializer.fill-byte-invalid";

    /// <summary>Saved Rule v2 does not satisfy the complete canonical contract schema.</summary>
    public const string V2ContractInvalid = "saved-rule.v2.contract-invalid";

    /// <summary>Saved Rule v2 broadens or references facts outside its exact trusted parent.</summary>
    public const string V2ParentNarrowingInvalid =
        "saved-rule.v2.parent-narrowing-invalid";

    /// <summary>Saved-rule compatibility object is required.</summary>
    public const string CompatibilityRequired = "saved-rule.compatibility.required";

    /// <summary>Saved-rule IC id does not use the expected catalog form.</summary>
    public const string IcIdInvalid = "saved-rule.ic-id.invalid";

    /// <summary>Saved-rule composition kind and source experience do not match.</summary>
    public const string ExperienceKindMismatch = "saved-rule.experience-kind.mismatch";

    /// <summary>Saved-rule has no mapping rows after validation.</summary>
    public const string MappingRowsEmpty = "saved-rule.mapping-rows.empty";

    /// <summary>Saved-rule has no operation fragments after validation.</summary>
    public const string OperationFragmentsEmpty = "saved-rule.operation-fragments.empty";

    /// <summary>Saved-rule root processor dependency is unsupported by current CLI projection.</summary>
    public const string ProcessorDependencyUnsupported = "saved-rule.processor-dependency.unsupported";

    /// <summary>Saved-rule evidence references are required for the declared support status.</summary>
    public const string EvidenceRequired = "saved-rule.evidence.required";

    /// <summary>Saved-rule input slot templates value is not an array.</summary>
    public const string InputSlotTemplatesInvalid = "saved-rule.input-slot-templates.invalid";

    /// <summary>Saved-rule input slot template entry is not an object.</summary>
    public const string InputSlotTemplateInvalid = "saved-rule.input-slot-template.invalid";

    /// <summary>Saved-rule input slot template id is duplicated.</summary>
    public const string InputSlotTemplateDuplicate = "saved-rule.input-slot-template.duplicate";

    /// <summary>Saved-rule mapping rows array is required.</summary>
    public const string MappingRowsRequired = "saved-rule.mapping-rows.required";

    /// <summary>Saved-rule mapping row entry is not an object.</summary>
    public const string MappingRowInvalid = "saved-rule.mapping-row.invalid";

    /// <summary>Saved-rule mapping row source reference is ambiguous or missing.</summary>
    public const string MappingRowSourceReference = "saved-rule.mapping-row.source-reference";

    /// <summary>Saved-rule mapping row references an undeclared source slot template.</summary>
    public const string MappingRowSourceSlotTemplateUnknown = "saved-rule.mapping-row.source-slot-template-unknown";

    /// <summary>Saved-rule mapping row source range is required for General Merge.</summary>
    public const string MappingRowSourceRangeRequired = "saved-rule.mapping-row.source-range-required";

    /// <summary>Saved-rule mapping row source and target range lengths do not match.</summary>
    public const string MappingRowLengthMismatch = "saved-rule.mapping-row.length-mismatch";

    /// <summary>Saved-rule General Replace row uses an unsupported non-zero source offset.</summary>
    public const string MappingRowReplaceSourceOffsetUnsupported = "saved-rule.mapping-row.replace-source-offset-unsupported";

    /// <summary>Saved-rule General Merge row targets an unsupported address space.</summary>
    public const string MappingRowTargetAddressSpaceUnsupported = "saved-rule.mapping-row.target-address-space-unsupported";

    /// <summary>Saved-rule General Merge row targets an unsupported region.</summary>
    public const string MappingRowTargetRegionUnsupported = "saved-rule.mapping-row.target-region-unsupported";

    /// <summary>Saved-rule General Merge row uses an unsupported overlap policy.</summary>
    public const string MappingRowOverlapPolicyUnsupported = "saved-rule.mapping-row.overlap-policy-unsupported";

    /// <summary>Saved-rule General Merge row does not satisfy declared alignment.</summary>
    public const string MappingRowAlignment = "saved-rule.mapping-row.alignment";

    /// <summary>Saved-rule mapping row id is duplicated.</summary>
    public const string MappingRowDuplicate = "saved-rule.mapping-row.duplicate";

    /// <summary>Saved-rule operation fragments array is required.</summary>
    public const string OperationFragmentsRequired = "saved-rule.operation-fragments.required";

    /// <summary>Saved-rule operation fragment entry is not an object.</summary>
    public const string OperationFragmentInvalid = "saved-rule.operation-fragment.invalid";

    /// <summary>Saved-rule operation fragment kind is unsupported by current CLI projection.</summary>
    public const string OperationFragmentKindUnsupported = "saved-rule.operation-fragment.kind-unsupported";

    /// <summary>Saved-rule operation fragment processor dependency is unsupported by current CLI projection.</summary>
    public const string OperationFragmentProcessorDependencyUnsupported = "saved-rule.operation-fragment.processor-dependency.unsupported";

    /// <summary>Saved-rule operation fragment references an unsupported number of mapping rows.</summary>
    public const string OperationFragmentMappingRowCount = "saved-rule.operation-fragment.mapping-row-count";

    /// <summary>Saved-rule operation fragment references an unknown mapping row.</summary>
    public const string OperationFragmentMappingRowUnknown = "saved-rule.operation-fragment.mapping-row-unknown";

    /// <summary>Saved-rule mapping row is referenced by more than one operation fragment.</summary>
    public const string OperationFragmentMappingRowDuplicateReference = "saved-rule.operation-fragment.mapping-row-duplicate-reference";

    /// <summary>Saved-rule operation fragment id is duplicated.</summary>
    public const string OperationFragmentDuplicate = "saved-rule.operation-fragment.duplicate";

    /// <summary>Saved-rule mapping row is not referenced by a supported operation fragment.</summary>
    public const string MappingRowUnreferenced = "saved-rule.mapping-row.unreferenced";

    /// <summary>Saved-rule JSON property is duplicated.</summary>
    public const string PropertyDuplicate = "saved-rule.property.duplicate";

    /// <summary>Saved-rule JSON property is not part of the reviewed schema.</summary>
    public const string PropertyUnknown = "saved-rule.property.unknown";

    /// <summary>Saved-rule JSON array property is required.</summary>
    public const string ArrayRequired = "saved-rule.array.required";

    /// <summary>Saved-rule JSON property is not an array.</summary>
    public const string ArrayInvalid = "saved-rule.array.invalid";

    /// <summary>Saved-rule JSON array item has an invalid scalar shape.</summary>
    public const string ArrayItemInvalid = "saved-rule.array-item.invalid";

    /// <summary>Saved-rule identifier is invalid.</summary>
    public const string IdInvalid = "saved-rule.id.invalid";

    /// <summary>Saved-rule JSON array contains duplicate values.</summary>
    public const string ArrayDuplicate = "saved-rule.array.duplicate";

    /// <summary>Saved-rule file extension value is invalid.</summary>
    public const string ExtensionInvalid = "saved-rule.extension.invalid";

    /// <summary>Saved-rule required string property is missing or empty.</summary>
    public const string StringRequired = "saved-rule.string.required";

    /// <summary>Saved-rule enum value is unsupported.</summary>
    public const string EnumInvalid = "saved-rule.enum.invalid";

    /// <summary>Saved-rule semantic version property is invalid.</summary>
    public const string SemverInvalid = "saved-rule.semver.invalid";

    /// <summary>Saved-rule range property is required.</summary>
    public const string RangeRequired = "saved-rule.range.required";

    /// <summary>Saved-rule range value is not an object.</summary>
    public const string RangeInvalid = "saved-rule.range.invalid";

    /// <summary>Saved-rule range length is invalid.</summary>
    public const string RangeLength = "saved-rule.range.length";

    /// <summary>Saved-rule range exceeds the supported address size.</summary>
    public const string RangeOverflow = "saved-rule.range.overflow";

    /// <summary>Saved-rule integer property is missing or not an integer.</summary>
    public const string IntegerRequired = "saved-rule.integer.required";

    /// <summary>Saved-rule integer property must be non-negative.</summary>
    public const string IntegerNegative = "saved-rule.integer.negative";

    /// <summary>Saved-rule integer property must be positive.</summary>
    public const string IntegerPositive = "saved-rule.integer.positive";
}

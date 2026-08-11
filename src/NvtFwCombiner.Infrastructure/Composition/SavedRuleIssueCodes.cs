namespace NvtFwCombiner.Infrastructure.Composition;

/// <summary>Stable Saved Rule v2 validation issue codes used by Bootstrap CLI contracts.</summary>
internal static class SavedRuleIssueCodes
{
    public const string FileNotFound = "saved-rule.file-not-found";
    public const string JsonInvalid = "saved-rule.json.invalid";
    public const string FileReadFailed = "saved-rule.file-read-failed";
    public const string SchemaVersionUnsupported =
        "saved-rule.schema-version.unsupported";
    public const string V2ContractInvalid = "saved-rule.v2.contract-invalid";
    public const string V2ParentNarrowingInvalid =
        "saved-rule.v2.parent-narrowing-invalid";
    public const string ParentUnavailable = "saved-rule.parent.unavailable";
    public const string OperationFragmentsEmpty =
        "saved-rule.operation-fragments.empty";
    public const string ProcessorDependencyUnsupported =
        "saved-rule.processor-dependency.unsupported";
    public const string InputSlotTemplatesInvalid =
        "saved-rule.input-slot-templates.invalid";
    public const string InputSlotTemplateInvalid =
        "saved-rule.input-slot-template.invalid";
    public const string InputSlotTemplateDuplicate =
        "saved-rule.input-slot-template.duplicate";
    public const string MappingRowInvalid = "saved-rule.mapping-row.invalid";
    public const string MappingRowSourceReference =
        "saved-rule.mapping-row.source-reference";
    public const string MappingRowSourceSlotTemplateUnknown =
        "saved-rule.mapping-row.source-slot-template-unknown";
    public const string MappingRowTargetRegionUnsupported =
        "saved-rule.mapping-row.target-region-unsupported";
    public const string MappingRowOverlapPolicyUnsupported =
        "saved-rule.mapping-row.overlap-policy-unsupported";
    public const string OperationFragmentsRequired =
        "saved-rule.operation-fragments.required";
    public const string OperationFragmentInvalid =
        "saved-rule.operation-fragment.invalid";
    public const string OperationFragmentKindUnsupported =
        "saved-rule.operation-fragment.kind-unsupported";
    public const string StringRequired = "saved-rule.string.required";
    public const string RangeInvalid = "saved-rule.range.invalid";
    public const string RangeOverflow = "saved-rule.range.overflow";
    public const string IntegerNegative = "saved-rule.integer.negative";
    public const string IntegerPositive = "saved-rule.integer.positive";
}

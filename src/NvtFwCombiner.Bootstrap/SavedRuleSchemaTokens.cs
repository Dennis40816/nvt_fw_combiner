namespace NvtFwCombiner.Bootstrap;

/// <summary>Stable saved-rule JSON enum tokens consumed by Bootstrap validation and CLI projection.</summary>
internal static class SavedRuleSchemaTokens
{
    internal const string CompositionKindMerge = "merge";
    internal const string CompositionKindReplace = "replace";

    internal const string SupportStatusDraft = "draft";
    internal const string SupportStatusCandidate = "candidate";
    internal const string SupportStatusSupported = "supported";
    internal const string SupportStatusDeprecated = "deprecated";

    internal const string ProtectedRangePolicyDenyCrossing = "deny-crossing";
    internal const string ProtectedRangePolicyDenyTouch = "deny-touch";
    internal const string ProtectedRangePolicyProfileDefined = "profile-defined";

    internal const string InputSlotCardinalityOne = "one";
    internal const string InputSlotCardinalityMany = "many";

    internal const string MappingOverlapReject = "reject";
    internal const string MappingOverlapAllowDeclared = "allow-declared";
    internal const string MappingOverlapReplaceExisting = "replace-existing";

    internal const string OperationKindCopyRange = "copy-range";
    internal const string OperationKindFillRange = "fill-range";
    internal const string OperationKindPatchScalar = "patch-scalar";
    internal const string OperationKindReplaceRange = "replace-range";
    internal const string OperationKindRunExternalProcessor = "run-external-processor";
    internal const string OperationKindAssertRange = "assert-range";
    internal const string OperationKindValidateChecksum = "validate-checksum";
}

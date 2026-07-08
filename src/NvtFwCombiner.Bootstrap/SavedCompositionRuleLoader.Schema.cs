using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class SavedCompositionRuleLoader
{
    private const string SupportedSchemaVersion = "1.0";

    private static readonly HashSet<string> TopLevelProperties =
    [
        "schemaVersion",
        "ruleId",
        "ruleVersion",
        "displayName",
        "description",
        "compositionKind",
        "sourceExperience",
        "supportStatus",
        "compatibility",
        "inputSlotTemplates",
        "mappingRows",
        "operationFragments",
        "processorDependencyIds",
        "validationRuleIds",
        "protectedRangePolicy",
        "owner",
        "reviewers",
        "evidenceRefs",
    ];

    private static readonly HashSet<string> CompatibilityProperties =
    [
        "profileIds",
        "icIds",
        "modeIds",
        "compatibilityTags",
    ];

    private static readonly HashSet<string> MappingRowProperties =
    [
        "rowId",
        "sourceBindingId",
        "sourceSlotTemplateId",
        "sourceRange",
        "targetAddressSpaceId",
        "targetRegionId",
        "targetRange",
        "overlapPolicy",
        "alignment",
        "reason",
    ];

    private static readonly HashSet<string> ByteRangeProperties = ["start", "length"];

    private static readonly HashSet<string> OperationFragmentProperties =
    [
        "operationId",
        "kind",
        "reason",
        "mappingRowIds",
        "processorDependencyIds",
    ];

    private static readonly HashSet<string> InputSlotTemplateProperties =
    [
        "slotTemplateId",
        "role",
        "cardinality",
        "acceptedExtensions",
    ];

    private static readonly string[] CompositionKindValues =
    [
        SavedRuleSchemaTokens.CompositionKindMerge,
        SavedRuleSchemaTokens.CompositionKindReplace,
    ];

    private static readonly string[] SourceExperienceValues = [IcWorkflowIds.GeneralMerge, IcWorkflowIds.GeneralReplace];
    private static readonly string[] SupportStatusValues =
    [
        SavedRuleSchemaTokens.SupportStatusDraft,
        SavedRuleSchemaTokens.SupportStatusCandidate,
        SavedRuleSchemaTokens.SupportStatusSupported,
        SavedRuleSchemaTokens.SupportStatusDeprecated,
    ];

    private static readonly string[] ProtectedRangePolicyValues =
    [
        SavedRuleSchemaTokens.ProtectedRangePolicyDenyCrossing,
        SavedRuleSchemaTokens.ProtectedRangePolicyDenyTouch,
        SavedRuleSchemaTokens.ProtectedRangePolicyProfileDefined,
    ];

    private static readonly string[] InputSlotCardinalityValues =
    [
        SavedRuleSchemaTokens.InputSlotCardinalityOne,
        SavedRuleSchemaTokens.InputSlotCardinalityMany,
    ];

    private static readonly string[] MappingOverlapPolicyValues =
    [
        SavedRuleSchemaTokens.MappingOverlapReject,
        SavedRuleSchemaTokens.MappingOverlapAllowDeclared,
        SavedRuleSchemaTokens.MappingOverlapReplaceExisting,
    ];

    private static readonly string[] OperationFragmentKindValues =
    [
        SavedRuleSchemaTokens.OperationKindCopyRange,
        SavedRuleSchemaTokens.OperationKindFillRange,
        SavedRuleSchemaTokens.OperationKindPatchScalar,
        SavedRuleSchemaTokens.OperationKindReplaceRange,
        SavedRuleSchemaTokens.OperationKindRunExternalProcessor,
        SavedRuleSchemaTokens.OperationKindAssertRange,
        SavedRuleSchemaTokens.OperationKindValidateChecksum,
    ];
}

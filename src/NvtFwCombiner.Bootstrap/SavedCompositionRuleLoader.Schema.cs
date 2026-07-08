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

    private static readonly string[] CompositionKindValues = ["merge", "replace"];
    private static readonly string[] SourceExperienceValues = [IcWorkflowIds.GeneralMerge, IcWorkflowIds.GeneralReplace];
    private static readonly string[] SupportStatusValues = ["draft", "candidate", "supported", "deprecated"];
    private static readonly string[] ProtectedRangePolicyValues = ["deny-crossing", "deny-touch", "profile-defined"];
    private static readonly string[] InputSlotCardinalityValues = ["one", "many"];
    private static readonly string[] MappingOverlapPolicyValues = ["reject", "allow-declared", "replace-existing"];
    private static readonly string[] OperationFragmentKindValues =
    [
        "copy-range",
        "fill-range",
        "patch-scalar",
        "replace-range",
        "run-external-processor",
        "assert-range",
        "validate-checksum",
    ];
}

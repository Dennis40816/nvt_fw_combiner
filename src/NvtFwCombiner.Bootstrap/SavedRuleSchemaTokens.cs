namespace NvtFwCombiner.Bootstrap;

/// <summary>Saved Rule v2 tokens consumed by canonical admission and draft projection.</summary>
internal static class SavedRuleSchemaTokens
{
    internal const string CompositionKindMerge = "merge";
    internal const string CompositionKindReplace = "replace";
    internal const string PromotionStageExecutableCandidate =
        "executable-candidate";
    internal const string PromotionBlockerGolden = "golden";
    internal const string PromotionBlockerHumanReview = "human-review";
    internal const string InputSlotCardinalityOne = "one";
    internal const string MappingOverlapReject = "reject";
    internal const string OperationKindCopyRange = "copy-range";
    internal const string OperationKindReplaceRange = "replace-range";
}

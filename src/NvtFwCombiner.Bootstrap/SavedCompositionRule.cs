using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal sealed record SavedCompositionRule(
    string SchemaVersion,
    string RuleId,
    string RuleVersion,
    string DisplayName,
    string CompositionKind,
    string SourceExperience,
    string SupportStatus,
    SavedRuleCompatibility Compatibility,
    IReadOnlyList<SavedRuleMappingRow> MappingRows,
    IReadOnlyList<SavedRuleOperationFragment> OperationFragments,
    IReadOnlyList<string> ProcessorDependencyIds,
    IReadOnlyList<string> ValidationRuleIds,
    string Owner,
    IReadOnlyList<string> EvidenceRefs);

internal sealed record SavedRuleCompatibility(
    IReadOnlyList<string> ProfileIds,
    IReadOnlyList<string> IcIds,
    IReadOnlyList<string> ModeIds,
    IReadOnlyList<string> CompatibilityTags);

internal sealed record SavedRuleMappingRow(
    string RowId,
    string SourceReference,
    ByteRange? SourceRange,
    string TargetAddressSpaceId,
    string? TargetRegionId,
    ByteRange TargetRange,
    string OverlapPolicy,
    int Alignment,
    string Reason);

internal sealed record SavedRuleOperationFragment(
    string OperationId,
    IReadOnlyList<string> MappingRowIds);

internal sealed record SavedRuleValidationIssue(string Code, string Message, string Path);

internal sealed record SavedCompositionRuleLoadResult(
    SavedCompositionRule? Rule,
    IReadOnlyList<SavedRuleValidationIssue> Issues)
{
    public bool IsValid => Rule is not null && Issues.Count == 0;
}

namespace NvtFwCombiner.Domain.Composition;

/// <summary>Canonical normalized header facts retained by one profile definition.</summary>
internal sealed record CompositionProfileHeader(
    string ExperienceId,
    LayoutPolicy LayoutPolicy,
    InputPolicy InputPolicy,
    V2CompilationContextKind CompilationContextKind,
    CompositionProfileMapBinding? MapBinding,
    string FamilyId,
    string FamilyVersion,
    string FamilyContentHash,
    IReadOnlyList<string> LogicalOutputMemberIds,
    bool AllowsConditionalProcessor,
    SourceEnvelopeProfileBinding? SourceEnvelopeBinding = null,
    AbCodeDefinition? Ab = null);

/// <summary>Profile-owned existing layout template and advisory facts for one complete DP source.</summary>
internal sealed record SourceEnvelopeProfileBinding
{
    internal SourceEnvelopeProfileBinding(
        string sourceSlotId,
        string layoutTemplateMapId,
        string rootRegionId,
        bool allowsAbsentSource,
        IReadOnlyList<long> expectedOuterLengths,
        string unexpectedLengthIssueCode)
    {
        SourceSlotId = CanonicalPolicyValueRules.RequireCanonicalId(sourceSlotId, nameof(sourceSlotId));
        LayoutTemplateMapId = CanonicalPolicyValueRules.RequireCanonicalId(layoutTemplateMapId, nameof(layoutTemplateMapId));
        RootRegionId = CanonicalPolicyValueRules.RequireCanonicalId(rootRegionId, nameof(rootRegionId));
        AllowsAbsentSource = allowsAbsentSource;
        ExpectedOuterLengths = Array.AsReadOnly(InputLengthPolicyLimits.SnapshotExpectedOuterLengths(
            expectedOuterLengths, nameof(expectedOuterLengths)));
        UnexpectedLengthIssueCode = RequiredValue.NotBlank(unexpectedLengthIssueCode);
    }

    internal string SourceSlotId { get; }
    internal string LayoutTemplateMapId { get; }
    internal string RootRegionId { get; }
    internal bool AllowsAbsentSource { get; }
    internal IReadOnlyList<long> ExpectedOuterLengths { get; }
    internal string UnexpectedLengthIssueCode { get; }
}

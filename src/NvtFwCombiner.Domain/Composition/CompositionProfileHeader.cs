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
    bool AllowsConditionalProcessor);

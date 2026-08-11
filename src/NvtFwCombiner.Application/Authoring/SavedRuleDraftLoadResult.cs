namespace NvtFwCombiner.Application.Authoring;

/// <summary>One stable issue produced while loading a Saved Rule draft.</summary>
public sealed record SavedRuleValidationIssue(
    string Code,
    string Message,
    string Path);

/// <summary>One executable typed draft closed over by a Saved Rule v2 document.</summary>
public sealed record SavedRuleV2DraftLoadResult<TDraft>(
    TDraft? Draft,
    SavedRuleParentIdentity? ParentBinding,
    SavedRuleExecutionIdentity? ExecutionIdentity,
    GeneralSavedRuleResourcePolicy? ResourcePolicy,
    IReadOnlyList<SavedRuleValidationIssue> Issues)
    where TDraft : class
{
    /// <summary>True only when the exact Parent and executable draft were resolved.</summary>
    public bool IsValid =>
        Draft is not null &&
        ParentBinding is not null &&
        ExecutionIdentity is not null &&
        ResourcePolicy is not null &&
        Issues.Count == 0;
}

/// <summary>One fully admitted Saved Rule v2 document inspection.</summary>
public sealed record SavedRuleV2InspectionResult(
    SavedRuleExecutionIdentity? Identity,
    GeneralMappingDraftState? Mappings,
    IReadOnlyList<SavedRuleValidationIssue> Issues)
{
    /// <summary>True only when exact Parent admission produced executable mappings.</summary>
    public bool IsValid => Identity is not null && Mappings is not null && Issues.Count == 0;
}

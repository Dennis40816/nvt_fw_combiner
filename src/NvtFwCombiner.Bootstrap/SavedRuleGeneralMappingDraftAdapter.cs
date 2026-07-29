using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>
/// Projects validated General Merge or General Replace saved-rule rows into
/// the canonical typed mapping draft without executing firmware.
/// </summary>
internal static class SavedRuleGeneralMappingDraftAdapter
{
    internal static bool TryCreate(
        SavedCompositionRule rule,
        Func<SavedRuleMappingRow, string?> sourceReferenceResolver,
        [NotNullWhen(true)] out GeneralMappingDraftState? draft,
        out IReadOnlyList<SavedRuleValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(sourceReferenceResolver);

        if (!TryGetExpectedOperation(
                rule,
                out string? expectedOperationKind,
                out ExplicitMappingOperationKind operationKind,
                out SavedRuleValidationIssue? experienceIssue))
        {
            draft = null;
            issues = [experienceIssue!];
            return false;
        }

        Dictionary<string, SavedRuleOperationFragment> fragmentsByRowId =
            CreateFragmentsByRowId(rule.OperationFragments);
        List<GeneralMappingDraftRow> rows = [];
        List<SavedRuleValidationIssue> issueList = [];
        foreach (SavedRuleMappingRow row in rule.MappingRows)
        {
            if (!fragmentsByRowId.TryGetValue(
                    row.RowId,
                    out SavedRuleOperationFragment? fragment))
            {
                issueList.Add(Issue(
                    SavedRuleIssueCodes.MappingRowUnreferenced,
                    $"Saved-rule mapping row '{row.RowId}' is not linked to one operation fragment.",
                    "$.mappingRows"));
                continue;
            }

            if (!string.Equals(
                    fragment.Kind,
                    expectedOperationKind,
                    StringComparison.Ordinal))
            {
                issueList.Add(Issue(
                    SavedRuleIssueCodes.OperationFragmentKindUnsupported,
                    $"Saved-rule operation '{fragment.OperationId}' must use '{expectedOperationKind}' for {rule.SourceExperience}.",
                    "$.operationFragments"));
                continue;
            }

            if (!string.Equals(
                    row.OverlapPolicy,
                    SavedRuleSchemaTokens.MappingOverlapReject,
                    StringComparison.Ordinal))
            {
                issueList.Add(Issue(
                    SavedRuleIssueCodes.MappingRowOverlapPolicyUnsupported,
                    $"Saved-rule mapping row '{row.RowId}' cannot be projected because its overlap policy is not reject.",
                    "$.mappingRows"));
                continue;
            }

            string? sourceReference = sourceReferenceResolver(row);
            if (string.IsNullOrWhiteSpace(sourceReference))
            {
                issueList.Add(Issue(
                    SavedRuleIssueCodes.MappingRowSourceReference,
                    $"Saved-rule mapping row '{row.RowId}' has no resolved source reference.",
                    "$.mappingRows"));
                continue;
            }

            ByteRange sourceRange = row.SourceRange ??
                new ByteRange(0, row.TargetRange.Length);
            if (sourceRange.Length != row.TargetRange.Length)
            {
                issueList.Add(Issue(
                    SavedRuleIssueCodes.MappingRowLengthMismatch,
                    $"Saved-rule mapping row '{row.RowId}' source and target lengths differ.",
                    "$.mappingRows"));
                continue;
            }

            if (row.TargetRange.Start % row.Alignment != 0)
            {
                issueList.Add(Issue(
                    SavedRuleIssueCodes.MappingRowAlignment,
                    $"Saved-rule mapping row '{row.RowId}' target start does not satisfy alignment {row.Alignment}.",
                    "$.mappingRows"));
                continue;
            }

            try
            {
                rows.Add(new GeneralMappingDraftRow(
                    fragment.OperationId,
                    operationKind,
                    GeneralMappingSource.File(sourceReference),
                    sourceRange,
                    row.TargetAddressSpaceId,
                    row.TargetRange,
                    OverlapPolicy.Reject,
                    row.Alignment,
                    row.Reason,
                    row.TargetRegionId,
                    OperationProvenance.SavedRule(rule.RuleId, rule.RuleVersion)));
            }
            catch (ArgumentException exception)
            {
                issueList.Add(Issue(
                    SavedRuleIssueCodes.MappingRowInvalid,
                    $"Saved-rule mapping row '{row.RowId}' cannot form a typed General mapping: {exception.Message}",
                    "$.mappingRows"));
            }
        }

        if (issueList.Count > 0)
        {
            draft = null;
            issues = issueList;
            return false;
        }

        try
        {
            draft = new GeneralMappingDraftState(rows);
            issues = [];
            return true;
        }
        catch (ArgumentException exception)
        {
            draft = null;
            issues =
            [
                Issue(
                    SavedRuleIssueCodes.OperationFragmentDuplicate,
                    $"Saved rule cannot form one typed General mapping draft: {exception.Message}",
                    "$.operationFragments"),
            ];
            return false;
        }
    }

    private static bool TryGetExpectedOperation(
        SavedCompositionRule rule,
        [NotNullWhen(true)] out string? expectedOperationKind,
        out ExplicitMappingOperationKind operationKind,
        out SavedRuleValidationIssue? issue)
    {
        if (string.Equals(
                rule.CompositionKind,
                SavedRuleSchemaTokens.CompositionKindMerge,
                StringComparison.Ordinal) &&
            string.Equals(
                rule.SourceExperience,
                IcWorkflowIds.GeneralMerge,
                StringComparison.Ordinal))
        {
            expectedOperationKind = SavedRuleSchemaTokens.OperationKindCopyRange;
            operationKind = ExplicitMappingOperationKind.CopyRange;
            issue = null;
            return true;
        }

        if (string.Equals(
                rule.CompositionKind,
                SavedRuleSchemaTokens.CompositionKindReplace,
                StringComparison.Ordinal) &&
            string.Equals(
                rule.SourceExperience,
                IcWorkflowIds.GeneralReplace,
                StringComparison.Ordinal))
        {
            expectedOperationKind = SavedRuleSchemaTokens.OperationKindReplaceRange;
            operationKind = ExplicitMappingOperationKind.ReplaceRange;
            issue = null;
            return true;
        }

        expectedOperationKind = null;
        operationKind = default;
        issue = Issue(
            SavedRuleIssueCodes.ExperienceKindMismatch,
            "Saved-rule mapping drafts support only General Merge or General Replace.",
            "$.sourceExperience");
        return false;
    }

    private static Dictionary<string, SavedRuleOperationFragment>
        CreateFragmentsByRowId(
            IReadOnlyList<SavedRuleOperationFragment> fragments)
    {
        Dictionary<string, SavedRuleOperationFragment> result =
            new(StringComparer.Ordinal);
        foreach (SavedRuleOperationFragment fragment in fragments)
        {
            foreach (string rowId in fragment.MappingRowIds)
            {
                _ = result.TryAdd(rowId, fragment);
            }
        }

        return result;
    }

    private static SavedRuleValidationIssue Issue(
        string code,
        string message,
        string path)
    {
        return new SavedRuleValidationIssue(code, message, path);
    }
}

using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Bootstrap;

internal static partial class MergeCliCommandHandler
{
    private static bool TryCreateMappingsFromSavedRule(
        string rulePath,
        IReadOnlyList<string> slotValues,
        string icId,
        TextWriter error,
        [NotNullWhen(true)] out GeneralMappingDraftState? mappings)
    {
        mappings = null;
        SavedCompositionRuleLoadResult load = SavedCompositionRuleLoader.Load(rulePath);
        if (!load.IsValid)
        {
            PrintSavedRuleIssues(load.Issues, error);
            return false;
        }

        SavedCompositionRule rule = load.Rule!;
        if (!string.Equals(rule.CompositionKind, SavedRuleSchemaTokens.CompositionKindMerge, StringComparison.Ordinal) ||
            !string.Equals(rule.SourceExperience, GeneralMergeModeId, StringComparison.Ordinal))
        {
            error.WriteLine($"error: saved rule '{rule.RuleId}' is for {rule.CompositionKind} / {rule.SourceExperience}, not {SavedRuleSchemaTokens.CompositionKindMerge} / {GeneralMergeModeId}");
            return false;
        }

        string profileId = WorkbenchCompositionService.GetGeneralMergeWorkbenchProfileId(icId);
        if (!MatchesCompatibility(rule.Compatibility.IcIds, icId, StringComparer.OrdinalIgnoreCase) ||
            !MatchesCompatibility(rule.Compatibility.ProfileIds, profileId, StringComparer.Ordinal) ||
            !MatchesCompatibility(rule.Compatibility.ModeIds, GeneralMergeModeId, StringComparer.Ordinal))
        {
            error.WriteLine($"error: saved rule '{rule.RuleId}' is not compatible with {icId} / {profileId} / {GeneralMergeModeId}");
            return false;
        }

        if (rule.ProcessorDependencyIds.Count > 0)
        {
            error.WriteLine($"error: saved rule '{rule.RuleId}' requires processors that General Merge saved-rule CLI consumption does not support yet");
            return false;
        }

        if (!TryCreateSlotBindings(slotValues, error, out Dictionary<string, string>? slotsById))
        {
            return false;
        }

        foreach (SavedRuleMappingRow row in rule.MappingRows)
        {
            if (!slotsById.ContainsKey(row.SourceReference))
            {
                error.WriteLine($"error: saved rule row '{row.RowId}' requires --slot {row.SourceReference}=<path>");
                return false;
            }
        }

        if (!SavedRuleGeneralMappingDraftAdapter.TryCreate(
                rule,
                row => slotsById[row.SourceReference],
                out mappings,
                out IReadOnlyList<SavedRuleValidationIssue> projectionIssues))
        {
            PrintSavedRuleIssues(projectionIssues, error);
            return false;
        }

        return true;
    }

    private static bool TryCreateSlotBindings(
        IReadOnlyList<string> slotValues,
        TextWriter error,
        [NotNullWhen(true)] out Dictionary<string, string>? slotsById)
    {
        slotsById = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string value in slotValues)
        {
            int separatorIndex = value.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
            {
                error.WriteLine("error: --slot must use <slot-id=path>");
                return false;
            }

            string slotId = value[..separatorIndex].Trim();
            string path = value[(separatorIndex + 1)..].Trim();
            if (slotId.Length == 0 || path.Length == 0)
            {
                error.WriteLine("error: --slot must use non-empty slot id and path");
                return false;
            }

            if (!slotsById.TryAdd(slotId, Path.GetFullPath(path)))
            {
                error.WriteLine($"error: duplicate --slot binding for '{slotId}'");
                return false;
            }
        }

        return true;
    }

    private static bool MatchesCompatibility(
        IReadOnlyList<string> values,
        string candidate,
        IEqualityComparer<string> comparer)
    {
        return values.Count == 0 || values.Contains(candidate, comparer);
    }

    private static void PrintSavedRuleIssues(IReadOnlyList<SavedRuleValidationIssue> issues, TextWriter error)
    {
        error.WriteLine("error: saved rule validation failed");
        foreach (SavedRuleValidationIssue issue in issues)
        {
            error.WriteLine($"  {issue.Code} at {issue.Path}: {issue.Message}");
        }
    }
}

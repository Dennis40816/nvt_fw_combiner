using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static partial class MergeCliCommandHandler
{
    private static bool TryCreateMappingsFromSavedRule(
        string rulePath,
        IReadOnlyList<string> slotValues,
        string icId,
        TextWriter error,
        [NotNullWhen(true)] out WorkbenchGeneralMergeMappingInput[]? mappings)
    {
        mappings = null;
        SavedCompositionRuleLoadResult load = SavedCompositionRuleLoader.Load(rulePath);
        if (!load.IsValid)
        {
            PrintSavedRuleIssues(load.Issues, error);
            return false;
        }

        SavedCompositionRule rule = load.Rule!;
        if (!string.Equals(rule.CompositionKind, "merge", StringComparison.Ordinal) ||
            !string.Equals(rule.SourceExperience, GeneralMergeModeId, StringComparison.Ordinal))
        {
            error.WriteLine($"error: saved rule '{rule.RuleId}' is for {rule.CompositionKind} / {rule.SourceExperience}, not merge / {GeneralMergeModeId}");
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

        var operationIdsByRowId = rule.OperationFragments
            .SelectMany(fragment => fragment.MappingRowIds.Select(rowId => new KeyValuePair<string, string>(
                rowId,
                fragment.OperationId)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        List<WorkbenchGeneralMergeMappingInput> items = [];
        foreach (SavedRuleMappingRow row in rule.MappingRows)
        {
            if (!string.Equals(row.TargetAddressSpaceId, "output-image", StringComparison.Ordinal))
            {
                error.WriteLine($"error: saved rule row '{row.RowId}' targets unsupported address space '{row.TargetAddressSpaceId}'");
                return false;
            }

            if (!string.Equals(row.OverlapPolicy, "reject", StringComparison.Ordinal))
            {
                error.WriteLine($"error: saved rule row '{row.RowId}' uses unsupported overlap policy '{row.OverlapPolicy}'");
                return false;
            }

            if (row.SourceRange is not { } sourceRange)
            {
                error.WriteLine($"error: saved rule row '{row.RowId}' has no sourceRange for General Merge");
                return false;
            }

            if (!slotsById.TryGetValue(row.SourceReference, out string? sourcePath))
            {
                error.WriteLine($"error: saved rule row '{row.RowId}' requires --slot {row.SourceReference}=<path>");
                return false;
            }

            if (!operationIdsByRowId.TryGetValue(row.RowId, out string? operationId))
            {
                error.WriteLine($"error: saved rule row '{row.RowId}' is not linked to a reviewed operation fragment");
                return false;
            }

            items.Add(new WorkbenchGeneralMergeMappingInput(
                operationId,
                sourcePath,
                CliCompositionRunSupport.FormatHex(sourceRange.Start),
                CliCompositionRunSupport.FormatHex(row.TargetRange.Start),
                CliCompositionRunSupport.FormatHex(row.TargetRange.Length),
                row.Alignment,
                row.Reason,
                OperationProvenance.SavedRule(rule.RuleId, rule.RuleVersion)));
        }

        mappings = [.. items];
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

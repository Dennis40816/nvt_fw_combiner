using Avalonia.Data.Converters;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Display-only labels for canonical workflow identities.</summary>
public static class WorkflowModeDisplayConverters
{
    /// <summary>Reopen only with the accepted Customized feature release; retained workflows remain executable.</summary>
    public static bool CustomizedEntriesVisible => false;

    /// <summary>Filters ordinary UI entries without changing workflow identities or authoring admission.</summary>
    public static IReadOnlyList<string> GetVisibleChoices(IEnumerable<string> choices)
    {
        return [.. choices.Where(static mode => CustomizedEntriesVisible ||
            mode is not (ExperienceIds.GeneralMerge or ExperienceIds.GeneralReplace))];
    }

    /// <summary>Maps canonical workflow ids to the current product wording.</summary>
    public static FuncValueConverter<string, string> DisplayName { get; } = new(GetDisplayName);

    /// <summary>Returns a product label without changing the canonical identity.</summary>
    public static string GetDisplayName(string? workflowId)
    {
        return workflowId switch
        {
            ExperienceIds.StandardMerge => "Standard",
            ExperienceIds.AbMerge => "AB Code",
            ExperienceIds.GeneralMerge => "Customized",
            ExperienceIds.DpReplace => "DP",
            ExperienceIds.CtrlRamReplace => "CtrlRAM",
            ExperienceIds.GeneralReplace => "General",
            _ => workflowId ?? string.Empty,
        };
    }
}

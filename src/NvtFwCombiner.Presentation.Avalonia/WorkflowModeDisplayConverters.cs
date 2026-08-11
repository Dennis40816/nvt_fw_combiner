using Avalonia.Data.Converters;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Display-only labels for canonical workflow identities.</summary>
public static class WorkflowModeDisplayConverters
{
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

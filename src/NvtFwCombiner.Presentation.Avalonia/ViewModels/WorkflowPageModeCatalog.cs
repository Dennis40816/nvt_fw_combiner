using System.Collections.ObjectModel;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Immutable Presentation-owned workflow membership and order for each authoring page.</summary>
internal static class WorkflowPageModeCatalog
{
    private static readonly ReadOnlyCollection<string> s_merge = Array.AsReadOnly(
    [
        ExperienceIds.StandardMerge,
        ExperienceIds.AbMerge,
        ExperienceIds.GeneralMerge,
    ]);
    private static readonly ReadOnlyCollection<string> s_replace = Array.AsReadOnly(
    [
        ExperienceIds.DpReplace,
        ExperienceIds.CtrlRamReplace,
        ExperienceIds.GeneralReplace,
    ]);

    internal static ReadOnlyCollection<string> ForPage(ShellPage page)
    {
        return page switch
        {
            ShellPage.Merge => s_merge,
            ShellPage.Replace => s_replace,
            ShellPage.Home or ShellPage.HexEditor => throw new ArgumentException(
                "Workflow page expected.",
                nameof(page)),
            _ => throw new InvalidOperationException("Unknown shell page."),
        };
    }
}

using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal static class WorkflowSelectorProjection
{
    private static readonly IReadOnlyList<string> s_mergeWorkflowIds =
        Array.AsReadOnly(
        [
            ExperienceIds.StandardMerge,
            ExperienceIds.AbMerge,
            ExperienceIds.GeneralMerge,
        ]);
    private static readonly IReadOnlyList<string> s_replaceWorkflowIds =
        Array.AsReadOnly(
        [
            ExperienceIds.DpReplace,
            ExperienceIds.CtrlRamReplace,
            ExperienceIds.GeneralReplace,
        ]);

    internal static ReadOnlyCollection<string> WorkflowIcChoices(
        CapabilitySelectorPublication publication,
        string workflowId)
    {
        ArgumentNullException.ThrowIfNull(publication);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        return Array.AsReadOnly(publication.IcIds
            .Where(icId => publication.IsWorkflowAuthorable(icId, workflowId))
            .ToArray());
    }

    internal static ReadOnlyCollection<string> PageIcChoices(
        CapabilitySelectorPublication publication,
        ShellPage page)
    {
        ArgumentNullException.ThrowIfNull(publication);
        IReadOnlyList<string> workflowIds = page switch
        {
            ShellPage.Merge => s_mergeWorkflowIds,
            ShellPage.Replace => s_replaceWorkflowIds,
            ShellPage.Home or ShellPage.HexEditor => throw new ArgumentException(
                "Workflow page IC choices require Merge or Replace ownership.",
                nameof(page)),
            _ => throw new InvalidOperationException("Unknown shell page."),
        };
        return Array.AsReadOnly(publication.IcIds
            .Where(icId => workflowIds.Any(workflowId =>
                publication.IsWorkflowAuthorable(icId, workflowId)))
            .ToArray());
    }

    internal static string Number(
        CapabilitySelectorPublication publication,
        string icId,
        string preferredToken,
        bool useAbTopology)
    {
        ArgumentNullException.ThrowIfNull(publication);
        if (string.IsNullOrWhiteSpace(icId))
        {
            return string.Empty;
        }

        IReadOnlyList<string> tokens = useAbTopology
            ? [.. publication.GetAbMergeTopologyChoices(icId).Select(static choice => choice.Token)]
            : [.. publication.GetNumberSelectionChoices(icId).Select(static choice => choice.Token)];
        return tokens.Count == 0
            ? string.Empty
            : tokens.Contains(preferredToken, StringComparer.Ordinal)
                ? preferredToken
                : tokens.FirstOrDefault(token => string.Equals(
                    token,
                    IcNumberSelectionTokens.SingleChip,
                    StringComparison.Ordinal)) ?? tokens[0];
    }

    internal static string ContextIc(
        CapabilitySelectorPublication publication,
        string? candidate,
        ShellPage page)
    {
        ReadOnlyCollection<string> choices = PageIcChoices(publication, page);
        return !string.IsNullOrWhiteSpace(candidate) &&
            choices.Contains(candidate, StringComparer.Ordinal)
                ? candidate
            : !string.IsNullOrWhiteSpace(publication.DefaultIcId) &&
            choices.Contains(publication.DefaultIcId, StringComparer.Ordinal)
                ? publication.DefaultIcId
                : choices.Count > 0
                    ? choices[0]
                    : string.Empty;
    }
}

using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <inheritdoc/>
internal static partial class UiCompositionRunner
{
    /// <summary>Gets grouped IC-number display choices while preserving planner tokens.</summary>
    internal static IReadOnlyList<IcNumberChoiceViewModel> GetNumberSelectionChoices(
        CapabilitySelectorPublication publication,
        string icId,
        string? workflowId = null)
    {
        ArgumentNullException.ThrowIfNull(publication);
        if (StringComparer.Ordinal.Equals(workflowId, ExperienceIds.AbMerge))
        {
            return
            [
                .. publication.GetAbMergeTopologyChoices(icId).Select(static choice =>
                    new IcNumberChoiceViewModel(choice.Token, choice.DisplayLabel)),
            ];
        }

        IReadOnlyList<CapabilityNumberChoice> canonicalChoices = workflowId is null
            ? publication.GetNumberSelectionChoices(icId)
            : publication.GetNumberSelectionChoices(icId, workflowId);
        IReadOnlyList<IcNumberChoiceViewModel> choices =
        [
            .. canonicalChoices
                .Select(choice => new IcNumberChoiceViewModel(choice.Token, choice.DisplayLabel)),
        ];

        return choices;
    }

}

using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <inheritdoc/>
internal static partial class UiCompositionRunner
{
    /// <summary>Gets grouped IC-number display choices while preserving planner tokens.</summary>
    internal static IReadOnlyList<IcNumberChoiceViewModel> GetNumberSelectionChoices(
        CapabilitySelectorPublication publication,
        string icId)
    {
        ArgumentNullException.ThrowIfNull(publication);
        IReadOnlyList<CapabilityNumberChoice> canonicalChoices =
            publication.GetNumberSelectionChoices(icId);
        IReadOnlyList<IcNumberChoiceViewModel> choices =
        [
            .. canonicalChoices
                .Select(choice => new IcNumberChoiceViewModel(choice.Token, choice.DisplayLabel)),
        ];

        return choices;
    }

}

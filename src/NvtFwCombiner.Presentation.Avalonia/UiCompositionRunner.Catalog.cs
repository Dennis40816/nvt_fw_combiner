using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

/// <inheritdoc/>
public static partial class UiCompositionRunner
{
    /// <summary>Gets grouped IC-number display choices while preserving planner tokens.</summary>
    public static IReadOnlyList<IcNumberChoiceViewModel> GetNumberSelectionChoices(string icId)
    {
        IReadOnlyList<WorkbenchIcNumberChoice> workbenchChoices =
            WorkbenchCompositionService.GetNumberSelectionChoices(icId);
        IReadOnlyList<IcNumberChoiceViewModel> choices =
        [
            .. workbenchChoices
                .Select(choice => new IcNumberChoiceViewModel(choice.Token, choice.DisplayLabel)),
        ];

        return choices.Count > 0
            ? choices
            : [new IcNumberChoiceViewModel(WorkbenchIcNumberTokens.SingleChip, "1 IC")];
    }

}

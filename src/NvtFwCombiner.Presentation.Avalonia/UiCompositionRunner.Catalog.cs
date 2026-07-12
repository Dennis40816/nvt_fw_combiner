using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public static partial class UiCompositionRunner
{
    /// <summary>Gets selectable IC ids from the workbench catalog.</summary>
    public static IReadOnlyList<string> GetSupportedIcIds()
    {
        return WorkbenchCompositionService.GetSupportedIcIds();
    }

    /// <summary>Gets the catalog-owned initial IC id for shell surfaces.</summary>
    public static string GetDefaultIcId()
    {
        return WorkbenchCompositionService.GetDefaultIcId();
    }

    /// <summary>Gets supported IC-number choices from the workbench catalog.</summary>
    public static IReadOnlyList<string> GetNumberChoices(string icId)
    {
        IReadOnlyList<string> choices = WorkbenchCompositionService.GetNumberChoices(icId);
        return choices.Count > 0 ? choices : [WorkbenchIcNumberTokens.SingleChip];
    }

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

    /// <summary>Returns true when the selected IC uses the catalog-backed DP Perspective policy.</summary>
    public static bool IsDpPerspectiveIc(string icId)
    {
        return WorkbenchCompositionService.IsDpPerspectiveIc(icId);
    }

    /// <summary>Gets catalog and tool summary data for the Settings page.</summary>
    public static WorkbenchSettingsSnapshot GetSettingsSnapshot()
    {
        return WorkbenchCompositionService.GetSettingsSnapshot();
    }
}

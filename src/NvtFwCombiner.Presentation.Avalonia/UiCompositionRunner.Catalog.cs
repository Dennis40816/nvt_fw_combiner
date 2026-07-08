using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia;

public static partial class UiCompositionRunner
{
    /// <summary>Gets selectable IC ids from the workbench catalog.</summary>
    public static IReadOnlyList<string> GetSupportedIcIds()
    {
        return WorkbenchCompositionService.GetSupportedIcIds();
    }

    /// <summary>Gets supported IC-number choices from the workbench catalog.</summary>
    public static IReadOnlyList<string> GetNumberChoices(string icId)
    {
        return WorkbenchCompositionService.GetNumberChoices(icId);
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

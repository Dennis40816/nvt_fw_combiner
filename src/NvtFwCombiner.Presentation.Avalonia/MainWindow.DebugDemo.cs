#if DEBUG
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia;

public sealed partial class MainWindow
{
    private static void ApplyDebugDemoWhenNoLaunchOptions(MainWindowViewModel viewModel, UiLaunchOptions launchOptions)
    {
        if (launchOptions.Page is not null ||
            !string.IsNullOrWhiteSpace(launchOptions.ReportPath) ||
            launchOptions.OpenReport ||
            launchOptions.Issues.Count > 0 ||
            !DebugDemoFixture.TryFindHexEditorBase(out string? basePath))
        {
            return;
        }

        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "2";
        viewModel.ShowHexEditorCommand.Execute(null);
        viewModel.SetSlotFile(WorkbenchSlotIds.ReplaceBase, basePath);
    }
}
#endif

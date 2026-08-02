using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>A clear confirmation keeps its original destination until the user decides.</summary>
    [Fact]
    public void NavigationClearConfirmationIgnoresSubsequentNavigationRequests()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-navigation-modal-isolation");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.SelectedIc = "NT51928";
        viewModel.Replace.SelectedReplaceMode = WorkbenchReplaceModes.CtrlRam;
        viewModel.SetSlotFile("replace-base", workspace.Write("base.bin", [0x10, 0x11]));

        viewModel.ShowMergeCommand.Execute(null);
        viewModel.ShowSettingsCommand.Execute(null);

        Assert.True(viewModel.IsNavigationClearConfirmationOpen);
        Assert.Equal("Replace → Merge", viewModel.NavigationClearRoute);

        viewModel.ConfirmNavigationAndClearCommand.Execute(null);

        Assert.True(viewModel.IsMergeVisible);
        Assert.False(viewModel.IsSettingsVisible);
    }
}

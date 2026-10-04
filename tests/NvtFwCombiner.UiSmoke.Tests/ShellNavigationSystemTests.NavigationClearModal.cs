using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>A newly opened application blocker closes Settings instead of nesting modal surfaces.</summary>
    [Fact]
    public void SettingsClosesWhenAnotherApplicationModalOpens()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.OpenSettingsCommand.Execute(null);

        viewModel.Navigation.IsNavigationClearConfirmationOpen = true;

        Assert.False(viewModel.IsSettingsModalOpen);
        Assert.True(viewModel.Navigation.IsNavigationClearConfirmationOpen);
    }

    /// <summary>A clear confirmation keeps its original destination until the user decides.</summary>
    [Fact]
    public void NavigationClearConfirmationIgnoresSubsequentNavigationRequests()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-navigation-modal-isolation");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51928";
        viewModel.Replace.SelectedReplaceMode = ExperienceIds.CtrlRamReplace;
        viewModel.SetSlotFile("replace-base", workspace.Write("base.bin", [0x10, 0x11]));

        viewModel.ShowMergeCommand.Execute(null);
        viewModel.OpenSettingsCommand.Execute(null);

        Assert.True(viewModel.Navigation.IsNavigationClearConfirmationOpen);
        Assert.Equal("Replace → Merge", viewModel.Navigation.NavigationClearRoute);

        viewModel.Navigation.ConfirmNavigationAndClearCommand.Execute(null);

        Assert.True(viewModel.IsMergeVisible);
        Assert.False(viewModel.IsSettingsModalOpen);
    }
}

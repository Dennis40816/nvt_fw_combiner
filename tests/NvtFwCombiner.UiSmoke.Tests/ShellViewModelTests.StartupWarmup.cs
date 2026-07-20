using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Primes deferred page state once without navigating or fabricating firmware input.</summary>
    [Fact]
    public void StartupWarmupPrimesDeferredStateWithoutChangingTheActivePage()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Assert.Equal(ShellPage.Home, viewModel.SelectedPage);
        Assert.Empty(viewModel.SettingsOverviewRows);
        Assert.Empty(viewModel.NumberSelectionChoices);
        Assert.Null(viewModel.LoadedHexEditorWorkspace);

        viewModel.WarmDeferredShellState();

        HexEditorWorkspaceViewModel workspace = Assert.IsType<HexEditorWorkspaceViewModel>(
            viewModel.LoadedHexEditorWorkspace);
        int settingsCount = viewModel.SettingsOverviewRows.Count;
        int numberChoiceCount = viewModel.NumberSelectionChoices.Count;
        int replaceSlotCount = viewModel.ReplaceSlots.Count;

        Assert.Equal(ShellPage.Home, viewModel.SelectedPage);
        Assert.NotEmpty(viewModel.SettingsOverviewRows);
        Assert.NotEmpty(viewModel.SettingsCapabilityRows);
        Assert.NotEmpty(viewModel.NumberSelectionChoices);
        Assert.NotEmpty(viewModel.ReplaceSlots);
        Assert.All(viewModel.MergeSlots, static slot => Assert.False(slot.HasFile));
        Assert.All(viewModel.ReplaceSlots, static slot => Assert.False(slot.HasFile));

        viewModel.WarmDeferredShellState();

        Assert.Same(workspace, viewModel.LoadedHexEditorWorkspace);
        Assert.Equal(settingsCount, viewModel.SettingsOverviewRows.Count);
        Assert.Equal(numberChoiceCount, viewModel.NumberSelectionChoices.Count);
        Assert.Equal(replaceSlotCount, viewModel.ReplaceSlots.Count);
        Assert.Equal(ShellPage.Home, viewModel.SelectedPage);
    }
}

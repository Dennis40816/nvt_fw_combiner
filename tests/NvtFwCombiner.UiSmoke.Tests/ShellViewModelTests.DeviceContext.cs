using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verifies a numeric IC selection never leaves the displayed number selector blank after an IC switch.</summary>
    [Fact]
    public void SwitchingToAliasOnlyIcNumberChoicesFallsBackToSingleChip()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = "3";

        viewModel.SelectedIc = "NT51923";

        Assert.Equal(WorkbenchIcNumberTokens.SingleChip, viewModel.SelectedNumber);
        Assert.Equal(WorkbenchIcNumberTokens.SingleChip, viewModel.SelectedNumberChoice?.Token);
        Assert.Contains(viewModel.NumberSelectionChoices, choice =>
            choice.Token == WorkbenchIcNumberTokens.SingleChip);
    }
}

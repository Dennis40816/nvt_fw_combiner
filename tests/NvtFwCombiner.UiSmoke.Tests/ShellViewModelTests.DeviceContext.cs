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

    /// <summary>NT51926 exposes its typed Number choice instead of the active-run read-only field.</summary>
    [Fact]
    public void Nt51926ReplaceShowsOneNumberSelectorAtRest()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51926";
        OpenReplace(viewModel, WorkbenchReplaceModes.CtrlRam);

        Assert.True(viewModel.IsDeviceContextNumberSelectionVisible);
        Assert.False(viewModel.IsRunInProgress);
        Assert.NotNull(viewModel.SelectedNumberChoice);
        Assert.NotEmpty(viewModel.SelectedNumberChoice.DisplayLabel);
    }

    /// <summary>NT51930 exposes only its implemented single and 2..13 postbuild plans.</summary>
    [Fact]
    public void Nt51930NumberChoicesExposeBoundedCascadeRange()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51930";

        Assert.Equal(
            [
                new IcNumberChoiceViewModel(WorkbenchIcNumberTokens.SingleChip, "1 IC"),
                new IcNumberChoiceViewModel(WorkbenchIcNumberTokens.CascadeTwoToThirteen, "2–13 IC"),
            ],
            viewModel.NumberSelectionChoices);
        Assert.DoesNotContain(
            viewModel.NumberSelectionChoices,
            static choice => choice.Token == WorkbenchIcNumberTokens.Cascade);
    }

    /// <summary>The consolidated IC detail exposes family, runtime, evidence, and support without badge-only meaning.</summary>
    [Fact]
    public void IcDetailTracksSelectedIcAndProvidesScreenReaderEquivalent()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.SelectedIc = "NT51929";

        Assert.Contains("IC Family source", viewModel.SelectedIcDetailFamily, StringComparison.Ordinal);
        Assert.Contains("AB", viewModel.SelectedIcDetailRuntime, StringComparison.Ordinal);
        Assert.Equal("! Open: DP, CtrlRAM, Customized", viewModel.SelectedIcDetailEvidence);
        Assert.DoesNotContain("golden", viewModel.SelectedIcDetailEvidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NT51929", viewModel.SelectedIcDetailAutomationText, StringComparison.Ordinal);
        Assert.Contains(viewModel.SelectedIcDetailEvidence, viewModel.SelectedIcDetailAutomationText, StringComparison.Ordinal);
        Assert.Contains(viewModel.SelectedIcDetailSupport, viewModel.SelectedIcDetailAutomationText, StringComparison.Ordinal);

        viewModel.SelectedIc = "NT51950";

        Assert.DoesNotContain("AB", viewModel.SelectedIcDetailRuntime, StringComparison.Ordinal);
        Assert.Equal("✓ Verified: DP · ! Open: CtrlRAM, Customized", viewModel.SelectedIcDetailEvidence);
        Assert.Contains("compiled profiles", viewModel.SelectedIcDetailSupport, StringComparison.Ordinal);

        viewModel.SelectedIc = "NT51931";

        Assert.Contains("— Unavailable: Customized", viewModel.SelectedIcDetailEvidence, StringComparison.Ordinal);
    }
}

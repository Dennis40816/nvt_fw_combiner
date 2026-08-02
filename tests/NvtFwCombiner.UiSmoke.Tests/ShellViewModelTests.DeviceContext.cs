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
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = "3";

        viewModel.WorkflowSession.SelectedIc = "NT51923";

        Assert.Equal(WorkbenchIcNumberTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);
        Assert.Equal(WorkbenchIcNumberTokens.SingleChip, viewModel.WorkflowSession.SelectedNumberChoice?.Token);
        Assert.Contains(viewModel.WorkflowSession.NumberSelectionChoices, choice =>
            choice.Token == WorkbenchIcNumberTokens.SingleChip);
    }

    /// <summary>NT51926 exposes its typed Number choice instead of the active-run read-only field.</summary>
    [Fact]
    public void Nt51926ReplaceShowsOneNumberSelectorAtRest()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, WorkbenchReplaceModes.CtrlRam);

        Assert.True(viewModel.WorkflowSession.IsDeviceContextNumberSelectionVisible);
        Assert.False(viewModel.RunSession.IsRunInProgress);
        Assert.NotNull(viewModel.WorkflowSession.SelectedNumberChoice);
        Assert.NotEmpty(viewModel.WorkflowSession.SelectedNumberChoice.DisplayLabel);
    }

    /// <summary>Leaving AB Merge restores the standard IC and Number projections for Replace.</summary>
    [Fact]
    public void ReplaceDoesNotInheritAbMergeSelectorProjection()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = WorkbenchMergeModes.AbCode;
        viewModel.WorkflowSession.SelectedNumber = WorkbenchIcNumberTokens.Cascade;

        viewModel.ShowReplaceCommand.Execute(null);

        Assert.True(viewModel.IsReplaceVisible);
        Assert.Equal(WorkbenchCompositionService.GetSupportedIcIds(), viewModel.WorkflowSession.IcChoices);
        Assert.NotEmpty(viewModel.WorkflowSession.NumberSelectionChoices);
        Assert.NotNull(viewModel.WorkflowSession.SelectedNumberChoice);

        viewModel.WorkflowSession.SelectedIc = "NT51929";

        Assert.Equal(
            [
                new IcNumberChoiceViewModel(WorkbenchIcNumberTokens.SingleChip, "1 IC"),
                new IcNumberChoiceViewModel(WorkbenchIcNumberTokens.CascadeTwoToEight, "2–8 IC"),
            ],
            viewModel.WorkflowSession.NumberSelectionChoices);
        Assert.Equal(WorkbenchIcNumberTokens.SingleChip, viewModel.WorkflowSession.SelectedNumberChoice?.Token);
    }

    /// <summary>Changing from AB Code to Standard Merge restores the full IC selector.</summary>
    [Fact]
    public void StandardMergeDoesNotInheritAbMergeIcChoices()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = WorkbenchMergeModes.AbCode;

        viewModel.Merge.SelectedMergeMode = WorkbenchMergeModes.Standard;

        Assert.True(viewModel.IsMergeVisible);
        Assert.Equal(WorkbenchCompositionService.GetSupportedIcIds(), viewModel.WorkflowSession.IcChoices);
    }

    /// <summary>The consolidated IC detail exposes family, runtime, evidence, and support without badge-only meaning.</summary>
    [Fact]
    public void IcDetailTracksSelectedIcAndProvidesScreenReaderEquivalent()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.WorkflowSession.SelectedIc = "NT51929";

        Assert.Contains("IC Family source", viewModel.WorkflowSession.SelectedIcDetailFamily, StringComparison.Ordinal);
        Assert.Contains("AB", viewModel.WorkflowSession.SelectedIcDetailRuntime, StringComparison.Ordinal);
        Assert.Equal("! Open: DP, CtrlRAM, Customized", viewModel.WorkflowSession.SelectedIcDetailEvidence);
        Assert.DoesNotContain("golden", viewModel.WorkflowSession.SelectedIcDetailEvidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NT51929", viewModel.WorkflowSession.SelectedIcDetailAutomationText, StringComparison.Ordinal);
        Assert.Contains(viewModel.WorkflowSession.SelectedIcDetailEvidence, viewModel.WorkflowSession.SelectedIcDetailAutomationText, StringComparison.Ordinal);
        Assert.Contains(viewModel.WorkflowSession.SelectedIcDetailSupport, viewModel.WorkflowSession.SelectedIcDetailAutomationText, StringComparison.Ordinal);

        viewModel.WorkflowSession.SelectedIc = "NT51950";

        Assert.Contains("AB", viewModel.WorkflowSession.SelectedIcDetailRuntime, StringComparison.Ordinal);
        Assert.Equal("✓ Verified: DP · ! Open: CtrlRAM, Customized", viewModel.WorkflowSession.SelectedIcDetailEvidence);
        Assert.Contains("compiled profile contracts", viewModel.WorkflowSession.SelectedIcDetailSupport, StringComparison.Ordinal);

    }
}

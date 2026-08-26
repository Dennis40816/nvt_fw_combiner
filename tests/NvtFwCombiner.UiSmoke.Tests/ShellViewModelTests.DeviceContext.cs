using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>An empty Application number disclosure stays empty instead of becoming a UI-authored topology.</summary>
    [Fact]
    public async Task EmptyPublishedNumberDisclosureFailsClosedWithoutUiFallback()
    {
        PresentationHostServices services =
            PresentationTestHost.CreateServices("0.10.6-empty-number-test");
        CapabilityCatalogReloadResult reload =
            await PresentationTestHost.LoadCanonicalCatalogAsync(
                services.CanonicalCatalogLoader,
                TestContext.Current.CancellationToken);
        CanonicalCapabilityCatalogSnapshot snapshot =
            Assert.IsType<CanonicalCapabilityCatalogSnapshot>(reload.Snapshot);
        CapabilitySelectorPublication publication = CapabilitySelectorPublication.Create(
            snapshot.ResolutionToken,
            snapshot.Capabilities,
            snapshot.DynamicRoutes,
            CanonicalCapabilityDisclosure.Empty);

        Assert.True(publication.IsWorkflowAuthorable(
            "NT51950",
            ExperienceIds.StandardMerge));
        Assert.Empty(publication.GetNumberSelectionChoices("NT51950"));
        Assert.Empty(UiCompositionRunner.GetNumberSelectionChoices(publication, "NT51950"));

        var setup = new WorkflowContextSetupViewModel();
        setup.Configure(
            publication,
            "NT51950",
            IcNumberSelectionTokens.SingleChip,
            showNumber: true);

        Assert.Empty(setup.NumberChoices);
        Assert.Equal(string.Empty, setup.SelectedNumber);
        Assert.Null(setup.SelectedNumberChoice);
    }

    /// <summary>Verifies a numeric IC selection never leaves the displayed number selector blank after an IC switch.</summary>
    [Fact]
    public void SwitchingToAliasOnlyIcNumberChoicesFallsBackToSingleChip()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = "3";

        viewModel.WorkflowSession.SelectedIc = "NT51923";

        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumberChoice?.Token);
        Assert.Contains(viewModel.WorkflowSession.NumberSelectionChoices, choice =>
            choice.Token == IcNumberSelectionTokens.SingleChip);
    }

    /// <summary>NT51926 exposes its typed Number choice instead of the active-run read-only field.</summary>
    [Fact]
    public void Nt51926ReplaceShowsOneNumberSelectorAtRest()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        Assert.True(viewModel.WorkflowSession.IsDeviceContextNumberSelectionVisible);
        Assert.False(viewModel.RunSession.IsRunInProgress);
        Assert.NotNull(viewModel.WorkflowSession.SelectedNumberChoice);
        Assert.NotEmpty(viewModel.WorkflowSession.SelectedNumberChoice.DisplayLabel);
    }

    /// <summary>Leaving AB Merge restores the standard IC and Number projections for Replace.</summary>
    [Fact]
    public void ReplaceDoesNotInheritAbMergeSelectorProjection()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;

        viewModel.ShowReplaceCommand.Execute(null);

        Assert.True(viewModel.IsReplaceVisible);
        Assert.Equal(TestProjection.GetIcIds(), viewModel.WorkflowSession.IcChoices);
        Assert.NotEmpty(viewModel.WorkflowSession.NumberSelectionChoices);
        Assert.NotNull(viewModel.WorkflowSession.SelectedNumberChoice);

        viewModel.WorkflowSession.SelectedIc = "NT51929";

        Assert.Equal(
            [
                new IcNumberChoiceViewModel(IcNumberSelectionTokens.SingleChip, "1 IC"),
                new IcNumberChoiceViewModel(IcNumberSelectionTokens.CascadeTwoToEight, "2–8 IC"),
            ],
            viewModel.WorkflowSession.NumberSelectionChoices);
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumberChoice?.Token);
    }

    /// <summary>Changing from AB Code to Standard Merge restores the full IC selector.</summary>
    [Fact]
    public void StandardMergeDoesNotInheritAbMergeIcChoices()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

        viewModel.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;

        Assert.True(viewModel.IsMergeVisible);
        Assert.Equal(TestProjection.GetIcIds(), viewModel.WorkflowSession.IcChoices);
    }

    /// <summary>The consolidated IC detail exposes family, runtime, evidence, and support without badge-only meaning.</summary>
    [Fact]
    public void IcDetailTracksSelectedIcAndProvidesScreenReaderEquivalent()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();

        viewModel.WorkflowSession.SelectedIc = "NT51929";

        Assert.Contains("Perfect IC Family", viewModel.WorkflowSession.SelectedIcDetailFamily, StringComparison.Ordinal);
        Assert.Contains("AB", viewModel.WorkflowSession.SelectedIcDetailRuntime, StringComparison.Ordinal);
        Assert.Equal("✓ Verified: CtrlRAM · ! Open: DP · — Unavailable: Customized", viewModel.WorkflowSession.SelectedIcDetailEvidence);
        Assert.DoesNotContain("golden", viewModel.WorkflowSession.SelectedIcDetailEvidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NT51929", viewModel.WorkflowSession.SelectedIcDetailAutomationText, StringComparison.Ordinal);
        Assert.Contains(viewModel.WorkflowSession.SelectedIcDetailEvidence, viewModel.WorkflowSession.SelectedIcDetailAutomationText, StringComparison.Ordinal);
        Assert.Contains(viewModel.WorkflowSession.SelectedIcDetailSupport, viewModel.WorkflowSession.SelectedIcDetailAutomationText, StringComparison.Ordinal);

        viewModel.WorkflowSession.SelectedIc = "NT51950";

        Assert.Contains("AB", viewModel.WorkflowSession.SelectedIcDetailRuntime, StringComparison.Ordinal);
        Assert.Equal("✓ Verified: CtrlRAM · ! Open: DP · — Unavailable: Customized", viewModel.WorkflowSession.SelectedIcDetailEvidence);
        Assert.Contains("compiled profile contracts", viewModel.WorkflowSession.SelectedIcDetailSupport, StringComparison.Ordinal);

    }
}

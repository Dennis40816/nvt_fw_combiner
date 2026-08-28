using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class DpReplaceWorkflowTests
{
    /// <summary>The shipped policy omits DP Replace without changing selector geometry.</summary>
    [Fact]
    public void ReplaceModeChoicesHideDpReplaceAndSelectAnAvailableMode()
    {
        MainWindowViewModel viewModel =
            PresentationTestHost.CreateProductViewModel();

        viewModel.ShowReplaceCommand.Execute(null);

        Assert.DoesNotContain(
            Domain.Composition.ExperienceIds.DpReplace,
            viewModel.Replace.ReplaceModeChoices);
        Assert.Contains(
            Domain.Composition.ExperienceIds.CtrlRamReplace,
            viewModel.Replace.ReplaceModeChoices);
        Assert.Equal(
            Domain.Composition.ExperienceIds.CtrlRamReplace,
            viewModel.Replace.SelectedReplaceMode);
        Assert.True(viewModel.Replace.IsCtrlRamReplaceModeSelected);
        Assert.False(viewModel.WorkflowSession.IsDpReplaceAvailable);
        string home = File.ReadAllText(RepositoryPaths.FromRepositoryRoot(
            "src",
            "NvtFwCombiner.Presentation.Avalonia",
            "Resources",
            "MainWindowPageTemplates.axaml"));
        Assert.Contains(
            "IsVisible=\"{Binding WorkflowSession.IsDpReplaceAvailable}\"",
            home,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A legacy page-global cascade selection cannot reopen the retired DP Replace workflow.
    /// This covers the NT51929 / cascade_2to8 state that v0.9.16 allowed the desktop UI to submit.
    /// </summary>
    [Fact]
    public void ProductPolicyRejectsLegacyNt51929CascadeStateBeforeDpReplaceExecution()
    {
        MainWindowViewModel viewModel =
            PresentationTestHost.CreateProductViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.WorkflowSession.SelectedNumber = "cascade_2to8";

        Assert.False(viewModel.BeginDpReplaceFromHomeCommand.CanExecute(null));

        viewModel.ShowReplaceCommand.Execute(null);
        string admittedMode = viewModel.Replace.SelectedReplaceMode;
        viewModel.Replace.SelectedReplaceMode = Domain.Composition.ExperienceIds.DpReplace;

        Assert.Equal(admittedMode, viewModel.Replace.SelectedReplaceMode);
        Assert.NotEqual(
            Domain.Composition.ExperienceIds.DpReplace,
            viewModel.Replace.SelectedReplaceMode);
        Assert.DoesNotContain(
            Domain.Composition.ExperienceIds.DpReplace,
            viewModel.Replace.ReplaceModeChoices);
    }

    /// <summary>General Replace cannot inherit NT51926 CtrlRAM cascade state.</summary>
    [Fact]
    public void GeneralReplaceReconcilesToItsCompiledSingleSelectorPolicy()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateProductViewModel();
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.Replace.SelectedReplaceMode = ExperienceIds.CtrlRamReplace;
        Assert.Contains(
            viewModel.WorkflowSession.NumberSelectionChoices,
            static choice => choice.Token == IcNumberSelectionTokens.Cascade);
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;

        viewModel.Replace.SelectedReplaceMode = ExperienceIds.GeneralReplace;

        Assert.Equal(
            [IcNumberSelectionTokens.SingleChip],
            viewModel.WorkflowSession.NumberSelectionChoices.Select(static choice => choice.Token));
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);
        Assert.Equal(
            IcNumberSelectionTokens.SingleChip,
            viewModel.WorkflowSession.GetWorkflowPageNumber(WorkflowInspectionOwner.Replace));

        viewModel.Replace.SelectedReplaceMode = ExperienceIds.CtrlRamReplace;

        Assert.Contains(
            viewModel.WorkflowSession.NumberSelectionChoices,
            static choice => choice.Token == IcNumberSelectionTokens.Cascade);
        Assert.Equal(IcNumberSelectionTokens.Cascade, viewModel.WorkflowSession.SelectedNumber);

        viewModel.ShowHomeCommand.Execute(null);
        viewModel.ShowReplaceCommand.Execute(null);

        Assert.Equal(ExperienceIds.CtrlRamReplace, viewModel.Replace.SelectedReplaceMode);
        Assert.Equal(IcNumberSelectionTokens.Cascade, viewModel.WorkflowSession.SelectedNumber);

        viewModel.Replace.SelectedReplaceMode = ExperienceIds.GeneralReplace;

        Assert.Equal(
            [IcNumberSelectionTokens.SingleChip],
            viewModel.WorkflowSession.NumberSelectionChoices.Select(static choice => choice.Token));
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);
    }

    /// <summary>Home shortcuts retain each Replace mode's own Number without cross-mode contamination.</summary>
    [Fact]
    public void ReplaceHomeShortcutsKeepModeSpecificNumberSelections()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateProductViewModel();
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.Replace.SelectedReplaceMode = ExperienceIds.CtrlRamReplace;
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        viewModel.ShowHomeCommand.Execute(null);

        viewModel.BeginGeneralReplaceFromHomeCommand.Execute(null);
        Assert.Equal(
            IcNumberSelectionTokens.SingleChip,
            viewModel.WorkflowSession.WorkflowContextSetup.SelectedNumber);
        viewModel.WorkflowSession.ConfirmWorkflowContextCommand.Execute(null);
        Assert.Equal(ExperienceIds.GeneralReplace, viewModel.Replace.SelectedReplaceMode);
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);
        viewModel.ShowHomeCommand.Execute(null);

        viewModel.BeginCtrlRamReplaceFromHomeCommand.Execute(null);
        Assert.Equal(
            IcNumberSelectionTokens.Cascade,
            viewModel.WorkflowSession.WorkflowContextSetup.SelectedNumber);
        viewModel.WorkflowSession.ConfirmWorkflowContextCommand.Execute(null);
        Assert.Equal(ExperienceIds.CtrlRamReplace, viewModel.Replace.SelectedReplaceMode);
        Assert.Equal(IcNumberSelectionTokens.Cascade, viewModel.WorkflowSession.SelectedNumber);
        viewModel.ShowHomeCommand.Execute(null);

        viewModel.BeginGeneralReplaceFromHomeCommand.Execute(null);
        Assert.Equal(
            IcNumberSelectionTokens.SingleChip,
            viewModel.WorkflowSession.WorkflowContextSetup.SelectedNumber);
    }

    /// <summary>Canonical evidence status does not disable an evidence-gated authoring flow.</summary>
    [Fact]
    public void ReplaceEvidenceBadgeDoesNotTurnPendingGoldenIntoFeatureBan()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.DpReplace);

        Assert.True(viewModel.Replace.IsSelectedReplaceModeEvidenceGated);
        Assert.Equal("Evidence open", viewModel.Replace.SelectedReplaceModeEvidenceLabel);
        Assert.Equal("Base firmware (FlashCode)", viewModel.Replace.ReplaceBaseSlot.Title);
        Assert.Contains("Complete FlashCode", viewModel.Replace.ReplaceBaseSlot.Description, StringComparison.Ordinal);
        Assert.Contains("Only declared DP ranges change", viewModel.Replace.ReplaceBaseSlot.Description, StringComparison.Ordinal);

        viewModel.WorkflowSession.SelectedIc = "NT51928";
        OpenReplace(viewModel, Domain.Composition.ExperienceIds.CtrlRamReplace);

        Assert.True(viewModel.Replace.IsSelectedReplaceModeEvidenceGated);
        Assert.True(viewModel.Replace.IsCtrlRamReplaceModeSelected);
        Assert.NotEmpty(viewModel.Replace.ReplaceSlots);
        Assert.Equal("Evidence open", viewModel.Replace.SelectedReplaceModeEvidenceLabel);
        Assert.Contains("does not ban authoring", viewModel.Replace.SelectedReplaceModeEvidenceTooltip, StringComparison.Ordinal);
    }

    /// <summary>Owner-declared perfect/partial IC-family facts are visible with their reuse boundary.</summary>
    [Theory]
    [InlineData("NT51917", "Perfect IC Family")]
    [InlineData("NT51928", "Partial IC Family")]
    [InlineData("NT51927", "Perfect IC Family")]
    public void IcFamilyBadgeExplainsOwnerDeclaredReuse(string icId, string expectedLabel)
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = icId;

        Assert.True(viewModel.WorkflowSession.HasSelectedIcFamily);
        Assert.Equal(expectedLabel, viewModel.WorkflowSession.SelectedIcFamilyLabel);
        Assert.Contains("Reusable scope:", viewModel.WorkflowSession.SelectedIcFamilyTooltip, StringComparison.Ordinal);
        Assert.DoesNotContain("Canonical IC:", viewModel.WorkflowSession.SelectedIcFamilyTooltip, StringComparison.Ordinal);
        Assert.Contains("never expands executable ranges", viewModel.WorkflowSession.SelectedIcFamilyTooltip, StringComparison.Ordinal);
    }
}

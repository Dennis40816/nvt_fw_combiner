using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

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

using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Canonical evidence status does not disable an evidence-gated authoring flow.</summary>
    [Fact]
    public void ReplaceEvidenceBadgeDoesNotTurnPendingGoldenIntoFeatureBan()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, "DP");

        Assert.True(viewModel.Replace.IsSelectedReplaceModeEvidenceGated);
        Assert.Equal("Evidence open", viewModel.Replace.SelectedReplaceModeEvidenceLabel);
        Assert.Equal("Base firmware (FlashCode)", viewModel.Replace.ReplaceBaseSlot.Title);
        Assert.Contains("Complete FlashCode", viewModel.Replace.ReplaceBaseSlot.Description, StringComparison.Ordinal);
        Assert.Contains("Only declared DP ranges change", viewModel.Replace.ReplaceBaseSlot.Description, StringComparison.Ordinal);

        viewModel.WorkflowSession.SelectedIc = "NT51932";
        OpenReplace(viewModel, "CtrlRAM");

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
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.WorkflowSession.SelectedIc = icId;

        Assert.True(viewModel.WorkflowSession.HasSelectedIcFamily);
        Assert.Equal(expectedLabel, viewModel.WorkflowSession.SelectedIcFamilyLabel);
        Assert.Contains("Reusable scope:", viewModel.WorkflowSession.SelectedIcFamilyTooltip, StringComparison.Ordinal);
        Assert.DoesNotContain("Canonical IC:", viewModel.WorkflowSession.SelectedIcFamilyTooltip, StringComparison.Ordinal);
        Assert.Contains("never expands executable ranges", viewModel.WorkflowSession.SelectedIcFamilyTooltip, StringComparison.Ordinal);
    }
}

using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>NT51931 General Replace remains unavailable even though DP Replace is supported.</summary>
    [Fact]
    public void Nt51931ReplaceShowsCatalogSupportGate()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51931";
        OpenReplace(viewModel, "General");

        Assert.False(viewModel.CanBuildReplace);
        Assert.False(viewModel.PreviewReplaceCommand.CanExecute(null));
        Assert.Empty(viewModel.ReplaceSlots);
        Assert.False(viewModel.IsGeneralReplaceModeSelected);
        Assert.False(viewModel.IsStructuredReplaceModeSelected);
        Assert.Equal("NT51931 Replace: Not available.", viewModel.ReplaceReadinessStatus);
        Assert.Equal("Not available", viewModel.ReplaceMemoryRangeLabel);
        Assert.True(viewModel.IsSelectedReplaceModeUnavailable);
        Assert.Equal("Not available", viewModel.SelectedReplaceModeEvidenceLabel);
        Assert.Contains("Open condition", viewModel.SelectedReplaceModeEvidenceTooltip, StringComparison.Ordinal);
    }

    /// <summary>Golden status reports verification without disabling an evidence-gated authoring flow.</summary>
    [Fact]
    public void ReplaceEvidenceBadgeDoesNotTurnPendingGoldenIntoFeatureBan()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        OpenReplace(viewModel, "DP");

        Assert.True(viewModel.IsSelectedReplaceModeGoldenVerified);
        Assert.Equal("Golden verified", viewModel.SelectedReplaceModeEvidenceLabel);
        Assert.Equal("Base firmware (FlashCode)", viewModel.ReplaceBaseSlot.Title);
        Assert.Contains("Standard/Normal Merge FlashCode", viewModel.ReplaceBaseSlot.Description, StringComparison.Ordinal);

        viewModel.SelectedIc = "NT51932";
        OpenReplace(viewModel, "CtrlRAM");

        Assert.True(viewModel.IsSelectedReplaceModeEvidenceGated);
        Assert.True(viewModel.IsCtrlRamReplaceModeSelected);
        Assert.NotEmpty(viewModel.ReplaceSlots);
        Assert.Equal("Evidence open", viewModel.SelectedReplaceModeEvidenceLabel);
        Assert.Contains("does not ban authoring", viewModel.SelectedReplaceModeEvidenceTooltip, StringComparison.Ordinal);
    }

    /// <summary>NT51930 exposes its canonical-map DP Replace contract while direct golden evidence remains open.</summary>
    [Fact]
    public void Nt51930DpReplaceIsAvailableWithEvidenceOpenBadge()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51930";
        OpenReplace(viewModel, "DP");

        Assert.True(viewModel.IsSelectedReplaceModeEvidenceGated);
        Assert.True(viewModel.IsNonCtrlRamStructuredReplaceModeSelected);
        Assert.Equal("Evidence open", viewModel.SelectedReplaceModeEvidenceLabel);
        Assert.Contains(viewModel.ReplaceSlots, static slot => slot.SlotId == "replace-base");
        FirmwareSlotViewModel replacement = Assert.Single(
            viewModel.ReplaceSlots,
            static slot => slot.SlotId == "replace-dp");
        Assert.Contains("0x6000", replacement.Description, StringComparison.Ordinal);
        Assert.Contains("0x40000", replacement.Description, StringComparison.Ordinal);
        Assert.Contains("does not ban authoring", viewModel.SelectedReplaceModeEvidenceTooltip, StringComparison.Ordinal);
    }

    /// <summary>Owner-declared perfect/partial IC-family facts are visible with their reuse boundary.</summary>
    [Theory]
    [InlineData("NT51917", "Perfect IC Family")]
    [InlineData("NT51928", "Partial IC Family")]
    [InlineData("NT51927", "IC Family source")]
    public void IcFamilyBadgeExplainsOwnerDeclaredReuse(string icId, string expectedLabel)
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = icId;

        Assert.True(viewModel.HasSelectedIcFamily);
        Assert.Equal(expectedLabel, viewModel.SelectedIcFamilyLabel);
        Assert.Contains("Canonical IC: NT51927", viewModel.SelectedIcFamilyTooltip, StringComparison.Ordinal);
        Assert.Contains("never expands executable ranges", viewModel.SelectedIcFamilyTooltip, StringComparison.Ordinal);
    }
}

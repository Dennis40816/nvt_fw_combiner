using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>Cancel keeps Merge selections; confirmation clears only files and then completes navigation.</summary>
    [Fact]
    public void MergeNavigationWarnsBeforeClearingSelectedFiles()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-navigation-clear-merge");
        string inputPath = workspace.Write("input.bin", [0x10, 0x11]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        GeneralMergeMappingViewModel mapping = Assert.Single(viewModel.Merge.GeneralMergeMappings);
        mapping.SourceStartAddress = "0x1";
        mapping.TargetStartAddress = "0x2";
        mapping.Length = "0x1";
        viewModel.SetSlotFile("merge-dp", inputPath);
        viewModel.SetSlotFile(mapping.MappingId, inputPath);

        viewModel.ShowHomeCommand.Execute(null);

        Assert.True(viewModel.IsNavigationClearConfirmationOpen);
        Assert.True(viewModel.IsMergeVisible);
        Assert.Contains(viewModel.Merge.MergeSlots, static slot => slot.HasFile);
        Assert.True(mapping.HasFile);

        viewModel.CancelNavigationClearCommand.Execute(null);

        Assert.False(viewModel.IsNavigationClearConfirmationOpen);
        Assert.True(viewModel.IsMergeVisible);
        Assert.True(mapping.HasFile);

        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.ConfirmNavigationAndClearCommand.Execute(null);

        Assert.True(viewModel.IsReplaceVisible);
        Assert.All(viewModel.Merge.MergeSlots, static slot => Assert.False(slot.HasFile));
        Assert.False(mapping.HasFile);
        Assert.Equal("0x1", mapping.SourceStartAddress);
        Assert.Equal("0x2", mapping.TargetStartAddress);
        Assert.Equal("0x1", mapping.Length);
        Assert.Equal("NT51927", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(ExperienceIds.GeneralMerge, viewModel.Merge.SelectedMergeMode);
    }

    /// <summary>AB inputs participate in the same navigation warning and are cleared before re-entry.</summary>
    [Fact]
    public async Task AbMergeNavigationWarnsAndClearsActiveProfileSlotsAsync()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-navigation-clear-ab");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.DpAbInput,
            workspace.Write("dp-ab.bin", new byte[0x80000]),
            TestContext.Current.CancellationToken);

        viewModel.ShowHomeCommand.Execute(null);

        Assert.True(viewModel.IsNavigationClearConfirmationOpen);
        Assert.True(viewModel.IsMergeVisible);
        Assert.True(viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionAddressSpaceIds.DpAbInput).HasFile);

        viewModel.ConfirmNavigationAndClearCommand.Execute(null);
        viewModel.ShowMergeCommand.Execute(null);

        Assert.True(viewModel.Merge.IsAbCodeMergeModeSelected);
        Assert.All(viewModel.Merge.MergeSlots, static slot => Assert.False(slot.HasFile));
    }

    /// <summary>Cached AB inputs still participate in the navigation guard after another Merge mode hides them.</summary>
    [Theory]
    [InlineData(ExperienceIds.StandardMerge)]
    [InlineData(ExperienceIds.GeneralMerge)]
    public async Task HiddenAbMergeSlotsWarnAndClearAcrossModesAndProfilesAsync(string nextMode)
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-navigation-clear-hidden-ab");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.DpAbInput,
            workspace.Write("dp-ab.bin", new byte[0x80000]),
            TestContext.Current.CancellationToken);

        viewModel.WorkflowSession.SelectedIc = "NT51919";
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput,
            workspace.Write("tpa.bin", new byte[0x40000]),
            TestContext.Current.CancellationToken);
        viewModel.Merge.SelectedMergeMode = nextMode;

        Assert.DoesNotContain(viewModel.Merge.MergeSlots, static slot =>
            slot.SlotId is CompositionAddressSpaceIds.DpAbInput or CompositionAddressSpaceIds.TpAInput);

        viewModel.ShowHomeCommand.Execute(null);

        Assert.True(viewModel.IsNavigationClearConfirmationOpen);
        Assert.True(viewModel.IsMergeVisible);

        viewModel.ConfirmNavigationAndClearCommand.Execute(null);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51932";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

        Assert.All(viewModel.Merge.MergeSlots, static slot => Assert.False(slot.HasFile));
    }

    /// <summary>Replace confirmation clears Base and mapping files while preserving device and mapping context.</summary>
    [Fact]
    public void ReplaceNavigationClearsFilesButKeepsAuthoringContext()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-navigation-clear-replace");
        string inputPath = workspace.Write("input.bin", [0x20, 0x21]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = "2";
        OpenReplace(viewModel, ExperienceIds.GeneralReplace);
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        mapping.TargetStartAddress = "0x10";
        mapping.Length = "0x2";
        viewModel.SetSlotFile("replace-base", inputPath);
        viewModel.SetSlotFile(mapping.MappingId, inputPath);

        viewModel.ShowSettingsCommand.Execute(null);

        Assert.True(viewModel.IsNavigationClearConfirmationOpen);
        Assert.True(viewModel.IsReplaceVisible);

        viewModel.ConfirmNavigationAndClearCommand.Execute(null);

        Assert.True(viewModel.IsSettingsVisible);
        Assert.False(viewModel.Replace.ReplaceBaseSlot.HasFile);
        Assert.All(viewModel.Replace.ReplaceSlots, static slot => Assert.False(slot.HasFile));
        Assert.False(mapping.HasFile);
        Assert.Equal("0x10", mapping.TargetStartAddress);
        Assert.Equal("0x2", mapping.Length);
        Assert.Equal("NT51927", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal("2", viewModel.WorkflowSession.SelectedNumber);
        Assert.Equal(ExperienceIds.GeneralReplace, viewModel.Replace.SelectedReplaceMode);
    }

    /// <summary>Merge mode binding writes stay on Replace and keep its selected Base firmware.</summary>
    [Theory]
    [InlineData(ExperienceIds.StandardMerge)]
    [InlineData(ExperienceIds.GeneralMerge)]
    public void MergeModeBindingWritesDoNotNavigateAwayFromReplace(string mergeMode)
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-navigation-merge-mode-binding");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        viewModel.SetSlotFile("replace-base", workspace.Write("base.bin", [0x10, 0x11]));

        viewModel.Merge.SelectedMergeMode = mergeMode;

        Assert.True(viewModel.IsReplaceVisible);
        Assert.False(viewModel.IsNavigationClearConfirmationOpen);
        Assert.True(viewModel.Replace.ReplaceBaseSlot.HasFile);
        Assert.Equal(mergeMode, viewModel.Merge.SelectedMergeMode);
    }

    /// <summary>Refreshing Merge choices after an IC change cannot request navigation from Replace.</summary>
    [Fact]
    public void IcChangeDoesNotOpenNavigationClearConfirmationOnReplace()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-navigation-ic-change");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        viewModel.SetSlotFile("replace-base", workspace.Write("base.bin", [0x10, 0x11]));

        viewModel.WorkflowSession.SelectedIc = "NT51928";

        Assert.True(viewModel.IsReplaceVisible);
        Assert.False(viewModel.IsNavigationClearConfirmationOpen);
        Assert.True(viewModel.Replace.ReplaceBaseSlot.HasFile);
    }

    /// <summary>An explicit workflow navigation still uses the guard and identifies its requested page.</summary>
    [Fact]
    public void ExplicitMergeNavigationShowsPendingRouteAndPreservesCancelBehavior()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-navigation-explicit-merge");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        viewModel.SetSlotFile("replace-base", workspace.Write("base.bin", [0x10, 0x11]));

        viewModel.ShowMergeCommand.Execute(null);

        Assert.True(viewModel.IsNavigationClearConfirmationOpen);
        Assert.True(viewModel.IsReplaceVisible);
        Assert.Equal("Replace → Merge", viewModel.NavigationClearRoute);

        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.True(viewModel.IsNavigationClearConfirmationOpen);
        Assert.Equal("取代 → 合併", viewModel.NavigationClearRoute);

        viewModel.CancelNavigationClearCommand.Execute(null);

        Assert.False(viewModel.IsNavigationClearConfirmationOpen);
        Assert.True(viewModel.IsReplaceVisible);
        Assert.True(viewModel.Replace.ReplaceBaseSlot.HasFile);
        Assert.Equal("首頁 > 取代", viewModel.NavigationClearRoute);
    }

    /// <summary>Back navigation does not mutate history or inputs until its clear action is confirmed.</summary>
    [Fact]
    public void BackNavigationWaitsForClearConfirmation()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-navigation-clear-back");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.SetSlotFile("merge-dp", workspace.Write("dp.bin", [0x30]));

        viewModel.GoBackCommand.Execute(null);

        Assert.True(viewModel.IsNavigationClearConfirmationOpen);
        Assert.True(viewModel.IsMergeVisible);
        Assert.True(viewModel.CanGoBack);

        viewModel.ConfirmNavigationAndClearCommand.Execute(null);

        Assert.True(viewModel.IsHomeVisible);
        Assert.False(viewModel.CanGoBack);
        Assert.All(viewModel.Merge.MergeSlots, static slot => Assert.False(slot.HasFile));
    }

    /// <summary>Navigation warning text remains complete in both supported languages.</summary>
    [Theory]
    [InlineData(ShellLanguage.English, "Clear selected files", "Clear and continue")]
    [InlineData(ShellLanguage.ChineseTraditional, "清除已選檔案", "清除並繼續")]
    public void NavigationClearWarningIsLocalized(
        ShellLanguage language,
        string expectedTitle,
        string expectedAction)
    {
        var text = ShellTextResources.For(language);

        Assert.Contains(expectedTitle, text.NavigationClearTitle, StringComparison.Ordinal);
        Assert.Equal(expectedAction, text.NavigationClearConfirmLabel);
        Assert.False(string.IsNullOrWhiteSpace(text.NavigationClearDetail));
        Assert.False(string.IsNullOrWhiteSpace(text.NavigationClearCancelLabel));
    }
}

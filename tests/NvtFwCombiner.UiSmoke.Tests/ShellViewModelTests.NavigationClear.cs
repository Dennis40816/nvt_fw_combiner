using System.Text.Json;
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
        string expectedReplaceIc = viewModel.WorkflowSession.SelectedIc;
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        GeneralMergeMappingViewModel mapping = Assert.Single(viewModel.Merge.GeneralMergeMappings);
        mapping.SourceStartAddress = "0x1";
        mapping.TargetStartAddress = "0x2";
        mapping.Length = "0x1";
        viewModel.SetSlotFile(mapping.MappingId, inputPath);

        viewModel.ShowHomeCommand.Execute(null);

        Assert.True(viewModel.Navigation.IsNavigationClearConfirmationOpen);
        Assert.True(viewModel.IsMergeVisible);
        Assert.True(mapping.HasFile);

        viewModel.Navigation.CancelNavigationClearCommand.Execute(null);

        Assert.False(viewModel.Navigation.IsNavigationClearConfirmationOpen);
        Assert.True(viewModel.IsMergeVisible);
        Assert.True(mapping.HasFile);

        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.Navigation.ConfirmNavigationAndClearCommand.Execute(null);

        Assert.True(viewModel.IsReplaceVisible);
        Assert.All(viewModel.Merge.MergeSlots, static slot => Assert.False(slot.HasFile));
        Assert.False(mapping.HasFile);
        Assert.Equal("0x1", mapping.SourceStartAddress);
        Assert.Equal("0x2", mapping.TargetStartAddress);
        Assert.Equal("0x1", mapping.Length);
        Assert.Equal(expectedReplaceIc, viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(ExperienceIds.GeneralMerge, viewModel.Merge.SelectedMergeMode);
        viewModel.ShowMergeCommand.Execute(null);
        Assert.Equal("NT51927", viewModel.WorkflowSession.SelectedIc);
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

        Assert.True(viewModel.Navigation.IsNavigationClearConfirmationOpen);
        Assert.True(viewModel.IsMergeVisible);
        Assert.True(viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionAddressSpaceIds.DpAbInput).HasFile);

        viewModel.Navigation.ConfirmNavigationAndClearCommand.Execute(null);
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

        Assert.True(viewModel.Navigation.IsNavigationClearConfirmationOpen);
        Assert.True(viewModel.IsMergeVisible);

        viewModel.Navigation.ConfirmNavigationAndClearCommand.Execute(null);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51932";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

        Assert.All(viewModel.Merge.MergeSlots, static slot => Assert.False(slot.HasFile));
    }

    /// <summary>A Standard input hidden by another Merge mode remains guarded and is cleared on page exit.</summary>
    [Fact]
    public async Task HiddenStandardMergeSelectionIsClearedBeforeLeavingMergeAsync()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("dp-input")),
            TestContext.Current.CancellationToken);
        Assert.True(viewModel.Merge.MergeDpSlot.HasFile);

        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        Assert.True(viewModel.Merge.IsGeneralMergeModeSelected);
        Assert.False(viewModel.Merge.IsNormalMergeModeSelected);
        Assert.True(viewModel.Merge.MergeDpSlot.HasFile);
        viewModel.ShowHomeCommand.Execute(null);

        Assert.True(viewModel.Navigation.IsNavigationClearConfirmationOpen);
        viewModel.Navigation.ConfirmNavigationAndClearCommand.Execute(null);
        Assert.True(viewModel.IsHomeVisible);
        Assert.False(viewModel.Merge.MergeDpSlot.HasFile);

        viewModel.ShowMergeCommand.Execute(null);
        viewModel.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;
        Assert.All(viewModel.Merge.MergeSlots, static slot => Assert.False(slot.HasFile));
    }

    /// <summary>A CtrlRAM session cannot survive the shared Replace page-exit clear through another mode.</summary>
    [Fact]
    public async Task HiddenCtrlRamSessionIsClearedBeforeLeavingReplaceAsync()
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51926-fw200-single-auto-prj-597-20260718");
        JsonElement baseArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("artifactId").GetString() == "tp-input");
        JsonElement replacementArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("artifactId").GetString() == "normal-ctrlram-input");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            CanonicalGoldenTestData.ArtifactPath(baseArtifact),
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            "replace-ctrlram-normal",
            CanonicalGoldenTestData.ArtifactPath(replacementArtifact),
            TestContext.Current.CancellationToken);
        Assert.True(viewModel.Replace.ReplaceBaseSlot.HasFile);
        Assert.True(viewModel.Replace.ReplaceSlots.Single(static slot =>
            slot.SlotId == "replace-ctrlram-normal").HasFile);
        Assert.True(viewModel.Replace.Inspection.State is WorkflowInspectionAttemptState.Succeeded);

        viewModel.Replace.SelectedReplaceMode = ExperienceIds.GeneralReplace;
        viewModel.ShowHomeCommand.Execute(null);

        Assert.True(viewModel.Navigation.IsNavigationClearConfirmationOpen);
        viewModel.Navigation.ConfirmNavigationAndClearCommand.Execute(null);
        Assert.True(viewModel.IsHomeVisible);
        Assert.False(viewModel.Replace.ReplaceBaseSlot.HasFile);

        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        Assert.All(viewModel.Replace.ReplaceSlots, static slot => Assert.False(slot.HasFile));
        Assert.False(viewModel.Replace.CanBuildReplace);
    }

    /// <summary>Settings bypasses navigation clearing and preserves Replace files and mapping context.</summary>
    [Fact]
    public void SettingsModalPreservesReplaceFilesAndAuthoringContext()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-navigation-clear-replace");
        string inputPath = workspace.Write("input.bin", [0x20, 0x21]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
        OpenReplace(viewModel, ExperienceIds.GeneralReplace);
        GeneralReplaceMappingViewModel mapping = Assert.Single(viewModel.Replace.GeneralReplaceMappings);
        mapping.TargetStartAddress = "0x10";
        mapping.Length = "0x2";
        viewModel.SetSlotFile("replace-base", inputPath);
        viewModel.SetSlotFile(mapping.MappingId, inputPath);

        viewModel.OpenSettingsCommand.Execute(null);

        Assert.False(viewModel.Navigation.IsNavigationClearConfirmationOpen);
        Assert.True(viewModel.IsReplaceVisible);
        Assert.True(viewModel.IsSettingsModalOpen);
        Assert.True(viewModel.Replace.ReplaceBaseSlot.HasFile);
        Assert.True(mapping.HasFile);
        Assert.Equal("0x10", mapping.TargetStartAddress);
        Assert.Equal("0x2", mapping.Length);
        Assert.Equal("NT51926", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);
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
        Assert.False(viewModel.Navigation.IsNavigationClearConfirmationOpen);
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
        Assert.False(viewModel.Navigation.IsNavigationClearConfirmationOpen);
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

        Assert.True(viewModel.Navigation.IsNavigationClearConfirmationOpen);
        Assert.True(viewModel.IsReplaceVisible);
        Assert.Equal("Replace → Merge", viewModel.Navigation.NavigationClearRoute);

        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.True(viewModel.Navigation.IsNavigationClearConfirmationOpen);
        Assert.Equal("取代 → 合併", viewModel.Navigation.NavigationClearRoute);

        viewModel.Navigation.CancelNavigationClearCommand.Execute(null);

        Assert.False(viewModel.Navigation.IsNavigationClearConfirmationOpen);
        Assert.True(viewModel.IsReplaceVisible);
        Assert.True(viewModel.Replace.ReplaceBaseSlot.HasFile);
        Assert.Equal("首頁 > 取代", viewModel.Navigation.NavigationClearRoute);
    }

    /// <summary>Back navigation does not mutate history or inputs until its clear action is confirmed.</summary>
    [Fact]
    public void BackNavigationWaitsForClearConfirmation()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-navigation-clear-back");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.SetSlotFile("merge-dp", workspace.Write("dp.bin", [0x30]));

        viewModel.Navigation.GoBackCommand.Execute(null);

        Assert.True(viewModel.Navigation.IsNavigationClearConfirmationOpen);
        Assert.True(viewModel.IsMergeVisible);
        Assert.True(viewModel.Navigation.CanGoBack);

        viewModel.Navigation.ConfirmNavigationAndClearCommand.Execute(null);

        Assert.True(viewModel.IsHomeVisible);
        Assert.False(viewModel.Navigation.CanGoBack);
        Assert.All(viewModel.Merge.MergeSlots, static slot => Assert.False(slot.HasFile));
    }

    /// <summary>Navigation warning text remains complete in both supported languages.</summary>
    [Theory]
    [InlineData("English", "Clear selected files", "Clear and continue")]
    [InlineData("ChineseTraditional", "清除已選檔案", "清除並繼續")]
    public void NavigationClearWarningIsLocalized(
        string languageName,
        string expectedTitle,
        string expectedAction)
    {
        ShellLanguage language = Enum.Parse<ShellLanguage>(languageName);
        var text = ShellTextResources.For(language);

        Assert.Contains(expectedTitle, text.NavigationClearTitle, StringComparison.Ordinal);
        Assert.Equal(expectedAction, text.NavigationClearConfirmLabel);
        Assert.False(string.IsNullOrWhiteSpace(text.NavigationClearDetail));
        Assert.False(string.IsNullOrWhiteSpace(text.NavigationClearCancelLabel));
    }
}

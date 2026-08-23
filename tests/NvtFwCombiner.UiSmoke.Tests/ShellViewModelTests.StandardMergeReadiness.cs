using System.Text.Json;
using Avalonia.Headless.XUnit;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>Selected Standard Merge sources reach terminal shared health before Build is admitted.</summary>
    [Fact]
    public async Task StandardMergeBuildRequiresCurrentTerminalSlotHealth()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        MainWindowViewModel viewModel = await PresentationTestHost.CreateViewModelAsync(
            TestContext.Current.CancellationToken);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";

        string dpPath = golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("dp-input"));
        string tpPath = golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("tp-input"));
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            dpPath,
            TestContext.Current.CancellationToken);

        FirmwareSlotViewModel dp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeDp);
        Assert.Contains(
            dp.SemanticState,
            new[] { FirmwareSlotSemanticState.Verified, FirmwareSlotSemanticState.Warning });
        Assert.False(dp.IsInputInspectionPending);
        Assert.False(viewModel.Merge.CanBuildMerge);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeTp,
            tpPath,
            TestContext.Current.CancellationToken);

        Assert.All(viewModel.Merge.MergeSlots.Where(static slot => !slot.IsOptional), static slot =>
        {
            Assert.Contains(
                slot.SemanticState,
                new[] { FirmwareSlotSemanticState.Verified, FirmwareSlotSemanticState.Warning });
            Assert.False(slot.IsInputInspectionPending);
        });
        Assert.True(viewModel.Merge.CanBuildMerge);
    }

    /// <summary>An invalid extension remains selected as visible Error and a later BIN selection recovers.</summary>
    [Fact]
    public async Task StandardMergeExtensionErrorIsRetainedUntilAcceptedSelectionRecovers()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        string sourcePath = golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("dp-input"));
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-extension-admission");
        byte[] source = File.ReadAllBytes(sourcePath);
        string rejectedPath = workspace.Write("dp-input.txt", source);
        string recoveredPath = workspace.Write("dp-input.BIN", source);
        MainWindowViewModel viewModel = await PresentationTestHost.CreateViewModelAsync(
            TestContext.Current.CancellationToken);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            rejectedPath,
            TestContext.Current.CancellationToken);

        FirmwareSlotViewModel dp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeDp);
        Assert.Equal(rejectedPath, dp.FilePath);
        Assert.Equal(FirmwareSlotSemanticState.Error, dp.SemanticState);
        Assert.Equal(FirmwareInputInspectionSeverity.Blocking, dp.InputInspectionSeverity);
        Assert.Contains("extension", dp.InputInspectionStatus, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.Merge.CanBuildMerge);

        viewModel.SelectedLanguage = "Traditional Chinese";
        Assert.Contains("副檔名", dp.InputInspectionStatus, StringComparison.Ordinal);
        Assert.Contains("副檔名", dp.SemanticStateAutomationText, StringComparison.Ordinal);
        viewModel.SelectedLanguage = "English";

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            recoveredPath,
            TestContext.Current.CancellationToken);

        Assert.Equal(recoveredPath, dp.FilePath);
        Assert.Contains(
            dp.SemanticState,
            new[] { FirmwareSlotSemanticState.Verified, FirmwareSlotSemanticState.Warning });
        Assert.DoesNotContain("extension", dp.InputInspectionStatus, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A multi-map Standard Merge route exposes the compiler-required DP prerequisite without IC rules in UI.</summary>
    [AvaloniaFact]
    public async Task MultiMapStandardMergeDefersOtherInputsUntilDpResolves()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51950");
        MainWindowViewModel viewModel = await PresentationTestHost.CreateViewModelAsync(
            TestContext.Current.CancellationToken);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        string dpPath = golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("dp-input"));

        FirmwareSlotViewModel dp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeDp);
        FirmwareSlotViewModel tp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeTp);
        Assert.True(dp.CanSelectFile);
        Assert.False(tp.CanSelectFile);
        Assert.True(tp.IsSemanticStatePendingInput);
        Assert.Equal("Waiting for DP BIN", tp.SelectionReadinessLabel);
        Assert.Contains("DP", tp.SelectionReadinessDetail, StringComparison.Ordinal);

        Task selection = viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            dpPath,
            TestContext.Current.CancellationToken);
        WorkflowInspectionLifecycle lifecycle = viewModel.Merge.InspectionLifecycles[
            ExperienceIds.StandardMerge];
        Task completed = await Task.WhenAny(
            selection,
            Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.True(
            ReferenceEquals(selection, completed),
            $"Lifecycle={lifecycle.State}; Progress={lifecycle.Progress}; " +
            $"DP={dp.SemanticState}/{dp.InputInspectionStatus}; " +
            $"TP={tp.SemanticState}/{tp.SelectionReadinessDetail}");
        await selection;

        dp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeDp);
        tp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeTp);
        Assert.True(
            tp.CanSelectFile,
            $"DP={dp.SemanticState}/{dp.InputInspectionStatus}; " +
            $"TP={tp.SemanticState}/{tp.SelectionReadinessDetail}/{tp.InputInspectionStatus}");
        Assert.False(tp.IsSemanticStatePendingInput);
        Assert.Contains(
            dp.SemanticState,
            new[] { FirmwareSlotSemanticState.Verified, FirmwareSlotSemanticState.Warning });
    }

    /// <summary>Visiting CtrlRAM Replace cannot prevent a later Standard Merge DP inspection from starting.</summary>
    [Fact]
    public async Task StandardMergeDpInspectionStartsAfterVisitingCtrlRamReplace()
    {
        using var standard = StandardMergeGoldenManifest.Load();
        JsonElement standardCase = standard.CaseByIc("51950");
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((icId, inputs) =>
            BuiltInFirmwareInspection.InspectFirmwareBatch(
                (BuiltInFirmwareInspection)TestHost.FirmwareInspectionExperience,
                icId,
                inputs));
        string dpPath = standard.ManifestPath(
            standardCase.GetProperty("inputs").GetProperty("dp-input"));
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        viewModel.ShowMergeCommand.Execute(null);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            dpPath,
            TestContext.Current.CancellationToken);

        WorkflowInspectionLifecycle lifecycle = viewModel.Merge.InspectionLifecycles[
            ExperienceIds.StandardMerge];
        FirmwareSlotViewModel dp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeDp);
        FirmwareSlotViewModel tp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeTp);
        Assert.False(lifecycle.IsRunning);
        Assert.False(dp.IsInputInspectionPending);
        Assert.Contains(
            dp.SemanticState,
            new[] { FirmwareSlotSemanticState.Verified, FirmwareSlotSemanticState.Warning });
        Assert.True(
            tp.CanSelectFile,
            $"Lifecycle={lifecycle.State}; DP={dp.SemanticState}/{dp.InputInspectionStatus}; " +
            $"TP={tp.SemanticState}/{tp.SelectionReadinessDetail}");
    }

    /// <summary>An accepted Standard Merge prerequisite cannot poison a later CtrlRAM context confirmation.</summary>
    [AvaloniaFact]
    public async Task AcceptedStandardMergeDpDoesNotPoisonCtrlRamHomeNavigation()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51950");
        MainWindowViewModel viewModel = await PresentationTestHost.CreateViewModelAsync(
            TestContext.Current.CancellationToken);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        string dpPath = golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("dp-input"));

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            dpPath,
            TestContext.Current.CancellationToken);

        viewModel.ShowHomeCommand.Execute(null);
        Assert.True(viewModel.IsNavigationClearConfirmationOpen);
        viewModel.ConfirmNavigationAndClearCommand.Execute(null);
        Assert.True(viewModel.IsHomeVisible);
        viewModel.BeginCtrlRamReplaceFromHomeCommand.Execute(null);
        Assert.True(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        viewModel.WorkflowSession.WorkflowContextSetup.SelectedIc = "NT51950";
        viewModel.WorkflowSession.WorkflowContextSetup.SelectedNumberChoice =
            viewModel.WorkflowSession.WorkflowContextSetup.NumberChoices.Single(
                static choice => choice.Token == IcNumberSelectionTokens.SingleChip);

        Exception? error = Record.Exception(
            () => viewModel.WorkflowSession.ConfirmWorkflowContextCommand.Execute(null));

        Assert.Null(error);
        Assert.True(viewModel.IsReplaceVisible);
        Assert.True(viewModel.Replace.IsCtrlRamReplaceModeSelected);
    }

    /// <summary>Dependent inputs remain disabled throughout the DP Checking interval.</summary>
    [Fact]
    public async Task MultiMapStandardMergeKeepsDependentsPendingWhileDpIsChecking()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51950");
        var readerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReader = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        PresentationHostServices services = PresentationTestHost.CreateServices("test");
        var viewModel = new MainWindowViewModel(
            "test",
            "test",
            ShellLanguage.English,
            services,
            new DelegatingFirmwareInspection(
                TestHost.FirmwareInspectionExperience,
                batchReader: (icId, inputs) =>
                {
                    _ = readerEntered.TrySetResult();
                    releaseReader.Task.GetAwaiter().GetResult();
                    return BuiltInFirmwareInspection.InspectFirmwareBatch(
                        (BuiltInFirmwareInspection)TestHost.FirmwareInspectionExperience,
                        icId,
                        inputs);
                }));
        _ = PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        FirmwareSlotViewModel dp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeDp);
        FirmwareSlotViewModel tp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeTp);

        Task selection = viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            golden.ManifestPath(goldenCase.GetProperty("inputs").GetProperty("dp-input")),
            TestContext.Current.CancellationToken);
        try
        {
            await readerEntered.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);
            Assert.Equal(FirmwareSlotSemanticState.Checking, dp.SemanticState);
            Assert.True(tp.IsSemanticStatePendingInput);
            Assert.False(tp.CanSelectFile);
            Assert.False(viewModel.Merge.CanBuildMerge);
        }
        finally
        {
            _ = releaseReader.TrySetResult();
        }

        await selection;
        dp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeDp);
        tp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeTp);
        Assert.True(tp.CanSelectFile);
        Assert.Contains(
            dp.SemanticState,
            new[] { FirmwareSlotSemanticState.Verified, FirmwareSlotSemanticState.Warning });
    }

    /// <summary>Changing to a route without LDC clears the obsolete selected source before reinspection.</summary>
    [Fact]
    public async Task StandardMergeRouteChangeRejectsStaleLdcSelection()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement withLdc = golden.CaseByIc("51928");
        JsonElement withoutLdc = golden.CaseByIc("51926");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51928";
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            golden.ManifestPath(withLdc.GetProperty("inputs").GetProperty("dp-input")),
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel ldc = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeLdc);
        Assert.True(
            ldc.CanSelectFile,
            $"LDC={ldc.SemanticState}/{ldc.SelectionReadinessDetail}; " +
            $"DP={viewModel.Merge.MergeDpSlot.SemanticState}/" +
            $"{viewModel.Merge.MergeDpSlot.InputInspectionStatus}");
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeLdc,
            golden.ManifestPath(withLdc.GetProperty("inputs").GetProperty("ldc-input")),
            TestContext.Current.CancellationToken);
        Assert.True(
            ldc.HasFile,
            $"LDC={ldc.SemanticState}/{ldc.SelectionReadinessDetail}/{ldc.InputInspectionStatus}");

        viewModel.WorkflowSession.SelectedIc = "NT51926";
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            golden.ManifestPath(withoutLdc.GetProperty("inputs").GetProperty("dp-input")),
            TestContext.Current.CancellationToken);

        Assert.False(ldc.HasFile);
        Assert.Null(ldc.FilePath);
        Assert.False(ldc.IsInputInspectionPending);
    }

    /// <summary>An unsupported DP capacity becomes terminal blocking UI health rather than stuck Checking.</summary>
    [Fact]
    public async Task MultiMapStandardMergeUnsupportedDpCapacityTerminatesAsError()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-standard-invalid-capacity");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            workspace.Write("unsupported-dp.bin", new byte[0x60000]),
            TestContext.Current.CancellationToken);

        FirmwareSlotViewModel dp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeDp);
        FirmwareSlotViewModel tp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeTp);
        Assert.Equal(FirmwareSlotSemanticState.Error, dp.SemanticState);
        Assert.False(dp.IsInputInspectionPending);
        Assert.True(dp.BlocksBuild);
        Assert.False(tp.CanSelectFile);
        Assert.False(viewModel.Merge.CanBuildMerge);
    }

    /// <summary>Language changes reproject Standard Merge readiness and automation without semantic drift.</summary>
    [Fact]
    public void StandardMergePendingReadinessIsRelocalized()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        FirmwareSlotViewModel tp = viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionSlotIds.MergeTp);
        string englishLabel = tp.SelectionReadinessLabel;
        string englishDetail = tp.SelectionReadinessDetail;
        string englishAutomation = tp.SelectionReadinessAutomationText;
        Assert.Equal("Waiting for DP BIN", englishLabel);

        viewModel.SelectedLanguage = "Traditional Chinese";

        Assert.True(tp.IsSemanticStatePendingInput);
        Assert.Equal("等待 DP BIN", tp.SelectionReadinessLabel);
        Assert.NotEqual(englishLabel, tp.SelectionReadinessLabel);
        Assert.NotEqual(englishDetail, tp.SelectionReadinessDetail);
        Assert.NotEqual(englishAutomation, tp.SelectionReadinessAutomationText);
        Assert.Contains(tp.SelectionReadinessLabel, tp.SelectionReadinessAutomationText, StringComparison.Ordinal);
        Assert.Contains(tp.SelectionReadinessDetail, tp.SelectionReadinessAutomationText, StringComparison.Ordinal);
    }

    /// <summary>A blocking compiled source inspection never leaves Standard Merge Build enabled.</summary>
    [Fact]
    public async Task StandardMergeBlockingInspectionDisablesBuild()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-standard-health");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51926";

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeDp,
            workspace.Write("short-dp.bin", [0x01]),
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.MergeTp,
            workspace.Write("short-tp.bin", [0x02]),
            TestContext.Current.CancellationToken);

        Assert.Contains(viewModel.Merge.MergeSlots, static slot =>
            slot.SemanticState == FirmwareSlotSemanticState.Error && slot.BlocksBuild);
        Assert.False(viewModel.Merge.CanBuildMerge);
    }
}

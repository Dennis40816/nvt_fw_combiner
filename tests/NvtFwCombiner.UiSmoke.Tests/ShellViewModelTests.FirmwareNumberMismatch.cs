using System.Text.Json;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Verified context requires confirmation and reuses the one worker snapshot after switching Number.</summary>
    [Fact]
    public async Task CtrlRamSlotInspectionSwitchesVerifiedNumberAndPreviewsSelectedPlan()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-context-preservation");
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51926"));
        int reads = 0;
        MainWindowViewModel viewModel = CreateInspectionViewModel((icId, path, tpPath, request) =>
        {
            reads++;
            return WorkbenchCompositionService.InspectFirmware(icId, path, tpPath, request);
        });
        viewModel.SelectedIc = "NT51926";
        viewModel.SelectedNumber = WorkbenchIcNumberTokens.SingleChip;
        OpenReplace(viewModel, WorkbenchReplaceModes.CtrlRam);
        FirmwareSlotViewModel replacement = viewModel.ReplaceSlots.First(slot =>
            !ReferenceEquals(slot, viewModel.ReplaceBaseSlot) &&
            slot.Title.Contains("VN CtrlRAM", StringComparison.Ordinal));
        string replacementPath = workspace.Write("vn-before-base.bin", [0x01]);
        await viewModel.SetSlotFileAsync(
            replacement.SlotId,
            replacementPath,
            TestContext.Current.CancellationToken);
        reads = 0;

        await viewModel.SetSlotFileAsync("replace-base", basePath, TestContext.Current.CancellationToken);

        Assert.Equal(1, reads);
        Assert.Equal(WorkbenchIcNumberTokens.SingleChip, viewModel.SelectedNumber);
        Assert.True(viewModel.IsFirmwareNumberMismatchModalOpen);
        Assert.Equal("1 IC", viewModel.FirmwareNumberMismatchCurrentNumber);
        Assert.Equal("Cascade", viewModel.FirmwareNumberMismatchDetectedNumber);

        viewModel.AcceptFirmwareNumberMismatchCommand.Execute(null);

        Assert.Equal(WorkbenchIcNumberTokens.Cascade, viewModel.SelectedNumber);
        Assert.False(viewModel.IsFirmwareNumberMismatchModalOpen);
        Assert.NotEmpty(viewModel.CtrlRamRegions);
        Assert.Contains(
            viewModel.ReplaceSlots,
            slot => slot.SlotId == replacement.SlotId && slot.FilePath == replacementPath);
        Assert.Equal("Context updated", viewModel.ShellToastTitle);

        Assert.True(viewModel.PreviewReplaceCommand.CanExecute(null));
        await viewModel.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.Equal(
            "nt51926-ctrlram-replace-fw141-runtime-cascade",
            viewModel.LoadedReport.ProfileId);
    }

    /// <summary>Cancel keeps the selected Number and files; the backend remains responsible for blocking a mismatch.</summary>
    [Fact]
    public async Task FirmwareNumberMismatchCancelKeepsCurrentContext()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-number-mismatch-cancel");
        string basePath = workspace.Write("base.bin", [0x01]);
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        [
            .. inputs.Select(input => new WorkbenchFirmwareInspectionResult(
                input.InspectionId,
                new WorkbenchFirmwareInspection(
                    "NT51926",
                    null,
                    null,
                    null,
                    new WorkbenchFirmwareContextSuggestion("NT51926", "cascade", 3, "2.1.0", 0x5192),
                    null))),
        ]);
        viewModel.SelectedIc = "NT51926";
        viewModel.SelectedNumber = WorkbenchIcNumberTokens.SingleChip;
        OpenReplace(viewModel, WorkbenchReplaceModes.CtrlRam);

        await viewModel.SetSlotFileAsync("replace-base", basePath, TestContext.Current.CancellationToken);

        Assert.True(viewModel.IsFirmwareNumberMismatchModalOpen);
        Assert.Equal("base.bin", viewModel.FirmwareNumberMismatchFileName);
        Assert.Equal((byte)3, viewModel.FirmwareNumberMismatchDetectedChipCount);

        viewModel.DismissFirmwareNumberMismatchCommand.Execute(null);

        Assert.False(viewModel.IsFirmwareNumberMismatchModalOpen);
        Assert.Equal(WorkbenchIcNumberTokens.SingleChip, viewModel.SelectedNumber);
        Assert.Equal(basePath, viewModel.ReplaceBaseSlot.FilePath);
    }

    /// <summary>A newly selected firmware replaces every visible field and action target of an open Number prompt.</summary>
    [Fact]
    public async Task FirmwareNumberMismatchTracksLatestSelectedFirmware()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-number-mismatch-latest");
        string firstPath = workspace.Write("first.bin", [0x01]);
        string secondPath = workspace.Write("second.bin", [0x02]);
        using var firstInspectionStarted = new ManualResetEventSlim();
        using var releaseFirstInspection = new ManualResetEventSlim();
        int batchCount = 0;
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        {
            if (Interlocked.Increment(ref batchCount) == 1)
            {
                firstInspectionStarted.Set();
                releaseFirstInspection.Wait(TestContext.Current.CancellationToken);
            }

            return
            [
                .. inputs.Select(input =>
                {
                    bool first = string.Equals(input.Path, firstPath, StringComparison.Ordinal);
                    byte chipCount = first ? (byte)2 : (byte)3;
                    return new WorkbenchFirmwareInspectionResult(
                        input.InspectionId,
                        new WorkbenchFirmwareInspection(
                            "NT51927",
                            null,
                            null,
                            null,
                            new WorkbenchFirmwareContextSuggestion(
                                "NT51927",
                                chipCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                chipCount,
                                "1.0.0",
                                0x5192),
                            null));
                }),
            ];
        });
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = WorkbenchIcNumberTokens.SingleChip;
        OpenReplace(viewModel, WorkbenchReplaceModes.CtrlRam);

        Task firstSelection = viewModel.SetSlotFileAsync(
            "replace-base",
            firstPath,
            TestContext.Current.CancellationToken);
        try
        {
            Assert.True(firstInspectionStarted.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            await viewModel.SetSlotFileAsync("merge-tp", secondPath, TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseFirstInspection.Set();
        }

        await firstSelection;

        Assert.True(viewModel.IsFirmwareNumberMismatchModalOpen);
        Assert.Equal("second.bin", viewModel.FirmwareNumberMismatchFileName);
        Assert.Equal("3 IC", viewModel.FirmwareNumberMismatchDetectedNumber);
        Assert.Equal((byte)3, viewModel.FirmwareNumberMismatchDetectedChipCount);
        viewModel.AcceptFirmwareNumberMismatchCommand.Execute(null);
        Assert.Equal("3", viewModel.SelectedNumber);
    }

    /// <summary>Reinspection of the same file and suggestion does not republish an unchanged Number prompt.</summary>
    [Fact]
    public async Task IdenticalFirmwareNumberMismatchPromptIsDeduplicated()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-number-mismatch-duplicate");
        string basePath = workspace.Write("base.bin", [0x01]);
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        [
            .. inputs.Select(input => new WorkbenchFirmwareInspectionResult(
                input.InspectionId,
                new WorkbenchFirmwareInspection(
                    "NT51927",
                    null,
                    null,
                    null,
                    new WorkbenchFirmwareContextSuggestion("NT51927", "2", 2, "1.0.0", 0x5192),
                    null))),
        ]);
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = WorkbenchIcNumberTokens.SingleChip;
        OpenReplace(viewModel, WorkbenchReplaceModes.CtrlRam);
        await viewModel.SetSlotFileAsync("replace-base", basePath, TestContext.Current.CancellationToken);
        Assert.True(viewModel.IsFirmwareNumberMismatchModalOpen);

        var promptPublications = new List<string?>();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(MainWindowViewModel.IsFirmwareNumberMismatchModalOpen) or
                nameof(MainWindowViewModel.FirmwareNumberMismatchFileName) or
                nameof(MainWindowViewModel.FirmwareNumberMismatchCurrentNumber) or
                nameof(MainWindowViewModel.FirmwareNumberMismatchDetectedNumber) or
                nameof(MainWindowViewModel.FirmwareNumberMismatchDetectedChipCount))
            {
                promptPublications.Add(args.PropertyName);
            }
        };

        await viewModel.RefreshAllSelectedFirmwareInspectionsAsync("replace-base");

        Assert.Empty(promptPublications);
        Assert.True(viewModel.IsFirmwareNumberMismatchModalOpen);
        Assert.Equal("base.bin", viewModel.FirmwareNumberMismatchFileName);
        Assert.Equal("2 IC", viewModel.FirmwareNumberMismatchDetectedNumber);
        Assert.Equal((byte)2, viewModel.FirmwareNumberMismatchDetectedChipCount);
    }

    /// <summary>Cancel preserves the UI choice but Build still reaches the backend mismatch gate and publishes no output.</summary>
    [Fact]
    public async Task FirmwareNumberMismatchCancelThenBuildRemainsFailClosed()
    {
        using var fixtures = CtrlRamReplaceFixtureManifest.LoadIfPresent();
        Assert.NotNull(fixtures);
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace",
            "nt51927-3chip-self-20260705");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-number-mismatch-build");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51927";
        viewModel.SelectedNumber = WorkbenchIcNumberTokens.SingleChip;
        OpenReplace(viewModel, WorkbenchReplaceModes.CtrlRam);

        fixtures.SetBaseSlot(viewModel, fixtureCase);
        await viewModel.FirmwareInspectionRefreshTask;
        Assert.True(viewModel.IsFirmwareNumberMismatchModalOpen);
        Assert.Equal("3 IC", viewModel.FirmwareNumberMismatchDetectedNumber);
        string basePath = Assert.IsType<string>(viewModel.ReplaceBaseSlot.FilePath);
        byte[] immutableBase = File.ReadAllBytes(basePath);
        FirmwareSlotViewModel nf = viewModel.ReplaceSlots.Single(slot => slot.SlotId == "replace-ctrlram-nf");
        viewModel.SetSlotFile(nf.SlotId, fixtures.ReplacementPathFor(fixtureCase, nf.SlotId));

        viewModel.DismissFirmwareNumberMismatchCommand.Execute(null);
        string outputPath = workspace.PathFor("must-not-exist.bin");
        await viewModel.BuildReplaceAsync(outputPath);

        Assert.False(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.Equal(WorkbenchIcNumberTokens.SingleChip, viewModel.SelectedNumber);
        Assert.False(File.Exists(outputPath));
        Assert.Equal(immutableBase, File.ReadAllBytes(basePath));
        Assert.True(viewModel.IsReportModalOpen);
        using var report = JsonDocument.Parse(viewModel.LoadedReportJson);
        Assert.Contains(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() == WorkbenchIssueCodes.ReplaceCtrlRamIcNumberMismatch);
        Assert.False(report.RootElement.GetProperty("Output").GetProperty("Committed").GetBoolean());
    }
}

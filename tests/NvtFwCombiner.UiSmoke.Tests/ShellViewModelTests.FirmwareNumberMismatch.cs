using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class CtrlRamWorkflowTests
{
    /// <summary>Verified context requires confirmation and reuses the one worker snapshot after switching Number.</summary>
    [Fact]
    public async Task CtrlRamSlotInspectionSwitchesVerifiedNumberAndPreviewsSelectedPlan()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-context-preservation");
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51926"));
        int reads = 0;
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((icId, inputs) =>
        {
            reads += inputs.Count;
            return BuiltInFirmwareInspection.InspectFirmwareBatch(
                (BuiltInFirmwareInspection)TestHost.FirmwareInspectionExperience,
                icId,
                inputs);
        });
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
        FirmwareSlotViewModel replacement = viewModel.Replace.ReplaceSlots.First(slot =>
            !ReferenceEquals(slot, viewModel.Replace.ReplaceBaseSlot) &&
            slot.Title.Contains("VN CtrlRAM", StringComparison.Ordinal));
        string replacementPath = workspace.Write("vn-before-base.bin", [0x01]);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            replacement.SlotId,
            replacementPath,
            TestContext.Current.CancellationToken);
        reads = 0;

        await viewModel.WorkflowSession.SetSlotFileAsync("replace-base", basePath, TestContext.Current.CancellationToken);

        Assert.Equal(2, reads);
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);
        Assert.True(viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen);
        Assert.Equal("1 IC", viewModel.WorkflowSession.FirmwareNumberMismatchCurrentNumber);
        Assert.Equal("Cascade", viewModel.WorkflowSession.FirmwareNumberMismatchDetectedNumber);

        viewModel.WorkflowSession.AcceptFirmwareNumberMismatchCommand.Execute(null);
        await CurrentInspection(viewModel).ActiveTask;

        Assert.Equal(IcNumberSelectionTokens.Cascade, viewModel.WorkflowSession.SelectedNumber);
        Assert.False(viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen);
        Assert.NotEmpty(viewModel.Replace.CtrlRamRegions);
        Assert.Contains(
            viewModel.Replace.ReplaceSlots,
            slot => slot.SlotId == replacement.SlotId && slot.FilePath == replacementPath);
        Assert.Equal("Context updated", viewModel.Reports.ShellToastTitle);

        Assert.True(
            viewModel.Replace.PreviewReplaceCommand.CanExecute(null),
            $"{viewModel.Replace.ReplaceReadinessStatus}; " +
            string.Join("; ", viewModel.Replace.ReplaceSlots.Append(viewModel.Replace.ReplaceBaseSlot)
                .Where(static slot => slot.HasFile)
                .Select(static slot => $"{slot.SlotId}={slot.InputInspectionSeverity}/{slot.InputInspectionStatus}")));
        await viewModel.Replace.PreviewReplaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.Equal(
            "nt51926-ctrlram-replace-fw141-runtime-cascade",
            viewModel.Reports.LoadedReport.ProfileId);
    }

    /// <summary>Cancel keeps the selected Number and files; the backend remains responsible for blocking a mismatch.</summary>
    [Fact]
    public async Task FirmwareNumberMismatchCancelKeepsCurrentContext()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-number-mismatch-cancel");
        string basePath = workspace.Write("base.bin", [0x01]);
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        [
            .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                input.InspectionId,
                new FirmwareInspectionSnapshot(
                    "NT51926",
                    null,
                    null,
                    null,
                    new FirmwareContextSuggestion("NT51926", "cascade", 3, "2.1.0", 0x5192),
                    null))),
        ]);
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;

        await viewModel.WorkflowSession.SetSlotFileAsync("replace-base", basePath, TestContext.Current.CancellationToken);

        Assert.True(viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen);
        Assert.Equal("base.bin", viewModel.WorkflowSession.FirmwareNumberMismatchFileName);
        Assert.Equal((byte)3, viewModel.WorkflowSession.FirmwareNumberMismatchDetectedChipCount);

        viewModel.WorkflowSession.DismissFirmwareNumberMismatchCommand.Execute(null);

        Assert.False(viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen);
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);
        Assert.Equal(basePath, viewModel.Replace.ReplaceBaseSlot.FilePath);
    }

    /// <summary>AB input metadata may describe its own FWConfig count but cannot alter hidden Number context.</summary>
    [Fact]
    public async Task AbMergeTpInspectionDoesNotPromptForFirmwareNumberContext()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-number-context");
        string tpPath = workspace.Write("tp-a.bin", [0x01]);
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        [
            .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                input.InspectionId,
                new FirmwareInspectionSnapshot(
                    null,
                    null,
                    null,
                    null,
                    new FirmwareContextSuggestion(
                        "NT51929",
                        IcNumberSelectionTokens.CascadeTwoToEight,
                        2,
                        "1.4.1",
                        0x5192),
                    null))),
        ]);
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput,
            tpPath,
            TestContext.Current.CancellationToken);

        Assert.False(viewModel.WorkflowSession.IsNumberSelectorVisible);
        Assert.False(viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen);
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);
    }

    /// <summary>Standard Merge records TP FWConfig topology as metadata without offering a hidden Number change.</summary>
    [Fact]
    public async Task StandardMergeTpInspectionDoesNotPromptForFirmwareNumberContext()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-standard-number-context");
        string tpPath = workspace.Write("tp.bin", [0x01]);
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        [
            .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                input.InspectionId,
                new FirmwareInspectionSnapshot(
                    null,
                    null,
                    null,
                    null,
                    new FirmwareContextSuggestion(
                        "NT51929",
                        IcNumberSelectionTokens.CascadeTwoToEight,
                        2,
                        "1.4.1",
                        0x5192),
                    null))),
        ]);
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;

        await viewModel.WorkflowSession.SetSlotFileAsync("merge-tp", tpPath, TestContext.Current.CancellationToken);

        Assert.True(viewModel.Merge.IsNormalMergeModeSelected);
        Assert.False(viewModel.WorkflowSession.IsNumberSelectorVisible);
        Assert.False(viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen);
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);
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
                    return new FirmwareInspectionSnapshotResult(
                        input.InspectionId,
                        new FirmwareInspectionSnapshot(
                            "NT51927",
                            null,
                            null,
                            null,
                            new FirmwareContextSuggestion(
                                "NT51927",
                                chipCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                chipCount,
                                "1.0.0",
                                0x5192),
                            null));
                }),
            ];
        });
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;

        Task firstSelection = viewModel.WorkflowSession.SetSlotFileAsync(
            "replace-base",
            firstPath,
            TestContext.Current.CancellationToken);
        try
        {
            Assert.True(firstInspectionStarted.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Task secondSelection = viewModel.WorkflowSession.SetSlotFileAsync(
                "replace-base",
                secondPath,
                TestContext.Current.CancellationToken);
            Assert.Equal(1, batchCount);
            releaseFirstInspection.Set();
            await Task.WhenAll(firstSelection, secondSelection);
        }
        finally
        {
            releaseFirstInspection.Set();
        }

        Assert.True(viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen);
        Assert.Equal("second.bin", viewModel.WorkflowSession.FirmwareNumberMismatchFileName);
        Assert.Equal("3 IC", viewModel.WorkflowSession.FirmwareNumberMismatchDetectedNumber);
        Assert.Equal((byte)3, viewModel.WorkflowSession.FirmwareNumberMismatchDetectedChipCount);
        viewModel.WorkflowSession.AcceptFirmwareNumberMismatchCommand.Execute(null);
        Assert.Equal("3", viewModel.WorkflowSession.SelectedNumber);
    }

    /// <summary>Reinspection of the same file and suggestion does not republish an unchanged Number prompt.</summary>
    [Fact]
    public async Task IdenticalFirmwareNumberMismatchPromptIsDeduplicated()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-number-mismatch-duplicate");
        string basePath = workspace.Write("base.bin", [0x01]);
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        [
            .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                input.InspectionId,
                new FirmwareInspectionSnapshot(
                    "NT51927",
                    null,
                    null,
                    null,
                    new FirmwareContextSuggestion("NT51927", "2", 2, "1.0.0", 0x5192),
                    null))),
        ]);
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
        await viewModel.WorkflowSession.SetSlotFileAsync("replace-base", basePath, TestContext.Current.CancellationToken);
        Assert.True(viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen);

        var promptPublications = new List<string?>();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(WorkflowSessionPresentationViewModel.IsFirmwareNumberMismatchModalOpen) or
                nameof(WorkflowSessionPresentationViewModel.FirmwareNumberMismatchFileName) or
                nameof(WorkflowSessionPresentationViewModel.FirmwareNumberMismatchCurrentNumber) or
                nameof(WorkflowSessionPresentationViewModel.FirmwareNumberMismatchDetectedNumber) or
                nameof(WorkflowSessionPresentationViewModel.FirmwareNumberMismatchDetectedChipCount))
            {
                promptPublications.Add(args.PropertyName);
            }
        };

        await viewModel.WorkflowSession.RefreshSelectedReplaceFirmwareInspectionsAsync("replace-base");

        Assert.Empty(promptPublications);
        Assert.True(viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen);
        Assert.Equal("base.bin", viewModel.WorkflowSession.FirmwareNumberMismatchFileName);
        Assert.Equal("2 IC", viewModel.WorkflowSession.FirmwareNumberMismatchDetectedNumber);
        Assert.Equal((byte)2, viewModel.WorkflowSession.FirmwareNumberMismatchDetectedChipCount);
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
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51927";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        fixtures.SetBaseSlot(viewModel, fixtureCase);
        await CurrentInspection(viewModel).ActiveTask;
        Assert.True(viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen);
        Assert.Equal("3 IC", viewModel.WorkflowSession.FirmwareNumberMismatchDetectedNumber);
        string basePath = Assert.IsType<string>(viewModel.Replace.ReplaceBaseSlot.FilePath);
        byte[] immutableBase = File.ReadAllBytes(basePath);
        FirmwareSlotViewModel nf = viewModel.Replace.ReplaceSlots.Single(slot => slot.SlotId == "replace-ctrlram-nf");
        viewModel.SetSlotFile(nf.SlotId, fixtures.ReplacementPathFor(fixtureCase, nf.SlotId));

        viewModel.WorkflowSession.DismissFirmwareNumberMismatchCommand.Execute(null);
        string outputPath = workspace.PathFor("must-not-exist.bin");
        await viewModel.Replace.BuildReplaceAsync(outputPath);

        Assert.False(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);
        Assert.False(File.Exists(outputPath));
        Assert.Equal(immutableBase, File.ReadAllBytes(basePath));
        Assert.True(viewModel.Reports.IsReportModalOpen);
        using var report = JsonDocument.Parse(viewModel.Reports.LoadedReportJson);
        Assert.Contains(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() == CompositionPlanningIssueCodes.UiRunFailed &&
                issue.GetProperty("Message").GetString()!.Contains(
                    AuthoringSessionIssueCodes.StaleInspection,
                    StringComparison.Ordinal));
        Assert.False(report.RootElement.GetProperty("Output").GetProperty("Committed").GetBoolean());
    }
}

using System.Text.Json;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>Only the latest worker inspection may publish facts for a rapidly replaced slot path.</summary>
    [Fact]
    public async Task SlotInspectionRunsOffCallerAndLatestGenerationWins()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-slot-inspection-generation");
        string firstPath = workspace.Write("first.bin", [0x01]);
        string secondPath = workspace.Write("second.bin", [0x02]);
        using var firstStarted = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        int callerThread = Environment.CurrentManagedThreadId;
        int firstReaderThread = callerThread;

        WorkbenchFirmwareInspection ReadInspection(
            string _,
            string path,
            string? __,
            WorkbenchCtrlRamInspectionRequest? ___)
        {
            if (string.Equals(path, firstPath, StringComparison.Ordinal))
            {
                firstReaderThread = Environment.CurrentManagedThreadId;
                firstStarted.Set();
                releaseFirst.Wait(TestContext.Current.CancellationToken);
                return DpInspection("0101");
            }

            return DpInspection("0202");
        }

        MainWindowViewModel viewModel = CreateInspectionViewModel(ReadInspection);
        Task first = viewModel.SetSlotFileAsync(
            "merge-dp",
            firstPath,
            TestContext.Current.CancellationToken);
        Assert.True(firstStarted.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.True(viewModel.IsFirmwareInspectionLoading);

        await viewModel.SetSlotFileAsync(
            "merge-dp",
            secondPath,
            TestContext.Current.CancellationToken);
        Assert.Equal(secondPath, viewModel.MergeSlots.Single(slot => slot.SlotId == "merge-dp").FilePath);
        Assert.Contains(
            viewModel.MergeSlots.Single(slot => slot.SlotId == "merge-dp").FirmwareFacts,
            fact => fact.Label == "DP" && fact.Value == "D02-02");
        Assert.False(viewModel.IsFirmwareInspectionLoading);

        releaseFirst.Set();
        await first;
        Assert.NotEqual(callerThread, firstReaderThread);
        Assert.Equal(secondPath, viewModel.MergeSlots.Single(slot => slot.SlotId == "merge-dp").FilePath);
        Assert.DoesNotContain(
            viewModel.MergeSlots.Single(slot => slot.SlotId == "merge-dp").FirmwareFacts,
            fact => fact.Value == "D01-01");
    }

    /// <summary>A selected file that changes during inspection cannot publish a mixed-identity snapshot.</summary>
    [Fact]
    public async Task SlotInspectionRejectsFileIdentityChanges()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-slot-inspection-identity");
        string path = workspace.Write("changing.bin", [0x01]);
        MainWindowViewModel viewModel = CreateInspectionViewModel((_, inspectedPath, _, _) =>
        {
            using var stream = new FileStream(inspectedPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            stream.WriteByte(0x02);
            return DpInspection("0303");
        });

        await viewModel.SetSlotFileAsync("merge-dp", path, TestContext.Current.CancellationToken);

        FirmwareSlotViewModel slot = viewModel.MergeSlots.Single(candidate => candidate.SlotId == "merge-dp");
        Assert.Empty(slot.FirmwareFacts);
        Assert.False(viewModel.IsFirmwareInspectionLoading);
    }

    /// <summary>Verified context and CtrlRAM display reuse the one worker snapshot without rereading the base.</summary>
    [Fact]
    public async Task CtrlRamSlotInspectionReprojectsVerifiedNumberFromOneRead()
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
        Assert.Equal(WorkbenchIcNumberTokens.Cascade, viewModel.SelectedNumber);
        Assert.NotEmpty(viewModel.CtrlRamRegions);
        Assert.Contains(
            viewModel.ReplaceSlots,
            slot => slot.SlotId == replacement.SlotId && slot.FilePath == replacementPath);
        Assert.Equal("Context updated", viewModel.ShellToastTitle);
    }

    /// <summary>UI projections keep one selected snapshot until explicit reselection; Build remains authoritative.</summary>
    [Fact]
    public async Task CtrlRamCachedDisplayKeepsSelectedSnapshotUntilReselection()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-context-external-mutation");
        string basePath = workspace.Write("base.bin", [0x01]);
        int reads = 0;
        MainWindowViewModel viewModel = CreateInspectionViewModel((_, _, _, _) =>
        {
            reads++;
            return DpInspection(reads == 1 ? "0101" : "0202");
        });
        viewModel.SelectedIc = "NT51926";
        viewModel.SelectedNumber = WorkbenchIcNumberTokens.Cascade;
        OpenReplace(viewModel, WorkbenchReplaceModes.CtrlRam);
        await viewModel.SetSlotFileAsync(
            "replace-base",
            basePath,
            TestContext.Current.CancellationToken);
        Assert.Equal(1, reads);
        Assert.Contains(viewModel.ReplaceBaseSlot.FirmwareFacts, fact => fact.Value == "D01-01");

        using (var stream = new FileStream(basePath, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            stream.WriteByte(0x00);
        }
        viewModel.SelectedNumber = WorkbenchIcNumberTokens.SingleChip;
        await viewModel.FirmwareInspectionRefreshTask;

        Assert.Equal(1, reads);
        Assert.Contains(viewModel.ReplaceBaseSlot.FirmwareFacts, fact => fact.Value == "D01-01");

        await viewModel.SetSlotFileAsync(
            "replace-base",
            basePath,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, reads);
        Assert.Contains(viewModel.ReplaceBaseSlot.FirmwareFacts, fact => fact.Value == "D02-02");
    }

    /// <summary>DP memory length uses the selected snapshot until reselection refreshes its worker-owned identity.</summary>
    [Fact]
    public async Task MergeDpLengthProjectionKeepsSelectedSnapshotUntilReselection()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-dp-length-snapshot");
        string dpPath = workspace.Write("dp.bin", new byte[0x40000]);
        MainWindowViewModel viewModel = CreateInspectionViewModel((_, _, _, _) => DpInspection("0101"));
        viewModel.SelectedIc = "NT51950";
        await viewModel.SetSlotFileAsync("merge-dp", dpPath, TestContext.Current.CancellationToken);
        Assert.Equal("0x00000-0x3FFFF (len 0x40000)", viewModel.MergeMemoryRangeLabel);

        using (var stream = new FileStream(dpPath, FileMode.Open, FileAccess.Write, FileShare.Read))
        {
            stream.SetLength(0x80000);
        }
        viewModel.SelectedMergeMode = WorkbenchMergeModes.General;
        viewModel.SelectedMergeMode = WorkbenchMergeModes.Standard;

        Assert.Equal("0x00000-0x3FFFF (len 0x40000)", viewModel.MergeMemoryRangeLabel);

        await viewModel.SetSlotFileAsync("merge-dp", dpPath, TestContext.Current.CancellationToken);

        Assert.Equal("0x00000-0x7FFFF (len 0x80000)", viewModel.MergeMemoryRangeLabel);
    }

    /// <summary>Merge-only refreshes cannot overwrite the typed CtrlRAM Replace projection.</summary>
    [Fact]
    public async Task MergeRefreshPreservesInspectedCtrlRamReplaceDisplay()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-cross-workflow-memory");
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51926"));
        string mergeDpPath = workspace.Write("merge-dp.bin", [0x01]);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51926";
        viewModel.SelectedNumber = WorkbenchIcNumberTokens.Cascade;
        OpenReplace(viewModel, WorkbenchReplaceModes.CtrlRam);
        await viewModel.SetSlotFileAsync("replace-base", basePath, TestContext.Current.CancellationToken);
        string replaceRange = viewModel.ReplaceMemoryRangeLabel;
        string[] replaceRows = [.. viewModel.ReplaceMemoryRows.Select(static row => row.RangeLabel)];
        string[] replaceCoverage = [.. viewModel.ReplaceCoverageSegments.Select(static segment => segment.RangeLabel)];

        await viewModel.SetSlotFileAsync("merge-dp", mergeDpPath, TestContext.Current.CancellationToken);
        viewModel.SelectedMergeMode = WorkbenchMergeModes.General;
        viewModel.GeneralMergeOutputLength = "0x80000";

        Assert.Equal(replaceRange, viewModel.ReplaceMemoryRangeLabel);
        Assert.Equal(replaceRows, viewModel.ReplaceMemoryRows.Select(static row => row.RangeLabel));
        Assert.Equal(replaceCoverage, viewModel.ReplaceCoverageSegments.Select(static segment => segment.RangeLabel));
    }

    /// <summary>A TP-derived Number update cannot replace profile-selected CtrlRAM slots with generic slots.</summary>
    [Fact]
    public async Task MergeTpContextKeepsProfileSelectedCtrlRamSlots()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-tp-context-ctrlram-slots");
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51926"));
        string tpPath = workspace.Write("tp.bin", [0x01]);
        MainWindowViewModel viewModel = CreateInspectionViewModel((icId, path, dependentTpPath, request) =>
            string.Equals(path, basePath, StringComparison.Ordinal)
                ? WorkbenchCompositionService.InspectFirmware(icId, path, dependentTpPath, request)
                : new WorkbenchFirmwareInspection(
                    null,
                    null,
                    null,
                    null,
                    new WorkbenchFirmwareContextSuggestion(
                        "NT51926",
                        WorkbenchIcNumberTokens.SingleChip,
                        1,
                        "1.4.1",
                        0x5101),
                    null));
        viewModel.SelectedIc = "NT51926";
        viewModel.SelectedNumber = WorkbenchIcNumberTokens.Cascade;
        OpenReplace(viewModel, WorkbenchReplaceModes.CtrlRam);
        await viewModel.SetSlotFileAsync("replace-base", basePath, TestContext.Current.CancellationToken);
        Assert.Contains(
            viewModel.ReplaceSlots,
            slot => slot.Description.Contains("max 5728 B", StringComparison.Ordinal));

        await viewModel.SetSlotFileAsync("merge-tp", tpPath, TestContext.Current.CancellationToken);

        Assert.Equal(WorkbenchIcNumberTokens.SingleChip, viewModel.SelectedNumber);
        Assert.Contains(
            viewModel.ReplaceSlots,
            slot => slot.Description.Contains("max 5728 B", StringComparison.Ordinal));
        Assert.DoesNotContain(
            viewModel.ReplaceSlots,
            slot => slot.Description.Contains("max 5278 B", StringComparison.Ordinal));
    }

    /// <summary>DP facts use the selected TP dependency regardless of file-selection order.</summary>
    [Fact]
    public async Task MergeDpInspectionKeepsTpDependencyAcrossSelectionOrder()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement fixture = golden.CaseByIc("51950");
        string dpPath = golden.ManifestPath(fixture.GetProperty("inputs").GetProperty("dp-input"));
        string tpPath = golden.ManifestPath(fixture.GetProperty("inputs").GetProperty("tp-input"));

        MainWindowViewModel tpFirst = ShellViewModelFactory.Create();
        tpFirst.SelectedIc = "NT51950";
        await tpFirst.SetSlotFileAsync("merge-tp", tpPath, TestContext.Current.CancellationToken);
        await tpFirst.SetSlotFileAsync("merge-dp", dpPath, TestContext.Current.CancellationToken);

        MainWindowViewModel dpFirst = ShellViewModelFactory.Create();
        dpFirst.SelectedIc = "NT51950";
        await dpFirst.SetSlotFileAsync("merge-dp", dpPath, TestContext.Current.CancellationToken);
        await dpFirst.SetSlotFileAsync("merge-tp", tpPath, TestContext.Current.CancellationToken);

        string[] tpFirstFacts =
        [
            .. tpFirst.MergeSlots.Single(slot => slot.SlotId == "merge-dp").FirmwareFacts
                .Select(static fact => $"{fact.Label}:{fact.Value}"),
        ];
        string[] dpFirstFacts =
        [
            .. dpFirst.MergeSlots.Single(slot => slot.SlotId == "merge-dp").FirmwareFacts
                .Select(static fact => $"{fact.Label}:{fact.Value}"),
        ];
        Assert.Equal(tpFirstFacts, dpFirstFacts);
        Assert.Contains("DP:DCC-00", tpFirstFacts);
        Assert.Contains("Jira:AUTO_PRJ-576", tpFirstFacts);
    }

    /// <summary>TP selection requests paired TP/DP projections in one batch and DP cannot alter Number.</summary>
    [Fact]
    public async Task MergeTpSelectionUsesOneBatchAndDpCannotApplyVerifiedContext()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-paired-inspection");
        string dpPath = workspace.Write("dp.bin", [0x01]);
        string tpPath = workspace.Write("tp.bin", [0x02]);
        var batches = new List<WorkbenchFirmwareInspectionInput[]>();
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        {
            batches.Add([.. inputs]);
            return
            [
                .. inputs.Select(input => new WorkbenchFirmwareInspectionResult(
                    input.InspectionId,
                    new WorkbenchFirmwareInspection(
                        null,
                        null,
                        input.InspectionId == "merge-dp" ? new WorkbenchDpVersionMetadata("0102") : null,
                        null,
                        input.InspectionId == "merge-dp"
                            ? new WorkbenchFirmwareContextSuggestion("NT51926", "cascade", 2, "1.4.1", 0x5102)
                            : null,
                        null))),
            ];
        });
        viewModel.SelectedIc = "NT51926";
        viewModel.SelectedNumber = WorkbenchIcNumberTokens.SingleChip;

        await viewModel.SetSlotFileAsync("merge-dp", dpPath, TestContext.Current.CancellationToken);
        Assert.Equal(WorkbenchIcNumberTokens.SingleChip, viewModel.SelectedNumber);
        await viewModel.SetSlotFileAsync("merge-tp", tpPath, TestContext.Current.CancellationToken);

        WorkbenchFirmwareInspectionInput[] pairedBatch = Assert.Single(
            batches,
            static batch => batch.Length == 2);
        Assert.Equal(["merge-tp", "merge-dp"], pairedBatch.Select(static input => input.InspectionId));
        Assert.Equal(tpPath, pairedBatch.Single(input => input.InspectionId == "merge-dp").TpPath);
        Assert.Equal(WorkbenchIcNumberTokens.SingleChip, viewModel.SelectedNumber);
    }

    /// <summary>Accepting an IC hint reinspects the retained Base and applies its verified Number off dispatcher.</summary>
    [Fact]
    public async Task AcceptedIcMismatchContinuesVerifiedBaseContext()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-accepted-ic-context");
        string basePath = workspace.Write("NT51927_base.bin", [0x01]);
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        [
            .. inputs.Select(input => new WorkbenchFirmwareInspectionResult(
                input.InspectionId,
                new WorkbenchFirmwareInspection(
                    "NT51927",
                    null,
                    null,
                    null,
                    new WorkbenchFirmwareContextSuggestion("NT51927", "cascade", 2, "1.4.1", 0x5102),
                    null))),
        ]);
        viewModel.SelectedIc = "NT51926";
        viewModel.SelectedNumber = WorkbenchIcNumberTokens.SingleChip;

        await viewModel.SetSlotFileAsync("replace-base", basePath, TestContext.Current.CancellationToken);
        Assert.True(viewModel.IsFirmwareIcMismatchModalOpen);
        Assert.Equal(WorkbenchIcNumberTokens.SingleChip, viewModel.SelectedNumber);

        viewModel.AcceptFirmwareIcMismatchCommand.Execute(null);
        await viewModel.FirmwareInspectionRefreshTask;

        Assert.Equal("NT51927", viewModel.SelectedIc);
        Assert.Equal(WorkbenchIcNumberTokens.Cascade, viewModel.SelectedNumber);
        Assert.False(viewModel.IsFirmwareIcMismatchModalOpen);
        Assert.Equal("Context updated", viewModel.ShellToastTitle);
    }

    /// <summary>Accepting a replacement hint retains a compatible slot and reinspects it in the new IC context.</summary>
    [Fact]
    public async Task AcceptedIcMismatchRetainsCompatibleCtrlRamReplacement()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-accepted-replacement-ic");
        string replacementPath = workspace.Write("NT51927_replacement.bin", [0x01]);
        var batches = new List<(string IcId, WorkbenchFirmwareInspectionInput[] Inputs)>();
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((icId, inputs) =>
        {
            batches.Add((icId, [.. inputs]));
            return
            [
                .. inputs.Select(input => new WorkbenchFirmwareInspectionResult(
                    input.InspectionId,
                    new WorkbenchFirmwareInspection("NT51927", null, null, null, null, null))),
            ];
        });
        viewModel.SelectedIc = "NT51926";
        viewModel.SelectedNumber = WorkbenchIcNumberTokens.Cascade;
        OpenReplace(viewModel, WorkbenchReplaceModes.CtrlRam);

        MainWindowViewModel targetContext = ShellViewModelFactory.Create();
        targetContext.SelectedIc = "NT51927";
        targetContext.SelectedNumber = WorkbenchIcNumberTokens.Cascade;
        OpenReplace(targetContext, WorkbenchReplaceModes.CtrlRam);
        HashSet<string> targetSlotIds =
        [
            .. targetContext.ReplaceSlots
                .Where(slot => !ReferenceEquals(slot, targetContext.ReplaceBaseSlot))
                .Select(static slot => slot.SlotId),
        ];
        FirmwareSlotViewModel replacement = viewModel.ReplaceSlots.First(slot =>
            !ReferenceEquals(slot, viewModel.ReplaceBaseSlot) && targetSlotIds.Contains(slot.SlotId));

        await viewModel.SetSlotFileAsync(
            replacement.SlotId,
            replacementPath,
            TestContext.Current.CancellationToken);
        Assert.True(viewModel.IsFirmwareIcMismatchModalOpen);

        viewModel.AcceptFirmwareIcMismatchCommand.Execute(null);
        await viewModel.FirmwareInspectionRefreshTask;

        Assert.Equal("NT51927", viewModel.SelectedIc);
        Assert.Equal(
            replacementPath,
            viewModel.ReplaceSlots.Single(slot => slot.SlotId == replacement.SlotId).FilePath);
        (string successorIc, WorkbenchFirmwareInspectionInput[] successorInputs) = batches[^1];
        Assert.Equal("NT51927", successorIc);
        Assert.Contains(successorInputs, input => input.InspectionId == replacement.SlotId);
        Assert.False(viewModel.IsFirmwareIcMismatchModalOpen);
    }

    /// <summary>An accepted replacement cannot cross into an IC that lacks the same safe slot silently.</summary>
    [Fact]
    public async Task AcceptedIcMismatchExplainsUnavailableReplacementSlot()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-accepted-missing-replacement");
        string replacementPath = workspace.Write("NT51931_replacement.bin", [0x01]);
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        [
            .. inputs.Select(input => new WorkbenchFirmwareInspectionResult(
                input.InspectionId,
                new WorkbenchFirmwareInspection("NT51931", null, null, null, null, null))),
        ]);
        viewModel.SelectedIc = "NT51926";
        viewModel.SelectedNumber = WorkbenchIcNumberTokens.Cascade;
        OpenReplace(viewModel, WorkbenchReplaceModes.CtrlRam);
        FirmwareSlotViewModel replacement = viewModel.ReplaceSlots.First(slot =>
            !ReferenceEquals(slot, viewModel.ReplaceBaseSlot));

        await viewModel.SetSlotFileAsync(
            replacement.SlotId,
            replacementPath,
            TestContext.Current.CancellationToken);
        Assert.True(viewModel.IsFirmwareIcMismatchModalOpen);

        viewModel.AcceptFirmwareIcMismatchCommand.Execute(null);
        await viewModel.FirmwareInspectionRefreshTask;

        Assert.Equal("NT51931", viewModel.SelectedIc);
        Assert.DoesNotContain(
            viewModel.ReplaceSlots,
            slot => string.Equals(slot.FilePath, replacementPath, StringComparison.Ordinal));
        Assert.Contains("NT51931_replacement.bin", viewModel.ReportToastText, StringComparison.Ordinal);
        Assert.Contains("same safe input slot", viewModel.ReportToastText, StringComparison.Ordinal);
    }

    /// <summary>A normal IC refresh never rereads populated CtrlRAM replacement slots whose projections are unused.</summary>
    [Fact]
    public async Task IcChangeSkipsUnusedCtrlRamReplacementInspection()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ic-unused-replacement");
        string replacementPath = workspace.Write("replacement.bin", [0x01]);
        var inspectedIds = new List<string>();
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        {
            inspectedIds.AddRange(inputs.Select(static input => input.InspectionId));
            return
            [
                .. inputs.Select(input => new WorkbenchFirmwareInspectionResult(
                    input.InspectionId,
                    new WorkbenchFirmwareInspection(null, null, null, null, null, null))),
            ];
        });
        viewModel.SelectedIc = "NT51926";
        viewModel.SelectedNumber = WorkbenchIcNumberTokens.Cascade;
        OpenReplace(viewModel, WorkbenchReplaceModes.CtrlRam);
        FirmwareSlotViewModel replacement = viewModel.ReplaceSlots.First(slot =>
            !ReferenceEquals(slot, viewModel.ReplaceBaseSlot));
        await viewModel.SetSlotFileAsync(
            replacement.SlotId,
            replacementPath,
            TestContext.Current.CancellationToken);
        inspectedIds.Clear();

        viewModel.SelectedIc = "NT51927";
        await viewModel.FirmwareInspectionRefreshTask;

        Assert.DoesNotContain(replacement.SlotId, inspectedIds);
    }

    /// <summary>A replacement selected during Base inspection starts a successor that retains both inputs.</summary>
    [Fact]
    public async Task CtrlRamReplacementSelectionPreservesPendingBaseInspection()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-base-replacement-race");
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51926"));
        using var firstBaseStarted = new ManualResetEventSlim();
        using var releaseFirstBase = new ManualResetEventSlim();
        int batches = 0;
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((icId, inputs) =>
        {
            if (Interlocked.Increment(ref batches) == 1)
            {
                firstBaseStarted.Set();
                releaseFirstBase.Wait(TestContext.Current.CancellationToken);
            }

            return WorkbenchCompositionService.InspectFirmwareBatch(icId, inputs);
        });
        viewModel.SelectedIc = "NT51926";
        viewModel.SelectedNumber = WorkbenchIcNumberTokens.Cascade;
        OpenReplace(viewModel, WorkbenchReplaceModes.CtrlRam);
        FirmwareSlotViewModel replacement = viewModel.ReplaceSlots.First(slot =>
            !ReferenceEquals(slot, viewModel.ReplaceBaseSlot) &&
            slot.Title.Contains("VN CtrlRAM", StringComparison.Ordinal));
        string replacementPath = workspace.Write("vn.bin", [0x01]);

        Task baseSelection = viewModel.SetSlotFileAsync(
            "replace-base",
            basePath,
            TestContext.Current.CancellationToken);
        Assert.True(firstBaseStarted.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Contains("Inspecting", viewModel.ReplaceReadinessStatus, StringComparison.Ordinal);

        await viewModel.SetSlotFileAsync(
            replacement.SlotId,
            replacementPath,
            TestContext.Current.CancellationToken);

        Assert.False(viewModel.IsFirmwareInspectionLoading);
        Assert.NotEmpty(viewModel.ReplaceBaseSlot.FirmwareFacts);
        Assert.NotEmpty(viewModel.CtrlRamRegions);
        Assert.Contains(
            viewModel.ReplaceSlots,
            slot => slot.SlotId == replacement.SlotId && slot.FilePath == replacementPath);
        Assert.True(viewModel.CanBuildReplace, viewModel.ReplaceReadinessStatus);

        releaseFirstBase.Set();
        await baseSelection;
        Assert.Contains(
            viewModel.ReplaceSlots,
            slot => slot.SlotId == replacement.SlotId && slot.FilePath == replacementPath);
    }

    private static MainWindowViewModel CreateInspectionViewModel(
        Func<string, string, string?, WorkbenchCtrlRamInspectionRequest?, WorkbenchFirmwareInspection> reader)
    {
        return new MainWindowViewModel(
            "test-shell",
            "test-app",
            ShellLanguage.English,
            static (_, _) => null,
            (icId, inputs) =>
            [
                .. inputs.Select(input => new WorkbenchFirmwareInspectionResult(
                    input.InspectionId,
                    reader(icId, input.Path, input.TpPath, input.CtrlRamRequest))),
            ]);
    }

    private static MainWindowViewModel CreateBatchInspectionViewModel(
        Func<
            string,
            IReadOnlyList<WorkbenchFirmwareInspectionInput>,
            IReadOnlyList<WorkbenchFirmwareInspectionResult>> reader)
    {
        return new MainWindowViewModel(
            "test-shell",
            "test-app",
            ShellLanguage.English,
            static (_, _) => null,
            reader);
    }

    private static WorkbenchFirmwareInspection DpInspection(string versionToken)
    {
        return new WorkbenchFirmwareInspection(
            null,
            null,
            new WorkbenchDpVersionMetadata(versionToken),
            null,
            null,
            null);
    }
}

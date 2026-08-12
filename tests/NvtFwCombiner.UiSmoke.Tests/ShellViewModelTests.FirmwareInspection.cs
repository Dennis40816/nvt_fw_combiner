using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
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

        FirmwareInspectionSnapshot ReadInspection(
            string _,
            string path,
            string? __,
            CtrlRamInspectionRequest? ___)
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
        Task first = viewModel.WorkflowSession.SetSlotFileAsync(
            "merge-dp",
            firstPath,
            TestContext.Current.CancellationToken);
        Assert.True(firstStarted.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.True(viewModel.WorkflowSession.IsFirmwareInspectionLoading);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            "merge-dp",
            secondPath,
            TestContext.Current.CancellationToken);
        Assert.Equal(secondPath, viewModel.Merge.MergeSlots.Single(slot => slot.SlotId == "merge-dp").FilePath);
        Assert.Contains(
            viewModel.Merge.MergeSlots.Single(slot => slot.SlotId == "merge-dp").FirmwareFacts,
            fact => fact.Label == "DP" && fact.Value == "D02-02");
        Assert.False(viewModel.WorkflowSession.IsFirmwareInspectionLoading);

        releaseFirst.Set();
        await first;
        Assert.NotEqual(callerThread, firstReaderThread);
        Assert.Equal(secondPath, viewModel.Merge.MergeSlots.Single(slot => slot.SlotId == "merge-dp").FilePath);
        Assert.DoesNotContain(
            viewModel.Merge.MergeSlots.Single(slot => slot.SlotId == "merge-dp").FirmwareFacts,
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

        await viewModel.WorkflowSession.SetSlotFileAsync("merge-dp", path, TestContext.Current.CancellationToken);

        FirmwareSlotViewModel slot = viewModel.Merge.MergeSlots.Single(candidate => candidate.SlotId == "merge-dp");
        Assert.Empty(slot.FirmwareFacts);
        Assert.False(viewModel.WorkflowSession.IsFirmwareInspectionLoading);
    }

    /// <summary>An AB input that changes while inspected leaves no invisible pending state.</summary>
    [Fact]
    public async Task AbInputInspectionMarksChangedFileAsBlocking()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-inspection-identity");
        string path = workspace.Write("changing-ab.bin", [0x01]);
        MainWindowViewModel viewModel = CreateInspectionViewModel((_, inspectedPath, _, _) =>
        {
            using var stream = new FileStream(inspectedPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            stream.WriteByte(0x02);
            return DpInspection("0303");
        });
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.DpAbInput,
            path,
            TestContext.Current.CancellationToken);

        FirmwareSlotViewModel slot = viewModel.Merge.MergeSlots.Single(candidate =>
            candidate.SlotId == CompositionAddressSpaceIds.DpAbInput);
        Assert.False(slot.IsInputInspectionPending);
        Assert.Equal(FirmwareInputInspectionSeverity.Blocking, slot.InputInspectionSeverity);
        Assert.Contains("file changed", slot.InputInspectionStatus, StringComparison.OrdinalIgnoreCase);
        Assert.True(slot.BlocksBuild);
        Assert.False(viewModel.Merge.CanBuildMerge);
    }

    /// <summary>Changing NT51950's selected AB topology discards projections and re-inspects the retained inputs.</summary>
    [Fact]
    public async Task AbTopologyChangeReinspectsSelectedInputs()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-topology-refresh");
        var observedTopologies = new List<string?>();
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((icId, inputs) =>
        {
            observedTopologies.AddRange(inputs.Select(static input => input.AbMergeTopologyToken));
            return BuiltInFirmwareInspection.InspectFirmwareBatch(
                (BuiltInFirmwareInspection)TestHost.FirmwareInspectionExperience,
                icId,
                inputs);
        });
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.DpAbInput,
            workspace.Write("dp-ab.bin", new byte[0x80000]),
            TestContext.Current.CancellationToken);
        Assert.Equal(["single"], observedTopologies);

        viewModel.WorkflowSession.SelectedNumber = "cascade";
        await viewModel.WorkflowSession.FirmwareInspectionRefreshTask;

        Assert.Equal(["single", "cascade"], observedTopologies);
    }

    /// <summary>Accepting a visible NT51950 topology prompt refreshes the retained AB input under the accepted selection.</summary>
    [Fact]
    public async Task AcceptingAbTopologyPromptReinspectsSelectedInputs()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-topology-prompt-refresh");
        var observedTopologies = new List<string?>();
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        {
            observedTopologies.AddRange(inputs.Select(static input => input.AbMergeTopologyToken));
            return
            [
                .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                    input.InspectionId,
                    new FirmwareInspectionSnapshot(
                        null,
                        null,
                        null,
                        null,
                        new FirmwareContextSuggestion("NT51950", "cascade", 2, "1.0.0", 0x5195),
                        null))),
            ];
        });
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput,
            workspace.Write("tp-a.bin", new byte[0x37000]),
            TestContext.Current.CancellationToken);
        Assert.True(viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen);
        Assert.Equal(["single"], observedTopologies);

        viewModel.WorkflowSession.AcceptFirmwareNumberMismatchCommand.Execute(null);
        await viewModel.WorkflowSession.FirmwareInspectionRefreshTask;

        Assert.Equal("cascade", viewModel.WorkflowSession.SelectedNumber);
        Assert.Equal(["single", "cascade"], observedTopologies);
    }

    /// <summary>Changing CtrlRAM Number reinspects selected files and replaces stale UI projections.</summary>
    [Fact]
    public async Task CtrlRamNumberChangeReinspectsSelectedSnapshot()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-context-external-mutation");
        string basePath = workspace.Write("base.bin", [0x01]);
        int reads = 0;
        MainWindowViewModel viewModel = CreateInspectionViewModel((_, _, _, _) =>
        {
            reads++;
            return DpInspection(reads == 1 ? "0101" : "0202");
        });
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            "replace-base",
            basePath,
            TestContext.Current.CancellationToken);
        Assert.Equal(1, reads);
        Assert.Contains(viewModel.Replace.ReplaceBaseSlot.FirmwareFacts, fact => fact.Value == "D01-01");

        using (var stream = new FileStream(basePath, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            stream.WriteByte(0x00);
        }
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
        await viewModel.WorkflowSession.FirmwareInspectionRefreshTask;

        Assert.Equal(2, reads);
        Assert.Contains(viewModel.Replace.ReplaceBaseSlot.FirmwareFacts, fact => fact.Value == "D02-02");

        await viewModel.WorkflowSession.SetSlotFileAsync(
            "replace-base",
            basePath,
            TestContext.Current.CancellationToken);

        Assert.Equal(3, reads);
        Assert.Contains(viewModel.Replace.ReplaceBaseSlot.FirmwareFacts, fact => fact.Value == "D02-02");
    }

    /// <summary>Non-canonical fact projections cannot publish a compiled memory layout.</summary>
    [Fact]
    public async Task MergeMemoryStaysPendingWithoutCanonicalInputPublication()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-dp-length-snapshot");
        string dpPath = workspace.Write("dp.bin", new byte[0x40000]);
        MainWindowViewModel viewModel = CreateInspectionViewModel((_, _, _, _) => DpInspection("0101"));
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        await viewModel.WorkflowSession.SetSlotFileAsync("merge-dp", dpPath, TestContext.Current.CancellationToken);
        Assert.Equal("Memory layout pending", viewModel.Merge.MergeMemoryRangeLabel);

        using (var stream = new FileStream(dpPath, FileMode.Open, FileAccess.Write, FileShare.Read))
        {
            stream.SetLength(0x80000);
        }
        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        viewModel.Merge.SelectedMergeMode = ExperienceIds.StandardMerge;

        Assert.Equal("Memory layout pending", viewModel.Merge.MergeMemoryRangeLabel);

        await viewModel.WorkflowSession.SetSlotFileAsync("merge-dp", dpPath, TestContext.Current.CancellationToken);

        Assert.Equal("Memory layout pending", viewModel.Merge.MergeMemoryRangeLabel);
    }

    /// <summary>Merge-only refreshes cannot overwrite the typed CtrlRAM Replace projection.</summary>
    [Fact]
    public async Task MergeRefreshPreservesInspectedCtrlRamReplaceDisplay()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-cross-workflow-memory");
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51926"));
        string mergeDpPath = workspace.Write("merge-dp.bin", [0x01]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        await viewModel.WorkflowSession.SetSlotFileAsync("replace-base", basePath, TestContext.Current.CancellationToken);
        string replaceRange = viewModel.Replace.ReplaceMemoryRangeLabel;
        string[] replaceRows = [.. viewModel.Replace.ReplaceMemoryRows.Select(static row => row.RangeLabel)];
        string[] replaceCoverage = [.. viewModel.Replace.ReplaceCoverageSegments.Select(static segment => segment.RangeLabel)];

        await viewModel.WorkflowSession.SetSlotFileAsync("merge-dp", mergeDpPath, TestContext.Current.CancellationToken);
        viewModel.Merge.SelectedMergeMode = ExperienceIds.GeneralMerge;
        viewModel.Merge.GeneralMergeOutputLength = "0x80000";

        Assert.Equal(replaceRange, viewModel.Replace.ReplaceMemoryRangeLabel);
        Assert.Equal(replaceRows, viewModel.Replace.ReplaceMemoryRows.Select(static row => row.RangeLabel));
        Assert.Equal(replaceCoverage, viewModel.Replace.ReplaceCoverageSegments.Select(static segment => segment.RangeLabel));
    }

    /// <summary>A hidden Standard Merge TP transition cannot replace profile-selected CtrlRAM context.</summary>
    [Fact]
    public async Task HiddenMergeTpContextKeepsProfileSelectedCtrlRamSlots()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-tp-context-ctrlram-slots");
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51926"));
        string tpPath = workspace.Write("tp.bin", [0x01]);
        MainWindowViewModel viewModel = CreateInspectionViewModel((icId, path, dependentTpPath, request) =>
            string.Equals(path, basePath, StringComparison.Ordinal)
                ? BuiltInFirmwareInspection.InspectFirmwareBatch(
                    (BuiltInFirmwareInspection)TestHost.FirmwareInspectionExperience,
                    icId,
                    [new FirmwareInspectionSnapshotInput(
                        "single",
                        path,
                        dependentTpPath,
                        request)])
                    .Single().Inspection
                : new FirmwareInspectionSnapshot(
                    null,
                    null,
                    null,
                    null,
                    new FirmwareContextSuggestion(
                        "NT51926",
                        IcNumberSelectionTokens.SingleChip,
                        1,
                        "1.4.1",
                        0x5101),
                    null));
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        await viewModel.WorkflowSession.SetSlotFileAsync("replace-base", basePath, TestContext.Current.CancellationToken);
        Assert.Contains(
            viewModel.Replace.ReplaceSlots,
            slot => slot.Description.Contains("max 5728 B", StringComparison.Ordinal));

        await viewModel.WorkflowSession.SetSlotFileAsync("merge-tp", tpPath, TestContext.Current.CancellationToken);

        Assert.Equal(IcNumberSelectionTokens.Cascade, viewModel.WorkflowSession.SelectedNumber);
        Assert.False(viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen);
        Assert.Contains(
            viewModel.Replace.ReplaceSlots,
            slot => slot.Description.Contains("max 5728 B", StringComparison.Ordinal));
        Assert.DoesNotContain(
            viewModel.Replace.ReplaceSlots,
            slot => slot.Description.Contains("max 5278 B", StringComparison.Ordinal));
    }

    /// <summary>TP-first is rejected until DP resolves, then the admitted sequence publishes paired facts.</summary>
    [Fact]
    public async Task MergeTpInspectionRequiresDpBeforePublishingPairedFacts()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement fixture = golden.CaseByIc("51950");
        string dpPath = golden.ManifestPath(fixture.GetProperty("inputs").GetProperty("dp-input"));
        string tpPath = golden.ManifestPath(fixture.GetProperty("inputs").GetProperty("tp-input"));

        MainWindowViewModel tpFirst = PresentationTestHost.CreateViewModel();
        tpFirst.WorkflowSession.SelectedIc = "NT51950";
        await tpFirst.WorkflowSession.SetSlotFileAsync("merge-tp", tpPath, TestContext.Current.CancellationToken);
        Assert.False(tpFirst.Merge.MergeSlots.Single(slot => slot.SlotId == "merge-tp").HasFile);
        await tpFirst.WorkflowSession.SetSlotFileAsync("merge-dp", dpPath, TestContext.Current.CancellationToken);
        await tpFirst.WorkflowSession.SetSlotFileAsync("merge-tp", tpPath, TestContext.Current.CancellationToken);

        MainWindowViewModel dpFirst = PresentationTestHost.CreateViewModel();
        dpFirst.WorkflowSession.SelectedIc = "NT51950";
        await dpFirst.WorkflowSession.SetSlotFileAsync("merge-dp", dpPath, TestContext.Current.CancellationToken);
        await dpFirst.WorkflowSession.SetSlotFileAsync("merge-tp", tpPath, TestContext.Current.CancellationToken);

        string[] tpFirstFacts =
        [
            .. tpFirst.Merge.MergeSlots.Single(slot => slot.SlotId == "merge-dp").FirmwareFacts
                .Select(static fact => $"{fact.Label}:{fact.Value}"),
        ];
        string[] dpFirstFacts =
        [
            .. dpFirst.Merge.MergeSlots.Single(slot => slot.SlotId == "merge-dp").FirmwareFacts
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
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement fixture = golden.CaseByIc("51926");
        string dpPath = golden.ManifestPath(fixture.GetProperty("inputs").GetProperty("dp-input"));
        string tpPath = golden.ManifestPath(fixture.GetProperty("inputs").GetProperty("tp-input"));
        var batches = new List<FirmwareInspectionSnapshotInput[]>();
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        {
            batches.Add([.. inputs]);
            return
            [
                .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                    input.InspectionId,
                    new FirmwareInspectionSnapshot(
                        null,
                        null,
                        input.InspectionId == "merge-dp" ? new DpVersionMetadata("0102") : null,
                        null,
                        input.InspectionId == "merge-dp"
                            ? new FirmwareContextSuggestion("NT51926", "cascade", 2, "1.4.1", 0x5102)
                            : null,
                        null))),
            ];
        });
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;

        await viewModel.WorkflowSession.SetSlotFileAsync("merge-dp", dpPath, TestContext.Current.CancellationToken);
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);
        await viewModel.WorkflowSession.SetSlotFileAsync("merge-tp", tpPath, TestContext.Current.CancellationToken);

        FirmwareInspectionSnapshotInput[] pairedBatch = Assert.Single(
            batches,
            static batch => batch.Length == 2);
        Assert.Equal(["merge-dp", "merge-tp"], pairedBatch.Select(static input => input.InspectionId));
        Assert.Equal(tpPath, pairedBatch.Single(input => input.InspectionId == "merge-dp").TpPath);
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);
    }

    /// <summary>Accepting an IC hint does not open a Number prompt while that control is hidden.</summary>
    [Fact]
    public async Task AcceptedIcMismatchDoesNotPromptForHiddenNumberContext()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-accepted-ic-context");
        string basePath = workspace.Write("NT51927_base.bin", [0x01]);
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        [
            .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                input.InspectionId,
                new FirmwareInspectionSnapshot(
                    "NT51927",
                    null,
                    null,
                    null,
                    new FirmwareContextSuggestion("NT51927", "2", 2, "1.4.1", 0x5102),
                    null))),
        ]);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;

        await viewModel.WorkflowSession.SetSlotFileAsync("replace-base", basePath, TestContext.Current.CancellationToken);
        Assert.True(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);

        viewModel.WorkflowSession.AcceptFirmwareIcMismatchCommand.Execute(null);
        await viewModel.WorkflowSession.FirmwareInspectionRefreshTask;

        Assert.Equal("NT51927", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(IcNumberSelectionTokens.SingleChip, viewModel.WorkflowSession.SelectedNumber);
        Assert.False(viewModel.WorkflowSession.IsFirmwareNumberMismatchModalOpen);
        Assert.False(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
    }

    /// <summary>A marker within one perfect family silently adopts the detected IC and retains the selected BIN.</summary>
    [Fact]
    public async Task PerfectFamilyIcHintAdoptsDetectedContextWithoutPrompt()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-perfect-family-context");
        string basePath = workspace.Write("NT51932_base.bin", [0x01]);
        var batches = new List<(string IcId, FirmwareInspectionSnapshotInput[] Inputs)>();
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((icId, inputs) =>
        {
            batches.Add((icId, [.. inputs]));
            return
            [
                .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                    input.InspectionId,
                    new FirmwareInspectionSnapshot("NT51932", null, null, null, null, null))),
            ];
        });
        viewModel.WorkflowSession.SelectedIc = "NT51929";

        await viewModel.WorkflowSession.SetSlotFileAsync("replace-base", basePath, TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.FirmwareInspectionRefreshTask;

        Assert.False(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
        Assert.Equal("NT51932", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(basePath, viewModel.Replace.ReplaceBaseSlot.FilePath);
        Assert.Equal("NT51932", batches[^1].IcId);
        Assert.Contains(batches[^1].Inputs, static input => input.InspectionId == "replace-base");
    }

    /// <summary>Accepting a replacement hint retains a compatible slot and reinspects it in the new IC context.</summary>
    [Fact]
    public async Task AcceptedIcMismatchRetainsCompatibleCtrlRamReplacement()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-accepted-replacement-ic");
        string replacementPath = workspace.Write("NT51927_replacement.bin", [0x01]);
        var batches = new List<(string IcId, FirmwareInspectionSnapshotInput[] Inputs)>();
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((icId, inputs) =>
        {
            batches.Add((icId, [.. inputs]));
            return
            [
                .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                    input.InspectionId,
                    new FirmwareInspectionSnapshot("NT51927", null, null, null, null, null))),
            ];
        });
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        MainWindowViewModel targetContext = PresentationTestHost.CreateViewModel();
        targetContext.WorkflowSession.SelectedIc = "NT51927";
        targetContext.WorkflowSession.SelectedNumber = "2";
        OpenReplace(targetContext, ExperienceIds.CtrlRamReplace);
        HashSet<string> targetSlotIds =
        [
            .. targetContext.Replace.ReplaceSlots
                .Where(slot => !ReferenceEquals(slot, targetContext.Replace.ReplaceBaseSlot))
                .Select(static slot => slot.SlotId),
        ];
        FirmwareSlotViewModel replacement = viewModel.Replace.ReplaceSlots.First(slot =>
            !ReferenceEquals(slot, viewModel.Replace.ReplaceBaseSlot) && targetSlotIds.Contains(slot.SlotId));

        await viewModel.WorkflowSession.SetSlotFileAsync(
            replacement.SlotId,
            replacementPath,
            TestContext.Current.CancellationToken);
        Assert.True(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);

        viewModel.WorkflowSession.AcceptFirmwareIcMismatchCommand.Execute(null);
        await viewModel.WorkflowSession.FirmwareInspectionRefreshTask;

        Assert.Equal("NT51927", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(
            replacementPath,
            viewModel.Replace.ReplaceSlots.Single(slot => slot.SlotId == replacement.SlotId).FilePath);
        (string successorIc, FirmwareInspectionSnapshotInput[] successorInputs) = batches[^1];
        Assert.Equal("NT51927", successorIc);
        Assert.Contains(successorInputs, input => input.InspectionId == replacement.SlotId);
        Assert.False(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);
    }

    /// <summary>An accepted replacement cannot cross into an IC that lacks the same safe slot silently.</summary>
    [Fact]
    public async Task AcceptedIcMismatchExplainsUnavailableReplacementSlot()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-accepted-missing-replacement");
        string replacementPath = workspace.Write("NT51929_replacement.bin", [0x01]);
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        [
            .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                input.InspectionId,
                new FirmwareInspectionSnapshot("NT51929", null, null, null, null, null))),
        ]);
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        FirmwareSlotViewModel replacement = viewModel.Replace.ReplaceSlots.Single(slot =>
            slot.SlotId == "replace-ctrlram-mp");

        await viewModel.WorkflowSession.SetSlotFileAsync(
            replacement.SlotId,
            replacementPath,
            TestContext.Current.CancellationToken);
        Assert.True(viewModel.WorkflowSession.IsFirmwareIcMismatchModalOpen);

        viewModel.WorkflowSession.AcceptFirmwareIcMismatchCommand.Execute(null);
        await viewModel.WorkflowSession.FirmwareInspectionRefreshTask;

        Assert.Equal("NT51929", viewModel.WorkflowSession.SelectedIc);
        Assert.DoesNotContain(
            viewModel.Replace.ReplaceSlots,
            slot => string.Equals(slot.FilePath, replacementPath, StringComparison.Ordinal));
        Assert.Contains("NT51929_replacement.bin", viewModel.Reports.ReportToastText, StringComparison.Ordinal);
        Assert.Contains("same safe input slot", viewModel.Reports.ReportToastText, StringComparison.Ordinal);
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
                .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                    input.InspectionId,
                    new FirmwareInspectionSnapshot(null, null, null, null, null, null))),
            ];
        });
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        FirmwareSlotViewModel replacement = viewModel.Replace.ReplaceSlots.First(slot =>
            !ReferenceEquals(slot, viewModel.Replace.ReplaceBaseSlot));
        await viewModel.WorkflowSession.SetSlotFileAsync(
            replacement.SlotId,
            replacementPath,
            TestContext.Current.CancellationToken);
        inspectedIds.Clear();

        viewModel.WorkflowSession.SelectedIc = "NT51927";
        await viewModel.WorkflowSession.FirmwareInspectionRefreshTask;

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

            return BuiltInFirmwareInspection.InspectFirmwareBatch(
                (BuiltInFirmwareInspection)TestHost.FirmwareInspectionExperience,
                icId,
                inputs);
        });
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        FirmwareSlotViewModel replacement = viewModel.Replace.ReplaceSlots.First(slot =>
            !ReferenceEquals(slot, viewModel.Replace.ReplaceBaseSlot) &&
            slot.Title.Contains("VN CtrlRAM", StringComparison.Ordinal));
        string replacementPath = workspace.Write("vn.bin", [0x01]);

        Task baseSelection = viewModel.WorkflowSession.SetSlotFileAsync(
            "replace-base",
            basePath,
            TestContext.Current.CancellationToken);
        Assert.True(firstBaseStarted.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Contains("Inspecting", viewModel.Replace.ReplaceReadinessStatus, StringComparison.Ordinal);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            replacement.SlotId,
            replacementPath,
            TestContext.Current.CancellationToken);

        Assert.False(viewModel.WorkflowSession.IsFirmwareInspectionLoading);
        Assert.NotEmpty(viewModel.Replace.ReplaceBaseSlot.FirmwareFacts);
        Assert.NotEmpty(viewModel.Replace.CtrlRamRegions);
        Assert.Contains(
            viewModel.Replace.ReplaceSlots,
            slot => slot.SlotId == replacement.SlotId && slot.FilePath == replacementPath);
        Assert.True(viewModel.Replace.CanBuildReplace, viewModel.Replace.ReplaceReadinessStatus);

        releaseFirstBase.Set();
        await baseSelection;
        Assert.Contains(
            viewModel.Replace.ReplaceSlots,
            slot => slot.SlotId == replacement.SlotId && slot.FilePath == replacementPath);
    }

    private MainWindowViewModel CreateInspectionViewModel(
        Func<string, string, string?, CtrlRamInspectionRequest?, FirmwareInspectionSnapshot> reader)
    {
        PresentationHostServices services = PresentationTestHost.CreateServices("test-app");
        var viewModel = new MainWindowViewModel(
            "test-shell",
            "test-app",
            ShellLanguage.English,
            services,
            new DelegatingFirmwareInspection(
                TestHost.FirmwareInspectionExperience,
                batchReader: (icId, inputs) =>
                [
                    .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                        input.InspectionId,
                        reader(icId, input.Path, input.TpPath, input.CtrlRamRequest))),
                ]));
        return PresentationTestHost.PublishCanonicalCatalog(services, viewModel);
    }

    private static FirmwareInspectionSnapshot DpInspection(string versionToken)
    {
        return new FirmwareInspectionSnapshot(
            null,
            null,
            new DpVersionMetadata(versionToken),
            null,
            null,
            null);
    }
}

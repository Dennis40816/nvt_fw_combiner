using System.Globalization;
using System.Text.Json;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellViewModelTests
{
    /// <summary>The unavailable AB readiness hint directs the user to declared routes without duplicating catalog facts.</summary>
    [Fact]
    public void AbMergeUnavailableReadinessDirectsToDeclaredRoutes()
    {
        MainWindowViewModel english = ShellViewModelFactory.Create();
        MainWindowViewModel traditionalChinese = ShellViewModelFactory.Create(ShellLanguage.ChineseTraditional);

        string englishHint = english.Text.GetAbMergeReadinessStatus("NT51917", false, 0, 0, 0, 0);
        string traditionalChineseHint = traditionalChinese.Text.GetAbMergeReadinessStatus("NT51917", false, 0, 0, 0, 0);

        Assert.Contains("declared AB Code route", englishHint, StringComparison.Ordinal);
        Assert.Contains("已宣告的 AB Code 路徑", traditionalChineseHint, StringComparison.Ordinal);
    }

    /// <summary>Verifies Normal Merge hides IC Number while preserving row layout space.</summary>
    [Fact]
    public void NormalMergeHidesNumberSelectorButKeepsPlaceholder()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowMergeCommand.Execute(null);

        Assert.True(viewModel.IsMergeVisible);
        Assert.True(viewModel.IsDeviceContextVisible);
        Assert.True(viewModel.Merge.IsNormalMergeModeSelected);
        Assert.Equal(["Normal", "AB Code", "General"], viewModel.Merge.MergeModeChoices);
        Assert.False(viewModel.WorkflowSession.IsNumberSelectorVisible);
        Assert.True(viewModel.WorkflowSession.IsNumberSelectorPlaceholderVisible);
        Assert.Equal("NT51950: refresh profile, slots, validation", viewModel.WorkflowSession.DeviceContextStatus);

        viewModel.ShowReplaceCommand.Execute(null);

        Assert.True(viewModel.IsReplaceVisible);
        Assert.True(viewModel.WorkflowSession.IsNumberSelectorVisible);
        Assert.False(viewModel.WorkflowSession.IsNumberSelectorPlaceholderVisible);
        Assert.Equal("NT51950 / single: refresh profile, slots, validation", viewModel.WorkflowSession.DeviceContextStatus);
    }

    /// <summary>Every admitted Standard Merge profile keeps IC Number out of its authoring context.</summary>
    [Fact]
    public void EverySupportedStandardMergeHidesNumberSelector()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowMergeCommand.Execute(null);

        foreach (WorkbenchProfileSummary profile in WorkbenchCompositionService.GetStandardMergeProfileSummaries())
        {
            viewModel.WorkflowSession.SelectedIc = profile.IcId;

            Assert.True(viewModel.Merge.IsNormalMergeModeSelected, profile.IcId);
            Assert.True(viewModel.Merge.IsStandardMergeSupported, profile.IcId);
            Assert.False(viewModel.WorkflowSession.IsNumberSelectorVisible, profile.IcId);
            Assert.True(viewModel.WorkflowSession.IsNumberSelectorPlaceholderVisible, profile.IcId);
        }
    }

    /// <summary>AB Code is available for all function-open profiles and Home exposes only those ICs.</summary>
    [Fact]
    public void AbMergeExposureAndHomeContextContainAllFunctionOpenProfiles()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Assert.Contains(WorkbenchMergeModes.AbCode, viewModel.Merge.MergeModeChoices);
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        Assert.Contains(WorkbenchMergeModes.AbCode, viewModel.Merge.MergeModeChoices);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        Assert.Contains(WorkbenchMergeModes.AbCode, viewModel.Merge.MergeModeChoices);
        viewModel.WorkflowSession.SelectedIc = "NT51951";
        Assert.Contains(WorkbenchMergeModes.AbCode, viewModel.Merge.MergeModeChoices);

        viewModel.BeginAbMergeFromHomeCommand.Execute(null);

        Assert.True(viewModel.WorkflowSession.IsWorkflowContextModalOpen);
        Assert.Equal(
            ["NT51919", "NT51929", "NT51932", "NT51950", "NT51951"],
            viewModel.WorkflowSession.WorkflowContextSetup.IcChoices);
        Assert.Equal("NT51951", viewModel.WorkflowSession.WorkflowContextSetup.SelectedIc);

        viewModel.WorkflowSession.WorkflowContextSetup.SelectedIc = "NT51929";
        viewModel.WorkflowSession.ConfirmWorkflowContextCommand.Execute(null);

        Assert.True(viewModel.IsMergeVisible);
        Assert.True(viewModel.Merge.IsAbCodeMergeModeSelected);
        Assert.False(viewModel.WorkflowSession.IsNumberSelectorVisible);
        Assert.True(viewModel.WorkflowSession.IsNumberSelectorPlaceholderVisible);
        Assert.Equal("NT51929", viewModel.WorkflowSession.SelectedIc);
        Assert.Equal(
            ["NT51919", "NT51929", "NT51932", "NT51950", "NT51951"],
            viewModel.WorkflowSession.IcChoices);
        Assert.Equal(
            [
                CompositionAddressSpaceIds.DpAbInput,
                CompositionAddressSpaceIds.TpAInput,
                CompositionAddressSpaceIds.TpBInput,
            ],
            viewModel.Merge.MergeSlots.Select(static slot => slot.SlotId));
    }

    /// <summary>NT51950 selects its profile-owned physical layout through the shared IC Number context.</summary>
    [Fact]
    public void Nt51950AbMergeSelectsSingleOrCascadeTopology()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = WorkbenchMergeModes.AbCode;

        Assert.True(viewModel.Merge.HasAbMergeTopologyChoices);
        Assert.True(viewModel.WorkflowSession.IsNumberSelectorVisible);
        Assert.False(viewModel.WorkflowSession.IsNumberSelectorPlaceholderVisible);
        Assert.Equal(
            ["single", "cascade"],
            viewModel.Merge.AbMergeTopologyChoices.Select(static choice => choice.Token));
        Assert.Equal(
            ["single", "cascade"],
            viewModel.WorkflowSession.NumberSelectionChoices.Select(static choice => choice.Token));
        Assert.Equal("single", viewModel.WorkflowSession.SelectedNumber);
        Assert.Equal("0x00000-0x7FFFF (len 0x80000)", viewModel.Merge.MergeMemoryRangeLabel);

        viewModel.WorkflowSession.SelectedNumber = "cascade";

        Assert.Equal("cascade", viewModel.WorkflowSession.SelectedNumber);
        Assert.Equal("0x00000-0xFFFFF (len 0x100000)", viewModel.Merge.MergeMemoryRangeLabel);

        viewModel.WorkflowSession.SelectedIc = "NT51951";
        Assert.True(viewModel.Merge.IsAbMergeSupported);
        Assert.False(viewModel.Merge.HasAbMergeTopologyChoices);
        Assert.False(viewModel.WorkflowSession.IsNumberSelectorVisible);
        Assert.True(viewModel.WorkflowSession.IsNumberSelectorPlaceholderVisible);
        Assert.Empty(viewModel.WorkflowSession.NumberSelectionChoices);

        viewModel.Merge.SelectedMergeMode = WorkbenchMergeModes.Standard;

        Assert.Equal(WorkbenchCompositionService.GetSupportedIcIds(), viewModel.WorkflowSession.IcChoices);
    }

    /// <summary>A rejected DP_AB size cannot override compiled coverage while processor effects remain on TPB.</summary>
    [Fact]
    public async Task Nt51950AbMemoryKeepsCompiledCapacityForRejectedDpLength()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-memory");
        string dpPath = workspace.Write("dp-ab-90000.bin", new byte[0x90000]);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = WorkbenchMergeModes.AbCode;

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.DpAbInput,
            dpPath,
            TestContext.Current.CancellationToken);

        Assert.Equal("0x00000-0x7FFFF (len 0x80000)", viewModel.Merge.MergeMemoryRangeLabel);
        Assert.Equal(
            ["DP AB", "TPA", "TPB"],
            viewModel.Merge.MergeMemoryRows.Select(static row => row.AfterSource));
        Assert.Contains(
            "does not match the compiled 0x80000 layout",
            viewModel.Merge.MergeMemoryRows[0].Detail,
            StringComparison.Ordinal);
        Assert.Equal("Transform + Overlay + Postbuild", viewModel.Merge.MergeMemoryRows[2].ActionLabel);
        Assert.Equal(
            ["DP AB", "TPA", "TPB"],
            viewModel.Merge.MergeCoverageSegments
                .Select(static segment => segment.SourceLabel)
                .Distinct(StringComparer.Ordinal));
        Assert.DoesNotContain(viewModel.Merge.MergeMemoryRows, static row =>
            row.Detail.Contains("CRC", StringComparison.OrdinalIgnoreCase) ||
            row.RangeLabel.Contains("Staging", StringComparison.OrdinalIgnoreCase));
        Assert.True(viewModel.Merge.MergeSlots.Single(static slot =>
            slot.SlotId == CompositionAddressSpaceIds.DpAbInput).BlocksBuild);
    }

    /// <summary>AB inputs publish independent versions and typed health before Preview becomes available.</summary>
    [Fact]
    public async Task AbMergeInputsInspectOnLoadAndPreviewThroughSharedRuntime()
    {
        const int dpLength = 0x80000;
        const int tpLength = 0x40000;
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-load");
        byte[] dp = new byte[dpLength];
        WriteUiAbCmi(dp, 0, major: 0x06, minor: 0x05, jira: 0x123);
        WriteUiAbCmi(dp, tpLength, major: 0x07, minor: 0x08, jira: 0x456);
        string dpPath = workspace.Write("dp-ab.bin", dp);
        string tpAPath = workspace.Write("tp-a.bin", CreateUiAbTpImage(
            0x81, 0x00, commonFwMajor: 1, commonFwMinor: 4, commonFwAdditional: 1, projectId: 0x5102));
        string tpBPath = workspace.Write("tp-b.bin", CreateUiAbTpImage(
            0x82, 0x03, commonFwMajor: 2, commonFwMinor: 0, commonFwAdditional: 0, projectId: 0x6A5C));
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.Merge.SelectedMergeMode = WorkbenchMergeModes.AbCode;

        Assert.False(viewModel.Merge.CanBuildMerge);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.DpAbInput,
            dpPath,
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput,
            tpAPath,
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpBInput,
            tpBPath,
            TestContext.Current.CancellationToken);

        Assert.All(viewModel.Merge.MergeSlots, static slot =>
        {
            Assert.Equal(WorkbenchInputInspectionSeverity.Valid, slot.InputInspectionSeverity);
            Assert.False(slot.BlocksBuild);
            Assert.False(slot.IsInputInspectionPending);
        });
        FirmwareSlotViewModel dpSlot = viewModel.Merge.MergeSlots.Single(
            static slot => slot.SlotId == CompositionAddressSpaceIds.DpAbInput);
        Assert.Contains(dpSlot.FirmwareFacts, static fact => fact.Label == "DP1" && fact.Value.StartsWith("D06-05", StringComparison.Ordinal));
        Assert.Contains(dpSlot.FirmwareFacts, static fact => fact.Label == "DP2" && fact.Value.StartsWith("D07-08", StringComparison.Ordinal));
        FirmwareSlotViewModel tpASlot = viewModel.Merge.MergeSlots.Single(
            static slot => slot.SlotId == CompositionAddressSpaceIds.TpAInput);
        Assert.Contains(
            tpASlot.FirmwareFacts,
            static fact => fact.Label == "TPA" && fact.Value == "T81-00");
        Assert.Contains(tpASlot.FirmwareFacts, static fact => fact.Label == "Common FW" && fact.Value == "1.4.1");
        Assert.Contains(tpASlot.FirmwareFacts, static fact => fact.Label == "PID" && fact.Value == "0x5102");
        Assert.DoesNotContain(tpASlot.FirmwareFacts, static fact => fact.Label == "TP");
        FirmwareSlotViewModel tpBSlot = viewModel.Merge.MergeSlots.Single(
            static slot => slot.SlotId == CompositionAddressSpaceIds.TpBInput);
        Assert.Contains(
            tpBSlot.FirmwareFacts,
            static fact => fact.Label == "TPB" && fact.Value == "T82-03");
        Assert.Contains(tpBSlot.FirmwareFacts, static fact => fact.Label == "Common FW" && fact.Value == "2.0.0");
        Assert.Contains(tpBSlot.FirmwareFacts, static fact => fact.Label == "PID" && fact.Value == "0x6A5C");
        Assert.DoesNotContain(tpBSlot.FirmwareFacts, static fact => fact.Label == "TP");
        Assert.True(viewModel.Merge.CanBuildMerge);
        Assert.True(viewModel.Merge.PreviewMergeCommand.CanExecute(null));

        string suggestedOutputName = await viewModel.Merge.ResolveMergeOutputFileNameForSaveAsync(
            TestContext.Current.CancellationToken);
        Assert.Matches(
            "^NT51929_FlashCode_A_D0605T8100_B_D0708T8203_[0-9]{8}\\.bin$",
            suggestedOutputName);
        Assert.DoesNotContain("D06-05", suggestedOutputName, StringComparison.Ordinal);
        MergeBuildSavePreparation initialPreparation = Assert.IsType<MergeBuildSavePreparation>(
            await viewModel.Merge.TryPrepareMergeBuildSaveAsync(TestContext.Current.CancellationToken));
        WorkbenchAbAFlashCodeDeliveryPlan initialAFlashCodePlan = Assert.IsType<WorkbenchAbAFlashCodeDeliveryPlan>(
            initialPreparation.AFlashCodePlan);

        WriteUiAbCmi(dp, 0, major: 0x0A, minor: 0x01, jira: 0x123);
        await File.WriteAllBytesAsync(dpPath, dp, TestContext.Current.CancellationToken);
        string automaticOutputPath = workspace.PathFor(suggestedOutputName);
        string automaticAFlashCodeOutputPath = workspace.PathFor(initialAFlashCodePlan.SuggestedFileName);
        await viewModel.Merge.BuildMergeAsync(
            automaticOutputPath,
            automaticAFlashCodeOutputPath,
            outputPathUsesAutomaticName: true,
            aFlashCodeOutputPathUsesAutomaticName: true);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.NotEqual(automaticOutputPath, viewModel.RunSession.LastRunResult.Output);
        Assert.Matches(
            "^NT51929_FlashCode_A_D0A01T8100_B_D0708T8203_[0-9]{8}\\.bin$",
            Path.GetFileName(viewModel.RunSession.LastRunResult.Output));
        Assert.True(File.Exists(viewModel.RunSession.LastRunResult.Output));
        using (var report = JsonDocument.Parse(viewModel.Reports.LoadedReportJson))
        {
            JsonElement naming = report.RootElement.GetProperty("OutputNaming");
            Assert.False(naming.GetProperty("IsExplicitOverride").GetBoolean());
            Assert.Equal(
                Path.GetFileName(viewModel.RunSession.LastRunResult.Output),
                naming.GetProperty("ActualFileName").GetString());
            Assert.Equal(
                Path.GetFileName(viewModel.RunSession.LastRunResult.Output),
                naming.GetProperty("AutomaticFileName").GetString());
            JsonElement aFlashCodeDelivery = Assert.Single(
                report.RootElement.GetProperty("DeliveryArtifacts").EnumerateArray());
            Assert.Matches(
                "^NT51929_FlashCode_D0A01T8100_[0-9]{8}\\.bin$",
                aFlashCodeDelivery.GetProperty("FileName").GetString());
            Assert.True(File.Exists(Path.Combine(
                workspace.Root,
                aFlashCodeDelivery.GetProperty("FileName").GetString()!)));
        }

        await viewModel.Merge.PreviewMergeCommand.ExecuteAsync(null);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(viewModel.Reports.HasLoadedReport);
    }

    /// <summary>A source that disappears after inspection becomes a Build report before the native save dialog opens.</summary>
    [Fact]
    public async Task AbMergeBuildSavePreparationReportsStaleInput()
    {
        const int dpLength = 0x80000;
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-stale-input");
        byte[] dp = new byte[dpLength];
        WriteUiAbCmi(dp, 0, major: 0x06, minor: 0x05, jira: 0x123);
        WriteUiAbCmi(dp, dpLength / 2, major: 0x07, minor: 0x08, jira: 0x456);
        string dpPath = workspace.Write("dp-ab.bin", dp);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.Merge.SelectedMergeMode = WorkbenchMergeModes.AbCode;
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.DpAbInput,
            dpPath,
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput,
            workspace.Write("tp-a.bin", CreateUiAbTpImage(0x81, 0x00, 1, 4, 1, 0x5102)),
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpBInput,
            workspace.Write("tp-b.bin", CreateUiAbTpImage(0x82, 0x03, 2, 0, 0, 0x6A5C)),
            TestContext.Current.CancellationToken);

        Assert.True(viewModel.Merge.CanBuildMerge);
        File.Delete(dpPath);

        await viewModel.WorkflowSession.RefreshSelectedMergeFirmwareInspectionsAsync();
        Assert.False(viewModel.Merge.CanBuildMerge);

        MergeBuildSavePreparation? preparation = await viewModel.Merge.TryPrepareMergeBuildSaveAsync(
            TestContext.Current.CancellationToken);

        Assert.Null(preparation);
        Assert.False(viewModel.RunSession.LastRunResult.Succeeded);
        Assert.Equal("Build failed", viewModel.RunSession.LastRunResult.Title);
        Assert.True(viewModel.Reports.HasLoadedReport);
        Assert.True(viewModel.Reports.IsReportModalOpen);
        Assert.Equal("ui.run.failed", viewModel.Reports.LoadedReport.PrimaryIssue.Title);
    }

    /// <summary>A short AB source blocks immediately while an ignored tail remains a non-blocking warning.</summary>
    [Fact]
    public async Task AbMergeLoadHealthDistinguishesBlockingAndWarning()
    {
        const int dpLength = 0x80000;
        const int tpLength = 0x40000;
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-health");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.Merge.SelectedMergeMode = WorkbenchMergeModes.AbCode;

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.DpAbInput,
            workspace.Write("dp-short.bin", new byte[dpLength - 1]),
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel dpSlot = viewModel.Merge.MergeSlots.Single(
            static slot => slot.SlotId == CompositionAddressSpaceIds.DpAbInput);
        Assert.Equal(WorkbenchInputInspectionSeverity.Blocking, dpSlot.InputInspectionSeverity);
        Assert.True(dpSlot.BlocksBuild);
        Assert.StartsWith("Error:", dpSlot.InputInspectionStatus, StringComparison.Ordinal);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput,
            workspace.Write("tp-tail.bin", new byte[tpLength + 1]),
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel tpSlot = viewModel.Merge.MergeSlots.Single(
            static slot => slot.SlotId == CompositionAddressSpaceIds.TpAInput);
        Assert.Equal(WorkbenchInputInspectionSeverity.Warning, tpSlot.InputInspectionSeverity);
        Assert.False(tpSlot.BlocksBuild);
        Assert.Contains("warning", tpSlot.InputInspectionStatus, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.Merge.CanBuildMerge);
    }

    /// <summary>Verifies General Merge uses its own mapping editor state and hides IC Number context.</summary>
    [Fact]
    public void GeneralMergeUsesEditableMappingsAndOwnOutputLength()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowMergeCommand.Execute(null);
        viewModel.Merge.SelectedMergeMode = "General";

        Assert.True(viewModel.Merge.IsGeneralMergeModeSelected);
        Assert.False(viewModel.Merge.IsNormalMergeModeSelected);
        Assert.True(viewModel.IsDeviceContextVisible);
        Assert.False(viewModel.WorkflowSession.IsNumberSelectorVisible);
        Assert.True(viewModel.WorkflowSession.IsNumberSelectorPlaceholderVisible);
        Assert.Equal("NT51950: refresh profile, slots, validation", viewModel.WorkflowSession.DeviceContextStatus);
        Assert.Equal("0x100000", viewModel.Merge.GeneralMergeOutputLength);
        Assert.Equal("0x00", viewModel.Merge.GeneralMergeOutputFillByte);
        Assert.Contains(
            viewModel.Merge.MergeMemoryRows,
            row => row.Detail.Contains("0x00", StringComparison.Ordinal));
        Assert.Equal(
            $"NT51950_FlashCode_DxxxxTxxxx_{DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.bin",
            viewModel.Merge.MergeOutputFileName);
        _ = Assert.Single(viewModel.Merge.GeneralMergeMappings);

        viewModel.Merge.AddGeneralMergeMappingCommand.Execute(null);

        Assert.Equal(2, viewModel.Merge.GeneralMergeMappings.Count);
        viewModel.WorkflowSession.RemoveGeneralMappingRow(viewModel.Merge.GeneralMergeMappings[0]);
        GeneralMergeMappingViewModel mapping = Assert.Single(viewModel.Merge.GeneralMergeMappings);
        Assert.Equal(1, mapping.Index);
        Assert.Equal("No source BIN selected", mapping.DisplayName);
        Assert.Contains("reserved", viewModel.Merge.MergeMemorySummary, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies the Home General Merge shortcut opens Merge in General mode.</summary>
    [Fact]
    public void GeneralMergeShortcutOpensGeneralMergeMode()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.BeginGeneralMergeFromHomeCommand.Execute(null);
        viewModel.WorkflowSession.ConfirmWorkflowContextCommand.Execute(null);

        Assert.True(viewModel.IsMergeVisible);
        Assert.True(viewModel.Merge.IsGeneralMergeModeSelected);
        Assert.False(viewModel.Merge.IsNormalMergeModeSelected);
        Assert.Equal("Home > Merge", viewModel.NavigationPath);
        Assert.False(viewModel.WorkflowSession.IsNumberSelectorVisible);
        Assert.True(viewModel.WorkflowSession.IsNumberSelectorPlaceholderVisible);
    }

    /// <summary>Verifies Standard Merge slots follow the selected profile instead of exposing LD globally.</summary>
    [Fact]
    public void MergeSlotsFollowSelectedProfileRequiredInputs()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.WorkflowSession.SelectedIc = "NT51926";

        Assert.Equal(["DP BIN", "TP BIN"], viewModel.Merge.MergeSlots.Select(slot => slot.Title));
        Assert.DoesNotContain(viewModel.Merge.MergeSlots, slot => slot.Title.Contains("LD", StringComparison.Ordinal));

        viewModel.WorkflowSession.SelectedIc = "NT51928";

        Assert.Equal(["DP BIN", "TP BIN", "LDC BIN"], viewModel.Merge.MergeSlots.Select(slot => slot.Title));
    }

    /// <summary>Verifies memory-map rows expose readable operation details without relying on tooltips.</summary>
    [Fact]
    public void MergeMemoryRowsExposeReadableOperationDetails()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.WorkflowSession.SelectedIc = "NT51926";

        MemoryMapRowViewModel copyRow = Assert.Single(
            viewModel.Merge.MergeMemoryRows,
            row => row.RangeLabel == "0x00000-0x3BFFF (len 0x3C000)" && row.ActionLabel == "Copy");
        Assert.Equal("Reserved -> TP BIN", copyRow.FlowLabel);
        Assert.Contains("Sequence 100", copyRow.Detail, StringComparison.Ordinal);
        Assert.Contains("Reason:", copyRow.Detail, StringComparison.Ordinal);
    }

    /// <summary>Verifies representative Standard Merge workflow shapes through the Merge ViewModel command path.</summary>
    [Theory]
    [MemberData(nameof(StandardMergeUiGoldenCases))]
    public async Task BuildMergeFromViewModelMatchesRepresentativeGolden(string ic)
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc(ic);
        using var workspace = TempWorkspace.Create($"nvt-fw-combiner-ui-{ic}");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.WorkflowSession.SelectedIc = $"NT{ic}";
        golden.CopyInputFilesToMergeSlots(viewModel, workspace, goldenCase);

        Assert.True(viewModel.Merge.PreviewMergeCommand.CanExecute(null));
        Assert.True(viewModel.Merge.BuildMergeCommand.CanExecute(null));
        Assert.True(viewModel.Merge.CanBuildMerge);

        await viewModel.Merge.PreviewMergeCommand.ExecuteAsync(null);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(viewModel.Merge.BuildMergeCommand.CanExecute(null));
        Assert.True(viewModel.Merge.CanBuildMerge);

        string outputPath = workspace.PathFor("selected-output.bin");
        await viewModel.Merge.BuildMergeAsync(outputPath);

        string expectedPath = golden.ExpectedOutputPath(goldenCase);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.Equal(outputPath, viewModel.RunSession.LastRunResult.Output);
        Assert.Contains("report ready", viewModel.RunSession.LastRunResult.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(
            viewModel.Reports.LoadedReport.OutputSha256[..Math.Min(12, viewModel.Reports.LoadedReport.OutputSha256.Length)],
            viewModel.RunSession.LastRunResult.Detail,
            StringComparison.Ordinal);
        Assert.True(File.Exists(outputPath), outputPath);
        Assert.Equal(File.ReadAllBytes(expectedPath), File.ReadAllBytes(outputPath));
        Assert.True(viewModel.Reports.HasLoadedReport);
        Assert.True(viewModel.Reports.LoadedReport.HasOutputArtifactPath);
        Assert.Equal(outputPath, viewModel.Reports.LoadedReport.OutputArtifactPath);
        Assert.Equal($"{viewModel.Reports.LoadedReport.OutputSize} bytes", viewModel.Reports.LoadedReport.OutputSizeLabel);
        Assert.Equal("Committed output", viewModel.Reports.LoadedReport.OutputCommitmentLabel);
        Assert.True(viewModel.Reports.HasReportToast);
        Assert.Equal(1, viewModel.Reports.ReportToastOpacity);
        Assert.Equal("Build report generated", viewModel.Reports.ReportToastText);
    }

    /// <summary>Verifies General Merge UI runs explicit mapping rows through Preview and Build.</summary>
    [Fact]
    public async Task GeneralMergePreviewAndBuildUseExplicitMappingRows()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-merge");
        string source = workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13, 0x14]);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.Merge.SelectedMergeMode = "General";
        viewModel.Merge.GeneralMergeOutputLength = "0x10";
        GeneralMergeMappingViewModel mapping = Assert.Single(viewModel.Merge.GeneralMergeMappings);
        mapping.SourceStartAddress = "0x1";
        mapping.TargetStartAddress = "0x4";
        mapping.Length = "0x3";
        List<string> propertyChanges = [];
        viewModel.Merge.PropertyChanged += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.PropertyName))
            {
                propertyChanges.Add(args.PropertyName);
            }
        };
        viewModel.SetSlotFile(mapping.MappingId, source);
        Assert.Contains(nameof(MergePresentationViewModel.MergeReadinessStatus), propertyChanges);
        Assert.Contains("maps 1 source BIN", viewModel.Merge.MergeReadinessStatus, StringComparison.Ordinal);
        viewModel.Merge.GeneralMergeOutputFillByte = "0x100";
        Assert.False(viewModel.Merge.PreviewMergeCommand.CanExecute(null));
        Assert.False(viewModel.Merge.CanBuildMerge);
        viewModel.Merge.GeneralMergeOutputFillByte = "0xA5";
        Assert.Contains(
            viewModel.Merge.MergeMemoryRows,
            row => row.Detail.Contains("0xA5", StringComparison.Ordinal));
        Assert.True(viewModel.Merge.PreviewMergeCommand.CanExecute(null));
        Assert.True(viewModel.Merge.CanBuildMerge);
        Assert.True(viewModel.Merge.IsGeneralMergeModeSelected);
        Assert.False(viewModel.Merge.IsNormalMergeModeSelected);
        await viewModel.Merge.PreviewMergeCommand.ExecuteAsync(null);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(viewModel.Merge.CanBuildMerge);
        Assert.True(viewModel.Merge.IsGeneralMergeModeSelected);
        string outputPath = workspace.PathFor("general-merge.bin");
        await File.WriteAllBytesAsync(
            source,
            [0x10, 0x11, 0x99, 0x13, 0x14],
            TestContext.Current.CancellationToken);
        await viewModel.Merge.BuildMergeAsync(outputPath);
        Assert.False(viewModel.RunSession.LastRunResult.Succeeded);
        Assert.Contains(
            "no longer matches",
            viewModel.RunSession.LastRunResult.Detail,
            StringComparison.Ordinal);
        Assert.False(File.Exists(outputPath));
        viewModel.SetSlotFile(mapping.MappingId, source);
        await viewModel.Merge.BuildMergeAsync(outputPath);
        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.Equal(outputPath, viewModel.RunSession.LastRunResult.Output);
        Assert.Equal(
            [0xA5, 0xA5, 0xA5, 0xA5, 0x11, 0x99, 0x13, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5],
            File.ReadAllBytes(outputPath));
        using var document = JsonDocument.Parse(viewModel.Reports.LoadedReportJson);
        JsonElement root = document.RootElement;
        Assert.Equal("nt51950-general-merge-logical-candidate", root.GetProperty("ProfileId").GetString());
        Assert.Equal("general-merge", root.GetProperty("ExperienceId").GetString());
        JsonElement initialization = root.GetProperty("ImageInitialization");
        Assert.Equal(0x10, initialization.GetProperty("Capacity").GetInt64());
        Assert.Equal(0xA5, initialization.GetProperty("FillByte").GetInt32());
        JsonElement operation = Assert.Single(root.GetProperty("Operations").EnumerateArray());
        Assert.Equal("CopyRange", operation.GetProperty("Kind").GetString());
        Assert.Equal("Succeeded", operation.GetProperty("Status").GetString());
    }

    /// <summary>Verifies Standard Merge Build validates the current context without a separate manual Preview.</summary>
    [Fact]
    public async Task BuildStandardMergeValidatesCurrentInputsWithoutManualPreview()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-merge-gate");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        golden.CopyInputFilesToMergeSlots(viewModel, workspace, goldenCase);

        Assert.True(viewModel.Merge.PreviewMergeCommand.CanExecute(null));
        Assert.True(viewModel.Merge.CanBuildMerge);

        JsonProperty firstInput = goldenCase.GetProperty("inputs").EnumerateObject().First();
        string replacementCopyPath = workspace.PathFor($"{firstInput.Name}-copy.bin");
        File.Copy(golden.ManifestPath(firstInput.Value), replacementCopyPath);
        viewModel.SetSlotFile(StandardMergeGoldenManifest.SlotIdForAddressSpace(firstInput.Name), replacementCopyPath);

        Assert.True(viewModel.Merge.PreviewMergeCommand.CanExecute(null));
        Assert.True(viewModel.Merge.CanBuildMerge);

        viewModel.WorkflowSession.SelectedIc = "NT51927";
        await viewModel.WorkflowSession.FirmwareInspectionRefreshTask;

        Assert.True(viewModel.Merge.PreviewMergeCommand.CanExecute(null));
        Assert.True(viewModel.Merge.CanBuildMerge);

        string oversizedTpPath = workspace.PathFor("tp-input-oversized.bin");
        File.WriteAllBytes(oversizedTpPath, new byte[0x40001]);
        viewModel.SetSlotFile(StandardMergeGoldenManifest.SlotIdForAddressSpace("tp-input"), oversizedTpPath);

        string outputPath = workspace.PathFor("source-view-standard-merge.bin");
        await viewModel.Merge.BuildMergeAsync(outputPath);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.Equal("Build succeeded", viewModel.RunSession.LastRunResult.Title);
        Assert.True(File.Exists(outputPath), outputPath);
        Assert.True(viewModel.Reports.HasLoadedReport);
        Assert.False(viewModel.Reports.LoadedReport.HasPrimaryIssue);
    }

    /// <summary>Verifies NT51950 accepts a TP BIN within the 256 KiB limit even when it exceeds the declared overlay span.</summary>
    [Fact]
    public async Task PreviewNt51950AcceptsTpInputWithinMaximum()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-950-negative");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        golden.CopyInputFilesToMergeSlots(viewModel, workspace, goldenCase);

        Assert.True(viewModel.Merge.PreviewMergeCommand.CanExecute(null));
        Assert.True(viewModel.Merge.CanBuildMerge);

        await viewModel.Merge.PreviewMergeCommand.ExecuteAsync(null);

        Assert.True(viewModel.RunSession.LastRunResult.Succeeded, viewModel.RunSession.LastRunResult.Detail);
        Assert.True(viewModel.Merge.CanBuildMerge);
        Assert.True(viewModel.Reports.HasLoadedReport);
        Assert.True(viewModel.Reports.CanOpenReport);
        Assert.True(viewModel.Reports.HasReportToast);
        Assert.NotEmpty(viewModel.Reports.LoadedReport.Issues);
        Assert.All(viewModel.Reports.LoadedReport.Issues, static issue => Assert.Equal("warning", issue.Severity));
        Assert.False(viewModel.Reports.LoadedReport.HasPrimaryIssue);
        Assert.True(viewModel.Reports.LoadedReport.HasInputs);
        Assert.True(viewModel.Reports.LoadedReport.HasOperations);
    }

    /// <summary>Gets one normal DP/TP, LD-input, and DP Perspective Standard Merge golden case.</summary>
    public static TheoryData<string> StandardMergeUiGoldenCases()
    {
        TheoryData<string> cases = [];
        cases.Add("51926");
        cases.Add("51928");
        cases.Add("51950");
        return cases;
    }

    private static void WriteUiAbCmi(
        byte[] image,
        int bankStart,
        byte major,
        byte minor,
        ushort jira)
    {
        const int register16Offset = 0x401A;
        int start = checked(bankStart + register16Offset);
        image[start] = checked((byte)(jira & 0xFF));
        image[start + 1] = major;
        image[start + 2] = checked((byte)((minor << 4) | ((jira >> 8) & 0x0F)));
    }

    private static byte[] CreateUiAbTpImage(
        byte version,
        byte subVersion,
        byte commonFwMajor,
        byte commonFwMinor,
        byte commonFwAdditional,
        ushort projectId)
    {
        const int tpLength = 0x40000;
        const int backupStart = 0x1000;
        const int markerStart = backupStart + 0xFFC;
        byte[] image = new byte[tpLength];
        image[backupStart + FirmwareConfigLayout.FirmwareVersionOffset] = version;
        image[backupStart + FirmwareConfigLayout.FirmwareVersionBarOffset] = unchecked((byte)~version);
        image[backupStart + FirmwareConfigLayout.FirmwareSubVersionOffset] = subVersion;
        image[backupStart + FirmwareConfigLayout.CommonFwMajorVersionOffset] = commonFwMajor;
        image[backupStart + FirmwareConfigLayout.CommonFwMinorVersionOffset] = commonFwMinor;
        image[backupStart + FirmwareConfigLayout.CommonFwAdditionalVersionOffset] = commonFwAdditional;
        image[backupStart + FirmwareConfigLayout.ProjectIdOffset] = (byte)(projectId & 0xFF);
        image[backupStart + FirmwareConfigLayout.ProjectIdOffset + 1] = checked((byte)(projectId >> 8));
        image[markerStart] = 0x00;
        image[markerStart + 1] = (byte)'N';
        image[markerStart + 2] = (byte)'V';
        image[markerStart + 3] = (byte)'T';
        return image;
    }
}

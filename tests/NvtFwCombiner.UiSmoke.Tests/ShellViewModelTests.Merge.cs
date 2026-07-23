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
    /// <summary>Verifies Normal Merge hides IC Number while preserving row layout space.</summary>
    [Fact]
    public void NormalMergeHidesNumberSelectorButKeepsPlaceholder()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowMergeCommand.Execute(null);

        Assert.True(viewModel.IsMergeVisible);
        Assert.True(viewModel.IsDeviceContextVisible);
        Assert.True(viewModel.IsNormalMergeModeSelected);
        Assert.Equal(["Normal", "AB Code", "General"], viewModel.MergeModeChoices);
        Assert.False(viewModel.IsNumberSelectorVisible);
        Assert.True(viewModel.IsNumberSelectorPlaceholderVisible);
        Assert.Equal("NT51950: refresh profile, slots, validation", viewModel.DeviceContextStatus);

        viewModel.ShowReplaceCommand.Execute(null);

        Assert.True(viewModel.IsReplaceVisible);
        Assert.True(viewModel.IsNumberSelectorVisible);
        Assert.False(viewModel.IsNumberSelectorPlaceholderVisible);
        Assert.Equal("NT51950 / single: refresh profile, slots, validation", viewModel.DeviceContextStatus);
    }

    /// <summary>Every admitted Standard Merge profile keeps IC Number out of its authoring context.</summary>
    [Fact]
    public void EverySupportedStandardMergeHidesNumberSelector()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowMergeCommand.Execute(null);

        foreach (WorkbenchProfileSummary profile in WorkbenchCompositionService.GetStandardMergeProfileSummaries())
        {
            viewModel.SelectedIc = profile.IcId;

            Assert.True(viewModel.IsNormalMergeModeSelected, profile.IcId);
            Assert.True(viewModel.IsStandardMergeSupported, profile.IcId);
            Assert.False(viewModel.IsNumberSelectorVisible, profile.IcId);
            Assert.True(viewModel.IsNumberSelectorPlaceholderVisible, profile.IcId);
        }
    }

    /// <summary>AB Code is available for all function-open profiles and Home exposes only those ICs.</summary>
    [Fact]
    public void AbMergeExposureAndHomeContextContainAllFunctionOpenProfiles()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        Assert.Contains(WorkbenchMergeModes.AbCode, viewModel.MergeModeChoices);
        viewModel.SelectedIc = "NT51929";
        Assert.Contains(WorkbenchMergeModes.AbCode, viewModel.MergeModeChoices);
        viewModel.SelectedIc = "NT51950";
        Assert.Contains(WorkbenchMergeModes.AbCode, viewModel.MergeModeChoices);
        viewModel.SelectedIc = "NT51951";
        Assert.Contains(WorkbenchMergeModes.AbCode, viewModel.MergeModeChoices);

        viewModel.BeginAbMergeFromHomeCommand.Execute(null);

        Assert.True(viewModel.IsWorkflowContextModalOpen);
        Assert.Equal(
            ["NT51919", "NT51929", "NT51932", "NT51950", "NT51951"],
            viewModel.WorkflowContextSetup.IcChoices);
        Assert.Equal("NT51951", viewModel.WorkflowContextSetup.SelectedIc);

        viewModel.WorkflowContextSetup.SelectedIc = "NT51929";
        viewModel.ConfirmWorkflowContextCommand.Execute(null);

        Assert.True(viewModel.IsMergeVisible);
        Assert.True(viewModel.IsAbCodeMergeModeSelected);
        Assert.False(viewModel.IsNumberSelectorVisible);
        Assert.True(viewModel.IsNumberSelectorPlaceholderVisible);
        Assert.Equal("NT51929", viewModel.SelectedIc);
        Assert.Equal(
            [
                CompositionAddressSpaceIds.DpAbInput,
                CompositionAddressSpaceIds.TpAInput,
                CompositionAddressSpaceIds.TpBInput,
            ],
            viewModel.MergeSlots.Select(static slot => slot.SlotId));
    }

    /// <summary>NT51950 selects its profile-owned physical layout before inspecting or building AB inputs.</summary>
    [Fact]
    public void Nt51950AbMergeSelectsSingleOrCascadeTopology()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        viewModel.SelectedMergeMode = WorkbenchMergeModes.AbCode;

        Assert.True(viewModel.HasAbMergeTopologyChoices);
        Assert.Equal(
            ["single", "cascade"],
            viewModel.AbMergeTopologyChoices.Select(static choice => choice.Token));
        Assert.Equal("single", viewModel.SelectedAbMergeTopologyChoice?.Token);
        Assert.Equal("0x00000-0x7FFFF (len 0x80000)", viewModel.MergeMemoryRangeLabel);

        viewModel.SelectedAbMergeTopologyChoice = viewModel.AbMergeTopologyChoices.Single(
            static choice => choice.Token == "cascade");

        Assert.Equal("cascade", viewModel.SelectedAbMergeTopologyChoice?.Token);
        Assert.Equal("0x00000-0xFFFFF (len 0x100000)", viewModel.MergeMemoryRangeLabel);

        viewModel.SelectedIc = "NT51951";
        Assert.True(viewModel.IsAbMergeSupported);
        Assert.False(viewModel.HasAbMergeTopologyChoices);
        Assert.Null(viewModel.SelectedAbMergeTopologyChoice);
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
        viewModel.SelectedIc = "NT51929";
        viewModel.SelectedMergeMode = WorkbenchMergeModes.AbCode;

        Assert.False(viewModel.CanBuildMerge);
        await viewModel.SetSlotFileAsync(
            CompositionAddressSpaceIds.DpAbInput,
            dpPath,
            TestContext.Current.CancellationToken);
        await viewModel.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput,
            tpAPath,
            TestContext.Current.CancellationToken);
        await viewModel.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpBInput,
            tpBPath,
            TestContext.Current.CancellationToken);

        Assert.All(viewModel.MergeSlots, static slot =>
        {
            Assert.Equal(WorkbenchInputInspectionSeverity.Valid, slot.InputInspectionSeverity);
            Assert.False(slot.BlocksBuild);
            Assert.False(slot.IsInputInspectionPending);
        });
        FirmwareSlotViewModel dpSlot = viewModel.MergeSlots.Single(
            static slot => slot.SlotId == CompositionAddressSpaceIds.DpAbInput);
        Assert.Contains(dpSlot.FirmwareFacts, static fact => fact.Label == "DP1" && fact.Value.StartsWith("D0605", StringComparison.Ordinal));
        Assert.Contains(dpSlot.FirmwareFacts, static fact => fact.Label == "DP2" && fact.Value.StartsWith("D0708", StringComparison.Ordinal));
        FirmwareSlotViewModel tpASlot = viewModel.MergeSlots.Single(
            static slot => slot.SlotId == CompositionAddressSpaceIds.TpAInput);
        Assert.Contains(
            tpASlot.FirmwareFacts,
            static fact => fact.Label == "TPA" && fact.Value == "T81-00");
        Assert.Contains(tpASlot.FirmwareFacts, static fact => fact.Label == "Common FW" && fact.Value == "1.4.1");
        Assert.Contains(tpASlot.FirmwareFacts, static fact => fact.Label == "PID" && fact.Value == "0x5102");
        Assert.DoesNotContain(tpASlot.FirmwareFacts, static fact => fact.Label == "TP");
        FirmwareSlotViewModel tpBSlot = viewModel.MergeSlots.Single(
            static slot => slot.SlotId == CompositionAddressSpaceIds.TpBInput);
        Assert.Contains(
            tpBSlot.FirmwareFacts,
            static fact => fact.Label == "TPB" && fact.Value == "T82-03");
        Assert.Contains(tpBSlot.FirmwareFacts, static fact => fact.Label == "Common FW" && fact.Value == "2.0.0");
        Assert.Contains(tpBSlot.FirmwareFacts, static fact => fact.Label == "PID" && fact.Value == "0x6A5C");
        Assert.DoesNotContain(tpBSlot.FirmwareFacts, static fact => fact.Label == "TP");
        Assert.True(viewModel.CanBuildMerge);
        Assert.True(viewModel.PreviewMergeCommand.CanExecute(null));

        await viewModel.PreviewMergeCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.HasLoadedReport);
    }

    /// <summary>A short AB source blocks immediately while an ignored tail remains a non-blocking warning.</summary>
    [Fact]
    public async Task AbMergeLoadHealthDistinguishesBlockingAndWarning()
    {
        const int dpLength = 0x80000;
        const int tpLength = 0x40000;
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-health");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51929";
        viewModel.SelectedMergeMode = WorkbenchMergeModes.AbCode;

        await viewModel.SetSlotFileAsync(
            CompositionAddressSpaceIds.DpAbInput,
            workspace.Write("dp-short.bin", new byte[dpLength - 1]),
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel dpSlot = viewModel.MergeSlots.Single(
            static slot => slot.SlotId == CompositionAddressSpaceIds.DpAbInput);
        Assert.Equal(WorkbenchInputInspectionSeverity.Blocking, dpSlot.InputInspectionSeverity);
        Assert.True(dpSlot.BlocksBuild);
        Assert.StartsWith("Error:", dpSlot.InputInspectionStatus, StringComparison.Ordinal);

        await viewModel.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput,
            workspace.Write("tp-tail.bin", new byte[tpLength + 1]),
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel tpSlot = viewModel.MergeSlots.Single(
            static slot => slot.SlotId == CompositionAddressSpaceIds.TpAInput);
        Assert.Equal(WorkbenchInputInspectionSeverity.Warning, tpSlot.InputInspectionSeverity);
        Assert.False(tpSlot.BlocksBuild);
        Assert.Contains("warning", tpSlot.InputInspectionStatus, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.CanBuildMerge);
    }

    /// <summary>Verifies General Merge uses its own mapping editor state and hides IC Number context.</summary>
    [Fact]
    public void GeneralMergeUsesEditableMappingsAndOwnOutputLength()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.ShowMergeCommand.Execute(null);
        viewModel.SelectedMergeMode = "General";

        Assert.True(viewModel.IsGeneralMergeModeSelected);
        Assert.False(viewModel.IsNormalMergeModeSelected);
        Assert.True(viewModel.IsDeviceContextVisible);
        Assert.False(viewModel.IsNumberSelectorVisible);
        Assert.True(viewModel.IsNumberSelectorPlaceholderVisible);
        Assert.Equal("NT51950: refresh profile, slots, validation", viewModel.DeviceContextStatus);
        Assert.Equal("0x100000", viewModel.GeneralMergeOutputLength);
        Assert.Equal(
            $"NT51950_FlashCode_DxxxxTxxxx_{DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.bin",
            viewModel.MergeOutputFileName);
        _ = Assert.Single(viewModel.GeneralMergeMappings);

        viewModel.AddGeneralMergeMappingCommand.Execute(null);

        Assert.Equal(2, viewModel.GeneralMergeMappings.Count);
        viewModel.RemoveGeneralMappingRow(viewModel.GeneralMergeMappings[0]);
        GeneralMergeMappingViewModel mapping = Assert.Single(viewModel.GeneralMergeMappings);
        Assert.Equal(1, mapping.Index);
        Assert.Equal("No source BIN selected", mapping.DisplayName);
        Assert.Contains("reserved", viewModel.MergeMemorySummary, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies the Home General Merge shortcut opens Merge in General mode.</summary>
    [Fact]
    public void GeneralMergeShortcutOpensGeneralMergeMode()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.BeginGeneralMergeFromHomeCommand.Execute(null);
        viewModel.ConfirmWorkflowContextCommand.Execute(null);

        Assert.True(viewModel.IsMergeVisible);
        Assert.True(viewModel.IsGeneralMergeModeSelected);
        Assert.False(viewModel.IsNormalMergeModeSelected);
        Assert.Equal("Home > Merge", viewModel.NavigationPath);
        Assert.False(viewModel.IsNumberSelectorVisible);
        Assert.True(viewModel.IsNumberSelectorPlaceholderVisible);
    }

    /// <summary>Verifies Standard Merge slots follow the selected profile instead of exposing LD globally.</summary>
    [Fact]
    public void MergeSlotsFollowSelectedProfileRequiredInputs()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.SelectedIc = "NT51926";

        Assert.Equal(["DP BIN", "TP BIN"], viewModel.MergeSlots.Select(slot => slot.Title));
        Assert.DoesNotContain(viewModel.MergeSlots, slot => slot.Title.Contains("LD", StringComparison.Ordinal));

        viewModel.SelectedIc = "NT51928";

        Assert.Equal(["DP BIN", "TP BIN", "LD BIN"], viewModel.MergeSlots.Select(slot => slot.Title));
    }

    /// <summary>Verifies memory-map rows expose readable operation details without relying on tooltips.</summary>
    [Fact]
    public void MergeMemoryRowsExposeReadableOperationDetails()
    {
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();

        viewModel.SelectedIc = "NT51926";

        MemoryMapRowViewModel copyRow = Assert.Single(
            viewModel.MergeMemoryRows,
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
        viewModel.SelectedIc = $"NT{ic}";
        golden.CopyInputFilesToMergeSlots(viewModel, workspace, goldenCase);

        Assert.True(viewModel.PreviewMergeCommand.CanExecute(null));
        Assert.True(viewModel.BuildMergeCommand.CanExecute(null));
        Assert.True(viewModel.CanBuildMerge);

        await viewModel.PreviewMergeCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.BuildMergeCommand.CanExecute(null));
        Assert.True(viewModel.CanBuildMerge);

        string outputPath = workspace.PathFor("selected-output.bin");
        await viewModel.BuildMergeAsync(outputPath);

        string expectedPath = golden.ExpectedOutputPath(goldenCase);
        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.Equal(outputPath, viewModel.LastRunResult.Output);
        Assert.Contains("report ready", viewModel.LastRunResult.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(
            viewModel.LoadedReport.OutputSha256[..Math.Min(12, viewModel.LoadedReport.OutputSha256.Length)],
            viewModel.LastRunResult.Detail,
            StringComparison.Ordinal);
        Assert.True(File.Exists(outputPath), outputPath);
        Assert.Equal(File.ReadAllBytes(expectedPath), File.ReadAllBytes(outputPath));
        Assert.True(viewModel.HasLoadedReport);
        Assert.True(viewModel.LoadedReport.HasOutputArtifactPath);
        Assert.Equal(outputPath, viewModel.LoadedReport.OutputArtifactPath);
        Assert.Equal($"{viewModel.LoadedReport.OutputSize} bytes", viewModel.LoadedReport.OutputSizeLabel);
        Assert.Equal("Committed output", viewModel.LoadedReport.OutputCommitmentLabel);
        Assert.True(viewModel.HasReportToast);
        Assert.Equal(1, viewModel.ReportToastOpacity);
        Assert.Equal("Build report generated", viewModel.ReportToastText);
    }

    /// <summary>Verifies General Merge UI runs explicit mapping rows through Preview and Build.</summary>
    [Fact]
    public async Task GeneralMergePreviewAndBuildUseExplicitMappingRows()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-general-merge");
        string source = workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13, 0x14]);
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.SelectedMergeMode = "General";
        viewModel.GeneralMergeOutputLength = "0x10";
        GeneralMergeMappingViewModel mapping = Assert.Single(viewModel.GeneralMergeMappings);
        mapping.SourceStartAddress = "0x1";
        mapping.TargetStartAddress = "0x4";
        mapping.Length = "0x3";
        List<string> propertyChanges = [];
        viewModel.PropertyChanged += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.PropertyName))
            {
                propertyChanges.Add(args.PropertyName);
            }
        };

        viewModel.SetSlotFile(mapping.MappingId, source);

        Assert.Contains(nameof(MainWindowViewModel.MergeReadinessStatus), propertyChanges);
        Assert.Contains("maps 1 source BIN", viewModel.MergeReadinessStatus, StringComparison.Ordinal);
        Assert.True(viewModel.PreviewMergeCommand.CanExecute(null));
        Assert.True(viewModel.CanBuildMerge);
        Assert.True(viewModel.IsGeneralMergeModeSelected);
        Assert.False(viewModel.IsNormalMergeModeSelected);

        await viewModel.PreviewMergeCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.CanBuildMerge);
        Assert.True(viewModel.IsGeneralMergeModeSelected);

        string outputPath = workspace.PathFor("general-merge.bin");
        await viewModel.BuildMergeAsync(outputPath);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.Equal(outputPath, viewModel.LastRunResult.Output);
        Assert.Equal(
            [0, 0, 0, 0, 0x11, 0x12, 0x13, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            File.ReadAllBytes(outputPath));

        using var document = JsonDocument.Parse(viewModel.LoadedReportJson);
        JsonElement root = document.RootElement;
        Assert.Equal("nt51950-general-merge-logical-candidate", root.GetProperty("ProfileId").GetString());
        Assert.Equal("general-merge", root.GetProperty("ExperienceId").GetString());
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
        viewModel.SelectedIc = "NT51926";
        golden.CopyInputFilesToMergeSlots(viewModel, workspace, goldenCase);

        Assert.True(viewModel.PreviewMergeCommand.CanExecute(null));
        Assert.True(viewModel.CanBuildMerge);

        JsonProperty firstInput = goldenCase.GetProperty("inputs").EnumerateObject().First();
        string replacementCopyPath = workspace.PathFor($"{firstInput.Name}-copy.bin");
        File.Copy(golden.ManifestPath(firstInput.Value), replacementCopyPath);
        viewModel.SetSlotFile(StandardMergeGoldenManifest.SlotIdForAddressSpace(firstInput.Name), replacementCopyPath);

        Assert.True(viewModel.PreviewMergeCommand.CanExecute(null));
        Assert.True(viewModel.CanBuildMerge);

        viewModel.SelectedIc = "NT51927";
        await viewModel.FirmwareInspectionRefreshTask;

        Assert.True(viewModel.PreviewMergeCommand.CanExecute(null));
        Assert.True(viewModel.CanBuildMerge);

        string oversizedTpPath = workspace.PathFor("tp-input-oversized.bin");
        File.WriteAllBytes(oversizedTpPath, new byte[0x40001]);
        viewModel.SetSlotFile(StandardMergeGoldenManifest.SlotIdForAddressSpace("tp-input"), oversizedTpPath);

        string outputPath = workspace.PathFor("blocked-standard-merge.bin");
        await viewModel.BuildMergeAsync(outputPath);

        Assert.False(viewModel.LastRunResult.Succeeded);
        Assert.Equal("Build blocked", viewModel.LastRunResult.Title);
        Assert.False(File.Exists(outputPath), outputPath);
        Assert.True(viewModel.HasLoadedReport);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthMismatch, viewModel.LoadedReport.PrimaryIssue.Title);
        Assert.Contains("tp-input", viewModel.LoadedReport.PrimaryIssue.Detail, StringComparison.Ordinal);
    }

    /// <summary>Verifies NT51950 accepts a TP BIN within the 256 KiB limit even when it exceeds the declared overlay span.</summary>
    [Fact]
    public async Task PreviewNt51950AcceptsTpInputWithinMaximum()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-950-negative");
        MainWindowViewModel viewModel = ShellViewModelFactory.Create();
        viewModel.SelectedIc = "NT51950";
        golden.CopyInputFilesToMergeSlots(viewModel, workspace, goldenCase);

        Assert.True(viewModel.PreviewMergeCommand.CanExecute(null));
        Assert.True(viewModel.CanBuildMerge);

        await viewModel.PreviewMergeCommand.ExecuteAsync(null);

        Assert.True(viewModel.LastRunResult.Succeeded, viewModel.LastRunResult.Detail);
        Assert.True(viewModel.CanBuildMerge);
        Assert.True(viewModel.HasLoadedReport);
        Assert.True(viewModel.CanOpenReport);
        Assert.True(viewModel.HasReportToast);
        Assert.Empty(viewModel.LoadedReport.Issues);
        Assert.False(viewModel.LoadedReport.HasPrimaryIssue);
        Assert.True(viewModel.LoadedReport.HasInputs);
        Assert.True(viewModel.LoadedReport.HasOperations);
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

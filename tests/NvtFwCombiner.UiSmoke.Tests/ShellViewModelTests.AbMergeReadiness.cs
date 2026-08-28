using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class FirmwareInspectionSlotTests
{
    /// <summary>AB required inputs inspect independently in every order; only the final peer admits Build.</summary>
    [Theory]
    [InlineData("dp-ab-input,tp-a-input,tp-b-input")]
    [InlineData("dp-ab-input,tp-b-input,tp-a-input")]
    [InlineData("tp-a-input,dp-ab-input,tp-b-input")]
    [InlineData("tp-a-input,tp-b-input,dp-ab-input")]
    [InlineData("tp-b-input,dp-ab-input,tp-a-input")]
    [InlineData("tp-b-input,tp-a-input,dp-ab-input")]
    public async Task AbMergeAcceptsEveryRequiredInputOrder(string selectionOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectionOrder);
        const int dpLength = 0x80000;
        const int tpLength = 0x40000;
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-selection-order");
        byte[] dp = new byte[dpLength];
        WriteUiAbCmi(dp, 0, major: 0x06, minor: 0x05, jira: 0x123);
        WriteUiAbCmi(dp, tpLength, major: 0x07, minor: 0x08, jira: 0x456);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpAbInput] = workspace.Write("dp-ab.bin", dp),
            [CompositionAddressSpaceIds.TpAInput] = workspace.Write(
                "tp-a.bin",
                CreateUiAbTpImage(0x81, 0x00, 1, 4, 1, 0x5102)),
            [CompositionAddressSpaceIds.TpBInput] = workspace.Write(
                "tp-b.bin",
                CreateUiAbTpImage(0x82, 0x03, 2, 0, 0, 0x6A5C)),
        };
        string[] orderedSlotIds = selectionOrder.Split(',', StringSplitOptions.RemoveEmptyEntries);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

        Assert.Equal(3, orderedSlotIds.Length);
        Assert.All(viewModel.Merge.MergeSlots, static slot => Assert.True(slot.CanSelectFile));
        for (int index = 0; index < orderedSlotIds.Length; index++)
        {
            string slotId = orderedSlotIds[index];
            await viewModel.WorkflowSession.SetSlotFileAsync(
                slotId,
                paths[slotId],
                TestContext.Current.CancellationToken);

            FirmwareSlotViewModel selected = viewModel.Merge.MergeSlots.Single(slot =>
                StringComparer.Ordinal.Equals(slot.SlotId, slotId));
            Assert.Contains(
                selected.SemanticState,
                new[] { FirmwareSlotSemanticState.Verified, FirmwareSlotSemanticState.Warning });
            Assert.False(selected.BlocksBuild);
            Assert.All(
                viewModel.Merge.MergeSlots.Where(slot => !slot.HasFile),
                static slot => Assert.True(slot.CanSelectFile));
            Assert.Equal(index == orderedSlotIds.Length - 1, viewModel.Merge.CanBuildMerge);
        }
    }

    /// <summary>Verifies NT51950 accepts a TP BIN within the 256 KiB limit even when it exceeds the declared overlay span.</summary>
    [Fact]
    public async Task PreviewNt51950AcceptsTpInputWithinMaximum()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement goldenCase = golden.CaseByIc("51926");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-950-negative");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
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

    /// <summary>NT51950 publishes processor readiness before the ordinary desktop Preview command executes.</summary>
    [Fact]
    public async Task Nt51950AbMergeRefreshesRuntimeReadinessForDesktopPreview()
    {
        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase(
            "ab-merge",
            "nt51950-ab-boe-d82t80");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-runtime-readiness");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

        foreach (JsonElement artifact in goldenCase.GetProperty("artifacts")
                     .EnumerateArray()
                     .Where(static candidate =>
                         candidate.GetProperty("role").GetString() == "input"))
        {
            string slotId = artifact.GetProperty("artifactId").GetString()!;
            string path = workspace.PathFor($"{slotId}.bin");
            File.Copy(CanonicalGoldenTestData.ArtifactPath(artifact), path);
            await viewModel.WorkflowSession.SetSlotFileAsync(
                slotId,
                path,
                TestContext.Current.CancellationToken);
        }

        Assert.True(viewModel.Merge.PreviewMergeCommand.CanExecute(null));
        Assert.True(viewModel.Merge.CanBuildMerge);

        await viewModel.Merge.PreviewMergeCommand.ExecuteAsync(null);

        Assert.True(
            viewModel.RunSession.LastRunResult.Succeeded,
            viewModel.RunSession.LastRunResult.Detail);
        Assert.True(viewModel.Merge.CanBuildMerge);
    }

    /// <summary>Informational AB facts cannot replace canonical session publication.</summary>
    [Fact]
    public async Task AbMergeFactsDoNotReplaceCanonicalStatus()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-canonical-gate");
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((_, inputs) =>
        [
            .. inputs.Select(input => new FirmwareInspectionSnapshotResult(
                input.InspectionId,
                new FirmwareInspectionSnapshot(null, null, null, null, null, null)
                {
                    AbMergeFacts = new AbMergeInputFacts(
                        input.AbMergeAddressSpaceId!,
                        []),
                })),
        ]);
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

        foreach (string slotId in new[]
                 {
                     CompositionAddressSpaceIds.DpAbInput,
                     CompositionAddressSpaceIds.TpAInput,
                     CompositionAddressSpaceIds.TpBInput,
                 })
        {
            await viewModel.WorkflowSession.SetSlotFileAsync(
                slotId,
                workspace.Write($"{slotId}.bin", [0xA5]),
                TestContext.Current.CancellationToken);
        }

        Assert.False(viewModel.Merge.CanBuildMerge);
    }

    /// <summary>A rejected AB input never publishes decoded facts, including after relocalization.</summary>
    [Fact]
    public async Task AbMergeExtensionErrorNeverPublishesFactsAcrossLanguageChange()
    {
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-extension-admission");
        byte[] tp = CreateUiAbTpImage(0x81, 0x00, 1, 4, 1, 0x5102);
        string acceptedPath = workspace.Write("tp-a.bin", tp);
        string rejectedPath = workspace.Write("tp-a.txt", tp);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51929";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput,
            acceptedPath,
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel slot = viewModel.Merge.MergeSlots.Single(static candidate =>
            candidate.SlotId == CompositionAddressSpaceIds.TpAInput);
        Assert.Contains(slot.FirmwareFacts, static fact => fact.Label == "TPA" && fact.Value == "T81-00");
        Assert.Contains(slot.FirmwareFacts, static fact => fact.Label == "Common FW Version");
        Assert.Contains(slot.FirmwareFacts, static fact => fact.Label == "PID" && fact.Value == "0x5102");

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.TpAInput,
            rejectedPath,
            TestContext.Current.CancellationToken);

        Assert.Equal(FirmwareSlotSemanticState.Error, slot.SemanticState);
        FirmwareInspectionSnapshot rejected = Assert.IsType<FirmwareInspectionSnapshot>(slot.CurrentInspectionProjection);
        Assert.True(Assert.IsType<AuthoringInputSlotStatus>(rejected.InputSlotStatus).BlocksBuild);
        Assert.Null(rejected.InputSlotStatus.FileStamp);
        Assert.False(viewModel.Merge.CanBuildMerge);
        Assert.Empty(slot.FirmwareFacts);

        viewModel.SelectedLanguage = "Traditional Chinese";
        Assert.Empty(slot.FirmwareFacts);
        viewModel.SelectedLanguage = "English";
        Assert.Empty(slot.FirmwareFacts);
    }

    /// <summary>A topology transition rejects terminal health from the former exact compilation.</summary>
    [Fact]
    public async Task Nt51950TopologyChangeReinspectsSelectedAbInput()
    {
        const int singleCapacity = 0x80000;
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ab-topology-readiness");
        byte[] dp = new byte[singleCapacity];
        WriteUiAbCmi(dp, 0, major: 0x06, minor: 0x05, jira: 0x123);
        WriteUiAbCmi(dp, singleCapacity / 2, major: 0x07, minor: 0x08, jira: 0x456);
        string path = workspace.Write("single-dp-ab.bin", dp);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.ShowMergeCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.Merge.SelectedMergeMode = ExperienceIds.AbMerge;

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionAddressSpaceIds.DpAbInput,
            path,
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel slot = viewModel.Merge.MergeSlots.Single(static candidate =>
            candidate.SlotId == CompositionAddressSpaceIds.DpAbInput);
        Assert.Equal(FirmwareInputInspectionSeverity.Valid, slot.InputInspectionSeverity);

        viewModel.WorkflowSession.SelectedNumber = "cascade";
        await CurrentInspection(viewModel).ActiveTask;

        Assert.Equal(path, slot.FilePath);
        Assert.Equal(FirmwareInputInspectionSeverity.Blocking, slot.InputInspectionSeverity);
        Assert.True(slot.BlocksBuild);
        Assert.False(viewModel.Merge.CanBuildMerge);
    }

}

using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class CtrlRamWorkflowTests
{
    /// <summary>A wrong-but-readable CtrlRAM artifact becomes a typed warning without terminating the shell.</summary>
    [Fact]
    public async Task Nt51950NormalCtrlRamLoadedIntoNfSlotBecomesTypedWarningWithoutThrowing()
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51950-fw200-single-auto-prj-676-20260717");
        JsonElement baseArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("artifactId").GetString() == "tp-input");
        JsonElement normalArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("role").GetString() == "input" &&
                artifact.GetProperty("originalFileName").GetString() == "Normal_Ctrlram.bin");
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.WorkflowSession.SelectedNumber = "single";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            CanonicalGoldenTestData.ArtifactPath(baseArtifact),
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel nfSlot = viewModel.Replace.ReplaceSlots.Single(slot =>
            slot.SlotId == "replace-ctrlram-nf");

        Exception? exception = await Record.ExceptionAsync(() =>
            viewModel.WorkflowSession.SetSlotFileAsync(
                nfSlot.SlotId,
                CanonicalGoldenTestData.ArtifactPath(normalArtifact),
                TestContext.Current.CancellationToken));
        nfSlot = viewModel.Replace.ReplaceSlots.Single(slot =>
            slot.SlotId == "replace-ctrlram-nf");

        Assert.Null(exception);
        Assert.Equal(FirmwareInputInspectionSeverity.Warning, nfSlot.InputInspectionSeverity);
        Assert.True(nfSlot.HasFile);
        Assert.True(nfSlot.IsSemanticStateWarning);
        Assert.Equal(WorkflowInspectionAttemptState.Succeeded, viewModel.Replace.Inspection.State);
    }

    /// <summary>A readable rejected CtrlRAM input keeps its typed blocking diagnostic through UI adoption.</summary>
    [Fact]
    public async Task Nt51923RejectedExtensionKeepsTypedBlockingDiagnostic()
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51923-fw141-single-auto-prj-662-20260717");
        JsonElement baseArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("artifactId").GetString() == "expected-output");
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-short-input");
        string rejectedInputPath = workspace.Write("normal-ctrlram.txt", [0x00]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51923";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            CanonicalGoldenTestData.ArtifactPath(baseArtifact),
            TestContext.Current.CancellationToken);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            "replace-ctrlram-normal",
            rejectedInputPath,
            TestContext.Current.CancellationToken);

        FirmwareSlotViewModel replacement = viewModel.Replace.ReplaceSlots.Single(slot =>
            slot.SlotId == "replace-ctrlram-normal");
        Assert.Equal(WorkflowInspectionAttemptState.Failed, viewModel.Replace.Inspection.State);
        Assert.False(replacement.IsInputInspectionPending);
        Assert.Equal(FirmwareInputInspectionSeverity.Blocking, replacement.InputInspectionSeverity);
        Assert.Equal(
            InputArtifactInspectionIssueCodes.ExtensionNotAccepted,
            Assert.IsType<AuthoringInputSlotStatus>(
                replacement.CurrentInspectionProjection?.InputSlotStatus).InspectionIssueCode);
        Assert.Contains("extension", replacement.InputInspectionStatus, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(viewModel.Text.FirmwareInspectionStaleFileStatus, replacement.InputInspectionStatus);
        Assert.True(replacement.BlocksBuild);
        Assert.False(viewModel.Replace.CanBuildReplace);
    }

    /// <summary>A readable CtrlRAM base outside every declared map blocks the workflow without throwing.</summary>
    [Fact]
    public async Task Nt51950BaseWithTrailingByteDoesNotEscapeTheInspectionTask()
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51950-fw200-single-auto-prj-676-20260717");
        JsonElement baseArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("artifactId").GetString() == "tp-input");
        JsonElement nfArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("originalFileName").GetString() == "NF_Ctrlram.bin");
        byte[] validBase = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(baseArtifact));
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-invalid-capacity");
        string invalidBasePath = workspace.Write("tp-with-trailing-byte.bin", [.. validBase, 0x00]);
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.WorkflowSession.SelectedIc = "NT51950";
        viewModel.WorkflowSession.SelectedNumber = "single";
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            invalidBasePath,
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel nfSlot = viewModel.Replace.ReplaceSlots.Single(slot =>
            slot.SlotId == "replace-ctrlram-nf");

        Exception? exception = await Record.ExceptionAsync(() =>
            viewModel.WorkflowSession.SetSlotFileAsync(
                nfSlot.SlotId,
                CanonicalGoldenTestData.ArtifactPath(nfArtifact),
                TestContext.Current.CancellationToken));

        Assert.Null(exception);
        Assert.False(viewModel.Replace.CanBuildReplace);
        Assert.Equal(WorkflowInspectionAttemptState.Failed, viewModel.Replace.Inspection.State);
        FirmwareSlotViewModel baseSlot = viewModel.Replace.ReplaceBaseSlot;
        const string diagnostic = "input.address-space.length-mismatch [replace-base]: Base firmware BIN length 0x37001 is unsupported for NT51950 / single CtrlRAM Replace; accepted exact reference lengths are 0x37000 / 0x40000.";
        Assert.Equal(FirmwareInputInspectionSeverity.Blocking, baseSlot.InputInspectionSeverity);
        Assert.Equal(diagnostic, baseSlot.InputInspectionStatus);
        Assert.True(baseSlot.BlocksBuild);
        Assert.True(baseSlot.HasSemanticState);
        Assert.Equal(FirmwareSlotSemanticState.Error, baseSlot.SemanticState);
        Assert.Equal("Error", baseSlot.SemanticStateLabel);
        Assert.Equal($"Error: {diagnostic}", baseSlot.SemanticStateAutomationText);
    }

    /// <summary>A valid target-family base alone reports discovery without fabricating terminal verification.</summary>
    [Theory]
    [InlineData("nt51950-fw200-single-auto-prj-676-20260717", "NT51950")]
    [InlineData("nt51951-fw200-single-auto-prj-695-20260718", "NT51951")]
    public async Task ValidBaseOnlyIsInspectedBeforeReplacementSelection(
        string caseId,
        string icId)
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", caseId);
        JsonElement baseArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("artifactId").GetString() == "tp-input");
        MainWindowViewModel viewModel = PresentationTestHost.CreateProductViewModel();
        viewModel.WorkflowSession.SelectedIc = icId;
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.SingleChip;
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);

        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            CanonicalGoldenTestData.ArtifactPath(baseArtifact),
            TestContext.Current.CancellationToken);

        FirmwareSlotViewModel baseSlot = viewModel.Replace.ReplaceBaseSlot;
        FirmwareInspectionSnapshot inspection = Assert.IsType<FirmwareInspectionSnapshot>(
            baseSlot.CurrentInspectionProjection);
        Assert.True(baseSlot.InputInspectionSeverity is null, baseSlot.InputInspectionStatus);
        Assert.False(baseSlot.BlocksBuild);
        Assert.Equal(FirmwareSlotSemanticState.Inspected, baseSlot.SemanticState);
        Assert.Equal("Base inspected", baseSlot.SemanticStateLabel);
        Assert.Equal(
            CtrlRamBaseDiscoveryReadiness.Inspected,
            inspection.CtrlRamBaseDiscoveryReadiness);
        Assert.Equal(BaseFirmwareArtifactKind.TpFirmware, inspection.BaseFirmwareArtifactKind);
        Assert.Equal(
            CompiledFirmwareArtifactKind.TpFirmware,
            Assert.IsType<CompiledFirmwareArtifactClassification>(inspection.ArtifactClassification).Kind);
        Assert.False(inspection.ArtifactClassification.IsDpMetadataApplicable);
        Assert.Null(inspection.DpVersion);
        Assert.Null(inspection.CmiDpCode);
        Assert.DoesNotContain(baseSlot.FirmwareFacts, fact => fact.Label is "DP Version" or "Jira Index");
        Assert.Equal(WorkflowInspectionAttemptState.Succeeded, viewModel.Replace.Inspection.State);
        Assert.False(viewModel.Replace.CanBuildReplace);
        Assert.DoesNotContain(
            CompositionPlanningIssueCodes.ReplaceCtrlRamNoRegionInput,
            baseSlot.InputInspectionStatus,
            StringComparison.Ordinal);
    }

    /// <summary>A CtrlRAM batch read rejects a replacement whose file identity changes in flight.</summary>
    [Fact]
    public async Task CtrlRamInputInspectionMarksChangedFileAsBlocking()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        using var workspace = TempWorkspace.Create("nvt-fw-combiner-ui-ctrlram-inspection-identity");
        string basePath = golden.ExpectedOutputPath(golden.CaseByIc("51926"));
        string replacementPath = workspace.Write("changing-ctrlram.bin", new byte[0x1660]);
        MainWindowViewModel viewModel = CreateBatchInspectionViewModel((icId, inputs) =>
        {
            if (inputs.Any(input => StringComparer.Ordinal.Equals(
                    input.Path,
                    replacementPath)))
            {
                using var stream = new FileStream(
                    replacementPath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read);
                stream.WriteByte(0x02);
            }
            return BuiltInFirmwareInspection.InspectFirmwareBatch(
                (BuiltInFirmwareInspection)TestHost.FirmwareInspectionExperience,
                icId,
                inputs);
        });
        viewModel.WorkflowSession.SelectedIc = "NT51926";
        viewModel.WorkflowSession.SelectedNumber = IcNumberSelectionTokens.Cascade;
        OpenReplace(viewModel, ExperienceIds.CtrlRamReplace);
        await viewModel.WorkflowSession.SetSlotFileAsync(
            CompositionSlotIds.ReplaceBase,
            basePath,
            TestContext.Current.CancellationToken);
        FirmwareSlotViewModel replacement = viewModel.Replace.ReplaceSlots.First(slot =>
            !ReferenceEquals(slot, viewModel.Replace.ReplaceBaseSlot) &&
            slot.Title.Contains("VN CtrlRAM", StringComparison.Ordinal));

        await viewModel.WorkflowSession.SetSlotFileAsync(
            replacement.SlotId,
            replacementPath,
            TestContext.Current.CancellationToken);
        replacement = viewModel.Replace.ReplaceSlots.Single(slot =>
            StringComparer.Ordinal.Equals(slot.SlotId, replacement.SlotId));

        Assert.False(replacement.IsInputInspectionPending);
        Assert.Equal(
            FirmwareInputInspectionSeverity.Blocking,
            replacement.InputInspectionSeverity);
        Assert.Contains(
            "file changed",
            replacement.InputInspectionStatus,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(replacement.BlocksBuild);
        Assert.False(viewModel.Replace.CanBuildReplace);
    }
}

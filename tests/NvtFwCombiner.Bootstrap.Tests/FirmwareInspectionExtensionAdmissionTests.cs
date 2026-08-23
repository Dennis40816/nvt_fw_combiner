using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Fixed authoring routes share typed compiled extension admission.</summary>
public sealed class FirmwareInspectionExtensionAdmissionTests
{
    /// <summary>Standard Merge retains a rejected DP selection as terminal typed Error.</summary>
    [Fact]
    public void StandardMergeRejectsUnacceptedExtensionThroughSharedInspector()
    {
        IReadOnlyList<FirmwareInspectionSnapshotResult> results =
            BuiltInFirmwareInspection.InspectFirmwareBatch(
                BootstrapTestHost.Canonical,
                "NT51926",
                [
                    new FirmwareInspectionSnapshotInput(
                        "dp",
                        "standard-dp.txt",
                        StandardMergeAddressSpaceId: CompositionAddressSpaceIds.DpInput),
                    new FirmwareInspectionSnapshotInput(
                        "tp",
                        "standard-tp.bin",
                        StandardMergeAddressSpaceId: CompositionAddressSpaceIds.TpInput),
                ],
                path => new byte[path == "standard-dp.txt" ? 0x40000 : 0x35000]);

        AssertExtensionError(results, "dp", "standard-dp.txt");
    }

    /// <summary>AB Merge rejects one invalid logical input without workflow-specific validation.</summary>
    [Fact]
    public void AbMergeRejectsUnacceptedExtensionThroughSharedInspector()
    {
        IReadOnlyList<FirmwareInspectionSnapshotResult> results =
            BuiltInFirmwareInspection.InspectFirmwareBatch(
                BootstrapTestHost.Canonical,
                "NT51929",
                [
                    new FirmwareInspectionSnapshotInput(
                        CompositionAddressSpaceIds.DpAbInput,
                        "ab-dp.bin",
                        AbMergeAddressSpaceId: CompositionAddressSpaceIds.DpAbInput),
                    new FirmwareInspectionSnapshotInput(
                        CompositionAddressSpaceIds.TpAInput,
                        "ab-tp-a.txt",
                        AbMergeAddressSpaceId: CompositionAddressSpaceIds.TpAInput),
                    new FirmwareInspectionSnapshotInput(
                        CompositionAddressSpaceIds.TpBInput,
                        "ab-tp-b.bin",
                        AbMergeAddressSpaceId: CompositionAddressSpaceIds.TpBInput),
                ],
                path => new byte[path == "ab-dp.bin" ? 0x80000 : 0x40000]);

        AssertExtensionError(
            results,
            CompositionAddressSpaceIds.TpAInput,
            "ab-tp-a.txt");
    }

    /// <summary>CtrlRAM Replace applies the same admission after exact base discovery.</summary>
    [Fact]
    public void CtrlRamReplaceRejectsUnacceptedExtensionThroughSharedInspector()
    {
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51950-fw200-single-auto-prj-676-20260717");
        JsonElement baseArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("artifactId").GetString() == "tp-input");
        JsonElement replacementArtifact = fixtureCase.GetProperty("artifacts").EnumerateArray().Single(
            artifact => artifact.GetProperty("originalFileName").GetString() == "NF_Ctrlram.bin");
        byte[] baseBytes = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(baseArtifact));
        byte[] replacementBytes = File.ReadAllBytes(
            CanonicalGoldenTestData.ArtifactPath(replacementArtifact));
        const string basePath = "ctrlram-base.bin";
        const string replacementPath = "nf-ctrlram.txt";
        ReplaceInputSlot replacementSlot = BootstrapTestHost.Services.CtrlRamAuthoring
            .GetDiscoveryDisplay("NT51950", IcNumberSelectionTokens.SingleChip, basePath: null)
            .InputSlots.Single(static slot => slot.SlotId == "replace-ctrlram-nf");

        IReadOnlyList<FirmwareInspectionSnapshotResult> results =
            BuiltInFirmwareInspection.InspectFirmwareBatch(
                BootstrapTestHost.Canonical,
                "NT51950",
                [
                    new FirmwareInspectionSnapshotInput(
                        CompositionSlotIds.ReplaceBase,
                        basePath,
                        CtrlRamRequest: new CtrlRamInspectionRequest(IcNumberSelectionTokens.SingleChip),
                        CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase),
                    new FirmwareInspectionSnapshotInput(
                        replacementSlot.SlotId,
                        replacementPath,
                        CtrlRamReplaceAddressSpaceId: replacementSlot.AddressSpaceId),
                ],
                path => path == basePath ? baseBytes : replacementBytes);

        AssertExtensionError(results, replacementSlot.SlotId, replacementPath);
    }

    private static void AssertExtensionError(
        IReadOnlyList<FirmwareInspectionSnapshotResult> results,
        string inspectionId,
        string selectedPath)
    {
        AuthoringInputSlotStatus status = Assert.IsType<AuthoringInputSlotStatus>(
            results.Single(result => result.InspectionId == inspectionId)
                .Inspection.InputSlotStatus);
        Assert.Equal(AuthoringSlotLifecycle.Error, status.InspectionLifecycle);
        Assert.Equal(InputArtifactInspectionIssueCodes.ExtensionNotAccepted, status.InspectionIssueCode);
        Assert.Equal(selectedPath, status.SelectedPathHint);
        Assert.True(status.BlocksBuild);
        Assert.Null(status.FileStamp);
        Assert.Null(status.AcceptedBytes);
    }
}

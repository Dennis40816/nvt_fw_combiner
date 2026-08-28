using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class HeadlessInputSlotInspectionContractTests
{
    /// <summary>Base-only CtrlRAM discovery is typed and never becomes terminal slot health.</summary>
    [Theory]
    [InlineData(false, false, CtrlRamBaseDiscoveryReadiness.Inspected)]
    [InlineData(true, false, CtrlRamBaseDiscoveryReadiness.NotApplicable)]
    [InlineData(false, true, CtrlRamBaseDiscoveryReadiness.NotApplicable)]
    public void CtrlRamBaseDiscoveryIsTypedAndFailsClosedForInvalidOrUnreadableBase(
        bool appendTrailingByte,
        bool unreadable,
        CtrlRamBaseDiscoveryReadiness expected)
    {
        ReloadCatalog();
        JsonElement fixtureCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51950-fw200-single-auto-prj-676-20260717");
        JsonElement baseArtifact = CanonicalGoldenTestData.Artifact(fixtureCase, "tp-input");
        byte[] baseBytes = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(baseArtifact));
        if (appendTrailingByte)
        {
            baseBytes = [.. baseBytes, 0x00];
        }

        FirmwareInspectionSnapshot inspection = Assert.Single(
            BuiltInFirmwareInspection.InspectFirmwareBatch(
                _host.Canonical,
                "NT51950",
                [new FirmwareInspectionSnapshotInput(
                    "base",
                    "base.bin",
                    CtrlRamRequest: new CtrlRamInspectionRequest(IcNumberSelectionTokens.SingleChip),
                    CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase)],
                _ => unreadable ? null : baseBytes)).Inspection;

        Assert.Equal(expected, inspection.CtrlRamBaseDiscoveryReadiness);
        Assert.Null(inspection.InputSlotStatus);
        Assert.Null(inspection.InputSlotCatalog);
        if (appendTrailingByte || unreadable)
        {
            Assert.NotEmpty(inspection.AuthoringCompilationIssues);
        }
        else
        {
            Assert.Empty(inspection.AuthoringCompilationIssues);
        }
    }
}

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeRuntimeAdmissionTests
{
    /// <summary>Load inspection projects independent DP1/DP2 and TPA/TPB values without routing on them.</summary>
    [Theory]
    [InlineData("NT51919")]
    [InlineData("NT51929")]
    [InlineData("NT51932")]
    public void WorkbenchLoadInspectionProjectsHealthAndFourVersionValues(string icId)
    {
        using var workspace = TempWorkspace.Create("nfc-ab-load-inspection");
        byte[] dpAb = new byte[DpLength];
        WriteCmi(dpAb, bankStart: 0, major: 0x06, minor: 0x05, jira: 0x123);
        WriteCmi(dpAb, bankStart: TpLength, major: 0x07, minor: 0x08, jira: 0x456);
        byte[] tpA = CreateTpImage(version: 0x81, subVersion: 0x00);
        byte[] tpB = CreateTpImage(version: 0x82, subVersion: 0x03);

        WorkbenchFirmwareInspection dp = InspectAbInput(
            icId,
            CompositionAddressSpaceIds.DpAbInput,
            workspace.Write("dp-ab.bin", dpAb));
        WorkbenchFirmwareInspection a = InspectAbInput(
            icId,
            CompositionAddressSpaceIds.TpAInput,
            workspace.Write("tp-a.bin", tpA));
        WorkbenchFirmwareInspection b = InspectAbInput(
            icId,
            CompositionAddressSpaceIds.TpBInput,
            workspace.Write("tp-b.bin", tpB));

        Assert.Equal(AuthoringSlotLifecycle.Verified, dp.InputSlotStatus!.InspectionLifecycle);
        Assert.Equal(
            [
                new CompiledInputVersionObservation(CompiledInputVersionKind.DpA, 0x06, 0x05, 0x123),
                new CompiledInputVersionObservation(CompiledInputVersionKind.DpB, 0x07, 0x08, 0x456),
            ],
            dp.AbMergeFacts!.Versions);
        Assert.Equal(
            new CompiledInputVersionObservation(CompiledInputVersionKind.TpA, 0x81, 0x00),
            Assert.Single(a.AbMergeFacts!.Versions));
        Assert.Equal(
            new CompiledInputVersionObservation(CompiledInputVersionKind.TpB, 0x82, 0x03),
            Assert.Single(b.AbMergeFacts!.Versions));
        Assert.False(dp.InputSlotStatus.BlocksBuild);
        Assert.False(a.InputSlotStatus!.BlocksBuild);
        Assert.False(b.InputSlotStatus!.BlocksBuild);
    }

    /// <summary>Accepted unreadable TP version metadata publishes canonical Warning without blocking Build.</summary>
    [Fact]
    public void WorkbenchLoadInspectionPublishesUnknownVersionWarning()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-load-unknown-version");

        WorkbenchFirmwareInspection inspection = InspectAbInput(
            "NT51929",
            CompositionAddressSpaceIds.TpAInput,
            workspace.Write("tp-a-unknown.bin", new byte[TpLength]));

        Assert.Equal(AuthoringSlotLifecycle.Warning, inspection.InputSlotStatus!.InspectionLifecycle);
        Assert.Equal(
            InputArtifactInspectionIssueCodes.AbVersionMetadataUnknown,
            inspection.InputSlotStatus.InspectionIssueCode);
        Assert.Equal(
            CompiledInputArtifactInspectionNextAction.ReviewUnknownVersion,
            inspection.InputSlotStatus.InspectionNextAction);
        Assert.False(inspection.InputSlotStatus.BlocksBuild);
        Assert.False(Assert.Single(inspection.AbMergeFacts!.Versions).IsKnown);
    }

    /// <summary>NT51950 Cascade projects DP versions from the compiled map CMI regions.</summary>
    [Fact]
    public void Nt51950CascadeLoadInspectionUsesCompiledCmiRegions()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-cascade-load-inspection");
        byte[] dpAb = new byte[0x100000];
        WriteCmiAt(dpAb, 0x5016, major: 0x82, minor: 0x03, jira: 0x123);
        WriteCmiAt(dpAb, 0x45016, major: 0x83, minor: 0x04, jira: 0x456);

        WorkbenchFirmwareInspection inspection = InspectAbInput(
            "NT51950",
            CompositionAddressSpaceIds.DpAbInput,
            workspace.Write("dp-ab-cascade.bin", dpAb),
            "cascade");

        Assert.Equal(AuthoringSlotLifecycle.Verified, inspection.InputSlotStatus!.InspectionLifecycle);
        Assert.Equal(
            [
                new CompiledInputVersionObservation(CompiledInputVersionKind.DpA, 0x82, 0x03, 0x123),
                new CompiledInputVersionObservation(CompiledInputVersionKind.DpB, 0x83, 0x04, 0x456),
            ],
            inspection.AbMergeFacts!.Versions);
    }

    /// <summary>Metadata stays bounded to the canonical accepted source view.</summary>
    [Fact]
    public void WorkbenchLoadInspectionBoundsMetadataToAcceptedPrefix()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-load-tail");
        byte[] exact = CreateTpImage(version: 0x81, subVersion: 0x02);
        byte[] oversized = [.. exact, .. CreateTpImage(version: 0x99, subVersion: 0x09)];

        WorkbenchFirmwareInspection inspection = InspectAbInput(
            "NT51929",
            CompositionAddressSpaceIds.TpAInput,
            workspace.Write("tp-a-oversized.bin", oversized));

        Assert.Equal(AuthoringSlotLifecycle.Verified, inspection.InputSlotStatus!.InspectionLifecycle);
        Assert.Equal("input.inspection.ready", inspection.InputSlotStatus.InspectionIssueCode);
        Assert.False(inspection.InputSlotStatus.BlocksBuild);
        Assert.Equal(TpLength, inspection.InputSlotStatus.Inspection!.IgnoredTrailingBytes);
        Assert.Equal(
            new CompiledInputVersionObservation(CompiledInputVersionKind.TpA, 0x81, 0x02),
            Assert.Single(inspection.AbMergeFacts!.Versions));
    }

    /// <summary>A short source blocks and keeps informational version facts explicitly Unknown.</summary>
    [Fact]
    public void WorkbenchLoadInspectionBlocksShortSource()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-load-short");
        WorkbenchFirmwareInspection inspection = InspectAbInput(
            "NT51929",
            CompositionAddressSpaceIds.DpAbInput,
            workspace.Write("dp-ab-short.bin", new byte[DpLength - 1]));

        Assert.True(inspection.InputSlotStatus!.BlocksBuild);
        Assert.Equal(AuthoringSlotLifecycle.Error, inspection.InputSlotStatus.InspectionLifecycle);
        Assert.Equal(
            CompositionIssueCodes.InputAddressSpaceLengthMismatch,
            inspection.InputSlotStatus.InspectionIssueCode);
        Assert.All(inspection.AbMergeFacts!.Versions, static version => Assert.False(version.IsKnown));
        Assert.DoesNotContain(
            inspection.InputSlotStatus.InspectionAdvisories,
            static advisory => advisory.IssueCode ==
                InputArtifactInspectionIssueCodes.AbVersionMetadataUnknown);
    }
}

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeRuntimeAdmissionTests
{
    /// <summary>Load inspection projects independent DP1/DP2 and TPA/TPB values without routing on them.</summary>
    [Theory]
    [InlineData("NT51919")]
    [InlineData("NT51929")]
    [InlineData("NT51932")]
    public async Task WorkbenchLoadInspectionProjectsHealthAndFourVersionValues(string icId)
    {
        using var workspace = TempWorkspace.Create("nfc-ab-load-inspection");
        byte[] dpAb = new byte[DpLength];
        WriteCmi(dpAb, bankStart: 0, major: 0x06, minor: 0x05, jira: 0x123);
        WriteCmi(dpAb, bankStart: TpLength, major: 0x07, minor: 0x08, jira: 0x456);
        byte[] tpA = CreateTpImage(version: 0x81, subVersion: 0x00);
        byte[] tpB = CreateTpImage(version: 0x82, subVersion: 0x03);

        FirmwareInspectionSnapshot dp = await InspectAbInputAsync(
            icId,
            CompositionAddressSpaceIds.DpAbInput,
            workspace.Write("dp-ab.bin", dpAb));
        FirmwareInspectionSnapshot a = await InspectAbInputAsync(
            icId,
            CompositionAddressSpaceIds.TpAInput,
            workspace.Write("tp-a.bin", tpA));
        FirmwareInspectionSnapshot b = await InspectAbInputAsync(
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
        Assert.Null(dp.DpVersion);
        Assert.Null(dp.CmiDpCode);
        Assert.False(dp.InputSlotStatus.BlocksBuild);
        Assert.False(a.InputSlotStatus!.BlocksBuild);
        Assert.False(b.InputSlotStatus!.BlocksBuild);
    }

    /// <summary>Even a lone TP slot blocks when canonical count cannot be read.</summary>
    [Fact]
    public async Task WorkbenchLoadInspectionBlocksUnreadableTpCount()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-load-unknown-version");

        FirmwareInspectionSnapshot inspection = await InspectAbInputAsync(
            "NT51929",
            CompositionAddressSpaceIds.TpAInput,
            workspace.Write("tp-a-unknown.bin", new byte[TpLength]));

        Assert.Equal(AuthoringSlotLifecycle.Error, inspection.InputSlotStatus!.InspectionLifecycle);
        Assert.Equal(
            "firmware-config.chip-count-unreadable",
            inspection.InputSlotStatus.InspectionIssueCode);
        Assert.Equal(
            CompiledInputArtifactInspectionNextAction.SelectCompatibleInput,
            inspection.InputSlotStatus.InspectionNextAction);
        Assert.True(inspection.InputSlotStatus.BlocksBuild);
        Assert.True(inspection.InputSlotStatus.AcceptedBytes.GetValueOrDefault().IsEmpty);
    }

    /// <summary>NT51950 Cascade projects DP versions from the compiled map CMI regions.</summary>
    [Fact]
    public async Task Nt51950CascadeLoadInspectionUsesCompiledCmiRegions()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51950-cascade-load-inspection");
        byte[] dpAb = new byte[0x100000];
        WriteCmiAt(dpAb, 0x5016, major: 0x82, minor: 0x03, jira: 0x123);
        WriteCmiAt(dpAb, 0x45016, major: 0x83, minor: 0x04, jira: 0x456);

        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        byte[] tp = CreateTpImage(0x81, 0, chipCount: 3, length: 0x37000);
        tp[0x22200] = 0x31;
        tp[0x22201] = 0xCE;
        tp[0x2220C] = 0x84;
        FirmwareInspectionBatchResult batch = await host.FirmwareInspectionExperience.InspectFirmwareBatchAsync("NT51950",
            [new("dp-ab-input", workspace.Write("dp-ab-cascade.bin", dpAb), AbMergeAddressSpaceId: "dp-ab-input", AbMergeTopologyToken: "cascade"),
             new("tp-a-input", workspace.Write("a.bin", tp), AbMergeAddressSpaceId: "tp-a-input", AbMergeTopologyToken: "cascade"),
             new("tp-b-input", workspace.Write("b.bin", tp), AbMergeAddressSpaceId: "tp-b-input", AbMergeTopologyToken: "cascade")],
            TestContext.Current.CancellationToken);
        FirmwareInspectionSnapshot inspection = batch.InspectionsById["dp-ab-input"];

        Assert.Equal(AuthoringSlotLifecycle.Verified, inspection.InputSlotStatus!.InspectionLifecycle);
        Assert.Equal(
            [
                new CompiledInputVersionObservation(CompiledInputVersionKind.DpA, 0x82, 0x03, 0x123),
                new CompiledInputVersionObservation(CompiledInputVersionKind.DpB, 0x83, 0x04, 0x456),
            ],
            inspection.AbMergeFacts!.Versions);
        Assert.Null(inspection.DpVersion);
        Assert.Null(inspection.CmiDpCode);
    }

    /// <summary>Metadata stays bounded to the canonical accepted source view.</summary>
    [Fact]
    public async Task WorkbenchLoadInspectionBoundsMetadataToAcceptedPrefix()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-load-tail");
        byte[] exact = CreateTpImage(version: 0x81, subVersion: 0x02);
        byte[] oversized = [.. exact, .. CreateTpImage(version: 0x99, subVersion: 0x09)];

        FirmwareInspectionSnapshot inspection = await InspectAbInputAsync(
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
    public async Task WorkbenchLoadInspectionBlocksShortSource()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-load-short");
        FirmwareInspectionSnapshot inspection = await InspectAbInputAsync(
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

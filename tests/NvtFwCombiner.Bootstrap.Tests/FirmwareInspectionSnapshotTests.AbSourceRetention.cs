using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class FirmwareInspectionSnapshotTests
{
    /// <summary>AB TP inputs retain a common read-only Event Buffer byte when no AB format policy exists.</summary>
    [Fact]
    public async Task Nt51929AbTouchInputsExposeCommonEventBufferWithoutFormatAdmission()
    {
        using var workspace = TempWorkspace.Create("ab-common-event-display");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        JsonElement golden = CanonicalGoldenTestData.LoadDirectCase("ab-merge", "nt51929-ab-t05-d06");
        string PathFor(string id)
        {
            return CanonicalGoldenTestData.ArtifactPath(
                golden.GetProperty("artifacts").EnumerateArray().Single(artifact =>
                    artifact.GetProperty("artifactId").GetString() == id));
        }

        FirmwareInspectionBatchResult batch = await host.FirmwareInspectionExperience.InspectFirmwareBatchAsync(
            "NT51929",
            [Input(CompositionAddressSpaceIds.DpAbInput),
                Input(CompositionAddressSpaceIds.TpAInput),
                Input(CompositionAddressSpaceIds.TpBInput)],
            TestContext.Current.CancellationToken);

        foreach (string slot in new[] { CompositionAddressSpaceIds.TpAInput,
                     CompositionAddressSpaceIds.TpBInput })
        {
            FirmwareInspectionSnapshot inspection = batch.InspectionsById[slot];
            Assert.Null(inspection.AbMergeFacts?.EventBufferFormat);
            _ = Assert.NotNull(inspection.AbCommonEventBufferFormatVersion);
            Assert.False(Assert.IsType<AuthoringInputSlotStatus>(inspection.InputSlotStatus).BlocksBuild);
        }
        Assert.Null(batch.InspectionsById[CompositionAddressSpaceIds.DpAbInput]
            .AbCommonEventBufferFormatVersion);

        FirmwareInspectionSnapshot tpA = batch.InspectionsById[CompositionAddressSpaceIds.TpAInput];
        byte[] acceptedTp = File.ReadAllBytes(PathFor(CompositionAddressSpaceIds.TpAInput));
        var canonical = new CanonicalTestContext(host);
        var resolver = new FirmwareArtifactClassificationResolver(canonical.Catalog, canonical.Compiler);
        long structureStart = Assert.IsType<FirmwareConfigMetadataSnapshot>(tpA.FirmwareConfig)
            .FirmwareConfigBackupStart;
        ResolutionToken current = Assert.IsType<AuthoringInputSlotStatus>(tpA.InputSlotStatus)
            .ResolutionToken;
        Assert.Equal(tpA.AbCommonEventBufferFormatVersion,
            resolver.ReadCommonEventBufferFormatForTp("NT51929", current, acceptedTp, structureStart));
        Assert.Null(resolver.ReadCommonEventBufferFormatForTp("NT51929",
            new ResolutionToken("stale-publication"), acceptedTp, structureStart));
        Assert.Null(resolver.ReadCommonEventBufferFormatForTp("NT51929", current,
            acceptedTp, structureStart + 1));
        Assert.Null(resolver.ReadCommonEventBufferFormatForTp("NT51929", current,
            acceptedTp.AsMemory(0, 16), structureStart));

        FirmwareInspectionSnapshotInput Input(string slot)
        {
            return new(slot, PathFor(slot),
                AbMergeAddressSpaceId: slot);
        }
    }

    /// <summary>Format discovery reads the complete nonstandard DP once and retains its accepted extent.</summary>
    [Fact]
    public async Task AbFormatDiscoveryReadsFullDpOnceAndAcceptsNonstandardExtent()
    {
        using var workspace = TempWorkspace.Create("ab-format-full-dp-inspection");
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, configurationPath: workspace.PathFor("format.json"));
        Assert.True(host.Catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        IEventBufferFormatConfigurationSession configuration = await host.GetEventBufferFormatConfigurationAsync(
            TestContext.Current.CancellationToken);
        Assert.True((await configuration.SaveAsync(configuration.CreateDefaultsDraft(),
            TestContext.Current.CancellationToken)).Succeeded);
        var canonical = new CanonicalTestContext(host);
        ResolvedCapabilityRoute previousRoute = Assert.Single(host.Catalog.GetCurrentSnapshot().DynamicRoutes,
            static route => route.Identity.IcId == "NT51950" &&
                route.Identity.WorkflowId == ExperienceIds.AbMerge &&
                route.Identity.MapVariant == "nt51950-ab-merge-maps" && route.Identity.IcCountVariant == "1-ic");
        Assert.True(canonical.Compiler.TryCompilePublishedDynamicCapability(previousRoute.Identity, null, null,
            out _, out ResolvedCapability? previousCapability, out IReadOnlyList<CompositionIssue> issues,
            previousRoute.AbMergeTopologyChoice!.Selection));
        Assert.Empty(issues);
        Assert.NotNull(previousCapability);

        byte[] dp = new byte[0x100000];
        dp[0] = 0x15;
        dp[0x80000] = 0xA6;
        dp[^1] = 0x97;
        Assert.True(dp.Length > CompiledInputArtifactInspectionService.ResolveMaximumContentReadBytes(
            previousCapability.CompiledComposition, CompositionAddressSpaceIds.DpAbInput));
        byte[] tp = new byte[0x37000];
        tp[0x22200] = 0x31;
        tp[0x22201] = 0xCE;
        tp[0x2220C] = 0x97;
        tp[0x36000] = 0x42;
        tp[0x36001] = 0xBD;
        tp[0x36017] = 1;
        tp[0x36FFD] = (byte)'N';
        tp[0x36FFE] = (byte)'V';
        tp[0x36FFF] = (byte)'T';
        string dpPath = workspace.Write("dp.bin", dp);
        string tpPath = workspace.Write("tp.bin", tp);
        var reads = new Dictionary<string, int>(StringComparer.Ordinal);
        var files = new FileContentSnapshotInspector([workspace.Root]);
        var inspection = new BuiltInFirmwareInspection(
            new FirmwareMetadataPlanAuthorityResolver(canonical.Catalog, canonical.Compiler), canonical.Projection,
            (StandardMergeAuthoringExperience)host.StandardMergeAuthoring,
            (AbMergeAuthoringExperience)host.AbMergeAuthoring,
            (CtrlRamAuthoringExperience)host.CtrlRamAuthoring,
            new FirmwareArtifactClassificationResolver(canonical.Catalog, host.Compiler),
            new DelegatingContentInspector((path, maximum, token) =>
            {
                reads[path] = reads.GetValueOrDefault(path) + 1;
                return files.InspectAsync(path, maximum, token);
            }));
        FirmwareInspectionBatchResult result = await inspection.InspectFirmwareBatchAsync("NT51950",
            [Input("dp-ab-input", dpPath), Input("tp-a-input", tpPath), Input("tp-b-input", tpPath)],
            TestContext.Current.CancellationToken);

        AuthoringInputSlotStatus status = Assert.IsType<AuthoringInputSlotStatus>(
            result.InspectionsById["dp-ab-input"].InputSlotStatus);
        Assert.Equal(dp, status.AcceptedBytes!.Value.ToArray());
        Assert.False(status.BlocksBuild);
        Assert.Equal("DP_NONSTANDARD_SIZE_WARNING", status.InspectionIssueCode);
        Assert.Equal(FileStamp.FromBytes(dp), result.FileStamps[dpPath]);
        ResolvedCapability selected = Assert.IsType<ResolvedCapability>(Assert.Single(
            result.InspectionsById["dp-ab-input"].InputSlotCatalog!.Routes).ExactCapability);
        Assert.Equal("nt51950-ab-merge-maps", selected.Identity.MapVariant);
        Assert.Equal(dp.Length, selected.CompiledComposition.Plan.OutputInitialization.Capacity);
        Assert.Equal(2, reads.Count);
        Assert.All(reads.Values, static count => Assert.Equal(1, count));
        Assert.Null(result.InspectionsById["tp-a-input"].AbCommonEventBufferFormatVersion);
        Assert.Null(result.InspectionsById["tp-b-input"].AbCommonEventBufferFormatVersion);

        FirmwareInspectionSnapshotInput Input(string slot, string path)
        {
            return new(slot, path, AbMergeAddressSpaceId: slot,
                AbMergeTopologyToken: "single", ExactCapability: previousCapability);
        }
    }
}

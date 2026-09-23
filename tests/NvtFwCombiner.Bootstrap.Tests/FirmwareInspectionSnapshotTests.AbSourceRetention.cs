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

        FirmwareInspectionSnapshotInput Input(string slot, string path)
        {
            return new(slot, path, AbMergeAddressSpaceId: slot,
                AbMergeTopologyToken: "single", ExactCapability: previousCapability);
        }
    }
}

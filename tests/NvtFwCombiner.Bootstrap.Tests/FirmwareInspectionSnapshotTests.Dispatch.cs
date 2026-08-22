using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class FirmwareInspectionSnapshotTests
{
    /// <summary>Only explicit typed workflow roles admit their inspection strategies.</summary>
    [Fact]
    public void InspectionDispatchUsesOnlyTypedApplicableStrategies()
    {
        FirmwareInspectionSnapshotResult result = Assert.Single(
            BuiltInFirmwareInspection.InspectFirmwareBatch(
                BootstrapTestHost.Canonical,
                "NT51926",
                [new FirmwareInspectionSnapshotInput("generic", "NT51926-dp-flash.bin")],
                static _ => new byte[0x40000]));

        Assert.Null(result.Inspection.InputSlotStatus);
        Assert.Null(result.Inspection.InputSlotCatalog);
        Assert.Null(result.Inspection.AbMergeFacts);
    }

    /// <summary>Selective dispatch preserves every workflow projection and distinct-path read count.</summary>
    [Fact]
    public void SelectiveDispatchMatchesAllStrategyBaselineForEveryWorkflow()
    {
        foreach ((string icId, FirmwareInspectionSnapshotInput[] inputs, Dictionary<string, byte[]> images)
                 in CreateWorkflowDispatchCases())
        {
            var selectiveReads = new Dictionary<string, int>(StringComparer.Ordinal);
            IReadOnlyList<FirmwareInspectionSnapshotResult> selective =
                BuiltInFirmwareInspection.InspectFirmwareBatch(
                    BootstrapTestHost.Canonical,
                    icId,
                    inputs,
                    path => ReadOnce(path, images, selectiveReads));

            var baselineReads = new Dictionary<string, int>(StringComparer.Ordinal);
            IReadOnlyList<FirmwareInspectionSnapshotResult> baseline =
                BuiltInFirmwareInspection.InspectFirmwareBatch(
                    BootstrapTestHost.Canonical,
                    icId,
                    inputs,
                    path => ReadOnce(path, images, baselineReads),
                    FirmwareInspectionDispatch.AllStrategiesBaseline);

            Assert.Equivalent(baseline, selective, strict: true);
            Assert.All(selectiveReads.Values, static count => Assert.Equal(1, count));
            Assert.Equal(images.Keys.Order(StringComparer.Ordinal), selectiveReads.Keys.Order(StringComparer.Ordinal));
            Assert.Equal(selectiveReads, baselineReads);
        }
    }

    /// <summary>Artifact classification follows the current publication instead of an IC-only cache.</summary>
    [Fact]
    public async Task ArtifactClassificationUsesTheCurrentCatalogPublication()
    {
        CapabilityCatalogLoadResult seedLoad =
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource().Load(
                TestContext.Current.CancellationToken);
        CanonicalCapabilityCatalogCandidate seed = seedLoad.Candidate!;
        var withoutNt51926StandardMerge = new CanonicalCapabilityCatalogCandidate(
            seed.CatalogId,
            "inspection-cache-test-2",
            seed.SourceSha256,
            [
                .. seed.Definitions.Where(static definition =>
                    !string.Equals(
                        definition.Identity.IcId,
                        "NT51926",
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        definition.Identity.WorkflowId,
                        ExperienceIds.StandardMerge,
                        StringComparison.Ordinal)),
            ],
            seed.DynamicDefinitions);
        var catalog = new CanonicalCapabilityCatalog(
            new QueuedCandidateSource(seed, withoutNt51926StandardMerge));
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        ResolvedCapability firstPublication = catalog.GetCurrentSnapshot().Capabilities.Single(
            static capability =>
                StringComparer.Ordinal.Equals(capability.Identity.IcId, "NT51926") &&
                StringComparer.Ordinal.Equals(
                    capability.Identity.WorkflowId,
                    ExperienceIds.StandardMerge));
        byte[] image = new byte[0x40000];
        BuiltInFirmwareInspection inspection = CreateInspection(
            catalog,
            new DelegatingContentInspector((path, _, _) =>
                ValueTask.FromResult(new SelectedFileContentInspection(
                    FileStamp.FromBytes(image),
                    Path.GetFileName(path),
                    acceptedBytes: image))));

        FirmwareInspectionSnapshot first = (await inspection.InspectFirmwareBatchAsync(
            "NT51926",
            [new FirmwareInspectionSnapshotInput(
                "base",
                "base.bin",
                ExactCapability: firstPublication)],
            TestContext.Current.CancellationToken)).InspectionsById["base"];
        Assert.NotNull(first.ArtifactClassification);

        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        FirmwareInspectionSnapshot second = (await inspection.InspectFirmwareBatchAsync(
            "NT51926",
            [new FirmwareInspectionSnapshotInput(
                "base",
                "base.bin",
                ExactCapability: firstPublication)],
            TestContext.Current.CancellationToken)).InspectionsById["base"];

        Assert.Null(second.ArtifactClassification);
        Assert.Equal(BaseFirmwareArtifactKind.Unknown, second.BaseFirmwareArtifactKind);
    }

    private static byte[] ReadOnce(
        string path,
        Dictionary<string, byte[]> images,
        Dictionary<string, int> reads)
    {
        reads[path] = reads.GetValueOrDefault(path) + 1;
        return images[path];
    }

    private static IEnumerable<(
        string IcId,
        FirmwareInspectionSnapshotInput[] Inputs,
        Dictionary<string, byte[]> Images)> CreateWorkflowDispatchCases()
    {
        yield return (
            "NT51926",
            [
                new("standard-dp", "standard-dp.bin", StandardMergeAddressSpaceId: CompositionAddressSpaceIds.DpInput),
                new("standard-tp", "standard-tp.bin", StandardMergeAddressSpaceId: CompositionAddressSpaceIds.TpInput),
            ],
            Images(("standard-dp.bin", 0x40000), ("standard-tp.bin", 0x40000)));
        yield return (
            "NT51929",
            [
                new("ab-dp", "ab-dp.bin", AbMergeAddressSpaceId: CompositionAddressSpaceIds.DpAbInput),
                new("ab-tp-a", "ab-tp-a.bin", AbMergeAddressSpaceId: CompositionAddressSpaceIds.TpAInput),
                new("ab-tp-b", "ab-tp-b.bin", AbMergeAddressSpaceId: CompositionAddressSpaceIds.TpBInput),
            ],
            Images(("ab-dp.bin", 0x80000), ("ab-tp-a.bin", 0x40000), ("ab-tp-b.bin", 0x40000)));
        yield return (
            "NT51928",
            [
                new("dp-base", "dp-base.bin", DpReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase),
                new("dp-replacement", "dp-replacement.bin", DpReplaceAddressSpaceId: CompositionAddressSpaceIds.InitialCodeReplacement),
            ],
            Images(("dp-base.bin", 0x40000), ("dp-replacement.bin", 0x40000)));
        yield return (
            "NT51926",
            [new(
                "ctrlram-base",
                "ctrlram-base.bin",
                CtrlRamRequest: new CtrlRamInspectionRequest(IcNumberSelectionTokens.Cascade),
                CtrlRamReplaceAddressSpaceId: CompositionAddressSpaceIds.ReferenceBase)],
            Images(("ctrlram-base.bin", 0x40000)));
        yield return (
            "NT51926",
            [new("general-merge", "general-merge.bin")],
            Images(("general-merge.bin", 0x40000)));
        yield return (
            "NT51926",
            [new("general-replace", "general-replace.bin")],
            Images(("general-replace.bin", 0x40000)));
    }

    private static Dictionary<string, byte[]> Images(params (string Path, int Length)[] definitions)
    {
        return definitions.ToDictionary(
            static definition => definition.Path,
            static definition => new byte[definition.Length],
            StringComparer.Ordinal);
    }

    private static BuiltInFirmwareInspection CreateInspection(
        ICanonicalCapabilityQuery catalog,
        ISelectedFileContentInspector contentInspector)
    {
        CompositionHostServices services = BootstrapTestHost.Services;
        return new BuiltInFirmwareInspection(
            catalog,
            BootstrapTestHost.Canonical.Projection,
            (StandardMergeAuthoringExperience)services.StandardMergeAuthoring,
            (AbMergeAuthoringExperience)services.AbMergeAuthoring,
            (DpReplaceAuthoringExperience)services.DpReplaceAuthoring,
            (CtrlRamAuthoringExperience)services.CtrlRamAuthoring,
            contentInspector);
    }

    private sealed class QueuedCandidateSource(
        params CanonicalCapabilityCatalogCandidate[] candidates)
        : ICanonicalCapabilityCatalogSource
    {
        private readonly Queue<CanonicalCapabilityCatalogCandidate> _candidates = new(candidates);

        public CapabilityCatalogLoadResult Load(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CapabilityCatalogLoadResult.Success(_candidates.Dequeue());
        }
    }
}

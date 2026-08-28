using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

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

    /// <summary>
    /// Application classification resolves exact multi-capacity containers,
    /// requires every TP-prefix candidate to agree, and fails closed otherwise.
    /// </summary>
    [Fact]
    public void Nt51928ArtifactClassificationIsCapacityExactAndFailsClosed()
    {
        CompositionHostServices classificationHost = CompositionHostServices.Create();
        var resolver = new FirmwareArtifactClassificationResolver(
            classificationHost.Catalog,
            classificationHost.Compiler);
        foreach (int capacity in new[] { 0x40000, 0x80000 })
        {
            CompiledFirmwareArtifactClassification classification = Assert.IsType<
                CompiledFirmwareArtifactClassification>(
                    resolver.Resolve(
                        "NT51928",
                        exactCapability: null,
                        CreateNonUniformArtifact(capacity)));
            Assert.Equal(CompiledFirmwareArtifactKind.FlashCode, classification.Kind);
        }

        CompiledFirmwareArtifactClassification tpClassification = Assert.IsType<
            CompiledFirmwareArtifactClassification>(
                resolver.Resolve(
                    "NT51928",
                    exactCapability: null,
                    CreateNonUniformArtifact(0x35000)));
        Assert.Equal(CompiledFirmwareArtifactKind.TpFirmware, tpClassification.Kind);
        Assert.False(tpClassification.IsDpMetadataApplicable);

        Assert.Null(resolver.Resolve("NT51928", null, new byte[17]));
        Assert.Null(resolver.Resolve(
            "NT51928",
            null,
            CreateNonUniformArtifact(0x50000)));
        Assert.Null(resolver.Resolve(
            "NT59999",
            null,
            CreateNonUniformArtifact(0x40000)));

        CompositionHostServices staleHost = CompositionHostServices.Create();
        ResolvedCapability staleStandard = staleHost.Catalog.GetCurrentSnapshot()
            .Capabilities.First(static capability =>
                capability.Identity.IcId == "NT51927" &&
                capability.Identity.WorkflowId == ExperienceIds.StandardMerge);
        Assert.Null(resolver.Resolve(
            "NT51927",
            staleStandard,
            CreateNonUniformArtifact(0x40000)));

        JsonElement ctrlRamCase = CanonicalGoldenTestData.LoadDirectCase(
            "ctrlram-replace",
            "nt51950-fw200-single-auto-prj-676-20260717");
        JsonElement[] ctrlRamArtifacts =
        [
            .. ctrlRamCase.GetProperty("artifacts").EnumerateArray(),
        ];
        string tpPath = CanonicalGoldenTestData.ArtifactPath(ctrlRamArtifacts.Single(artifact =>
            artifact.GetProperty("artifactId").GetString() == "tp-input"));
        string nfPath = CanonicalGoldenTestData.ArtifactPath(ctrlRamArtifacts.Single(artifact =>
            artifact.GetProperty("artifactId").GetString() == "postbuild-nf-ctrlram"));
        var slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = tpPath,
            ["replace-ctrlram-nf"] = nfPath,
        };
        (ActiveSessionSnapshot? accepted, IReadOnlyList<CompositionIssue> issues) =
            CtrlRamReplaceTestSupport.Prepare(
                new CanonicalTestContext(classificationHost),
                "NT51950",
                IcNumberSelectionTokens.SingleChip,
                slotPaths,
                firmwareVersionEdit: null);
        ActiveSessionSnapshot session = Assert.IsType<ActiveSessionSnapshot>(
            accepted,
            exactMatch: false);
        Assert.Empty(issues);
        ResolvedCapability ctrlRamTpWork = Assert.IsType<ResolvedCapability>(
            session.GetAcceptedCapability(AuthoringDerivedResultKind.Inspection));
        Assert.Equal(ExperienceIds.CtrlRamReplace, ctrlRamTpWork.Identity.WorkflowId);
        Assert.Equal(
            "nt51950-ctrlram-fw200-single-tp-work",
            ctrlRamTpWork.CompiledComposition.V2Details.Provenance.ResolvedMap.ImageMap.MapId);
        Assert.Equal(
            0x37000,
            ctrlRamTpWork.CompiledComposition.Plan.OutputInitialization.Capacity);
        CompiledFirmwareArtifactClassification acceptedTp = Assert.IsType<
            CompiledFirmwareArtifactClassification>(resolver.Resolve(
                "NT51950",
                ctrlRamTpWork,
                File.ReadAllBytes(tpPath)));
        Assert.Equal(CompiledFirmwareArtifactKind.TpFirmware, acceptedTp.Kind);
        Assert.False(acceptedTp.IsDpMetadataApplicable);
        Assert.Null(resolver.Resolve(
            "NT51950",
            ctrlRamTpWork,
            CreateNonUniformArtifact(0x40000)));
    }

    /// <summary>NT51928 multi-capacity classification survives the full inspection facade.</summary>
    [Fact]
    public async Task Nt51928ArtifactClassificationFlowsThroughInspectionFacade()
    {
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["compact.bin"] = CreateNonUniformArtifact(0x40000),
            ["extended.bin"] = CreateNonUniformArtifact(0x80000),
            ["tp.bin"] = CreateNonUniformArtifact(0x35000),
            ["divergent.bin"] = CreateNonUniformArtifact(0x50000),
        };
        var host = new IsolatedBootstrapTestHost();
        BuiltInFirmwareInspection inspection = CreateInspection(
            host,
            new DelegatingContentInspector((path, _, _) =>
                ValueTask.FromResult(new SelectedFileContentInspection(
                    FileStamp.FromBytes(images[path]),
                    Path.GetFileName(path),
                    acceptedBytes: images[path]))));

        FirmwareInspectionBatchResult batch = await inspection.InspectFirmwareBatchAsync(
            "NT51928",
            [
                new("compact", "compact.bin"),
                new("extended", "extended.bin"),
                new("tp", "tp.bin"),
                new("divergent", "divergent.bin"),
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal(
            CompiledFirmwareArtifactKind.FlashCode,
            Assert.IsType<CompiledFirmwareArtifactClassification>(
                batch.InspectionsById["compact"].ArtifactClassification).Kind);
        Assert.Equal(
            CompiledFirmwareArtifactKind.FlashCode,
            Assert.IsType<CompiledFirmwareArtifactClassification>(
                batch.InspectionsById["extended"].ArtifactClassification).Kind);
        FirmwareInspectionSnapshot tp = batch.InspectionsById["tp"];
        Assert.Equal(
            CompiledFirmwareArtifactKind.TpFirmware,
            Assert.IsType<CompiledFirmwareArtifactClassification>(tp.ArtifactClassification).Kind);
        Assert.Equal(BaseFirmwareArtifactKind.TpFirmware, tp.BaseFirmwareArtifactKind);
        Assert.Null(tp.DpVersion);
        Assert.Null(tp.CmiDpCode);
        Assert.Null(batch.InspectionsById["divergent"].ArtifactClassification);
        Assert.Equal(
            BaseFirmwareArtifactKind.Unknown,
            batch.InspectionsById["divergent"].BaseFirmwareArtifactKind);
    }

    /// <summary>Read-only artifact classification is independent from authoring availability.</summary>
    [Fact]
    public void Nt51928ArtifactClassificationUsesPublishedGeometryWhenAuthoringIsUnavailable()
    {
        var sourceCatalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource());
        CapabilityCatalogReloadResult sourceLoad = sourceCatalog.Reload(
            TestContext.Current.CancellationToken);
        ResolvedCapabilityRoute route = sourceLoad.Snapshot!.DynamicRoutes.Single(candidate =>
            candidate.Identity.IcId == "NT51928" &&
            candidate.Identity.WorkflowId == ExperienceIds.StandardMerge);
        var unavailable = new CanonicalDynamicCapabilityDefinition(
            route.Identity,
            route.CapabilityFingerprint,
            route.CompilationContract,
            new PinnedCapabilityDecision<CapabilityAuthoringAvailability>(
                route.Authoring.DecisionId,
                route.Identity.RouteId,
                route.CapabilityFingerprint,
                CapabilityAuthoringAvailability.Unavailable,
                route.Authoring.SourceReference),
            route.Publication,
            route.Evidence);
        var catalog = new CanonicalCapabilityCatalog(
            new QueuedCandidateSource(new CanonicalCapabilityCatalogCandidate(
                "classification-authoring-unavailable",
                "1.0.0",
                new string('a', 64),
                [],
                [unavailable])));
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        var resolver = new FirmwareArtifactClassificationResolver(
            catalog,
            new CanonicalCapabilityCompilerAdapter(
                catalog,
                new BuiltInV2DynamicCompilationAdapter()));

        Assert.False(catalog.HasAuthorableCapability("NT51928", ExperienceIds.StandardMerge));
        CompiledFirmwareArtifactClassification classification = Assert.IsType<
            CompiledFirmwareArtifactClassification>(resolver.Resolve(
                "NT51928",
                exactCapability: null,
                CreateNonUniformArtifact(0x40000)));
        Assert.Equal(CompiledFirmwareArtifactKind.FlashCode, classification.Kind);
    }

    /// <summary>A malformed dynamic compiler fails artifact classification closed.</summary>
    [Fact]
    public void DynamicArtifactClassificationFailsClosedOnInvalidCompilationData()
    {
        var catalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource());
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        var resolver = new FirmwareArtifactClassificationResolver(
            catalog,
            new CanonicalCapabilityCompilerAdapter(
                catalog,
                new InvalidDataDynamicCompilationAdapter()));

        CompiledFirmwareArtifactClassification? classification = resolver.Resolve(
            "NT51928",
            exactCapability: null,
            CreateNonUniformArtifact(0x40000));

        Assert.Null(classification);
    }

    /// <summary>An incomplete dynamic-map enumeration cannot classify an artifact.</summary>
    [Fact]
    public void DynamicArtifactClassificationFailsClosedOnIncompletePublicationEnumeration()
    {
        var catalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource());
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        var resolver = new FirmwareArtifactClassificationResolver(
            catalog,
            new CanonicalCapabilityCompilerAdapter(
                catalog,
                new IncompleteDynamicCompilationAdapter(
                    new BuiltInV2DynamicCompilationAdapter())));

        CompiledFirmwareArtifactClassification? classification = resolver.Resolve(
            "NT51928",
            exactCapability: null,
            CreateNonUniformArtifact(0x40000));

        Assert.Null(classification);
    }

    /// <summary>A catalog publication rollover during dynamic compilation fails closed.</summary>
    [Fact]
    public void DynamicArtifactClassificationFailsClosedOnPublicationRollover()
    {
        CapabilityCatalogLoadResult seedLoad =
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource().Load(
                TestContext.Current.CancellationToken);
        CanonicalCapabilityCatalogCandidate seed = seedLoad.Candidate!;
        var rollover = new CanonicalCapabilityCatalogCandidate(
            seed.CatalogId,
            "classification-rollover-2",
            seed.SourceSha256,
            seed.Definitions,
            seed.DynamicDefinitions);
        var catalog = new CanonicalCapabilityCatalog(
            new QueuedCandidateSource(seed, rollover));
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        CapabilityCatalogReloadResult? rolloverResult = null;
        var adapter = new RolloverDynamicCompilationAdapter(
            new BuiltInV2DynamicCompilationAdapter(),
            () => rolloverResult = catalog.Reload(TestContext.Current.CancellationToken));
        var resolver = new FirmwareArtifactClassificationResolver(
            catalog,
            new CanonicalCapabilityCompilerAdapter(catalog, adapter));

        CompiledFirmwareArtifactClassification? classification = resolver.Resolve(
            "NT51928",
            exactCapability: null,
            CreateNonUniformArtifact(0x40000));

        Assert.Null(classification);
        Assert.True(rolloverResult?.Succeeded);
        Assert.Equal(1, adapter.RolloverCalls);
    }

    private static byte[] CreateNonUniformArtifact(int length)
    {
        var artifact = new byte[length];
        for (int index = 0; index < artifact.Length; index++)
        {
            artifact[index] = checked((byte)(index % 251));
        }

        return artifact;
    }

    private sealed class InvalidDataDynamicCompilationAdapter :
        ICanonicalDynamicCompilationAdapter
    {
        public IReadOnlyList<long> GetMapCapacities(
            string icId,
            string workflowId,
            out IReadOnlyList<CompositionIssue> issues)
        {
            issues = [];
            return [];
        }

        public void Compile(
            string icId,
            string workflowId,
            long? requestedMapCapacity,
            IReadOnlyCollection<string>? selectedInputSlotIds,
            out CompiledComposition? composition,
            out MetadataPlanDefinition? metadataPlan,
            out IReadOnlyList<CompositionIssue> issues)
        {
            throw new InvalidDataException("Synthetic malformed dynamic compilation.");
        }
    }

    private sealed class IncompleteDynamicCompilationAdapter(
        ICanonicalDynamicCompilationAdapter inner) : ICanonicalDynamicCompilationAdapter
    {
        public IReadOnlyList<long> GetMapCapacities(
            string icId,
            string workflowId,
            out IReadOnlyList<CompositionIssue> issues)
        {
            return inner.GetMapCapacities(icId, workflowId, out issues);
        }

        public void Compile(
            string icId,
            string workflowId,
            long? requestedMapCapacity,
            IReadOnlyCollection<string>? selectedInputSlotIds,
            out CompiledComposition? composition,
            out MetadataPlanDefinition? metadataPlan,
            out IReadOnlyList<CompositionIssue> issues)
        {
            inner.Compile(
                icId,
                workflowId,
                requestedMapCapacity,
                selectedInputSlotIds: [],
                out composition,
                out metadataPlan,
                out issues);
        }
    }

    private sealed class RolloverDynamicCompilationAdapter(
        ICanonicalDynamicCompilationAdapter inner,
        Action rollover) : ICanonicalDynamicCompilationAdapter
    {
        private int _rolloverCalls;

        internal int RolloverCalls => _rolloverCalls;

        public IReadOnlyList<long> GetMapCapacities(
            string icId,
            string workflowId,
            out IReadOnlyList<CompositionIssue> issues)
        {
            return inner.GetMapCapacities(icId, workflowId, out issues);
        }

        public void Compile(
            string icId,
            string workflowId,
            long? requestedMapCapacity,
            IReadOnlyCollection<string>? selectedInputSlotIds,
            out CompiledComposition? composition,
            out MetadataPlanDefinition? metadataPlan,
            out IReadOnlyList<CompositionIssue> issues)
        {
            inner.Compile(
                icId,
                workflowId,
                requestedMapCapacity,
                selectedInputSlotIds,
                out composition,
                out metadataPlan,
                out issues);
            if (Interlocked.Increment(ref _rolloverCalls) == 1)
            {
                rollover();
            }
        }
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
            new FirmwareMetadataPlanAuthorityResolver(catalog),
            BootstrapTestHost.Canonical.Projection,
            (StandardMergeAuthoringExperience)services.StandardMergeAuthoring,
            (AbMergeAuthoringExperience)services.AbMergeAuthoring,
            (DpReplaceAuthoringExperience)services.DpReplaceAuthoring,
            (CtrlRamAuthoringExperience)services.CtrlRamAuthoring,
            new FirmwareArtifactClassificationResolver(catalog, services.Compiler),
            contentInspector);
    }

    private sealed class InterceptingMetadataPlanQuery(
        ICanonicalCapabilityQuery inner,
        Func<string, string, string, long?, MetadataPlanResolutionResult> resolveMetadataPlan)
        : ICanonicalCapabilityQuery
    {
        public CanonicalCapabilityCatalogSnapshot GetCurrentSnapshot()
        {
            return inner.GetCurrentSnapshot();
        }

        public CanonicalCapabilityCatalogSnapshot? TryGetCurrentSnapshot()
        {
            return inner.TryGetCurrentSnapshot();
        }

        public CapabilityResolutionResult Resolve(string routeId)
        {
            return inner.Resolve(routeId);
        }

        public CapabilityRouteResolutionResult ResolveDynamicRoute(string routeId)
        {
            return inner.ResolveDynamicRoute(routeId);
        }

        public CapabilityResolutionResult ResolveUniqueRoute(
            string icId,
            string workflowId,
            string icCountVariant,
            long? outputCapacity = null)
        {
            return inner.ResolveUniqueRoute(icId, workflowId, icCountVariant, outputCapacity);
        }

        public MetadataPlanResolutionResult ResolveUniqueMetadataPlan(
            string icId,
            string workflowId,
            string icCountVariant,
            long? outputCapacity = null)
        {
            return resolveMetadataPlan(
                icId,
                workflowId,
                icCountVariant,
                outputCapacity);
        }

        public CapabilityResolutionResult ResolveUniqueTopologyRoute(
            string icId,
            string workflowId,
            TopologySelection? topology)
        {
            return inner.ResolveUniqueTopologyRoute(icId, workflowId, topology);
        }

        public bool HasAuthorableCapability(string icId, string workflowId)
        {
            return inner.HasAuthorableCapability(icId, workflowId);
        }

        public ResolvedCapability? ResolveCurrentCompilation(
            CompiledComposition composition,
            ResolvedCapability? acceptedCapability = null)
        {
            return inner.ResolveCurrentCompilation(composition, acceptedCapability);
        }
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

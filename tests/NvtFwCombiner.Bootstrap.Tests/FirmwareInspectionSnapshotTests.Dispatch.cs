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
    /// <summary>Read-only Standard metadata follows exact trusted capacity and never compiles an envelope from length alone.</summary>
    [Theory]
    [InlineData("NT51950", 0x40000)]
    [InlineData("NT51950", 0x80000)]
    [InlineData("NT51950", 0x100000)]
    [InlineData("NT51951", 0x40000)]
    [InlineData("NT51951", 0x80000)]
    [InlineData("NT51951", 0x100000)]
    public void GenericStandardMetadataUsesOnlyPublishedExactCapacity(string icId, long capacity)
    {
        var query = (IStandardMergeMetadataPlanQuery)BootstrapTestHost.Canonical.Compiler;
        MetadataPlanResolutionResult exact = Assert.IsType<MetadataPlanResolutionResult>(
            query.ResolveSourceEnvelopeMetadataPlan(icId, capacity));
        Assert.NotNull(exact.MetadataPlan);
        Assert.Null(exact.Issue);
        Assert.Equal(BootstrapTestHost.Canonical.Catalog.GetCurrentSnapshot().ResolutionToken,
            exact.MetadataPlan.ResolutionToken);

        MetadataPlanResolutionResult nonstandard = Assert.IsType<MetadataPlanResolutionResult>(
            query.ResolveSourceEnvelopeMetadataPlan(icId, capacity + 1));
        Assert.Null(nonstandard.MetadataPlan);
        Assert.Equal(CapabilityCatalogIssueCodes.RouteUnavailable, nonstandard.Issue?.Code);
    }

    /// <summary>The actual generic facade consumes one captured full image and a terminal query failure never reopens DP metadata.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenericFullImageFacadeUsesOneCapturedPayloadAndTerminalAuthority(bool unavailable)
    {
        byte[] bytes = CreateNonUniformArtifact(0x40000);
        bytes[0x36000] = 0x42;
        bytes[0x36001] = 0xBD;
        bytes[0x36017] = 1;
        new byte[] { 0, 0x4E, 0x56, 0x54 }.CopyTo(bytes, 0x36FFC);
        bytes[0x3B016] = 0x2E;
        bytes[0x3B017] = 3;
        bytes[0x3B018] = 0xA4;
        ICanonicalCapabilityQuery query = BootstrapTestHost.Canonical.Catalog;
        int queries = 0;
        int reads = 0;
        BuiltInFirmwareInspection inspection = CreateInspection(new InterceptingMetadataPlanQuery(query,
            (ic, workflow, count, capacity) =>
            {
                queries++;
                Assert.Equal(("NT51950", "full-image", "none", 0x40000L), (ic, workflow, count, capacity));
                return unavailable
                    ? new(null, new(CapabilityCatalogIssueCodes.FullImageMetadataUnavailable, "No declared view."))
                    : query.ResolveFullImageMetadataPlan(ic, capacity!.Value);
            }), new DelegatingContentInspector((path, _, _) =>
            {
                reads++;
                return ValueTask.FromResult(new SelectedFileContentInspection(
                    FileStamp.FromBytes(bytes), Path.GetFileName(path), acceptedBytes: bytes));
            }));
        FirmwareInspectionBatchResult batch = await inspection.InspectFirmwareBatchAsync("NT51950",
            [new("reference", "generic-reference.bin")], TestContext.Current.CancellationToken);
        bytes[0x3B017] = 0xFF;
        Assert.Equal(1, reads);
        Assert.Equal(1, queries);
        FirmwareInspectionSnapshot result = batch.InspectionsById["reference"];
        if (unavailable)
        {
            Assert.Null(result.DpVersion);
            Assert.Null(result.CmiDpCode);
        }
        else
        {
            Assert.True(result.ArtifactClassification!.IsDpMetadataApplicable);
            Assert.Equal("030A", Assert.IsType<DpVersionMetadata>(result.DpVersion).VersionToken);
            Assert.Equal((ushort)1070, Assert.IsType<CmiDpCodeMetadata>(result.CmiDpCode).JiraNumber);
        }
    }

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
    public async Task SelectiveDispatchMatchesAllStrategyBaselineForEveryWorkflow()
    {
        foreach ((string icId, FirmwareInspectionSnapshotInput[] inputs, Dictionary<string, byte[]> images)
                 in CreateWorkflowDispatchCases())
        {
            // AB owns asynchronous format capture; compare assembly strategies over
            // that same immutable result instead of reintroducing synchronous discovery.
            AbMergeInspectionBatch abBatch = await
                ((AbMergeAuthoringExperience)BootstrapTestHost.Services.AbMergeAuthoring)
                .InspectInputSlotsAsync(icId, inputs, path => images[path],
                    TestContext.Current.CancellationToken);
            var selectiveReads = new Dictionary<string, int>(StringComparer.Ordinal);
            IReadOnlyList<FirmwareInspectionSnapshotResult> selective =
                BuiltInFirmwareInspection.InspectFirmwareBatch(
                    BootstrapTestHost.Canonical,
                    icId,
                    inputs,
                    path => ReadOnce(path, images, selectiveReads),
                    capturedAbBatch: abBatch);

            var baselineReads = new Dictionary<string, int>(StringComparer.Ordinal);
            IReadOnlyList<FirmwareInspectionSnapshotResult> baseline =
                BuiltInFirmwareInspection.InspectFirmwareBatch(
                    BootstrapTestHost.Canonical,
                    icId,
                    inputs,
                    path => ReadOnce(path, images, baselineReads),
                    FirmwareInspectionDispatch.AllStrategiesBaseline,
                    capturedAbBatch: abBatch);

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
        CapabilityCatalogReloadResult load = catalog.Reload(TestContext.Current.CancellationToken);
        Assert.True(load.Succeeded, string.Join(" | ", load.Issues.Select(static issue => issue.Message)));
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

    /// <summary>Every published Standard capacity can classify its own complete Base.</summary>
    [Theory]
    [InlineData("NT51950", 0x40000)]
    [InlineData("NT51950", 0x80000)]
    [InlineData("NT51950", 0x100000)]
    [InlineData("NT51951", 0x40000)]
    [InlineData("NT51951", 0x80000)]
    [InlineData("NT51951", 0x100000)]
    public void StandardCapacityRoutesClassifyTheirExactBase(string icId, int capacity)
    {
        var catalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource());
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        var resolver = new FirmwareArtifactClassificationResolver(
            catalog,
            new CanonicalCapabilityCompilerAdapter(
                catalog,
                new BuiltInV2DynamicCompilationAdapter()));

        CompiledFirmwareArtifactClassification classification = Assert.IsType<
            CompiledFirmwareArtifactClassification>(resolver.Resolve(
                icId,
                exactCapability: null,
                CreateNonUniformArtifact(capacity)));
        Assert.Equal(CompiledFirmwareArtifactKind.FlashCode, classification.Kind);
    }

    /// <summary>One classification resolves the trusted capacity-to-route table once.</summary>
    [Fact]
    public void StandardCapacityClassificationDoesNotRequeryRoutesPerCandidate()
    {
        var catalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource());
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        var adapter = new FaultedStandardCapacityAdapter(
            new BuiltInV2DynamicCompilationAdapter(), "none");
        var resolver = new FirmwareArtifactClassificationResolver(
            catalog, new CanonicalCapabilityCompilerAdapter(catalog, adapter));

        Assert.NotNull(resolver.Resolve("NT51950", exactCapability: null,
            CreateNonUniformArtifact(0x40000)));
        Assert.Equal(1, adapter.CapacityQueryCount);
        Assert.Equal(4, adapter.MapVariantQueryCount);
    }

    /// <summary>Classification requires every route; metadata compiles only its selected exact route.</summary>
    [Theory]
    [InlineData("missing", CapabilityCatalogIssueCodes.RouteUnavailable)]
    [InlineData("duplicate", CapabilityCatalogIssueCodes.RouteAmbiguous)]
    [InlineData("uncompiled", CapabilityCatalogIssueCodes.RouteUnavailable)]
    public void StandardCapacityClassificationRejectsIncompletePublication(string fault, string expectedMetadataIssue)
    {
        var catalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource());
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        var compiler = new CanonicalCapabilityCompilerAdapter(
            catalog,
            new FaultedStandardCapacityAdapter(
                new BuiltInV2DynamicCompilationAdapter(), fault));
        var resolver = new FirmwareArtifactClassificationResolver(catalog, compiler);

        Assert.Null(resolver.Resolve(
            "NT51950",
            exactCapability: null,
            CreateNonUniformArtifact(0x40000)));
        MetadataPlanResolutionResult metadata = Assert.IsType<MetadataPlanResolutionResult>(
            ((IStandardMergeMetadataPlanQuery)compiler).ResolveSourceEnvelopeMetadataPlan(
                "NT51950", 0x40000));
        if (fault == "uncompiled")
        {
            Assert.NotNull(metadata.MetadataPlan);
            Assert.Null(metadata.Issue);
            metadata = Assert.IsType<MetadataPlanResolutionResult>(
                ((IStandardMergeMetadataPlanQuery)compiler).ResolveSourceEnvelopeMetadataPlan(
                    "NT51950", 0x80000));
        }
        Assert.Null(metadata.MetadataPlan);
        Assert.Equal(expectedMetadataIssue, metadata.Issue?.Code);
    }

    /// <summary>Changing the publication mid-enumeration cannot return a mixed-route classification.</summary>
    [Fact]
    public void StandardCapacityClassificationRejectsPublicationRollover()
    {
        CanonicalCapabilityCatalogCandidate seed =
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource().Load(
                TestContext.Current.CancellationToken).Candidate!;
        var rollover = new CanonicalCapabilityCatalogCandidate(
            seed.CatalogId,
            "standard-capacity-rollover-2",
            seed.SourceSha256,
            seed.Definitions,
            seed.DynamicDefinitions);
        var catalog = new CanonicalCapabilityCatalog(new QueuedCandidateSource(seed, rollover));
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        CapabilityCatalogReloadResult? rolloverResult = null;
        var resolver = new FirmwareArtifactClassificationResolver(
            catalog,
            new CanonicalCapabilityCompilerAdapter(
                catalog,
                new FaultedStandardCapacityAdapter(
                    new BuiltInV2DynamicCompilationAdapter(),
                    "none",
                    () => rolloverResult = catalog.Reload(TestContext.Current.CancellationToken))));

        Assert.Null(resolver.Resolve(
            "NT51950",
            exactCapability: null,
            CreateNonUniformArtifact(0x40000)));
        Assert.True(rolloverResult?.Succeeded);
    }

    /// <summary>Length-only Standard compilation never fabricates a missing or duplicate exact capacity.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    public void StandardLengthOnlyCompilationRejectsInvalidCapacityLookup(string fault)
    {
        var catalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource());
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        var compiler = new CanonicalCapabilityCompilerAdapter(
            catalog,
            new FaultedStandardCapacityAdapter(new BuiltInV2DynamicCompilationAdapter(), fault));

        Assert.False(compiler.TryCompileStandardMerge(
            "NT51950", 0x40000, out CompiledComposition? composition, out _));
        Assert.Null(composition);
    }

    /// <summary>A failed source-envelope query is terminal and retains its typed issue.</summary>
    [Theory]
    [InlineData("declaration-error")]
    [InlineData("map-error")]
    public void StandardRouteQueryFailureDoesNotFallThrough(string fault)
    {
        var catalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource());
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        var adapter = new FaultedStandardCapacityAdapter(
            new BuiltInV2DynamicCompilationAdapter(), fault);
        var compiler = new CanonicalCapabilityCompilerAdapter(catalog, adapter);

        Assert.False(compiler.TryCompileStandardMerge(
            "NT51950", 0x40000, out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues));
        Assert.Null(composition);
        Assert.Equal("test.route-query-failed", Assert.Single(issues).Code);
        Assert.Equal(0, adapter.CompileCallCount);
    }

    /// <summary>A reload during route-set materialization cannot publish a mixed selection.</summary>
    [Fact]
    public void StandardRouteQueryRejectsPublicationRollover()
    {
        CanonicalCapabilityCatalogCandidate seed =
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource().Load(
                TestContext.Current.CancellationToken).Candidate!;
        var rollover = new CanonicalCapabilityCatalogCandidate(
            seed.CatalogId, "standard-query-rollover-2", seed.SourceSha256,
            seed.Definitions, seed.DynamicDefinitions);
        var catalog = new CanonicalCapabilityCatalog(new QueuedCandidateSource(seed, rollover));
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        var adapter = new FaultedStandardCapacityAdapter(
            new BuiltInV2DynamicCompilationAdapter(), "none",
            afterCapacityQuery: () => catalog.Reload(TestContext.Current.CancellationToken));
        var compiler = new CanonicalCapabilityCompilerAdapter(catalog, adapter);

        Assert.False(compiler.TryCompileStandardMerge(
            "NT51950", 0x40000, out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues));
        Assert.Null(composition);
        Assert.Equal(AuthoringSessionIssueCodes.StalePublication, Assert.Single(issues).Code);
        Assert.Equal(0, adapter.CompileCallCount);
    }

    /// <summary>A malformed selected compilation cannot escape generic metadata inspection.</summary>
    [Fact]
    public void StandardMetadataFailsClosedWhenSelectedCompilationThrows()
    {
        var catalog = new CanonicalCapabilityCatalog(
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource());
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        var compiler = new CanonicalCapabilityCompilerAdapter(
            catalog, new FaultedStandardCapacityAdapter(
                new BuiltInV2DynamicCompilationAdapter(), "invalid-data"));

        MetadataPlanResolutionResult metadata = Assert.IsType<MetadataPlanResolutionResult>(
            ((IStandardMergeMetadataPlanQuery)compiler).ResolveSourceEnvelopeMetadataPlan(
                "NT51950", 0x40000));
        Assert.Null(metadata.MetadataPlan);
        Assert.Equal(CapabilityCatalogIssueCodes.RouteUnavailable, metadata.Issue?.Code);
    }

    /// <summary>Length-only exact compilation cannot bind a route from a newer publication.</summary>
    [Fact]
    public void StandardLengthOnlyCompilationRejectsPublicationRollover()
    {
        CanonicalCapabilityCatalogCandidate seed =
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource().Load(
                TestContext.Current.CancellationToken).Candidate!;
        var rollover = new CanonicalCapabilityCatalogCandidate(
            seed.CatalogId, "standard-length-rollover-2", seed.SourceSha256,
            seed.Definitions, seed.DynamicDefinitions);
        var catalog = new CanonicalCapabilityCatalog(new QueuedCandidateSource(seed, rollover));
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        CapabilityCatalogReloadResult? rolloverResult = null;
        var compiler = new CanonicalCapabilityCompilerAdapter(
            catalog,
            new FaultedStandardCapacityAdapter(
                new BuiltInV2DynamicCompilationAdapter(), "none",
                () => rolloverResult = catalog.Reload(TestContext.Current.CancellationToken)));

        Assert.False(compiler.TryCompileStandardMerge(
            "NT51950", 0x40000, out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues));
        Assert.Null(composition);
        Assert.True(rolloverResult?.Succeeded);
        Assert.Contains(issues, static issue => issue.Code == AuthoringSessionIssueCodes.StalePublication);
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

    /// <summary>Reload between AB layout and its Standard metadata counterpart cannot publish mixed bank facts.</summary>
    [Fact]
    public void AbBaseFactsRejectPublicationRolloverDuringStandardCompilation()
    {
        CanonicalCapabilityCatalogCandidate seed =
            CompositionHostServices.CreateCanonicalCapabilityCatalogSource().Load(
                TestContext.Current.CancellationToken).Candidate!;
        var rollover = new CanonicalCapabilityCatalogCandidate(
            seed.CatalogId, "ab-base-event-rollover-2", seed.SourceSha256,
            seed.Definitions, seed.DynamicDefinitions);
        var catalog = new CanonicalCapabilityCatalog(new QueuedCandidateSource(seed, rollover));
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        ResolutionToken originalToken = catalog.TryGetCurrentSnapshot()!.ResolutionToken;
        CapabilityCatalogReloadResult? rolloverResult = null;
        int standardQueries = 0;
        var query = new InterceptingMetadataPlanQuery(catalog,
            catalog.ResolveUniqueMetadataPlan,
            (ic, workflow) =>
            {
                if (ic == "NT51929" && workflow == ExperienceIds.StandardMerge && ++standardQueries == 1)
                {
                    rolloverResult = catalog.Reload(TestContext.Current.CancellationToken);
                }
            });
        var resolver = new FirmwareArtifactClassificationResolver(
            query, new CanonicalCapabilityCompilerAdapter(query, new BuiltInV2DynamicCompilationAdapter()));
        byte[] reference = File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(
            "ab-merge", "NT51929", "expected-output", "t05-d06"));

        CtrlRamBaseInspection inspection = resolver.ResolveCtrlRamBase(
            "NT51929", exactCapability: null, reference, draft: null,
            adapter: new BuiltInCtrlRamAuthoringAdapter(catalog, BootstrapTestHost.Canonical.Projection));

        Assert.True(rolloverResult?.Succeeded);
        Assert.NotEqual(originalToken, catalog.TryGetCurrentSnapshot()!.ResolutionToken);
        Assert.Equal(1, standardQueries);
        Assert.NotEqual(CtrlRamBaseKind.AbFlash, inspection.Kind);
        Assert.Empty(inspection.Banks);
        Assert.Contains(inspection.Issues, static issue => issue.Code == AuthoringSessionIssueCodes.StaleInspection);
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

    private sealed class FaultedStandardCapacityAdapter(
        ICanonicalDynamicCompilationAdapter inner,
        string fault,
        Action? afterCompile = null,
        Action? afterCapacityQuery = null) : ICanonicalDynamicCompilationAdapter
    {
        private int _compileCalls;
        public int CapacityQueryCount { get; private set; }
        public int MapVariantQueryCount { get; private set; }
        public int CompileCallCount => _compileCalls;

        public bool TryGetSourceEnvelopeMapVariant(string icId, string workflowId,
            long? sourceLength, out string? mapVariant,
            out IReadOnlyList<CompositionIssue> issues)
        {
            MapVariantQueryCount++;
            if ((fault == "declaration-error" && sourceLength is null) ||
                (fault == "map-error" && sourceLength == 0x80000))
            {
                mapVariant = null;
                issues = [new CompositionIssue("test.route-query-failed", "Synthetic route query failure.")];
                return false;
            }
            return inner.TryGetSourceEnvelopeMapVariant(
                icId, workflowId, sourceLength, out mapVariant, out issues);
        }

        public bool TryGetAbAuthoringDefinition(CapabilityRouteIdentity identity,
            out CanonicalAbAuthoringDefinition? definition,
            out IReadOnlyList<CompositionIssue> issues)
        {
            return inner.TryGetAbAuthoringDefinition(identity, out definition, out issues);
        }

        public IReadOnlyList<long> GetMapCapacities(string icId, string workflowId,
            out IReadOnlyList<CompositionIssue> issues)
        {
            CapacityQueryCount++;
            IReadOnlyList<long> capacities = inner.GetMapCapacities(icId, workflowId, out issues);
            afterCapacityQuery?.Invoke();
            return fault switch
            {
                "missing" => [.. capacities.Skip(1)],
                "duplicate" => [capacities[0], .. capacities],
                _ => capacities,
            };
        }

        public void Compile(CapabilityRouteIdentity identity, long? requestedMapCapacity,
            IReadOnlyCollection<string>? selectedInputSlotIds,
            out CompiledComposition? composition,
            out MetadataPlanDefinition? metadataPlan,
            out IReadOnlyList<CompositionIssue> issues,
            TopologySelection? requestedTopology = null)
        {
            if (fault == "invalid-data")
            {
                throw new InvalidDataException("Synthetic malformed selected compilation.");
            }
            if (fault == "uncompiled" &&
                identity.MapVariant == "nt51950-standard-merge-512k")
            {
                composition = null;
                metadataPlan = null;
                issues = [new CompositionIssue("test.incomplete-route", "One published route did not compile.")];
                return;
            }

            inner.Compile(identity, requestedMapCapacity, selectedInputSlotIds,
                out composition, out metadataPlan, out issues, requestedTopology);
            if (Interlocked.Increment(ref _compileCalls) == 1)
            {
                afterCompile?.Invoke();
            }
        }
    }

    private sealed class InvalidDataDynamicCompilationAdapter :
        ICanonicalDynamicCompilationAdapter
    {
        public bool TryGetAbAuthoringDefinition(CapabilityRouteIdentity identity,
            out CanonicalAbAuthoringDefinition? definition, out IReadOnlyList<CompositionIssue> issues)
        {
            throw new InvalidDataException("Synthetic malformed authoring declaration.");
        }

        public IReadOnlyList<long> GetMapCapacities(
            string icId,
            string workflowId,
            out IReadOnlyList<CompositionIssue> issues)
        {
            issues = [];
            return [];
        }

        public void Compile(
            CapabilityRouteIdentity identity,
            long? requestedMapCapacity,
            IReadOnlyCollection<string>? selectedInputSlotIds,
            out CompiledComposition? composition,
            out MetadataPlanDefinition? metadataPlan,
            out IReadOnlyList<CompositionIssue> issues,
            TopologySelection? requestedTopology = null)
        {
            throw new InvalidDataException("Synthetic malformed dynamic compilation.");
        }

    }

    private sealed class IncompleteDynamicCompilationAdapter(
        ICanonicalDynamicCompilationAdapter inner) : ICanonicalDynamicCompilationAdapter
    {
        public bool TryGetAbAuthoringDefinition(CapabilityRouteIdentity identity,
            out CanonicalAbAuthoringDefinition? definition, out IReadOnlyList<CompositionIssue> issues)
        {
            return inner.TryGetAbAuthoringDefinition(identity, out definition, out issues);
        }

        public IReadOnlyList<long> GetMapCapacities(
            string icId,
            string workflowId,
            out IReadOnlyList<CompositionIssue> issues)
        {
            return inner.GetMapCapacities(icId, workflowId, out issues);
        }

        public void Compile(
            CapabilityRouteIdentity identity,
            long? requestedMapCapacity,
            IReadOnlyCollection<string>? selectedInputSlotIds,
            out CompiledComposition? composition,
            out MetadataPlanDefinition? metadataPlan,
            out IReadOnlyList<CompositionIssue> issues,
            TopologySelection? requestedTopology = null)
        {
            inner.Compile(
                identity,
                requestedMapCapacity,
                selectedInputSlotIds: [],
                out composition,
                out metadataPlan,
                out issues,
                requestedTopology);
        }

    }

    private sealed class RolloverDynamicCompilationAdapter(
        ICanonicalDynamicCompilationAdapter inner,
        Action rollover) : ICanonicalDynamicCompilationAdapter
    {
        public bool TryGetAbAuthoringDefinition(CapabilityRouteIdentity identity,
            out CanonicalAbAuthoringDefinition? definition, out IReadOnlyList<CompositionIssue> issues)
        {
            bool result = inner.TryGetAbAuthoringDefinition(identity, out definition, out issues);
            rollover();
            return result;
        }

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
            CapabilityRouteIdentity identity,
            long? requestedMapCapacity,
            IReadOnlyCollection<string>? selectedInputSlotIds,
            out CompiledComposition? composition,
            out MetadataPlanDefinition? metadataPlan,
            out IReadOnlyList<CompositionIssue> issues,
            TopologySelection? requestedTopology = null)
        {
            inner.Compile(
                identity,
                requestedMapCapacity,
                selectedInputSlotIds,
                out composition,
                out metadataPlan,
                out issues,
                requestedTopology);
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
            new FirmwareMetadataPlanAuthorityResolver(catalog, services.Compiler),
            BootstrapTestHost.Canonical.Projection,
            (StandardMergeAuthoringExperience)services.StandardMergeAuthoring,
            (AbMergeAuthoringExperience)services.AbMergeAuthoring,
            (CtrlRamAuthoringExperience)services.CtrlRamAuthoring,
            new FirmwareArtifactClassificationResolver(catalog, services.Compiler),
            contentInspector);
    }

    private sealed class InterceptingMetadataPlanQuery(
        ICanonicalCapabilityQuery inner,
        Func<string, string, string, long?, MetadataPlanResolutionResult> resolveMetadataPlan,
        Action<string, string>? afterResolveRoute = null)
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
            CapabilityResolutionResult result = inner.ResolveUniqueRoute(
                icId, workflowId, icCountVariant, outputCapacity);
            afterResolveRoute?.Invoke(icId, workflowId);
            return result;
        }

        public MetadataPlanResolutionResult ResolveFullImageMetadataPlan(string icId, long inputLength)
        {
            return resolveMetadataPlan(icId, "full-image", "none", inputLength);
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

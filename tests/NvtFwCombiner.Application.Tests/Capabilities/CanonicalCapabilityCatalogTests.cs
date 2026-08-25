using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Capabilities;

/// <summary>Tests the Application-owned canonical capability snapshot and reload boundary.</summary>
public sealed partial class CanonicalCapabilityCatalogTests
{
    private static readonly CapabilityRouteIdentity Route = new(
        "NT51929",
        "standard-merge",
        "selector-free",
        "nt51929-standard-merge-256k");

    /// <summary>Stable route identity excludes executable firmware semantics.</summary>
    [Fact]
    public void RouteIdentityContainsOnlySelectionAxes()
    {
        Assert.Equal(
            "route-7-nt51929-14-standard-merge-13-selector-free-27-nt51929-standard-merge-256k",
            Route.RouteId);
    }

    /// <summary>Every policy and evidence decision must pin the current capability fingerprint.</summary>
    [Theory]
    [InlineData("authoring")]
    [InlineData("publication")]
    [InlineData("evidence")]
    public void DefinitionRejectsDecisionPinnedToAnotherFingerprint(
        string decisionKind)
    {
        CompiledComposition composition = CreateCompiledComposition();
        string staleFingerprint = new('0', 64);

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            CreateDefinition(
                composition,
                authoringFingerprint:
                    decisionKind == "authoring" ? staleFingerprint : null,
                publicationFingerprint:
                    decisionKind == "publication" ? staleFingerprint : null,
                evidenceFingerprint:
                    decisionKind == "evidence" ? staleFingerprint : null));

        Assert.Contains(
            $"{decisionKind} decision",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A complete candidate publishes one immutable snapshot and resolvable capability.</summary>
    [Fact]
    public void ReloadPublishesResolvableSnapshot()
    {
        CanonicalCapabilityCatalogCandidate candidate = CreateCandidate();
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(CapabilityCatalogLoadResult.Success(candidate)));

        CapabilityCatalogReloadResult reload =
            catalog.Reload(TestContext.Current.CancellationToken);
        CapabilityResolutionResult resolution = catalog.Resolve(Route.RouteId);

        Assert.True(reload.Succeeded);
        Assert.False(reload.RetainedLastKnownGood);
        Assert.NotNull(reload.Snapshot);
        Assert.True(resolution.Succeeded);
        Assert.Same(
            candidate.Definitions[0].CompiledComposition,
            resolution.Capability!.CompiledComposition);
        Assert.Equal(reload.Snapshot.ResolutionToken, resolution.Capability.ResolutionToken);
        Assert.Equal(
            candidate.Definitions[0].CapabilityFingerprint,
            resolution.Capability.CapabilityFingerprint);
    }

    /// <summary>A caller can resolve the sole map variant without restating map facts.</summary>
    [Fact]
    public void ResolveUniqueRouteUsesThePublishedSelectionAxes()
    {
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(
                CapabilityCatalogLoadResult.Success(CreateCandidate())));
        _ = catalog.Reload(TestContext.Current.CancellationToken);

        CapabilityResolutionResult resolution = catalog.ResolveUniqueRoute(
            "NT51929",
            "standard-merge",
            "selector-free");

        Assert.True(resolution.Succeeded);
        Assert.Equal(Route.RouteId, resolution.Capability!.Identity.RouteId);
    }

    /// <summary>Availability and execution retention are resolved by the publication owner.</summary>
    [Fact]
    public void CatalogOwnsAvailabilityAndCurrentCompilationRetention()
    {
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(
                CapabilityCatalogLoadResult.Success(CreateCandidate())));
        _ = catalog.Reload(TestContext.Current.CancellationToken);
        ResolvedCapability capability = catalog.Resolve(Route.RouteId).Capability!;

        Assert.True(catalog.HasAuthorableCapability("NT51929", "standard-merge"));
        Assert.Same(
            capability,
            catalog.ResolveCurrentCompilation(
                capability.CompiledComposition,
                capability));
        Assert.Null(catalog.ResolveCurrentCompilation(CreateCompiledComposition()));
    }

    /// <summary>Every successful publication receives a fresh token even for identical source bytes.</summary>
    [Fact]
    public void RepeatedSuccessfulReloadPublishesFreshResolutionToken()
    {
        CanonicalCapabilityCatalogCandidate candidate = CreateCandidate();
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(
                CapabilityCatalogLoadResult.Success(candidate),
                CapabilityCatalogLoadResult.Success(candidate)));

        CapabilityCatalogReloadResult first =
            catalog.Reload(TestContext.Current.CancellationToken);
        CapabilityCatalogReloadResult second =
            catalog.Reload(TestContext.Current.CancellationToken);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotEqual(first.Snapshot!.ResolutionToken, second.Snapshot!.ResolutionToken);
    }

    /// <summary>A rejected reload retains the complete last-known-good snapshot.</summary>
    [Fact]
    public void FailedReloadRetainsLastKnownGoodSnapshot()
    {
        CanonicalCapabilityCatalogCandidate candidate = CreateCandidate();
        var sourceIssue = new CapabilityCatalogIssue(
            CapabilityCatalogIssueCodes.SourceInvalid,
            "The trusted source hash is invalid.");
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(
                CapabilityCatalogLoadResult.Success(candidate),
                CapabilityCatalogLoadResult.Failure(sourceIssue)));

        CapabilityCatalogReloadResult first =
            catalog.Reload(TestContext.Current.CancellationToken);
        CapabilityCatalogReloadResult failed =
            catalog.Reload(TestContext.Current.CancellationToken);

        Assert.True(first.Succeeded);
        Assert.False(failed.Succeeded);
        Assert.True(failed.RetainedLastKnownGood);
        Assert.Same(first.Snapshot, failed.Snapshot);
        Assert.Equal(sourceIssue, Assert.Single(failed.Issues));
        Assert.Equal(
            first.Snapshot!.ResolutionToken,
            catalog.Resolve(Route.RouteId).Capability!.ResolutionToken);
    }

    /// <summary>Cold start without a valid snapshot returns one stable Build blocker.</summary>
    [Fact]
    public void ColdStartFailureReturnsTypedCatalogUnavailableIssue()
    {
        var sourceIssue = new CapabilityCatalogIssue(
            CapabilityCatalogIssueCodes.SourceUnavailable,
            "The trusted catalog is missing.");
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(CapabilityCatalogLoadResult.Failure(sourceIssue)));

        CapabilityCatalogReloadResult reload =
            catalog.Reload(TestContext.Current.CancellationToken);
        CapabilityResolutionResult resolution = catalog.Resolve(Route.RouteId);
        MetadataPlanResolutionResult metadata = catalog.ResolveUniqueMetadataPlan(
            "NT51929",
            "standard-merge",
            "selector-free");

        Assert.False(reload.Succeeded);
        Assert.False(reload.RetainedLastKnownGood);
        Assert.Null(reload.Snapshot);
        Assert.False(resolution.Succeeded);
        Assert.Equal(CapabilityCatalogIssueCodes.CatalogUnavailable, resolution.Issue!.Code);
        Assert.False(metadata.Succeeded);
        Assert.Equal(CapabilityCatalogIssueCodes.CatalogUnavailable, metadata.Issue!.Code);
    }

    /// <summary>An explicit retry bypasses the cached cold-start failure and can publish a repaired source.</summary>
    [Fact]
    public void ExplicitReloadRecoversFromCachedColdStartFailure()
    {
        var sourceIssue = new CapabilityCatalogIssue(
            CapabilityCatalogIssueCodes.SourceUnavailable,
            "The trusted catalog is temporarily unavailable.");
        var catalog = new CanonicalCapabilityCatalog(new QueueCapabilitySource(
            CapabilityCatalogLoadResult.Failure(sourceIssue),
            CapabilityCatalogLoadResult.Success(CreateCandidate())));

        _ = catalog.TryGetCurrentSnapshot();
        _ = catalog.TryGetCurrentSnapshot();
        CanonicalSupportMatrixQueryResult blocked = catalog.Query();
        CapabilityCatalogReloadResult recovered = catalog.Reload(
            TestContext.Current.CancellationToken);

        Assert.Equal(CanonicalSupportMatrixCatalogState.ColdStartBlocked, blocked.State);
        Assert.Equal(sourceIssue, Assert.Single(blocked.ReloadIssues));
        Assert.True(recovered.Succeeded);
        Assert.False(recovered.RetainedLastKnownGood);
        Assert.NotNull(recovered.Snapshot);
        Assert.Equal(
            CanonicalSupportMatrixCatalogState.Current,
            catalog.Query().State);
        Assert.True(catalog.Resolve(Route.RouteId).Succeeded);
    }

    /// <summary>Duplicate exact routes reject the complete candidate instead of selecting a winner.</summary>
    [Fact]
    public void DuplicateRouteRejectsCompleteCandidate()
    {
        CanonicalCapabilityDefinition definition = CreateDefinition(CreateCompiledComposition());
        var candidate = new CanonicalCapabilityCatalogCandidate(
            "canonical-capability-catalog",
            "1.0.0",
            new string('a', 64),
            [definition, definition]);
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(CapabilityCatalogLoadResult.Success(candidate)));

        CapabilityCatalogReloadResult result =
            catalog.Reload(TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Snapshot);
        Assert.Equal(
            CapabilityCatalogIssueCodes.InvalidCandidate,
            Assert.Single(result.Issues).Code);
    }

    /// <summary>Missing evidence stays resolvable while certification reports the inconsistency.</summary>
    [Fact]
    public void MissingEvidenceDoesNotRewriteBuildAdmission()
    {
        CanonicalCapabilityDefinition definition = CreateDefinition(
            CreateCompiledComposition(),
            evidenceStatus: CapabilityEvidenceStatus.Missing);
        var candidate = new CanonicalCapabilityCatalogCandidate(
            "canonical-capability-catalog",
            "1.0.0",
            new string('a', 64),
            [definition]);
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(
                CapabilityCatalogLoadResult.Success(candidate)));

        CapabilityCatalogReloadResult reload =
            catalog.Reload(TestContext.Current.CancellationToken);
        CapabilityResolutionResult resolution = catalog.Resolve(Route.RouteId);

        Assert.True(reload.Succeeded);
        Assert.True(resolution.Succeeded);
        Assert.Equal(
            CapabilityCatalogIssueCodes.SupportedWithoutEvidence,
            Assert.Single(reload.Snapshot!.CertificationIssues).Code);
    }

    /// <summary>Runtime requirements are references selected by the compiled plan, not re-inferred profile facts.</summary>
    [Fact]
    public void RuntimeDependencyRequestUsesOnlyCompiledExternalProcessorReferences()
    {
        CanonicalCapabilityDefinition definition = CreateDefinition(
            CreateCompiledCompositionWithExternalProcessor());
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(
                CapabilityCatalogLoadResult.Success(
                    new CanonicalCapabilityCatalogCandidate(
                        "canonical-capability-catalog",
                        "1.0.0",
                        new string('a', 64),
                        [definition]))));
        CapabilityCatalogReloadResult reload =
            catalog.Reload(TestContext.Current.CancellationToken);
        ResolvedCapability capability = catalog.Resolve(Route.RouteId).Capability!;

        var revision = new AuthoringRevision(17);
        var request =
            RuntimeDependencyReadinessRequest.FromResolvedCapability(capability, revision);
        var admission =
            CapabilityAdmissionSnapshot.FromResolvedCapability(capability, revision);

        Assert.Equal(Route.RouteId, request.RouteId);
        Assert.Equal(capability.CapabilityFingerprint, request.CapabilityFingerprint);
        Assert.Equal(reload.Snapshot!.ResolutionToken, request.ResolutionToken);
        Assert.Equal(revision, request.AuthoringRevision);
        Assert.Equal(revision, admission.AuthoringRevision);
        Assert.Equal(capability.ExecutionAdmitted, admission.ExecutionAdmitted);
        Assert.Equal(capability.Evidence.Value, admission.EvidenceStatus);
        Assert.Equal(capability.Publication.Value, admission.PublicationStatus);
        ExternalProcessorDependencyReference dependency = Assert.Single(request.Dependencies);
        Assert.Equal("crc-worker", dependency.ProcessorId);
        Assert.Equal("nvt-crc-worker", dependency.ToolBindingId);
    }

    /// <summary>Report metadata cannot cross either capability or compilation identity.</summary>
    [Theory]
    [InlineData("capability")]
    [InlineData("compilation")]
    [InlineData("instance")]
    public void RunRequestRejectsResolvedCapabilityFingerprintDrift(string drift)
    {
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(
                CapabilityCatalogLoadResult.Success(CreateCandidate())));
        _ = catalog.Reload(TestContext.Current.CancellationToken);
        ResolvedCapability capability = catalog.Resolve(Route.RouteId).Capability!;
        CompiledComposition composition = drift switch
        {
            "capability" => CreateCompiledComposition()
                .BindCapabilityFingerprint(new string('b', 64)),
            "compilation" => CreateCompiledCompositionWithExternalProcessor()
                .BindCapabilityFingerprint(capability.CapabilityFingerprint),
            "instance" => CreateCompiledComposition()
                .BindCapabilityFingerprint(capability.CapabilityFingerprint),
            _ => throw new InvalidOperationException($"Unknown drift '{drift}'."),
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new CompositionRunRequest(
                "capability-fingerprint-drift",
                composition,
                [],
                composition.V2Details.OutputNamingRequirement.FileNameTemplate,
                resolvedCapability: capability));

        Assert.Equal("resolvedCapability", exception.ParamName);
    }

    /// <summary>An explicit unavailable authoring decision blocks the exact route.</summary>
    [Fact]
    public void ResolveRejectsUnavailableAuthoring()
    {
        CanonicalCapabilityDefinition definition = CreateDefinition(
            CreateCompiledComposition(),
            authoringAvailability:
                CapabilityAuthoringAvailability.Unavailable);
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(
                CapabilityCatalogLoadResult.Success(
                    new CanonicalCapabilityCatalogCandidate(
                        "canonical-capability-catalog",
                        "1.0.0",
                        new string('a', 64),
                        [definition]))));
        _ = catalog.Reload(TestContext.Current.CancellationToken);

        CapabilityResolutionResult resolution = catalog.Resolve(Route.RouteId);

        Assert.False(resolution.Succeeded);
        Assert.Equal(
            CapabilityCatalogIssueCodes.AuthoringUnavailable,
            resolution.Issue!.Code);
    }

    /// <summary>Read-only metadata lookup retains the publication plan without reopening authoring.</summary>
    [Fact]
    public void ResolveUniqueMetadataPlanIgnoresAuthoringWithoutReturningExecutionAuthority()
    {
        CanonicalCapabilityDefinition definition = CreateDefinition(
            CreateCompiledComposition(),
            authoringAvailability:
                CapabilityAuthoringAvailability.Unavailable);
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(
                CapabilityCatalogLoadResult.Success(
                    new CanonicalCapabilityCatalogCandidate(
                        "canonical-capability-catalog",
                        "1.0.0",
                        new string('a', 64),
                        [definition]))));
        CapabilityCatalogReloadResult reload =
            catalog.Reload(TestContext.Current.CancellationToken);

        CapabilityResolutionResult authoring = catalog.ResolveUniqueRoute(
            "NT51929",
            "standard-merge",
            "selector-free",
            outputCapacity: 8);
        MetadataPlanResolutionResult metadata = catalog.ResolveUniqueMetadataPlan(
            "NT51929",
            "standard-merge",
            "selector-free",
            outputCapacity: 8);

        Assert.False(authoring.Succeeded);
        Assert.Equal(
            CapabilityCatalogIssueCodes.AuthoringUnavailable,
            authoring.Issue!.Code);
        Assert.True(metadata.Succeeded);
        Assert.NotNull(metadata.MetadataPlan);
        Assert.Equal(
            reload.Snapshot!.ResolutionToken,
            metadata.MetadataPlan.ResolutionToken);
    }

    /// <summary>Read-only metadata lookup uses exact capacity and fails closed on ambiguous axes.</summary>
    [Fact]
    public void ResolveUniqueMetadataPlanUsesCapacityAndRejectsAmbiguity()
    {
        var alternateRoute = new CapabilityRouteIdentity(
            "NT51929",
            "standard-merge",
            "selector-free",
            "nt51929-standard-merge-alternate");
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(
                CapabilityCatalogLoadResult.Success(
                    new CanonicalCapabilityCatalogCandidate(
                        "canonical-capability-catalog",
                        "1.0.0",
                        new string('a', 64),
                        [
                            CreateDefinition(CreateCompiledComposition()),
                            CreateDefinition(
                                CreateCompiledComposition(
                                    alternateRoute.MapVariant,
                                    outputCapacity: 16),
                                alternateRoute),
                        ]))));
        CapabilityCatalogReloadResult reload =
            catalog.Reload(TestContext.Current.CancellationToken);

        MetadataPlanResolutionResult ambiguous = catalog.ResolveUniqueMetadataPlan(
            "NT51929",
            "standard-merge",
            "selector-free");
        MetadataPlanResolutionResult exact = catalog.ResolveUniqueMetadataPlan(
            "NT51929",
            "standard-merge",
            "selector-free",
            outputCapacity: 16);
        MetadataPlanResolutionResult unavailable = catalog.ResolveUniqueMetadataPlan(
            "NT51950",
            "standard-merge",
            "selector-free",
            outputCapacity: 16);
        MetadataPlanResolutionResult wrongWorkflow = catalog.ResolveUniqueMetadataPlan(
            "NT51929",
            "ab-merge",
            "selector-free",
            outputCapacity: 16);
        MetadataPlanResolutionResult wrongCount = catalog.ResolveUniqueMetadataPlan(
            "NT51929",
            "standard-merge",
            "2-ic",
            outputCapacity: 16);

        Assert.False(ambiguous.Succeeded);
        Assert.Equal(
            CapabilityCatalogIssueCodes.RouteAmbiguous,
            ambiguous.Issue!.Code);
        Assert.True(exact.Succeeded);
        Assert.Equal(
            reload.Snapshot!.ResolutionToken,
            exact.MetadataPlan!.ResolutionToken);
        Assert.False(unavailable.Succeeded);
        Assert.Equal(
            CapabilityCatalogIssueCodes.RouteUnavailable,
            unavailable.Issue!.Code);
        Assert.Equal(
            CapabilityCatalogIssueCodes.RouteUnavailable,
            wrongWorkflow.Issue!.Code);
        Assert.Equal(
            CapabilityCatalogIssueCodes.RouteUnavailable,
            wrongCount.Issue!.Code);
    }

    /// <summary>Selection without a map variant fails closed when more than one map is published.</summary>
    [Fact]
    public void ResolveUniqueRouteRejectsAmbiguousMapVariants()
    {
        var alternateRoute = new CapabilityRouteIdentity(
            "NT51929",
            "standard-merge",
            "selector-free",
            "nt51929-standard-merge-alternate");
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(
                CapabilityCatalogLoadResult.Success(
                    new CanonicalCapabilityCatalogCandidate(
                        "canonical-capability-catalog",
                        "1.0.0",
                        new string('a', 64),
                        [
                            CreateDefinition(CreateCompiledComposition()),
                            CreateDefinition(
                                CreateCompiledComposition(alternateRoute.MapVariant),
                                alternateRoute),
                        ]))));
        _ = catalog.Reload(TestContext.Current.CancellationToken);

        CapabilityResolutionResult resolution = catalog.ResolveUniqueRoute(
            "NT51929",
            "standard-merge",
            "selector-free");

        Assert.False(resolution.Succeeded);
        Assert.Equal(
            CapabilityCatalogIssueCodes.RouteAmbiguous,
            resolution.Issue!.Code);
    }

    /// <summary>An empty candidate is rejected before it can replace the current snapshot.</summary>
    [Fact]
    public void ReloadRejectsEmptyCandidate()
    {
        var candidate = new CanonicalCapabilityCatalogCandidate(
            "canonical-capability-catalog",
            "1.0.0",
            new string('a', 64),
            []);
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(
                CapabilityCatalogLoadResult.Success(candidate)));

        CapabilityCatalogReloadResult reload =
            catalog.Reload(TestContext.Current.CancellationToken);

        Assert.False(reload.Succeeded);
        Assert.Equal(
            CapabilityCatalogIssueCodes.InvalidCandidate,
            Assert.Single(reload.Issues).Code);
    }

    private static CanonicalCapabilityCatalogCandidate CreateCandidate()
    {
        CanonicalCapabilityDefinition definition = CreateDefinition(CreateCompiledComposition());
        return new CanonicalCapabilityCatalogCandidate(
            "canonical-capability-catalog",
            "1.0.0",
            new string('a', 64),
            [definition]);
    }

    private static CanonicalCapabilityDefinition CreateDefinition(
        CompiledComposition composition,
        CapabilityRouteIdentity? route = null,
        string? authoringFingerprint = null,
        string? publicationFingerprint = null,
        string? evidenceFingerprint = null,
        CapabilityAuthoringAvailability authoringAvailability =
            CapabilityAuthoringAvailability.Available,
        CapabilityEvidenceStatus evidenceStatus =
            CapabilityEvidenceStatus.DirectGolden)
    {
        string fingerprint = composition.CompilationFingerprint;
        CapabilityRouteIdentity effectiveRoute = route ?? Route;
        return new CanonicalCapabilityDefinition(
            effectiveRoute,
            fingerprint,
            composition,
            new PinnedCapabilityDecision<CapabilityAuthoringAvailability>(
                "nt51929-standard-merge-authoring",
                effectiveRoute.RouteId,
                authoringFingerprint ?? fingerprint,
                authoringAvailability,
                "owner-approved:#173"),
            new PinnedCapabilityDecision<CapabilityPublicationStatus>(
                "nt51929-standard-merge-publication",
                effectiveRoute.RouteId,
                publicationFingerprint ?? fingerprint,
                CapabilityPublicationStatus.Supported,
                "owner-approved:#173"),
            new PinnedCapabilityDecision<CapabilityEvidenceStatus>(
                "nt51929-standard-merge-golden",
                effectiveRoute.RouteId,
                evidenceFingerprint ?? fingerprint,
                evidenceStatus,
                "canonical-golden:nt51929-gen-flash"));
    }

    private static CompiledComposition CreateCompiledComposition(
        string? mapId = null,
        long outputCapacity = 8)
    {
        AddressSpace[] addressSpaces =
        [
            new("dp-input", 4, AddressSpaceMutability.Immutable),
            new("tp-input", 4, AddressSpaceMutability.Immutable),
            new("output-image", outputCapacity, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", outputCapacity, 0),
            addressSpaces,
            [
                CompositionOperation.CopyRange(
                    "copy-tp",
                    100,
                    "tp-input",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(4, 4),
                    OverlapPolicy.Reject,
                    "Copy TP."),
                CompositionOperation.CopyRange(
                    "copy-dp",
                    200,
                    "dp-input",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(0, 4),
                    OverlapPolicy.Reject,
                    "Copy DP."),
            ]);
        return CompiledCompositionTestFactory.Create(
            plan,
            new TestCompiledCompositionIdentity(
                "synthetic-nt51929-standard-merge",
                "1.0.0",
                "NT51929",
                "standard-merge",
                "standard-merge",
                CompositionKind.Merge),
            "synthetic-nt51929-standard-merge.bin",
            null,
            mapId: mapId ?? Route.MapVariant);
    }

    private static CompiledComposition CreateCompiledCompositionWithExternalProcessor()
    {
        AddressSpace[] addressSpaces =
        [
            new("processor-input", 8, AddressSpaceMutability.Immutable),
            new("output-image", 8, AddressSpaceMutability.Mutable),
        ];
        var invocation = new ExternalProcessorInvocation(
            "crc-worker",
            "nvt-crc-worker",
            [new ByteRange(0, 8)],
            [new ByteRange(0, 8)]);
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 8, 0),
            addressSpaces,
            [
                CompositionOperation.RunExternalProcessor(
                    "run-crc-worker",
                    100,
                    "output-image",
                    new ByteRange(0, 8),
                    invocation,
                    OverlapPolicy.Reject,
                    "Run the compiled CRC worker."),
            ]);
        return CompiledCompositionTestFactory.Create(
            plan,
            new TestCompiledCompositionIdentity(
                "synthetic-nt51929-standard-merge-with-processor",
                "1.0.0",
                "NT51929",
                "standard-merge",
                "standard-merge",
                CompositionKind.Merge),
            "synthetic-nt51929-standard-merge.bin",
            null,
            mapId: Route.MapVariant);
    }

    private sealed class QueueCapabilitySource(
        params CapabilityCatalogLoadResult[] results) : ICanonicalCapabilityCatalogSource
    {
        private readonly Queue<CapabilityCatalogLoadResult> _results = new(results);

        public CapabilityCatalogLoadResult Load(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _results.Dequeue();
        }
    }
}

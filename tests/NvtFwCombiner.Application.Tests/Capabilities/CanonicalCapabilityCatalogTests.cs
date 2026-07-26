using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Capabilities;

/// <summary>Tests the Application-owned canonical capability snapshot and reload boundary.</summary>
public sealed class CanonicalCapabilityCatalogTests
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

        Assert.False(reload.Succeeded);
        Assert.False(reload.RetainedLastKnownGood);
        Assert.Null(reload.Snapshot);
        Assert.False(resolution.Succeeded);
        Assert.Equal(CapabilityCatalogIssueCodes.CatalogUnavailable, resolution.Issue!.Code);
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
        string? authoringFingerprint = null,
        string? publicationFingerprint = null,
        string? evidenceFingerprint = null,
        CapabilityEvidenceStatus evidenceStatus =
            CapabilityEvidenceStatus.DirectGolden)
    {
        string fingerprint = composition.CompilationFingerprint;
        return new CanonicalCapabilityDefinition(
            Route,
            composition,
            new PinnedCapabilityDecision<CapabilityAuthoringAvailability>(
                "nt51929-standard-merge-authoring",
                Route.RouteId,
                authoringFingerprint ?? fingerprint,
                CapabilityAuthoringAvailability.Available,
                "owner-approved:#173"),
            new PinnedCapabilityDecision<CapabilityPublicationStatus>(
                "nt51929-standard-merge-publication",
                Route.RouteId,
                publicationFingerprint ?? fingerprint,
                CapabilityPublicationStatus.Supported,
                "owner-approved:#173"),
            new PinnedCapabilityDecision<CapabilityEvidenceStatus>(
                "nt51929-standard-merge-golden",
                Route.RouteId,
                evidenceFingerprint ?? fingerprint,
                evidenceStatus,
                "canonical-golden:nt51929-gen-flash"));
    }

    private static CompiledComposition CreateCompiledComposition()
    {
        AddressSpace[] addressSpaces =
        [
            new("dp-input", 4, AddressSpaceMutability.Immutable),
            new("tp-input", 4, AddressSpaceMutability.Immutable),
            new("output-image", 8, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 8, 0),
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
        return CompiledComposition.CreateLegacy(
            plan,
            new LegacyCompiledCompositionIdentity(
                "synthetic-nt51929-standard-merge",
                "1.0.0",
                "NT51929",
                "standard-merge",
                "standard-merge",
                CompositionKind.Merge),
            "synthetic-nt51929-standard-merge.bin",
            CompiledIcNumberPolicy.NotApplicable);
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

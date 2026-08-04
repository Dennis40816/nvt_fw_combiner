using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Capabilities;

public sealed partial class CanonicalCapabilityCatalogTests
{
    /// <summary>The reporting query projects fixed and authoring-compiled routes from one canonical snapshot.</summary>
    [Fact]
    public void SupportMatrixProjectsFixedAndDynamicCanonicalRoutes()
    {
        CanonicalCapabilityDefinition fixedDefinition =
            CreateDefinition(CreateCompiledComposition());
        CanonicalDynamicCapabilityDefinition dynamicDefinition =
            CreateDynamicDefinition(
                CapabilityPublicationStatus.Candidate,
                CapabilityEvidenceStatus.ContractOnly);
        CapabilityRouteIdentity dynamicIdentity = dynamicDefinition.Identity;
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(CapabilityCatalogLoadResult.Success(
                new CanonicalCapabilityCatalogCandidate(
                    "canonical-capability-catalog",
                    "2.0.0",
                    new string('b', 64),
                    [fixedDefinition],
                    [dynamicDefinition]))));

        CapabilityCatalogReloadResult reload = catalog.Reload(
            TestContext.Current.CancellationToken);
        CanonicalSupportMatrixQueryResult result =
            CanonicalSupportMatrixQuery.Project(reload);

        Assert.Equal(CanonicalSupportMatrixCatalogState.Current, result.State);
        Assert.False(result.IsStale);
        Assert.Empty(result.ReloadIssues);
        CanonicalSupportMatrixSnapshot matrix = Assert.IsType<CanonicalSupportMatrixSnapshot>(result.Matrix);
        Assert.Equal("2.0.0", matrix.CatalogVersion);
        Assert.Equal(new string('b', 64), matrix.SourceSha256);
        Assert.Equal(reload.Snapshot!.ResolutionToken, matrix.ResolutionToken);
        Assert.Equal(2, matrix.Rows.Count);

        CanonicalSupportMatrixRow fixedRow = matrix.Rows.Single(
            row => row.Identity == Route);
        Assert.Equal(
            CanonicalSupportMatrixExecutionState.Admitted,
            fixedRow.ExecutionState);
        Assert.Same(reload.Snapshot.Capabilities.Single().Authoring, fixedRow.Authoring);
        Assert.Empty(fixedRow.Blockers);

        CanonicalSupportMatrixRow dynamicRow = matrix.Rows.Single(
            row => row.Identity == dynamicIdentity);
        Assert.Equal(
            CanonicalSupportMatrixExecutionState.RequiresAuthoringCompilation,
            dynamicRow.ExecutionState);
        Assert.Equal(CapabilityPublicationStatus.Candidate, dynamicRow.Publication.Value);
        Assert.Equal(CapabilityEvidenceStatus.ContractOnly, dynamicRow.Evidence.Value);
        Assert.Equal("owner-approved:#207", dynamicRow.Authoring.SourceReference);
        Assert.Empty(dynamicRow.Blockers);
    }

    /// <summary>Certification inconsistency applies equally to dynamic exact routes.</summary>
    [Fact]
    public void SupportMatrixBlocksSupportedDynamicRouteWithoutEvidence()
    {
        CanonicalDynamicCapabilityDefinition dynamicDefinition =
            CreateDynamicDefinition(
                CapabilityPublicationStatus.Supported,
                CapabilityEvidenceStatus.Missing);
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(CapabilityCatalogLoadResult.Success(
                new CanonicalCapabilityCatalogCandidate(
                    "canonical-capability-catalog",
                    "2.0.0",
                    new string('b', 64),
                    [],
                    [dynamicDefinition]))));

        CanonicalSupportMatrixQueryResult result = CanonicalSupportMatrixQuery.Project(
            catalog.Reload(TestContext.Current.CancellationToken));

        CanonicalSupportMatrixRow row = Assert.Single(result.Matrix!.Rows);
        CanonicalSupportMatrixBlocker blocker = Assert.Single(row.Blockers);
        Assert.Equal(
            CanonicalSupportMatrixBlockerKind.CertificationInconsistency,
            blocker.Kind);
        Assert.Equal(
            CapabilityCatalogIssueCodes.SupportedWithoutEvidence,
            blocker.Code);
        Assert.Equal(
            dynamicDefinition.Evidence.SourceReference,
            blocker.SourceReference);
    }

    /// <summary>Unavailable authoring and certification inconsistency remain typed independent blockers.</summary>
    [Fact]
    public void SupportMatrixRetainsIndependentTypedBlockers()
    {
        CanonicalCapabilityDefinition definition = CreateDefinition(
            CreateCompiledComposition(),
            authoringAvailability: CapabilityAuthoringAvailability.Unavailable,
            evidenceStatus: CapabilityEvidenceStatus.Missing);
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(CapabilityCatalogLoadResult.Success(
                new CanonicalCapabilityCatalogCandidate(
                    "canonical-capability-catalog",
                    "1.0.0",
                    new string('a', 64),
                    [definition]))));

        CanonicalSupportMatrixQueryResult result = CanonicalSupportMatrixQuery.Project(
            catalog.Reload(TestContext.Current.CancellationToken));

        CanonicalSupportMatrixRow row = Assert.Single(result.Matrix!.Rows);
        Assert.Equal(
            [
                CanonicalSupportMatrixBlockerKind.AuthoringUnavailable,
                CanonicalSupportMatrixBlockerKind.CertificationInconsistency,
            ],
            row.Blockers.Select(static blocker => blocker.Kind));
        Assert.Equal(
            CapabilityCatalogIssueCodes.AuthoringUnavailable,
            row.Blockers[0].Code);
        Assert.Equal(
            CapabilityCatalogIssueCodes.SupportedWithoutEvidence,
            row.Blockers[1].Code);
        Assert.Equal(
            definition.Authoring.SourceReference,
            row.Blockers[0].SourceReference);
        Assert.Equal(
            definition.Evidence.SourceReference,
            row.Blockers[1].SourceReference);
    }

    /// <summary>A failed reload retains the last immutable matrix and exposes typed stale provenance.</summary>
    [Fact]
    public void SupportMatrixReportsLastKnownGoodAfterReloadFailure()
    {
        var sourceIssue = new CapabilityCatalogIssue(
            CapabilityCatalogIssueCodes.SourceUnavailable,
            "Reload failed.");
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(
                CapabilityCatalogLoadResult.Success(CreateCandidate()),
                CapabilityCatalogLoadResult.Failure(sourceIssue)));
        CapabilityCatalogReloadResult first = catalog.Reload(
            TestContext.Current.CancellationToken);
        CapabilityCatalogReloadResult failed = catalog.Reload(
            TestContext.Current.CancellationToken);

        CanonicalSupportMatrixQueryResult result =
            CanonicalSupportMatrixQuery.Project(failed);

        Assert.True(first.Succeeded);
        Assert.False(failed.Succeeded);
        Assert.True(failed.RetainedLastKnownGood);
        Assert.Equal(
            CanonicalSupportMatrixCatalogState.LastKnownGood,
            result.State);
        Assert.True(result.IsStale);
        Assert.Same(first.Snapshot, failed.Snapshot);
        Assert.Equal(first.Snapshot!.ResolutionToken, result.Matrix!.ResolutionToken);
        Assert.Equal(sourceIssue, Assert.Single(result.ReloadIssues));
    }

    /// <summary>A cold-start source failure exposes no fabricated matrix.</summary>
    [Fact]
    public void SupportMatrixFailsClosedWithoutColdStartSnapshot()
    {
        var sourceIssue = new CapabilityCatalogIssue(
            CapabilityCatalogIssueCodes.SourceUnavailable,
            "Initial load failed.");
        var catalog = new CanonicalCapabilityCatalog(
            new QueueCapabilitySource(
                CapabilityCatalogLoadResult.Failure(sourceIssue)));

        CanonicalSupportMatrixQueryResult result = CanonicalSupportMatrixQuery.Project(
            catalog.Reload(TestContext.Current.CancellationToken));

        Assert.Equal(
            CanonicalSupportMatrixCatalogState.ColdStartBlocked,
            result.State);
        Assert.Null(result.Matrix);
        Assert.False(result.IsStale);
        Assert.Equal(sourceIssue, Assert.Single(result.ReloadIssues));
    }

    private static CanonicalDynamicCapabilityDefinition CreateDynamicDefinition(
        CapabilityPublicationStatus publication,
        CapabilityEvidenceStatus evidence)
    {
        CapabilityRouteIdentity identity = new(
            "NT51929",
            "standard-merge",
            "selector-free",
            "nt51929-standard-merge-512k");
        CompiledComposition composition = CreateCompiledComposition(
            identity.MapVariant);
        var contract = CanonicalCapabilityCompilationContract.FromCompiled(
            identity,
            composition);
        string fingerprint = CapabilityDefinitionFingerprint.Compute(
            identity,
            contract.ProfileId,
            contract.ProfileVersion,
            contract.TrustedDefinitionSha256,
            contract.AllowedMapVariantIds,
            contract.CompilerSemanticId,
            contract.SemanticBindingIds);
        return new CanonicalDynamicCapabilityDefinition(
            identity,
            fingerprint,
            contract,
            Decision(
                identity,
                fingerprint,
                CapabilityAuthoringAvailability.Available,
                "dynamic-authoring",
                "owner-approved:#207"),
            Decision(
                identity,
                fingerprint,
                publication,
                "dynamic-publication",
                "owner-approved:#207"),
            Decision(
                identity,
                fingerprint,
                evidence,
                "dynamic-evidence",
                "canonical-contract:#194"));
    }

    private static PinnedCapabilityDecision<TValue> Decision<TValue>(
        CapabilityRouteIdentity identity,
        string fingerprint,
        TValue value,
        string decisionId,
        string sourceReference)
        where TValue : struct, Enum
    {
        return new PinnedCapabilityDecision<TValue>(
            decisionId,
            identity.RouteId,
            fingerprint,
            value,
            sourceReference);
    }
}

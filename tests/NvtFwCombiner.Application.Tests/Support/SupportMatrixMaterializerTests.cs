using NvtFwCombiner.Application.Support;

namespace NvtFwCombiner.Application.Tests.Support;

/// <summary>Contract tests for the non-authorizing Support Matrix materializer.</summary>
public sealed class SupportMatrixMaterializerTests
{
    /// <summary>Verifies policy and validated exact evidence remain independently materialized with strongest precedence.</summary>
    [Fact]
    public void MaterializesIndependentPolicyAndStrongestExactEvidence()
    {
        SupportPublicationPolicySnapshot policy = Policy(
            new SupportPublicationDecision(
                "candidate-route",
                "nt51950-ab-merge-single",
                SupportPublicationStatus.Candidate,
                Provenance()));
        SupportRouteDescriptor route = Route();

        SupportMatrix matrix = SupportMatrixMaterializer.Materialize(
            policy,
            [route],
            EvidenceCatalog(new SupportEvidenceDeclaration(
                "golden:vector",
                SupportEvidenceStatus.DirectGolden,
                route.RouteId)));

        SupportMatrixRow row = Assert.Single(matrix.Rows);
        Assert.True(matrix.IsMigrationReady);
        Assert.Equal(SupportPublicationStatus.Candidate, row.PublicationStatus);
        Assert.Equal("candidate-route", row.PublicationDecision!.DecisionId);
        Assert.Equal(SupportEvidenceStatus.DirectGolden, row.Evidence.Status);
        Assert.Equal("golden:vector", row.Evidence.SourceDeclarationId);
    }

    /// <summary>Verifies unmatched policy rows and unclassified routes keep the migration gate fail-closed.</summary>
    [Fact]
    public void FailsMigrationForUnclassifiedSelectableRouteAndUnresolvedPolicyRoute()
    {
        SupportPublicationPolicySnapshot policy = Policy(
            new SupportPublicationDecision(
                "other-route",
                "nt51919-general-replace-generic",
                SupportPublicationStatus.TestOnly,
                Provenance()));

        SupportMatrix matrix = SupportMatrixMaterializer.Materialize(policy, [Route()], EvidenceCatalog());

        Assert.False(matrix.IsMigrationReady);
        Assert.Contains(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.UnclassifiedRoute);
        Assert.Contains(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.PolicyRouteUnresolved);
    }

    /// <summary>Verifies an explicit owner unclassified decision is not confused with a missing decision.</summary>
    [Fact]
    public void RetainsExplicitUnclassifiedDecisionWithoutReportingAMissingPolicyRow()
    {
        SupportRouteDescriptor route = Route();

        SupportMatrix matrix = SupportMatrixMaterializer.Materialize(
            Policy(new SupportPublicationDecision(
                "explicit-unclassified",
                route.RouteId,
                SupportPublicationStatus.Unclassified,
                Provenance())),
            [route],
            EvidenceCatalog());

        SupportMatrixRow row = Assert.Single(matrix.Rows);
        Assert.Equal(SupportPublicationStatus.Unclassified, row.PublicationStatus);
        Assert.Equal("explicit-unclassified", row.PublicationDecision!.DecisionId);
        Assert.DoesNotContain(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.UnclassifiedRoute);
    }

    /// <summary>Verifies selectable routes cannot be represented as execution-inadmissible exact routes.</summary>
    [Fact]
    public void FailsMigrationForSelectableButNonExecutableExactRoute()
    {
        SupportMatrix matrix = SupportMatrixMaterializer.Materialize(
            Policy(new SupportPublicationDecision(
                "candidate-route",
                "nt51950-ab-merge-single",
                SupportPublicationStatus.Candidate,
                Provenance())),
            [Route(executionAdmitted: false)],
            EvidenceCatalog());

        Assert.Contains(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.SelectableNotExecutable);
    }

    /// <summary>Verifies an exact route with unresolved authoring remains a migration blocker.</summary>
    [Fact]
    public void FailsMigrationForExactRouteWithUnknownAuthoringAvailability()
    {
        SupportRouteDescriptor route = Route(authoringAvailability: SupportAuthoringAvailability.Unknown);

        SupportMatrix matrix = SupportMatrixMaterializer.Materialize(
            Policy(Decision(route.RouteId)),
            [route],
            EvidenceCatalog());

        Assert.Contains(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.AuthoringRouteUnresolved &&
            diagnostic.Subject == route.RouteId);
    }

    /// <summary>Verifies execution-only routes require an explicit candidate, internal, or test-only policy state.</summary>
    [Fact]
    public void AllowsNonSelectableExecutionOnlyWhenPolicyTypesItAsCandidateInternalOrTestOnly()
    {
        SupportRouteDescriptor hidden = Route(authoringAvailability: SupportAuthoringAvailability.Unavailable);
        SupportMatrix candidate = SupportMatrixMaterializer.Materialize(
            Policy(new SupportPublicationDecision(
                "candidate-route",
                hidden.RouteId,
                SupportPublicationStatus.Candidate,
                Provenance())),
            [hidden],
            EvidenceCatalog());
        SupportMatrix supported = SupportMatrixMaterializer.Materialize(
            Policy(new SupportPublicationDecision(
                "supported-route",
                hidden.RouteId,
                SupportPublicationStatus.Supported,
                Provenance())),
            [hidden],
            EvidenceCatalog());

        Assert.True(candidate.IsMigrationReady);
        Assert.Contains(supported.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.ExecutableNotSelectable);
    }

    /// <summary>Verifies a coarse source is recorded as unresolved rather than fabricated into a route.</summary>
    [Fact]
    public void PreservesUnresolvedCatalogScopesRatherThanInventingAnExactRoute()
    {
        SupportMatrix matrix = SupportMatrixMaterializer.Materialize(
            Policy(),
            [],
            EvidenceCatalog(),
            [new SupportUnresolvedScope(
                "ic-support:NT51919:general-replace",
                "NT51919",
                "general-replace",
                "The source has no exact IC-count or map binding.")]);

        Assert.Empty(matrix.Rows);
        Assert.False(matrix.IsMigrationReady);
        Assert.Contains(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.SourceScopeUnresolved);
    }

    /// <summary>Verifies an alias needs an exact direct-golden target and an applicable source fact scope.</summary>
    [Fact]
    public void ResolvesOnlyAliasesWhoseExactTargetAndFactScopeAreValidated()
    {
        SupportRouteDescriptor source = Route();
        SupportRouteDescriptor target = Route(
            routeId: "nt51929-ab-merge-single",
            icId: "NT51929",
            mapVariant: "nt51929-ab-merge-512k");
        SupportEvidenceFactScope scope = new(
            "shared-dlm-crc-single",
            source.IcId,
            source.WorkflowId,
            [source.IcCountVariant],
            [source.MapVariant]);
        SupportEvidenceCatalogSnapshot evidence = EvidenceCatalog(
            new SupportEvidenceDeclaration("golden:target", SupportEvidenceStatus.DirectGolden, target.RouteId),
            new SupportEvidenceDeclaration(
                "alias:source",
                SupportEvidenceStatus.ApprovedAlias,
                source.RouteId,
                target.RouteId,
                scope));

        SupportMatrix matrix = SupportMatrixMaterializer.Materialize(
            Policy(Decision(source.RouteId)),
            [source, target],
            evidence);

        SupportMatrixRow sourceRow = Assert.Single(matrix.Rows, row => row.Route.RouteId == source.RouteId);
        Assert.Equal(SupportEvidenceStatus.ApprovedAlias, sourceRow.Evidence.Status);
        Assert.Equal("alias:source", sourceRow.Evidence.SourceDeclarationId);
        Assert.Equal(target.RouteId, sourceRow.Evidence.TargetRouteId);
        Assert.Equal(scope.FactScopeId, sourceRow.Evidence.FactScopeId);
    }

    /// <summary>Verifies a mismatched fact scope cannot promote a route to approved-alias evidence.</summary>
    [Fact]
    public void RejectsAliasWhoseFactScopeDoesNotCoverTheSourceMapVariant()
    {
        SupportRouteDescriptor source = Route();
        SupportRouteDescriptor target = Route(
            routeId: "nt51929-ab-merge-single",
            icId: "NT51929",
            mapVariant: "nt51929-ab-merge-512k");
        SupportEvidenceCatalogSnapshot evidence = EvidenceCatalog(
            new SupportEvidenceDeclaration("golden:target", SupportEvidenceStatus.DirectGolden, target.RouteId),
            new SupportEvidenceDeclaration(
                "alias:wrong-map",
                SupportEvidenceStatus.ApprovedAlias,
                source.RouteId,
                target.RouteId,
                new SupportEvidenceFactScope(
                    "shared-dlm-crc-wrong-map",
                    source.IcId,
                    source.WorkflowId,
                    [source.IcCountVariant],
                    ["other-map"])));

        _ = Assert.Throws<ArgumentException>(() => SupportMatrixMaterializer.Materialize(
            Policy(Decision(source.RouteId)),
            [source, target],
            evidence));
    }

    /// <summary>Verifies an unresolved alias target is rejected rather than classified as approved alias evidence.</summary>
    [Fact]
    public void RejectsAliasWhoseTargetIsNotAnExactCanonicalRoute()
    {
        SupportRouteDescriptor route = Route();
        SupportEvidenceCatalogSnapshot evidence = EvidenceCatalog(new SupportEvidenceDeclaration(
            "alias:missing-target",
            SupportEvidenceStatus.ApprovedAlias,
            route.RouteId,
            "missing-route",
            new SupportEvidenceFactScope(
                "scope",
                route.IcId,
                route.WorkflowId,
                [route.IcCountVariant],
                [route.MapVariant])));

        _ = Assert.Throws<ArgumentException>(() => SupportMatrixMaterializer.Materialize(
            Policy(Decision(route.RouteId)),
            [route],
            evidence));
    }

    /// <summary>Verifies a named execution source cannot fabricate direct-golden evidence without a declaration.</summary>
    [Fact]
    public void DoesNotInferDirectGoldenFromAnExecutionSourceName()
    {
        SupportRouteDescriptor route = Route(executionSourceId: "GoldenVerified:profile-promotion");

        SupportMatrix matrix = SupportMatrixMaterializer.Materialize(
            Policy(Decision(route.RouteId)),
            [route],
            EvidenceCatalog());

        Assert.Equal(SupportEvidenceStatus.ContractOnly, Assert.Single(matrix.Rows).Evidence.Status);
    }

    /// <summary>Verifies a publication provenance cannot claim an authority other than the exact owner decision.</summary>
    [Fact]
    public void RejectsPublicationDecisionWhoseAuthorityIsNotOwnerDecision()
    {
        SupportRouteDescriptor route = Route();
        SupportPublicationPolicySnapshot policy = Policy(new SupportPublicationDecision(
            "candidate-route",
            route.RouteId,
            SupportPublicationStatus.Candidate,
            new SupportPublicationProvenance("inferred", "2026-07-25", "test", "test")));

        _ = Assert.Throws<ArgumentException>(() => SupportMatrixMaterializer.Materialize(
            policy,
            [route],
            EvidenceCatalog()));
    }

    /// <summary>Verifies public policy, evidence, fact-scope, and result snapshots never retain caller-owned arrays.</summary>
    [Fact]
    public void CopiesAllPublicSnapshotCollectionsAtTheirBoundaries()
    {
        string[] supersededIds = ["old-decision"];
        var decision = new SupportPublicationDecision(
            "candidate-route",
            "nt51950-ab-merge-single",
            SupportPublicationStatus.Candidate,
            Provenance(),
            supersededIds);
        SupportPublicationDecision[] decisions = [decision];
        SupportPublicationPolicySnapshot policy = new(
            "support-publication-policy",
            "2.0.0",
            new string('a', 64),
            decisions,
            "1.0.0");
        string[] counts = ["single"];
        string[] maps = ["nt51950-ab-merge-512k"];
        SupportEvidenceFactScope scope = new("scope", "NT51950", "ab-merge", counts, maps);
        SupportEvidenceDeclaration[] declarations =
        [
            new SupportEvidenceDeclaration("golden:route", SupportEvidenceStatus.DirectGolden, decision.RouteId),
        ];
        SupportEvidenceCatalogSnapshot evidence = new("evidence", "1.0.0", "test", declarations);
        SupportMatrix matrix = SupportMatrixMaterializer.Materialize(policy, [Route()], evidence);

        supersededIds[0] = "mutated-decision";
        decisions[0] = new SupportPublicationDecision("mutated", decision.RouteId, SupportPublicationStatus.Internal, Provenance());
        counts[0] = "cascade";
        maps[0] = "mutated-map";
        declarations[0] = new SupportEvidenceDeclaration("mutated", SupportEvidenceStatus.SyntheticOracle, decision.RouteId);

        Assert.Equal("old-decision", policy.Decisions.Single().SupersedesDecisionIds.Single());
        Assert.Equal("candidate-route", policy.Decisions.Single().DecisionId);
        Assert.Equal("single", scope.IcCountVariants.Single());
        Assert.Equal("nt51950-ab-merge-512k", scope.MapVariants.Single());
        Assert.Equal("golden:route", evidence.Declarations.Single().DeclarationId);
        _ = Assert.Single(matrix.Rows);
    }

    private static SupportRouteDescriptor Route(
        string routeId = "nt51950-ab-merge-single",
        string icId = "NT51950",
        string mapVariant = "nt51950-ab-merge-512k",
        SupportAuthoringAvailability authoringAvailability = SupportAuthoringAvailability.Available,
        bool executionAdmitted = true,
        string executionSourceId = "v2:nt51950-ab-merge@0.2.0")
    {
        return new SupportRouteDescriptor(
            routeId,
            icId,
            "ab-merge",
            "single",
            mapVariant,
            authoringAvailability,
            executionAdmitted,
            $"authoring:{icId}:ab-merge",
            executionSourceId);
    }

    private static SupportEvidenceCatalogSnapshot EvidenceCatalog(params SupportEvidenceDeclaration[] declarations)
    {
        return new SupportEvidenceCatalogSnapshot(
            "test-evidence",
            "1.0.0",
            "test:canonical-evidence",
            declarations);
    }

    private static SupportPublicationPolicySnapshot Policy(params SupportPublicationDecision[] decisions)
    {
        return new SupportPublicationPolicySnapshot(
            "support-publication-policy",
            "1.0.0",
            new string('a', 64),
            decisions);
    }

    private static SupportPublicationProvenance Provenance()
    {
        return new SupportPublicationProvenance("owner-decision", "2026-07-25", "owner-chat:test", "test");
    }

    private static SupportPublicationDecision Decision(string routeId)
    {
        return new SupportPublicationDecision("candidate-route", routeId, SupportPublicationStatus.Candidate, Provenance());
    }
}

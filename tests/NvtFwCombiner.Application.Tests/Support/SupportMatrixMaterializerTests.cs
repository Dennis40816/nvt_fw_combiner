using NvtFwCombiner.Application.Support;

namespace NvtFwCombiner.Application.Tests.Support;

/// <summary>Contract tests for the non-authorizing Support Matrix materializer.</summary>
public sealed class SupportMatrixMaterializerTests
{
    /// <summary>Route ids always include the exact AB IC Count and map variant.</summary>
    [Fact]
    public void RouteIdentityDerivesStableExactPolicyReference()
    {
        var oneIc = new SupportRouteIdentity(
            "NT51950",
            "ab-merge",
            "1-ic",
            "nt51950-ab-merge-512k");
        var twoPlusIc = new SupportRouteIdentity(
            "NT51950",
            "ab-merge",
            "2-plus-ic",
            "nt51950-ab-merge-1024k");
        var generic = new SupportRouteIdentity(
            "NT51919",
            "general-merge",
            "not-applicable",
            "generic");

        Assert.Equal(
            "nt51950-ab-merge-1-ic-nt51950-ab-merge-512k",
            oneIc.RouteId);
        Assert.Equal(
            "nt51950-ab-merge-2-plus-ic-nt51950-ab-merge-1024k",
            twoPlusIc.RouteId);
        Assert.Equal("nt51919-general-merge-generic", generic.RouteId);
    }

    /// <summary>Integrity behavior is a stable identity axis without exposing unsafe text.</summary>
    [Fact]
    public void RouteIdentityIncludesStableIntegrityRouteHash()
    {
        var first = new SupportRouteIdentity(
            "NT51926",
            "ctrlram-replace",
            "1-ic",
            "nt51926-ctrlram-fw200-single",
            "nfc.nt51926.ctrlram-postbuild-v1:SingleChip");
        var same = new SupportRouteIdentity(
            "NT51926",
            "ctrlram-replace",
            "1-ic",
            "nt51926-ctrlram-fw200-single",
            "nfc.nt51926.ctrlram-postbuild-v1:SingleChip");
        var different = new SupportRouteIdentity(
            "NT51926",
            "ctrlram-replace",
            "1-ic",
            "nt51926-ctrlram-fw200-single",
            "nfc.nt51926.ctrlram-postbuild-v1:Cascade");

        Assert.Equal(first.RouteId, same.RouteId);
        Assert.NotEqual(first.RouteId, different.RouteId);
        Assert.Matches(
            "^nt51926-ctrlram-replace-1-ic-nt51926-ctrlram-fw200-single-integrity-[0-9a-f]{16}$",
            first.RouteId);
    }

    /// <summary>Policy and exact evidence remain independent with strongest precedence.</summary>
    [Fact]
    public void MaterializesIndependentPolicyAndStrongestExactEvidence()
    {
        SupportRouteDescriptor route = Route();
        SupportEvidenceCatalogSnapshot evidence = EvidenceCatalog(
            new SupportEvidenceDeclaration(
                "oracle:vector",
                SupportEvidenceStatus.SyntheticOracle,
                route.RouteId),
            new SupportEvidenceDeclaration(
                "golden:vector",
                SupportEvidenceStatus.DirectGolden,
                route.RouteId));

        SupportMatrix matrix = SupportMatrixMaterializer.Materialize(
            Policy(Decision(route.RouteId)),
            [route],
            evidence);

        SupportMatrixRow row = Assert.Single(matrix.Rows);
        Assert.True(matrix.IsMigrationReady);
        Assert.Equal(SupportPublicationStatus.Candidate, row.PublicationStatus);
        Assert.Equal("candidate-route", row.PublicationDecision!.DecisionId);
        Assert.Equal(SupportEvidenceStatus.DirectGolden, row.Evidence.Status);
        Assert.Equal("golden:vector", row.Evidence.SourceDeclarationId);
    }

    /// <summary>Unmatched policy rows and unclassified routes fail closed.</summary>
    [Fact]
    public void FailsMigrationForUnclassifiedRouteAndUnresolvedPolicyRoute()
    {
        SupportPublicationPolicySnapshot policy = Policy(
            new SupportPublicationDecision(
                "other-route",
                "nt51919-general-replace-generic",
                SupportPublicationStatus.TestOnly,
                Provenance()));

        SupportMatrix matrix = SupportMatrixMaterializer.Materialize(
            policy,
            [Route()],
            EvidenceCatalog());

        Assert.False(matrix.IsMigrationReady);
        Assert.Contains(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.UnclassifiedRoute);
        Assert.Contains(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.PolicyRouteUnresolved);
    }

    /// <summary>An explicit unclassified decision is retained and remains migration-blocking.</summary>
    [Fact]
    public void RetainsExplicitUnclassifiedDecision()
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
        Assert.Same(matrix.Policy.Decisions.Single(), row.PublicationDecision);
        Assert.Contains(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.UnclassifiedRoute &&
            diagnostic.Subject == route.RouteId);
        Assert.False(matrix.IsMigrationReady);
    }

    /// <summary>Selectable routes cannot be represented as execution-inadmissible.</summary>
    [Fact]
    public void FailsMigrationForSelectableButNonExecutableRoute()
    {
        SupportRouteDescriptor route = Route(executionAdmitted: false);

        SupportMatrix matrix = SupportMatrixMaterializer.Materialize(
            Policy(Decision(route.RouteId)),
            [route],
            EvidenceCatalog());

        Assert.Contains(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.SelectableNotExecutable);
        Assert.Equal(SupportEvidenceStatus.Missing, Assert.Single(matrix.Rows).Evidence.Status);
    }

    /// <summary>Unknown exact authoring availability remains a migration blocker.</summary>
    [Fact]
    public void FailsMigrationForUnknownAuthoringAvailability()
    {
        SupportRouteDescriptor route = Route(
            authoringAvailability: SupportAuthoringAvailability.Unknown);

        SupportMatrix matrix = SupportMatrixMaterializer.Materialize(
            Policy(Decision(route.RouteId)),
            [route],
            EvidenceCatalog());

        Assert.Contains(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.AuthoringRouteUnresolved &&
            diagnostic.Subject == route.RouteId);
    }

    /// <summary>Execution-only routes require an explicit non-public status.</summary>
    [Fact]
    public void AllowsExecutionOnlyRouteOnlyForNonPublicPolicyState()
    {
        SupportRouteDescriptor hidden = Route(
            authoringAvailability: SupportAuthoringAvailability.Unavailable);
        SupportMatrix candidate = SupportMatrixMaterializer.Materialize(
            Policy(Decision(hidden.RouteId)),
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

    /// <summary>A coarse source remains unresolved instead of creating a fabricated route.</summary>
    [Fact]
    public void PreservesUnresolvedCatalogScope()
    {
        SupportMatrix matrix = SupportMatrixMaterializer.Materialize(
            Policy(),
            [],
            EvidenceCatalog(),
            [new SupportUnresolvedScope(
                "ic-support:NT51919:general-replace",
                "NT51919",
                "general-replace",
                "The source has no exact IC Count or map binding.")]);

        Assert.Empty(matrix.Rows);
        Assert.False(matrix.IsMigrationReady);
        Assert.Contains(matrix.Diagnostics, diagnostic =>
            diagnostic.Code == SupportMatrixMaterializer.SourceScopeUnresolved);
    }

    /// <summary>An alias needs a direct-golden target and applicable fact scope.</summary>
    [Fact]
    public void ResolvesValidatedAlias()
    {
        SupportRouteDescriptor source = Route();
        SupportRouteDescriptor target = Route(
            icId: "NT51929",
            mapVariant: "nt51929-ab-merge-512k");
        SupportEvidenceFactScope scope = Scope(source, "shared-dlm-crc-single");
        SupportEvidenceCatalogSnapshot evidence = EvidenceCatalog(
            new SupportEvidenceDeclaration(
                "golden:target",
                SupportEvidenceStatus.DirectGolden,
                target.RouteId),
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

        SupportMatrixRow sourceRow = Assert.Single(
            matrix.Rows,
            row => row.Route.RouteId == source.RouteId);
        Assert.Equal(SupportEvidenceStatus.ApprovedAlias, sourceRow.Evidence.Status);
        Assert.Equal("alias:source", sourceRow.Evidence.SourceDeclarationId);
        Assert.Equal(target.RouteId, sourceRow.Evidence.TargetRouteId);
        Assert.Equal(scope.FactScopeId, sourceRow.Evidence.FactScopeId);
    }

    /// <summary>A mismatched fact scope cannot promote alias evidence.</summary>
    [Fact]
    public void RejectsAliasWithWrongMapScope()
    {
        SupportRouteDescriptor source = Route();
        SupportRouteDescriptor target = Route(
            icId: "NT51929",
            mapVariant: "nt51929-ab-merge-512k");
        SupportEvidenceCatalogSnapshot evidence = EvidenceCatalog(
            new SupportEvidenceDeclaration(
                "golden:target",
                SupportEvidenceStatus.DirectGolden,
                target.RouteId),
            new SupportEvidenceDeclaration(
                "alias:wrong-map",
                SupportEvidenceStatus.ApprovedAlias,
                source.RouteId,
                target.RouteId,
                new SupportEvidenceFactScope(
                    "shared-dlm-crc-wrong-map",
                    source.Identity.IcId,
                    source.Identity.WorkflowId,
                    [source.Identity.IcCountVariant],
                    ["other-map"])));

        _ = Assert.Throws<ArgumentException>(() =>
            SupportMatrixMaterializer.Materialize(
                Policy(Decision(source.RouteId)),
                [source, target],
                evidence));
    }

    /// <summary>An unresolved alias target is rejected.</summary>
    [Fact]
    public void RejectsAliasWithUnknownTarget()
    {
        SupportRouteDescriptor route = Route();
        SupportEvidenceCatalogSnapshot evidence = EvidenceCatalog(
            new SupportEvidenceDeclaration(
                "alias:missing-target",
                SupportEvidenceStatus.ApprovedAlias,
                route.RouteId,
                "missing-route",
                Scope(route, "scope")));

        _ = Assert.Throws<ArgumentException>(() =>
            SupportMatrixMaterializer.Materialize(
                Policy(Decision(route.RouteId)),
                [route],
                evidence));
    }

    /// <summary>Evidence declarations cannot name a route outside the exact catalog snapshot.</summary>
    [Fact]
    public void RejectsEvidenceForUnknownSourceRoute()
    {
        SupportRouteDescriptor route = Route();
        SupportEvidenceCatalogSnapshot evidence = EvidenceCatalog(
            new SupportEvidenceDeclaration(
                "golden:missing-route",
                SupportEvidenceStatus.DirectGolden,
                "missing-route"));

        _ = Assert.Throws<ArgumentException>(() =>
            SupportMatrixMaterializer.Materialize(
                Policy(Decision(route.RouteId)),
                [route],
                evidence));
    }

    /// <summary>Derived fallback states cannot masquerade as declared evidence.</summary>
    [Theory]
    [InlineData(SupportEvidenceStatus.ContractOnly)]
    [InlineData(SupportEvidenceStatus.Missing)]
    public void RejectsDerivedEvidenceStatusDeclarations(SupportEvidenceStatus status)
    {
        SupportRouteDescriptor route = Route();
        SupportEvidenceCatalogSnapshot evidence = EvidenceCatalog(
            new SupportEvidenceDeclaration(
                "derived:route",
                status,
                route.RouteId));

        _ = Assert.Throws<ArgumentException>(() =>
            SupportMatrixMaterializer.Materialize(
                Policy(Decision(route.RouteId)),
                [route],
                evidence));
    }

    /// <summary>Exact evidence cannot carry the target metadata reserved for aliases.</summary>
    [Fact]
    public void RejectsExactEvidenceWithAliasMetadata()
    {
        SupportRouteDescriptor route = Route();
        SupportEvidenceCatalogSnapshot evidence = EvidenceCatalog(
            new SupportEvidenceDeclaration(
                "golden:route",
                SupportEvidenceStatus.DirectGolden,
                route.RouteId,
                route.RouteId));

        _ = Assert.Throws<ArgumentException>(() =>
            SupportMatrixMaterializer.Materialize(
                Policy(Decision(route.RouteId)),
                [route],
                evidence));
    }

    /// <summary>An approved alias cannot cite a target without direct golden evidence.</summary>
    [Fact]
    public void RejectsAliasWithoutDirectGoldenTarget()
    {
        SupportRouteDescriptor source = Route();
        SupportRouteDescriptor target = Route(
            icId: "NT51929",
            mapVariant: "nt51929-ab-merge-512k");
        SupportEvidenceCatalogSnapshot evidence = EvidenceCatalog(
            new SupportEvidenceDeclaration(
                "alias:source",
                SupportEvidenceStatus.ApprovedAlias,
                source.RouteId,
                target.RouteId,
                Scope(source, "shared-scope")));

        _ = Assert.Throws<ArgumentException>(() =>
            SupportMatrixMaterializer.Materialize(
                Policy(Decision(source.RouteId)),
                [source, target],
                evidence));
    }

    /// <summary>Route identity rejects uppercase, empty, and punctuation-bearing token segments.</summary>
    [Theory]
    [InlineData("AB-merge")]
    [InlineData("ab--merge")]
    [InlineData("ab_merge")]
    public void RejectsInvalidRouteIdentityToken(string workflowId)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new SupportRouteIdentity(
                "NT51950",
                workflowId,
                "1-ic",
                "nt51950-ab-merge-512k"));
    }

    /// <summary>The same fact-scope id cannot conceal conflicting definitions.</summary>
    [Fact]
    public void RejectsConflictingFactScopeIdentityReuse()
    {
        SupportRouteDescriptor source = Route();
        SupportRouteDescriptor second = Route(
            icId: "NT51932",
            mapVariant: "nt51932-ab-merge-512k");
        SupportRouteDescriptor target = Route(
            icId: "NT51929",
            mapVariant: "nt51929-ab-merge-512k");
        SupportEvidenceCatalogSnapshot evidence = EvidenceCatalog(
            new SupportEvidenceDeclaration(
                "golden:target",
                SupportEvidenceStatus.DirectGolden,
                target.RouteId),
            new SupportEvidenceDeclaration(
                "alias:first",
                SupportEvidenceStatus.ApprovedAlias,
                source.RouteId,
                target.RouteId,
                Scope(source, "shared-scope")),
            new SupportEvidenceDeclaration(
                "alias:second",
                SupportEvidenceStatus.ApprovedAlias,
                second.RouteId,
                target.RouteId,
                Scope(second, "shared-scope")));

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            SupportMatrixMaterializer.Materialize(
                Policy(Decision(source.RouteId)),
                [source, second, target],
                evidence));

        Assert.Contains("conflicting definitions", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Execution source labels cannot fabricate golden evidence.</summary>
    [Fact]
    public void DoesNotInferGoldenFromExecutionSourceName()
    {
        SupportRouteDescriptor route = Route(
            executionSourceId: "GoldenVerified:profile-promotion");

        SupportMatrix matrix = SupportMatrixMaterializer.Materialize(
            Policy(Decision(route.RouteId)),
            [route],
            EvidenceCatalog());

        Assert.Equal(
            SupportEvidenceStatus.ContractOnly,
            Assert.Single(matrix.Rows).Evidence.Status);
    }

    /// <summary>Publication provenance must be an owner decision.</summary>
    [Fact]
    public void RejectsNonOwnerPublicationAuthority()
    {
        SupportRouteDescriptor route = Route();
        SupportPublicationPolicySnapshot policy = Policy(
            new SupportPublicationDecision(
                "candidate-route",
                route.RouteId,
                SupportPublicationStatus.Candidate,
                new SupportPublicationProvenance(
                    "inferred",
                    "2026-07-25",
                    "test",
                    "test")));

        _ = Assert.Throws<ArgumentException>(() =>
            SupportMatrixMaterializer.Materialize(
                policy,
                [route],
                EvidenceCatalog()));
    }

    /// <summary>Policy hash and supersession relationships fail closed.</summary>
    [Theory]
    [InlineData("invalid-hash")]
    [InlineData("same-version")]
    [InlineData("current-decision")]
    public void RejectsInvalidPolicyIntegrity(string mutation)
    {
        SupportRouteDescriptor route = Route();
        SupportPublicationDecision decision = mutation == "current-decision"
            ? new SupportPublicationDecision(
                "candidate-route",
                route.RouteId,
                SupportPublicationStatus.Candidate,
                Provenance(),
                ["candidate-route"])
            : Decision(route.RouteId);
        SupportPublicationPolicySnapshot policy = new(
            "support-publication-policy",
            "2.0.0",
            mutation == "invalid-hash" ? "not-a-hash" : new string('a', 64),
            [decision],
            mutation == "same-version" ? "2.0.0" : "1.0.0");

        _ = Assert.Throws<ArgumentException>(() =>
            SupportMatrixMaterializer.Materialize(
                policy,
                [route],
                EvidenceCatalog()));
    }

    /// <summary>Duplicate exact routes cannot enter one snapshot.</summary>
    [Fact]
    public void RejectsDuplicateExactRoutes()
    {
        SupportRouteDescriptor route = Route();

        _ = Assert.Throws<ArgumentException>(() =>
            SupportMatrixMaterializer.Materialize(
                Policy(Decision(route.RouteId)),
                [route, route],
                EvidenceCatalog()));
    }

    /// <summary>Public snapshots never retain caller-owned arrays.</summary>
    [Fact]
    public void CopiesAllPublicSnapshotCollections()
    {
        SupportRouteDescriptor route = Route();
        string[] supersededIds = ["old-decision"];
        var decision = new SupportPublicationDecision(
            "candidate-route",
            route.RouteId,
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
        SupportEvidenceFactScope scope = new(
            "scope",
            "NT51950",
            "ab-merge",
            counts,
            maps);
        SupportEvidenceDeclaration[] declarations =
        [
            new(
                "golden:route",
                SupportEvidenceStatus.DirectGolden,
                decision.RouteId),
        ];
        SupportEvidenceCatalogSnapshot evidence = new(
            "evidence",
            "1.0.0",
            "test",
            declarations);
        SupportMatrix matrix = SupportMatrixMaterializer.Materialize(
            policy,
            [route],
            evidence);

        supersededIds[0] = "mutated-decision";
        decisions[0] = new SupportPublicationDecision(
            "mutated",
            decision.RouteId,
            SupportPublicationStatus.Internal,
            Provenance());
        counts[0] = "cascade";
        maps[0] = "mutated-map";
        declarations[0] = new SupportEvidenceDeclaration(
            "mutated",
            SupportEvidenceStatus.SyntheticOracle,
            decision.RouteId);

        Assert.Equal(
            "old-decision",
            policy.Decisions.Single().SupersedesDecisionIds.Single());
        Assert.Equal("candidate-route", policy.Decisions.Single().DecisionId);
        Assert.Equal("single", scope.IcCountVariants.Single());
        Assert.Equal("nt51950-ab-merge-512k", scope.MapVariants.Single());
        Assert.Equal("golden:route", evidence.Declarations.Single().DeclarationId);
        _ = Assert.Single(matrix.Rows);
    }

    private static SupportRouteDescriptor Route(
        string icId = "NT51950",
        string mapVariant = "nt51950-ab-merge-512k",
        SupportAuthoringAvailability authoringAvailability =
            SupportAuthoringAvailability.Available,
        bool executionAdmitted = true,
        string executionSourceId = "v2:nt51950-ab-merge@0.2.0")
    {
        return new SupportRouteDescriptor(
            new SupportRouteIdentity(icId, "ab-merge", "single", mapVariant),
            authoringAvailability,
            executionAdmitted,
            $"authoring:{icId}:ab-merge",
            executionSourceId);
    }

    private static SupportEvidenceFactScope Scope(
        SupportRouteDescriptor route,
        string factScopeId)
    {
        return new SupportEvidenceFactScope(
            factScopeId,
            route.Identity.IcId,
            route.Identity.WorkflowId,
            [route.Identity.IcCountVariant],
            [route.Identity.MapVariant]);
    }

    private static SupportEvidenceCatalogSnapshot EvidenceCatalog(
        params SupportEvidenceDeclaration[] declarations)
    {
        return new SupportEvidenceCatalogSnapshot(
            "test-evidence",
            "1.0.0",
            "test:canonical-evidence",
            declarations);
    }

    private static SupportPublicationPolicySnapshot Policy(
        params SupportPublicationDecision[] decisions)
    {
        return new SupportPublicationPolicySnapshot(
            "support-publication-policy",
            "1.0.0",
            new string('a', 64),
            decisions);
    }

    private static SupportPublicationProvenance Provenance()
    {
        return new SupportPublicationProvenance(
            "owner-decision",
            "2026-07-25",
            "owner-chat:test",
            "test");
    }

    private static SupportPublicationDecision Decision(string routeId)
    {
        return new SupportPublicationDecision(
            "candidate-route",
            routeId,
            SupportPublicationStatus.Candidate,
            Provenance());
    }
}

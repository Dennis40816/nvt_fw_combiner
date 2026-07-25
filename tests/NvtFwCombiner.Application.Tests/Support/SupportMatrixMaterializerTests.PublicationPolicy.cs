using NvtFwCombiner.Application.Support;

namespace NvtFwCombiner.Application.Tests.Support;

public sealed partial class SupportMatrixMaterializerTests
{
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

    /// <summary>A canonical superseded id still fails when no prior snapshot proves it existed.</summary>
    [Fact]
    public void RejectsUnprovenSupersededDecisionId()
    {
        SupportRouteDescriptor route = Route();
        SupportPublicationPolicySnapshot policy = new(
            "support-publication-policy",
            "2.0.0",
            new string('a', 64),
            [new SupportPublicationDecision(
                "candidate-route-v2",
                route.RouteId,
                SupportPublicationStatus.Candidate,
                Provenance(),
                ["misspelled-prior-decision"])],
            "1.0.0",
            new string('b', 64));

        _ = Assert.Throws<ArgumentException>(() =>
            SupportPublicationPolicyValidator.Validate(policy));
    }

    /// <summary>A superseded id must be present in the exact prior policy snapshot.</summary>
    [Fact]
    public void RejectsSupersededDecisionIdAbsentFromPriorPolicy()
    {
        SupportRouteDescriptor route = Route();
        SupportPublicationPolicySnapshot prior = Policy(new SupportPublicationDecision(
            "actual-prior-decision",
            route.RouteId,
            SupportPublicationStatus.TestOnly,
            Provenance()));
        SupportPublicationPolicySnapshot policy = new(
            "support-publication-policy",
            "2.0.0",
            new string('b', 64),
            [new SupportPublicationDecision(
                "candidate-route-v2",
                route.RouteId,
                SupportPublicationStatus.Candidate,
                Provenance(),
                ["misspelled-prior-decision"])],
            prior.PolicyVersion,
            prior.Sha256);

        _ = Assert.Throws<ArgumentException>(() =>
            SupportPublicationPolicyValidator.Validate(policy, prior));
    }

    /// <summary>An exact prior policy snapshot closes a valid supersession relationship.</summary>
    [Fact]
    public void AcceptsSupersededDecisionIdFromPriorPolicy()
    {
        SupportRouteDescriptor route = Route();
        SupportPublicationPolicySnapshot prior = Policy(new SupportPublicationDecision(
            "prior-candidate-route",
            route.RouteId,
            SupportPublicationStatus.TestOnly,
            Provenance()));
        SupportPublicationPolicySnapshot policy = new(
            "support-publication-policy",
            "2.0.0",
            new string('b', 64),
            [new SupportPublicationDecision(
                "candidate-route-v2",
                route.RouteId,
                SupportPublicationStatus.Candidate,
                Provenance(),
                ["prior-candidate-route"])],
            prior.PolicyVersion,
            prior.Sha256);

        SupportPublicationPolicyValidator.Validate(policy, prior);
    }

    /// <summary>A same-id and same-version snapshot cannot replace the exact pinned predecessor.</summary>
    [Fact]
    public void RejectsPriorPolicyWithDifferentSha256()
    {
        SupportRouteDescriptor route = Route();
        SupportPublicationPolicySnapshot prior = Policy(new SupportPublicationDecision(
            "prior-candidate-route",
            route.RouteId,
            SupportPublicationStatus.TestOnly,
            Provenance()));
        SupportPublicationPolicySnapshot policy = new(
            "support-publication-policy",
            "2.0.0",
            new string('b', 64),
            [new SupportPublicationDecision(
                "candidate-route-v2",
                route.RouteId,
                SupportPublicationStatus.Candidate,
                Provenance(),
                ["prior-candidate-route"])],
            prior.PolicyVersion,
            new string('c', 64));

        _ = Assert.Throws<ArgumentException>(() =>
            SupportPublicationPolicyValidator.Validate(policy, prior));
    }
}

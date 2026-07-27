using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests owner-declared firmware family relationship invariants.</summary>
public sealed class FirmwareFamilyRelationshipTests
{
    /// <summary>A family relationship cannot collapse to a single member.</summary>
    [Fact]
    public void ConstructorRejectsFewerThanTwoMembers()
    {
        _ = Assert.Throws<ArgumentException>(() => Create(memberIds: ["NT51929"]));
    }

    /// <summary>Every relationship requires an explicit evidence reference.</summary>
    [Fact]
    public void ConstructorRejectsMissingEvidence()
    {
        _ = Assert.Throws<ArgumentException>(() => Create(evidenceRefs: []));
    }

    /// <summary>A perfect-like relationship owns the whole definition, not a partial scope.</summary>
    [Fact]
    public void ConstructorRejectsPartialScopeForPerfectLikeFamily()
    {
        _ = Assert.Throws<ArgumentException>(() => Create(sharedRegionIds: ["initial-code"]));
    }

    /// <summary>A shared-part relationship must name the exact shared physical region.</summary>
    [Fact]
    public void ConstructorRejectsMissingRegionForSharedPartFamily()
    {
        _ = Assert.Throws<ArgumentException>(() => Create(
            kind: FirmwareFamilyRelationshipKind.InitialCodeSharedFamily));
    }

    /// <summary>Set-like relationship inputs reject blank and duplicate identities.</summary>
    [Fact]
    public void ConstructorRejectsInvalidSetValues()
    {
        _ = Assert.Throws<ArgumentException>(() => Create(
            memberIds: ["NT51929", " "]));
        _ = Assert.Throws<ArgumentException>(() => Create(
            memberIds: ["NT51929", "NT51929"]));
    }

    private static FirmwareFamilyRelationship Create(
        FirmwareFamilyRelationshipKind kind = FirmwareFamilyRelationshipKind.PerfectLikeFamily,
        IEnumerable<string>? memberIds = null,
        IEnumerable<string>? sharedRegionIds = null,
        IEnumerable<string>? evidenceRefs = null)
    {
        return new FirmwareFamilyRelationship(
            "nt51919-nt51929-nt51932",
            kind,
            memberIds ?? ["NT51919", "NT51929", "NT51932"],
            sharedRegionIds ?? [],
            [],
            "Owner-confirmed perfect-like family.",
            evidenceRefs ?? ["SPEC.md"]);
    }
}

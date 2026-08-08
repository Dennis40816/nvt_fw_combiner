using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

public sealed partial class MemoryLayoutProjectorTests
{
    /// <summary>Retains the actual inspection or validation issue for each blocked slot.</summary>
    [Fact]
    public void BlockedItemsRetainDistinctIdentityPinnedIssueReferences()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge);
        var dpIssue = new AuthoringSlotIssueReference(
            AuthoringDerivedResultKind.Inspection,
            "inspection-result:dp:3",
            "input.length.invalid");
        var tpIssue = new AuthoringSlotIssueReference(
            AuthoringDerivedResultKind.Validation,
            "validation-result:tp:3",
            "tp.header.signature.invalid");
        ActiveSessionSnapshot session = CreateSession(
            fixture,
            Slot("dp-input", AuthoringSlotLifecycle.Error, Capacity, dpIssue),
            Slot("tp-input", AuthoringSlotLifecycle.Error, Capacity, tpIssue));

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability,
            session,
            compiledOverlay: null);

        Assert.Equal(2, snapshot.PendingItems.Count);
        MemoryLayoutBlockedIssueReference dp = Assert.IsType<MemoryLayoutBlockedIssueReference>(
            snapshot.PendingItems.Single(static item => item.SlotId == "dp-input").BlockedIssue);
        MemoryLayoutBlockedIssueReference tp = Assert.IsType<MemoryLayoutBlockedIssueReference>(
            snapshot.PendingItems.Single(static item => item.SlotId == "tp-input").BlockedIssue);
        Assert.Same(dpIssue, dp.Issue);
        Assert.Same(tpIssue, tp.Issue);
        Assert.NotEqual(dp.Issue.ResultReference, tp.Issue.ResultReference);
        Assert.NotEqual(dp.Issue.IssueId, tp.Issue.IssueId);
        Assert.Equal(Token, dp.ResolutionToken);
        Assert.Equal(new AuthoringRevision(3), dp.AuthoringRevision);
        Assert.Equal("dp-input", dp.SlotIdentity.DefinitionId);
        Assert.Equal("dp-input.bin", dp.SlotIdentity.SelectedPath);
        Assert.Equal(Capacity, dp.SlotIdentity.FileStamp!.Value.AcceptedLength);
        Assert.All(
            snapshot.PendingItems,
            static item =>
            {
                Assert.Equal(MemoryLayoutReadiness.Blocked, item.Readiness);
                Assert.Equal(MemoryDiagnosticSeverity.Error, item.DiagnosticSeverity);
            });
    }

    /// <summary>Rejects missing, misplaced, or non-diagnostic slot issue references.</summary>
    [Fact]
    public void SlotIssueReferencesRemainLifecycleBound()
    {
        var issue = new AuthoringSlotIssueReference(
            AuthoringDerivedResultKind.Validation,
            "validation-result:dp:3",
            "input.invalid");

        _ = Assert.Throws<ArgumentException>(() =>
            Slot("dp-input", AuthoringSlotLifecycle.Error, Capacity));
        _ = Assert.Throws<ArgumentException>(() =>
            Slot("dp-input", AuthoringSlotLifecycle.Verified, Capacity, issue));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AuthoringSlotIssueReference(
                AuthoringDerivedResultKind.Build,
                "build-result:dp:3",
                "runtime.issue"));
    }

    /// <summary>Projects ordered overlaps with checked half-open split boundaries.</summary>
    [Fact]
    public void OrderedOperationsSplitHalfOpenCoverageAndRetainContributorOrder()
    {
        ProjectionFixture fixture = CreateFixture(
            CompositionKind.Merge,
            OverlappingMergePlan());
        ActiveSessionSnapshot session = CreateSession(
            fixture,
            Slot("dp-input", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("tp-input", AuthoringSlotLifecycle.Verified, Capacity));

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability,
            session,
            fixture.Composition);

        Assert.Equal(
            [
                new ByteRange(0, 4),
                new ByteRange(4, 4),
                new ByteRange(8, 4),
                new ByteRange(12, 4),
            ],
            snapshot.AfterSegments.Select(static segment => segment.Range));
        Assert.Equal(
            ["copy-first"],
            snapshot.AfterSegments[0].ContributingOperationIds);
        Assert.Equal(
            ["copy-first", "copy-overlap"],
            snapshot.AfterSegments[1].ContributingOperationIds);
        Assert.Equal(
            ["copy-touching"],
            snapshot.AfterSegments[2].ContributingOperationIds);
        Assert.Equal("tp-input", snapshot.AfterSegments[1].SourceSlotId);
        Assert.Equal("tp-input", snapshot.AfterSegments[2].SourceSlotId);
        Assert.Empty(snapshot.AfterSegments[3].ContributingOperationIds);
        Assert.All(
            snapshot.AfterSegments,
            static segment => Assert.True(segment.Range.Length > 0));
        Assert.Equal(
            Capacity,
            snapshot.AfterSegments.Sum(static segment => segment.Range.Length));
    }

    private static CompositionPlan OverlappingMergePlan()
    {
        return new CompositionPlan(
            ImageInitialization.Blank("output-image", Capacity, 0),
            [
                new AddressSpace("dp-input", Capacity, AddressSpaceMutability.Immutable),
                new AddressSpace("tp-input", Capacity, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", Capacity, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.CopyRange(
                    "copy-first",
                    100,
                    "dp-input",
                    new ByteRange(0, 8),
                    "output-image",
                    new ByteRange(0, 8),
                    OverlapPolicy.Reject,
                    "copy first half"),
                CompositionOperation.CopyRange(
                    "copy-overlap",
                    200,
                    "tp-input",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(4, 4),
                    OverlapPolicy.ReplaceExisting,
                    "replace the overlapping half"),
                CompositionOperation.CopyRange(
                    "copy-touching",
                    300,
                    "tp-input",
                    new ByteRange(8, 4),
                    "output-image",
                    new ByteRange(8, 4),
                    OverlapPolicy.Reject,
                    "touch the prior exclusive end"),
            ]);
    }
}

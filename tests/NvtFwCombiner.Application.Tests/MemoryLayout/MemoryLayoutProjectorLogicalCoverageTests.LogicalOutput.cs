using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

/// <summary>Exercises logical output, retained coverage, and declared section parentage.</summary>
[Collection(nameof(MemoryLayoutProjectorSerialGroup))]
public sealed partial class MemoryLayoutProjectorLogicalCoverageTests
{
    /// <summary>Projects General Merge logical output without fabricating a physical map or region.</summary>
    [Fact]
    public void GeneralMergeProjectsLogicalOutputGeometry()
    {
        CompiledComposition composition = CreateLogicalGeneralMergeComposition();
        ResolvedCapability capability = CreateLogicalGeneralMergeCapability(composition);
        var route = new CapabilityRouteIdentity("NT-SYNTHETIC", ExperienceIds.GeneralMerge, "selector-free", "generic");
        var session = new ActiveSessionSnapshot(
            route.WorkflowId,
            Token,
            new AuthoringRevision(3),
            route.RouteId,
            capability.CapabilityFingerprint,
            executionAdmitted: true,
            route.IcId,
            route.IcCountVariant,
            route.MapVariant,
            [route.IcId],
            [route.IcCountVariant],
            [
                Slot("source-a", AuthoringSlotLifecycle.Verified, 2),
                Slot("source-b", AuthoringSlotLifecycle.Verified, 2),
            ],
            draftState: null,
            draftCapabilityFingerprint: null,
            derivedPublications: []);

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            capability,
            session,
            capability.CompiledComposition);

        Assert.Equal(MemoryLayoutGeometryKind.LogicalOutput, snapshot.GeometryKind);
        Assert.All(snapshot.BeforeSegments.Concat(snapshot.AfterSegments), static segment => Assert.True(segment.IsPrimaryContent));
        Assert.Null(snapshot.MapId);
        Assert.Empty(snapshot.CanonicalRegions);
        Assert.Equal("output-image", snapshot.AddressSpaceId);
        Assert.Equal(6, snapshot.Capacity);
        MemoryLayoutSegment before = Assert.Single(snapshot.BeforeSegments);
        Assert.Equal(new ByteRange(0, 6), before.Range);
        Assert.Equal(MemoryWorkflowDisposition.Blank, before.Disposition);
        Assert.Equal(
            [new ByteRange(0, 2), new ByteRange(2, 2), new ByteRange(4, 2)],
            snapshot.AfterSegments.Select(static segment => segment.Range));
        Assert.Equal(
            [
                MemoryWorkflowDisposition.WillWrite,
                MemoryWorkflowDisposition.Blank,
                MemoryWorkflowDisposition.WillWrite,
            ],
            snapshot.AfterSegments.Select(static segment => segment.Disposition));
        Assert.All(snapshot.AfterSegments, segment =>
        {
            Assert.Equal("output-image", segment.RegionId);
            Assert.Null(segment.CanonicalRegion);
            Assert.Equal(MemoryContentRole.General, segment.ContentRole);
        });
        Assert.Equal(
            ["source-a", null, "source-b"],
            snapshot.AfterSegments.Select(static segment => segment.SourceSlotId));
        Assert.Equal(
            ["slot:source-a", "segment:output-image:2-4", "slot:source-b"],
            snapshot.AfterSegments.Select(static segment => segment.LogicalCoverageGroupId));
        Assert.Equal(
            snapshot.AfterSegments.Count,
            snapshot.AfterSegments
                .Select(static segment => segment.LogicalCoverageGroupId)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }
}

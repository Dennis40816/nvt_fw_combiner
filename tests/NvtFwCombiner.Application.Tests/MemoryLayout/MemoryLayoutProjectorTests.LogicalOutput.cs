using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

public sealed partial class MemoryLayoutProjectorTests
{
    /// <summary>Projects General Merge logical output without fabricating a physical map or region.</summary>
    [Fact]
    public void GeneralMergeProjectsLogicalOutputGeometry()
    {
        CompiledComposition composition = CreateLogicalGeneralMergeComposition();
        var route = new CapabilityRouteIdentity(
            "NT-SYNTHETIC",
            ExperienceIds.GeneralMerge,
            "selector-free",
            "generic");
        const string capabilityFingerprint =
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
        var compilationContract = new CanonicalCapabilityCompilationContract(
            "logical-general-merge",
            "1.0.0",
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            ["generic"],
            CapabilityDefinitionFingerprint.LogicalOutputCompilerSemanticId,
            ["family:synthetic-family"],
            allowsLogicalOutput: true);
        var capability = new ResolvedCapability(
            route,
            capabilityFingerprint,
            composition,
            Decision(
                "authoring",
                route,
                capabilityFingerprint,
                CapabilityAuthoringAvailability.Available),
            Decision(
                "publication",
                route,
                capabilityFingerprint,
                CapabilityPublicationStatus.Supported),
            Decision(
                "evidence",
                route,
                capabilityFingerprint,
                CapabilityEvidenceStatus.SyntheticOracle),
            MetadataPlanDefinition.Empty.Resolve(Token),
            Token,
            compilationContract);
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

    private static CompiledComposition CreateLogicalGeneralMergeComposition()
    {
        var plan = new CompositionPlan(
            [ImageInitialization.Blank("output-image", 6, 0)],
            "output-image",
            [
                new AddressSpace("source-a", 2, AddressSpaceMutability.Immutable),
                new AddressSpace("source-b", 2, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", 6, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.CopyRange(
                    "copy-a",
                    10,
                    "source-a",
                    new ByteRange(0, 2),
                    "output-image",
                    new ByteRange(0, 2),
                    OverlapPolicy.Reject,
                    "copy first logical input"),
                CompositionOperation.CopyRange(
                    "copy-b",
                    20,
                    "source-b",
                    new ByteRange(0, 2),
                    "output-image",
                    new ByteRange(4, 2),
                    OverlapPolicy.Reject,
                    "copy second logical input"),
            ]);
        var provenance = new V2CompilationProvenance(
            new ProfileBundleIdentity(
                "logical-bundle-v2",
                "1.0.0",
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "release-binding"),
            new ProfileBundleEntryIdentity(
                "logical-profile-entry",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
            new LogicalOutputV2CompilationContext(
                "synthetic-family",
                "1.0.0",
                "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                "NT-SYNTHETIC"),
            new CompiledProfilePromotion(CompiledProfilePromotionStage.ExecutableCandidate, []),
            ["logical-profile-evidence"],
            [],
            []);
        var details = new V2CompiledCompositionDetails(
            "logical-general-merge",
            "1.0.0",
            ExperienceIds.GeneralMerge,
            CompositionKind.Merge,
            provenance,
            new CompiledInputContract(
                [CompiledInputSlotTestFactory.Create(
                    "source-slot",
                    "source",
                    CompiledInputArtifactClass.Auxiliary,
                    required: true,
                    CompiledInputSlotCardinality.OneOrMore,
                    [".bin"],
                    new CompiledBoundedInputLengthRequirement(1, int.MaxValue),
                    new CompiledNoInputNormalization())],
                [
                    new CompiledInputSpaceBinding(
                        "source-a",
                        "source-slot",
                        CompiledInputInstancePolicy.PerBinding),
                    new CompiledInputSpaceBinding(
                        "source-b",
                        "source-slot",
                        CompiledInputInstancePolicy.PerBinding),
                ]),
            new CompiledRegionAccessContract([], []),
            new CompiledOutputNamingRequirement(
                "logical-output.bin",
                allowOverride: false,
                CompiledOutputInvalidCharacterPolicy.Reject,
                []));
        return CompiledComposition.CreateV2(plan, details);
    }
}

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

public sealed partial class MemoryLayoutProjectorTests
{
    /// <summary>Maps synthetic logical Merge rows while keeping shared inline sources draft-owned; this is not authoring admission.</summary>
    [Fact]
    public void LogicalMergeProjectionKeepsFileAndInlineSourceAttribution()
    {
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", Capacity, 0),
            [
                new AddressSpace("file-map-input", 4, AddressSpaceMutability.Immutable),
                new AddressSpace("inline-map-input", 4, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", Capacity, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.CopyRange(
                    "file-map",
                    100,
                    "file-map-input",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(0, 4),
                    OverlapPolicy.Reject,
                    "copy from file"),
                CompositionOperation.ReplaceRange(
                    "inline-map",
                    101,
                    "inline-map-input",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(4, 4),
                    OverlapPolicy.Reject,
                    "copy from inline source"),
            ]);
        var contract = new CompiledInputContract(
            [
                SlotRequirement(
                    "general-source",
                    "source",
                    CompiledInputArtifactClass.Auxiliary,
                    new CompiledExactBytesInputLengthRequirement(4)),
            ],
            [
                new CompiledInputSpaceBinding(
                    "file-map-input",
                    "general-source",
                    CompiledInputInstancePolicy.PerBinding),
                new CompiledInputSpaceBinding(
                    "inline-map-input",
                    "general-source",
                    CompiledInputInstancePolicy.PerBinding),
            ]);
        CompiledComposition composition = CreateLogicalGeneralMergeComposition(plan, contract);
        ResolvedCapability capability = CreateLogicalGeneralMergeCapability(composition);
        var draft = new GeneralMappingDraftState(
            [
                new GeneralMappingDraftRow(
                    "file-map",
                    ExplicitMappingOperationKind.CopyRange,
                    GeneralMappingSource.File("file-map.bin"),
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(0, 4),
                    OverlapPolicy.Reject,
                    1,
                    "copy from file"),
                new GeneralMappingDraftRow(
                    "inline-map",
                    ExplicitMappingOperationKind.ReplaceRange,
                    GeneralMappingSource.HexOverwrite("A5A5A5A5"),
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(4, 4),
                    OverlapPolicy.Reject,
                    1,
                    "copy from inline source"),
            ]);
        var route = new CapabilityRouteIdentity("NT-SYNTHETIC", ExperienceIds.GeneralMerge, "selector-free", "generic");
        var session = new ActiveSessionSnapshot(
            route.WorkflowId, Token, new AuthoringRevision(3), route.RouteId, capability.CapabilityFingerprint,
            executionAdmitted: true, route.IcId, route.IcCountVariant, route.MapVariant,
            [route.IcId], [route.IcCountVariant], [Slot("file-map", AuthoringSlotLifecycle.Verified, 4)],
            new GeneralMergeDraftState(new GeneralMergeOutputInitializer(Capacity), draft),
            capability.CapabilityFingerprint, derivedPublications: []);

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(capability, session, capability.CompiledComposition);

        Assert.Empty(snapshot.PendingItems);
        MemoryLayoutSegment file = Assert.Single(
            snapshot.AfterSegments,
            static segment => segment.ContributingOperations.Any(
                static operation => operation.OperationId == "file-map"));
        Assert.Equal("file-map", file.SourceSlotId);
        Assert.Equal(MemorySelectionState.Selected, file.Selection);
        MemoryLayoutSegment inline = Assert.Single(
            snapshot.AfterSegments,
            static segment => segment.ContributingOperations.Any(
                static operation => operation.OperationId == "inline-map"));
        Assert.Null(inline.SourceSlotId);
        Assert.Equal("inline-map-input", inline.SourceSpaceId);
        Assert.Equal(MemorySelectionState.NotSelected, inline.Selection);
    }
}

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

public sealed partial class MemoryLayoutProjectorTests
{
    /// <summary>Maps request-scoped General file rows while keeping inline sources draft-owned.</summary>
    [Fact]
    public void GeneralReplaceProjectsPerBindingFileAndInlineSources()
    {
        var plan = new CompositionPlan(
            ImageInitialization.Reference("output-image", "reference-base", Capacity),
            [
                new AddressSpace("reference-base", Capacity, AddressSpaceMutability.Immutable),
                new AddressSpace("file-map-input", 4, AddressSpaceMutability.Immutable),
                new AddressSpace("inline-map-input", 4, AddressSpaceMutability.Immutable),
                new AddressSpace("output-image", Capacity, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.ReplaceRange(
                    "file-map",
                    100,
                    "file-map-input",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(0, 4),
                    OverlapPolicy.Reject,
                    "replace from file"),
                CompositionOperation.ReplaceRange(
                    "inline-map",
                    101,
                    "inline-map-input",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(4, 4),
                    OverlapPolicy.Reject,
                    "replace from inline source"),
            ]);
        var contract = new CompiledInputContract(
            [
                SlotRequirement(
                    "reference-base",
                    "reference",
                    CompiledInputArtifactClass.ReferenceImage,
                    new CompiledExactResolvedMapCapacityInputLengthRequirement(Capacity)),
                SlotRequirement(
                    "general-source",
                    "source",
                    CompiledInputArtifactClass.Auxiliary,
                    new CompiledExactBytesInputLengthRequirement(4)),
            ],
            [
                new CompiledInputSpaceBinding(
                    "reference-base",
                    "reference-base",
                    CompiledInputInstancePolicy.Singleton),
                new CompiledInputSpaceBinding(
                    "file-map-input",
                    "general-source",
                    CompiledInputInstancePolicy.PerBinding),
                new CompiledInputSpaceBinding(
                    "inline-map-input",
                    "general-source",
                    CompiledInputInstancePolicy.PerBinding),
            ]);
        ProjectionFixture fixture = CreateFixture(
            CompositionKind.Replace,
            plan,
            contract,
            ExperienceIds.GeneralReplace);
        var draft = new GeneralMappingDraftState(
            [
                new GeneralMappingDraftRow(
                    "file-map",
                    ExplicitMappingOperationKind.ReplaceRange,
                    GeneralMappingSource.File("file-map.bin"),
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(0, 4),
                    OverlapPolicy.Reject,
                    1,
                    "replace from file"),
                new GeneralMappingDraftRow(
                    "inline-map",
                    ExplicitMappingOperationKind.ReplaceRange,
                    GeneralMappingSource.HexOverwrite("A5A5A5A5"),
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(4, 4),
                    OverlapPolicy.Reject,
                    1,
                    "replace from inline source"),
            ]);
        ActiveSessionSnapshot session = CreateSessionWithDraft(
            fixture,
            draft,
            Slot("reference-base", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("file-map", AuthoringSlotLifecycle.Verified, 4));

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability,
            session,
            fixture.Composition);

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

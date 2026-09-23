using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

public sealed partial class MemoryLayoutProjectorTests
{
    /// <summary>Physical projection retains canonical template geometry and attributes the extra DP tail only to the typed extent.</summary>
    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void SourceEnvelopeProjectsOpaquePreservedTailAtActualLength(int actualLength)
    {
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap map =
            CreateResolvedMap(ExperienceIds.StandardMerge, ctrlRamMap: false, customRegions: null);
        var envelope = new SourceEnvelopeExtent("dp-input", "flash-image", map.ImageMap.MapId,
            Capacity, actualLength, [Capacity], "DP_NONSTANDARD_SIZE_WARNING");
        var dpDefinition = new CompositionInputSlotDefinition(
            "dp-input", "dp", CompiledInputArtifactClass.DpFirmware, required: true,
            CompiledInputSlotCardinality.ExactlyOne, [".bin"],
            new ResolvedMapCapacityInputLengthDefinition(), new CompiledNoInputNormalization());
        CompiledInputSlotRequirement dpRequirement = new CompiledInputSlotRequirement(dpDefinition,
            new CompiledExactResolvedMapCapacityInputLengthRequirement(Capacity))
            .ResolveSourceEnvelopeLength(actualLength);
        var inputContract = new CompiledInputContract(
            [dpRequirement],
            [new CompiledInputSpaceBinding("dp-input", "dp-input", CompiledInputInstancePolicy.Singleton)]);
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", actualLength, 0),
            [
                new AddressSpace("dp-input", actualLength, AddressSpaceMutability.Immutable,
                    allowedInputLengths: [actualLength], expectedInputLengths: [Capacity],
                    unexpectedInputLengthIssueCode: "DP_NONSTANDARD_SIZE_WARNING"),
                new AddressSpace("output-image", actualLength, AddressSpaceMutability.Mutable),
            ],
            [CompositionOperation.CopyRange("copy-dp", 100, "dp-input", new ByteRange(0, actualLength),
                "output-image", new ByteRange(0, actualLength), OverlapPolicy.Reject, "retain complete DP")]);
        var provenance = new V2CompilationProvenance(
            new ProfileBundleIdentity("bundle-standard-merge", "1.0.0", new string('a', 64), "test-binding"),
            new ProfileBundleEntryIdentity("profile-standard-merge", new string('b', 64)),
            new ResolvedMapV2CompilationContext(map, envelope),
            new CompiledProfilePromotion(CompiledProfilePromotionStage.ExecutableCandidate, []),
            ["test-evidence"], [], []);
        var details = new V2CompiledCompositionDetails(
            "profile-standard-merge", "1.0.0", ExperienceIds.StandardMerge,
            CompositionKind.Merge, provenance, inputContract,
            new CompiledRegionAccessContract([], []),
            new CompiledOutputNamingRequirement("standard-merge.bin", allowOverride: true,
                CompiledOutputInvalidCharacterPolicy.Reject, []), null);
        CompiledComposition composition = CompiledComposition.CreateV2RuntimeExecutable(plan, details);
        var route = new CapabilityRouteIdentity("NT-SYNTHETIC", ExperienceIds.StandardMerge,
            "selector-free", map.ImageMap.MapId);
        var fixture = new ProjectionFixture(route, Capability(route, composition), composition, map,
            map.ImageMap.Regions.Single(static region => region.RegionId == "dp-code"),
            map.ImageMap.Regions.Single(static region => region.RegionId == "tp-code"));
        ActiveSessionSnapshot session = CreateSession(fixture, Slot("dp-input", AuthoringSlotLifecycle.Verified, actualLength));

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability, session, fixture.Capability.CompiledComposition);

        Assert.Equal(actualLength, snapshot.Capacity);
        Assert.Same(envelope, snapshot.SourceEnvelope);
        if (actualLength > Capacity)
        {
            MemoryLayoutSegment tail = Assert.Single(snapshot.AfterSegments,
                segment => segment.Range == new ByteRange(Capacity, actualLength - Capacity));
            Assert.Null(tail.CanonicalRegion);
            Assert.Equal(MemoryContentRole.Dp, tail.ContentRole);
            Assert.Equal("dp-input", tail.SourceSlotId);
            Assert.Equal(["copy-dp"], tail.ContributingOperations.Select(static operation => operation.OperationId));
        }
        else
        {
            Assert.DoesNotContain(snapshot.AfterSegments,
                static segment => segment.CanonicalRegion is null);
        }
        Assert.DoesNotContain(snapshot.CanonicalRegions,
            static region => region.Range.EndExclusive > Capacity);
        Assert.Equal(actualLength, snapshot.AfterSegments[^1].Range.EndExclusive);
    }
}

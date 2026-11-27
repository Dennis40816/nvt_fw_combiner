// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Application.Tests.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using static NvtFwCombiner.Application.Tests.MemoryLayout.MemoryLayoutProjectorTestSupport;

namespace NvtFwCombiner.Bootstrap.Tests.MemoryLayout;

/// <summary>Uses declared AB extractor carriers without firmware files or version-value inference.</summary>
[Collection(nameof(MemoryLayoutProjectorSerialGroup))]
public sealed class MemoryLayoutAbVersionCarrierTests
{
    /// <summary>Each bank keeps its independent canonical carrier, including the nonuniform cascade B coordinate.</summary>
    [Theory]
    [InlineData("NT51929", "nt51929-ab-merge-512k", 0x80000, 0, 0x401A, 0x4401A)]
    [InlineData("NT51950", "nt51950-ab-merge-maps", 0x80000, 1, 0x3B016, 0x7B016)]
    [InlineData("NT51950", "nt51950-ab-cascade-maps", 0x100000, 2, 0x5016, 0x84016)]
    [InlineData("NT51951", "nt51951-ab-merge-1024k", 0x100000, 0, 0x5016, 0x84016)]
    public void BuiltInAbCarriersRetainCanonicalIdentityAndIndependentOutputCoordinatesWithoutSourceBytes(
        string icId, string mapVariantSetId, long capacity, int topologyCount, long aStart, long bStart)
    {
        (CompiledComposition composition, MetadataPlanDefinition metadata) =
            BuiltInCompositionTestProfiles.AbMerge(icId, mapVariantSetId, capacity, topologyCount);
        MapBoundV2CompilationContext context = Assert.IsType<MapBoundV2CompilationContext>(
            composition.V2Details.Provenance.Context, exactMatch: false);
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolved = context.ResolvedMap;
        var route = new CapabilityRouteIdentity(icId, ExperienceIds.AbMerge, "selector-free", resolved.ImageMap.MapId);
        ResolvedCapability capability = Capability(route, composition, metadata);
        var fixture = new ProjectionFixture(route, capability, capability.CompiledComposition, resolved,
            resolved.ImageMap.Regions[0], resolved.ImageMap.Regions[^1]);
        ActiveSessionSnapshot session = CreateSession(fixture,
            [.. composition.V2Details.InputContract.SpaceBindings.Select(binding => Slot(binding.AddressSpaceId,
                AuthoringSlotLifecycle.Verified, composition.Plan.AddressSpaces.Single(space =>
                    space.AddressSpaceId == binding.AddressSpaceId).Length))]);
        Assert.Null(session.MetadataInspection);

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(capability, session, compiledOverlay: null);

        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, layout.DpVersionLocations.Status);
        MemoryLayoutVersionCarrierLocation[] locations = [.. layout.DpVersionLocations.Values.Select(
            static location => Assert.IsType<MemoryLayoutVersionCarrierLocation>(location))];
        Assert.Equal(
            [(CompiledInputVersionKind.DpA, "a-cmi-dp-version", new ByteRange(aStart, 3)),
             (CompiledInputVersionKind.DpB, "b-cmi-dp-version", new ByteRange(bStart, 3))],
            locations.Select(static location => (location.VersionKind, location.FieldId, location.OutputRange.Range)));
        Assert.All(locations, location =>
        {
            Assert.Equal(resolved.ImageMap.AddressSpaceId, location.OutputRange.AddressSpaceId);
            Assert.Same(resolved.ImageMap.Regions.Single(region => region.RegionId == location.FieldId), location.CanonicalRegion);
            Assert.False(CompiledInputArtifactObservationService.DecodeDpRegion(
                composition, location.VersionKind, location.FieldId, ReadOnlyMemory<byte>.Empty).IsKnown);
        });
    }

    /// <summary>The shared declaration lookup preserves the existing Reg16-18 decoder and unreadable behavior.</summary>
    [Fact]
    public void ExistingAbDecoderKeepsVersionTrackerAndShortInputBehavior()
    {
        CompiledComposition composition = BuiltInCompositionTestProfiles.AbMerge(
            "NT51929", "nt51929-ab-merge-512k", 0x80000).Composition;
        byte[] bytes = new byte[0x80000];
        foreach ((CompiledInputVersionKind kind, FirmwareRegion? region) in
            CompiledInputArtifactObservationService.GetDeclaredDpVersionCarriers(composition))
        {
            Assert.NotNull(region);
            int start = checked((int)region.Range.Start);
            bytes[start] = 0x34;
            bytes[start + 1] = 0x12;
            bytes[start + 2] = 0xA2;
            CompiledInputVersionObservation known = CompiledInputArtifactObservationService.DecodeDpRegion(
                composition, kind, region.RegionId, bytes);
            Assert.Equal((kind, (byte?)0x12, (byte?)0x0A, (ushort?)0x234),
                (known.Kind, known.Major, known.Minor, known.TrackerId));
            CompiledInputVersionObservation shortInput = CompiledInputArtifactObservationService.DecodeDpRegion(
                composition, kind, region.RegionId, bytes.AsMemory(0, start + 2));
            Assert.False(shortInput.IsKnown);
            Assert.Null(shortInput.TrackerId);
        }
    }
}

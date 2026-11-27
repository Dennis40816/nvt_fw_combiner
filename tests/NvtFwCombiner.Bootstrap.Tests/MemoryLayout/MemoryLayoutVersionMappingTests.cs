// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Application.Tests.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using static NvtFwCombiner.Application.Tests.MemoryLayout.MemoryLayoutProjectorTestSupport;

namespace NvtFwCombiner.Bootstrap.Tests.MemoryLayout;

/// <summary>Exercises synthetic copy geometry over existing built-in metadata declarations, without executing bytes.</summary>
[Collection(nameof(MemoryLayoutProjectorSerialGroup))]
public sealed class MemoryLayoutVersionMappingTests
{
    /// <summary>The field's source coordinates do not masquerade as its relocated output coordinates.</summary>
    [Fact]
    public void NonzeroSourceVersionOffsetIsTranslatedToNonzeroOutputOffset()
    {
        ProjectionFixture fixture = RelocatedFixture(duplicate: false, overwriteMajor: false);

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(
            fixture.Capability, CreateInspectedSession(fixture), fixture.Composition);

        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, layout.DpVersionLocations.Status);
        Assert.Equal([("dp-major", new ByteRange(0x101, 1)), ("dp-minor", new ByteRange(0x102, 1))],
            layout.DpVersionLocations.Values.Select(static location => (location.FieldId, location.OutputRange.Range)));
        Assert.All(layout.DpVersionLocations.Values,
            static location => Assert.Equal("flash", location.OutputRange.AddressSpaceId));
    }

    /// <summary>One declared source field can have multiple proven output placements.</summary>
    [Fact]
    public void DuplicatedVersionFieldsPublishEveryProvenOutputLocation()
    {
        ProjectionFixture fixture = RelocatedFixture(duplicate: true, overwriteMajor: false);

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(
            fixture.Capability, CreateInspectedSession(fixture), fixture.Composition);

        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, layout.DpVersionLocations.Status);
        Assert.Equal(
            [("dp-major", new ByteRange(0x101, 1)), ("dp-major", new ByteRange(0x201, 1)),
             ("dp-minor", new ByteRange(0x102, 1)), ("dp-minor", new ByteRange(0x202, 1))],
            layout.DpVersionLocations.Values.Select(static location => (location.FieldId, location.OutputRange.Range)));
        Assert.Same(Assert.IsType<MemoryLayoutMetadataVersionLocation>(layout.DpVersionLocations.Values[0]).Field, Assert.IsType<MemoryLayoutMetadataVersionLocation>(layout.DpVersionLocations.Values[1]).Field);
    }

    /// <summary>Content attribution through a scalar write is insufficient proof of a metadata field location.</summary>
    [Fact]
    public void OverwrittenVersionCarrierIsUnavailableDespiteRetainedContentAttribution()
    {
        ProjectionFixture fixture = RelocatedFixture(duplicate: false, overwriteMajor: true);

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(
            fixture.Capability, CreateInspectedSession(fixture), fixture.Composition);

        Assert.Equal(MemoryLayoutFactStatus.Unavailable, layout.DpVersionLocations.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.OutputLocationNotResolved, layout.DpVersionLocations.UnavailableReason);
        Assert.Empty(layout.DpVersionLocations.Values);
        Assert.Contains(layout.AfterSegments, static segment => segment.Range == new ByteRange(0x101, 1) &&
            segment.ContentSource?.SourceSpaceId == "dp-input");
    }

    private static ProjectionFixture RelocatedFixture(bool duplicate, bool overwriteMajor)
    {
        (CompiledComposition original, MetadataPlanDefinition metadata) = BuiltInCompositionTestProfiles.StandardMerge();
        CompositionPlan baseline = original.Plan;
        List<CompositionOperation> operations = [Copy("version-a", 10, 0x100)];
        if (duplicate)
        {
            operations.Add(Copy("version-b", 20, 0x200));
        }
        if (overwriteMajor)
        {
            operations.Add(CompositionOperation.PatchScalar("overwrite-major", 30, baseline.OutputSpaceId,
                new ByteRange(0x101, 1), [0xAA], OverlapPolicy.ReplaceExisting, "Synthetic overwrite of version carrier"));
        }
        var plan = new CompositionPlan(baseline.OutputInitialization, baseline.AddressSpaces, operations);
        var composition = CompiledComposition.CreateV2RuntimeExecutable(plan, original.V2Details);
        var context = (MapBoundV2CompilationContext)composition.V2Details.Provenance.Context;
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolved = context.ResolvedMap;
        var route = new CapabilityRouteIdentity(resolved.MemberId, composition.V2Details.ExperienceId,
            "selector-free", resolved.ImageMap.MapId);
        ResolvedCapability capability = Capability(route, composition, metadata);
        return new ProjectionFixture(route, capability, capability.CompiledComposition, resolved,
            resolved.ImageMap.Regions.First(static region => region.RegionId == "dp-code"),
            resolved.ImageMap.Regions.First(static region => region.RegionId == "tp-code"));

        CompositionOperation Copy(string id, int sequence, long targetStart)
        {
            return CompositionOperation.CopyRange(
                id, sequence, "dp-input", new ByteRange(0x3E014, 3), baseline.OutputSpaceId,
                new ByteRange(targetStart, 3), OverlapPolicy.Reject, "Synthetic relocation of declared version carriers");
        }
    }
}

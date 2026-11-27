// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

/// <summary>Checks synthetic layout facts and the original snapshot contract.</summary>
[Collection(nameof(MemoryLayoutProjectorSerialGroup))]
public sealed class MemoryLayoutFactTests
{
    /// <summary>Logical output cannot invent physical access or a DP version declaration.</summary>
    [Fact]
    public void LogicalOutputReportsPhysicalAccessAndDpVersionUnavailable()
    {
        ResolvedCapability capability = CreateLogicalGeneralMergeCapability(CreateLogicalGeneralMergeComposition());
        CapabilityRouteIdentity route = capability.Identity;
        AuthoringSlotState[] slots = [.. capability.CompiledComposition.V2Details.InputContract.SpaceBindings.Select(binding =>
            Slot(binding.AddressSpaceId, AuthoringSlotLifecycle.Verified,
                capability.CompiledComposition.Plan.AddressSpaces.First(space => space.AddressSpaceId == binding.AddressSpaceId).Length))];
        var session = new ActiveSessionSnapshot(route.WorkflowId, Token, new AuthoringRevision(3), route.RouteId,
            capability.CapabilityFingerprint, true, route.IcId, route.IcCountVariant, route.MapVariant,
            [route.IcId], [route.IcCountVariant], slots, null, null, []);

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(capability, session, capability.CompiledComposition);

        Assert.Equal(MemoryLayoutFactStatus.Unavailable, layout.ReadOnlyRanges.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.NoPhysicalMap, layout.ReadOnlyRanges.UnavailableReason);
        Assert.Empty(layout.ReadOnlyRanges.Values);
        Assert.Equal(MemoryLayoutFactStatus.Unavailable, layout.DpVersionLocations.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.NotDeclared, layout.DpVersionLocations.UnavailableReason);
    }

    /// <summary>Publishing a fact set takes ownership of its collection without exposing mutation.</summary>
    [Fact]
    public void FactValuesOwnAnImmutableCopyOfTheirInputCollection()
    {
        List<MemoryLayoutVersionLocation> values = [];
        MemoryLayoutFactSet<MemoryLayoutVersionLocation> facts = MemoryLayoutFactSet<MemoryLayoutVersionLocation>.Known(values);
        values.Add(new MemoryLayoutMetadataVersionLocation("binding", "structure",
            new FirmwareMetadataField("version", 0, 1, FirmwareMetadataEncoding.Bytes),
            new FirmwareAddressedRange("output", new ByteRange(0, 1))));

        Assert.Equal(MemoryLayoutFactStatus.KnownEmpty, facts.Status);
        Assert.Empty(facts.Values);
        _ = Assert.Throws<NotSupportedException>(() => ((IList<MemoryLayoutVersionLocation>)facts.Values).Add(values[0]));
    }

    /// <summary>Adding facts preserves the original synthetic Standard Merge identities and segment results.</summary>
    [Fact]
    public void ExistingStandardMergeSnapshotMembersRemainUnchanged()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge);
        ActiveSessionSnapshot session = CreateSession(fixture,
            Slot("dp-input", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("tp-input", AuthoringSlotLifecycle.Verified, Capacity));

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability, session, fixture.Composition);

        Assert.Equal((fixture.Route.RouteId, fixture.Capability.CapabilityFingerprint,
            fixture.Composition.CompilationFingerprint, Token, session.AuthoringRevision),
            (layout.RouteId, layout.CapabilityFingerprint, layout.CompilationFingerprint,
                layout.ResolutionToken, layout.AuthoringRevision));
        Assert.Equal((MemoryLayoutGeometryKind.PhysicalMap, "synthetic-map", "flash", Capacity, (byte?)0),
            (layout.GeometryKind, layout.MapId, layout.AddressSpaceId, layout.Capacity, layout.BlankFillByte));
        Assert.Equal(fixture.ResolvedMap.ImageMap.Regions, layout.CanonicalRegions);
        Assert.Null(layout.SourceEnvelope);
        Assert.Equal(
            [(new ByteRange(0, 8), "dp-code", MemoryWorkflowDisposition.Blank),
             (new ByteRange(8, 4), "reserved-gap", MemoryWorkflowDisposition.Resolved),
             (new ByteRange(12, 4), "tp-code", MemoryWorkflowDisposition.Blank)],
            layout.BeforeSegments.Select(static segment => (segment.Range, segment.RegionId, segment.Disposition)));
        Assert.Equal(
            [(new ByteRange(0, 4), "dp-code", MemoryContentRole.Dp, MemoryWorkflowDisposition.WillWrite, "dp-input", "copy-dp"),
             (new ByteRange(4, 4), "dp-code", MemoryContentRole.Dp, MemoryWorkflowDisposition.Blank, null, ""),
             (new ByteRange(8, 4), "reserved-gap", MemoryContentRole.Reserved, MemoryWorkflowDisposition.Resolved, null, ""),
             (new ByteRange(12, 4), "tp-code", MemoryContentRole.Tp, MemoryWorkflowDisposition.WillWrite, "tp-input", "copy-tp")],
            layout.AfterSegments.Select(static segment => (segment.Range, segment.RegionId, segment.ContentRole,
                segment.Disposition, segment.SourceSpaceId,
                string.Join(",", segment.ContributingOperations.Select(static operation => operation.OperationId)))));
        Assert.Equal(["artifact-0", null, null, "artifact-1"],
            layout.AfterSegments.Select(static segment => segment.ContentSource?.ArtifactIdentity));
        Assert.All(layout.AfterSegments, static segment =>
        {
            Assert.Equal("flash", segment.AddressSpaceId);
            Assert.Equal(MemoryProcessorEffect.None, segment.ProcessorEffect);
            Assert.Equal(MemoryDiagnosticSeverity.None, segment.DiagnosticSeverity);
            Assert.Equal(MemoryObservedChange.NotObserved, segment.ObservedChange);
            Assert.Equal(MemoryFocusState.NotFocused, segment.Focus);
            Assert.Equal(MemoryEndpointIdentity.NotApplicable, segment.Endpoint);
            Assert.Equal(MemoryBankIdentity.NotApplicable, segment.Bank);
            Assert.Empty(segment.PreservationDetails);
        });
        Assert.Empty(layout.PendingItems);
        Assert.Empty(layout.SectionLocators);
        Assert.Empty(layout.Banks);
        Assert.False(layout.CanViewIndividualBanks);
    }
}

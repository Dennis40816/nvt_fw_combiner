// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Application.Tests.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using static NvtFwCombiner.Application.Tests.MemoryLayout.MemoryLayoutProjectorTestSupport;

namespace NvtFwCombiner.Bootstrap.Tests.MemoryLayout;

/// <summary>Checks declared layout facts against existing built-in profiles and the original snapshot contract.</summary>
[Collection(nameof(MemoryLayoutProjectorSerialGroup))]
public sealed class MemoryLayoutBuiltInFactTests
{
    /// <summary>Standard Merge steps retain the built-in compiler's ids, ordering and complete source/target ranges.</summary>
    [Fact]
    public void BuiltInStandardMergeStepFactsReuseExactCompiledOperations()
    {
        ProjectionFixture fixture = BuiltInFixture(BuiltInCompositionTestProfiles.StandardMerge());
        ActiveSessionSnapshot session = StandardSession(fixture);

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability, session, fixture.Composition);

        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, layout.Steps.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.None, layout.Steps.UnavailableReason);
        Assert.Equal(
            [
                ("copy-tp", 100, "tp-input", (ByteRange?)new ByteRange(0, 0x3C000), "output-image", new ByteRange(0, 0x3C000)),
                ("copy-dp", 200, "dp-input", (ByteRange?)new ByteRange(0x3E000, 0x2000), "output-image", new ByteRange(0x3E000, 0x2000)),
            ],
            layout.Steps.Values.Select(static step =>
                (step.OperationId, step.Sequence, step.SourceSpaceId, step.SourceRange, step.TargetSpaceId, step.TargetRange)));
        Assert.All(layout.Steps.Values, (step, index) =>
        {
            Assert.Same(fixture.Composition.Plan.OrderedOperations[index], step);
            Assert.Equal(CompositionOperationKind.CopyRange, step.Kind);
            Assert.Equal([step.TargetRange], step.DeclaredWriteRanges);
        });
        Assert.Equal("flash", layout.AddressSpaceId);
    }

    /// <summary>Authoring prohibitions survive permitted processor writes and are not preservation segments.</summary>
    [Fact]
    public void BuiltInAuthoringProhibitionsRemainReadOnlyWhenProcessorWillWriteThem()
    {
        ProjectionFixture fixture = BuiltInFixture(BuiltInCompositionTestProfiles.CtrlRamReplace());
        ActiveSessionSnapshot session = CreateSession(fixture,
            Slot("reference-base", AuthoringSlotLifecycle.Verified, fixture.Composition.Plan.OutputInitialization.Capacity),
            Slot("ctrlram-source-1", AuthoringSlotLifecycle.Verified, 1));

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability, session, fixture.Composition);
        MemoryLayoutSnapshot skeleton = MemoryLayoutProjector.Project(fixture.Capability, session, compiledOverlay: null);

        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, layout.ReadOnlyRanges.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.None, layout.ReadOnlyRanges.UnavailableReason);
        MemoryLayoutReadOnlyRange crc = Assert.Single(layout.ReadOnlyRanges.Values,
            static fact => fact.RegionId == "header-crc-0018");
        Assert.Equal(CompiledReadOnlyReasonKind.ProfileAccess, crc.ReasonKind);
        Assert.Equal(RegionAccessKind.Hidden, crc.Declaration.AccessRule!.Access);
        Assert.Equal(new FirmwareAddressedRange(layout.AddressSpaceId, new ByteRange(0x18, 4)), crc.OutputRange);
        Assert.Same(fixture.Composition.V2Details.RegionAccessContract.Requirements.First(
            static requirement => requirement.RegionId == "header-crc-0018"), crc.Declaration.AccessRule);
        Assert.Equal("Only the declared Legacy Combiner processor may recalculate ILM CRC 0.", crc.Declaration.AccessRule!.Reason);
        Assert.Equal(skeleton.ReadOnlyRanges.Values, layout.ReadOnlyRanges.Values);
        Assert.Contains(layout.AfterSegments, segment =>
            segment.ProcessorEffect == MemoryProcessorEffect.DeclaredWrite && segment.Range.Contains(crc.OutputRange.Range));
    }

    /// <summary>A forbidden gap remains effectively read-only without an explicit access rule.</summary>
    [Fact]
    public void BuiltInStandardMergeIncludesForbiddenGapWithoutAccessRule()
    {
        ProjectionFixture fixture = BuiltInFixture(BuiltInCompositionTestProfiles.StandardMerge());
        ActiveSessionSnapshot session = StandardSession(fixture);

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability, session, fixture.Composition);

        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, layout.ReadOnlyRanges.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.None, layout.ReadOnlyRanges.UnavailableReason);
        MemoryLayoutReadOnlyRange gap = Assert.Single(layout.ReadOnlyRanges.Values);
        Assert.Equal("unmapped-gap", gap.RegionId);
        Assert.Equal(new ByteRange(0x3C000, 0x2000), gap.OutputRange.Range);
        Assert.Equal(CompiledReadOnlyReasonKind.NoAccessRule, gap.ReasonKind);
        Assert.Equal(FirmwareWriteConstraint.Forbidden, gap.Declaration.Region.WriteConstraint);
    }

    /// <summary>A declared full writable DP container has a known-empty prohibition set.</summary>
    [Fact]
    public void BuiltInFullContainerGrantHasKnownEmptyReadOnlyRanges()
    {
        ProjectionFixture fixture = BuiltInFixture(BuiltInCompositionTestProfiles.StandardMerge("NT51950"));

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability, StandardSession(fixture), fixture.Composition);

        Assert.Equal(MemoryLayoutFactStatus.KnownEmpty, layout.ReadOnlyRanges.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.None, layout.ReadOnlyRanges.UnavailableReason);
        Assert.Empty(layout.ReadOnlyRanges.Values);
    }

    /// <summary>Canonical DPCMI carriers retain field identity, bit slice, and exact output coordinates.</summary>
    [Fact]
    public void BuiltInDpVersionLocationsRetainDeclaredFieldsAndNonzeroOutputOffsets()
    {
        ProjectionFixture fixture = BuiltInFixture(BuiltInCompositionTestProfiles.StandardMerge());
        ActiveSessionSnapshot session = InspectedStandardSession(fixture);

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability, session, fixture.Composition);

        Assert.Equal(MemoryLayoutFactStatus.KnownWithValues, layout.DpVersionLocations.Status);
        Assert.Equal(
            [
                ("dpcmi-inspection", "dpcmi", "dp-major", "flash", new ByteRange(0x3E015, 1)),
                ("dpcmi-inspection", "dpcmi", "dp-minor", "flash", new ByteRange(0x3E016, 1)),
            ],
            layout.DpVersionLocations.Values.Cast<MemoryLayoutMetadataVersionLocation>().Select(static fact =>
                (fact.BindingId, fact.StructureId, fact.FieldId, fact.OutputRange.AddressSpaceId, fact.OutputRange.Range)));
        FirmwareMetadataStructure definition = fixture.Capability.MetadataPlan.Entries.First(
            static entry => entry.Definition.BindingId == "dpcmi-inspection").Definition.StructureDefinition;
        Assert.Same(definition.Fields.First(static field => field.FieldId == "dp-major"),
            Assert.IsType<MemoryLayoutMetadataVersionLocation>(layout.DpVersionLocations.Values[0]).Field);
        Assert.Same(definition.Fields.First(static field => field.FieldId == "dp-minor"),
            Assert.IsType<MemoryLayoutMetadataVersionLocation>(layout.DpVersionLocations.Values[1]).Field);
        Assert.Equal(new FirmwareMetadataBitSlice(4, 4), Assert.IsType<MemoryLayoutMetadataVersionLocation>(layout.DpVersionLocations.Values[1]).Field.BitSlice);
    }

    /// <summary>A declaration alone does not fabricate a metadata location.</summary>
    [Fact]
    public void DeclaredDpVersionWithoutCurrentInspectionIsUnavailable()
    {
        ProjectionFixture fixture = BuiltInFixture(BuiltInCompositionTestProfiles.StandardMerge());

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(
            fixture.Capability, StandardSession(fixture), fixture.Composition);

        Assert.Equal(MemoryLayoutFactStatus.Unavailable, layout.DpVersionLocations.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.MetadataNotResolved, layout.DpVersionLocations.UnavailableReason);
        Assert.Empty(layout.DpVersionLocations.Values);
    }

    /// <summary>Blank initialization does not contain the inspected source version before a copy overlay.</summary>
    [Fact]
    public void InspectedDpVersionWithoutOutputOverlayHasNoGuessedLocation()
    {
        ProjectionFixture fixture = BuiltInFixture(BuiltInCompositionTestProfiles.StandardMerge());

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(
            fixture.Capability, InspectedStandardSession(fixture), compiledOverlay: null);

        Assert.Equal(MemoryLayoutFactStatus.Unavailable, layout.DpVersionLocations.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.OutputLocationNotResolved, layout.DpVersionLocations.UnavailableReason);
        Assert.Empty(layout.DpVersionLocations.Values);
    }

    /// <summary>A profile without a DP version declaration reports unavailable rather than known empty.</summary>
    [Fact]
    public void BuiltInProfileWithoutDpVersionReportsNotDeclared()
    {
        ProjectionFixture fixture = BuiltInFixture(BuiltInCompositionTestProfiles.CtrlRamReplace());
        ActiveSessionSnapshot session = CreateSession(fixture,
            Slot("reference-base", AuthoringSlotLifecycle.Verified, fixture.Composition.Plan.OutputInitialization.Capacity),
            Slot("ctrlram-source-1", AuthoringSlotLifecycle.Verified, 1));

        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(fixture.Capability, session, fixture.Composition);

        Assert.Equal(MemoryLayoutFactStatus.Unavailable, layout.DpVersionLocations.Status);
        Assert.Equal(MemoryLayoutFactUnavailableReason.NotDeclared, layout.DpVersionLocations.UnavailableReason);
        Assert.Empty(layout.DpVersionLocations.Values);
    }

    private static ProjectionFixture BuiltInFixture(
        (CompiledComposition Composition, MetadataPlanDefinition MetadataPlan) profile)
    {
        MapBoundV2CompilationContext context = Assert.IsType<MapBoundV2CompilationContext>(
            profile.Composition.V2Details.Provenance.Context, exactMatch: false);
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolved = context.ResolvedMap;
        var route = new CapabilityRouteIdentity(resolved.MemberId, profile.Composition.V2Details.ExperienceId,
            profile.Composition.V2Details.IcNumberInputMode is null ? "selector-free" : "1-ic", resolved.ImageMap.MapId);
        CanonicalCapabilityCompilationContract? contract = context is RuntimeReferenceReplaceV2CompilationContext
            ? new CanonicalCapabilityCompilationContract(profile.Composition.V2Details.ProfileId,
                profile.Composition.V2Details.ProfileVersion, profile.Composition.V2Details.Provenance.Bundle.ContentHash,
                [resolved.ImageMap.MapId], CapabilityDefinitionFingerprint.RuntimeReferenceReplaceCompilerSemanticId,
                profile.Composition.Plan.OrderedOperations
                    .Where(static operation => operation.Kind == CompositionOperationKind.RunExternalProcessor)
                    .Select(static operation => $"postbuild-processor:{operation.ExternalProcessorInvocation!.ProcessorId}"))
            : null;
        ResolvedCapability capability = Capability(route, profile.Composition, profile.MetadataPlan, contract);
        return new ProjectionFixture(route, capability, capability.CompiledComposition, resolved,
            resolved.ImageMap.Regions[0], resolved.ImageMap.Regions[^1]);
    }

    private static ActiveSessionSnapshot StandardSession(ProjectionFixture fixture)
    {
        return CreateSession(fixture,
            Slot("dp-input", AuthoringSlotLifecycle.Verified, fixture.ResolvedMap.CapacityBytes),
            Slot("tp-input", AuthoringSlotLifecycle.Verified, fixture.ResolvedMap.CapacityBytes));
    }

    private static ActiveSessionSnapshot InspectedStandardSession(ProjectionFixture fixture)
    {
        return CreateInspectedSession(fixture);
    }
}

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

/// <summary>Exercises the pure canonical memory-layout projection contract.</summary>
public sealed partial class MemoryLayoutProjectorTests
{
    private const long Capacity = 16;
    private static readonly ResolutionToken Token = new("catalog-1");

    /// <summary>Projects Merge blank initialization, selected writes, and pending input.</summary>
    [Fact]
    public void StandardMergeProjectsCanonicalGeometryAndSelectedWrites()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge);
        ActiveSessionSnapshot session = CreateSession(
            fixture,
            Slot("dp-input", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("tp-input", AuthoringSlotLifecycle.Empty));

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability,
            session,
            fixture.Composition);

        Assert.Equal(
            fixture.Capability.CapabilityFingerprint,
            snapshot.CapabilityFingerprint);
        Assert.Equal(
            fixture.Composition.CompilationFingerprint,
            snapshot.CompilationFingerprint);
        Assert.NotEqual(
            snapshot.CapabilityFingerprint,
            snapshot.CompilationFingerprint);
        Assert.Equal("flash", snapshot.AddressSpaceId);
        Assert.Equal(Capacity, snapshot.Capacity);
        Assert.Equal(
            [new ByteRange(0, 8), new ByteRange(8, 4), new ByteRange(12, 4)],
            snapshot.BeforeSegments.Select(static segment => segment.Range));
        Assert.Equal(
            [
                MemoryWorkflowDisposition.Blank,
                MemoryWorkflowDisposition.Resolved,
                MemoryWorkflowDisposition.Blank,
            ],
            snapshot.BeforeSegments.Select(static segment => segment.Disposition));
        Assert.Equal(
            [
                MemoryWorkflowDisposition.WillWrite,
                MemoryWorkflowDisposition.Blank,
                MemoryWorkflowDisposition.Resolved,
                MemoryWorkflowDisposition.Blank,
            ],
            snapshot.AfterSegments.Select(static segment => segment.Disposition));
        Assert.Equal(
            [
                MemoryContentRole.Dp,
                MemoryContentRole.Dp,
                MemoryContentRole.Reserved,
                MemoryContentRole.Tp,
            ],
            snapshot.AfterSegments.Select(static segment => segment.ContentRole));

        MemoryLayoutSegment dp = snapshot.AfterSegments[0];
        Assert.Same(fixture.DpRegion, dp.CanonicalRegion);
        Assert.Equal("dp-input", dp.SourceSlotId);
        Assert.Equal(["copy-dp"], dp.ContributingOperationIds);
        Assert.Equal(MemorySelectionState.Selected, dp.Selection);
        Assert.Equal(MemoryObservedChange.NotObserved, dp.ObservedChange);
        Assert.Equal(MemoryFocusState.NotFocused, dp.Focus);
        Assert.Equal(MemoryBankIdentity.NotApplicable, dp.Bank);
        Assert.Equal(MemoryEndpointIdentity.NotApplicable, dp.Endpoint);

        MemoryLayoutPendingItem pending = Assert.Single(snapshot.PendingItems);
        Assert.Equal("tp-input", pending.SlotId);
        Assert.Equal(MemoryLayoutReadiness.PendingInput, pending.Readiness);
        Assert.Equal(MemoryLayoutPrerequisite.SelectInput, pending.Prerequisite);
        Assert.Equal(MemoryLayoutNextAction.SelectInput, pending.NextAction);
        Assert.Null(pending.KnownInputLength);
        Assert.DoesNotContain(
            typeof(MemoryLayoutPendingItem).GetProperties(),
            static property => property.PropertyType == typeof(ByteRange));
        Assert.DoesNotContain(
            snapshot.AfterSegments,
            static segment => segment.Disposition == MemoryWorkflowDisposition.Kept);
    }

    /// <summary>Projects Replace reference preservation outside admitted selected writes.</summary>
    [Fact]
    public void DpReplaceKeepsReferenceBytesOutsideSelectedReplacement()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Replace);
        ActiveSessionSnapshot session = CreateSession(
            fixture,
            Slot("reference-base", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("dp-replacement", AuthoringSlotLifecycle.Warning, Capacity));

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability,
            session,
            fixture.Composition);

        Assert.All(
            snapshot.BeforeSegments,
            static segment => Assert.Equal(
                MemoryWorkflowDisposition.Kept,
                segment.Disposition));
        Assert.Equal(
            [
                MemoryWorkflowDisposition.WillReplace,
                MemoryWorkflowDisposition.Kept,
                MemoryWorkflowDisposition.Kept,
                MemoryWorkflowDisposition.Kept,
            ],
            snapshot.AfterSegments.Select(static segment => segment.Disposition));
        Assert.Equal(
            [
                MemoryDiagnosticSeverity.Warning,
                MemoryDiagnosticSeverity.None,
                MemoryDiagnosticSeverity.None,
                MemoryDiagnosticSeverity.None,
            ],
            snapshot.AfterSegments.Select(static segment => segment.DiagnosticSeverity));
        Assert.Empty(snapshot.PendingItems);
        Assert.Same(fixture.DpRegion, snapshot.AfterSegments[0].CanonicalRegion);
        Assert.Same(fixture.DpRegion, snapshot.AfterSegments[1].CanonicalRegion);
        Assert.Same(fixture.TpRegion, snapshot.AfterSegments[3].CanonicalRegion);
        Assert.Equal(["replace-dp"], snapshot.AfterSegments[0].ContributingOperationIds);
        Assert.Empty(snapshot.AfterSegments[1].ContributingOperationIds);
        Assert.Empty(snapshot.AfterSegments[3].ContributingOperationIds);
    }

    /// <summary>Keeps unresolved inputs non-geometric until a compiled overlay is supplied.</summary>
    [Fact]
    public void MissingCompiledOverlayKeepsSkeletonAndPublishesNonGeometricPendingState()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge);
        ActiveSessionSnapshot session = CreateSession(
            fixture,
            Slot("dp-input", AuthoringSlotLifecycle.Checking, length: 12),
            Slot("tp-input", AuthoringSlotLifecycle.Empty));

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability,
            session,
            compiledOverlay: null);

        Assert.Same(snapshot.BeforeSegments, snapshot.AfterSegments);
        Assert.Equal(2, snapshot.PendingItems.Count);
        MemoryLayoutPendingItem checking = Assert.Single(
            snapshot.PendingItems,
            static item => item.SlotId == "dp-input");
        Assert.Equal(12, checking.KnownInputLength);
        Assert.Equal(MemoryLayoutPrerequisite.CompleteInspection, checking.Prerequisite);
        Assert.Equal(MemoryLayoutNextAction.WaitForInspection, checking.NextAction);
        Assert.All(
            snapshot.AfterSegments,
            static segment => Assert.Empty(segment.ContributingOperationIds));

        ActiveSessionSnapshot selectedSession = CreateSession(
            fixture,
            Slot("dp-input", AuthoringSlotLifecycle.Selected, length: 12),
            Slot("tp-input", AuthoringSlotLifecycle.Empty));
        MemoryLayoutPendingItem selected = Assert.Single(
            MemoryLayoutProjector.Project(
                    fixture.Capability,
                    selectedSession,
                    compiledOverlay: null)
                .PendingItems,
            static item => item.SlotId == "dp-input");
        Assert.Equal(MemoryLayoutNextAction.RunInspection, selected.NextAction);
        Assert.Equal(MemoryLayoutReadiness.PendingInput, selected.Readiness);
        Assert.Null(selected.BlockedIssue);
    }

    /// <summary>Retains kept subranges under one primary canonical segment.</summary>
    [Fact]
    public void PreservationDetailsRemainSubordinateToOneCanonicalSegment()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Replace);
        var detail = new MemoryLayoutPreservationDetail(
            "active-nf-0",
            blockIndex: 0,
            MemoryEndpointIdentity.Slave,
            "diffdlm-input",
            new ByteRange(0x0B90, 0x0870),
            new ByteRange(2, 2));
        var segment = MemoryLayoutSegment.Create(
            "dp-code:0-8",
            "flash",
            new ByteRange(0, 8),
            fixture.DpRegion,
            MemoryContentRole.Dp,
            MemoryWorkflowDisposition.WillReplace,
            MemoryEndpointIdentity.NotApplicable,
            MemoryBankIdentity.NotApplicable,
            MemoryProcessorEffect.None,
            MemoryDiagnosticSeverity.None,
            MemoryObservedChange.NotObserved,
            MemorySelectionState.Selected,
            MemoryFocusState.NotFocused,
            "diffdlm-input",
            "diffdlm",
            ["replace-active-dlm"],
            [detail]);

        MemoryLayoutPreservationDetail actual = Assert.Single(segment.PreservationDetails);
        Assert.Same(detail, actual);
        Assert.Equal(MemoryWorkflowDisposition.Kept, actual.Disposition);
        Assert.Equal(new ByteRange(0, 8), segment.Range);
        Assert.Equal(new ByteRange(2, 2), actual.ResolvedRange);
        Assert.Same(fixture.DpRegion, segment.CanonicalRegion);
    }

    /// <summary>Rejects subordinate kept details that overlap inside their primary segment.</summary>
    [Fact]
    public void PreservationDetailsRemainCheckedAndNonAuthoritative()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Replace);
        var first = new MemoryLayoutPreservationDetail(
            "nf-0",
            0,
            MemoryEndpointIdentity.Slave,
            "diffdlm-input",
            new ByteRange(0, 2),
            new ByteRange(1, 2));
        var overlapping = new MemoryLayoutPreservationDetail(
            "nf-1",
            1,
            MemoryEndpointIdentity.Slave,
            "diffdlm-input",
            new ByteRange(2, 2),
            new ByteRange(2, 2));

        _ = Assert.Throws<ArgumentException>(() =>
            MemoryLayoutSegment.Create(
                "dp-code:0-8",
                "flash",
                new ByteRange(0, 8),
                fixture.DpRegion,
                MemoryContentRole.Dp,
                MemoryWorkflowDisposition.WillReplace,
                MemoryEndpointIdentity.NotApplicable,
                MemoryBankIdentity.NotApplicable,
                MemoryProcessorEffect.None,
                MemoryDiagnosticSeverity.None,
                MemoryObservedChange.NotObserved,
                MemorySelectionState.Selected,
                MemoryFocusState.NotFocused,
                "diffdlm-input",
                "diffdlm",
                ["replace-active-dlm"],
                [first, overlapping]));
    }

    /// <summary>Exposes projection collections as immutable snapshots.</summary>
    [Fact]
    public void SnapshotCollectionsAreImmutable()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge);
        ActiveSessionSnapshot session = CreateSession(
            fixture,
            Slot("dp-input", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("tp-input", AuthoringSlotLifecycle.Empty));
        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability,
            session,
            fixture.Composition);

        Assert.True(((IList<MemoryLayoutSegment>)snapshot.AfterSegments).IsReadOnly);
        Assert.True(((IList<FirmwareRegion>)snapshot.CanonicalRegions).IsReadOnly);
        Assert.True(((IList<MemoryLayoutPendingItem>)snapshot.PendingItems).IsReadOnly);
    }

    /// <summary>Rejects stale authoring identity and separately compiled look-alike artifacts.</summary>
    [Fact]
    public void ProjectionRejectsStaleSessionAndRecompiledOverlay()
    {
        ProjectionFixture fixture = CreateFixture(CompositionKind.Merge);
        ActiveSessionSnapshot stale = CreateSession(
            fixture,
            capabilityFingerprint:
                "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
            Slot("dp-input", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("tp-input", AuthoringSlotLifecycle.Verified, Capacity));

        _ = Assert.Throws<ArgumentException>(() =>
            MemoryLayoutProjector.Project(fixture.Capability, stale, fixture.Composition));

        ProjectionFixture other = CreateFixture(CompositionKind.Merge);
        ActiveSessionSnapshot current = CreateSession(
            fixture,
            Slot("dp-input", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("tp-input", AuthoringSlotLifecycle.Verified, Capacity));
        _ = Assert.Throws<ArgumentException>(() =>
            MemoryLayoutProjector.Project(
                fixture.Capability,
                current,
                other.Composition));
    }

    private static ProjectionFixture CreateFixture(
        CompositionKind kind,
        CompositionPlan? customPlan = null)
    {
        string workflowId = kind == CompositionKind.Merge
            ? ExperienceIds.StandardMerge
            : ExperienceIds.DpReplace;
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap =
            CreateResolvedMap(workflowId);
        FirmwareRegion dp = resolvedMap.ImageMap.Regions.Single(
            static region => region.RegionId == "dp-code");
        FirmwareRegion tp = resolvedMap.ImageMap.Regions.Single(
            static region => region.RegionId == "tp-code");
        CompiledInputContract inputContract = kind == CompositionKind.Merge
            ? MergeInputContract()
            : ReplaceInputContract();
        CompositionPlan plan = customPlan ??
            (kind == CompositionKind.Merge
                ? MergePlan()
                : ReplacePlan());
        var provenance = new V2CompilationProvenance(
            new ProfileBundleIdentity(
                $"bundle-{workflowId}",
                "1.0.0",
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "test-binding"),
            new ProfileBundleEntryIdentity(
                $"profile-{workflowId}",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
            new ResolvedMapV2CompilationContext(resolvedMap),
            new CompiledProfilePromotion(CompiledProfilePromotionStage.Supported, []),
            ["test-evidence"],
            [],
            []);
        var details = new V2CompiledCompositionDetails(
            $"profile-{workflowId}",
            "1.0.0",
            workflowId,
            kind,
            provenance,
            inputContract,
            new CompiledRegionAccessContract([], []),
            new CompiledOutputNamingRequirement(
                $"{workflowId}.bin",
                allowOverride: true,
                CompiledOutputInvalidCharacterPolicy.Reject,
                []),
            kind == CompositionKind.Merge
                ? null
                : IcNumberInputMode.SingleSelector);
        var composition = CompiledComposition.CreateV2RuntimeExecutable(plan, details);
        var route = new CapabilityRouteIdentity(
            "NT-SYNTHETIC",
            workflowId,
            "selector-free",
            resolvedMap.ImageMap.MapId);
        ResolvedCapability capability = Capability(route, composition);
        return new ProjectionFixture(
            route,
            capability,
            capability.CompiledComposition,
            resolvedMap,
            dp,
            tp);
    }

    private static FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap
        CreateResolvedMap(string modeId)
    {
        FirmwareRegion[] regions =
        [
            new(
                "flash-image",
                parentRegionId: null,
                FirmwareRegionOwner.System,
                FirmwareRegionKind.Image,
                new ByteRange(0, Capacity),
                FirmwareWriteConstraint.Forbidden),
            new(
                "dp-code",
                "flash-image",
                FirmwareRegionOwner.Dp,
                FirmwareRegionKind.Code,
                new ByteRange(0, 8),
                FirmwareWriteConstraint.WholeRegion),
            new(
                "dp-code-before-anchor",
                "dp-code",
                FirmwareRegionOwner.Dp,
                FirmwareRegionKind.Code,
                new ByteRange(0, 4),
                FirmwareWriteConstraint.WholeRegion),
            new(
                "dp-code-anchor",
                "dp-code",
                FirmwareRegionOwner.Dp,
                FirmwareRegionKind.Command,
                new ByteRange(4, 4),
                FirmwareWriteConstraint.WholeRegion),
            new(
                "reserved-gap",
                "flash-image",
                FirmwareRegionOwner.Reserved,
                FirmwareRegionKind.Reserved,
                new ByteRange(8, 4),
                FirmwareWriteConstraint.Forbidden),
            new(
                "tp-code",
                "flash-image",
                FirmwareRegionOwner.Tp,
                FirmwareRegionKind.Code,
                new ByteRange(12, 4),
                FirmwareWriteConstraint.WholeRegion),
        ];
        FirmwareImageMap map = FirmwareImageMapTestFactory.CreateDirect(
            "synthetic-map",
            "flash",
            new FirmwareMapApplicability(
                ["NT-SYNTHETIC"],
                [modeId],
                TopologyRequirement.NoTopologyConstraint(),
                Capacity),
            FirmwareImageMapCoveragePolicy.CompleteWithExplicitGaps,
            [new FirmwareRegionSet("physical", "flash", regions, ["test-evidence"])],
            [],
            ["test-evidence"]);
        var definition = new FirmwareFamilyResolutionDefinition(
            "synthetic-family",
            "1.0.0",
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
            [map],
            []);
        FirmwareMapResolutionResult result = definition.ResolveMap(
            new FirmwareMapResolutionInputs(
                "NT-SYNTHETIC",
                modeId,
                Capacity,
                requestedTopology: null,
                []));
        return Assert.IsType<FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap>(
            result.ResolvedMap);
    }

    private static CompiledInputContract MergeInputContract()
    {
        return new CompiledInputContract(
            [
                SlotRequirement(
                    "dp-input",
                    "dp",
                    CompiledInputArtifactClass.DpFirmware,
                    new CompiledExactResolvedMapCapacityInputLengthRequirement(Capacity)),
                SlotRequirement(
                    "tp-input",
                    "tp",
                    CompiledInputArtifactClass.TpFirmware,
                    new CompiledExactBytesInputLengthRequirement(Capacity)),
            ],
            [
                new CompiledInputSpaceBinding(
                    "dp-input",
                    "dp-input",
                    CompiledInputInstancePolicy.Singleton),
                new CompiledInputSpaceBinding(
                    "tp-input",
                    "tp-input",
                    CompiledInputInstancePolicy.Singleton),
            ]);
    }

    private static CompiledInputContract ReplaceInputContract()
    {
        return new CompiledInputContract(
            [
                SlotRequirement(
                    "reference-base",
                    "reference",
                    CompiledInputArtifactClass.ReferenceImage,
                    new CompiledExactResolvedMapCapacityInputLengthRequirement(Capacity)),
                SlotRequirement(
                    "dp-replacement",
                    "dp",
                    CompiledInputArtifactClass.DpFirmware,
                    new CompiledExactResolvedMapCapacityInputLengthRequirement(Capacity)),
            ],
            [
                new CompiledInputSpaceBinding(
                    "reference-base",
                    "reference-base",
                    CompiledInputInstancePolicy.Singleton),
                new CompiledInputSpaceBinding(
                    "dp-replacement",
                    "dp-replacement",
                    CompiledInputInstancePolicy.Singleton),
            ]);
    }

    private static CompiledInputSlotRequirement SlotRequirement(
        string slotId,
        string role,
        CompiledInputArtifactClass artifactClass,
        CompiledInputLengthRequirement length)
    {
        return CompiledInputSlotTestFactory.Create(
            slotId,
            role,
            artifactClass,
            required: true,
            CompiledInputSlotCardinality.ExactlyOne,
            [".bin"],
            length,
            new CompiledNoInputNormalization());
    }

    private static CompositionPlan MergePlan()
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
                    "copy-dp",
                    100,
                    "dp-input",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(0, 4),
                    OverlapPolicy.Reject,
                    "copy DP"),
                CompositionOperation.CopyRange(
                    "copy-tp",
                    200,
                    "tp-input",
                    new ByteRange(12, 4),
                    "output-image",
                    new ByteRange(12, 4),
                    OverlapPolicy.Reject,
                    "copy TP"),
            ]);
    }

    private static CompositionPlan ReplacePlan()
    {
        return new CompositionPlan(
            ImageInitialization.Reference(
                "output-image",
                "reference-base",
                Capacity),
            [
                new AddressSpace(
                    "reference-base",
                    Capacity,
                    AddressSpaceMutability.Immutable),
                new AddressSpace(
                    "dp-replacement",
                    Capacity,
                    AddressSpaceMutability.Immutable),
                new AddressSpace(
                    "output-image",
                    Capacity,
                    AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.ReplaceRange(
                    "replace-dp",
                    100,
                    "dp-replacement",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(0, 4),
                    OverlapPolicy.Reject,
                    "replace DP"),
            ]);
    }

    private static ResolvedCapability Capability(
        CapabilityRouteIdentity route,
        CompiledComposition composition)
    {
        const string fingerprint =
            "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
        return new ResolvedCapability(
            route,
            fingerprint,
            composition,
            Decision(
                "authoring",
                route,
                fingerprint,
                CapabilityAuthoringAvailability.Available),
            Decision(
                "publication",
                route,
                fingerprint,
                CapabilityPublicationStatus.Supported),
            Decision(
                "evidence",
                route,
                fingerprint,
                CapabilityEvidenceStatus.SyntheticOracle),
            MetadataPlanDefinition.Empty.Resolve(Token),
            Token);
    }

    private static PinnedCapabilityDecision<T> Decision<T>(
        string id,
        CapabilityRouteIdentity route,
        string fingerprint,
        T value)
        where T : struct, Enum
    {
        return new PinnedCapabilityDecision<T>(
            id,
            route.RouteId,
            fingerprint,
            value,
            "test-source");
    }

    private static ActiveSessionSnapshot CreateSession(
        ProjectionFixture fixture,
        params AuthoringSlotState[] slots)
    {
        return CreateSession(fixture, fixture.Capability.CapabilityFingerprint, slots);
    }

    private static ActiveSessionSnapshot CreateSession(
        ProjectionFixture fixture,
        string capabilityFingerprint,
        params AuthoringSlotState[] slots)
    {
        return new ActiveSessionSnapshot(
            fixture.Route.WorkflowId,
            Token,
            new AuthoringRevision(3),
            fixture.Route.RouteId,
            capabilityFingerprint,
            executionAdmitted: true,
            fixture.Route.IcId,
            fixture.Route.IcCountVariant,
            fixture.Route.MapVariant,
            [fixture.Route.IcId],
            [fixture.Route.IcCountVariant],
            slots,
            draftState: null,
            draftCapabilityFingerprint: null,
            derivedPublications: []);
    }

    private static AuthoringSlotState Slot(
        string slotId,
        AuthoringSlotLifecycle lifecycle,
        long length = 0,
        AuthoringSlotIssueReference? blockingIssue = null)
    {
        bool empty = lifecycle == AuthoringSlotLifecycle.Empty;
        return new AuthoringSlotState(
            slotId,
            empty ? null : $"{slotId}.bin",
            empty
                ? null
                : new FileStamp(length, new string('0', 64)),
            lifecycle,
            blockingIssue);
    }

    private sealed record ProjectionFixture(
        CapabilityRouteIdentity Route,
        ResolvedCapability Capability,
        CompiledComposition Composition,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap ResolvedMap,
        FirmwareRegion DpRegion,
        FirmwareRegion TpRegion);
}

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
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
                MemoryWorkflowDisposition.WillWrite,
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
        Assert.Equal(["copy-dp"], dp.ContributingOperations.Select(static operation => operation.OperationId));
        Assert.Equal(MemorySelectionState.Selected, dp.Selection);
        Assert.Equal(MemoryObservedChange.NotObserved, dp.ObservedChange);
        Assert.Equal(MemoryFocusState.NotFocused, dp.Focus);
        Assert.Equal(MemoryBankIdentity.NotApplicable, dp.Bank);
        Assert.Equal(MemoryEndpointIdentity.NotApplicable, dp.Endpoint);

        MemoryLayoutSegment tp = Assert.Single(
            snapshot.AfterSegments,
            static segment => segment.ContributingOperations.Any(
                static operation => operation.OperationId == "copy-tp"));
        Assert.Equal("tp-input", tp.SourceSlotId);
        Assert.Equal(MemorySelectionState.NotSelected, tp.Selection);

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
            static segment =>
            {
                Assert.Equal(MemoryWorkflowDisposition.Kept, segment.Disposition);
                Assert.Equal("reference-base", segment.SourceSpaceId);
                Assert.Equal("reference-base", segment.SourceSlotId);
            });
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
        Assert.Equal(
            ["replace-dp"],
            snapshot.AfterSegments[0].ContributingOperations.Select(static operation => operation.OperationId));
        Assert.Empty(snapshot.AfterSegments[1].ContributingOperations);
        Assert.Empty(snapshot.AfterSegments[3].ContributingOperations);
        Assert.All(
            snapshot.AfterSegments.Where(static segment =>
                segment.Disposition == MemoryWorkflowDisposition.Kept),
            static segment =>
            {
                Assert.Equal("reference-base", segment.SourceSpaceId);
                Assert.Equal("reference-base", segment.SourceSlotId);
            });
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
            static segment => Assert.Empty(segment.ContributingOperations));

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

    /// <summary>TP Overview labels cannot redefine canonical CtrlRAM geometry or grouping identity.</summary>
    [Fact]
    public void CtrlRamDisplayGroupingBindsByExactCanonicalRange()
    {
        ProjectionFixture fixture = CreateFixture(
            CompositionKind.Replace,
            ctrlRamMap: true);
        ActiveSessionSnapshot session = CreateSession(
            fixture,
            Slot("reference-base", AuthoringSlotLifecycle.Verified, Capacity),
            Slot("dp-replacement", AuthoringSlotLifecycle.Verified, Capacity));
        var display = new CtrlRamRegion(
            "tp-overview-display-id",
            "Slave Right CtrlRAM",
            fixture.TpRegion.Range.Start,
            fixture.TpRegion.Range.Length,
            IsMultiChipOnly: true,
            ReplaceRegionGroup.SlaveRight,
            CtrlRamRegionRole.Vn);

        MemoryLayoutSnapshot snapshot = MemoryLayoutProjector.Project(
            fixture.Capability,
            session,
            fixture.Composition,
            [display]);

        MemoryLayoutSegment segment = Assert.Single(
            snapshot.AfterSegments,
            candidate => ReferenceEquals(candidate.CanonicalRegion, fixture.TpRegion));
        Assert.Equal("tp-code", segment.RegionId);
        Assert.Equal(ReplaceRegionGroup.SlaveRight, segment.RegionGroup);
        Assert.Equal(CtrlRamRegionRole.Vn, segment.CtrlRamRegionRole);

        _ = Assert.Throws<ArgumentException>(() =>
        {
            _ = MemoryLayoutProjector.Project(
                fixture.Capability,
                session,
                fixture.Composition,
                [display, display with { RegionId = "duplicate-display-id" }]);
        });
        _ = Assert.Throws<ArgumentException>(() =>
        {
            _ = MemoryLayoutProjector.Project(
                fixture.Capability,
                session,
                fixture.Composition,
                [display with { Start = display.Start + 1 }]);
        });
    }

    private static ProjectionFixture CreateFixture(
        CompositionKind kind,
        CompositionPlan? customPlan = null,
        CompiledInputContract? customInputContract = null,
        string? customWorkflowId = null,
        bool ctrlRamMap = false,
        IReadOnlyList<FirmwareRegion>? customRegions = null)
    {
        string workflowId = customWorkflowId ?? (kind == CompositionKind.Merge
            ? ExperienceIds.StandardMerge
            : ExperienceIds.DpReplace);
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap =
            CreateResolvedMap(workflowId, ctrlRamMap, customRegions);
        FirmwareRegion dp = resolvedMap.ImageMap.Regions.Single(
            static region => region.RegionId == "dp-code");
        FirmwareRegion tp = resolvedMap.ImageMap.Regions.Single(
            static region => region.RegionId == "tp-code");
        CompiledInputContract inputContract = customInputContract ??
            (kind == CompositionKind.Merge
                ? MergeInputContract()
                : ReplaceInputContract());
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
            StringComparer.Ordinal.Equals(workflowId, ExperienceIds.GeneralReplace)
                ? "1-ic"
                : "selector-free",
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
        CreateResolvedMap(
            string modeId,
            bool ctrlRamMap,
            IReadOnlyList<FirmwareRegion>? customRegions)
    {
        FirmwareRegion[] regions = customRegions is null
            ?
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
                    ctrlRamMap ? FirmwareRegionKind.CtrlRam : FirmwareRegionKind.Code,
                    new ByteRange(12, 4),
                    FirmwareWriteConstraint.WholeRegion),
            ]
            : [.. customRegions];
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

    private static ActiveSessionSnapshot CreateSessionWithDraft(
        ProjectionFixture fixture,
        AuthoringDraftState draftState,
        params AuthoringSlotState[] slots)
    {
        return new ActiveSessionSnapshot(
            fixture.Route.WorkflowId,
            Token,
            new AuthoringRevision(3),
            fixture.Route.RouteId,
            fixture.Capability.CapabilityFingerprint,
            executionAdmitted: true,
            fixture.Route.IcId,
            fixture.Route.IcCountVariant,
            fixture.Route.MapVariant,
            [fixture.Route.IcId],
            [fixture.Route.IcCountVariant],
            slots,
            draftState,
            fixture.Capability.CapabilityFingerprint,
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

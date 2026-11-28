using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests.MemoryLayout;

/// <summary>Provides shared fixtures and helpers for memory-layout projector tests.</summary>
internal static class MemoryLayoutProjectorTestSupport
{
    internal const long Capacity = 16;
    internal static readonly ResolutionToken Token = new("catalog-1");

    internal static void AssertOutputWriteInvariants(MemoryLayoutSnapshot layout, CompositionPlan plan)
    {
        Assert.NotEqual(MemoryLayoutFactStatus.Unavailable, layout.OutputWrites.Status);
        ByteRange output = new(0, layout.Capacity);
        Assert.All(layout.OutputWrites.Values, write =>
        {
            Assert.Equal(layout.AddressSpaceId, write.OutputRange.AddressSpaceId);
            Assert.True(output.Contains(write.OutputRange.Range));
            Assert.Contains(plan.OrderedOperations, step => step.OperationId == write.OperationId);
        });
        foreach (MemoryLayoutOutputWrite left in layout.OutputWrites.Values)
        {
            foreach (MemoryLayoutOutputWrite right in layout.OutputWrites.Values.Where(right =>
                right.OperationId != left.OperationId && right.OutputRange.Range.Overlaps(left.OutputRange.Range)))
            {
                CompositionOperation first = plan.OrderedOperations.First(step => step.OperationId == left.OperationId);
                CompositionOperation second = plan.OrderedOperations.First(step => step.OperationId == right.OperationId);
                ByteRange intersection = left.OutputRange.Range.Intersect(right.OutputRange.Range)!.Value;
                CompositionOperation later = first.Sequence > second.Sequence ? first : second;
                Assert.True(later.OverlapPolicy is OverlapPolicy.ReplaceExisting or OverlapPolicy.AllowDeclared ||
                    PlacementAllowsOverlap(plan, first.TargetSpaceId, first.TargetRange, first.Sequence, intersection) ||
                    PlacementAllowsOverlap(plan, second.TargetSpaceId, second.TargetRange, second.Sequence, intersection),
                    $"Output writes '{first.OperationId}' and '{second.OperationId}' overlap without a declared overlap policy.");
            }
        }
    }

    private static bool PlacementAllowsOverlap(CompositionPlan plan, string spaceId, ByteRange range,
        int sequence, ByteRange outputIntersection)
    {
        foreach (CompositionOperation copy in plan.OrderedOperations.Where(operation =>
            operation.Sequence > sequence && operation.SourceSpaceId == spaceId &&
            operation.Kind is CompositionOperationKind.CopyRange or CompositionOperationKind.ReplaceRange))
        {
            if (copy.SourceRange is not { } read || range.Intersect(read) is not { } slice)
            {
                continue;
            }
            ByteRange placed = new(checked(copy.TargetRange.Start + slice.Start - read.Start), slice.Length);
            if (copy.TargetSpaceId == plan.OutputSpaceId)
            {
                if (placed.Contains(outputIntersection) &&
                    copy.OverlapPolicy is OverlapPolicy.ReplaceExisting or OverlapPolicy.AllowDeclared)
                {
                    return true;
                }
            }
            else if (PlacementAllowsOverlap(plan, copy.TargetSpaceId, placed, copy.Sequence, outputIntersection))
            {
                return true;
            }
        }
        return false;
    }

    internal static ProjectionFixture CreateFixture(
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

    internal static FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap
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

    internal static CompiledInputContract MergeInputContract()
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

    internal static CompiledInputContract ReplaceInputContract()
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

    internal static CompiledInputSlotRequirement SlotRequirement(
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

    internal static CompositionPlan MergePlan()
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

    internal static CompositionPlan ReplacePlan()
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

    internal static ResolvedCapability Capability(
        CapabilityRouteIdentity route,
        CompiledComposition composition,
        MetadataPlanDefinition? metadataPlan = null,
        CanonicalCapabilityCompilationContract? compilationContract = null)
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
            (metadataPlan ?? MetadataPlanDefinition.Empty).Resolve(Token),
            Token,
            compilationContract);
    }

    internal static PinnedCapabilityDecision<T> Decision<T>(
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

    internal static ActiveSessionSnapshot CreateSession(
        ProjectionFixture fixture,
        params AuthoringSlotState[] slots)
    {
        return CreateSession(fixture, fixture.Capability.CapabilityFingerprint, slots);
    }

    internal static ActiveSessionSnapshot CreateInspectedSession(ProjectionFixture fixture)
    {
        CompositionPlan plan = fixture.Composition.Plan;
        ActiveSessionSnapshot session = CreateSession(fixture,
            Slot("dp-input", AuthoringSlotLifecycle.Verified, plan.OutputInitialization.Capacity),
            Slot("tp-input", AuthoringSlotLifecycle.Verified, plan.OutputInitialization.Capacity));
        MetadataInspectionSnapshot inspection = FirmwareMetadataInspector.Inspect(new MetadataInspectionRequest(
            fixture.Capability.MetadataPlan, session.AuthoringRevision.Value,
            [new FirmwareArtifactPayload("dp-input", new byte[checked((int)plan.OutputInitialization.Capacity)]),
             new FirmwareArtifactPayload("tp-input", new byte[checked((int)plan.OutputInitialization.Capacity)])]));
        return new ActiveSessionSnapshot(session.WorkflowId, session.ResolutionToken, session.AuthoringRevision,
            session.SelectedRouteId, session.CapabilityFingerprint, session.ExecutionAdmitted, session.SelectedIc,
            session.SelectedIcCount, session.SelectedMapVariant, session.IcChoices, session.IcCountChoices, session.Slots,
            session.DraftState, session.DraftCapabilityFingerprint, session.DerivedPublications,
            fixture.Composition.CompilationFingerprint, fixture.Capability, metadataInspection: inspection);
    }

    internal static ActiveSessionSnapshot CreateSession(
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

    internal static AuthoringSlotState Slot(
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

    internal sealed record ProjectionFixture(
        CapabilityRouteIdentity Route,
        ResolvedCapability Capability,
        CompiledComposition Composition,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap ResolvedMap,
        FirmwareRegion DpRegion,
        FirmwareRegion TpRegion);

    internal static ResolvedCapability CreateLogicalGeneralMergeCapability(CompiledComposition composition)
    {
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
        return new ResolvedCapability(
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
    }

    internal static CompiledComposition CreateLogicalGeneralMergeComposition(
        CompositionPlan? customPlan = null,
        CompiledInputContract? customInputContract = null)
    {
        CompositionPlan plan = customPlan ?? new CompositionPlan(
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
            customInputContract ?? new CompiledInputContract(
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

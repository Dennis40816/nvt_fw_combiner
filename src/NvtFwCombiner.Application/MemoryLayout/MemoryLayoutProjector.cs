using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.MemoryLayout;

/// <summary>
/// Pure Application projection from one resolved capability and authoring
/// revision to immutable memory-layout coverage.
/// </summary>
public static partial class MemoryLayoutProjector
{
    /// <summary>
    /// Projects exact typed General admission intersections without recomputing
    /// occupancy or accepting presentation-owned range policy.
    /// </summary>
    public static IReadOnlyList<MemoryLayoutConflict> ProjectAdmissionConflicts(
        GeneralAuthoringAdmissionResult admission,
        long outputCapacity)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputCapacity);
        var outputRange = new ByteRange(0, outputCapacity);
        return Array.AsReadOnly(
        [
            .. admission.Issues
                .Where(static issue => issue.Intersection is not null)
                .Select(issue =>
                {
                    ByteRange range = issue.Intersection!.Value;
                    return outputRange.Contains(range)
                        ? new MemoryLayoutConflict(
                            issue.IssueId,
                            range,
                            issue.Message,
                            issue.MappingIds)
                        : throw new ArgumentException(
                            "A General admission conflict must remain inside the projected output.",
                            nameof(admission));
                }),
        ]);
    }

    /// <summary>
    /// Projects canonical physical geometry and, when supplied, the exact
    /// already-compiled composition overlay. This method performs no I/O,
    /// resolution, compilation, or byte execution.
    /// </summary>
    public static MemoryLayoutSnapshot Project(
        ResolvedCapability capability,
        ActiveSessionSnapshot authoring,
        CompiledComposition? compiledOverlay,
        IReadOnlyList<CtrlRamRegion>? ctrlRamRegions = null)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(authoring);
        ValidateIdentity(capability, authoring, compiledOverlay);

        CompiledComposition composition = capability.CompiledComposition;
        V2CompiledCompositionDetails details = composition.V2Details;
        CompositionPlan plan = composition.Plan;
        ImageInitialization initialization = plan.OutputInitialization;
        FirmwareImageMap? map;
        long capacity;
        ProjectionRegion[] primaryRegions;
        switch (details.Provenance.Context)
        {
            case MapBoundV2CompilationContext mapContext:
                FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap =
                    mapContext.ResolvedMap;
                map = resolvedMap.ImageMap;
                capacity = resolvedMap.CapacityBytes;
                if (initialization.Capacity != capacity || map.CapacityBytes != capacity)
                {
                    throw new ArgumentException(
                        "Compiled output and resolved physical-map capacities must agree.",
                        nameof(capability));
                }

                primaryRegions = SelectPrimaryRegions(map, ctrlRamRegions);
                break;
            case LogicalOutputV2CompilationContext:
                if (!StringComparer.Ordinal.Equals(details.ExperienceId, ExperienceIds.GeneralMerge) ||
                    initialization.Kind != ImageInitializationKind.Blank)
                {
                    throw new ArgumentException(
                        "Logical memory-layout geometry is reserved for blank-initialized General Merge output.",
                        nameof(capability));
                }

                map = null;
                capacity = initialization.Capacity;
                primaryRegions =
                [
                    new ProjectionRegion(
                        plan.OutputSpaceId,
                        new ByteRange(0, capacity),
                        MemoryContentRole.General,
                        CanonicalRegion: null,
                        ReplaceRegionGroup.Common,
                        CtrlRamRegionRole.Other),
                ];
                break;
            default:
                throw new ArgumentException(
                    "Memory-layout projection requires declared physical or logical output provenance.",
                    nameof(capability));
        }

        CompiledInputContract inputContract = details.InputContract;
        var slotsById =
            inputContract.Slots.ToDictionary(static slot => slot.SlotId, StringComparer.Ordinal);
        var statesById =
            authoring.Slots.ToDictionary(
                static slot => slot.DefinitionId,
                StringComparer.Ordinal);
        (
            Dictionary<string, string> slotsBySpace,
            Dictionary<string, CompiledInputSlotRequirement> requirementsByStateId) =
            ResolveAuthoringInputBindings(
                inputContract,
                slotsById,
                plan,
                authoring.DraftState,
                statesById);
        ValidateAuthoringSlots(requirementsByStateId, statesById);

        MemoryLayoutPendingItem[] pendingItems =
            ProjectPendingItems(requirementsByStateId, statesById, authoring);
        MemoryLayoutSegment[] before =
            CreateInitialCoverage(
                primaryRegions,
                map?.AddressSpaceId ?? plan.OutputSpaceId,
                initialization,
                slotsBySpace,
                statesById);
        MemoryLayoutSegment[] after = compiledOverlay is null
            ? before
            : ApplyOperations(
                primaryRegions,
                map?.AddressSpaceId ?? plan.OutputSpaceId,
                composition,
                slotsBySpace,
                statesById);

        return map is null
            ? new MemoryLayoutSnapshot(
                capability,
                authoring,
                plan.OutputSpaceId,
                capacity,
                before,
                after,
                pendingItems)
            : new MemoryLayoutSnapshot(
                capability,
                authoring,
                map,
                capacity,
                before,
                after,
                pendingItems);
    }

    private static void ValidateIdentity(
        ResolvedCapability capability,
        ActiveSessionSnapshot authoring,
        CompiledComposition? compiledOverlay)
    {
        if (!StringComparer.Ordinal.Equals(
                capability.Identity.RouteId,
                authoring.SelectedRouteId) ||
            !StringComparer.Ordinal.Equals(
                capability.Identity.WorkflowId,
                authoring.WorkflowId) ||
            !StringComparer.Ordinal.Equals(
                capability.CapabilityFingerprint,
                authoring.CapabilityFingerprint) ||
            capability.ResolutionToken != authoring.ResolutionToken)
        {
            throw new ArgumentException(
                "Authoring state must pin the exact resolved capability publication.",
                nameof(authoring));
        }

        if (compiledOverlay is not null &&
            !ReferenceEquals(compiledOverlay, capability.CompiledComposition))
        {
            throw new ArgumentException(
                "The optional overlay must be the exact compiled artifact owned by the capability.",
                nameof(compiledOverlay));
        }
    }

    private static ProjectionRegion[] SelectPrimaryRegions(
        FirmwareImageMap map,
        IReadOnlyList<CtrlRamRegion>? ctrlRamRegions)
    {
        FirmwareRegion[] roots =
        [
            .. map.Regions.Where(static region => region.ParentRegionId is null)
                .OrderBy(static region => region.Range.Start)
                .ThenBy(static region => region.RegionId, StringComparer.Ordinal),
        ];
        var fullRange = new ByteRange(0, map.CapacityBytes);
        if (!Tiles(roots, fullRange))
        {
            throw new ArgumentException(
                "Memory-layout projection requires canonical image roots that tile the complete output.",
                nameof(map));
        }

        List<FirmwareRegion> baseRegions = [];
        foreach (FirmwareRegion root in roots)
        {
            FirmwareRegion[] children =
            [
                .. map.Regions
                    .Where(region => StringComparer.Ordinal.Equals(
                        region.ParentRegionId,
                        root.RegionId))
                    .OrderBy(static region => region.Range.Start)
                    .ThenBy(static region => region.RegionId, StringComparer.Ordinal),
            ];
            baseRegions.AddRange(Tiles(children, root.Range) ? children : [root]);
        }

        var ctrlRamByRegionId = new Dictionary<string, CtrlRamRegion>(StringComparer.Ordinal);
        foreach (CtrlRamRegion projected in ctrlRamRegions ?? [])
        {
            FirmwareRegion[] matches =
            [
                .. map.Regions.Where(region =>
                    region.Kind == FirmwareRegionKind.CtrlRam &&
                    projected.Start == region.Range.Start &&
                    projected.Length == region.Range.Length),
            ];
            if (matches.Length != 1 ||
                !ctrlRamByRegionId.TryAdd(matches[0].RegionId, projected))
            {
                throw new ArgumentException(
                    "CtrlRAM display regions must resolve one-to-one to exact canonical map ranges.",
                    nameof(ctrlRamRegions));
            }
        }

        FirmwareRegion[] classifiedCtrlRamRegions =
        [
            .. map.Regions.Where(region => ctrlRamByRegionId.ContainsKey(region.RegionId)),
        ];

        SortedSet<long> boundaries = [0, map.CapacityBytes];
        foreach (FirmwareRegion region in baseRegions.Concat(classifiedCtrlRamRegions))
        {
            _ = boundaries.Add(region.Range.Start);
            _ = boundaries.Add(region.Range.EndExclusive);
        }
        long[] points = [.. boundaries];
        var primary = new List<ProjectionRegion>(points.Length - 1);
        for (int index = 0; index < points.Length - 1; index++)
        {
            var range = ByteRange.FromStartEndExclusive(points[index], points[index + 1]);
            FirmwareRegion canonical = classifiedCtrlRamRegions
                .Where(region => region.Range.Contains(range))
                .OrderBy(static region => region.Range.Length)
                .ThenBy(static region => region.RegionId, StringComparer.Ordinal)
                .FirstOrDefault() ?? baseRegions.Single(region => region.Range.Contains(range));
            primary.Add(new ProjectionRegion(
                canonical.RegionId,
                range,
                ClassifyContent(canonical),
                canonical,
                ctrlRamByRegionId.TryGetValue(canonical.RegionId, out CtrlRamRegion? ctrlRam)
                    ? ctrlRam.RegionGroup
                    : ctrlRamRegions is null
                        ? ReplaceRegionGroup.Common
                        : ReplaceRegionGroup.Base,
                ctrlRam?.Role ?? CtrlRamRegionRole.Other));
        }

        return [.. primary];
    }

    private static bool Tiles(IReadOnlyList<FirmwareRegion> regions, ByteRange range)
    {
        long cursor = range.Start;
        foreach (FirmwareRegion region in regions)
        {
            if (region.Range.Start != cursor || !range.Contains(region.Range))
            {
                return false;
            }

            cursor = region.Range.EndExclusive;
        }

        return cursor == range.EndExclusive;
    }

    private static void ValidateAuthoringSlots(
        Dictionary<string, CompiledInputSlotRequirement> requirements,
        Dictionary<string, AuthoringSlotState> states)
    {
        if (states.Count != requirements.Count ||
            states.Keys.Any(slotId => !requirements.ContainsKey(slotId)))
        {
            throw new ArgumentException(
                $"Authoring slot state must exactly reference the compiled input contract. " +
                $"Expected [{string.Join(", ", requirements.Keys.Order(StringComparer.Ordinal))}]; " +
                $"actual [{string.Join(", ", states.Keys.Order(StringComparer.Ordinal))}].",
                nameof(states));
        }
    }

    private static MemoryLayoutPendingItem[] ProjectPendingItems(
        IReadOnlyDictionary<string, CompiledInputSlotRequirement> requirements,
        Dictionary<string, AuthoringSlotState> states,
        ActiveSessionSnapshot authoring)
    {
        List<MemoryLayoutPendingItem> pending = [];
        foreach ((string stateId, CompiledInputSlotRequirement requirement) in requirements)
        {
            AuthoringSlotState state = states[stateId];
            if (IsAdmitted(state) ||
                (!requirement.Required && state.Lifecycle == AuthoringSlotLifecycle.Empty))
            {
                continue;
            }

            (
                MemoryLayoutReadiness readiness,
                MemoryLayoutPrerequisite prerequisite,
                MemoryLayoutNextAction nextAction,
                MemoryDiagnosticSeverity severity) = state.Lifecycle switch
                {
                    AuthoringSlotLifecycle.Empty => (
                        MemoryLayoutReadiness.PendingInput,
                        MemoryLayoutPrerequisite.SelectInput,
                        MemoryLayoutNextAction.SelectInput,
                        MemoryDiagnosticSeverity.Information),
                    AuthoringSlotLifecycle.Selected => (
                        MemoryLayoutReadiness.PendingInput,
                        MemoryLayoutPrerequisite.CompleteInspection,
                        MemoryLayoutNextAction.RunInspection,
                        MemoryDiagnosticSeverity.Information),
                    AuthoringSlotLifecycle.Checking => (
                        MemoryLayoutReadiness.PendingInput,
                        MemoryLayoutPrerequisite.CompleteInspection,
                        MemoryLayoutNextAction.WaitForInspection,
                        MemoryDiagnosticSeverity.Information),
                    AuthoringSlotLifecycle.Error => (
                        MemoryLayoutReadiness.Blocked,
                        MemoryLayoutPrerequisite.ResolveInputIssue,
                        MemoryLayoutNextAction.ReviewInputIssue,
                        MemoryDiagnosticSeverity.Error),
                    AuthoringSlotLifecycle.Verified or AuthoringSlotLifecycle.Warning =>
                        throw new InvalidOperationException(
                            "Admitted authoring lifecycle reached pending projection."),
                    _ => throw new InvalidOperationException(
                        "Unknown authoring lifecycle reached pending projection."),
                };
            pending.Add(
                new MemoryLayoutPendingItem(
                    stateId,
                    requirement,
                    readiness,
                    prerequisite,
                    nextAction,
                    state.FileStamp?.AcceptedLength,
                    severity,
                    state.BlockingIssue is null
                        ? null
                        : new MemoryLayoutBlockedIssueReference(
                            authoring.ResolutionToken,
                            authoring.AuthoringRevision,
                            new AuthoringSlotPublicationIdentity(
                                state.DefinitionId,
                                state.SelectedPath,
                                state.FileStamp),
                            state.BlockingIssue)));
        }

        return
        [
            .. pending.OrderBy(static item => item.SlotId, StringComparer.Ordinal),
        ];
    }

    private static MemoryLayoutSegment[] CreateInitialCoverage(
        ProjectionRegion[] primaryRegions,
        string addressSpaceId,
        ImageInitialization initialization,
        Dictionary<string, string> slotsBySpace,
        Dictionary<string, AuthoringSlotState> statesById)
    {
        string? referenceSlotId = GetAdmittedReferenceSlot(
            initialization,
            slotsBySpace,
            statesById);
        bool referenceAdmitted = referenceSlotId is not null;

        return
        [
            .. primaryRegions.Select(region =>
                CreateSegment(
                    region,
                    region.Range,
                    addressSpaceId,
                    InitialDisposition(region, initialization.Kind, referenceAdmitted),
                    referenceAdmitted ? initialization.ReferenceSpaceId : null,
                    referenceSlotId,
                    contributingOperations: [],
                    diagnosticSeverity: MemoryDiagnosticSeverity.None,
                    selection: MemorySelectionState.NotSelected,
                    processorEffect: MemoryProcessorEffect.None)),
        ];
    }

    private static MemoryLayoutSegment[] ApplyOperations(
        ProjectionRegion[] primaryRegions,
        string addressSpaceId,
        CompiledComposition composition,
        Dictionary<string, string> slotsBySpace,
        Dictionary<string, AuthoringSlotState> statesById)
    {
        CompositionPlan plan = composition.Plan;
        CompositionOperation[] planned =
        [
            .. plan.OrderedOperations.Where(operation =>
                StringComparer.Ordinal.Equals(operation.TargetSpaceId, plan.OutputSpaceId)),
        ];
        string? referenceSlotId = GetAdmittedReferenceSlot(
            plan.OutputInitialization,
            slotsBySpace,
            statesById);
        SortedSet<long> boundaries = [0, plan.OutputInitialization.Capacity];
        foreach (ProjectionRegion region in primaryRegions)
        {
            _ = boundaries.Add(region.Range.Start);
            _ = boundaries.Add(region.Range.EndExclusive);
        }

        foreach (CompositionOperation operation in planned)
        {
            if (operation.ExternalProcessorInvocation is { } processor)
            {
                foreach (ByteRange range in processor.AllowedWriteRanges)
                {
                    _ = boundaries.Add(range.Start);
                    _ = boundaries.Add(range.EndExclusive);
                }
            }
            else
            {
                _ = boundaries.Add(operation.TargetRange.Start);
                _ = boundaries.Add(operation.TargetRange.EndExclusive);
            }
        }

        long[] points = [.. boundaries];
        List<MemoryLayoutSegment> segments = [];
        for (int index = 0; index < points.Length - 1; index++)
        {
            var range = ByteRange.FromStartEndExclusive(points[index], points[index + 1]);
            ProjectionRegion canonicalRegion = primaryRegions.Single(
                region => region.Range.Contains(range));
            CompositionOperation[] contributors =
            [
                .. planned.Where(operation => OperationWritesRange(operation, range)),
            ];
            if (contributors.Length == 0)
            {
                segments.Add(
                    CreateSegment(
                        canonicalRegion,
                        range,
                        addressSpaceId,
                        InitialDisposition(
                            canonicalRegion,
                            plan.OutputInitialization.Kind,
                            referenceSlotId is not null),
                        referenceSlotId is null
                            ? null
                            : plan.OutputInitialization.ReferenceSpaceId,
                        referenceSlotId,
                        contributingOperations: [],
                        diagnosticSeverity: MemoryDiagnosticSeverity.None,
                        selection: MemorySelectionState.NotSelected,
                        processorEffect: MemoryProcessorEffect.None));
                continue;
            }

            CompositionOperation dominant = contributors.LastOrDefault(
                static operation => operation.SourceSpaceId is not null) ?? contributors[^1];
            string? sourceSlotId = dominant.SourceSpaceId is not null &&
                slotsBySpace.TryGetValue(dominant.SourceSpaceId, out string? boundSlot)
                    ? boundSlot
                    : null;
            bool sourceAdmitted = sourceSlotId is not null &&
                IsAdmitted(statesById[sourceSlotId]);
            MemoryDiagnosticSeverity severity = sourceSlotId is not null &&
                statesById[sourceSlotId].Lifecycle == AuthoringSlotLifecycle.Warning
                    ? MemoryDiagnosticSeverity.Warning
                    : MemoryDiagnosticSeverity.None;
            segments.Add(
                CreateSegment(
                    canonicalRegion,
                    range,
                    addressSpaceId,
                    composition.V2Details.CompositionKind == CompositionKind.Merge
                        ? MemoryWorkflowDisposition.WillWrite
                        : MemoryWorkflowDisposition.WillReplace,
                    dominant.SourceSpaceId,
                    sourceSlotId,
                    contributors,
                    severity,
                    sourceAdmitted
                        ? MemorySelectionState.Selected
                        : MemorySelectionState.NotSelected,
                    contributors.Any(static operation =>
                        operation.ExternalProcessorInvocation is not null)
                            ? MemoryProcessorEffect.DeclaredWrite
                            : MemoryProcessorEffect.None));
        }

        return [.. segments];
    }

    private static bool OperationWritesRange(
        CompositionOperation operation,
        ByteRange range)
    {
        return operation.ExternalProcessorInvocation is { } processor
            ? processor.AllowedWriteRanges.Any(candidate => candidate.Contains(range))
            : operation.TargetRange.Contains(range);
    }

    private static string? GetAdmittedReferenceSlot(
        ImageInitialization initialization,
        Dictionary<string, string> slotsBySpace,
        Dictionary<string, AuthoringSlotState> statesById)
    {
        return initialization.Kind == ImageInitializationKind.Reference &&
            initialization.ReferenceSpaceId is not null &&
            slotsBySpace.TryGetValue(initialization.ReferenceSpaceId, out string? slotId) &&
            IsAdmitted(statesById[slotId])
                ? slotId
                : null;
    }

    private static bool IsAdmitted(AuthoringSlotState state)
    {
        return state.Lifecycle is
            AuthoringSlotLifecycle.Verified or AuthoringSlotLifecycle.Warning;
    }

    private static MemoryWorkflowDisposition InitialDisposition(
        ProjectionRegion region,
        ImageInitializationKind initializationKind,
        bool referenceAdmitted)
    {
        return initializationKind == ImageInitializationKind.Reference
            ? referenceAdmitted
                ? MemoryWorkflowDisposition.Kept
                : MemoryWorkflowDisposition.Resolved
            : region.ContentRole is
                MemoryContentRole.CustomerInformation or
                MemoryContentRole.Reserved or
                MemoryContentRole.Unmapped
                ? MemoryWorkflowDisposition.Resolved
                : MemoryWorkflowDisposition.Blank;
    }

    private static MemoryLayoutSegment CreateSegment(
        ProjectionRegion canonicalRegion,
        ByteRange range,
        string addressSpaceId,
        MemoryWorkflowDisposition disposition,
        string? sourceSpaceId,
        string? sourceSlotId,
        IReadOnlyList<CompositionOperation> contributingOperations,
        MemoryDiagnosticSeverity diagnosticSeverity,
        MemorySelectionState selection,
        MemoryProcessorEffect processorEffect)
    {
        string segmentId = FormattableString.Invariant(
            $"{canonicalRegion.RegionId}:{range.Start:x}-{range.EndExclusive:x}");
        return canonicalRegion.CanonicalRegion is { } physicalRegion
            ? MemoryLayoutSegment.Create(
                segmentId,
                addressSpaceId,
                range,
                physicalRegion,
                canonicalRegion.ContentRole,
                disposition,
                MemoryEndpointIdentity.NotApplicable,
                MemoryBankIdentity.NotApplicable,
                processorEffect,
                diagnosticSeverity,
                MemoryObservedChange.NotObserved,
                selection,
                MemoryFocusState.NotFocused,
                sourceSpaceId,
                sourceSlotId,
                contributingOperations,
                [],
                canonicalRegion.RegionGroup,
                canonicalRegion.CtrlRamRegionRole)
            : MemoryLayoutSegment.CreateLogical(
                segmentId,
                addressSpaceId,
                range,
                canonicalRegion.RegionId,
                canonicalRegion.ContentRole,
                disposition,
                MemoryEndpointIdentity.NotApplicable,
                MemoryBankIdentity.NotApplicable,
                processorEffect,
                diagnosticSeverity,
                MemoryObservedChange.NotObserved,
                selection,
                MemoryFocusState.NotFocused,
                sourceSpaceId,
                sourceSlotId,
                contributingOperations,
                [],
                canonicalRegion.RegionGroup,
                canonicalRegion.CtrlRamRegionRole);
    }

    private static MemoryContentRole ClassifyContent(FirmwareRegion region)
    {
        return region.Kind == FirmwareRegionKind.CtrlRam
            ? MemoryContentRole.CtrlRam
            : region.Owner switch
            {
                FirmwareRegionOwner.Dp => MemoryContentRole.Dp,
                FirmwareRegionOwner.Tp => MemoryContentRole.Tp,
                FirmwareRegionOwner.Ldc => MemoryContentRole.Ldc,
                FirmwareRegionOwner.Customer => MemoryContentRole.CustomerInformation,
                FirmwareRegionOwner.Reserved => MemoryContentRole.Reserved,
                _ when region.Kind == FirmwareRegionKind.Reserved =>
                    MemoryContentRole.Reserved,
                _ when region.Kind == FirmwareRegionKind.Unmapped =>
                    MemoryContentRole.Unmapped,
                FirmwareRegionOwner.System or
                FirmwareRegionOwner.Register or
                FirmwareRegionOwner.Shared or
                FirmwareRegionOwner.Unknown => MemoryContentRole.General,
                _ => throw new InvalidOperationException("Unknown canonical firmware-region owner."),
            };
    }

    private sealed record ProjectionRegion(
        string RegionId,
        ByteRange Range,
        MemoryContentRole ContentRole,
        FirmwareRegion? CanonicalRegion,
        ReplaceRegionGroup RegionGroup,
        CtrlRamRegionRole CtrlRamRegionRole);
}

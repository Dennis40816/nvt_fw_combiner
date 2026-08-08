using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.MemoryLayout;

/// <summary>
/// Pure Application projection from one resolved capability and authoring
/// revision to immutable memory-layout coverage.
/// </summary>
public static class MemoryLayoutProjector
{
    /// <summary>
    /// Projects canonical physical geometry and, when supplied, the exact
    /// already-compiled composition overlay. This method performs no I/O,
    /// resolution, compilation, or byte execution.
    /// </summary>
    public static MemoryLayoutSnapshot Project(
        ResolvedCapability capability,
        ActiveSessionSnapshot authoring,
        CompiledComposition? compiledOverlay)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(authoring);
        ValidateIdentity(capability, authoring, compiledOverlay);

        CompiledComposition composition = capability.CompiledComposition;
        V2CompiledCompositionDetails details = composition.V2Details;
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap =
            details.Provenance.Context is MapBoundV2CompilationContext mapContext
                ? mapContext.ResolvedMap
                : throw new ArgumentException(
                    "Memory-layout projection requires canonical physical-map provenance.",
                    nameof(capability));
        FirmwareImageMap map = resolvedMap.ImageMap;
        CompositionPlan plan = composition.Plan;
        ImageInitialization initialization = plan.OutputInitialization;
        if (initialization.Capacity != resolvedMap.CapacityBytes ||
            map.CapacityBytes != resolvedMap.CapacityBytes)
        {
            throw new ArgumentException(
                "Compiled output and resolved physical-map capacities must agree.",
                nameof(capability));
        }

        FirmwareRegion[] primaryRegions = SelectPrimaryRegions(map);
        CompiledInputContract inputContract = details.InputContract;
        var slotsById =
            inputContract.Slots.ToDictionary(static slot => slot.SlotId, StringComparer.Ordinal);
        var slotsBySpace =
            inputContract.SpaceBindings.ToDictionary(
                static binding => binding.AddressSpaceId,
                static binding => binding.SlotId,
                StringComparer.Ordinal);
        var statesById =
            authoring.Slots.ToDictionary(
                static slot => slot.DefinitionId,
                StringComparer.Ordinal);
        ValidateAuthoringSlots(slotsById, statesById);

        MemoryLayoutPendingItem[] pendingItems =
            ProjectPendingItems(inputContract.Slots, statesById, authoring);
        MemoryLayoutSegment[] before =
            CreateInitialCoverage(
                primaryRegions,
                map.AddressSpaceId,
                initialization,
                slotsBySpace,
                statesById);
        MemoryLayoutSegment[] after = compiledOverlay is null
            ? before
            : ApplyOperations(
                primaryRegions,
                map.AddressSpaceId,
                composition,
                slotsBySpace,
                statesById);

        return new MemoryLayoutSnapshot(
            capability,
            authoring,
            map,
            resolvedMap.CapacityBytes,
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

    private static FirmwareRegion[] SelectPrimaryRegions(FirmwareImageMap map)
    {
        FirmwareRegion[] roots =
        [
            .. map.Regions.Where(static region => region.ParentRegionId is null),
        ];
        if (roots.Length != 1 ||
            roots[0].Range != new ByteRange(0, map.CapacityBytes))
        {
            throw new ArgumentException(
                "Memory-layout projection requires one complete canonical image root.",
                nameof(map));
        }

        FirmwareRegion[] children =
        [
            .. map.Regions
                .Where(region => StringComparer.Ordinal.Equals(
                    region.ParentRegionId,
                    roots[0].RegionId))
                .OrderBy(static region => region.Range.Start)
                .ThenBy(static region => region.RegionId, StringComparer.Ordinal),
        ];
        return children.Length == 0 ? roots : children;
    }

    private static void ValidateAuthoringSlots(
        Dictionary<string, CompiledInputSlotRequirement> requirements,
        Dictionary<string, AuthoringSlotState> states)
    {
        if (states.Count != requirements.Count ||
            states.Keys.Any(slotId => !requirements.ContainsKey(slotId)))
        {
            throw new ArgumentException(
                "Authoring slot state must exactly reference the compiled input contract.",
                nameof(states));
        }
    }

    private static MemoryLayoutPendingItem[] ProjectPendingItems(
        IReadOnlyList<CompiledInputSlotRequirement> requirements,
        Dictionary<string, AuthoringSlotState> states,
        ActiveSessionSnapshot authoring)
    {
        List<MemoryLayoutPendingItem> pending = [];
        foreach (CompiledInputSlotRequirement requirement in requirements)
        {
            AuthoringSlotState state = states[requirement.SlotId];
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
        FirmwareRegion[] primaryRegions,
        string addressSpaceId,
        ImageInitialization initialization,
        Dictionary<string, string> slotsBySpace,
        Dictionary<string, AuthoringSlotState> statesById)
    {
        bool referenceAdmitted =
            initialization.Kind == ImageInitializationKind.Reference &&
            initialization.ReferenceSpaceId is not null &&
            slotsBySpace.TryGetValue(initialization.ReferenceSpaceId, out string? slotId) &&
            IsAdmitted(statesById[slotId]);

        return
        [
            .. primaryRegions.Select(region =>
                CreateSegment(
                    region,
                    region.Range,
                    addressSpaceId,
                    InitialDisposition(region, initialization.Kind, referenceAdmitted),
                    sourceSpaceId: null,
                    sourceSlotId: null,
                    contributingOperations: [],
                    diagnosticSeverity: MemoryDiagnosticSeverity.None,
                    selection: MemorySelectionState.NotSelected,
                    processorEffect: MemoryProcessorEffect.None)),
        ];
    }

    private static MemoryLayoutSegment[] ApplyOperations(
        FirmwareRegion[] primaryRegions,
        string addressSpaceId,
        CompiledComposition composition,
        Dictionary<string, string> slotsBySpace,
        Dictionary<string, AuthoringSlotState> statesById)
    {
        CompositionPlan plan = composition.Plan;
        CompositionOperation[] applied =
        [
            .. plan.OrderedOperations.Where(operation =>
                StringComparer.Ordinal.Equals(operation.TargetSpaceId, plan.OutputSpaceId) &&
                IsOperationAdmitted(operation, slotsBySpace, statesById)),
        ];
        SortedSet<long> boundaries = [0, plan.OutputInitialization.Capacity];
        foreach (FirmwareRegion region in primaryRegions)
        {
            _ = boundaries.Add(region.Range.Start);
            _ = boundaries.Add(region.Range.EndExclusive);
        }

        foreach (CompositionOperation operation in applied)
        {
            _ = boundaries.Add(operation.TargetRange.Start);
            _ = boundaries.Add(operation.TargetRange.EndExclusive);
        }

        long[] points = [.. boundaries];
        List<MemoryLayoutSegment> segments = [];
        for (int index = 0; index < points.Length - 1; index++)
        {
            var range = ByteRange.FromStartEndExclusive(points[index], points[index + 1]);
            FirmwareRegion canonicalRegion = primaryRegions.Single(
                region => region.Range.Contains(range));
            CompositionOperation[] contributors =
            [
                .. applied.Where(operation => operation.TargetRange.Contains(range)),
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
                            IsReferenceAdmitted(
                                plan.OutputInitialization,
                                slotsBySpace,
                                statesById)),
                        sourceSpaceId: null,
                        sourceSlotId: null,
                        contributingOperations: [],
                        diagnosticSeverity: MemoryDiagnosticSeverity.None,
                        selection: MemorySelectionState.NotSelected,
                        processorEffect: MemoryProcessorEffect.None));
                continue;
            }

            CompositionOperation dominant = contributors[^1];
            string? sourceSlotId = dominant.SourceSpaceId is not null &&
                slotsBySpace.TryGetValue(dominant.SourceSpaceId, out string? boundSlot)
                    ? boundSlot
                    : null;
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
                    sourceSlotId is null
                        ? MemorySelectionState.NotSelected
                        : MemorySelectionState.Selected,
                    contributors.Any(static operation =>
                        operation.ExternalProcessorInvocation is not null)
                            ? MemoryProcessorEffect.DeclaredWrite
                            : MemoryProcessorEffect.None));
        }

        return [.. segments];
    }

    private static bool IsReferenceAdmitted(
        ImageInitialization initialization,
        Dictionary<string, string> slotsBySpace,
        Dictionary<string, AuthoringSlotState> statesById)
    {
        return initialization.Kind == ImageInitializationKind.Reference &&
            initialization.ReferenceSpaceId is not null &&
            slotsBySpace.TryGetValue(initialization.ReferenceSpaceId, out string? slotId) &&
            IsAdmitted(statesById[slotId]);
    }

    private static bool IsOperationAdmitted(
        CompositionOperation operation,
        Dictionary<string, string> slotsBySpace,
        Dictionary<string, AuthoringSlotState> statesById)
    {
        return operation.SourceSpaceId is null ||
            !slotsBySpace.TryGetValue(operation.SourceSpaceId, out string? slotId) ||
            IsAdmitted(statesById[slotId]);
    }

    private static bool IsAdmitted(AuthoringSlotState state)
    {
        return state.Lifecycle is
            AuthoringSlotLifecycle.Verified or AuthoringSlotLifecycle.Warning;
    }

    private static MemoryWorkflowDisposition InitialDisposition(
        FirmwareRegion region,
        ImageInitializationKind initializationKind,
        bool referenceAdmitted)
    {
        return initializationKind == ImageInitializationKind.Reference
            ? referenceAdmitted
                ? MemoryWorkflowDisposition.Kept
                : MemoryWorkflowDisposition.Resolved
            : ClassifyContent(region) == MemoryContentRole.Reserved
                ? MemoryWorkflowDisposition.Resolved
                : MemoryWorkflowDisposition.Blank;
    }

    private static MemoryLayoutSegment CreateSegment(
        FirmwareRegion canonicalRegion,
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
        return MemoryLayoutSegment.Create(
            segmentId,
            addressSpaceId,
            range,
            canonicalRegion,
            ClassifyContent(canonicalRegion),
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
            contributingOperations.Select(static operation => operation.OperationId),
            []);
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
                FirmwareRegionOwner.Reserved => MemoryContentRole.Reserved,
                _ when region.Kind is FirmwareRegionKind.Reserved or FirmwareRegionKind.Unmapped =>
                    MemoryContentRole.Reserved,
                FirmwareRegionOwner.System or
                FirmwareRegionOwner.Register or
                FirmwareRegionOwner.Customer or
                FirmwareRegionOwner.Shared or
                FirmwareRegionOwner.Unknown => MemoryContentRole.General,
                _ => throw new InvalidOperationException("Unknown canonical firmware-region owner."),
            };
    }
}

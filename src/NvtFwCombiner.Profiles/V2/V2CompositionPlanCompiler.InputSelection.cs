using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    private const string InputSelectionInvalid = "profile.v2.plan.input-selection-invalid";
    private const string InputSelectionNotApplicable = "profile.v2.plan.input-selection-not-applicable";

    private sealed record ResolvedInputSelection(
        IReadOnlySet<string> ActiveSlotIds,
        IReadOnlySet<string> ActiveOperationIds,
        IReadOnlySet<string> ActiveViewIds,
        IReadOnlySet<string> ActiveRegionIds,
        IReadOnlyList<CompiledInputSelectionGroup> Groups);

    private static ResolvedInputSelection ResolveInputSelection(
        CompositionProfileDefinition profile,
        FirmwareFamilyResolutionDefinition.ResolvedFirmwareImageMap resolvedMap,
        IReadOnlyCollection<string>? requestedSlotIds,
        List<CompositionIssue> issues)
    {
        var slots = profile.InputSlots.ToDictionary(static slot => slot.SlotId, StringComparer.Ordinal);
        var requested = requestedSlotIds?.ToHashSet(StringComparer.Ordinal);
        if (requested is not null)
        {
            foreach (string slotId in requested.Where(slotId => !slots.ContainsKey(slotId)))
            {
                issues.Add(new CompositionIssue(
                    InputSelectionInvalid,
                    $"Input selection names unknown slot '{slotId}'.",
                    slotId));
            }
        }

        var activeSlotIds = profile.InputSlots
            .Where(static slot => slot.Required)
            .Select(static slot => slot.SlotId)
            .ToHashSet(StringComparer.Ordinal);
        var regions = resolvedMap.ImageMap.Regions
            .Select(static region => region.RegionId)
            .ToHashSet(StringComparer.Ordinal);
        if (profile.InputSelectionGroups.Count == 0)
        {
            return new ResolvedInputSelection(
                profile.InputSlots.Select(static slot => slot.SlotId).ToHashSet(StringComparer.Ordinal),
                profile.Operations.Select(static operation => operation.OperationId).ToHashSet(StringComparer.Ordinal),
                profile.Views.Select(static view => view.ViewId).ToHashSet(StringComparer.Ordinal),
                regions,
                []);
        }

        var compiledGroups = new List<CompiledInputSelectionGroup>();
        foreach (InputSelectionGroupDefinition group in profile.InputSelectionGroups)
        {
            string[] applicable =
            [
                .. group.MemberSlotIds.Where(slotId => IsSlotApplicable(profile, slotId, regions)),
            ];
            string[] selected = requested is null
                ? [.. applicable.Take(group.MinimumSelected)]
                : [.. applicable.Where(requested.Contains)];
            var notApplicableReasons = group.MemberSlotIds
                .Except(applicable, StringComparer.Ordinal)
                .ToDictionary(
                    static slotId => slotId,
                    slotId => slots[slotId].NotApplicableReason ??
                        $"Input slot '{slotId}' is not available for the resolved map.",
                    StringComparer.Ordinal);
            if (requested is not null)
            {
                foreach (string slotId in group.MemberSlotIds.Where(requested.Contains).Except(applicable, StringComparer.Ordinal))
                {
                    issues.Add(new CompositionIssue(
                        InputSelectionNotApplicable,
                        notApplicableReasons[slotId],
                        slotId));
                }
            }

            int maximumSelected = Math.Min(group.MaximumSelected, applicable.Length);
            if (group.MinimumSelected > maximumSelected ||
                selected.Length < group.MinimumSelected ||
                selected.Length > maximumSelected)
            {
                issues.Add(new CompositionIssue(
                    InputSelectionInvalid,
                    $"Input selection group '{group.GroupId}' requires {group.MinimumSelected} to {maximumSelected} applicable selections; found {selected.Length}.",
                    group.GroupId));
                continue;
            }

            activeSlotIds.UnionWith(selected);
            compiledGroups.Add(new CompiledInputSelectionGroup(
                group,
                applicable,
                selected,
                maximumSelected,
                notApplicableReasons));
        }

        if (issues.Count != 0)
        {
            return new ResolvedInputSelection(
                activeSlotIds,
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                compiledGroups);
        }

        var inactiveInputSpaces = profile.Spaces
            .OfType<InputArtifactProfileSpace>()
            .Where(space => !activeSlotIds.Contains(space.SlotId))
            .Select(static space => space.SpaceId)
            .ToHashSet(StringComparer.Ordinal);
        var views = profile.Views.ToDictionary(static view => view.ViewId, StringComparer.Ordinal);
        var activeOperationIds = profile.Operations
            .Where(operation => !ReferencesInactiveInput(operation, views, inactiveInputSpaces))
            .Select(static operation => operation.OperationId)
            .ToHashSet(StringComparer.Ordinal);
        var activeViewIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (CompositionOperationDefinition operation in profile.Operations.Where(operation =>
                     activeOperationIds.Contains(operation.OperationId)))
        {
            AddReferencedViews(operation, activeViewIds);
        }

        foreach (SourceViewNonUniformValidationDefinition validation in profile.Validations
                     .OfType<SourceViewNonUniformValidationDefinition>())
        {
            CompositionProfileView view = views[validation.ViewId];
            if (!inactiveInputSpaces.Contains(view.SpaceId) &&
                ViewRegionExists(view, regions))
            {
                _ = activeViewIds.Add(validation.ViewId);
            }
        }

        var activeRegionIds = activeViewIds
            .Select(viewId => TryGetViewRegionId(views[viewId]))
            .Where(static regionId => regionId is not null)
            .Select(static regionId => regionId!)
            .ToHashSet(StringComparer.Ordinal);
        return new ResolvedInputSelection(
            activeSlotIds,
            activeOperationIds,
            activeViewIds,
            activeRegionIds,
            compiledGroups);
    }

    private static bool IsSlotApplicable(
        CompositionProfileDefinition profile,
        string slotId,
        IReadOnlySet<string> availableRegionIds)
    {
        string[] spaceIds =
        [
            .. profile.Spaces
                .OfType<InputArtifactProfileSpace>()
                .Where(space => StringComparer.Ordinal.Equals(space.SlotId, slotId))
                .Select(static space => space.SpaceId),
        ];
        return profile.Views
            .Where(view => spaceIds.Contains(view.SpaceId, StringComparer.Ordinal))
            .All(view => ViewRegionExists(view, availableRegionIds));
    }

    private static bool ViewRegionExists(
        CompositionProfileView view,
        IReadOnlySet<string> availableRegionIds)
    {
        string? regionId = TryGetViewRegionId(view);
        return regionId is null || availableRegionIds.Contains(regionId);
    }

    private static string? TryGetViewRegionId(CompositionProfileView view)
    {
        return view.Selector switch
        {
            MapRegionViewSelector region => region.RegionId,
            MapRegionSliceViewSelector slice => slice.RegionId,
            _ => null,
        };
    }

    private static bool ReferencesInactiveInput(
        CompositionOperationDefinition operation,
        Dictionary<string, CompositionProfileView> views,
        HashSet<string> inactiveInputSpaces)
    {
        return operation.Kind switch
        {
            CompositionOperationKind.CopyRange or
            CompositionOperationKind.ReplaceRange or
            CompositionOperationKind.TransformScalar =>
                inactiveInputSpaces.Contains(views[operation.SourceViewId].SpaceId) ||
                inactiveInputSpaces.Contains(views[operation.TargetViewId].SpaceId),
            CompositionOperationKind.FillRange or
            CompositionOperationKind.PatchScalar => inactiveInputSpaces.Contains(views[operation.TargetViewId].SpaceId),
            CompositionOperationKind.RunExternalProcessor => false,
            _ => throw new InvalidOperationException("Unknown canonical operation kind."),
        };
    }

    private static void AddReferencedViews(
        CompositionOperationDefinition operation,
        HashSet<string> viewIds)
    {
        switch (operation.Kind)
        {
            case CompositionOperationKind.CopyRange:
            case CompositionOperationKind.ReplaceRange:
            case CompositionOperationKind.TransformScalar:
                _ = viewIds.Add(operation.SourceViewId);
                _ = viewIds.Add(operation.TargetViewId);
                break;
            case CompositionOperationKind.FillRange:
            case CompositionOperationKind.PatchScalar:
                _ = viewIds.Add(operation.TargetViewId);
                break;
            case CompositionOperationKind.RunExternalProcessor:
                break;
            default:
                throw new InvalidOperationException("Unknown canonical operation kind.");
        }
    }

    private static CompiledValidationRequirement[] LowerInputValidations(
        CompositionProfileDefinition profile,
        IReadOnlyDictionary<string, ResolvedView> views)
    {
        return
        [
            .. profile.Validations
                .OfType<SourceViewNonUniformValidationDefinition>()
                .Where(validation => views.ContainsKey(validation.ViewId))
                .Select(validation =>
                {
                    ResolvedView view = views[validation.ViewId];
                    return CompiledValidationRequirements.RejectUniformInputRanges(
                        validation.RuleId,
                        CompiledValidationSeverity.Warning,
                        validation.IssueCode,
                        view.SpaceId,
                        [view.Range]);
                }),
        ];
    }
}

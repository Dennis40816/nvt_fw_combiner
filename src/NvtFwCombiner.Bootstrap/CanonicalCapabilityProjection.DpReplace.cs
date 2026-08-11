using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class CanonicalCapabilityProjection
{
    internal static bool TryResolveBuiltInV2DpReplaceContracts(
        string icId,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IReadOnlyList<CompiledComposition>? compositions)
    {
        compositions = null;
        if (!BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.TryGetValue(
                IcIdentifier.Normalize(icId),
                out BuiltInV2Registration? registration))
        {
            return false;
        }

        IReadOnlyList<long> capacities = registration.GetMapCapacities(
            out IReadOnlyList<CompositionIssue> capacityIssues);
        if (capacityIssues.Count != 0 || capacities.Count == 0)
        {
            return false;
        }

        var resolved = new List<CompiledComposition>();
        foreach (long capacity in capacities.Order())
        {
            registration.TryCompile(
                capacity,
                out CompiledComposition? defaultComposition,
                out IReadOnlyList<CompositionIssue> issues);
            if (defaultComposition is null || issues.Count != 0)
            {
                return false;
            }

            resolved.Add(defaultComposition);
            IReadOnlyList<CompiledInputSelectionGroup> groups =
                defaultComposition.V2Details.InputContract.SelectionGroups;
            foreach (CompiledInputSelectionGroup group in groups)
            {
                foreach (string memberSlotId in group.ApplicableMemberSlotIds
                             .Except(group.SelectedSlotIds, StringComparer.Ordinal))
                {
                    string[] selectedSlotIds = CreateDpReplaceProjectionSelection(
                        groups,
                        group,
                        memberSlotId);
                    registration.TryCompile(
                        capacity,
                        selectedSlotIds,
                        out CompiledComposition? memberComposition,
                        out issues);
                    if (memberComposition is null || issues.Count != 0)
                    {
                        return false;
                    }

                    resolved.Add(memberComposition);
                }
            }
        }

        compositions = resolved.AsReadOnly();
        return true;
    }

    private static string[] CreateDpReplaceProjectionSelection(
        IReadOnlyList<CompiledInputSelectionGroup> groups,
        CompiledInputSelectionGroup targetGroup,
        string memberSlotId)
    {
        var selected = groups
            .SelectMany(group => group.SelectedSlotIds)
            .ToHashSet(StringComparer.Ordinal);
        if (targetGroup.SelectedSlotIds.Count >= targetGroup.MaximumSelected)
        {
            _ = selected.Remove(targetGroup.SelectedSlotIds[^1]);
        }

        _ = selected.Add(memberSlotId);
        return [.. selected.Order(StringComparer.Ordinal)];
    }

    /// <summary>Returns true when the registered DP Replace profile declares an input-selection group.</summary>
    public static bool HasBuiltInV2DpReplaceSelectionGroup(string icId)
    {
        return TryResolveBuiltInV2DpReplaceContracts(
                icId,
                out IReadOnlyList<CompiledComposition>? compositions) &&
            compositions.Any(static composition =>
                composition.V2Details.InputContract.SelectionGroups.Count != 0);
    }

    /// <summary>
    /// Resolves the one Application-owned selection result used by DP Replace
    /// headless clients without recreating group cardinality in an adapter.
    /// </summary>
    internal static bool TryResolveBuiltInV2DpReplaceInputSelection(
        string icId,
        long? baseCapacity,
        IReadOnlyCollection<string> selectedInputSlotIds,
        AuthoringRevision authoringRevision,
        out InputSelectionReadinessSnapshot? readiness,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(selectedInputSlotIds);
        readiness = null;
        if (!BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.TryGetValue(
                IcIdentifier.Normalize(icId),
                out BuiltInV2Registration? registration))
        {
            issues = [];
            return false;
        }

        long? discoveryCapacity = baseCapacity;
        if (discoveryCapacity is null)
        {
            IReadOnlyList<long> capacities = registration.GetMapCapacities(out issues);
            if (issues.Count != 0 || capacities.Count == 0)
            {
                return false;
            }

            discoveryCapacity = capacities[0];
        }

        registration.TryCompile(
            discoveryCapacity,
            out CompiledComposition? composition,
            out issues);
        if (composition is null || issues.Count != 0)
        {
            return false;
        }

        V2CompiledCompositionDetails details = composition.V2Details;

        readiness = InputSelectionReadinessResolver.Resolve(
            authoringRevision,
            details.InputContract.SelectionGroups,
            selectedInputSlotIds,
            baseCapacity is null ? CompositionAddressSpaceIds.ReferenceBase : null);
        return true;
    }

    /// <summary>Resolves the V2 DP Replace facts needed by the workbench display without consulting legacy maps.</summary>
    internal static bool TryResolveBuiltInV2DpReplaceDisplay(
        string icId,
        long? baseCapacity,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BuiltInV2DpReplaceDisplay? display)
    {
        if (!BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.TryGetValue(
                IcIdentifier.Normalize(icId),
                out BuiltInV2Registration? registration))
        {
            display = null;
            return false;
        }

        IReadOnlyList<long> capacities = registration.GetMapCapacities(out IReadOnlyList<CompositionIssue> capacityIssues);
        if (capacityIssues.Count != 0 || baseCapacity is null)
        {
            display = new BuiltInV2DpReplaceDisplay(baseCapacity, capacities, Composition: null, capacityIssues);
            return true;
        }

        registration.TryCompile(baseCapacity.Value, out CompiledComposition? composition, out IReadOnlyList<CompositionIssue> issues);
        display = new BuiltInV2DpReplaceDisplay(baseCapacity, capacities, composition, issues);
        return true;
    }

    /// <summary>Resolves a DP Replace CLI selector from the supported V2 profile registrations.</summary>
    internal static bool TryResolveBuiltInV2DpReplaceSelector(
        string selector,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? icId)
    {
        string normalized = selector.Trim();
        foreach (BuiltInV2Registration registration in BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.Values)
        {
            if (registration.MatchesSelector(normalized))
            {
                icId = registration.IcId;
                return true;
            }
        }

        icId = null;
        return false;
    }

    internal static string FormatBuiltInV2DpReplaceIcIds()
    {
        return string.Join(
            "/",
            BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>Returns true when the IC has one registered built-in V2 DP Replace route.</summary>
    public static bool HasBuiltInV2DpReplace(string icId)
    {
        return !string.IsNullOrWhiteSpace(icId) &&
            BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.ContainsKey(
                IcIdentifier.Normalize(icId));
    }

    /// <summary>Returns true only when Standard Merge uses the multi-capacity DP Perspective family shape.</summary>
    public static bool IsDpPerspectiveIc(string icId)
    {
        return !string.IsNullOrWhiteSpace(icId) &&
            BuiltInV2RegistrationRegistry.StandardMergeByIc.TryGetValue(
                IcIdentifier.Normalize(icId),
                out BuiltInV2Registration? registration) &&
            registration.TryGetContainerPolicy(out _);
    }
}

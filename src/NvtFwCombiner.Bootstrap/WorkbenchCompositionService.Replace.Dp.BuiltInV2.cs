using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Compiles one registered DP Replace profile from its hash-anchored V2 bundle.</summary>
    internal static bool TryCompileBuiltInV2DpReplace(
        string icId,
        long baseCapacity,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryCompilePublishedDynamicCapability(
                icId,
                IcWorkflowIds.DpReplace,
                "1-ic",
                baseCapacity,
                selectedInputSlotIds: null,
                out composition,
                out _,
                out issues) ||
            TryCompilePublishedDpReplaceCapability(
                icId,
                baseCapacity,
                out composition,
                out _,
                out issues);
    }

    internal static bool TryCompileBuiltInV2DpReplace(
        string icId,
        long baseCapacity,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryCompileBuiltInV2DpReplace(
            icId,
            baseCapacity,
            selectedInputSlotIds,
            out composition,
            out _,
            out issues);
    }

    internal static bool TryCompileBuiltInV2DpReplace(
        string icId,
        long baseCapacity,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryCompilePublishedDynamicCapability(
                icId,
                IcWorkflowIds.DpReplace,
                "1-ic",
                baseCapacity,
                selectedInputSlotIds,
                out composition,
                out resolvedCapability,
                out issues) ||
            TryCompilePublishedDpReplaceCapability(
                icId,
                baseCapacity,
                out composition,
                out resolvedCapability,
                out issues);
    }

    private static bool IsDpReplaceSelectionGroupMember(string icId, string addressSpaceId)
    {
        return BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.TryGetValue(
                icId,
                out BuiltInV2Registration? registration) &&
            registration.InputSelectionGroupMemberSlotIds.Contains(
                addressSpaceId,
                StringComparer.Ordinal);
    }

    /// <summary>Returns true when the registered DP Replace profile declares an input-selection group.</summary>
    public static bool HasBuiltInV2DpReplaceSelectionGroup(string icId)
    {
        return BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.TryGetValue(
                icId,
                out BuiltInV2Registration? registration) &&
            registration.InputSelectionGroupMemberSlotIds.Count != 0;
    }

    /// <summary>
    /// Resolves the one Application-owned selection result used by DP Replace
    /// headless clients without recreating group cardinality in an adapter.
    /// </summary>
    internal static bool TryResolveBuiltInV2DpReplaceInputSelection(
        string icId,
        long? baseCapacity,
        IReadOnlyCollection<string> selectedInputSlotIds,
        out InputSelectionReadinessSnapshot? readiness,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(selectedInputSlotIds);
        readiness = null;
        if (!BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.TryGetValue(
                icId,
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
            new AuthoringRevision(0),
            details.InputContract.SelectionGroups,
            selectedInputSlotIds,
            baseCapacity is null ? CompositionAddressSpaceIds.ReferenceBase : null);
        return true;
    }

    /// <summary>
    /// Exposes the Application-owned DP Replace selection result unchanged to Presentation clients.
    /// </summary>
    public static bool TryGetDpReplaceInputSelectionReadiness(
        string icId,
        long? baseCapacity,
        IEnumerable<string> selectedInputAddressSpaceIds,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out InputSelectionReadinessSnapshot? readiness)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(selectedInputAddressSpaceIds);
        if (!HasBuiltInV2DpReplaceSelectionGroup(icId))
        {
            readiness = null;
            return false;
        }

        return TryResolveBuiltInV2DpReplaceInputSelection(
            icId,
            baseCapacity,
            [.. selectedInputAddressSpaceIds],
            out readiness,
            out _);
    }

    /// <summary>Resolves the V2 DP Replace facts needed by the workbench display without consulting legacy maps.</summary>
    internal static bool TryResolveBuiltInV2DpReplaceDisplay(
        string icId,
        long? baseCapacity,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BuiltInV2DpReplaceDisplay? display)
    {
        if (!BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.TryGetValue(
                icId,
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
                IcSupportCatalog.NormalizeIcId(icId));
    }

    /// <summary>Returns true only when Standard Merge uses the multi-capacity DP Perspective family shape.</summary>
    public static bool IsDpPerspectiveIc(string icId)
    {
        return !string.IsNullOrWhiteSpace(icId) &&
            BuiltInV2RegistrationRegistry.StandardMergeByIc.TryGetValue(
                IcSupportCatalog.NormalizeIcId(icId),
                out BuiltInV2Registration? registration) &&
            registration.TryGetContainerPolicy(out _);
    }
}

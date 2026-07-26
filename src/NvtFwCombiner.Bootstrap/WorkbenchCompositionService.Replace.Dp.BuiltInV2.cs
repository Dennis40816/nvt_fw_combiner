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
        if (TryCompilePublishedDpReplaceCapability(
                icId,
                baseCapacity,
                out composition,
                out issues))
        {
            return true;
        }

        if (!BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.TryGetValue(
                icId,
                out BuiltInV2Registration? registration))
        {
            composition = null;
            issues = [];
            return false;
        }

        registration.TryCompile(baseCapacity, out composition, out issues);
        return true;
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

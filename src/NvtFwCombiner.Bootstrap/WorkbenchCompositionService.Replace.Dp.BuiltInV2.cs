using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Compiles one supported DP Perspective Replace profile from its hash-anchored V2 bundle.</summary>
    internal static bool TryCompileDpPerspectiveDpReplace(
        string icId,
        long baseCapacity,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        if (!BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.TryGetValue(
                icId,
                out BuiltInV2DpReplaceRegistration? registration))
        {
            composition = null;
            issues = [];
            return false;
        }

        registration.TryCompile(baseCapacity, out composition, out issues);
        return true;
    }

    /// <summary>Resolves the V2 DP Replace facts needed by the workbench display without consulting legacy maps.</summary>
    internal static bool TryResolveDpPerspectiveDpReplaceDisplay(
        string icId,
        long? baseCapacity,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out DpPerspectiveDpReplaceDisplay? display)
    {
        if (!BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.TryGetValue(
                icId,
                out BuiltInV2DpReplaceRegistration? registration))
        {
            display = null;
            return false;
        }

        IReadOnlyList<long> capacities = registration.GetMapCapacities(out IReadOnlyList<CompositionIssue> capacityIssues);
        if (capacityIssues.Count != 0 || baseCapacity is null)
        {
            display = new DpPerspectiveDpReplaceDisplay(baseCapacity, capacities, Composition: null, capacityIssues);
            return true;
        }

        registration.TryCompile(baseCapacity.Value, out CompiledComposition? composition, out IReadOnlyList<CompositionIssue> issues);
        display = new DpPerspectiveDpReplaceDisplay(baseCapacity, capacities, composition, issues);
        return true;
    }

    /// <summary>Resolves a DP Replace CLI selector from the supported V2 profile registrations.</summary>
    internal static bool TryResolveDpPerspectiveDpReplaceSelector(
        string selector,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? icId)
    {
        string normalized = selector.Trim();
        foreach (BuiltInV2DpReplaceRegistration registration in BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.Values)
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

    /// <summary>Returns true when the IC uses the DP Perspective family policy.</summary>
    public static bool IsDpPerspectiveIc(string icId)
    {
        return !string.IsNullOrWhiteSpace(icId) &&
            BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.ContainsKey(
                IcSupportCatalog.NormalizeIcId(icId));
    }
}

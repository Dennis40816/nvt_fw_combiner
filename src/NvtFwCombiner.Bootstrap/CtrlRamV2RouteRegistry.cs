using System.Collections.ObjectModel;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Projects production CtrlRAM routes from the reviewed package trust index.</summary>
internal static class CtrlRamV2RouteRegistry
{
    private static readonly ReadOnlyCollection<CtrlRamV2Route> Routes = Array.AsReadOnly(
    [
        .. BuiltInV2BundleRegistry.TrustIndex.Bundles
            .SelectMany(
                static bundle => bundle.RuntimeRegistrations,
                static (bundle, registration) => (Bundle: bundle, Registration: registration))
            .Where(static item => StringComparer.Ordinal.Equals(
                item.Registration.WorkflowId,
                IcWorkflowIds.CtrlRamReplace))
            .Select(static item => new CtrlRamV2Route(
                new CtrlRamV2RouteKey(
                    item.Registration.IcId,
                    item.Registration.PostbuildProcessorId!,
                    ParseBranch(item.Registration.PostbuildBranch!)),
                item.Bundle.BundleDirectory,
                item.Registration.ProfileId,
                item.Registration.ProfileVersion))
            .OrderBy(static route => route.Key.IcId, StringComparer.Ordinal)
            .ThenBy(static route => route.Key.PostbuildProcessorId, StringComparer.Ordinal)
            .ThenBy(static route => route.Key.Branch),
    ]);

    private static readonly ReadOnlyDictionary<CtrlRamV2RouteKey, CtrlRamV2Route> ByKey =
        new(Routes.ToDictionary(static route => route.Key));

    internal static IReadOnlyList<CtrlRamV2Route> All => Routes;

    internal static bool TryResolve(
        LegacyCombinerPostbuildCommandPlan plan,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out CtrlRamV2Route? route)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return TryResolve(plan.Profile, plan.Branch, out route);
    }

    internal static bool TryResolve(
        LegacyCombinerPostbuildProfile profile,
        LegacyCombinerPostbuildBranch branch,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out CtrlRamV2Route? route)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return ByKey.TryGetValue(
            new CtrlRamV2RouteKey(profile.IcId, profile.ProcessorId, branch),
            out route);
    }

    private static LegacyCombinerPostbuildBranch ParseBranch(string token)
    {
        return token switch
        {
            "single-chip" => LegacyCombinerPostbuildBranch.SingleChip,
            "two-chip" => LegacyCombinerPostbuildBranch.TwoChip,
            "three-chip" => LegacyCombinerPostbuildBranch.ThreeChip,
            "cascade" => LegacyCombinerPostbuildBranch.Cascade,
            _ => throw new InvalidDataException("Unknown package trust-index postbuild branch."),
        };
    }
}

internal sealed record CtrlRamV2RouteKey(
    string IcId,
    string PostbuildProcessorId,
    LegacyCombinerPostbuildBranch Branch);

internal sealed record CtrlRamV2Route(
    CtrlRamV2RouteKey Key,
    string BundleId,
    string ProfileId,
    string ProfileVersion);

using System.Collections.ObjectModel;
using NvtFwCombiner.Application.ExternalTools;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Production CtrlRAM V2 routes keyed only by IC, effective postbuild profile, and build plan.</summary>
internal static class CtrlRamV2RouteRegistry
{
    private const string Nt51926Fw200ProcessorId = "nfc.nt51926.ctrlram-postbuild-v1";

    private static readonly ReadOnlyCollection<CtrlRamV2Route> Routes = Array.AsReadOnly(
    [
        Route("NT51917", "nfc.nt51917.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip,
            "nt51917-ctrlram-replace-alias-candidate", "nt51917-ctrlram-replace-fw141-single"),
        Route("NT51917", "nfc.nt51917.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.TwoChip,
            "nt51917-ctrlram-replace-alias-candidate", "nt51917-ctrlram-replace-fw132-twochip"),
        Route("NT51917", "nfc.nt51917.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.ThreeChip,
            "nt51917-ctrlram-replace-alias-candidate", "nt51917-ctrlram-replace-fw140-threechip"),
        Route("NT51919", "nfc.nt51919.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip,
            "nt51929-ctrlram-replace-candidate", "nt51919-ctrlram-replace-fw200-single"),
        Route("NT51919", "nfc.nt51919.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade,
            "nt51929-ctrlram-replace-candidate", "nt51919-ctrlram-replace-fw1x-cascade", "0.3.0"),
        Route("NT51923", "nfc.nt51923.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip,
            "nt51923-ctrlram-replace-candidate", "nt51923-ctrlram-replace-fw141-single", "0.3.0"),
        Route("NT51923", "nfc.nt51923.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade,
            "nt51923-ctrlram-replace-candidate", "nt51923-ctrlram-replace-fw141-cascade3", "0.3.0"),
        Route("NT51926", "nfc.nt51926.ctrlram-postbuild-fw1.4.1", LegacyCombinerPostbuildBranch.SingleChip,
            "nt51926-ctrlram-replace-candidate", "nt51926-ctrlram-replace-fw141-runtime-single", "0.3.0"),
        Route("NT51926", "nfc.nt51926.ctrlram-postbuild-fw1.4.1", LegacyCombinerPostbuildBranch.Cascade,
            "nt51926-ctrlram-replace-candidate", "nt51926-ctrlram-replace-fw141-runtime-cascade", "0.3.0"),
        Route("NT51926", Nt51926Fw200ProcessorId, LegacyCombinerPostbuildBranch.SingleChip,
            "nt51926-ctrlram-replace-candidate", "nt51926-ctrlram-replace-fw200-runtime-single", "0.3.0"),
        Route("NT51926", Nt51926Fw200ProcessorId, LegacyCombinerPostbuildBranch.Cascade,
            "nt51926-ctrlram-replace-candidate", "nt51926-ctrlram-replace-fw200-runtime-cascade", "0.3.0"),
        Route("NT51927", "nfc.nt51927.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip,
            "nt51927-ctrlram-replace-candidate", "nt51927-ctrlram-replace-fw141-single"),
        Route("NT51927", "nfc.nt51927.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.TwoChip,
            "nt51927-ctrlram-replace-candidate", "nt51927-ctrlram-replace-fw132-twochip"),
        Route("NT51927", "nfc.nt51927.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.ThreeChip,
            "nt51927-ctrlram-replace-candidate", "nt51927-ctrlram-replace-fw140-threechip"),
        Route("NT51928", "nfc.nt51928.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.TwoChip,
            "nt51928-ctrlram-replace-candidate", "nt51928-ctrlram-replace-fw132-twochip"),
        Route("NT51928", "nfc.nt51928.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip,
            "nt51928-ctrlram-replace-candidate", "nt51928-ctrlram-replace-fw141-single", "0.3.0"),
        Route("NT51928", "nfc.nt51928.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.ThreeChip,
            "nt51928-ctrlram-replace-candidate", "nt51928-ctrlram-replace-fw140-threechip", "0.3.0"),
        Route("NT51929", "nfc.nt51929.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip,
            "nt51929-ctrlram-replace-candidate", "nt51929-ctrlram-replace-fw200-single"),
        Route("NT51929", "nfc.nt51929.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade,
            "nt51929-ctrlram-replace-candidate", "nt51929-ctrlram-replace-fw1x-cascade", "0.3.0"),
        Route("NT51932", "nfc.nt51932.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade,
            "nt51932-ctrlram-replace-candidate", "nt51932-ctrlram-replace-fw200-cascade3", "0.3.0"),
        Route("NT51932", "nfc.nt51932.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip,
            "nt51932-ctrlram-replace-candidate", "nt51932-ctrlram-replace-fw1x-single"),
        Route("NT51950", "nfc.nt51950.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip,
            "nt51950-ctrlram-replace-candidate", "nt51950-ctrlram-replace-fw200-single", "0.3.0"),
        Route("NT51950", "nfc.nt51950.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade,
            "nt51950-ctrlram-replace-candidate", "nt51950-ctrlram-replace-fw1x-cascade", "0.5.0"),
        Route("NT51951", "nfc.nt51951.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip,
            "nt51951-ctrlram-replace-candidate", "nt51951-ctrlram-replace-fw200-single", "0.3.0"),
        Route("NT51951", "nfc.nt51951.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade,
            "nt51951-ctrlram-replace-candidate", "nt51951-ctrlram-replace-fw1x-cascade", "0.5.0"),
    ]);

    private static readonly ReadOnlyDictionary<CtrlRamV2RouteKey, CtrlRamV2Route> ByKey =
        new(
            Routes.ToDictionary(static route => route.Key));

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

    private static CtrlRamV2Route Route(
        string icId,
        string postbuildProcessorId,
        LegacyCombinerPostbuildBranch branch,
        string bundleId,
        string profileId,
        string profileVersion = "0.2.0")
    {
        return new CtrlRamV2Route(
            new CtrlRamV2RouteKey(icId, postbuildProcessorId, branch),
            bundleId,
            profileId,
            profileVersion);
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

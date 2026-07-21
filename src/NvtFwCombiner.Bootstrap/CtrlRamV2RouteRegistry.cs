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
        Route("NT51920", "nfc.nt51920.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip,
            "nt51920-ctrlram-replace-candidate", "nt51920-ctrlram-replace-fw120-single"),
        Route("NT51920", "nfc.nt51920.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade,
            "nt51920-ctrlram-replace-candidate", "nt51920-ctrlram-replace-fw120-cascade2"),
        Route("NT51923", "nfc.nt51923.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip,
            "nt51923-ctrlram-replace-candidate", "nt51923-ctrlram-replace-fw141-single"),
        Route("NT51923", "nfc.nt51923.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade,
            "nt51923-ctrlram-replace-candidate", "nt51923-ctrlram-replace-fw141-cascade3"),
        Route("NT51926", "nfc.nt51926.ctrlram-postbuild-fw1.4.1", LegacyCombinerPostbuildBranch.Cascade,
            "nt51926-ctrlram-replace-candidate", "nt51926-ctrlram-replace-fw141-runtime-cascade"),
        Route("NT51926", Nt51926Fw200ProcessorId, LegacyCombinerPostbuildBranch.SingleChip,
            "nt51926-ctrlram-replace-candidate", "nt51926-ctrlram-replace-fw200-runtime-single"),
        Route("NT51926", Nt51926Fw200ProcessorId, LegacyCombinerPostbuildBranch.Cascade,
            "nt51926-ctrlram-replace-candidate", "nt51926-ctrlram-replace-fw200-runtime-cascade"),
        Route("NT51927", "nfc.nt51927.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip,
            "nt51927-ctrlram-replace-candidate", "nt51927-ctrlram-replace-fw141-single"),
        Route("NT51927", "nfc.nt51927.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.TwoChip,
            "nt51927-ctrlram-replace-candidate", "nt51927-ctrlram-replace-fw132-twochip"),
        Route("NT51927", "nfc.nt51927.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.ThreeChip,
            "nt51927-ctrlram-replace-candidate", "nt51927-ctrlram-replace-fw140-threechip"),
        Route("NT51928", "nfc.nt51928.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.TwoChip,
            "nt51928-ctrlram-replace-candidate", "nt51928-ctrlram-replace-fw132-twochip"),
        Route("NT51929", "nfc.nt51929.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip,
            "nt51929-ctrlram-replace-candidate", "nt51929-ctrlram-replace-fw200-single"),
        Route("NT51930", "nfc.nt51930.ctrlram-postbuild-fw1.x", LegacyCombinerPostbuildBranch.Cascade,
            "nt51930-ctrlram-replace-candidate", "nt51930-ctrlram-replace-fw130-cascade3"),
        Route("NT51931", "nfc.nt51931.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade,
            "nt51931-ctrlram-replace-candidate", "nt51931-ctrlram-replace-fw130-cascade6"),
        Route("NT51932", "nfc.nt51932.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade,
            "nt51932-ctrlram-replace-candidate", "nt51932-ctrlram-replace-fw200-cascade3"),
        Route("NT51950", "nfc.nt51950.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip,
            "nt51950-ctrlram-replace-candidate", "nt51950-ctrlram-replace-fw200-single"),
        Route("NT51951", "nfc.nt51951.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip,
            "nt51951-ctrlram-replace-candidate", "nt51951-ctrlram-replace-fw200-single"),
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
        return ByKey.TryGetValue(
            new CtrlRamV2RouteKey(plan.Profile.IcId, plan.Profile.ProcessorId, plan.Branch),
            out route);
    }

    private static CtrlRamV2Route Route(
        string icId,
        string postbuildProcessorId,
        LegacyCombinerPostbuildBranch branch,
        string bundleId,
        string profileId)
    {
        return new CtrlRamV2Route(
            new CtrlRamV2RouteKey(icId, postbuildProcessorId, branch),
            bundleId,
            profileId,
            "0.2.0");
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

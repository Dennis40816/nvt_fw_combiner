using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Support;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class CurrentSupportMatrixCatalog
{
    private static void AddCtrlRamRoutes(
        List<SupportRouteDescriptor> routes,
        List<SupportUnresolvedScope> unresolved)
    {
        foreach (CtrlRamV2Route route in CtrlRamV2RouteRegistry.All)
        {
            LegacyCombinerPostbuildProfile? postbuildProfile =
                BuiltInPostbuildProfileCatalog.GetProfiles(route.Key.IcId)
                    .SingleOrDefault(profile =>
                        StringComparer.Ordinal.Equals(
                            profile.ProcessorId,
                            route.Key.PostbuildProcessorId));
            LegacyCombinerPostbuildPlanSelector? selector =
                postbuildProfile?.PlanSelectors.SingleOrDefault(
                    candidate => candidate.Branch == route.Key.Branch);
            if (postbuildProfile is null || selector is null)
            {
                unresolved.Add(Unresolved(
                    route.Key.IcId,
                    IcWorkflowIds.CtrlRamReplace,
                    $"{route.ProfileId}:{route.Key.Branch}",
                    "CtrlRAM execution route has no exact postbuild IC Count selector."));
                continue;
            }

            string countVariant = FormatIcCountVariant(selector);
            IReadOnlyList<FirmwareImageMap> maps =
                BuiltInV2BundleRegistry.All[route.BundleId].GetMapVariants(
                    route.ProfileId,
                    route.ProfileVersion,
                    route.Key.IcId,
                    IcWorkflowIds.CtrlRamReplace,
                    out IReadOnlyList<CompositionIssue> issues);
            if (issues.Count != 0 || maps.Count == 0)
            {
                unresolved.Add(Unresolved(
                    route.Key.IcId,
                    IcWorkflowIds.CtrlRamReplace,
                    $"{route.ProfileId}:{route.Key.Branch}",
                    $"CtrlRAM maps could not be resolved: {IssueCodes(issues)}."));
                continue;
            }

            LegacyCombinerPostbuildCommandPlan plan =
                LegacyCombinerPostbuildPlanner.CreatePlan(
                    postbuildProfile,
                    selector);
            foreach (FirmwareImageMap map in maps)
            {
                TopologyRequirement mapRequirement =
                    map.Applicability.TopologyRequirement;
                if (mapRequirement.Kind != TopologyRequirementKind.None &&
                    !StringComparer.Ordinal.Equals(
                        countVariant,
                        FormatIcCountVariant(mapRequirement)))
                {
                    unresolved.Add(Unresolved(
                        route.Key.IcId,
                        IcWorkflowIds.CtrlRamReplace,
                        $"{route.ProfileId}:{map.MapId}",
                        "CtrlRAM map topology conflicts with its postbuild IC Count selector."));
                    continue;
                }

                string planFingerprint =
                    LegacyCombinerPostbuildPlanner.CalculateIntegrityFingerprint(
                        plan,
                        map.CapacityBytes);
                string integrityRouteId =
                    $"{postbuildProfile.ProcessorId}:" +
                    $"{postbuildProfile.ToolBindingId}:{route.Key.Branch}|" +
                    $"fingerprint:{planFingerprint}";
                routes.Add(Route(
                    new SupportRouteIdentity(
                        route.Key.IcId,
                        IcWorkflowIds.CtrlRamReplace,
                        countVariant,
                        map.MapId,
                        integrityRouteId),
                    route.Key.IcId,
                    IcWorkflowIds.CtrlRamReplace,
                    executionAdmitted: true,
                    $"ctrlram-v2:{route.ProfileId}@{route.ProfileVersion}:" +
                    $"{map.MapId}:{integrityRouteId}"));
            }
        }
    }

    private static string FormatIcCountVariant(
        LegacyCombinerPostbuildPlanSelector selector)
    {
        return selector.Kind switch
        {
            LegacyCombinerPostbuildPlanSelectorKind.SingleChip => "1-ic",
            LegacyCombinerPostbuildPlanSelectorKind.GenericCascade =>
                "2-plus-ic",
            LegacyCombinerPostbuildPlanSelectorKind.ExactCount =>
                FormattableString.Invariant($"{selector.MinimumCount}-ic"),
            LegacyCombinerPostbuildPlanSelectorKind.CountRange =>
                FormattableString.Invariant(
                    $"{selector.MinimumCount}-{selector.MaximumCount}-ic"),
            _ => throw new InvalidOperationException(
                "Unknown postbuild IC Count selector kind."),
        };
    }
}

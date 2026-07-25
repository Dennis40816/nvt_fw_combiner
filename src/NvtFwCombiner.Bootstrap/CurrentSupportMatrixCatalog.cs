using NvtFwCombiner.Application.Support;
using NvtFwCombiner.Infrastructure.Support;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Projects current registration facts into one non-authorizing matrix.</summary>
internal static partial class CurrentSupportMatrixCatalog
{
    private static readonly SupportEvidenceCatalogSnapshot BaselineEvidence =
        new(
            "support-evidence-baseline",
            "1.0.0",
            "baseline:no-canonical-golden-or-oracle-catalog",
            []);

    internal static SupportMatrix Create()
    {
        var routes = new List<SupportRouteDescriptor>();
        var unresolved = new List<SupportUnresolvedScope>();
        AddV2Routes(BuiltInV2RegistrationRegistry.StandardMerge, routes, unresolved);
        AddV2Routes(BuiltInV2RegistrationRegistry.AbMerge, routes, unresolved);
        AddV2Routes(
            BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.Values,
            routes,
            unresolved);
        AddGeneralMergeRoutes(routes);
        AddGeneralReplaceRoutes(routes, unresolved);
        AddCtrlRamRoutes(routes, unresolved);
        AddUnboundAuthoringScopes(routes, unresolved);
        return SupportMatrixMaterializer.Materialize(
            BuiltInSupportPublicationPolicy.Load(),
            routes,
            BaselineEvidence,
            unresolved);
    }

    private static void AddGeneralMergeRoutes(
        List<SupportRouteDescriptor> routes)
    {
        foreach (GeneralMergeV2CandidateRegistration registration in
                 BuiltInV2RegistrationRegistry.GeneralMergeByIc.Values)
        {
            routes.Add(Route(
                new SupportRouteIdentity(
                    registration.IcId,
                    IcWorkflowIds.GeneralMerge,
                    "not-applicable",
                    "generic"),
                registration.IcId,
                IcWorkflowIds.GeneralMerge,
                executionAdmitted: true,
                $"built-in-v2-general-merge:{registration.ProfileId}@" +
                WorkbenchCompositionService.GeneralMergeV2CandidateProfileVersion));
        }
    }

    private static void AddGeneralReplaceRoutes(
        List<SupportRouteDescriptor> routes,
        List<SupportUnresolvedScope> unresolved)
    {
        IReadOnlyList<Domain.Firmware.FirmwareImageMap> maps =
            WorkbenchCompositionService.GetNt51926GeneralReplaceSupportMaps(
                out Domain.Composition.IcNumberInputMode?
                    icNumberInputMode,
                out IReadOnlyList<Domain.Composition.CompositionIssue>
                    issues);
        if (issues.Count != 0 || maps.Count == 0)
        {
            unresolved.Add(Unresolved(
                WorkbenchCompositionService.Nt51926GeneralReplaceIcId,
                IcWorkflowIds.GeneralReplace,
                "general-replace-v2",
                $"Exact General Replace maps could not be resolved: {IssueCodes(issues)}."));
            return;
        }

        foreach (Domain.Firmware.FirmwareImageMap map in maps)
        {
            string? countVariant = TryFormatIcCountVariant(
                map.Applicability.TopologyRequirement,
                icNumberInputMode);
            if (countVariant is null)
            {
                unresolved.Add(Unresolved(
                    WorkbenchCompositionService.Nt51926GeneralReplaceIcId,
                    IcWorkflowIds.GeneralReplace,
                    map.MapId,
                    "General Replace numeric IC Count has no exact map topology binding."));
                continue;
            }

            routes.Add(Route(
                new SupportRouteIdentity(
                    WorkbenchCompositionService.Nt51926GeneralReplaceIcId,
                    IcWorkflowIds.GeneralReplace,
                    countVariant,
                    map.MapId),
                WorkbenchCompositionService.Nt51926GeneralReplaceIcId,
                IcWorkflowIds.GeneralReplace,
                executionAdmitted: true,
                $"built-in-v2-general-replace:" +
                $"{WorkbenchCompositionService.Nt51926GeneralReplaceDpProfileId}@" +
                $"{WorkbenchCompositionService.Nt51926GeneralReplaceDpProfileVersion}:" +
                map.MapId));
        }
    }

    private static SupportRouteDescriptor Route(
        SupportRouteIdentity identity,
        string icId,
        string workflowId,
        bool executionAdmitted,
        string executionSourceId)
    {
        (SupportAuthoringAvailability authoringAvailability, string authoringSourceId) =
            ResolveAuthoring(icId, workflowId);
        return new SupportRouteDescriptor(
            identity,
            authoringAvailability,
            executionAdmitted,
            authoringSourceId,
            executionSourceId);
    }

    private static (SupportAuthoringAvailability Availability, string SourceId)
        ResolveAuthoring(string icId, string workflowId)
    {
        if (StringComparer.Ordinal.Equals(workflowId, IcWorkflowIds.AbMerge))
        {
            bool available =
                AbMergeWorkbenchCompositionService.IsAbMergeSupported(icId);
            return available
                ? (
                    SupportAuthoringAvailability.Available,
                    $"ab-merge-profile-inventory:{icId}")
                : (
                    SupportAuthoringAvailability.Unavailable,
                    $"ab-merge-profile-unavailable:{icId}");
        }

        bool catalogAvailable =
            IcSupportCatalog.SupportsWorkflow(icId, workflowId);
        if (catalogAvailable &&
            StringComparer.Ordinal.Equals(
                workflowId,
                IcWorkflowIds.GeneralReplace))
        {
            return (
                SupportAuthoringAvailability.Unknown,
                $"ic-support:{icId}:{workflowId}:unbound-exact-route");
        }

        return catalogAvailable
            ? (
                SupportAuthoringAvailability.Available,
                $"ic-support:{icId}:{workflowId}")
            : (
                SupportAuthoringAvailability.Unavailable,
                $"authoring-unavailable:{icId}:{workflowId}");
    }

    private static void AddUnboundAuthoringScopes(
        IReadOnlyList<SupportRouteDescriptor> routes,
        List<SupportUnresolvedScope> unresolved)
    {
        HashSet<(string IcId, string WorkflowId)> covered =
        [
            .. routes
                .Where(static route =>
                    route.AuthoringAvailability !=
                        SupportAuthoringAvailability.Unknown)
                .Select(static route =>
                    (route.Identity.IcId, route.Identity.WorkflowId)),
        ];
        foreach (IcSupportEntry entry in IcSupportCatalog.All)
        {
            foreach (string workflowId in entry.WorkflowIds.Where(
                         workflowId => !covered.Contains(
                             (entry.IcId, workflowId))))
            {
                unresolved.Add(Unresolved(
                    entry.IcId,
                    workflowId,
                    "authoring",
                    "Selectable catalog scope has no exact route binding."));
            }
        }
    }

    private static SupportUnresolvedScope Unresolved(
        string icId,
        string workflowId,
        string variant,
        string reason)
    {
        return new SupportUnresolvedScope(
            $"support-source:{icId}:{workflowId}:{variant}",
            icId,
            workflowId,
            reason);
    }

    private static string IssueCodes(
        IReadOnlyList<Domain.Composition.CompositionIssue> issues)
    {
        return issues.Count == 0
            ? "no issue was reported"
            : string.Join(", ", issues.Select(static issue => issue.Code));
    }
}

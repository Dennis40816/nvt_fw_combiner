using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Support;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Support;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Bootstrap adapter that projects current catalog facts into one non-authorizing Support Matrix.</summary>
internal static class CurrentSupportMatrixCatalog
{
    private static readonly SupportEvidenceCatalogSnapshot BaselineEvidenceCatalog = new(
        "support-evidence-baseline",
        "1.0.0",
        "baseline:no-canonical-golden-or-oracle-catalog",
        []);

    internal static SupportMatrix Create()
    {
        var routes = new List<SupportRouteDescriptor>();
        var unresolvedScopes = new List<SupportUnresolvedScope>();
        AddV2Routes(BuiltInV2RegistrationRegistry.StandardMerge, routes, unresolvedScopes);
        AddV2Routes(BuiltInV2RegistrationRegistry.AbMerge, routes, unresolvedScopes);
        AddV2Routes(BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.Values, routes, unresolvedScopes);
        AddGeneralMergeRoutes(routes);
        AddGeneralReplaceRoutes(routes, unresolvedScopes);
        AddCtrlRamRoutes(routes, unresolvedScopes);
        AddUnresolvedAuthoringScopes(unresolvedScopes);
        return SupportMatrixMaterializer.Materialize(
            BuiltInSupportPublicationPolicy.Load(),
            routes,
            BaselineEvidenceCatalog,
            unresolvedScopes);
    }

    private static void AddV2Routes(
        IEnumerable<BuiltInV2Registration> registrations,
        List<SupportRouteDescriptor> routes,
        List<SupportUnresolvedScope> unresolvedScopes)
    {
        foreach (BuiltInV2Registration registration in registrations)
        {
            IReadOnlyList<long> capacities = registration.GetMapCapacities(out IReadOnlyList<CompositionIssue> issues);
            if (issues.Count != 0 || capacities.Count == 0)
            {
                unresolvedScopes.Add(Unresolved(
                    registration.IcId,
                    registration.WorkflowId,
                    $"Built-in V2 registration cannot resolve declared map variants: {string.Join(", ", issues.Select(static issue => issue.Code))}."));
                continue;
            }

            int admitted = 0;
            foreach (long capacity in capacities)
            {
                foreach ((string countVariant, TopologySelection? topology) in TopologiesFor(registration))
                {
                    registration.TryCompile(capacity, topology, out CompiledComposition? composition, out IReadOnlyList<CompositionIssue> compileIssues);
                    if (composition?.V2Details?.Provenance.ResolvedMap.ImageMap is not { } map)
                    {
                        unresolvedScopes.Add(UnresolvedVariant(
                            registration.IcId,
                            registration.WorkflowId,
                            countVariant,
                            capacity,
                            $"Built-in V2 registration cannot compile its declared exact variant: {string.Join(", ", compileIssues.Select(static issue => issue.Code))}."));
                        continue;
                    }

                    admitted++;
                    routes.Add(new SupportRouteDescriptor(
                        CreateRouteId(registration.IcId, registration.WorkflowId, countVariant, map.MapId),
                        registration.IcId,
                        registration.WorkflowId,
                        countVariant,
                        map.MapId,
                        SupportAuthoringAvailability.Unknown,
                        ExecutionAdmitted: true,
                        UnresolvedAuthoringSource(registration.IcId, registration.WorkflowId),
                        $"built-in-v2:{registration.ProfileId}@{registration.ProfileVersion}:{map.MapId}"));
                }
            }

            if (admitted == 0)
            {
                unresolvedScopes.Add(Unresolved(
                    registration.IcId,
                    registration.WorkflowId,
                    "Built-in V2 registration exposed no execution-admitted exact map variant."));
            }
        }
    }

    private static IEnumerable<(string CountVariant, TopologySelection? Topology)> TopologiesFor(
        BuiltInV2Registration registration)
    {
        if (registration.WorkflowId == IcWorkflowIds.DpReplace)
        {
            return [("single", null)];
        }

        if (registration.WorkflowId != IcWorkflowIds.AbMerge || !registration.HasMultipleMapCapacities)
        {
            return [("selector-free", null)];
        }

        return
        [
            ("single", new TopologySelection(1, "1 IC", TopologySelectionSource.Requested, "support-matrix")),
            ("cascade", new TopologySelection(2, "Cascade", TopologySelectionSource.Requested, "support-matrix")),
        ];
    }

    private static void AddGeneralMergeRoutes(List<SupportRouteDescriptor> routes)
    {
        foreach (GeneralMergeV2CandidateRegistration registration in BuiltInV2RegistrationRegistry.GeneralMergeByIc.Values)
        {
            string executionSource = $"built-in-v2-general-merge-candidate:{registration.ProfileId}";
            routes.Add(new SupportRouteDescriptor(
                $"{registration.IcId.ToLowerInvariant()}-general-merge-generic",
                registration.IcId,
                IcWorkflowIds.GeneralMerge,
                "not-applicable",
                "generic",
                SupportAuthoringAvailability.Unknown,
                ExecutionAdmitted: false,
                UnresolvedAuthoringSource(registration.IcId, IcWorkflowIds.GeneralMerge),
                executionSource));
        }
    }

    private static void AddGeneralReplaceRoutes(
        List<SupportRouteDescriptor> routes,
        List<SupportUnresolvedScope> unresolvedScopes)
    {
        IReadOnlyList<string> mapIds = WorkbenchCompositionService.GetNt51926GeneralReplaceDpSupportMapIds(
            out IReadOnlyList<CompositionIssue> issues);
        if (issues.Count != 0 || mapIds.Count == 0)
        {
            unresolvedScopes.Add(Unresolved(
                WorkbenchCompositionService.Nt51926GeneralReplaceIcId,
                IcWorkflowIds.GeneralReplace,
                $"Built-in V2 General Replace runtime route cannot resolve declared map variants: {string.Join(", ", issues.Select(static issue => issue.Code))}."));
            return;
        }

        foreach (string mapId in mapIds)
        {
            routes.Add(new SupportRouteDescriptor(
                CreateRouteId(WorkbenchCompositionService.Nt51926GeneralReplaceIcId, IcWorkflowIds.GeneralReplace, "single", mapId),
                WorkbenchCompositionService.Nt51926GeneralReplaceIcId,
                IcWorkflowIds.GeneralReplace,
                "single",
                mapId,
                SupportAuthoringAvailability.Unknown,
                ExecutionAdmitted: true,
                UnresolvedAuthoringSource(WorkbenchCompositionService.Nt51926GeneralReplaceIcId, IcWorkflowIds.GeneralReplace),
                $"built-in-v2-general-replace:{WorkbenchCompositionService.Nt51926GeneralReplaceDpProfileId}@{WorkbenchCompositionService.Nt51926GeneralReplaceDpProfileVersion}:{mapId}"));
        }
    }

    private static void AddCtrlRamRoutes(
        List<SupportRouteDescriptor> routes,
        List<SupportUnresolvedScope> unresolvedScopes)
    {
        foreach (CtrlRamV2Route route in CtrlRamV2RouteRegistry.All)
        {
            string countVariant = route.Key.Branch switch
            {
                LegacyCombinerPostbuildBranch.SingleChip => "single",
                LegacyCombinerPostbuildBranch.TwoChip => "two-chip",
                LegacyCombinerPostbuildBranch.ThreeChip => "three-chip",
                LegacyCombinerPostbuildBranch.Cascade => "cascade",
                _ => throw new InvalidOperationException("Unknown CtrlRAM IC-count branch."),
            };
            string executionSource = $"ctrlram-v2:{route.ProfileId}@{route.ProfileVersion}:{route.Key.PostbuildProcessorId}";
            string integrityRouteId = CreateIntegrityRouteId(route);
            IReadOnlyList<string> mapIds = BuiltInV2BundleRegistry.All[route.BundleId].GetMapIds(
                route.ProfileId,
                route.ProfileVersion,
                route.Key.IcId,
                IcWorkflowIds.CtrlRamReplace,
                out IReadOnlyList<CompositionIssue> issues);
            if (issues.Count != 0 || mapIds.Count == 0)
            {
                unresolvedScopes.Add(Unresolved(
                    route.Key.IcId,
                    IcWorkflowIds.CtrlRamReplace,
                    $"CtrlRAM V2 registration cannot resolve declared map variants: {string.Join(", ", issues.Select(static issue => issue.Code))}."));
                continue;
            }

            foreach (string mapId in mapIds)
            {
                routes.Add(new SupportRouteDescriptor(
                    CreateRouteId(route.Key.IcId, IcWorkflowIds.CtrlRamReplace, countVariant, mapId, integrityRouteId),
                    route.Key.IcId,
                    IcWorkflowIds.CtrlRamReplace,
                    countVariant,
                    mapId,
                    SupportAuthoringAvailability.Unknown,
                    ExecutionAdmitted: true,
                    UnresolvedAuthoringSource(route.Key.IcId, IcWorkflowIds.CtrlRamReplace),
                    $"{executionSource}:{mapId}",
                    integrityRouteId));
            }
        }
    }

    private static void AddUnresolvedAuthoringScopes(List<SupportUnresolvedScope> unresolvedScopes)
    {
        foreach (IcSupportEntry entry in IcSupportCatalog.All)
        {
            foreach (string workflowId in entry.WorkflowIds)
            {
                unresolvedScopes.Add(Unresolved(
                    entry.IcId,
                    workflowId,
                    "IC support catalog exposes only IC/workflow authoring scope; no exact IC-count and map-variant binding is available."));
            }
        }
    }

    private static SupportUnresolvedScope Unresolved(string icId, string workflowId, string reason)
    {
        return new SupportUnresolvedScope($"ic-support:{icId}:{workflowId}", icId, workflowId, reason);
    }

    private static SupportUnresolvedScope UnresolvedVariant(
        string icId,
        string workflowId,
        string countVariant,
        long capacity,
        string reason)
    {
        return new SupportUnresolvedScope(
            $"built-in-v2:{icId}:{workflowId}:{countVariant}:0x{capacity:X}",
            icId,
            workflowId,
            reason);
    }

    private static string UnresolvedAuthoringSource(string icId, string workflowId)
    {
        return $"authoring-unresolved:{icId}:{workflowId}";
    }

    private static string CreateRouteId(
        string icId,
        string workflowId,
        string countVariant,
        string mapVariant,
        string? integrityRouteId = null)
    {
        string prefix = $"{icId.ToLowerInvariant()}-{workflowId}";
        string routeId = workflowId == IcWorkflowIds.GeneralMerge
            ? $"{prefix}-generic"
            : workflowId == IcWorkflowIds.AbMerge
                ? countVariant == "selector-free"
                    ? $"{prefix}-selector-free"
                    : $"{prefix}-{countVariant}"
                : $"{prefix}-{mapVariant}";
        return string.IsNullOrWhiteSpace(integrityRouteId)
            ? routeId
            : $"{routeId}-integrity-{RouteIdHash(integrityRouteId)}";
    }

    private static string CreateIntegrityRouteId(CtrlRamV2Route route)
    {
        return $"{route.Key.PostbuildProcessorId}:{route.Key.Branch}";
    }

    private static string RouteIdHash(string value)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];
    }
}

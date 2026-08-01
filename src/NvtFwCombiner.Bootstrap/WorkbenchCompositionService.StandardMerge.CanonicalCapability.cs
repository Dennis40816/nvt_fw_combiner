using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string SelectorFreeIcCountVariant = "selector-free";

    private static readonly CanonicalCapabilityCatalog
        s_canonicalCapabilityCatalog = CreateCanonicalCapabilityCatalog();

    internal static CapabilityCatalogReloadResult
        ReloadCanonicalCapabilityCatalog(CancellationToken cancellationToken)
    {
        return s_canonicalCapabilityCatalog.Reload(cancellationToken);
    }

    internal static CapabilityResolutionResult
        ResolveCanonicalStandardMergeCapability(string icId)
    {
        return s_canonicalCapabilityCatalog.ResolveUniqueRoute(
            icId,
            IcWorkflowIds.StandardMerge,
            SelectorFreeIcCountVariant);
    }

    private static CapabilityResolutionResult
        ResolveCanonicalStandardMergeCapability(string icId, long? outputCapacity)
    {
        return s_canonicalCapabilityCatalog.ResolveUniqueRoute(
            icId,
            IcWorkflowIds.StandardMerge,
            SelectorFreeIcCountVariant,
            outputCapacity);
    }

    internal static CapabilityResolutionResult
        ResolveCanonicalDpReplaceCapability(string icId)
    {
        return s_canonicalCapabilityCatalog.ResolveUniqueRoute(
            icId,
            IcWorkflowIds.DpReplace,
            "1-ic");
    }

    private static CapabilityResolutionResult ResolveCanonicalDpReplaceCapability(
        string icId,
        long outputCapacity)
    {
        return s_canonicalCapabilityCatalog.ResolveUniqueRoute(
            icId,
            IcWorkflowIds.DpReplace,
            "1-ic",
            outputCapacity);
    }

    internal static CapabilityResolutionResult ResolveCanonicalAbMergeCapability(
        string icId,
        TopologySelection? topology)
    {
        return s_canonicalCapabilityCatalog.ResolveUniqueTopologyRoute(
            icId,
            IcWorkflowIds.AbMerge,
            topology);
    }

    internal static bool HasCanonicalCapability(
        string icId,
        string workflowId)
    {
        return s_canonicalCapabilityCatalog.CurrentSnapshot?.Capabilities.Any(
            capability =>
                StringComparer.Ordinal.Equals(capability.Identity.IcId, icId) &&
                StringComparer.Ordinal.Equals(
                    capability.Identity.WorkflowId,
                    workflowId) &&
                capability.Authoring.Value ==
                    CapabilityAuthoringAvailability.Available &&
                capability.ExecutionAdmitted) == true;
    }

    private static bool TryCompilePublishedStandardMergeCapability(
        string icId,
        long? outputCapacity,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        CapabilityResolutionResult resolution =
            ResolveCanonicalStandardMergeCapability(icId, outputCapacity);
        if (outputCapacity is null &&
            StringComparer.Ordinal.Equals(
                resolution.Issue?.Code,
                CapabilityCatalogIssueCodes.RouteAmbiguous) &&
            GetCanonicalOutputCapacities(
                icId,
                IcWorkflowIds.StandardMerge).Length > 1)
        {
            composition = null;
            issues = [];
            return false;
        }

        if (StringComparer.Ordinal.Equals(
                resolution.Issue?.Code,
                CapabilityCatalogIssueCodes.RouteUnavailable))
        {
            long[] capacities = GetCanonicalOutputCapacities(
                icId,
                IcWorkflowIds.StandardMerge);
            if (outputCapacity is not null && capacities.Length != 0)
            {
                composition = null;
                issues =
                [
                    new CompositionIssue(
                        WorkbenchIssueCodes.StandardMergeDpLengthUnsupported,
                        $"Selected DP BIN length 0x{outputCapacity.Value:X} is unsupported; {icId} Standard Merge profile accepts DP input lengths {BuiltInV2Bundle.FormatCapacities(capacities)}."),
                ];
                return true;
            }

            composition = null;
            issues = [];
            return false;
        }

        composition = resolution.Capability?.CompiledComposition;
        issues = resolution.Issue is null
            ? []
            :
            [
                new CompositionIssue(
                    resolution.Issue.Code,
                    resolution.Issue.Message),
            ];
        return true;
    }

    private static bool TryCompilePublishedDpReplaceCapability(
        string icId,
        long baseCapacity,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        CapabilityResolutionResult resolution =
            ResolveCanonicalDpReplaceCapability(icId, baseCapacity);
        if (StringComparer.Ordinal.Equals(
                resolution.Issue?.Code,
                CapabilityCatalogIssueCodes.RouteUnavailable))
        {
            long[] capacities = GetCanonicalOutputCapacities(
                icId,
                IcWorkflowIds.DpReplace);
            if (capacities.Length != 0)
            {
                composition = null;
                issues =
                [
                    new CompositionIssue(
                        CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                        $"{icId} DP Replace base flash BIN length must be one of {BuiltInV2Bundle.FormatCapacities(capacities)} (actual 0x{baseCapacity:X})."),
                ];
                return true;
            }

            composition = null;
            issues = [];
            return false;
        }

        composition = resolution.Capability?.CompiledComposition;
        long? resolvedCapacity = composition?.V2Details?.Provenance
            .ResolvedMap.CapacityBytes;
        if (composition is not null && resolvedCapacity != baseCapacity)
        {
            composition = null;
            issues =
            [
                new CompositionIssue(
                    CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                    $"{icId} DP Replace base flash BIN length must be 0x{resolvedCapacity:X} (actual 0x{baseCapacity:X})."),
            ];
            return true;
        }

        issues = resolution.Issue is null
            ? []
            :
            [
                new CompositionIssue(
                    resolution.Issue.Code,
                    resolution.Issue.Message),
            ];
        return true;
    }

    private static bool TryCompilePublishedDynamicCapability(
        string icId,
        string workflowId,
        string icCountVariant,
        long? requestedMapCapacity,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ResolvedCapabilityRoute? publishedRoute =
            s_canonicalCapabilityCatalog.CurrentSnapshot?.DynamicRoutes
                .SingleOrDefault(route =>
                    StringComparer.Ordinal.Equals(route.Identity.IcId, icId) &&
                    StringComparer.Ordinal.Equals(
                        route.Identity.WorkflowId,
                        workflowId) &&
                    StringComparer.Ordinal.Equals(
                        route.Identity.IcCountVariant,
                        icCountVariant));
        if (publishedRoute is null)
        {
            composition = null;
            issues = [];
            return false;
        }

        CapabilityRouteResolutionResult resolution =
            s_canonicalCapabilityCatalog.ResolveDynamicRoute(
                publishedRoute.Identity.RouteId);
        if (!resolution.Succeeded)
        {
            composition = null;
            issues =
            [
                new CompositionIssue(
                    resolution.Issue!.Code,
                    resolution.Issue.Message),
            ];
            return true;
        }

        BuiltInV2Registration registration = workflowId switch
        {
            IcWorkflowIds.StandardMerge =>
                BuiltInV2RegistrationRegistry.StandardMergeByIc[icId],
            IcWorkflowIds.DpReplace =>
                BuiltInV2RegistrationRegistry.DpReplaceByIc.Value[icId],
            _ => throw new InvalidOperationException(
                "Only registered map-bound dynamic routes use this compiler adapter."),
        };
        registration.TryCompile(
            requestedMapCapacity,
            requestedTopology: null,
            selectedInputSlotIds,
            out CompiledComposition? compiled,
            out issues);
        if (compiled is null || issues.Count != 0)
        {
            composition = null;
            return true;
        }

        composition = resolution.Route!.BindCompilation(
            compiled,
            registration.CreateMetadataPlan(compiled)).CompiledComposition;
        return true;
    }

    private static long[] GetCanonicalOutputCapacities(
        string icId,
        string workflowId)
    {
        IReadOnlyList<ResolvedCapability> capabilities =
            s_canonicalCapabilityCatalog.CurrentSnapshot?.Capabilities ?? [];
        return
        [
            .. capabilities
                .Where(capability =>
                    StringComparer.Ordinal.Equals(capability.Identity.IcId, icId) &&
                    StringComparer.Ordinal.Equals(
                        capability.Identity.WorkflowId,
                        workflowId))
                .Select(static capability =>
                    capability.CompiledComposition.Plan.OutputInitialization.Capacity)
                .Distinct()
                .Order(),
        ];
    }

    internal static ResolvedCapability? ResolveCanonicalCapabilityForRun(
        CompiledComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);
        CanonicalCapabilityCatalogSnapshot? snapshot =
            s_canonicalCapabilityCatalog.CurrentSnapshot;
        if (snapshot is null || composition.CapabilityFingerprint is null)
        {
            return null;
        }

        ResolvedCapability? fixedCapability = snapshot.Capabilities.SingleOrDefault(
            capability =>
                StringComparer.Ordinal.Equals(
                    capability.CapabilityFingerprint,
                    composition.CapabilityFingerprint) &&
                StringComparer.Ordinal.Equals(
                    capability.CompiledComposition.CompilationFingerprint,
                    composition.CompilationFingerprint));
        if (fixedCapability is not null)
        {
            return fixedCapability;
        }

        ResolvedCapabilityRoute? dynamicRoute = snapshot.DynamicRoutes.SingleOrDefault(
            route => StringComparer.Ordinal.Equals(
                route.CapabilityFingerprint,
                composition.CapabilityFingerprint));
        if (dynamicRoute is null)
        {
            return null;
        }

        BuiltInV2Registration? registration = dynamicRoute.Identity.WorkflowId switch
        {
            IcWorkflowIds.StandardMerge =>
                BuiltInV2RegistrationRegistry.StandardMergeByIc.GetValueOrDefault(
                    dynamicRoute.Identity.IcId),
            IcWorkflowIds.DpReplace =>
                BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.GetValueOrDefault(
                    dynamicRoute.Identity.IcId),
            _ => null,
        };
        return registration is null
            ? null
            : dynamicRoute.BindCompilation(
                composition,
                registration.CreateMetadataPlan(composition));
    }

    private static CanonicalCapabilityCatalog CreateCanonicalCapabilityCatalog()
    {
        var catalog = new CanonicalCapabilityCatalog(
            new CanonicalCapabilityCatalogMigrationSource());
        _ = catalog.Reload();
        return catalog;
    }
}

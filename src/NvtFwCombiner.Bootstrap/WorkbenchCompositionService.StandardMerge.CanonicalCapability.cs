using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private const string SelectorFreeIcCountVariant = "selector-free";

    internal static CapabilityCatalogReloadResult
        ReloadCanonicalCapabilityCatalog(CancellationToken cancellationToken)
    {
        return WorkbenchHostServices.CanonicalCapabilities.Reload(cancellationToken);
    }

    internal static CapabilityResolutionResult
        ResolveCanonicalStandardMergeCapability(string icId)
    {
        return WorkbenchHostServices.CanonicalCapabilities.Read(
            catalog => catalog.ResolveUniqueRoute(
                icId,
                IcWorkflowIds.StandardMerge,
                SelectorFreeIcCountVariant));
    }

    private static CapabilityResolutionResult
        ResolveCanonicalStandardMergeCapability(string icId, long? outputCapacity)
    {
        return WorkbenchHostServices.CanonicalCapabilities.Read(
            catalog => catalog.ResolveUniqueRoute(
                icId,
                IcWorkflowIds.StandardMerge,
                SelectorFreeIcCountVariant,
                outputCapacity));
    }

    internal static CapabilityResolutionResult
        ResolveCanonicalDpReplaceCapability(string icId)
    {
        return WorkbenchHostServices.CanonicalCapabilities.Read(
            catalog => catalog.ResolveUniqueRoute(
                icId,
                IcWorkflowIds.DpReplace,
                "1-ic"));
    }

    private static CapabilityResolutionResult ResolveCanonicalDpReplaceCapability(
        string icId,
        long outputCapacity)
    {
        return WorkbenchHostServices.CanonicalCapabilities.Read(
            catalog => catalog.ResolveUniqueRoute(
                icId,
                IcWorkflowIds.DpReplace,
                "1-ic",
                outputCapacity));
    }

    internal static CapabilityResolutionResult ResolveCanonicalAbMergeCapability(
        string icId,
        TopologySelection? topology)
    {
        return WorkbenchHostServices.CanonicalCapabilities.Read(
            catalog => catalog.ResolveUniqueTopologyRoute(
                icId,
                IcWorkflowIds.AbMerge,
                topology));
    }

    internal static bool HasCanonicalCapability(
        string icId,
        string workflowId)
    {
        return HasCanonicalCapability(
            WorkbenchHostServices.CanonicalCapabilities.Read(
                static catalog => catalog.CurrentSnapshot),
            icId,
            workflowId);
    }

    internal static bool HasCanonicalCapability(
        CanonicalCapabilityCatalogSnapshot? snapshot,
        string icId,
        string workflowId)
    {
        if (snapshot is null ||
            string.IsNullOrWhiteSpace(icId) ||
            string.IsNullOrWhiteSpace(workflowId))
        {
            return false;
        }

        string normalizedIcId = IcSupportCatalog.NormalizeIcId(icId);
        return snapshot.Capabilities.Any(
            capability =>
                StringComparer.Ordinal.Equals(
                    capability.Identity.IcId,
                    normalizedIcId) &&
                StringComparer.Ordinal.Equals(
                    capability.Identity.WorkflowId,
                    workflowId) &&
                capability.Authoring.Value ==
                    CapabilityAuthoringAvailability.Available &&
                capability.ExecutionAdmitted) ||
            snapshot.DynamicRoutes.Any(route =>
                StringComparer.Ordinal.Equals(
                    route.Identity.IcId,
                    normalizedIcId) &&
                StringComparer.Ordinal.Equals(
                    route.Identity.WorkflowId,
                    workflowId) &&
                route.Authoring.Value ==
                    CapabilityAuthoringAvailability.Available);
    }

    private static bool TryCompilePublishedStandardMergeCapability(
        string icId,
        long? outputCapacity,
        out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        resolvedCapability = null;
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
        resolvedCapability = resolution.Capability;
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
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        resolvedCapability = null;
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
        long? resolvedCapacity = composition?.V2Details.Provenance
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

        resolvedCapability = resolution.Capability;
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
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        resolvedCapability = null;
        string normalizedIcId = IcSupportCatalog.NormalizeIcId(icId);
        ResolvedCapabilityRoute? publishedRoute =
            WorkbenchHostServices.CanonicalCapabilities.Read(
                static catalog => catalog.CurrentSnapshot)?.DynamicRoutes
                .SingleOrDefault(route =>
                    StringComparer.Ordinal.Equals(
                        route.Identity.IcId,
                        normalizedIcId) &&
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
            WorkbenchHostServices.CanonicalCapabilities.Read(
                catalog => catalog.ResolveDynamicRoute(
                    publishedRoute.Identity.RouteId));
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
                BuiltInV2RegistrationRegistry.StandardMergeByIc[normalizedIcId],
            IcWorkflowIds.DpReplace =>
                BuiltInV2RegistrationRegistry.DpReplaceByIc.Value[normalizedIcId],
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

        resolvedCapability = resolution.Route!.BindCompilation(
            compiled,
            registration.CreateMetadataPlan(compiled));
        composition = resolvedCapability.CompiledComposition;
        return true;
    }

    private static long[] GetCanonicalOutputCapacities(
        string icId,
        string workflowId)
    {
        string normalizedIcId = IcSupportCatalog.NormalizeIcId(icId);
        IReadOnlyList<ResolvedCapability> capabilities =
            WorkbenchHostServices.CanonicalCapabilities.Read(
                static catalog => catalog.CurrentSnapshot)?.Capabilities ?? [];
        return
        [
            .. capabilities
                .Where(capability =>
                    StringComparer.Ordinal.Equals(
                        capability.Identity.IcId,
                        normalizedIcId) &&
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
        CompiledComposition composition,
        ResolvedCapability? acceptedCapability = null)
    {
        ArgumentNullException.ThrowIfNull(composition);
        CanonicalCapabilityCatalogSnapshot? snapshot =
            WorkbenchHostServices.CanonicalCapabilities.Read(
                static catalog => catalog.CurrentSnapshot);
        if (snapshot is null || composition.CapabilityFingerprint is null)
        {
            return null;
        }

        if (acceptedCapability is not null)
        {
            if (!ReferenceEquals(
                    acceptedCapability.CompiledComposition,
                    composition) ||
                acceptedCapability.ResolutionToken != snapshot.ResolutionToken ||
                acceptedCapability.Authoring.Value !=
                    CapabilityAuthoringAvailability.Available)
            {
                return null;
            }

            CapabilityResolutionResult fixedResolution =
                WorkbenchHostServices.CanonicalCapabilities.Read(
                    catalog => catalog.Resolve(
                        acceptedCapability.Identity.RouteId));
            if (fixedResolution.Succeeded)
            {
                return ReferenceEquals(
                        fixedResolution.Capability!.CompiledComposition,
                        composition) &&
                    fixedResolution.Capability.ResolutionToken ==
                        acceptedCapability.ResolutionToken
                            ? acceptedCapability
                            : null;
            }

            CapabilityRouteResolutionResult dynamicResolution =
                WorkbenchHostServices.CanonicalCapabilities.Read(
                    catalog => catalog.ResolveDynamicRoute(
                        acceptedCapability.Identity.RouteId));
            return dynamicResolution.Succeeded &&
                dynamicResolution.Route!.ResolutionToken ==
                    acceptedCapability.ResolutionToken &&
                StringComparer.Ordinal.Equals(
                    dynamicResolution.Route.CapabilityFingerprint,
                    acceptedCapability.CapabilityFingerprint)
                        ? acceptedCapability
                        : null;
        }

        ResolvedCapability? fixedCapability = snapshot.Capabilities.SingleOrDefault(
            capability => ReferenceEquals(
                capability.CompiledComposition,
                composition));
        if (fixedCapability is null)
        {
            return null;
        }

        CapabilityResolutionResult current =
            WorkbenchHostServices.CanonicalCapabilities.Read(
                catalog => catalog.Resolve(fixedCapability.Identity.RouteId));
        return current.Succeeded &&
            ReferenceEquals(current.Capability!.CompiledComposition, composition)
                ? current.Capability
                : null;
    }
}

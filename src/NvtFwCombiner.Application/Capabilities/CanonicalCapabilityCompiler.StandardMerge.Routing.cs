using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Capabilities;

internal sealed partial class CanonicalCapabilityCompilerAdapter
{
    private MetadataPlanResolutionResult? ResolveSourceEnvelopeMetadataPlan(
        string icId,
        long dpInputLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentOutOfRangeException.ThrowIfNegative(dpInputLength);
        string normalizedIcId = IcIdentifier.Normalize(icId);
        CanonicalCapabilityCatalogSnapshot? snapshot = _catalog.TryGetCurrentSnapshot();
        if (snapshot is null)
        {
            return MetadataFailure(CapabilityCatalogIssueCodes.CatalogUnavailable,
                "No valid canonical capability catalog is loaded.");
        }

        bool declared = TryGetStandardSourceEnvelopeRoutes(
            normalizedIcId, out StandardSourceEnvelopeRoutes? routes,
            out IReadOnlyList<CompositionIssue> issues);
        if (issues.Count != 0)
        {
            return MetadataFailure(issues[0].Code, issues[0].Message);
        }
        if (!declared)
        {
            return null;
        }

        ResolvedCapabilityRoute? route = routes!.Select(dpInputLength, captured: false);
        if (route is null)
        {
            return MetadataFailure(CapabilityCatalogIssueCodes.RouteUnavailable,
                "The Standard DP length has no published exact metadata route.");
        }

        bool compiled = TryCompilePublishedClassificationCandidate(
            route, dpInputLength, [], out ResolvedCapability? capability);
        return !IsCurrentPublishedRoute(routes.Snapshot, route)
            ? MetadataFailure(AuthoringSessionIssueCodes.StalePublication,
                "The canonical Standard publication changed during metadata lookup.")
            : compiled && capability is not null
                ? new MetadataPlanResolutionResult(capability.MetadataPlan, null)
                : MetadataFailure(CapabilityCatalogIssueCodes.RouteUnavailable,
                    "The published Standard exact route did not supply its metadata plan.");

        MetadataPlanResolutionResult MetadataFailure(string code, string message)
        {
            return new(null, new CapabilityCatalogIssue(code, message, normalizedIcId));
        }
    }

    internal bool TryGetPublishedStandardSourceEnvelopeRoute(
        string icId,
        out ResolvedCapabilityRoute? route,
        out IReadOnlyList<CompositionIssue> issues)
    {
        bool declared = TryGetStandardSourceEnvelopeRoutes(
            icId, out StandardSourceEnvelopeRoutes? routes, out issues);
        route = issues.Count == 0 ? routes?.TemplateRoute : null;
        return declared;
    }

    internal bool TryGetPublishedStandardSourceEnvelopeClassificationRoutes(
        CanonicalCapabilityCatalogSnapshot expectedSnapshot,
        string icId,
        out IReadOnlyList<(ResolvedCapabilityRoute Route, long Capacity)> exactRoutes)
    {
        bool declared = TryGetStandardSourceEnvelopeRoutes(
            icId, out StandardSourceEnvelopeRoutes? routes,
            out IReadOnlyList<CompositionIssue> issues);
        exactRoutes = [];
        if (issues.Count != 0)
        {
            return false;
        }
        if (!declared)
        {
            return _catalog.TryGetCurrentSnapshot()?.ResolutionToken ==
                expectedSnapshot.ResolutionToken;
        }
        if (routes!.Snapshot.ResolutionToken != expectedSnapshot.ResolutionToken ||
            !IsCurrentPublishedRoute(expectedSnapshot, routes.TemplateRoute))
        {
            return false;
        }
        exactRoutes = [.. routes.ExactRoutes.Select(static item =>
            (item.Route, item.Capacity))];
        return true;
    }

    private sealed record StandardSourceEnvelopeRoutes(
        CanonicalCapabilityCatalogSnapshot Snapshot,
        ResolvedCapabilityRoute TemplateRoute,
        IReadOnlyList<(long Capacity, ResolvedCapabilityRoute Route)> ExactRoutes)
    {
        internal ResolvedCapabilityRoute? Select(long length, bool captured)
        {
            foreach ((long capacity, ResolvedCapabilityRoute route) in ExactRoutes)
            {
                if (capacity == length)
                {
                    return route;
                }
            }
            return captured ? TemplateRoute : null;
        }
    }

    // The only Standard source-envelope capacity/map-to-route decision in Application.
    private bool TryGetStandardSourceEnvelopeRoutes(
        string icId,
        out StandardSourceEnvelopeRoutes? selectedRoutes,
        out IReadOnlyList<CompositionIssue> issues)
    {
        selectedRoutes = null;
        issues = [];
        string normalizedIcId = IcIdentifier.Normalize(icId);
        CanonicalCapabilityCatalogSnapshot? snapshot = _catalog.TryGetCurrentSnapshot();
        ResolvedCapabilityRoute[] published = snapshot is null ? [] :
        [.. snapshot.DynamicRoutes.Where(route =>
            StringComparer.Ordinal.Equals(route.Identity.IcId, normalizedIcId) &&
            StringComparer.Ordinal.Equals(route.Identity.WorkflowId, ExperienceIds.StandardMerge) &&
            StringComparer.Ordinal.Equals(route.Identity.IcCountVariant, SelectorFreeIcCountVariant))];
        if (published.Length == 0)
        {
            return false;
        }

        bool declared = _dynamicCompiler.TryGetSourceEnvelopeMapVariant(
            normalizedIcId, ExperienceIds.StandardMerge, sourceLength: null,
            out string? templateMapId, out issues);
        if (issues.Count != 0)
        {
            return true;
        }
        if (!declared)
        {
            return false;
        }

        ResolvedCapabilityRoute[] templates =
            [.. published.Where(route => StringComparer.Ordinal.Equals(
                route.Identity.MapVariant, templateMapId))];
        if (templates.Length != 1)
        {
            issues = [new CompositionIssue(templates.Length == 0
                ? CapabilityCatalogIssueCodes.RouteUnavailable
                : CapabilityCatalogIssueCodes.RouteAmbiguous,
                "The source-envelope template has no unique published route.")];
            return true;
        }

        IReadOnlyList<long> capacities = _dynamicCompiler.GetMapCapacities(
            normalizedIcId, ExperienceIds.StandardMerge, out issues);
        if (issues.Count != 0)
        {
            return true;
        }
        var exact = new List<(long Capacity, ResolvedCapabilityRoute Route)>();
        var mappedRoutes = new HashSet<ResolvedCapabilityRoute>();
        foreach (long capacity in capacities)
        {
            if (capacity <= 0 || exact.Any(item => item.Capacity == capacity))
            {
                issues = [new CompositionIssue(CapabilityCatalogIssueCodes.RouteAmbiguous,
                    "The trusted Standard exact capacities are invalid or ambiguous.")];
                return true;
            }
            bool exactDeclared = _dynamicCompiler.TryGetSourceEnvelopeMapVariant(
                normalizedIcId, ExperienceIds.StandardMerge, capacity,
                out string? mapId, out issues);
            if (issues.Count != 0)
            {
                return true;
            }
            if (!exactDeclared || mapId is null)
            {
                issues = [new CompositionIssue(CapabilityCatalogIssueCodes.RouteUnavailable,
                    "The trusted Standard exact capacity has no map declaration.")];
                return true;
            }
            ResolvedCapabilityRoute[] matches =
                [.. published.Where(route => StringComparer.Ordinal.Equals(
                    route.Identity.MapVariant, mapId))];
            if (matches.Length != 1 || !mappedRoutes.Add(matches[0]))
            {
                issues = [new CompositionIssue(matches.Length == 0
                    ? CapabilityCatalogIssueCodes.RouteUnavailable
                    : CapabilityCatalogIssueCodes.RouteAmbiguous,
                    "The trusted Standard exact map has no unique published route.")];
                return true;
            }
            exact.Add((capacity, matches[0]));
        }
        if (_catalog.TryGetCurrentSnapshot()?.ResolutionToken != snapshot!.ResolutionToken)
        {
            issues = [new CompositionIssue(AuthoringSessionIssueCodes.StalePublication,
                "The Standard source-envelope publication changed during route selection.")];
            return true;
        }
        if (exact.Count == 0 || mappedRoutes.Count != published.Length ||
            !mappedRoutes.Contains(templates[0]))
        {
            issues = [new CompositionIssue(CapabilityCatalogIssueCodes.RouteUnavailable,
                "The Standard source-envelope route set is incomplete.")];
            return true;
        }
        selectedRoutes = new(snapshot!, templates[0], exact);
        return true;
    }

    private static bool HasSelectedExactMap(
        CompiledComposition composition, ResolvedCapabilityRoute route, long capacity)
    {
        return composition.V2Details.Provenance.Context is
            ResolvedMapV2CompilationContext { SourceEnvelope: null } context &&
            context.ResolvedMap.CapacityBytes == capacity &&
            composition.Plan.OutputInitialization.Capacity == capacity &&
            StringComparer.Ordinal.Equals(
                context.ResolvedMap.ImageMap.MapId, route.Identity.MapVariant);
    }

    private bool TryGetBuiltInV2StandardMergeCompilation(
        string icId,
        long? dpInputLength,
        out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryGetBuiltInV2StandardMergeCompilation(
            icId,
            dpInputLength,
            out composition,
            out _,
            out issues);
    }

    private bool TryGetBuiltInV2StandardMergeCompilation(
        string icId,
        long? dpInputLength,
        out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryGetBuiltInV2StandardMergeCompilation(
            icId,
            dpInputLength,
            selectedInputSlotIds: null,
            out composition,
            out resolvedCapability,
            out issues);
    }

    private bool TryGetBuiltInV2StandardMergeCompilation(
        string icId,
        long? dpInputLength,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryCompileSourceEnvelopeExactLength(
                icId,
                dpInputLength,
                selectedInputSlotIds,
                out composition,
                out resolvedCapability,
                out issues) ||
            TryCompilePublishedDynamicCapability(
                icId,
                ExperienceIds.StandardMerge,
                "selector-free",
                dpInputLength,
                selectedInputSlotIds,
                out composition,
                out resolvedCapability,
                out issues) ||
            TryCompilePublishedStandardMergeCapability(
                icId,
                dpInputLength,
                out composition,
                out resolvedCapability,
                out issues);
    }

    private bool TryCompileSourceEnvelopeExactLength(
        string icId,
        long? dpInputLength,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        composition = null;
        resolvedCapability = null;
        issues = [];
        bool declared = TryGetStandardSourceEnvelopeRoutes(
            icId, out StandardSourceEnvelopeRoutes? routes, out issues);
        if (issues.Count != 0)
        {
            return true;
        }
        if (!declared || dpInputLength is null)
        {
            return declared;
        }

        ResolvedCapabilityRoute? route = routes!.Select(dpInputLength.Value, captured: false);
        if (route is null)
        {
            return true;
        }
        if (route.Authoring.Value == CapabilityAuthoringAvailability.Unavailable)
        {
            issues = [new CompositionIssue(CapabilityCatalogIssueCodes.AuthoringUnavailable,
                "The selected Standard route is unavailable for authoring.")];
            return true;
        }

        CompileAndBindDynamicRoute(route, dpInputLength, selectedInputSlotIds,
            out composition, out resolvedCapability, out issues);
        if (!IsCurrentPublishedRoute(routes.Snapshot, route))
        {
            composition = null;
            resolvedCapability = null;
            issues = [new CompositionIssue(AuthoringSessionIssueCodes.StalePublication,
                "The canonical Standard publication changed during exact-capacity compilation.")];
            return true;
        }

        if (composition is not null && issues.Count == 0 &&
            (resolvedCapability is null ||
             !HasSelectedExactMap(composition, route, dpInputLength.Value)))
        {
            composition = null;
            resolvedCapability = null;
            issues = [new CompositionIssue(CapabilityCatalogIssueCodes.RouteUnavailable,
                "The selected Standard compilation did not retain its published exact map.")];
        }

        return true;
    }

    private bool TryGetBuiltInV2StandardMergeCompilation(
        string icId,
        ReadOnlyMemory<byte> capturedDp,
        IReadOnlyCollection<string> selectedInputSlotIds,
        out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        bool declared = TryGetStandardSourceEnvelopeRoutes(
            icId, out StandardSourceEnvelopeRoutes? routes, out issues);
        if (!declared)
        {
            if (issues.Count != 0)
            {
                composition = null;
                resolvedCapability = null;
                return true;
            }

            // Only profiles without a source-envelope declaration retain the legacy exact path.
            return TryGetBuiltInV2StandardMergeCompilation(
                icId, capturedDp.Length, selectedInputSlotIds,
                out composition, out resolvedCapability, out issues);
        }

        if (issues.Count != 0)
        {
            composition = null;
            resolvedCapability = null;
            return true;
        }

        ResolvedCapabilityRoute route = routes!.Select(capturedDp.Length, captured: true)!;
        if (route.Authoring.Value == CapabilityAuthoringAvailability.Unavailable)
        {
            composition = null;
            resolvedCapability = null;
            issues = [new CompositionIssue(CapabilityCatalogIssueCodes.AuthoringUnavailable,
                "The selected Standard route is unavailable for authoring.")];
            return true;
        }
        return CompileAndBindCapturedDynamicRoute(
            routes.Snapshot, route, capturedDp.Length,
            [new FirmwareArtifactPayload(CompositionAddressSpaceIds.DpInput, capturedDp.Span)],
            selectedInputSlotIds, out composition, out resolvedCapability, out issues);
    }
}

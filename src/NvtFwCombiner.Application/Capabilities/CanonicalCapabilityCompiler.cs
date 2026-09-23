using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>
/// Resolves and binds canonical capabilities through the current publication.
/// Profile compilation is delegated to one injected canonical adapter.
/// </summary>
internal sealed partial class CanonicalCapabilityCompilerAdapter :
    IStandardMergeCompilationPort
{
    private const string SelectorFreeIcCountVariant = "selector-free";
    private readonly ICanonicalCapabilityQuery _catalog;
    private readonly ICanonicalDynamicCompilationAdapter _dynamicCompiler;

    internal CanonicalCapabilityCompilerAdapter(
        ICanonicalCapabilityQuery catalog,
        ICanonicalDynamicCompilationAdapter dynamicCompiler)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _dynamicCompiler = dynamicCompiler ??
            throw new ArgumentNullException(nameof(dynamicCompiler));
    }

    internal bool TryCompilePublishedStandardMergeCapability(
        string icId,
        long? outputCapacity,
        out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        resolvedCapability = null;
        CapabilityResolutionResult resolution = _catalog.ResolveUniqueRoute(
                icId,
                ExperienceIds.StandardMerge,
                SelectorFreeIcCountVariant,
                outputCapacity);
        if (outputCapacity is null &&
            StringComparer.Ordinal.Equals(
                resolution.Issue?.Code,
                CapabilityCatalogIssueCodes.RouteAmbiguous) &&
            GetCanonicalOutputCapacities(
                icId,
                ExperienceIds.StandardMerge).Length > 1)
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
                ExperienceIds.StandardMerge);
            if (outputCapacity is not null && capacities.Length != 0)
            {
                composition = null;
                issues =
                [
                    new CompositionIssue(
                        CompositionPlanningIssueCodes.StandardMergeDpLengthUnsupported,
                        $"Selected DP BIN length 0x{outputCapacity.Value:X} is unsupported; {icId} Standard Merge profile accepts DP input lengths {FormatCapacities(capacities)}."),
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

    internal bool TryCompilePublishedDynamicCapability(
        string icId,
        string workflowId,
        string icCountVariant,
        long? requestedMapCapacity,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues,
        TopologySelection? requestedTopology = null)
    {
        resolvedCapability = null;
        string normalizedIcId = IcIdentifier.Normalize(icId);
        ResolvedCapabilityRoute[] matches = [.. (_catalog
            .TryGetCurrentSnapshot()?.DynamicRoutes ?? [])
                .Where(route =>
                    StringComparer.Ordinal.Equals(
                        route.Identity.IcId,
                        normalizedIcId) &&
                    StringComparer.Ordinal.Equals(
                        route.Identity.WorkflowId,
                        workflowId) &&
                    StringComparer.Ordinal.Equals(
                        route.Identity.IcCountVariant,
                        icCountVariant))];
        if (matches.Length != 1)
        {
            composition = null;
            issues = [new CompositionIssue(matches.Length == 0
                ? CapabilityCatalogIssueCodes.RouteUnavailable
                : CapabilityCatalogIssueCodes.RouteAmbiguous,
                "Dynamic compilation requires one exact published route.")];
            return false;
        }

        return TryCompilePublishedDynamicCapability(matches[0].Identity,
            requestedMapCapacity, selectedInputSlotIds, out composition,
            out resolvedCapability, out issues, requestedTopology);
    }

    internal bool TryCompilePublishedDynamicCapability(
        CapabilityRouteIdentity identity,
        long? requestedMapCapacity,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues,
        TopologySelection? requestedTopology = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        resolvedCapability = null;
        CapabilityRouteResolutionResult resolution = _catalog.ResolveDynamicRoute(
                identity.RouteId);
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

        CompileAndBindDynamicRoute(
            resolution.Route!,
            requestedMapCapacity,
            selectedInputSlotIds,
            out composition,
            out resolvedCapability,
            out issues,
            requestedTopology);
        return true;
    }

    /// <summary>Compiles one published route from an immutable captured source without a length-only fallback.</summary>
    internal bool TryCompilePublishedDynamicCapability(
        CapabilityRouteIdentity identity,
        long requestedMapCapacity,
        IReadOnlyList<FirmwareArtifactPayload> capturedArtifacts,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(capturedArtifacts);
        CanonicalCapabilityCatalogSnapshot? snapshot = _catalog.TryGetCurrentSnapshot();
        ResolvedCapabilityRoute? route = snapshot?.DynamicRoutes.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.Identity.RouteId, identity.RouteId));
        if (route is null)
        {
            composition = null;
            resolvedCapability = null;
            issues = [new CompositionIssue(
                CapabilityCatalogIssueCodes.RouteUnavailable,
                "Captured compilation requires one exact published dynamic route.")];
            return false;
        }

        return CompileAndBindCapturedDynamicRoute(
            snapshot!, route, requestedMapCapacity, capturedArtifacts,
            selectedInputSlotIds, out composition, out resolvedCapability, out issues);
    }

    private bool CompileAndBindCapturedDynamicRoute(
        CanonicalCapabilityCatalogSnapshot snapshot,
        ResolvedCapabilityRoute route,
        long requestedMapCapacity,
        IReadOnlyList<FirmwareArtifactPayload> capturedArtifacts,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        if (!IsCurrentPublishedRoute(snapshot, route))
        {
            return StaleCapturedCompilation(out composition, out resolvedCapability, out issues);
        }

        _dynamicCompiler.Compile(
            route.Identity,
            requestedMapCapacity,
            capturedArtifacts,
            selectedInputSlotIds,
            out CompiledComposition? compiled,
            out MetadataPlanDefinition? metadataPlan,
            out issues);
        if (!IsCurrentPublishedRoute(snapshot, route))
        {
            return StaleCapturedCompilation(out composition, out resolvedCapability, out issues);
        }
        if (compiled is null || issues.Count != 0)
        {
            composition = null;
            resolvedCapability = null;
            return true;
        }

        ResolvedCapability bound = route.BindCompilation(
            compiled,
            metadataPlan ?? throw new InvalidOperationException(
                "Canonical captured compilation omitted its metadata plan."));
        if (!IsCurrentPublishedRoute(snapshot, route))
        {
            return StaleCapturedCompilation(out composition, out resolvedCapability, out issues);
        }

        resolvedCapability = bound;
        composition = bound.CompiledComposition;
        return true;
    }

    private bool IsCurrentPublishedRoute(
        CanonicalCapabilityCatalogSnapshot snapshot,
        ResolvedCapabilityRoute route)
    {
        CanonicalCapabilityCatalogSnapshot? current = _catalog.TryGetCurrentSnapshot();
        return current?.ResolutionToken == snapshot.ResolutionToken &&
            current.DynamicRoutes.Any(candidate => ReferenceEquals(candidate, route));
    }

    private static bool StaleCapturedCompilation(
        out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        composition = null;
        resolvedCapability = null;
        issues = [new CompositionIssue(
            AuthoringSessionIssueCodes.StalePublication,
            "The canonical publication changed during captured compilation.")];
        return true;
    }

    private long[] GetCanonicalOutputCapacities(
        string icId,
        string workflowId)
    {
        string normalizedIcId = IcIdentifier.Normalize(icId);
        IReadOnlyList<ResolvedCapability> capabilities =
            _catalog.TryGetCurrentSnapshot()?.Capabilities ?? [];
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

    internal IReadOnlyList<long> GetDynamicMapCapacities(
        string icId,
        string workflowId,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return _dynamicCompiler.GetMapCapacities(
            IcIdentifier.Normalize(icId),
            workflowId,
            out issues);
    }

    internal IReadOnlyList<string> GetPublishedDynamicSelectionGroupMemberSlotIds(
        string icId,
        string workflowId,
        string icCountVariant)
    {
        string normalizedIcId = IcIdentifier.Normalize(icId);
        CanonicalCapabilityCatalogSnapshot? snapshot = _catalog.TryGetCurrentSnapshot();
        ResolvedCapabilityRoute[] routes =
            snapshot is null ? [] :
            [
                .. snapshot.DynamicRoutes.Where(candidate =>
                StringComparer.Ordinal.Equals(candidate.Identity.IcId, normalizedIcId) &&
                StringComparer.Ordinal.Equals(candidate.Identity.WorkflowId, workflowId) &&
                StringComparer.Ordinal.Equals(
                    candidate.Identity.IcCountVariant,
                    icCountVariant)),
            ];
        if (routes.Length == 0)
        {
            return [];
        }

        IReadOnlyList<string> memberSlotIds = routes[0].CompilationContract.SemanticBindingIds;
        return routes.Length == 1
            ? StringComparer.Ordinal.Equals(
                    routes[0].CompilationContract.CompilerSemanticId,
                    CapabilityDefinitionFingerprint.MapBoundCompilerSemanticId)
                ? memberSlotIds
                : []
            : routes.Any(route =>
                !StringComparer.Ordinal.Equals(
                    route.CompilationContract.CompilerSemanticId,
                    CapabilityDefinitionFingerprint.MapBoundCompilerSemanticId) ||
                !route.CompilationContract.SemanticBindingIds.SequenceEqual(
                    memberSlotIds, StringComparer.Ordinal))
            ? throw new InvalidOperationException(
                $"Published input-slot declarations disagree for '{normalizedIcId}'/" +
                $"'{workflowId}'/'{icCountVariant}' across routes: " +
                string.Join(", ", routes.Select(static route => route.Identity.RouteId)))
            : memberSlotIds;
    }

    /// <summary>
    /// Compiles and binds one read-only classification candidate from an exact
    /// current publication without applying authoring availability.
    /// </summary>
    internal bool TryCompilePublishedClassificationCandidate(
        ResolvedCapabilityRoute publishedRoute,
        IReadOnlyCollection<string> selectedInputSlotIds,
        out ResolvedCapability? capability)
    {
        return TryCompileClassificationCandidate(
            publishedRoute, requestedMapCapacity: null, selectedInputSlotIds,
            exactSourceEnvelopeRoute: false, out capability);
    }

    internal bool TryCompilePublishedClassificationCandidate(
        ResolvedCapabilityRoute publishedRoute,
        long exactMapCapacity,
        IReadOnlyCollection<string> selectedInputSlotIds,
        out ResolvedCapability? capability)
    {
        return TryCompileClassificationCandidate(
            publishedRoute, exactMapCapacity, selectedInputSlotIds,
            exactSourceEnvelopeRoute: true, out capability);
    }

    private bool TryCompileClassificationCandidate(
        ResolvedCapabilityRoute publishedRoute,
        long? requestedMapCapacity,
        IReadOnlyCollection<string> selectedInputSlotIds,
        bool exactSourceEnvelopeRoute,
        out ResolvedCapability? capability)
    {
        ArgumentNullException.ThrowIfNull(publishedRoute);
        ArgumentNullException.ThrowIfNull(selectedInputSlotIds);
        capability = null;
        CanonicalCapabilityCatalogSnapshot? snapshot = _catalog.TryGetCurrentSnapshot();
        if (snapshot is null ||
            !snapshot.DynamicRoutes.Any(route => ReferenceEquals(route, publishedRoute)) ||
            !StringComparer.Ordinal.Equals(
                publishedRoute.CompilationContract.CompilerSemanticId,
                CapabilityDefinitionFingerprint.MapBoundCompilerSemanticId))
        {
            return false;
        }

        try
        {
            if (exactSourceEnvelopeRoute)
            {
                if (requestedMapCapacity is null or <= 0 ||
                    publishedRoute.CompilationContract.AllowedMapVariantIds.Count != 1 ||
                    !StringComparer.Ordinal.Equals(
                        publishedRoute.CompilationContract.AllowedMapVariantIds[0],
                        publishedRoute.Identity.MapVariant))
                {
                    return false;
                }
            }
            else if (_dynamicCompiler.TryGetSourceEnvelopeMapVariant(
                    publishedRoute.Identity.IcId,
                    publishedRoute.Identity.WorkflowId,
                    sourceLength: null,
                    out _,
                    out IReadOnlyList<CompositionIssue> envelopeIssues) ||
                envelopeIssues.Count != 0)
            {
                // Source-envelope routes must arrive with the one selected route set.
                return false;
            }

            CompileAndBindDynamicRoute(
                publishedRoute,
                requestedMapCapacity,
                selectedInputSlotIds,
                out _,
                out ResolvedCapability? bound,
                out IReadOnlyList<CompositionIssue> issues);
            if (bound is null || issues.Count != 0)
            {
                return false;
            }

            if (exactSourceEnvelopeRoute &&
                !HasSelectedExactMap(bound.CompiledComposition,
                    publishedRoute, requestedMapCapacity!.Value))
            {
                return false;
            }

            CanonicalCapabilityCatalogSnapshot? current = _catalog.TryGetCurrentSnapshot();
            if (current?.ResolutionToken != snapshot.ResolutionToken ||
                !current.DynamicRoutes.Any(route => ReferenceEquals(route, publishedRoute)))
            {
                return false;
            }

            capability = bound;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private void CompileAndBindDynamicRoute(
        ResolvedCapabilityRoute route,
        long? requestedMapCapacity,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues,
        TopologySelection? requestedTopology = null)
    {
        _dynamicCompiler.Compile(
            route.Identity,
            requestedMapCapacity,
            selectedInputSlotIds,
            out CompiledComposition? compiled,
            out MetadataPlanDefinition? metadataPlan,
            out issues,
            requestedTopology);
        if (compiled is null || issues.Count != 0)
        {
            composition = null;
            resolvedCapability = null;
            return;
        }

        resolvedCapability = route.BindCompilation(
            compiled,
            metadataPlan ?? throw new InvalidOperationException(
                "Canonical dynamic compilation omitted its metadata plan."));
        composition = resolvedCapability.CompiledComposition;
    }

    private static string FormatCapacities(IEnumerable<long> capacities)
    {
        return string.Join(
            " / ",
            capacities.Select(static capacity => $"0x{capacity:X}"));
    }

}

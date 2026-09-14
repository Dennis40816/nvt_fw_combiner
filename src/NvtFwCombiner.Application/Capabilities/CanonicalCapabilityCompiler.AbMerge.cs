using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Capabilities;

internal sealed partial class CanonicalCapabilityCompilerAdapter
{
    internal bool TryGetAbAuthoringDefinition(
        ResolvedCapabilityRoute requestedRoute,
        [NotNullWhen(true)] out CanonicalAbAuthoringDefinition? definition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(requestedRoute);
        definition = null;
        CapabilityRouteResolutionResult current = _catalog.ResolveDynamicRoute(requestedRoute.Identity.RouteId);
        if (!current.Succeeded || requestedRoute.Identity.WorkflowId != ExperienceIds.AbMerge ||
            current.Route!.ResolutionToken != requestedRoute.ResolutionToken ||
            current.Route.CapabilityFingerprint != requestedRoute.CapabilityFingerprint)
        {
            issues = [new CompositionIssue(current.Issue?.Code ?? CapabilityCatalogIssueCodes.RouteUnavailable,
                current.Issue?.Message ?? "AB declarations require the current published route, not a stale or foreign route.")];
            return false;
        }

        if (!_dynamicCompiler.TryGetAbAuthoringDefinition(current.Route.Identity,
                out CanonicalAbAuthoringDefinition? candidate, out issues) || candidate is null || issues.Count != 0)
        {
            if (issues.Count == 0)
            {
                issues = [new CompositionIssue(CapabilityCatalogIssueCodes.RouteUnavailable, "The trusted AB declaration is unavailable.")];
            }
            return false;
        }

        if (!candidate.Matches(current.Route) ||
            _catalog.TryGetCurrentSnapshot()?.ResolutionToken != current.Route.ResolutionToken)
        {
            issues = [new CompositionIssue(CapabilityCatalogIssueCodes.RouteUnavailable,
                "The AB declaration does not match the current profile, family, input membership or publication.")];
            return false;
        }

        definition = candidate;
        return true;
    }

    internal bool TryCompileAbMerge(
        string icId,
        TopologySelection? requestedTopology,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryCompileAbMergeCapability(icId, requestedTopology, null,
            out composition, out _, out issues);
    }

    internal bool TryCompileAbMergeCapability(
        string icId,
        TopologySelection? requestedTopology,
        IReadOnlyCollection<string>? selectedInputSlotIds,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out ResolvedCapability? capability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        composition = null;
        capability = null;
        issues = [];
        string normalizedIcId = IcIdentifier.Normalize(icId);
        ResolvedCapabilityRoute[] routes = [.. _catalog.GetCurrentSnapshot().DynamicRoutes
            .Where(candidate => candidate.Identity.IcId == normalizedIcId &&
                candidate.Identity.WorkflowId == ExperienceIds.AbMerge &&
                (candidate.AbMergeTopologyChoice is { } choice
                    ? requestedTopology is not null &&
                        (choice.Selection.ChipCount == 1
                            ? requestedTopology.ChipCount == 1
                            : requestedTopology.ChipCount >= 2)
                    : requestedTopology is null))];
        if (routes.Length != 1)
        {
            issues = [new CompositionIssue(routes.Length == 0
                ? CapabilityCatalogIssueCodes.RouteUnavailable
                : CapabilityCatalogIssueCodes.RouteAmbiguous,
                "AB compilation requires one exact published format route for the selected IC and topology.")];
            return false;
        }

        _ = TryCompilePublishedDynamicCapability(routes[0].Identity,
            requestedMapCapacity: null, selectedInputSlotIds,
            out composition, out capability, out issues, requestedTopology);
        return composition is not null;
    }

    internal bool TryCompileAbMerge(
        string icId,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryCompileAbMerge(
            icId,
            requestedTopology: null,
            out composition,
            out issues);
    }

    internal IReadOnlyList<CapabilityTopologyChoice> GetAbMergeTopologyChoices(
        string icId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return _catalog.GetCurrentSnapshot()
            .SelectorPublication
            .GetAbMergeTopologyChoices(icId);
    }

    internal TopologySelection? ResolveAbMergeTopologySelection(
        string icId,
        string? token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        CapabilityTopologyChoice? choice = GetAbMergeTopologyChoices(icId)
            .SingleOrDefault(candidate => StringComparer.OrdinalIgnoreCase.Equals(
                candidate.Token,
                token.Trim()));
        return choice?.Selection ?? throw new ArgumentException(
            "The AB Merge topology token is not declared by the selected IC's compiled capability.",
            nameof(token));
    }
}

/// <summary>
/// Sole Application projection from accepted compiled AB topology to selector
/// choices. Both the selector publication and compiler disclosure consume it.
/// </summary>
internal static class AbMergeTopologyChoiceProjection
{
    internal static void ValidateDefinition(CapabilityRouteIdentity identity, CapabilityTopologyChoice? choice)
    {
        bool valid = identity.WorkflowId != ExperienceIds.AbMerge || identity.IcCountVariant == "selector-free"
            ? choice is null
            : identity.IcCountVariant switch
            {
                "1-ic" => choice is { Token: "single", Selection.ChipCount: 1 },
                "2-plus-ic" => choice is { Token: "cascade", Selection.ChipCount: 2 },
                _ => false,
            };
        if (!valid)
        {
            throw new ArgumentException("AB topology disclosure must match its declared route count axis.", nameof(choice));
        }
    }

    internal static void ValidateCompilation(CapabilityRouteIdentity identity, CompiledComposition composition)
    {
        if (identity.WorkflowId != ExperienceIds.AbMerge) { return; }
        TopologySelection? topology = composition.V2Details.Provenance.Context is MapBoundV2CompilationContext context
            ? context.ResolvedMap.TopologySelection
            : throw new ArgumentException("AB topology requires a map-bound compilation.", nameof(composition));
        bool valid = identity.IcCountVariant switch
        {
            "selector-free" => topology is null,
            "1-ic" => topology?.ChipCount == 1,
            "2-plus-ic" => topology?.ChipCount >= 2,
            _ => false,
        };
        if (!valid)
        {
            throw new ArgumentException("Compiled AB topology does not match its declared route count axis.", nameof(composition));
        }
    }

    internal static IReadOnlyList<CapabilityTopologyChoice> Project(
        IReadOnlyList<ResolvedCapability> capabilities,
        string icId,
        IReadOnlyList<ResolvedCapabilityRoute>? dynamicRoutes = null)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        string normalizedIcId = IcIdentifier.Normalize(icId);
        CapabilityTopologyChoice[] choices =
        [
            .. capabilities
                .Where(capability =>
                    capability.Authoring.Value ==
                        CapabilityAuthoringAvailability.Available &&
                    StringComparer.Ordinal.Equals(
                        capability.Identity.IcId,
                        normalizedIcId) &&
                    StringComparer.Ordinal.Equals(
                        capability.Identity.WorkflowId,
                        ExperienceIds.AbMerge))
                .Select(CapabilityPublicationCoherence
                    .GetAcceptedAbMergeTopologySelection)
                .Where(static topology => topology is not null)
                .Select(static topology => topology!)
                .Concat((dynamicRoutes ?? [])
                    .Where(route => route.Authoring.Value == CapabilityAuthoringAvailability.Available &&
                        route.Identity.WorkflowId == ExperienceIds.AbMerge &&
                        StringComparer.Ordinal.Equals(route.Identity.IcId, normalizedIcId) &&
                        route.AbMergeTopologyChoice is not null)
                    .Select(static route => route.AbMergeTopologyChoice!.Selection))
                .GroupBy(static topology => topology.ChipCount == 1
                    ? TopologyRequirement.RequireSingleChip().CanonicalId
                    : TopologyRequirement.RequireCascade().CanonicalId,
                    StringComparer.Ordinal)
                .Select(static group => new CapabilityTopologyChoice(
                    group.Key,
                    group.OrderBy(static topology => topology.ChipCount).First()))
                .OrderBy(static choice => choice.Selection.ChipCount)
                .ThenBy(static choice => choice.Token, StringComparer.Ordinal),
        ];
        return Array.AsReadOnly(choices);
    }
}

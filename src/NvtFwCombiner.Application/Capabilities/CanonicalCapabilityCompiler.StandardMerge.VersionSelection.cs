// Copyright (c) 2026 Dennis Liu. All rights reserved.
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Capabilities;

internal sealed partial class CanonicalCapabilityCompilerAdapter
{
    internal CanonicalMapSelectionPrerequisite? GetStandardMapSelectionPrerequisite(string icId)
    {
        return _dynamicCompiler.GetMapSelectionPrerequisite(IcIdentifier.Normalize(icId), ExperienceIds.StandardMerge);
    }

    internal bool TryGetPublishedStandardVersionRoute(string icId, out ResolvedCapabilityRoute? route,
        out CanonicalMapSelectionPrerequisite? prerequisite, out IReadOnlyList<CompositionIssue> issues)
    {
        prerequisite = GetStandardMapSelectionPrerequisite(icId);
        route = null;
        issues = [];
        if (prerequisite is null) { return false; }
        ResolvedCapabilityRoute[] routes = [.. (_catalog.TryGetCurrentSnapshot()?.DynamicRoutes ?? [])
            .Where(candidate => candidate.Identity.IcId == IcIdentifier.Normalize(icId) &&
                candidate.Identity.WorkflowId == ExperienceIds.StandardMerge &&
                candidate.Identity.IcCountVariant == SelectorFreeIcCountVariant)];
        if (routes.Length == 1) { route = routes[0]; }
        else
        {
            issues = [new CompositionIssue(CapabilityCatalogIssueCodes.RouteUnavailable,
            "Version selection requires one published Standard map-set route.")];
        }
        return true;
    }

    private bool TryCompileStandardVersionSelection(string icId, ReadOnlyMemory<byte> captured,
        CanonicalMapSelectionPrerequisite prerequisite, IReadOnlyCollection<string> slots,
        out CompiledComposition? composition, out ResolvedCapability? capability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        composition = null;
        capability = null;
        _ = TryGetPublishedStandardVersionRoute(icId, out ResolvedCapabilityRoute? route, out _, out issues);
        if (route is null || issues.Count != 0) { return false; }
        if (route.Authoring.Value != CapabilityAuthoringAvailability.Available)
        {
            issues = [new CompositionIssue(CapabilityCatalogIssueCodes.AuthoringUnavailable,
                "The selected Standard route is unavailable for authoring.")];
            return false;
        }
        CanonicalCapabilityCatalogSnapshot? snapshot = _catalog.TryGetCurrentSnapshot();
        if (snapshot is null || !snapshot.DynamicRoutes.Any(candidate => ReferenceEquals(candidate, route)))
        {
            issues = [new CompositionIssue(AuthoringSessionIssueCodes.StalePublication,
                "The canonical Standard publication changed during version selection.")];
            return false;
        }
        return CompileAndBindCapturedDynamicRoute(snapshot, route,
            prerequisite.MapCapacity, [new FirmwareArtifactPayload(prerequisite.SlotId, captured.Span)],
            slots, out composition, out capability, out issues);
    }
}

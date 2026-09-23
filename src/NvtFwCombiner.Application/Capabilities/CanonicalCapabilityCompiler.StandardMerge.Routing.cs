using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Capabilities;

internal sealed partial class CanonicalCapabilityCompilerAdapter
{
    internal bool TryGetPublishedStandardSourceEnvelopeRoute(
        string icId,
        out ResolvedCapabilityRoute? route,
        out IReadOnlyList<CompositionIssue> issues)
    {
        bool declared = _dynamicCompiler.TryGetSourceEnvelopeMapVariant(
            IcIdentifier.Normalize(icId), ExperienceIds.StandardMerge,
            sourceLength: null, out string? templateMapId, out issues);
        route = null;
        if (!declared || issues.Count != 0)
        {
            return declared;
        }

        ResolvedCapabilityRoute[] matches = [.. (_catalog.TryGetCurrentSnapshot()?.DynamicRoutes ?? [])
            .Where(candidate =>
                StringComparer.Ordinal.Equals(candidate.Identity.IcId, IcIdentifier.Normalize(icId)) &&
                StringComparer.Ordinal.Equals(candidate.Identity.WorkflowId, ExperienceIds.StandardMerge) &&
                StringComparer.Ordinal.Equals(candidate.Identity.IcCountVariant, "selector-free") &&
                StringComparer.Ordinal.Equals(candidate.Identity.MapVariant, templateMapId))];
        if (matches.Length != 1)
        {
            issues = [new CompositionIssue(matches.Length == 0
                ? CapabilityCatalogIssueCodes.RouteUnavailable
                : CapabilityCatalogIssueCodes.RouteAmbiguous,
                "The source-envelope template has no unique published route.")];
            return true;
        }

        route = matches[0];
        return true;
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
        return TryCompilePublishedDynamicCapability(
                icId,
                ExperienceIds.StandardMerge,
                "selector-free",
                dpInputLength,
                selectedInputSlotIds: null,
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

    private bool TryGetBuiltInV2StandardMergeCompilation(
        string icId,
        long? dpInputLength,
        IReadOnlyCollection<string> selectedInputSlotIds,
        out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryCompilePublishedDynamicCapability(
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

    private bool TryGetBuiltInV2StandardMergeCompilation(
        string icId,
        ReadOnlyMemory<byte> capturedDp,
        IReadOnlyCollection<string> selectedInputSlotIds,
        out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        bool hasEnvelope = _dynamicCompiler.TryGetSourceEnvelopeMapVariant(
            IcIdentifier.Normalize(icId), ExperienceIds.StandardMerge,
            capturedDp.Length, out string? mapVariant, out issues);
        if (!hasEnvelope)
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

        ResolvedCapabilityRoute[] routes = [.. (_catalog.TryGetCurrentSnapshot()?.DynamicRoutes ?? [])
            .Where(route =>
                StringComparer.Ordinal.Equals(route.Identity.IcId, IcIdentifier.Normalize(icId)) &&
                StringComparer.Ordinal.Equals(route.Identity.WorkflowId, ExperienceIds.StandardMerge) &&
                StringComparer.Ordinal.Equals(route.Identity.IcCountVariant, "selector-free") &&
                StringComparer.Ordinal.Equals(route.Identity.MapVariant, mapVariant))];
        if (routes.Length != 1)
        {
            composition = null;
            resolvedCapability = null;
            issues = [new CompositionIssue(routes.Length == 0
                ? CapabilityCatalogIssueCodes.RouteUnavailable
                : CapabilityCatalogIssueCodes.RouteAmbiguous,
                "The captured Standard source has no unique published exact or profile-declared template route.")];
            return true;
        }

        return TryCompilePublishedDynamicCapability(
            routes[0].Identity,
            capturedDp.Length,
            [new FirmwareArtifactPayload(CompositionAddressSpaceIds.DpInput, capturedDp.Span)],
            selectedInputSlotIds,
            out composition,
            out resolvedCapability,
            out issues);
    }
}

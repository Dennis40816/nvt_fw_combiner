using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Capabilities;

internal sealed partial class CanonicalCapabilityCompilerAdapter
{
    internal bool TryCompileAbMerge(
        string icId,
        TopologySelection? requestedTopology,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        composition = null;
        issues = [];
        CapabilityResolutionResult resolution = _catalog.ResolveUniqueTopologyRoute(
                IcIdentifier.Normalize(icId),
                ExperienceIds.AbMerge,
                requestedTopology);
        if (StringComparer.Ordinal.Equals(
                resolution.Issue?.Code,
                CapabilityCatalogIssueCodes.RouteUnavailable))
        {
            return false;
        }

        composition = resolution.Capability?.CompiledComposition;
        issues = resolution.Issue is null
            ? []
            : [new CompositionIssue(resolution.Issue.Code, resolution.Issue.Message)];
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
        string normalizedIcId = IcIdentifier.Normalize(icId);
        CanonicalCapabilityCatalogSnapshot snapshot = _catalog.GetCurrentSnapshot();
        CapabilityTopologyChoice[] choices =
        [
            .. snapshot.Capabilities
                .Where(capability =>
                    capability.Authoring.Value ==
                        CapabilityAuthoringAvailability.Available &&
                    StringComparer.Ordinal.Equals(
                        capability.Identity.IcId,
                        normalizedIcId) &&
                    StringComparer.Ordinal.Equals(
                        capability.Identity.WorkflowId,
                        ExperienceIds.AbMerge))
                .Select(static capability =>
                    capability.CompiledComposition.V2Details.Provenance.Context)
                .OfType<MapBoundV2CompilationContext>()
                .Select(static context => context.ResolvedMap.TopologySelection)
                .Where(static topology => topology is not null)
                .Select(static topology => topology!)
                .GroupBy(static topology => topology.ChipCount == 1
                    ? TopologyRequirement.RequireSingleChip().CanonicalId
                    : TopologyRequirement.RequireCascade().CanonicalId,
                    StringComparer.Ordinal)
                .Select(static group => new CapabilityTopologyChoice(
                    group.Key,
                    group.OrderBy(static topology => topology.ChipCount).First()))
                .OrderBy(static choice => choice.Selection.ChipCount),
        ];
        return Array.AsReadOnly(choices);
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

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
    internal static IReadOnlyList<CapabilityTopologyChoice> Project(
        IReadOnlyList<ResolvedCapability> capabilities,
        string icId)
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

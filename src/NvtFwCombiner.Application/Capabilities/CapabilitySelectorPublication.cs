using System.Collections.ObjectModel;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>
/// Immutable selector facts eagerly projected from one exact canonical catalog
/// publication. Clients never join these facts across catalog generations.
/// </summary>
public sealed class CapabilitySelectorPublication
{
    private readonly ReadOnlyCollection<string> _icIds;
    private readonly ReadOnlyCollection<string> _abMergeIcIds;
    private readonly ReadOnlyDictionary<string, ReadOnlyCollection<string>>
        _authorableWorkflowIdsByIc;
    private readonly ReadOnlyDictionary<
        string,
        ReadOnlyCollection<CapabilityNumberChoice>> _numberChoicesByIc;
    private readonly ReadOnlyDictionary<
        string,
        ReadOnlyCollection<CapabilityNumberChoice>> _numberChoicesByWorkflowAndIc;
    private readonly ReadOnlyDictionary<
        string,
        ReadOnlyCollection<CapabilityTopologyChoice>> _abTopologyChoicesByIc;

    private CapabilitySelectorPublication(
        ResolutionToken resolutionToken,
        string? defaultIcId,
        IEnumerable<string> icIds,
        IEnumerable<string> abMergeIcIds,
        IReadOnlyDictionary<string, IReadOnlyList<string>>
            authorableWorkflowIdsByIc,
        IReadOnlyDictionary<string, IReadOnlyList<CapabilityNumberChoice>>
            numberChoicesByIc,
        IReadOnlyDictionary<string, IReadOnlyList<CapabilityNumberChoice>>
            numberChoicesByWorkflowAndIc,
        IReadOnlyDictionary<string, IReadOnlyList<CapabilityTopologyChoice>>
            abTopologyChoicesByIc)
    {
        resolutionToken.EnsureValid(nameof(resolutionToken));
        ArgumentNullException.ThrowIfNull(icIds);
        ArgumentNullException.ThrowIfNull(abMergeIcIds);
        ArgumentNullException.ThrowIfNull(authorableWorkflowIdsByIc);
        ArgumentNullException.ThrowIfNull(numberChoicesByIc);
        ArgumentNullException.ThrowIfNull(numberChoicesByWorkflowAndIc);
        ArgumentNullException.ThrowIfNull(abTopologyChoicesByIc);

        ResolutionToken = resolutionToken;
        DefaultIcId = defaultIcId is null
            ? null
            : IcIdentifier.Normalize(defaultIcId);
        _icIds = Array.AsReadOnly(
        [
            .. icIds
                .Select(IcIdentifier.Normalize)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ]);
        _abMergeIcIds = Array.AsReadOnly(
        [
            .. abMergeIcIds
                .Select(IcIdentifier.Normalize)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ]);
        _authorableWorkflowIdsByIc = CreateReadOnlyMap(
            authorableWorkflowIdsByIc,
            static values =>
            [
                .. values
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal),
            ]);
        _numberChoicesByIc = CreateReadOnlyMap(
            numberChoicesByIc,
            static values => [.. values]);
        _numberChoicesByWorkflowAndIc = CreateReadOnlyWorkflowMap(
            numberChoicesByWorkflowAndIc);
        _abTopologyChoicesByIc = CreateReadOnlyMap(
            abTopologyChoicesByIc,
            static values =>
            [
                .. values
                    .OrderBy(static choice => choice.Selection.ChipCount)
                    .ThenBy(static choice => choice.Token, StringComparer.Ordinal),
            ]);
    }

    /// <summary>Exact catalog publication identity shared by every selector fact.</summary>
    public ResolutionToken ResolutionToken { get; }

    /// <summary>
    /// Deterministic initial IC, or null when the valid publication has no
    /// authorable route.
    /// </summary>
    public string? DefaultIcId { get; }

    /// <summary>Globally authorable IC identifiers in stable order.</summary>
    public IReadOnlyList<string> IcIds => _icIds;

    /// <summary>IC identifiers with an authorable AB Merge route.</summary>
    public IReadOnlyList<string> AbMergeIcIds => _abMergeIcIds;

    /// <summary>Gets profile-owned IC-number choices from this publication.</summary>
    public IReadOnlyList<CapabilityNumberChoice> GetNumberSelectionChoices(
        string icId)
    {
        return GetValues(_numberChoicesByIc, icId);
    }

    /// <summary>Gets workflow-scoped IC-number choices from this publication.</summary>
    public IReadOnlyList<CapabilityNumberChoice> GetNumberSelectionChoices(
        string icId,
        string workflowId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        return _numberChoicesByWorkflowAndIc.TryGetValue(
            CreateWorkflowKey(icId, workflowId),
            out ReadOnlyCollection<CapabilityNumberChoice>? values)
                ? values
                : Array.AsReadOnly(Array.Empty<CapabilityNumberChoice>());
    }

    /// <summary>Gets compiler-derived AB topology choices from this publication.</summary>
    public IReadOnlyList<CapabilityTopologyChoice> GetAbMergeTopologyChoices(
        string icId)
    {
        return GetValues(_abTopologyChoicesByIc, icId);
    }

    /// <summary>Gets authorable workflows for one IC in stable order.</summary>
    public IReadOnlyList<string> GetAuthorableWorkflowIds(string icId)
    {
        return GetValues(_authorableWorkflowIdsByIc, icId);
    }

    /// <summary>Returns whether this exact publication admits one workflow.</summary>
    public bool IsWorkflowAuthorable(string icId, string workflowId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        return GetAuthorableWorkflowIds(icId).Contains(
            workflowId,
            StringComparer.Ordinal);
    }

    internal static CapabilitySelectorPublication Create(
        ResolutionToken resolutionToken,
        IReadOnlyList<ResolvedCapability> capabilities,
        IReadOnlyList<ResolvedCapabilityRoute> dynamicRoutes,
        CanonicalCapabilityDisclosure disclosure)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(dynamicRoutes);
        ArgumentNullException.ThrowIfNull(disclosure);

        CapabilityRouteIdentity[] authorableIdentities =
        [
            .. capabilities
                .Where(static capability => IsAuthorable(capability.Authoring))
                .Select(static capability => capability.Identity)
                .Concat(dynamicRoutes
                    .Where(static route => IsAuthorable(route.Authoring))
                    .Select(static route => route.Identity))
                .DistinctBy(static identity => identity.RouteId, StringComparer.Ordinal)
                .OrderBy(static identity => identity.RouteId, StringComparer.Ordinal),
        ];
        string[] icIds =
        [
            .. authorableIdentities
                .Select(static identity => identity.IcId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        string? defaultIcId = authorableIdentities
            .GroupBy(static identity => identity.IcId, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenByDescending(static group => group
                .Select(static identity => identity.WorkflowId)
                .Distinct(StringComparer.Ordinal)
                .Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => group.Key)
            .FirstOrDefault();
        Dictionary<string, IReadOnlyList<string>> workflowsByIc = icIds
            .ToDictionary(
                static icId => icId,
                icId => (IReadOnlyList<string>)Array.AsReadOnly(
                [
                    .. authorableIdentities
                        .Where(identity => StringComparer.Ordinal.Equals(
                            identity.IcId,
                            icId))
                        .Select(static identity => identity.WorkflowId)
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal),
                ]),
                StringComparer.Ordinal);
        string[] abMergeIcIds =
        [
            .. icIds.Where(icId => workflowsByIc[icId].Contains(
                ExperienceIds.AbMerge,
                StringComparer.Ordinal)),
        ];
        Dictionary<string, IReadOnlyList<CapabilityNumberChoice>> numberChoices =
            icIds.ToDictionary(
                static icId => icId,
                disclosure.GetNumberChoices,
                StringComparer.Ordinal);
        var workflowNumberChoices = new Dictionary<
            string,
            IReadOnlyList<CapabilityNumberChoice>>(StringComparer.Ordinal);
        foreach (string icId in icIds)
        {
            if (workflowsByIc[icId].Contains(
                    ExperienceIds.CtrlRamReplace,
                    StringComparer.Ordinal))
            {
                workflowNumberChoices.Add(
                    CreateWorkflowKey(icId, ExperienceIds.CtrlRamReplace),
                    numberChoices[icId]);
            }

            const string workflowId = ExperienceIds.GeneralReplace;
            if (workflowsByIc[icId].Contains(workflowId, StringComparer.Ordinal))
            {
                workflowNumberChoices.Add(
                    CreateWorkflowKey(icId, workflowId),
                    ProjectWorkflowNumberChoices(
                        dynamicRoutes,
                        icId,
                        workflowId));
            }
        }
        Dictionary<string, IReadOnlyList<CapabilityTopologyChoice>> topologyChoices =
            abMergeIcIds.ToDictionary(
                static icId => icId,
                icId => AbMergeTopologyChoiceProjection.Project(
                    capabilities,
                    icId),
                StringComparer.Ordinal);

        return new CapabilitySelectorPublication(
            resolutionToken,
            defaultIcId,
            icIds,
            abMergeIcIds,
            workflowsByIc,
            numberChoices,
            workflowNumberChoices,
            topologyChoices);
    }

    private static bool IsAuthorable(
        PinnedCapabilityDecision<CapabilityAuthoringAvailability> decision)
    {
        return decision.Value == CapabilityAuthoringAvailability.Available;
    }

    private static ReadOnlyCollection<TValue> GetValues<TValue>(
        IReadOnlyDictionary<string, ReadOnlyCollection<TValue>> valuesByIc,
        string icId)
    {
        string normalizedIcId = IcIdentifier.Normalize(icId);
        return valuesByIc.TryGetValue(
            normalizedIcId,
            out ReadOnlyCollection<TValue>? values)
            ? values
            : Array.AsReadOnly(Array.Empty<TValue>());
    }

    private static ReadOnlyDictionary<string, ReadOnlyCollection<TValue>>
        CreateReadOnlyMap<TValue>(
            IReadOnlyDictionary<string, IReadOnlyList<TValue>> source,
            Func<IReadOnlyList<TValue>, TValue[]> copyValues)
    {
        var copy = new Dictionary<string, ReadOnlyCollection<TValue>>(
            StringComparer.Ordinal);
        foreach ((string key, IReadOnlyList<TValue> values) in source
                     .OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            copy.Add(
                IcIdentifier.Normalize(key),
                Array.AsReadOnly(copyValues(values)));
        }

        return new ReadOnlyDictionary<string, ReadOnlyCollection<TValue>>(copy);
    }

    private static ReadOnlyDictionary<string, ReadOnlyCollection<CapabilityNumberChoice>>
        CreateReadOnlyWorkflowMap(
            IReadOnlyDictionary<string, IReadOnlyList<CapabilityNumberChoice>> source)
    {
        return new ReadOnlyDictionary<string, ReadOnlyCollection<CapabilityNumberChoice>>(
            source.ToDictionary(
                static pair => pair.Key,
                static pair => Array.AsReadOnly(pair.Value.ToArray()),
                StringComparer.Ordinal));
    }

    private static ReadOnlyCollection<CapabilityNumberChoice> ProjectWorkflowNumberChoices(
        IReadOnlyList<ResolvedCapabilityRoute> dynamicRoutes,
        string icId,
        string workflowId)
    {
        CapabilityNumberChoice[] choices =
        [
            .. dynamicRoutes
                .Where(route =>
                    StringComparer.Ordinal.Equals(route.Identity.IcId, icId) &&
                    StringComparer.Ordinal.Equals(route.Identity.WorkflowId, workflowId) &&
                    IsAuthorable(route.Authoring))
                .Select(static route => route.NumberChoice)
                .Where(static choice => choice is not null)
                .Select(static choice => choice!)
                .DistinctBy(static choice => choice.Token, StringComparer.Ordinal)
                .OrderBy(static choice => choice.Token, StringComparer.Ordinal),
        ];
        return Array.AsReadOnly(choices);
    }

    private static string CreateWorkflowKey(string icId, string workflowId)
    {
        return $"{IcIdentifier.Normalize(icId)}\n{workflowId}";
    }
}

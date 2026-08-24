using System.Collections.ObjectModel;
using NvtFwCombiner.Domain.Composition;

#pragma warning disable CS1591 // Infrastructure adapter contracts are not end-user API.

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>
/// Immutable client-disclosure facts materialized with one trusted catalog
/// candidate. They are published atomically with the routes and are never
/// recompiled by UI or CLI queries.
/// </summary>
public sealed class CanonicalCapabilityDisclosure
{
    private readonly ReadOnlyDictionary<string, CapabilityProfileSummary[]>
        _profileSummariesByWorkflow;
    private readonly ReadOnlyDictionary<string, CapabilityNumberChoice[]>
        _numberChoicesByIc;
    private readonly ReadOnlyDictionary<string, long[]>
        _dpReferenceCapacitiesByIc;
    private readonly ReadOnlyDictionary<string, CapabilityFamilySummary>
        _familyByIc;
    private readonly HashSet<string> _dpPerspectiveIcs;

    public CanonicalCapabilityDisclosure(
        IReadOnlyDictionary<string, IReadOnlyList<CapabilityProfileSummary>>
            profileSummariesByWorkflow,
        IReadOnlyDictionary<string, IReadOnlyList<CapabilityNumberChoice>>
            numberChoicesByIc,
        IReadOnlyDictionary<string, IReadOnlyList<long>>
            dpReferenceCapacitiesByIc,
        IReadOnlyDictionary<string, CapabilityFamilySummary> familyByIc,
        IEnumerable<string> dpPerspectiveIcs)
    {
        ArgumentNullException.ThrowIfNull(profileSummariesByWorkflow);
        ArgumentNullException.ThrowIfNull(numberChoicesByIc);
        ArgumentNullException.ThrowIfNull(dpReferenceCapacitiesByIc);
        ArgumentNullException.ThrowIfNull(familyByIc);
        ArgumentNullException.ThrowIfNull(dpPerspectiveIcs);

        _profileSummariesByWorkflow = new ReadOnlyDictionary<string, CapabilityProfileSummary[]>(
            profileSummariesByWorkflow.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value
                    .OrderBy(static summary => summary.IcId, StringComparer.Ordinal)
                    .ThenBy(static summary => summary.ProfileId, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal));
        _numberChoicesByIc = new ReadOnlyDictionary<string, CapabilityNumberChoice[]>(
            numberChoicesByIc.ToDictionary(
                static pair => IcIdentifier.Normalize(pair.Key),
                static pair => pair.Value.ToArray(),
                StringComparer.Ordinal));
        _dpReferenceCapacitiesByIc = new ReadOnlyDictionary<string, long[]>(
            dpReferenceCapacitiesByIc.ToDictionary(
                static pair => IcIdentifier.Normalize(pair.Key),
                static pair => pair.Value.Distinct().Order().ToArray(),
                StringComparer.Ordinal));
        _familyByIc = new ReadOnlyDictionary<string, CapabilityFamilySummary>(
            familyByIc.ToDictionary(
                static pair => IcIdentifier.Normalize(pair.Key),
                static pair => pair.Value,
                StringComparer.Ordinal));
        _dpPerspectiveIcs = dpPerspectiveIcs
            .Select(IcIdentifier.Normalize)
            .ToHashSet(StringComparer.Ordinal);
    }

    internal static CanonicalCapabilityDisclosure Empty { get; } = new(
        new Dictionary<string, IReadOnlyList<CapabilityProfileSummary>>(
            StringComparer.Ordinal),
        new Dictionary<string, IReadOnlyList<CapabilityNumberChoice>>(
            StringComparer.Ordinal),
        new Dictionary<string, IReadOnlyList<long>>(StringComparer.Ordinal),
        new Dictionary<string, CapabilityFamilySummary>(StringComparer.Ordinal),
        []);

    internal IReadOnlyList<CapabilityProfileSummary> GetProfileSummaries(
        string workflowId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        return _profileSummariesByWorkflow.TryGetValue(
                workflowId,
                out CapabilityProfileSummary[]? summaries)
            ? Array.AsReadOnly(summaries)
            : [];
    }

    internal IReadOnlyList<CapabilityNumberChoice> GetNumberChoices(string icId)
    {
        return _numberChoicesByIc.TryGetValue(
                IcIdentifier.Normalize(icId),
                out CapabilityNumberChoice[]? choices)
            ? Array.AsReadOnly(choices)
            : [];
    }

    internal IReadOnlyList<long> GetDpReferenceCapacities(string icId)
    {
        return _dpReferenceCapacitiesByIc.TryGetValue(
                IcIdentifier.Normalize(icId),
                out long[]? capacities)
            ? Array.AsReadOnly(capacities)
            : [];
    }

    internal CapabilityFamilySummary GetFamilySummary(string icId)
    {
        return _familyByIc.GetValueOrDefault(IcIdentifier.Normalize(icId)) ??
            new CapabilityFamilySummary(
                null,
                CapabilityFamilyRelationship.Standalone,
                null);
    }

    internal bool IsDpPerspectiveIc(string icId)
    {
        return !string.IsNullOrWhiteSpace(icId) &&
            _dpPerspectiveIcs.Contains(IcIdentifier.Normalize(icId));
    }
}

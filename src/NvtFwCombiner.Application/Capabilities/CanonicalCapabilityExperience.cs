using System.Collections.ObjectModel;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>
/// Application-owned client disclosure over the one current canonical
/// publication. It never compiles, resolves profile registries, or reopens an
/// authoring input.
/// </summary>
public sealed class CanonicalCapabilityExperience : ICompositionCapabilityExperience
{
    private readonly ICanonicalCapabilityQuery _catalog;
    private readonly ICanonicalSupportMatrixQuery _supportMatrix;

    /// <summary>Creates the focused disclosure query over one publication owner.</summary>
    public CanonicalCapabilityExperience(
        ICanonicalCapabilityQuery catalog,
        ICanonicalSupportMatrixQuery supportMatrix)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _supportMatrix = supportMatrix ??
            throw new ArgumentNullException(nameof(supportMatrix));
    }

    /// <inheritdoc />
    public string DefaultIcId
    {
        get
        {
            CanonicalCapabilityCatalogSnapshot snapshot =
                _catalog.GetCurrentSnapshot();
            return snapshot.Capabilities
                .Where(static capability => IsAuthorable(capability.Authoring))
                .Select(static capability => capability.Identity)
                .Concat(snapshot.DynamicRoutes
                    .Where(static route => IsAuthorable(route.Authoring))
                    .Select(static route => route.Identity))
                .DistinctBy(static identity => identity.RouteId, StringComparer.Ordinal)
                .GroupBy(static identity => identity.IcId, StringComparer.Ordinal)
                .OrderByDescending(static group => group.Count())
                .ThenByDescending(static group => group
                    .Select(static identity => identity.WorkflowId)
                    .Distinct(StringComparer.Ordinal)
                    .Count())
                .ThenBy(static group => group.Key, StringComparer.Ordinal)
                .Select(static group => group.Key)
                .FirstOrDefault() ?? throw new InvalidOperationException(
                    "The canonical capability publication has no authorable IC route.");
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetIcIds()
    {
        CanonicalCapabilityCatalogSnapshot snapshot = _catalog.GetCurrentSnapshot();
        return Array.AsReadOnly(
            snapshot.Capabilities
                .Where(static capability => IsAuthorable(capability.Authoring))
                .Select(static capability => capability.Identity.IcId)
                .Concat(snapshot.DynamicRoutes
                    .Where(static route => IsAuthorable(route.Authoring))
                    .Select(static route => route.Identity.IcId))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    /// <inheritdoc />
    public IReadOnlyList<CapabilityNumberChoice> GetNumberSelectionChoices(
        string icId)
    {
        return _catalog.GetCurrentSnapshot().Disclosure.GetNumberChoices(icId);
    }

    /// <inheritdoc />
    public CapabilityCatalogSummary GetCatalogSummary()
    {
        IReadOnlyList<string> icIds = GetIcIds();
        return new CapabilityCatalogSummary(
            icIds.Count,
            GetStandardMergeProfileSummaries().Count,
            GetDpReplaceProfileSummaries().Count,
            CountAuthorableIcs(ExperienceIds.CtrlRamReplace));
    }

    /// <inheritdoc />
    public string? GetDpReplaceReferenceCapacityLabel(string icId)
    {
        IReadOnlyList<long> capacities = _catalog.GetCurrentSnapshot()
            .Disclosure.GetDpReferenceCapacities(icId);
        return capacities.Count == 0
            ? null
            : string.Join(
                " / ",
                capacities.Select(static capacity => $"0x{capacity:X}"));
    }

    /// <inheritdoc />
    public IReadOnlyList<CapabilityProfileSummary> GetAbMergeProfileSummaries()
    {
        return GetProfileSummaries(ExperienceIds.AbMerge);
    }

    /// <inheritdoc />
    public IReadOnlyList<CapabilityProfileSummary>
        GetStandardMergeProfileSummaries()
    {
        return GetProfileSummaries(ExperienceIds.StandardMerge);
    }

    /// <inheritdoc />
    public IReadOnlyList<CapabilityProfileSummary> GetDpReplaceProfileSummaries()
    {
        return GetProfileSummaries(ExperienceIds.DpReplace);
    }

    /// <inheritdoc />
    public CapabilityWorkflowReadiness GetReplaceWorkflowReadiness(
        string icId,
        string replaceMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(replaceMode);
        string normalizedIcId = IcIdentifier.Normalize(icId);
        string? workflowId = replaceMode switch
        {
            ExperienceIds.DpReplace => ExperienceIds.DpReplace,
            ExperienceIds.CtrlRamReplace => ExperienceIds.CtrlRamReplace,
            ExperienceIds.GeneralReplace => ExperienceIds.GeneralReplace,
            _ => null,
        };
        bool isDpReplace = StringComparer.Ordinal.Equals(
            workflowId,
            ExperienceIds.DpReplace);
        string unsupportedReason = workflowId is null
            ? "The selected Replace mode is not declared by the canonical capability contract."
            : isDpReplace
                ? "No owner-approved DP Replace profile/map is registered for this IC."
                : "No owner-approved executable and safety contract is registered for this IC and Replace mode.";
        string openCondition = workflowId is null
            ? "Add an owner-reviewed capability definition, profile/safety contract, and full-byte evidence."
            : isDpReplace
                ? "Add the IC-specific DP map/profile, full-byte golden parity, and firmware-owner review."
                : "Owner must reactivate the scope with a safe executable contract, direct evidence, and firmware-owner review.";
        return CapabilityWorkflowReadinessProjector.Project(
            _supportMatrix.Query().Matrix,
            normalizedIcId,
            workflowId,
            workflowId is not null && _catalog.HasAuthorableCapability(
                normalizedIcId,
                workflowId),
            unsupportedReason,
            openCondition);
    }

    /// <inheritdoc />
    public bool IsReplaceWorkflowAvailable(string icId, string replaceMode)
    {
        return GetReplaceWorkflowReadiness(icId, replaceMode).IsAvailable;
    }

    /// <inheritdoc />
    public CapabilityFamilySummary GetIcFamilySummary(string icId)
    {
        return _catalog.GetCurrentSnapshot().Disclosure.GetFamilySummary(icId);
    }

    /// <inheritdoc />
    public bool ArePerfectFamilyMembers(string firstIcId, string secondIcId)
    {
        CapabilityFamilySummary first = GetIcFamilySummary(firstIcId);
        CapabilityFamilySummary second = GetIcFamilySummary(secondIcId);
        return !string.IsNullOrWhiteSpace(first.FamilyId) &&
            StringComparer.Ordinal.Equals(first.FamilyId, second.FamilyId) &&
            first.Relationship == CapabilityFamilyRelationship.PerfectAlias &&
            second.Relationship == CapabilityFamilyRelationship.PerfectAlias;
    }

    /// <inheritdoc />
    public bool IsDpPerspectiveIc(string icId)
    {
        return _catalog.GetCurrentSnapshot().Disclosure.IsDpPerspectiveIc(icId);
    }

    internal CapabilityProfileSummary? FindStandardMergeProfileSummary(
        string icId)
    {
        string normalizedIcId = IcIdentifier.Normalize(icId);
        return GetStandardMergeProfileSummaries().FirstOrDefault(summary =>
            StringComparer.Ordinal.Equals(summary.IcId, normalizedIcId));
    }

    internal bool IsKnownIcId(string icId)
    {
        return GetIcIds().Contains(
            IcIdentifier.Normalize(icId),
            StringComparer.Ordinal);
    }

    private ReadOnlyCollection<CapabilityProfileSummary> GetProfileSummaries(
        string workflowId)
    {
        CanonicalCapabilityCatalogSnapshot snapshot = _catalog.GetCurrentSnapshot();
        var authorableIcs = snapshot.Capabilities
            .Where(capability =>
                IsAuthorable(capability.Authoring) &&
                StringComparer.Ordinal.Equals(
                    capability.Identity.WorkflowId,
                    workflowId))
            .Select(static capability => capability.Identity.IcId)
            .Concat(snapshot.DynamicRoutes
                .Where(route =>
                    IsAuthorable(route.Authoring) &&
                    StringComparer.Ordinal.Equals(
                        route.Identity.WorkflowId,
                        workflowId))
                .Select(static route => route.Identity.IcId))
            .ToHashSet(StringComparer.Ordinal);
        return Array.AsReadOnly(
        [
            .. snapshot.Disclosure.GetProfileSummaries(workflowId)
                .Where(summary => authorableIcs.Contains(summary.IcId))
                .OrderBy(static summary => summary.IcId, StringComparer.Ordinal)
                .ThenBy(static summary => summary.ProfileId, StringComparer.Ordinal),
        ]);
    }

    private int CountAuthorableIcs(string workflowId)
    {
        CanonicalCapabilityCatalogSnapshot snapshot = _catalog.GetCurrentSnapshot();
        return snapshot.Capabilities
            .Where(capability => IsAuthorable(capability.Authoring) &&
                StringComparer.Ordinal.Equals(
                    capability.Identity.WorkflowId,
                    workflowId))
            .Select(static capability => capability.Identity.IcId)
            .Concat(snapshot.DynamicRoutes
                .Where(route => IsAuthorable(route.Authoring) &&
                    StringComparer.Ordinal.Equals(
                        route.Identity.WorkflowId,
                        workflowId))
                .Select(static route => route.Identity.IcId))
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private static bool IsAuthorable(
        PinnedCapabilityDecision<CapabilityAuthoringAvailability> decision)
    {
        return decision.Value == CapabilityAuthoringAvailability.Available;
    }
}

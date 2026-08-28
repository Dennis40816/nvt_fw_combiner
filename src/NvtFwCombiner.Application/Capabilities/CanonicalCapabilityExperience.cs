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
    public CapabilitySelectorPublication GetSelectorPublication()
    {
        return _catalog.GetCurrentSnapshot().SelectorPublication;
    }

    /// <inheritdoc />
    public string DefaultIcId => GetSelectorPublication().DefaultIcId ??
        throw new InvalidOperationException(
            "The canonical capability publication has no authorable IC route.");

    /// <inheritdoc />
    public IReadOnlyList<string> GetIcIds()
    {
        return GetSelectorPublication().IcIds;
    }

    /// <inheritdoc />
    public IReadOnlyList<CapabilityNumberChoice> GetNumberSelectionChoices(
        string icId)
    {
        return GetSelectorPublication().GetNumberSelectionChoices(icId);
    }

    /// <inheritdoc />
    public CapabilityCatalogSummary GetCatalogSummary()
    {
        CanonicalCapabilityCatalogSnapshot snapshot = _catalog.GetCurrentSnapshot();
        CapabilitySelectorPublication selector = snapshot.SelectorPublication;
        return new CapabilityCatalogSummary(
            selector.IcIds.Count,
            GetProfileSummaries(snapshot, ExperienceIds.StandardMerge).Count,
            GetProfileSummaries(snapshot, ExperienceIds.DpReplace).Count,
            CountAuthorableIcs(selector, ExperienceIds.CtrlRamReplace));
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
                ? "DP Replace authoring is hidden until the 1.1.0 retirement decision."
                : "No owner-approved executable and safety contract is registered for this IC and Replace mode.";
        string openCondition = workflowId is null
            ? "Add an owner-reviewed capability definition, profile/safety contract, and full-byte evidence."
            : isDpReplace
                ? "At 1.1.0, owner must retire the route or re-enable authoring after approved AB/non-AB admission evidence."
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

    /// <inheritdoc />
    public CapabilityProfileSummary? FindStandardMergeProfileSummary(
        string icId)
    {
        string normalizedIcId = IcIdentifier.Normalize(icId);
        return GetStandardMergeProfileSummaries().FirstOrDefault(summary =>
            StringComparer.Ordinal.Equals(summary.IcId, normalizedIcId));
    }

    /// <inheritdoc />
    public bool IsKnownIcId(string icId)
    {
        return GetIcIds().Contains(
            IcIdentifier.Normalize(icId),
            StringComparer.Ordinal);
    }

    private ReadOnlyCollection<CapabilityProfileSummary> GetProfileSummaries(
        string workflowId)
    {
        return GetProfileSummaries(_catalog.GetCurrentSnapshot(), workflowId);
    }

    private static ReadOnlyCollection<CapabilityProfileSummary> GetProfileSummaries(
        CanonicalCapabilityCatalogSnapshot snapshot,
        string workflowId)
    {
        return Array.AsReadOnly(
        [
            .. snapshot.Disclosure.GetProfileSummaries(workflowId)
                .Where(summary => snapshot.SelectorPublication
                    .IsWorkflowAuthorable(summary.IcId, workflowId))
                .OrderBy(static summary => summary.IcId, StringComparer.Ordinal)
                .ThenBy(static summary => summary.ProfileId, StringComparer.Ordinal),
        ]);
    }

    private static int CountAuthorableIcs(
        CapabilitySelectorPublication selector,
        string workflowId)
    {
        return selector.IcIds.Count(icId =>
            selector.IsWorkflowAuthorable(icId, workflowId));
    }
}

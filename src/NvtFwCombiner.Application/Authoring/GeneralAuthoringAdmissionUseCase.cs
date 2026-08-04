using System.Collections.ObjectModel;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>
/// Complete typed input for one General admission resolution. Parent and Saved
/// Rule authority are distinct and cannot be replaced by observed file facts.
/// </summary>
public sealed record GeneralAuthoringAdmissionRequest
{
    private readonly ReadOnlyDictionary<string, long> _targetCapacities;

    /// <summary>Creates one immutable admission request.</summary>
    public GeneralAuthoringAdmissionRequest(
        GeneralMappingDraftState draft,
        IReadOnlyDictionary<string, long> targetAddressSpaceCapacities,
        GeneralTrustedParentResourcePolicy trustedParent,
        GeneralSavedRuleResourcePolicy? savedRule = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(targetAddressSpaceCapacities);
        ArgumentNullException.ThrowIfNull(trustedParent);
        if (targetAddressSpaceCapacities.Count == 0 ||
            targetAddressSpaceCapacities.Any(static item =>
                string.IsNullOrWhiteSpace(item.Key) || item.Value < 0))
        {
            throw new ArgumentException(
                "General admission requires named non-negative target capacities.",
                nameof(targetAddressSpaceCapacities));
        }

        Draft = draft;
        _targetCapacities = new ReadOnlyDictionary<string, long>(
            new Dictionary<string, long>(
                targetAddressSpaceCapacities,
                StringComparer.Ordinal));
        TargetAddressSpaceCapacities = _targetCapacities;
        TrustedParent = trustedParent;
        SavedRule = savedRule;
    }

    /// <summary>Exact immutable draft to admit and later compile.</summary>
    public GeneralMappingDraftState Draft { get; }

    /// <summary>Named target address-space capacities for this exact route.</summary>
    public IReadOnlyDictionary<string, long> TargetAddressSpaceCapacities { get; }

    /// <summary>Exact Trusted Parent authority.</summary>
    public GeneralTrustedParentResourcePolicy TrustedParent { get; }

    /// <summary>Optional Saved Rule narrowing authority.</summary>
    public GeneralSavedRuleResourcePolicy? SavedRule { get; }
}

/// <summary>
/// Sole production use case for General occupancy/resource admission. It owns
/// technical ceilings and consumes only content identities accepted by inspection.
/// </summary>
public static class GeneralAuthoringAdmissionUseCase
{
    /// <summary>Resolves one immutable result used unchanged by every consumer.</summary>
    public static GeneralAuthoringAdmissionResult Resolve(
        GeneralAuthoringAdmissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        GeneralInputResource[] resources =
        [
            .. request.Draft.Rows
                .Where(static row => row.Source.AcceptedFileStamp is not null)
                .Select(static row => new GeneralInputResource(
                    row.MappingId,
                    row.Source.AcceptedFileStamp!.Value.AcceptedLength)),
        ];

        return GeneralAuthoringAdmission.Evaluate(
            request.Draft,
            request.TargetAddressSpaceCapacities,
            resources,
            GeneralAuthoringTechnicalLimits.Default,
            request.TrustedParent,
            request.SavedRule);
    }

    /// <summary>
    /// Resolves the same admission from bounded pre-binding lengths. This may
    /// compile an exact inspection contract but never admits execution without
    /// accepted content stamps.
    /// </summary>
    public static GeneralAuthoringAdmissionResult ResolveCandidate(
        GeneralAuthoringAdmissionRequest request,
        IReadOnlyDictionary<string, long> observedFileLengths)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(observedFileLengths);
        GeneralInputResource[] resources =
        [
            .. request.Draft.Rows
                .Where(static row => row.Source.Kind == GeneralMappingSourceKind.FileArtifact)
                .Select(row => new GeneralInputResource(
                    row.MappingId,
                    row.Source.AcceptedFileStamp?.AcceptedLength ??
                        observedFileLengths.GetValueOrDefault(row.MappingId))),
        ];
        return GeneralAuthoringAdmission.Evaluate(
            request.Draft,
            request.TargetAddressSpaceCapacities,
            resources,
            GeneralAuthoringTechnicalLimits.Default,
            request.TrustedParent,
            request.SavedRule);
    }
}

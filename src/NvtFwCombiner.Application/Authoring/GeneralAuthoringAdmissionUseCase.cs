using System.Collections.ObjectModel;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>One opaque file-resource observation requested by General admission.</summary>
public sealed record GeneralInputResourceObservationRequest(
    string SlotId,
    string ResourceReference);

/// <summary>
/// Inward port used by Application to observe whole-file lengths without taking
/// a filesystem dependency.
/// </summary>
public interface IGeneralInputResourceObservationPort
{
    /// <summary>Tries to observe one selected resource without interpreting its contents.</summary>
    bool TryObserveLength(
        GeneralInputResourceObservationRequest request,
        out long lengthBytes);
}

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
/// technical ceilings and asks an inward port only for observed lengths.
/// </summary>
public sealed class GeneralAuthoringAdmissionUseCase
{
    private readonly IGeneralInputResourceObservationPort _resourceObserver;

    /// <summary>Creates the use case with one host resource observer.</summary>
    public GeneralAuthoringAdmissionUseCase(
        IGeneralInputResourceObservationPort resourceObserver)
    {
        ArgumentNullException.ThrowIfNull(resourceObserver);
        _resourceObserver = resourceObserver;
    }

    /// <summary>Resolves one immutable result used unchanged by every consumer.</summary>
    public GeneralAuthoringAdmissionResult Resolve(
        GeneralAuthoringAdmissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<GeneralInputResource> resources = [];
        foreach (GeneralMappingDraftRow row in request.Draft.Rows.Where(
                     static row =>
                         row.Source.Kind == GeneralMappingSourceKind.FileArtifact))
        {
            var observation = new GeneralInputResourceObservationRequest(
                row.MappingId,
                row.Source.Reference);
            if (_resourceObserver.TryObserveLength(
                    observation,
                    out long lengthBytes))
            {
                resources.Add(new GeneralInputResource(
                    row.MappingId,
                    lengthBytes));
            }
        }

        return GeneralAuthoringAdmission.Evaluate(
            request.Draft,
            request.TargetAddressSpaceCapacities,
            resources,
            GeneralAuthoringTechnicalLimits.Default,
            request.TrustedParent,
            request.SavedRule);
    }
}

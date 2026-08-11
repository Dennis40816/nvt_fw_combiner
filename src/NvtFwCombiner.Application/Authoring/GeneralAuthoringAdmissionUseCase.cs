using System.Collections.ObjectModel;
using NvtFwCombiner.Domain.Composition;

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
    /// <summary>Resolves one accepted-stamp draft for a named output capacity.</summary>
    public static GeneralAuthoringAdmissionResult Resolve(
        GeneralMappingDraftState mappingDraft,
        long outputCapacity,
        GeneralTrustedParentResourcePolicy trustedParent,
        GeneralSavedRuleResourcePolicy? savedRule = null)
    {
        return Resolve(new GeneralAuthoringAdmissionRequest(
            mappingDraft,
            new Dictionary<string, long>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.OutputImage] = outputCapacity,
            },
            trustedParent,
            savedRule));
    }

    /// <summary>Resolves one pre-binding draft for observed immutable file lengths.</summary>
    public static GeneralAuthoringAdmissionResult ResolveCandidate(
        GeneralMappingDraftState mappingDraft,
        long outputCapacity,
        GeneralTrustedParentResourcePolicy trustedParent,
        IReadOnlyDictionary<string, long> observedFileLengths,
        GeneralSavedRuleResourcePolicy? savedRule = null)
    {
        return ResolveCandidate(
            new GeneralAuthoringAdmissionRequest(
                mappingDraft,
                new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    [CompositionAddressSpaceIds.OutputImage] = outputCapacity,
                },
                trustedParent,
                savedRule),
            observedFileLengths);
    }

    /// <summary>Creates the current exact-parent resource envelope for one General draft.</summary>
    public static GeneralTrustedParentResourcePolicy CreateTrustedParentPolicy(
        string parentId,
        GeneralMappingDraftState mappingDraft,
        SavedRuleParentIdentity? parentIdentity = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentId);
        ArgumentNullException.ThrowIfNull(mappingDraft);
        GeneralResourceLimits technical = GeneralAuthoringTechnicalLimits.Default;
        var limits = new GeneralResourceLimits(
            technical.MaximumMappingCount,
            technical.MaximumTotalWriteBytes,
            technical.MaximumFileBytes,
            technical.MaximumSafeMaterializationBytes,
            mappingDraft.Rows
                .Where(static row =>
                    row.Source.Kind == GeneralMappingSourceKind.FileArtifact)
                .Select(static row => new GeneralSlotLengthLimits(
                    row.MappingId,
                    minimumBytes: 1,
                    maximumBytes: int.MaxValue)));
        return parentIdentity is null
            ? new GeneralTrustedParentResourcePolicy(parentId, limits)
            : new GeneralTrustedParentResourcePolicy(parentIdentity, limits);
    }

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

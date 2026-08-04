using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static GeneralAuthoringAdmissionResult AdmitGeneralMappingDraft(
        GeneralMappingDraftState mappingDraft,
        long outputCapacity,
        GeneralTrustedParentResourcePolicy trustedParent,
        GeneralSavedRuleResourcePolicy? savedRule = null)
    {
        return GeneralAuthoringAdmissionUseCase.Resolve(
            new GeneralAuthoringAdmissionRequest(
                mappingDraft,
                new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    [CompositionAddressSpaceIds.OutputImage] = outputCapacity,
                },
                trustedParent,
                savedRule));
    }

    private static GeneralAuthoringAdmissionResult AdmitGeneralMappingCandidate(
        GeneralMappingDraftState mappingDraft,
        long outputCapacity,
        GeneralTrustedParentResourcePolicy trustedParent,
        IReadOnlyDictionary<string, long> observedFileLengths)
    {
        return GeneralAuthoringAdmissionUseCase.ResolveCandidate(
            new GeneralAuthoringAdmissionRequest(
                mappingDraft,
                new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    [CompositionAddressSpaceIds.OutputImage] = outputCapacity,
                },
                trustedParent),
            observedFileLengths);
    }

    /// <summary>Resolves the same exact Parent admission used by General Replace execution.</summary>
    public static GeneralAuthoringAdmissionResult? GetGeneralReplaceAuthoringAdmission(
        string icId,
        long referenceCapacity,
        GeneralMappingDraftState mappingDraft)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(referenceCapacity);
        ArgumentNullException.ThrowIfNull(mappingDraft);
        GeneralReplaceV2Registration? registration =
            ResolveGeneralReplaceValidationRegistration(
                Profiles.IcSupportCatalog.NormalizeIcId(icId));
        return registration is null
            ? null
            : AdmitGeneralMappingDraft(
                mappingDraft,
                referenceCapacity,
                CreateCurrentGeneralTrustedParentPolicy(
                    registration.ExactParent.Admission.ParentBinding.ProfileId,
                    mappingDraft,
                    registration.ExactParent.Admission.ParentBinding));
    }

    private static GeneralReplaceV2Registration?
        ResolveGeneralReplaceValidationRegistration(string normalizedIcId)
    {
        return BuiltInV2RegistrationRegistry.GeneralReplaceByIc.TryGetValue(
                normalizedIcId,
                out GeneralReplaceV2Registration? registration)
            ? registration
            : null;
    }

    /// <summary>
    /// Compatibility projection for current General V2 Parents whose source
    /// slot is one-or-more unnormalized BINs bounded to 1..Int32.MaxValue.
    /// Delete when profile schema resource envelopes are projected directly.
    /// </summary>
    private static GeneralTrustedParentResourcePolicy
        CreateCurrentGeneralTrustedParentPolicy(
            string parentId,
            GeneralMappingDraftState mappingDraft,
            SavedRuleParentIdentity? parentIdentity = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentId);
        ArgumentNullException.ThrowIfNull(mappingDraft);
        GeneralResourceLimits technical =
            GeneralAuthoringTechnicalLimits.Default;
        var limits = new GeneralResourceLimits(
                technical.MaximumMappingCount,
                technical.MaximumTotalWriteBytes,
                technical.MaximumFileBytes,
                technical.MaximumSafeMaterializationBytes,
                mappingDraft.Rows
                    .Where(static row =>
                        row.Source.Kind ==
                        GeneralMappingSourceKind.FileArtifact)
                    .Select(static row =>
                        new GeneralSlotLengthLimits(
                            row.MappingId,
                            minimumBytes: 1,
                            maximumBytes: int.MaxValue)));
        return parentIdentity is null
            ? new GeneralTrustedParentResourcePolicy(parentId, limits)
            : new GeneralTrustedParentResourcePolicy(parentIdentity, limits);
    }
}

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static readonly GeneralAuthoringAdmissionUseCase
        GeneralAdmissionUseCase = new(new GeneralFileResourceObservationAdapter());

    private static GeneralAuthoringAdmissionResult AdmitGeneralMappingDraft(
        GeneralMappingDraftState mappingDraft,
        long outputCapacity,
        GeneralTrustedParentResourcePolicy trustedParent,
        GeneralSavedRuleResourcePolicy? savedRule = null)
    {
        return GeneralAdmissionUseCase.Resolve(
            new GeneralAuthoringAdmissionRequest(
                mappingDraft,
                new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    [CompositionAddressSpaceIds.OutputImage] = outputCapacity,
                },
                trustedParent,
                savedRule));
    }

    /// <summary>
    /// Compatibility projection for current General V2 Parents whose source
    /// slot is one-or-more unnormalized BINs bounded to 1..Int32.MaxValue.
    /// Delete when profile schema resource envelopes are projected directly.
    /// </summary>
    private static GeneralTrustedParentResourcePolicy
        CreateCurrentGeneralTrustedParentPolicy(
            string parentId,
            GeneralMappingDraftState mappingDraft)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parentId);
        ArgumentNullException.ThrowIfNull(mappingDraft);
        GeneralResourceLimits technical =
            GeneralAuthoringTechnicalLimits.Default;
        return new GeneralTrustedParentResourcePolicy(
            parentId,
            new GeneralResourceLimits(
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
                            maximumBytes: int.MaxValue))));
    }

    private sealed class GeneralFileResourceObservationAdapter :
        IGeneralInputResourceObservationPort
    {
        public bool TryObserveLength(
            GeneralInputResourceObservationRequest request,
            out long lengthBytes)
        {
            ArgumentNullException.ThrowIfNull(request);
            string fullPath = Path.GetFullPath(request.ResourceReference);
            if (!File.Exists(fullPath))
            {
                lengthBytes = 0;
                return false;
            }

            lengthBytes = new FileInfo(fullPath).Length;
            return true;
        }
    }
}

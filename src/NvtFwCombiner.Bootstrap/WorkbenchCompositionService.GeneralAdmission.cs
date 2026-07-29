using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static GeneralAuthoringAdmissionResult AdmitGeneralMappingDraft(
        GeneralMappingDraftState mappingDraft,
        long outputCapacity)
    {
        GeneralInputResource[] resources =
        [
            .. mappingDraft.Rows
                .Where(static row =>
                    row.Source.Kind == GeneralMappingSourceKind.FileArtifact)
                .Select(static row => CreateGeneralInputResource(row)),
        ];
        GeneralSlotLengthLimits[] exactParentSlots =
        [
            // Logical-output V2 compiles one exact immutable runtime binding per selected file.
            // This projects that Parent binding identity; it does not infer firmware facts.
            .. resources.Select(static resource => new GeneralSlotLengthLimits(
                resource.SlotId,
                minimumBytes: 0,
                maximumBytes: GeneralAuthoringTechnicalLimits.Default.MaximumFileBytes,
                allowedLengths: [resource.LengthBytes])),
        ];
        GeneralResourceLimits trustedParentLimits = new(
            GeneralAuthoringTechnicalLimits.Default.MaximumMappingCount,
            GeneralAuthoringTechnicalLimits.Default.MaximumTotalWriteBytes,
            GeneralAuthoringTechnicalLimits.Default.MaximumFileBytes,
            GeneralAuthoringTechnicalLimits.Default.MaximumSafeMaterializationBytes,
            exactParentSlots);

        return GeneralAuthoringAdmission.Evaluate(
            mappingDraft,
            new Dictionary<string, long>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.OutputImage] = outputCapacity,
            },
            resources,
            GeneralAuthoringTechnicalLimits.Default,
            trustedParentLimits,
            savedRuleLimits: null);
    }

    private static GeneralInputResource CreateGeneralInputResource(
        GeneralMappingDraftRow row)
    {
        string fullPath = Path.GetFullPath(row.Source.Reference);
        long observedLength = File.Exists(fullPath)
            ? new FileInfo(fullPath).Length
            : row.SourceRange.EndExclusive;
        return new GeneralInputResource(row.MappingId, observedLength);
    }
}

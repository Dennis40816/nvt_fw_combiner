using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static bool TryCreateGeneralMergeMappings(
        GeneralAuthoringAdmissionResult admission,
        out IReadOnlyList<ExplicitMapping> explicitMappings,
        out IReadOnlyList<AddressSpace> requestAddressSpaces,
        out IReadOnlyList<InputArtifactBinding> mappingBindings,
        out IReadOnlyList<CompositionIssue> issues,
        bool allowUnbound = false)
    {
        GeneralMappingDraftState mappingDraft = admission.RequireAdmittedDraft();
        var resources =
            admission.InputResources.ToDictionary(
                static resource => resource.SlotId,
                StringComparer.Ordinal);
        List<ExplicitMapping> mappings = [];
        List<AddressSpace> spaces = [];
        List<InputArtifactBinding> bindings = [];
        List<CompositionIssue> issueList = [];
        for (int index = 0; index < mappingDraft.Rows.Count; index++)
        {
            GeneralMappingDraftRow input = mappingDraft.Rows[index];

            string addressSpaceId = $"{input.MappingId}-input";
            string fullPath = Path.GetFullPath(input.Source.Reference);
            if (!resources.TryGetValue(
                    input.MappingId,
                    out GeneralInputResource? resource))
            {
                throw new InvalidOperationException(
                    $"Admitted General Merge mapping '{input.MappingId}' has no observed input resource.");
            }

            FileStamp? acceptedStamp = input.Source.AcceptedFileStamp;
            if (acceptedStamp is null && !allowUnbound)
            {
                issueList.Add(new CompositionIssue(
                    GeneralSelectedFileInspectionIssueCodes.SnapshotRequired,
                    $"General Merge mapping '{input.MappingId}' has no accepted selected-file content snapshot.",
                    input.MappingId));
                continue;
            }

            long declaredLength = acceptedStamp?.AcceptedLength ?? resource.LengthBytes;
            if (resource.LengthBytes != declaredLength)
            {
                issueList.Add(new CompositionIssue(
                    CompositionRunIssueCodes.InputArtifactContentSnapshotMismatch,
                    $"General Merge mapping '{input.MappingId}' no longer matches its accepted selected-file length.",
                    input.MappingId));
                continue;
            }

            if (declaredLength < input.SourceRange.EndExclusive)
            {
                issueList.Add(new CompositionIssue(
                    WorkbenchIssueCodes.GeneralMergeSourceOutOfBounds,
                    $"General Merge mapping '{input.MappingId}' source range exceeds the selected input file length.",
                    input.MappingId));
                continue;
            }

            spaces.Add(new AddressSpace(addressSpaceId, declaredLength, AddressSpaceMutability.Immutable));
            if (acceptedStamp is not null)
            {
                bindings.Add(new InputArtifactBinding(
                    addressSpaceId,
                    input.MappingId,
                    fullPath,
                    acceptedContentStamp: acceptedStamp));
            }
            mappings.Add(new ExplicitMapping(
                input.MappingId,
                100 + (index * 10),
                ExplicitMappingOperationKind.CopyRange,
                addressSpaceId,
                input.SourceRange,
                input.TargetAddressSpaceId,
                input.TargetRange,
                input.OverlapPolicy,
                input.Alignment,
                input.Reason,
                targetRegionId: input.TargetRegionId,
                provenance: input.Provenance));
        }

        explicitMappings = mappings;
        requestAddressSpaces = spaces;
        mappingBindings = bindings;
        issues = issueList;
        return issueList.Count == 0;
    }

    private static Dictionary<string, string> CreateGeneralMergeReportSlotPaths(
        GeneralMappingDraftState mappingDraft)
    {
        Dictionary<string, string> paths = new(StringComparer.Ordinal);
        foreach (GeneralMappingDraftRow mapping in mappingDraft.Rows)
        {
            if (mapping.Source.Kind == GeneralMappingSourceKind.FileArtifact &&
                !string.IsNullOrWhiteSpace(mapping.Source.Reference))
            {
                paths[mapping.MappingId] = mapping.Source.Reference;
            }
        }

        return paths;
    }

    private static string GeneralMergeSourceLabel(GeneralMappingDraftRow mapping)
    {
        return string.IsNullOrWhiteSpace(mapping.Source.Reference)
            ? "Source BIN"
            : Path.GetFileName(mapping.Source.Reference);
    }
}

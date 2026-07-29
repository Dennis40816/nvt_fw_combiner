using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static bool TryCreateGeneralMergeMappings(
        GeneralMappingDraftState mappingDraft,
        out IReadOnlyList<ExplicitMapping> explicitMappings,
        out IReadOnlyList<AddressSpace> requestAddressSpaces,
        out IReadOnlyList<InputArtifactBinding> mappingBindings,
        out IReadOnlyList<CompositionIssue> issues)
    {
        List<ExplicitMapping> mappings = [];
        List<AddressSpace> spaces = [];
        List<InputArtifactBinding> bindings = [];
        List<CompositionIssue> issueList = [];
        for (int index = 0; index < mappingDraft.Rows.Count; index++)
        {
            GeneralMappingDraftRow input = mappingDraft.Rows[index];

            string addressSpaceId = $"{input.MappingId}-input";
            string fullPath = Path.GetFullPath(input.Source.Reference);
            long declaredLength = File.Exists(fullPath)
                ? new FileInfo(fullPath).Length
                : input.SourceRange.EndExclusive;
            if (declaredLength < input.SourceRange.EndExclusive)
            {
                issueList.Add(new CompositionIssue(
                    WorkbenchIssueCodes.GeneralMergeSourceOutOfBounds,
                    $"General Merge mapping '{input.MappingId}' source range exceeds the selected input file length.",
                    input.MappingId));
                continue;
            }

            spaces.Add(new AddressSpace(addressSpaceId, declaredLength, AddressSpaceMutability.Immutable));
            bindings.Add(new InputArtifactBinding(addressSpaceId, input.MappingId, fullPath));
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

/// <summary>One user-authored General Merge mapping row from the workbench surface.</summary>
public sealed record WorkbenchGeneralMergeMappingInput(
    string MappingId,
    string FilePath,
    string SourceStart,
    string TargetStart,
    string Length,
    int Alignment = 1,
    string? Reason = null,
    OperationProvenance? Provenance = null);

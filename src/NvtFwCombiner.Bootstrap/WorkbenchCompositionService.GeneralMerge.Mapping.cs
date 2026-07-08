using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static bool TryCreateGeneralMergeMappings(
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs,
        out IReadOnlyList<ExplicitMapping> explicitMappings,
        out IReadOnlyList<AddressSpace> requestAddressSpaces,
        out IReadOnlyList<InputArtifactBinding> mappingBindings,
        out IReadOnlyList<CompositionIssue> issues)
    {
        List<ExplicitMapping> mappings = [];
        List<AddressSpace> spaces = [];
        List<InputArtifactBinding> bindings = [];
        List<CompositionIssue> issueList = [];
        for (int index = 0; index < mappingInputs.Count; index++)
        {
            WorkbenchGeneralMergeMappingInput input = mappingInputs[index];
            if (!TryParseGeneralMergeMapping(input, out ByteRange sourceRange, out ByteRange targetRange, out CompositionIssue? issue))
            {
                issueList.Add(issue);
                continue;
            }

            string addressSpaceId = $"{input.MappingId}-input";
            string fullPath = Path.GetFullPath(input.FilePath);
            long declaredLength = File.Exists(fullPath)
                ? new FileInfo(fullPath).Length
                : sourceRange.EndExclusive;
            if (declaredLength < sourceRange.EndExclusive)
            {
                issueList.Add(new CompositionIssue(
                    "ui.general-merge.source-out-of-bounds",
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
                sourceRange,
                CompositionAddressSpaceIds.OutputImage,
                targetRange,
                OverlapPolicy.Reject,
                input.Alignment,
                input.Reason ?? "Copy explicit General Merge mapping.",
                targetRegionId: "general-output",
                provenance: input.Provenance));
        }

        explicitMappings = mappings;
        requestAddressSpaces = spaces;
        mappingBindings = bindings;
        issues = issueList;
        return issueList.Count == 0;
    }

    private static bool TryParseGeneralMergeMapping(
        WorkbenchGeneralMergeMappingInput input,
        out ByteRange sourceRange,
        out ByteRange targetRange,
        out CompositionIssue issue)
    {
        sourceRange = default;
        targetRange = default;
        if (!BootstrapRangeText.TryParseNonNegativeLong(input.SourceStart, out long sourceStart) ||
            !BootstrapRangeText.TryParseNonNegativeLong(input.TargetStart, out long targetStart) ||
            !BootstrapRangeText.TryParseNonNegativeLong(input.Length, out long length) ||
            length <= 0)
        {
            issue = new CompositionIssue(
                "ui.general-merge.range-invalid",
                $"General Merge mapping '{input.MappingId}' must use valid source start, target start, and positive length values.",
                input.MappingId);
            return false;
        }

        try
        {
            sourceRange = new ByteRange(sourceStart, length);
            targetRange = new ByteRange(targetStart, length);
            issue = default!;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or OverflowException)
        {
            issue = new CompositionIssue(
                "ui.general-merge.range-invalid",
                $"General Merge mapping '{input.MappingId}' range exceeds the supported address size.",
                input.MappingId);
            return false;
        }
    }

    private static IReadOnlyList<OperationRunSummary> CreateGeneralMergePlanningOperations(
        IReadOnlyList<ExplicitMapping> explicitMappings)
    {
        return
        [
            .. explicitMappings.Select(mapping => new OperationRunSummary(
                mapping.MappingId,
                mapping.Sequence,
                CompositionOperationKind.CopyRange,
                OperationRunStatus.Skipped,
                mapping.SourceBindingId,
                mapping.SourceRange,
                mapping.TargetSpaceId,
                mapping.TargetRange,
                mapping.OverlapPolicy,
                null,
                null,
                [],
                [],
                mapping.Reason,
                mapping.Provenance)),
        ];
    }

    private static Dictionary<string, string> CreateGeneralMergeReportSlotPaths(
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs)
    {
        Dictionary<string, string> paths = new(StringComparer.Ordinal);
        foreach (WorkbenchGeneralMergeMappingInput mapping in mappingInputs)
        {
            if (!string.IsNullOrWhiteSpace(mapping.FilePath))
            {
                paths[mapping.MappingId] = mapping.FilePath;
            }
        }

        return paths;
    }

    private static string GeneralMergeSourceLabel(WorkbenchGeneralMergeMappingInput mapping)
    {
        return string.IsNullOrWhiteSpace(mapping.FilePath)
            ? "Source BIN"
            : Path.GetFileName(mapping.FilePath);
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

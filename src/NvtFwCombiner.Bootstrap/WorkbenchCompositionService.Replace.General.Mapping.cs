using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    private static bool TryCreateGeneralReplaceMappings(
        WorkbenchGeneralReplaceMappingInput[] mappingInputs,
        out IReadOnlyList<ExplicitMapping> explicitMappings,
        out IReadOnlyList<AddressSpace> requestAddressSpaces,
        out IReadOnlyList<InputArtifactBinding> mappingBindings,
        out IReadOnlyList<CompositionIssue> issues)
    {
        List<ExplicitMapping> mappings = [];
        List<AddressSpace> spaces = [];
        List<InputArtifactBinding> bindings = [];
        List<CompositionIssue> issueList = [];
        for (int index = 0; index < mappingInputs.Length; index++)
        {
            WorkbenchGeneralReplaceMappingInput input = mappingInputs[index];
            if (!TryParseGeneralReplaceRange(input, out ByteRange targetRange, out CompositionIssue? issue))
            {
                issueList.Add(issue);
                continue;
            }

            string addressSpaceId = $"{input.MappingId}-input";
            string fullPath = Path.GetFullPath(input.FilePath);
            long declaredLength = File.Exists(fullPath)
                ? new FileInfo(fullPath).Length
                : targetRange.Length;
            spaces.Add(new AddressSpace(addressSpaceId, declaredLength, AddressSpaceMutability.Immutable));
            bindings.Add(new InputArtifactBinding(addressSpaceId, input.MappingId, fullPath));
            mappings.Add(new ExplicitMapping(
                input.MappingId,
                100 + (index * 10),
                ExplicitMappingOperationKind.ReplaceRange,
                addressSpaceId,
                new ByteRange(0, targetRange.Length),
                CompositionAddressSpaceIds.OutputImage,
                targetRange,
                OverlapPolicy.Reject,
                alignment: 1,
                "Replace explicit General range.",
                targetRegionId: null));
        }

        explicitMappings = mappings;
        requestAddressSpaces = spaces;
        mappingBindings = bindings;
        issues = issueList;
        return issueList.Count == 0;
    }

    private static bool TryParseGeneralReplaceRange(
        WorkbenchGeneralReplaceMappingInput input,
        out ByteRange targetRange,
        out CompositionIssue issue)
    {
        targetRange = default;
        if (!BootstrapRangeText.TryParseNonNegativeLong(input.TargetStart, out long start) ||
            !BootstrapRangeText.TryParseNonNegativeLong(input.TargetEndInclusive, out long endInclusive) ||
            endInclusive < start)
        {
            issue = new CompositionIssue(
                "ui.general-replace.range-invalid",
                $"General Replace mapping '{input.MappingId}' must use a valid inclusive start/end range.",
                input.MappingId);
            return false;
        }

        try
        {
            targetRange = ByteRange.FromStartEndExclusive(start, checked(endInclusive + 1));
            issue = default!;
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            issue = new CompositionIssue(
                "ui.general-replace.range-invalid",
                $"General Replace mapping '{input.MappingId}' must use a valid inclusive start/end range.",
                input.MappingId);
            return false;
        }
        catch (OverflowException)
        {
            issue = new CompositionIssue(
                "ui.general-replace.range-invalid",
                $"General Replace mapping '{input.MappingId}' range exceeds the supported address size.",
                input.MappingId);
            return false;
        }
    }

    private static IReadOnlyList<OperationRunSummary> CreateGeneralReplacePlanningOperations(
        IReadOnlyList<ExplicitMapping> explicitMappings)
    {
        return
        [
            .. explicitMappings.Select(mapping => new OperationRunSummary(
                mapping.MappingId,
                mapping.Sequence,
                CompositionOperationKind.ReplaceRange,
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

    private static Dictionary<string, string> CreateGeneralReplaceReportSlotPaths(
        IReadOnlyDictionary<string, string> slotPaths,
        IReadOnlyList<WorkbenchGeneralReplaceMappingInput> mappingInputs)
    {
        Dictionary<string, string> paths = new(slotPaths, StringComparer.Ordinal);
        foreach (WorkbenchGeneralReplaceMappingInput mapping in mappingInputs)
        {
            if (!string.IsNullOrWhiteSpace(mapping.FilePath))
            {
                paths[mapping.MappingId] = mapping.FilePath;
            }
        }

        return paths;
    }
}

/// <summary>One user-authored General Replace mapping row from the workbench surface.</summary>
public sealed record WorkbenchGeneralReplaceMappingInput(
    string MappingId,
    string FilePath,
    string TargetStart,
    string TargetEndInclusive);

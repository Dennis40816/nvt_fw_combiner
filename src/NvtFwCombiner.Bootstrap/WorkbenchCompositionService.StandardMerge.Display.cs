using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets readable memory-map rows for the selected Standard Merge profile and DP input length.</summary>
    public static IReadOnlyList<WorkbenchMemoryMapRow> GetStandardMergeMemoryMapRows(string icId, long? dpInputLength)
    {
        if (FindStandardMergeProfileSummaryByIc(icId) is null)
        {
            return
            [
                new WorkbenchMemoryMapRow(
                    "Profile",
                    "No profile",
                    "Blocked",
                    "No output",
                    $"Standard Merge is not available for {icId}."),
            ];
        }

        bool lengthPending = IsStandardMergeDpLengthPending(icId, dpInputLength);
        if (lengthPending)
        {
            return
            [
                new WorkbenchMemoryMapRow(
                    "Selected DP BIN length pending",
                    "No output",
                    "Initialize",
                    "Blank output 0x00",
                    FormatStandardMergeInitializationDetail(icId, lengthPending: true)),
            ];
        }

        if (!TryCompileStandardMerge(
                icId,
                dpInputLength,
                out CompiledComposition? composition,
                out IReadOnlyList<CompositionIssue> issues))
        {
            return
            [
                new WorkbenchMemoryMapRow(
                    "Profile",
                    "Profile",
                    "Blocked",
                    "No output",
                    FormatIssues(issues)),
            ];
        }

        ImageInitialization initialization = composition.Plan.OutputInitialization;
        string initializedState = FormatStandardMergeInitializationState(initialization);
        List<WorkbenchMemoryMapRow> rows =
        [
            new(
                FormatStandardMergeInitializationRangeLabel(composition, lengthPending),
                "No output",
                "Initialize",
                initializedState,
                FormatStandardMergeInitializationDetail(icId, lengthPending)),
        ];
        foreach (CompositionOperation operation in composition.Plan.OrderedOperations)
        {
            string afterSource = operation.SourceSpaceId is null
                ? operation.Kind.ToString()
                : AddressSpaceLabel(operation.SourceSpaceId);
            string sourceRange = operation.SourceRange is null
                ? "no source range"
                : FormatDisplayRange(operation.SourceRange.Value);
            rows.Add(new WorkbenchMemoryMapRow(
                FormatDisplayRange(operation.TargetRange),
                initializedState,
                ActionLabel(operation.Kind),
                afterSource,
                $"Sequence {operation.Sequence}: {operation.Kind} {sourceRange} -> output image {FormatDisplayRange(operation.TargetRange)}. Reason: {operation.Reason}"));
        }

        return rows;
    }
}

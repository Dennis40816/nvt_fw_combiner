using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets final visual coverage segments for the selected Standard Merge profile and DP input length.</summary>
    public static IReadOnlyList<WorkbenchMemoryCoverageSegment> GetStandardMergeCoverageSegments(
        string icId,
        long? dpInputLength)
    {
        if (FindStandardMergeProfileSummaryByIc(icId) is null)
        {
            return
            [
                new WorkbenchMemoryCoverageSegment(
                    "No range",
                    "No profile",
                    "Standard Merge is unavailable.",
                    "#CBD5E1",
                    280,
                    false),
            ];
        }

        bool lengthPending = IsStandardMergeDpLengthPending(icId, dpInputLength);
        if (lengthPending)
        {
            return
            [
                new WorkbenchMemoryCoverageSegment(
                    "Selected DP BIN length pending",
                    "DP length pending",
                    $"Select a DP BIN before final ownership is drawn. Supported DP lengths are {FormatStandardMergeSupportedDpLengths(icId)}.",
                    "#CBD5E1",
                    280,
                    false),
            ];
        }

        if (!TryCompileStandardMerge(
                icId,
                dpInputLength,
                out CompiledComposition? composition,
                out IReadOnlyList<CompositionIssue> issues))
        {
            bool unsupportedLength = issues.Any(issue =>
                string.Equals(
                    issue.Code,
                    WorkbenchIssueCodes.StandardMergeDpLengthUnsupported,
                    StringComparison.Ordinal));
            return
            [
                new WorkbenchMemoryCoverageSegment(
                    "Profile",
                    unsupportedLength ? "Invalid DP length" : "Invalid profile",
                    FormatIssues(issues),
                    "#F97316",
                    280,
                    false),
            ];
        }

        ImageInitialization initialization = composition.Plan.OutputInitialization;
        string initializationLabel = initialization.Kind == ImageInitializationKind.Blank
            ? $"Blank 0x{initialization.FillByte:X2}"
            : $"Reference {initialization.ReferenceSpaceId}";
        CoverageSegment[] segments =
        [
            new CoverageSegment(
                new ByteRange(0, initialization.Capacity),
                initializationLabel,
                "No source input writes this output range.",
                "#CBD5E1",
                false),
        ];

        foreach (CompositionOperation operation in composition.Plan.OrderedOperations)
        {
            string label = operation.SourceSpaceId is null
                ? ActionLabel(operation.Kind)
                : AddressSpaceLabel(operation.SourceSpaceId);
            string detail = operation.SourceRange is null
                ? $"Operation {operation.OperationId}, sequence {operation.Sequence}."
                : $"Copies source {FormatDisplayRange(operation.SourceRange.Value)} into this output range.";
            segments = ApplyCoverageWrite(
                segments,
                new CoverageSegment(
                    operation.TargetRange,
                    label,
                    detail,
                    CoverageFill(label),
                    false));
        }

        return
        [
            .. segments.Select(segment => new WorkbenchMemoryCoverageSegment(
                FormatDisplayRange(segment.Range),
                segment.SourceLabel,
                segment.Detail,
                segment.Fill,
                WidthForRange(segment.Range, initialization.Capacity),
                false)),
        ];
    }
}

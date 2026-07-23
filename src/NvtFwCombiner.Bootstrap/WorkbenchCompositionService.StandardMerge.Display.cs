using NvtFwCombiner.Domain.Composition;
using static NvtFwCombiner.Bootstrap.WorkbenchMemoryDisplayProjection;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets one compiled Standard Merge memory display snapshot for UI projection.</summary>
    public static WorkbenchMemoryDisplay GetStandardMergeMemoryDisplay(string icId, long? dpInputLength)
    {
        if (FindStandardMergeProfileSummaryByIc(icId) is null)
        {
            return CreateMessageDisplay(
                "No Standard Merge profile",
                (
                    "Profile",
                    "No profile",
                    "Blocked",
                    "No output",
                    $"Standard Merge is not available for {icId}."),
                (
                    "No range",
                    "No profile",
                    "Standard Merge is unavailable.",
                    "#CBD5E1"));
        }

        bool lengthPending = dpInputLength is null && IsBuiltInV2StandardMergeMapCapacityPending(icId);
        if (lengthPending)
        {
            return CreateMessageDisplay(
                "Selected DP BIN length pending",
                (
                    "Selected DP BIN length pending",
                    "No output",
                    "Initialize",
                    "Blank output 0x00",
                    FormatStandardMergeInitializationDetail(icId, lengthPending: true)),
                (
                    "Selected DP BIN length pending",
                    "DP length pending",
                    $"Select a DP BIN before final ownership is drawn. Supported DP lengths are {FormatStandardMergeSupportedDpLengths(icId)}.",
                    "#CBD5E1"));
        }

        if (!TryCompileStandardMerge(
                icId,
                dpInputLength,
                out CompiledComposition? composition,
                out IReadOnlyList<CompositionIssue> issues))
        {
            string detail = FormatIssues(issues);
            bool unsupportedLength = issues.Any(issue => string.Equals(
                issue.Code,
                WorkbenchIssueCodes.StandardMergeDpLengthUnsupported,
                StringComparison.Ordinal));
            return CreateMessageDisplay(
                detail,
                (
                    "Profile",
                    "Profile",
                    "Blocked",
                    "No output",
                    detail),
                (
                    "Profile",
                    unsupportedLength ? "Invalid DP length" : "Invalid profile",
                    detail,
                    "#F97316"));
        }

        ImageInitialization initialization = composition.Plan.OutputInitialization;
        string initializedState = initialization.Kind == ImageInitializationKind.Blank
            ? $"Blank output 0x{initialization.FillByte:X2}"
            : $"Reference {initialization.ReferenceSpaceId}";
        List<WorkbenchMemoryMapRow> rows =
        [
            new(
                FormatFullRange(initialization.Capacity),
                "No output",
                "Initialize",
                initializedState,
                FormatStandardMergeInitializationDetail(icId, lengthPending: false)),
        ];
        string initializationLabel = initialization.Kind == ImageInitializationKind.Blank
            ? $"Blank 0x{initialization.FillByte:X2}"
            : $"Reference {initialization.ReferenceSpaceId}";
        CoverageSegment[] coverage =
        [
            new(
                new ByteRange(0, initialization.Capacity),
                initializationLabel,
                "No source input writes this output range.",
                "#CBD5E1",
                false,
                WorkbenchMemoryCoverageRole.Standard),
        ];

        foreach (CompositionOperation operation in composition.Plan.OrderedOperations)
        {
            string afterSource = operation.SourceSpaceId is null
                ? operation.Kind.ToString()
                : AddressSpaceLabel(operation.SourceSpaceId);
            string sourceRange = operation.SourceRange is ByteRange source
                ? FormatDisplayRange(source)
                : "no source range";
            rows.Add(new WorkbenchMemoryMapRow(
                FormatDisplayRange(operation.TargetRange),
                initializedState,
                ActionLabel(operation.Kind),
                afterSource,
                $"Sequence {operation.Sequence}: {operation.Kind} {sourceRange} -> output image {FormatDisplayRange(operation.TargetRange)}. Reason: {operation.Reason}"));

            string coverageLabel = operation.SourceSpaceId is null
                ? ActionLabel(operation.Kind)
                : AddressSpaceLabel(operation.SourceSpaceId);
            string coverageDetail = operation.SourceRange is ByteRange range
                ? $"Copies source {FormatDisplayRange(range)} into this output range."
                : $"Operation {operation.OperationId}, sequence {operation.Sequence}.";
            coverage = ApplyCoverageWrite(
                coverage,
                new CoverageSegment(
                    operation.TargetRange,
                    coverageLabel,
                    coverageDetail,
                    CoverageFill(coverageLabel),
                    false,
                    WorkbenchMemoryCoverageRole.Standard));
        }

        return new WorkbenchMemoryDisplay(
            FormatFullRange(initialization.Capacity),
            rows,
            ToWorkbenchCoverageSegments(coverage, initialization.Capacity));
    }

    private static string FormatStandardMergeInitializationDetail(string icId, bool lengthPending)
    {
        return !TryGetBuiltInV2StandardMergeContainerPolicy(icId, out V2StandardMergeContainerPolicy? policy)
            ? "Start with the initialized image. Unlisted ranges keep this value until a later operation writes them."
            : lengthPending
                ? $"Start with the initialized image after selecting a DP BIN. Supported DP lengths are {BuiltInV2Bundle.FormatCapacities(policy.SupportedCapacities)}."
                : "Start with the initialized image using the selected DP BIN length. Unlisted ranges keep this value until a later operation writes them.";
    }
}

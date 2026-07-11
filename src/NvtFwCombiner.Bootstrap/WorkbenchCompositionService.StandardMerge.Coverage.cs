using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets final visual coverage segments for the selected Standard Merge profile.</summary>
    public static IReadOnlyList<WorkbenchMemoryCoverageSegment> GetStandardMergeCoverageSegments(string icId)
    {
        return GetStandardMergeCoverageSegments(icId, dpInputLength: null);
    }

    /// <summary>Gets final visual coverage segments for the selected Standard Merge profile and DP input length.</summary>
    public static IReadOnlyList<WorkbenchMemoryCoverageSegment> GetStandardMergeCoverageSegments(
        string icId,
        long? dpInputLength)
    {
        if (!StandardMergeProfilesByIc.TryGetValue(icId, out CompositionProfileDefinition? profile))
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

        if (IsDpPerspectiveLengthPending(profile, dpInputLength))
        {
            return
            [
                new WorkbenchMemoryCoverageSegment(
                    "Selected DP BIN length pending",
                    "DP length pending",
                    $"Select a DP BIN before final ownership is drawn. Supported DP lengths are {DpPerspectiveCatalog.FormatSupportedLengths()}.",
                    "#CBD5E1",
                    280,
                    false),
            ];
        }

        if (!TryResolveStandardMergeProfileForDisplay(profile, dpInputLength, out profile, out string profileIssue))
        {
            return
            [
                new WorkbenchMemoryCoverageSegment(
                    "Profile",
                    "Invalid DP length",
                    profileIssue,
                    "#F97316",
                    280,
                    false),
            ];
        }

        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        if (!compile.IsSuccess)
        {
            return
            [
                new WorkbenchMemoryCoverageSegment(
                    "Profile",
                    "Invalid profile",
                    FormatIssues(compile.Issues),
                    "#F97316",
                    280,
                    false),
            ];
        }

        CoverageSegment[] segments =
        [
            new CoverageSegment(
                new ByteRange(0, profile.Initialization.Capacity),
                $"Blank 0x{profile.Initialization.FillByte:X2}",
                "No source input writes this output range.",
                "#CBD5E1",
                false),
        ];

        foreach (CompositionOperation operation in compile.CompiledComposition!.Plan.OrderedOperations)
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
                WidthForRange(segment.Range, profile.Initialization.Capacity),
                false)),
        ];
    }
}

using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using static NvtFwCombiner.Bootstrap.WorkbenchMemoryDisplayProjection;

namespace NvtFwCombiner.Bootstrap;

public static partial class CompositionMemoryProjection
{
    /// <summary>Projects General Replace from the same occupancy/admission result used by execution.</summary>
    public static WorkbenchMemoryDisplay GetGeneralReplaceMemoryDisplay(
        long referenceCapacity,
        GeneralAuthoringAdmissionResult admission)
    {
        ArgumentNullException.ThrowIfNull(admission);
        return GetGeneralReplaceMemoryDisplayCore(referenceCapacity, admission, []);
    }

    /// <summary>Projects invalid editable mappings without falling back to another workflow.</summary>
    public static WorkbenchMemoryDisplay GetGeneralReplaceMemoryDisplay(
        long referenceCapacity,
        IReadOnlyList<AuthoringMappingState> authoringStates)
    {
        ArgumentNullException.ThrowIfNull(authoringStates);
        return GetGeneralReplaceMemoryDisplayCore(
            referenceCapacity,
            admission: null,
            [
                .. authoringStates
                    .Where(static state => state.Issue is not null)
                    .Select(static state => new CompositionIssue(
                        WorkbenchIssueCodes.GeneralReplaceRangeInvalid,
                        state.Issue!.Message,
                        state.MappingId)),
            ]);
    }

    private static WorkbenchMemoryDisplay GetGeneralReplaceMemoryDisplayCore(
        long referenceCapacity,
        GeneralAuthoringAdmissionResult? admission,
        IReadOnlyList<CompositionIssue> authoringIssues)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(referenceCapacity);
        List<WorkbenchMemoryMapRow> rows =
        [
            new(
                FormatFullRange(referenceCapacity),
                "Reference",
                "Kept",
                "Reference",
                "Unmapped bytes are kept from the immutable Reference image."),
        ];
        CoverageSegment[] segments =
        [
            new(
                new ByteRange(0, referenceCapacity),
                "Reference",
                "Kept from Reference.",
                false,
                WorkbenchMemoryCoverageRole.BaseFirmware),
        ];

        HashSet<string> blockedMappings =
        [
            .. admission?.Issues
                .Where(static issue => issue.Code != GeneralAuthoringIssueCodes.TargetIntersection)
                .SelectMany(static issue => issue.MappingIds) ?? [],
        ];
        foreach (GeneralOccupancySegment mapping in admission?.OccupancySegments.Where(mapping =>
                     !blockedMappings.Contains(mapping.MappingId)) ?? [])
        {
            string source = mapping.SourceKind switch
            {
                GeneralMappingSourceKind.FileArtifact => "File",
                GeneralMappingSourceKind.HexOverwrite => "Hex Overwrite",
                GeneralMappingSourceKind.HexFill => "Hex Fill",
                _ => throw new InvalidOperationException("Unknown General mapping source kind."),
            };
            string detail =
                $"{mapping.MappingId} will replace {FormatDisplayRange(mapping.TargetRange)} from {source}.";
            rows.Add(new WorkbenchMemoryMapRow(
                FormatDisplayRange(mapping.TargetRange),
                "Reference",
                "WillReplace",
                source,
                detail));
            segments = ApplyCoverageWrite(
                segments,
                new CoverageSegment(
                    mapping.TargetRange,
                    source,
                    detail,
                    true,
                    WorkbenchMemoryCoverageRole.Standard));
        }

        foreach (GeneralAuthoringAdmissionIssue issue in admission?.Issues ?? [])
        {
            string range = issue.Intersection is { } intersection
                ? FormatDisplayRange(intersection)
                : "General draft";
            rows.Add(new WorkbenchMemoryMapRow(
                range,
                "Authored mappings",
                "Error",
                "Blocked",
                issue.Message));
            if (issue.Intersection is { } exact)
            {
                segments = ApplyCoverageWrite(
                    segments,
                    new CoverageSegment(
                        exact,
                        "Overlap error",
                        issue.Message,
                        true,
                        WorkbenchMemoryCoverageRole.Standard));
            }
        }

        rows.AddRange(authoringIssues.Select(static issue => new WorkbenchMemoryMapRow(
            issue.OperationId is null ? "General draft" : $"Mapping {issue.OperationId}",
            "Authored mappings",
            "Error",
            "Blocked",
            issue.Message)));

        return new WorkbenchMemoryDisplay(
            FormatFullRange(referenceCapacity),
            rows,
            ToWorkbenchCoverageSegments(segments, referenceCapacity));
    }
}

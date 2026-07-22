using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets one coherent General Merge range, row, and coverage snapshot.</summary>
    public static WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplay(
        string outputLength,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs)
    {
        ArgumentNullException.ThrowIfNull(mappingInputs);

        if (!TryParseGeneralMergeCapacity(outputLength, out long capacity, out CompositionIssue? issue))
        {
            return CreateMessageDisplay(
                "Enter a valid output length",
                ("Output length", "No output", "Blocked", "No output", issue!.Message),
                ("Output length", "Pending", "Enter a valid General Merge output length.", "#CBD5E1"));
        }

        List<WorkbenchMemoryMapRow> rows =
        [
            new(
                FormatFullRange(capacity),
                "No output",
                "Initialize",
                $"Blank output 0x{GeneralMergeFillByte:X2}",
                "Start with a blank output image. Unmapped ranges remain reserved until an explicit mapping writes them."),
        ];
        CoverageSegment[] segments =
        [
            new CoverageSegment(
                new ByteRange(0, capacity),
                $"Blank 0x{GeneralMergeFillByte:X2}",
                "No source mapping writes this output range.",
                "#CBD5E1",
                false,
                WorkbenchMemoryCoverageRole.Standard),
        ];
        ByteRange outputRange = new(0, capacity);

        foreach (WorkbenchGeneralMergeMappingInput mapping in mappingInputs)
        {
            if (!TryParseGeneralMergeMapping(
                    mapping,
                    out ByteRange sourceRange,
                    out ByteRange targetRange,
                    out CompositionIssue parseIssue))
            {
                rows.Add(new WorkbenchMemoryMapRow(
                    $"Mapping {mapping.MappingId}",
                    "Pending",
                    "Blocked",
                    "No output",
                    parseIssue.Message));
                continue;
            }

            rows.Add(new WorkbenchMemoryMapRow(
                FormatDisplayRange(targetRange),
                "Reserved",
                "Copy",
                "Source BIN",
                $"Copy source {FormatDisplayRange(sourceRange)} from {GeneralMergeSourceLabel(mapping)} into output {FormatDisplayRange(targetRange)}."));

            if (!outputRange.Contains(targetRange))
            {
                continue;
            }

            segments = ApplyCoverageWrite(
                segments,
                new CoverageSegment(
                    targetRange,
                    "Source BIN",
                    $"Written by {mapping.MappingId} from {GeneralMergeSourceLabel(mapping)}.",
                    CoverageFill("Source BIN"),
                    true,
                    WorkbenchMemoryCoverageRole.Standard));
        }

        return new WorkbenchMemoryDisplay(
            FormatFullRange(capacity),
            rows,
            ToWorkbenchCoverageSegments(segments, capacity));
    }

    /// <summary>Runs General Merge preview or build through the admitted logical-output V2 profile.</summary>
    public static ValueTask<WorkbenchRunResult> RunGeneralMergeAsync(
        string icId,
        string outputLength,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        bool overwrite = true)
    {
        return RunGeneralMergeV2Async(
            icId,
            outputLength,
            mappingInputs,
            build,
            cancellationToken,
            outputPath,
            overwrite,
            progress: null);
    }

    /// <summary>Runs General Merge and publishes bounded Application-owned lifecycle phases.</summary>
    public static async ValueTask<WorkbenchRunResult> RunGeneralMergeWithProgressAsync(
        string icId,
        string outputLength,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null,
        bool overwrite = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(mappingInputs);
        ArgumentNullException.ThrowIfNull(progress);

        return await RunGeneralMergeV2Async(
            icId,
            outputLength,
            mappingInputs,
            build,
            cancellationToken,
            outputPath,
            overwrite,
            progress).ConfigureAwait(false);
    }
}

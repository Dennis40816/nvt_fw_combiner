using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets output address coverage text for a General Merge output length.</summary>
    public static string GetGeneralMergeMemoryRangeLabel(string outputLength)
    {
        return TryParseGeneralMergeCapacity(outputLength, out long capacity, out _)
            ? FormatFullRange(capacity)
            : "Enter a valid output length";
    }

    /// <summary>Gets readable memory-map rows for General Merge authoring state.</summary>
    public static IReadOnlyList<WorkbenchMemoryMapRow> GetGeneralMergeMemoryMapRows(
        string outputLength,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs)
    {
        ArgumentNullException.ThrowIfNull(mappingInputs);

        if (!TryParseGeneralMergeCapacity(outputLength, out long capacity, out CompositionIssue? issue))
        {
            return
            [
                new WorkbenchMemoryMapRow(
                    "Output length",
                    "No output",
                    "Blocked",
                    "No output",
                    issue!.Message),
            ];
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
        }

        return rows;
    }

    /// <summary>Gets visual coverage segments for General Merge authoring state.</summary>
    public static IReadOnlyList<WorkbenchMemoryCoverageSegment> GetGeneralMergeCoverageSegments(
        string outputLength,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs)
    {
        ArgumentNullException.ThrowIfNull(mappingInputs);

        if (!TryParseGeneralMergeCapacity(outputLength, out long capacity, out _))
        {
            return
            [
                new WorkbenchMemoryCoverageSegment(
                    "Output length",
                    "Pending",
                    "Enter a valid General Merge output length.",
                    "#CBD5E1",
                    280,
                    false),
            ];
        }

        CoverageSegment[] segments =
        [
            new CoverageSegment(
                new ByteRange(0, capacity),
                $"Blank 0x{GeneralMergeFillByte:X2}",
                "No source mapping writes this output range.",
                "#CBD5E1",
                false),
        ];

        ByteRange outputRange = new(0, capacity);
        foreach (WorkbenchGeneralMergeMappingInput mapping in mappingInputs)
        {
            if (!TryParseGeneralMergeMapping(mapping, out _, out ByteRange targetRange, out _) ||
                !outputRange.Contains(targetRange))
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
                    true));
        }

        return ToWorkbenchCoverageSegments(segments, capacity);
    }

    /// <summary>Runs General Merge preview or build through the application core.</summary>
    public static async ValueTask<WorkbenchRunResult> RunGeneralMergeAsync(
        string icId,
        string outputLength,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null,
        bool overwrite = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(mappingInputs);

        Dictionary<string, string> reportSlotPaths = CreateGeneralMergeReportSlotPaths(mappingInputs);
        string defaultOutputFileName = GetGeneralMergeDefaultOutputFileName(icId);
        if (!TryParseGeneralMergeCapacity(outputLength, out long capacity, out CompositionIssue? capacityIssue))
        {
            return CreateGeneralMergeReportRunResult(
                icId,
                reportSlotPaths,
                build,
                [],
                [capacityIssue!],
                defaultOutputFileName,
                succeeded: false);
        }

        if (mappingInputs.Count == 0)
        {
            return CreateGeneralMergeReportRunResult(
                icId,
                reportSlotPaths,
                build,
                [],
                [
                    new CompositionIssue(
                        WorkbenchIssueCodes.GeneralMergeMappingRequired,
                        "General Merge requires at least one explicit source-to-target mapping.",
                        IcWorkflowIds.GeneralMerge),
                ],
                defaultOutputFileName,
                succeeded: false);
        }

        if (!TryCreateGeneralMergeMappings(
                mappingInputs,
                out IReadOnlyList<ExplicitMapping> explicitMappings,
                out IReadOnlyList<AddressSpace> requestAddressSpaces,
                out IReadOnlyList<InputArtifactBinding> mappingBindings,
                out IReadOnlyList<CompositionIssue> mappingIssues))
        {
            return CreateGeneralMergeReportRunResult(
                icId,
                reportSlotPaths,
                build,
                CreateGeneralMergePlanningOperations(explicitMappings),
                mappingIssues,
                defaultOutputFileName,
                succeeded: false);
        }

        CompositionProfileDefinition profile = CreateGeneralMergeProfile(icId, capacity);
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(
            profile,
            explicitMappings,
            requestAddressSpaces);
        WorkbenchRunResult? compileFailure = !compile.IsSuccess
            ? CreateGeneralMergeReportRunResult(
                icId,
                reportSlotPaths,
                build,
                CreateGeneralMergePlanningOperations(explicitMappings),
                compile.Issues,
                profile.DefaultOutputFileName,
                succeeded: false)
            : null;
        return compileFailure ?? await RunCompiledCompositionAsync(
            GeneralMergeRunIdPrefix,
            profile,
            compile.Plan!,
            mappingBindings,
            mappingBindings[0].ArtifactId,
            build,
            outputPath,
            externalProcessor: null,
            icNumberSelection: null,
            overwrite: overwrite,
            cancellationToken).ConfigureAwait(false);
    }

}

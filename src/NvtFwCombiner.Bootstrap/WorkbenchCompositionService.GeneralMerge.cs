using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using static NvtFwCombiner.Bootstrap.WorkbenchMemoryDisplayProjection;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets one coherent General Merge range, row, and coverage snapshot.</summary>
    public static WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplay(
        string outputLength,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs)
    {
        ArgumentNullException.ThrowIfNull(mappingInputs);
        if (!TryParseGeneralMergeCapacity(
                outputLength,
                out _,
                out CompositionIssue? capacityIssue))
        {
            return CreateMessageDisplay(
                "Enter a valid output length",
                ("Output length", "No output", "Blocked", "No output", capacityIssue!.Message),
                ("Output length", "Pending", "Enter a valid General Merge output length.", "#CBD5E1"));
        }

        List<GeneralMergeDisplayMapping> displayMappings = [];
        foreach (WorkbenchGeneralMergeMappingInput input in mappingInputs)
        {
            _ = TryCreateGeneralMergeDraftRow(
                input,
                out GeneralMappingDraftRow? row,
                out CompositionIssue? issue);
            displayMappings.Add(new GeneralMergeDisplayMapping(
                input.MappingId,
                row,
                issue));
        }

        return GetGeneralMergeMemoryDisplayCore(outputLength, displayMappings);
    }

    /// <summary>Gets one coherent General Merge display from the canonical typed draft.</summary>
    public static WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplay(
        string outputLength,
        GeneralMappingDraftState mappingDraft)
    {
        ArgumentNullException.ThrowIfNull(mappingDraft);

        return GetGeneralMergeMemoryDisplayCore(
            outputLength,
            [
                .. mappingDraft.Rows.Select(static row =>
                    new GeneralMergeDisplayMapping(row.MappingId, row, null)),
            ]);
    }

    private static WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplayCore(
        string outputLength,
        IReadOnlyList<GeneralMergeDisplayMapping> displayMappings)
    {
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

        foreach (GeneralMergeDisplayMapping displayMapping in displayMappings)
        {
            if (displayMapping.Mapping is not { } mapping)
            {
                rows.Add(new WorkbenchMemoryMapRow(
                    $"Mapping {displayMapping.MappingId}",
                    "Pending",
                    "Blocked",
                    "No output",
                    displayMapping.Issue!.Message));
                continue;
            }

            rows.Add(new WorkbenchMemoryMapRow(
                FormatDisplayRange(mapping.TargetRange),
                "Reserved",
                "Copy",
                "Source BIN",
                $"Copy source {FormatDisplayRange(mapping.SourceRange)} from {GeneralMergeSourceLabel(mapping)} into output {FormatDisplayRange(mapping.TargetRange)}."));

            if (!outputRange.Contains(mapping.TargetRange))
            {
                continue;
            }

            segments = ApplyCoverageWrite(
                segments,
                new CoverageSegment(
                    mapping.TargetRange,
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

    private sealed record GeneralMergeDisplayMapping(
        string MappingId,
        GeneralMappingDraftRow? Mapping,
        CompositionIssue? Issue);

    /// <summary>Runs General Merge preview or build through the admitted logical-output V2 profile.</summary>
    public static ValueTask<WorkbenchRunResult> RunGeneralMergeAsync(
        string icId,
        string outputLength,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        ArgumentNullException.ThrowIfNull(mappingInputs);
        _ = TryCreateGeneralMergeDraft(
            mappingInputs,
            out GeneralMappingDraftState? draft,
            out IReadOnlyList<CompositionIssue> draftIssues);
        return RunGeneralMergeWithInitialInspectionAsync(
            icId,
            outputLength,
            draft,
            draftIssues,
            new AuthoringRevision(1),
            build,
            outputPath,
            progress: null,
            cancellationToken);
    }

    /// <summary>Runs General Merge from one canonical typed mapping draft.</summary>
    public static ValueTask<WorkbenchRunResult> RunGeneralMergeDraftAsync(
        string icId,
        string outputLength,
        GeneralMappingDraftState mappingDraft,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        ArgumentNullException.ThrowIfNull(mappingDraft);
        return RunGeneralMergeV2Async(
            icId,
            outputLength,
            mappingDraft,
            draftIssues: null,
            build,
            cancellationToken,
            outputPath,
            progress: null);
    }

    /// <summary>
    /// Ephemeral CLI/Saved Rule boundary: inspect once, then execute the exact
    /// content-bound draft through the strict General Merge runner.
    /// </summary>
    public static ValueTask<WorkbenchRunResult> RunGeneralMergeEphemeralDraftAsync(
        string icId,
        string outputLength,
        GeneralMappingDraftState mappingDraft,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        ArgumentNullException.ThrowIfNull(mappingDraft);
        return RunGeneralMergeWithInitialInspectionAsync(
            icId,
            outputLength,
            mappingDraft,
            draftIssues: null,
            new AuthoringRevision(1),
            build,
            outputPath,
            progress: null,
            cancellationToken);
    }

    /// <summary>Runs General Merge and publishes bounded Application-owned lifecycle phases.</summary>
    public static async ValueTask<WorkbenchRunResult> RunGeneralMergeWithProgressAsync(
        string icId,
        string outputLength,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(mappingInputs);
        ArgumentNullException.ThrowIfNull(progress);

        _ = TryCreateGeneralMergeDraft(
            mappingInputs,
            out GeneralMappingDraftState? draft,
            out IReadOnlyList<CompositionIssue> draftIssues);
        return await RunGeneralMergeWithInitialInspectionAsync(
            icId,
            outputLength,
            draft,
            draftIssues,
            new AuthoringRevision(1),
            build,
            outputPath,
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs General Merge from the exact content-bound draft returned by an
    /// earlier desktop Preview or explicit Reload/Rebind.
    /// </summary>
    public static ValueTask<WorkbenchRunResult> RunGeneralMergeAcceptedDraftWithProgressAsync(
        string icId,
        string outputLength,
        GeneralMappingDraftState acceptedMappingDraft,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(acceptedMappingDraft);
        ArgumentNullException.ThrowIfNull(progress);
        return RunGeneralMergeV2Async(
            icId,
            outputLength,
            acceptedMappingDraft,
            draftIssues: null,
            build,
            cancellationToken,
            outputPath,
            progress);
    }

    private static async ValueTask<WorkbenchRunResult>
        RunGeneralMergeWithInitialInspectionAsync(
            string icId,
            string outputLength,
            GeneralMappingDraftState? mappingDraft,
            IReadOnlyList<CompositionIssue>? draftIssues,
            AuthoringRevision inspectionRevision,
            bool build,
            string? outputPath,
            CompositionRunProgressFeed? progress,
            CancellationToken cancellationToken)
    {
        if (mappingDraft is not null &&
            draftIssues is not { Count: > 0 })
        {
            GeneralSelectedFileBindingResult accepted =
                await AcceptGeneralSelectedFilesAsync(
                    mappingDraft,
                    inspectionRevision,
                    cancellationToken)
                .ConfigureAwait(false);
            if (accepted.Succeeded)
            {
                mappingDraft = accepted.Draft;
            }
            else
            {
                draftIssues = accepted.Issues;
            }
        }

        return await RunGeneralMergeV2Async(
            icId,
            outputLength,
            mappingDraft,
            draftIssues,
            build,
            cancellationToken,
            outputPath,
            progress).ConfigureAwait(false);
    }
}

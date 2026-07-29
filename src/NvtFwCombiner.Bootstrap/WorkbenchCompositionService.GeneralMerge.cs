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
        return GetGeneralMergeMemoryDisplay(
            outputLength,
            outputFillByte: null,
            mappingInputs);
    }

    /// <summary>Gets one coherent General Merge snapshot with an explicit fill-byte selection.</summary>
    public static WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplay(
        string outputLength,
        string? outputFillByte,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs)
    {
        ArgumentNullException.ThrowIfNull(mappingInputs);
        bool isResolved = TryResolveGeneralMergeInitializer(
            outputLength,
            outputFillByte,
            out GeneralMergeOutputInitializer? initializer,
            out CompositionIssue? initializationIssue);
        return isResolved
            ? GetGeneralMergeMemoryDisplay(
                new WorkbenchGeneralMergeInitializer(initializer!),
                mappingInputs)
            : CreateMessageDisplay(
                "Enter a valid output length",
                ("Output initialization", "No output", "Blocked", "No output", initializationIssue!.Message),
                ("Output length", "Pending", "Enter a valid General Merge output length.", "#CBD5E1"));
    }

    /// <summary>Gets one coherent General Merge snapshot from an already resolved initializer.</summary>
    public static WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplay(
        WorkbenchGeneralMergeInitializer initializer,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs)
    {
        ArgumentNullException.ThrowIfNull(initializer);
        ArgumentNullException.ThrowIfNull(mappingInputs);
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

        return GetGeneralMergeMemoryDisplayCore(initializer.Value, displayMappings);
    }

    /// <summary>Gets one coherent General Merge display from the canonical typed draft.</summary>
    public static WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplay(
        GeneralMergeDraftState draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return GetGeneralMergeMemoryDisplayCore(
            draft.OutputInitializer,
            [
                .. draft.Mappings.Rows.Select(static row =>
                    new GeneralMergeDisplayMapping(row.MappingId, row, null)),
            ]);
    }

    private static WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplayCore(
        GeneralMergeOutputInitializer initializer,
        IReadOnlyList<GeneralMergeDisplayMapping> displayMappings)
    {
        long capacity = initializer.Capacity;
        List<WorkbenchMemoryMapRow> rows =
        [
            new(
                FormatFullRange(capacity),
                "No output",
                "Initialize",
                $"Blank output 0x{initializer.FillByte:X2}",
                $"Start with a blank 0x{initializer.FillByte:X2} output image. Unmapped ranges retain that fill until an explicit mapping writes them."),
        ];
        CoverageSegment[] segments =
        [
            new CoverageSegment(
                new ByteRange(0, capacity),
                $"Blank 0x{initializer.FillByte:X2}",
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
        return RunGeneralMergeAsync(
            icId,
            outputLength,
            outputFillByte: null,
            mappingInputs,
            build,
            cancellationToken,
            outputPath);
    }

    /// <summary>Runs General Merge with one shared capacity/fill validation contract.</summary>
    public static ValueTask<WorkbenchRunResult> RunGeneralMergeAsync(
        string icId,
        string outputLength,
        string? outputFillByte,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        ArgumentNullException.ThrowIfNull(mappingInputs);
        _ = TryResolveGeneralMergeInitializer(
            outputLength,
            outputFillByte,
            out GeneralMergeOutputInitializer? initializer,
            out CompositionIssue? initializationIssue);
        _ = TryCreateGeneralMergeDraft(
            mappingInputs,
            out GeneralMappingDraftState? mappings,
            out IReadOnlyList<CompositionIssue> draftIssues);
        GeneralMergeDraftState? draft =
            initializer is not null && mappings is not null
                ? new GeneralMergeDraftState(initializer, mappings)
                : null;
        return RunGeneralMergeV2Async(
            icId,
            draft,
            initializationIssue is null
                ? draftIssues
                : [initializationIssue, .. draftIssues],
            build,
            cancellationToken,
            outputPath,
            progress: null);
    }

    /// <summary>Runs General Merge from one canonical typed mapping draft.</summary>
    public static ValueTask<WorkbenchRunResult> RunGeneralMergeDraftAsync(
        string icId,
        GeneralMergeDraftState draft,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return RunGeneralMergeV2Async(
            icId,
            draft,
            draftIssues: null,
            build,
            cancellationToken,
            outputPath,
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
        string? outputPath = null)
    {
        return await RunGeneralMergeWithProgressAsync(
            icId,
            outputLength,
            outputFillByte: null,
            mappingInputs,
            build,
            progress,
            cancellationToken,
            outputPath).ConfigureAwait(false);
    }

    /// <summary>Runs General Merge with progress and the shared capacity/fill contract.</summary>
    public static async ValueTask<WorkbenchRunResult> RunGeneralMergeWithProgressAsync(
        string icId,
        string outputLength,
        string? outputFillByte,
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
            out GeneralMappingDraftState? mappings,
            out IReadOnlyList<CompositionIssue> draftIssues);
        _ = TryResolveGeneralMergeInitializer(
            outputLength,
            outputFillByte,
            out GeneralMergeOutputInitializer? initializer,
            out CompositionIssue? initializationIssue);
        GeneralMergeDraftState? draft =
            initializer is not null && mappings is not null
                ? new GeneralMergeDraftState(initializer, mappings)
                : null;
        return await RunGeneralMergeV2Async(
            icId,
            draft,
            initializationIssue is null
                ? draftIssues
                : [initializationIssue, .. draftIssues],
            build,
            cancellationToken,
            outputPath,
            progress).ConfigureAwait(false);
    }

    /// <summary>Runs General Merge with one already resolved initializer and progress contract.</summary>
    public static async ValueTask<WorkbenchRunResult> RunGeneralMergeInitializerWithProgressAsync(
        string icId,
        WorkbenchGeneralMergeInitializer initializer,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(initializer);
        ArgumentNullException.ThrowIfNull(mappingInputs);
        ArgumentNullException.ThrowIfNull(progress);

        _ = TryCreateGeneralMergeDraft(
            mappingInputs,
            out GeneralMappingDraftState? mappings,
            out IReadOnlyList<CompositionIssue> draftIssues);
        GeneralMergeDraftState? draft = mappings is null
            ? null
            : new GeneralMergeDraftState(initializer.Value, mappings);
        return await RunGeneralMergeV2Async(
            icId,
            draft,
            draftIssues,
            build,
            cancellationToken,
            outputPath,
            progress).ConfigureAwait(false);
    }
}

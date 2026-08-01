using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using static NvtFwCombiner.Bootstrap.WorkbenchMemoryDisplayProjection;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets one coherent General Merge range, row, and coverage snapshot.</summary>
    public static WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplay(
        string icId,
        string outputLength,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs)
    {
        return GetGeneralMergeMemoryDisplay(
            icId,
            outputLength,
            outputFillByte: null,
            mappingInputs);
    }

    /// <summary>Gets one coherent General Merge snapshot with an explicit fill-byte selection.</summary>
    public static WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplay(
        string icId,
        string outputLength,
        string? outputFillByte,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(mappingInputs);
        bool isResolved = TryResolveGeneralMergeInitializer(
            outputLength,
            outputFillByte,
            out GeneralMergeOutputInitializer? initializer,
            out CompositionIssue? initializationIssue);
        return isResolved
            ? GetGeneralMergeMemoryDisplay(
                icId,
                new WorkbenchGeneralMergeInitializer(initializer!),
                mappingInputs)
            : CreateMessageDisplay(
                "Enter a valid output length",
                ("Output initialization", "No output", "Blocked", "No output", initializationIssue!.Message),
                ("Output length", "Pending", "Enter a valid General Merge output length.", "#CBD5E1"));
    }

    /// <summary>Gets one coherent General Merge snapshot from an already resolved initializer.</summary>
    public static WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplay(
        string icId,
        WorkbenchGeneralMergeInitializer initializer,
        IReadOnlyList<WorkbenchGeneralMergeMappingInput> mappingInputs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
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

        return GetGeneralMergeMemoryDisplayCore(
            icId,
            initializer.Value,
            displayMappings);
    }

    /// <summary>Gets one coherent General Merge display from the canonical typed draft.</summary>
    public static WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplay(
        string icId,
        GeneralMergeDraftState draft)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(draft);
        return GetGeneralMergeMemoryDisplayCore(
            icId,
            draft.OutputInitializer,
            [
                .. draft.Mappings.Rows.Select(static row =>
                    new GeneralMergeDisplayMapping(row.MappingId, row, null)),
            ]);
    }

    private static WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplayCore(
        string icId,
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
        GeneralAuthoringAdmissionResult? admission =
            ResolveGeneralMergeDisplayAdmission(
                icId,
                displayMappings,
                capacity);
        Dictionary<string, GeneralAuthoringAdmissionIssue> blockersByMappingId =
            new(StringComparer.Ordinal);
        GeneralAuthoringAdmissionIssue[] draftBlockers = admission is null
            ? []
            :
            [
                .. admission.Issues.Where(static issue =>
                    issue.MappingIds.Count == 0),
            ];
        if (admission is not null)
        {
            foreach (GeneralAuthoringAdmissionIssue admissionIssue in admission.Issues)
            {
                foreach (string mappingId in admissionIssue.MappingIds)
                {
                    _ = blockersByMappingId.TryAdd(
                        mappingId,
                        admissionIssue);
                }
            }
        }

        rows.AddRange(draftBlockers.Select(static blocker =>
            new WorkbenchMemoryMapRow(
                "General draft",
                "Authored mappings",
                "Blocked",
                "No output",
                blocker.Message)));

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

            if (draftBlockers.Length > 0)
            {
                rows.Add(new WorkbenchMemoryMapRow(
                    FormatDisplayRange(mapping.TargetRange),
                    "Reserved",
                    "Blocked",
                    "No output",
                    draftBlockers[0].Message));
                continue;
            }

            if (blockersByMappingId.TryGetValue(
                    mapping.MappingId,
                    out GeneralAuthoringAdmissionIssue? blocker))
            {
                rows.Add(new WorkbenchMemoryMapRow(
                    FormatDisplayRange(mapping.TargetRange),
                    "Reserved",
                    "Blocked",
                    "No output",
                    blocker.Message));
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

    private static GeneralAuthoringAdmissionResult?
        ResolveGeneralMergeDisplayAdmission(
            string icId,
            IReadOnlyList<GeneralMergeDisplayMapping> displayMappings,
            long capacity)
    {
        GeneralMappingDraftRow[] validMappings =
        [
            .. displayMappings
                .Where(static item => item.Mapping is not null)
                .Select(static item => item.Mapping!),
        ];
        if (validMappings.Length == 0 ||
            validMappings.Select(static row => row.MappingId)
                .Distinct(StringComparer.Ordinal).Count() != validMappings.Length)
        {
            return null;
        }

        var draft = new GeneralMappingDraftState(validMappings);
        string parentId =
            BuiltInV2RegistrationRegistry.GeneralMergeByIc.TryGetValue(
                icId,
                out GeneralMergeV2CandidateRegistration? registration)
                ? registration.ProfileId
                : $"{icId}:general-merge-unavailable";
        return AdmitGeneralMappingDraft(
            draft,
            capacity,
            CreateCurrentGeneralTrustedParentPolicy(parentId, draft));
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
        return RunGeneralMergeWithInitialInspectionAsync(
            icId,
            draft,
            initializationIssue is null
                ? draftIssues
                : [initializationIssue, .. draftIssues],
            savedRulePolicy: null,
            new AuthoringRevision(1),
            build,
            outputPath,
            progress: null,
            cancellationToken);
    }

    /// <summary>
    /// Runs a canonical typed General Merge draft after binding its selected
    /// files to immutable content snapshots.
    /// </summary>
    public static ValueTask<WorkbenchRunResult> RunGeneralMergeDraftAsync(
        string icId,
        GeneralMergeDraftState draft,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        return RunGeneralMergeDraftAsync(
            icId,
            draft,
            savedRulePolicy: null,
            build,
            outputPath,
            cancellationToken);
    }

    /// <summary>
    /// Ephemeral CLI/Saved Rule boundary: inspect once, then execute the exact
    /// content-bound draft through the strict General Merge runner.
    /// </summary>
    public static ValueTask<WorkbenchRunResult> RunGeneralMergeEphemeralDraftAsync(
        string icId,
        GeneralMergeDraftState draft,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        return RunGeneralMergeDraftAsync(
            icId,
            draft,
            savedRulePolicy: null,
            build,
            outputPath,
            cancellationToken);
    }

    /// <summary>
    /// Runs a Saved Rule draft with its separate resource-narrowing authority.
    /// </summary>
    internal static ValueTask<WorkbenchRunResult> RunGeneralMergeEphemeralDraftAsync(
        string icId,
        GeneralMergeDraftState draft,
        GeneralSavedRuleResourcePolicy? savedRulePolicy,
        bool build,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        return RunGeneralMergeDraftAsync(
            icId,
            draft,
            savedRulePolicy,
            build,
            outputPath,
            cancellationToken);
    }

    private static ValueTask<WorkbenchRunResult> RunGeneralMergeDraftAsync(
        string icId,
        GeneralMergeDraftState draft,
        GeneralSavedRuleResourcePolicy? savedRulePolicy,
        bool build,
        string? outputPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return RunGeneralMergeWithInitialInspectionAsync(
            icId,
            draft,
            draftIssues: null,
            savedRulePolicy,
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
        return await RunGeneralMergeWithInitialInspectionAsync(
            icId,
            draft,
            initializationIssue is null
                ? draftIssues
                : [initializationIssue, .. draftIssues],
            savedRulePolicy: null,
            new AuthoringRevision(1),
            build,
            outputPath,
            progress,
            cancellationToken).ConfigureAwait(false);
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
        return await RunGeneralMergeWithInitialInspectionAsync(
            icId,
            draft,
            draftIssues,
            savedRulePolicy: null,
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
        WorkbenchGeneralMergeInitializer initializer,
        GeneralMappingDraftState acceptedMappingDraft,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(initializer);
        ArgumentNullException.ThrowIfNull(acceptedMappingDraft);
        ArgumentNullException.ThrowIfNull(progress);
        return RunGeneralMergeV2Async(
            icId,
            new GeneralMergeDraftState(
                initializer.Value,
                acceptedMappingDraft),
            draftIssues: null,
            savedRulePolicy: null,
            build,
            cancellationToken,
            outputPath,
            progress);
    }

    private static async ValueTask<WorkbenchRunResult>
        RunGeneralMergeWithInitialInspectionAsync(
            string icId,
            GeneralMergeDraftState? draft,
            IReadOnlyList<CompositionIssue>? draftIssues,
            GeneralSavedRuleResourcePolicy? savedRulePolicy,
            AuthoringRevision inspectionRevision,
            bool build,
            string? outputPath,
            CompositionRunProgressFeed? progress,
            CancellationToken cancellationToken)
    {
        if (draft is not null &&
            draftIssues is not { Count: > 0 })
        {
            GeneralSelectedFileBindingResult accepted =
                await AcceptGeneralSelectedFilesAsync(
                    draft.Mappings,
                    inspectionRevision,
                    cancellationToken)
                .ConfigureAwait(false);
            if (accepted.Succeeded)
            {
                draft = new GeneralMergeDraftState(
                    draft.OutputInitializer,
                    accepted.Draft!);
            }
            else
            {
                draftIssues = accepted.Issues;
            }
        }

        return await RunGeneralMergeV2Async(
            icId,
            draft,
            draftIssues,
            savedRulePolicy,
            build,
            cancellationToken,
            outputPath,
            progress).ConfigureAwait(false);
    }
}

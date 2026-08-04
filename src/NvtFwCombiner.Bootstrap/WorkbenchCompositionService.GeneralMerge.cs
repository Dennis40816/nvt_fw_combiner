using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using static NvtFwCombiner.Bootstrap.WorkbenchMemoryDisplayProjection;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets initializer-only feedback while no complete typed draft exists.</summary>
    public static WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplay(
        string icId,
        string outputLength,
        string? outputFillByte)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        bool isResolved = TryResolveGeneralMergeInitializer(
            outputLength,
            outputFillByte,
            out GeneralMergeOutputInitializer? initializer,
            out CompositionIssue? initializationIssue);
        return isResolved
            ? GetGeneralMergeMemoryDisplayCore(
                icId,
                initializer!,
                [])
            : CreateMessageDisplay(
                "Enter a valid output length",
                ("Output initialization", "No output", "Blocked", "No output", initializationIssue!.Message),
                ("Output length", "Pending", "Enter a valid General Merge output length.", "#CBD5E1"));
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
            ],
            GetGeneralMergeAuthoringAdmission(icId, draft));
    }

    /// <summary>Gets the canonical admission result shared by General Merge command and layout state.</summary>
    public static GeneralAuthoringAdmissionResult GetGeneralMergeAuthoringAdmission(
        string icId,
        GeneralMergeDraftState draft)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(draft);
        string parentId =
            BuiltInV2RegistrationRegistry.GeneralMergeByIc.TryGetValue(
                icId,
                out GeneralMergeV2CandidateRegistration? registration)
                    ? registration.ProfileId
                    : $"{icId}:general-merge-unavailable";
        return AdmitGeneralMappingDraft(
            draft.Mappings,
            draft.OutputInitializer.Capacity,
            CreateCurrentGeneralTrustedParentPolicy(parentId, draft.Mappings));
    }

    /// <summary>Projects already parsed editable states without reparsing Presentation text.</summary>
    public static WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplay(
        string icId,
        WorkbenchGeneralMergeInitializer initializer,
        IReadOnlyList<AuthoringMappingState> states,
        GeneralAuthoringAdmissionResult? admission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(initializer);
        ArgumentNullException.ThrowIfNull(states);
        return GetGeneralMergeMemoryDisplayCore(
            icId,
            initializer.Value,
            [
                .. states.Select(state => new GeneralMergeDisplayMapping(
                    state.MappingId,
                    state.Mapping,
                    state.Issue is null
                        ? null
                        : new CompositionIssue(
                            WorkbenchIssueCodes.GeneralMergeRangeInvalid,
                            $"General Merge mapping '{state.MappingId}' is invalid: {state.Issue.Message}",
                            state.MappingId))),
            ],
            admission);
    }

    private static WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplayCore(
        string icId,
        GeneralMergeOutputInitializer initializer,
        IReadOnlyList<GeneralMergeDisplayMapping> displayMappings,
        GeneralAuthoringAdmissionResult? suppliedAdmission = null)
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
        GeneralAuthoringAdmissionResult? admission = suppliedAdmission ??
            ResolveGeneralMergeDisplayAdmission(
                icId,
                displayMappings,
                initializer);
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
                    if (!blockersByMappingId.TryGetValue(mappingId, out GeneralAuthoringAdmissionIssue? current) ||
                        (current.Code == GeneralAuthoringIssueCodes.TargetIntersection &&
                         admissionIssue.Code != GeneralAuthoringIssueCodes.TargetIntersection))
                    {
                        blockersByMappingId[mappingId] = admissionIssue;
                    }
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
                    out GeneralAuthoringAdmissionIssue? blocker) &&
                blocker.Code != GeneralAuthoringIssueCodes.TargetIntersection)
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
                "WillWrite",
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

        foreach (GeneralAuthoringAdmissionIssue blocker in admission?.Issues.Where(
                     static issue =>
                         issue.Code == GeneralAuthoringIssueCodes.TargetIntersection &&
                         issue.Intersection is not null) ?? [])
        {
            ByteRange intersection = blocker.Intersection!.Value;
            rows.Add(new WorkbenchMemoryMapRow(
                FormatDisplayRange(intersection),
                "Authored mappings",
                "Error",
                "Overlap error",
                blocker.Message));
            segments = ApplyCoverageWrite(
                segments,
                new CoverageSegment(
                    intersection,
                    "Overlap error",
                    blocker.Message,
                    "#DC2626",
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
            GeneralMergeOutputInitializer initializer)
    {
        GeneralMappingDraftRow[] validMappings =
        [
            .. displayMappings
                .Where(static item => item.Mapping is not null)
                .Select(static item => item.Mapping!),
        ];
        return validMappings.Length == 0 ||
               validMappings.Select(static row => row.MappingId)
                   .Distinct(StringComparer.Ordinal).Count() != validMappings.Length
            ? null
            : GetGeneralMergeAuthoringAdmission(
                icId,
                new GeneralMergeDraftState(
                    initializer,
                    new GeneralMappingDraftState(validMappings)));
    }

    private sealed record GeneralMergeDisplayMapping(
        string MappingId,
        GeneralMappingDraftRow? Mapping,
        CompositionIssue? Issue);

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
        return RunGeneralMergeEphemeralDraftAsync(
            icId,
            draft,
            savedRulePolicy: null,
            build,
            cancellationToken,
            outputPath);
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

    /// <summary>
    /// Runs General Merge from the exact content-bound draft returned by an
    /// earlier desktop Preview or explicit Reload/Rebind.
    /// </summary>
    public static ValueTask<WorkbenchRunResult> RunGeneralMergeAcceptedSessionWithProgressAsync(
        string icId,
        ActiveSessionSnapshot acceptedSession,
        bool build,
        CompositionRunProgressFeed progress,
        CancellationToken cancellationToken,
        string? outputPath = null)
    {
        ResolvedCapability capability = RequireAcceptedCapability(
            acceptedSession,
            Profiles.IcWorkflowIds.GeneralMerge,
            icId,
            AuthoringDerivedResultKind.Validation);
        GeneralMergeDraftState draft = acceptedSession.DraftState as GeneralMergeDraftState ??
            throw new InvalidOperationException(
                "The accepted General Merge session has no exact typed draft.");
        return RunGeneralMergeV2Async(
            icId,
            draft,
            draftIssues: null,
            savedRulePolicy: null,
            build,
            cancellationToken,
            outputPath,
            progress,
            capability);
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
                await InspectGeneralSelectedFilesAsync(
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

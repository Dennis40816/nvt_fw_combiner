using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using static NvtFwCombiner.Bootstrap.WorkbenchMemoryDisplayProjection;

namespace NvtFwCombiner.Bootstrap;

public static partial class CompositionMemoryProjection
{
    /// <summary>Gets initializer-only feedback while no complete typed draft exists.</summary>
    public static WorkbenchMemoryDisplay GetGeneralMergeMemoryDisplay(
        string icId,
        string outputLength,
        string? outputFillByte)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        bool isResolved = CanonicalAuthoringAdapter.TryResolveGeneralMergeInitializer(
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
                ("Output length", "Pending", "Enter a valid General Merge output length."));
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
            CanonicalAuthoringAdapter.GetGeneralMergeAuthoringAdmission(icId, draft));
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
            : CanonicalAuthoringAdapter.GetGeneralMergeAuthoringAdmission(
                icId,
                new GeneralMergeDraftState(
                    initializer,
                    new GeneralMappingDraftState(validMappings)));
    }

    private sealed record GeneralMergeDisplayMapping(
        string MappingId,
        GeneralMappingDraftRow? Mapping,
        CompositionIssue? Issue);

}

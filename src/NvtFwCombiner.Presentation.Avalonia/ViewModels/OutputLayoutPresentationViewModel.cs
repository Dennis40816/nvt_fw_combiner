// Copyright (c) 2026 Dennis Liu. All rights reserved.
using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Publishes the panel and supporting display facts together from one memory-layout revision.</summary>
internal sealed class OutputLayoutPresentationViewModel : ObservableObject
{
    // Merge serializes preparation with its gate; Replace publishes on the UI thread.
    private OutputLayoutPublication _current = new(string.Empty, [], [], [], null, null);

    /// <summary>Gets the complete display; consumers observe one replacement after preparation finishes.</summary>
    public OutputLayoutPublication Current => _current;

    /// <summary>Adopts one complete display after the page has prepared every dependent fact.</summary>
    internal void Publish(OutputLayoutPublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        _ = SetProperty(ref _current, publication, nameof(Current));
    }

    internal void Publish(
        PresentationCompositionServices services,
        ActiveSessionSnapshot? authoring,
        ShellTextResources text,
        IEnumerable<FirmwareSlotViewModel> slots,
        MemoryPendingPrerequisite fallbackPrerequisite,
        GeneralAuthoringAdmissionResult? admission = null,
        IReadOnlyList<string>? requiredAddressSpaces = null,
        string summaryText = "",
        CapabilityActionAvailability? availability = null)
    {
        FirmwareSlotViewModel[] currentSlots = [.. slots];
        string rangeLabel;
        IReadOnlyList<MemoryMapRowViewModel> rows;
        IReadOnlyList<MemoryCoverageSegmentViewModel> coverageSegments;
        OutputMapModel? map = null;
        OutputLayoutNotice? notice = null;
        string capacityLabel = string.Empty;
        try
        {
            if (authoring?.ExactCapability is { } capability)
            {
                (rangeLabel, rows, coverageSegments) = UiCompositionRunner.GetMemoryDisplay(
                    services, authoring, text, out _, out MemoryLayoutSnapshot layout, admission);
                map = OutputMapProjection.Create(layout, authoring, capability.CompiledComposition.Plan.OutputSpaceId, text);
                capacityLabel = FormattableString.Invariant($"{layout.Capacity / 1024d:0.###} KiB");
            }
            else
            {
                (rangeLabel, rows, coverageSegments) = UiCompositionRunner.GetPendingMemoryDisplay(
                    text, currentSlots, fallbackPrerequisite, out MemoryLayoutPendingDisplay pending, authoring, requiredAddressSpaces);
                notice = new(pending.Readiness == MemoryLayoutReadiness.Blocked
                    ? OutputLayoutNoticeKind.Blocked : OutputLayoutNoticeKind.Waiting,
                    rangeLabel, rows[0].Detail);
            }
        }
        catch (MemoryLayoutDisplayProjectionException)
        {
            // A display failure drops only derived preview state; Build keeps its existing admission owner.
            rangeLabel = string.Empty;
            rows = [];
            coverageSegments = [];
            map = null;
            notice = new(OutputLayoutNoticeKind.Unavailable, text.MemoryLayoutUnavailableTitle, text.MemoryLayoutUnavailableDetail);
        }

        if (notice?.Kind != OutputLayoutNoticeKind.Unavailable && admission is { IsAdmitted: false } &&
            availability is { Blockers.Count: > 0 } && !coverageSegments.Any(static segment => segment.FillRole == MemoryCoverageFillRole.Conflict))
        {
            var blocker = IssueCardViewModel.FromBuildAvailability(availability, currentSlots, text, false, string.Empty);
            notice = new(OutputLayoutNoticeKind.Blocked, blocker.Subject, blocker.AutomationText);
        }

        // Logical items assign the interaction shared by the rail and legend before either can bind it.
        IReadOnlyList<MemoryCoverageLogicalItemViewModel> logicalItems =
            ReplaceRegionGroupBuilder.CreateLogicalItems(coverageSegments, text);
        IReadOnlyList<MemoryCoverageSegmentViewModel> coverageRows =
            Array.AsReadOnly([.. logicalItems.SelectMany(static item => item.Ranges)]);
        (string start, string end) = MemoryCoverageBarProjection.OuterAddresses(coverageSegments);
        int slotErrors = currentSlots.Count(static slot => slot.IsSemanticStateError);
        var panel = new OutputLayoutPanelModel(coverageSegments, capacityLabel, rangeLabel, start, end,
            coverageRows, currentSlots.Length > 0 ? currentSlots.Count(static slot => slot.IsInputInspectionWarning)
                : CountDiagnostics(coverageSegments, MemoryDiagnosticSeverity.Warning),
            admission is { Issues.Count: > 0 } ? admission.Issues.Count : slotErrors > 0 ? slotErrors
                : CountDiagnostics(coverageSegments, MemoryDiagnosticSeverity.Error),
            notice, text: text,
            rowOptions: coverageSegments.Where(static segment => segment.FillRole == MemoryCoverageFillRole.Conflict)
                .Select(static segment => new OutputLayoutRowOptions(OutputLayoutRowIdentity.For(segment), OutputLayoutGroupKind.Write)),
            summaryText: summaryText);
        var publication = new OutputLayoutPublication(
            rangeLabel,
            Array.AsReadOnly([.. rows]),
            Array.AsReadOnly([.. coverageSegments]),
            coverageRows,
            map,
            authoring?.AuthoringRevision,
            panel);
        Publish(publication);
    }

    private static int CountDiagnostics(IEnumerable<MemoryCoverageSegmentViewModel> segments, MemoryDiagnosticSeverity severity)
    {
        return segments.Where(segment => segment.DiagnosticSeverity == severity)
            .DistinctBy(static segment => segment.SourceSlotId ?? segment.LogicalCoverageGroupId, StringComparer.Ordinal).Count();
    }
}

/// <summary>One prepared display, including pending or blocked details when geometry is unavailable.</summary>
internal sealed record OutputLayoutPublication(
    string RangeLabel,
    IReadOnlyList<MemoryMapRowViewModel> Rows,
    IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments,
    IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageRows,
    OutputMapModel? Map,
    AuthoringRevision? Revision,
    OutputLayoutPanelModel? Panel = null)
{
    public string StartAddress => MemoryCoverageBarProjection.OuterAddresses(CoverageSegments).Start;

    public string EndAddress => MemoryCoverageBarProjection.OuterAddresses(CoverageSegments).End;
}

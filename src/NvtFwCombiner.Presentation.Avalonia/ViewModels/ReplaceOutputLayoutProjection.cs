// Copyright (c) 2026 Dennis Liu. All rights reserved.
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Typed display preparation; pending, available and unavailable geometry share one lifetime.</summary>
internal sealed record ReplaceMemoryDisplayContext(
    MemoryLayoutSnapshot? Layout = null, MemoryLayoutPendingDisplay? Pending = null, bool IsUnavailable = false,
    MemoryLayoutBankLocator? Viewport = null);

/// <summary>Copies one prepared Replace display into the same atomic panel publication used by Merge.</summary>
internal static class ReplaceOutputLayoutProjection
{
    internal static OutputLayoutPublication Create(
        ReplaceMemoryDisplayContext context, ShellTextResources text, string rangeLabel,
        IEnumerable<MemoryMapRowViewModel> rows,
        IEnumerable<MemoryCoverageSegmentViewModel> coverage,
        IEnumerable<MemoryCoverageSegmentViewModel> overview,
        IEnumerable<MemoryFocusLaneViewModel> focusLanes,
        bool useFocusLayout, OutputLayoutBankDisplay? bank,
        IEnumerable<FirmwareSlotViewModel> slots,
        GeneralAuthoringAdmissionResult? admission,
        CapabilityActionAvailability availability, string summary)
    {
        MemoryMapRowViewModel[] currentRows = [.. rows];
        MemoryCoverageSegmentViewModel[] segments = [.. coverage];
        MemoryCoverageSegmentViewModel[] rail = useFocusLayout ? [.. overview] : segments;
        MemoryFocusLaneViewModel[] lanes = useFocusLayout ? [.. focusLanes] : [];
        FirmwareSlotViewModel[] currentSlots = [.. slots];
        IReadOnlyList<MemoryCoverageSegmentViewModel> coverageRows = Array.AsReadOnly([
            .. ReplaceRegionGroupBuilder.CreateLogicalItems(segments, text).SelectMany(static item => item.Ranges),
        ]);
        OutputLayoutNotice? notice = Notice(context, text, rangeLabel, currentRows);
        if (notice is null && admission is { IsAdmitted: false } && availability.Blockers.Count > 0 &&
            !segments.Any(static segment => segment.FillRole == MemoryCoverageFillRole.Conflict))
        {
            IssueCardViewModel blocker = IssueCardViewModel.FromBuildAvailability(availability, currentSlots, text, false, string.Empty);
            notice = new(OutputLayoutNoticeKind.Blocked, blocker.Subject, blocker.AutomationText);
        }
        MemoryCoverageSegmentViewModel[] visibleRows = context.Viewport is not { } viewport ? segments :
            [.. segments.Where(segment => segment.AddressSpaceId == viewport.AddressSpaceId &&
                segment.RangeStart >= viewport.Range.Start && segment.RangeEndExclusive <= viewport.Range.EndExclusive)];
        (string start, string end) = MemoryCoverageBarProjection.OuterAddresses(rail);
        int slotErrors = currentSlots.Count(static slot => slot.IsSemanticStateError);
        var panel = new OutputLayoutPanelModel(rail,
            context.Layout is { } layout
                ? FormattableString.Invariant($"{(context.Viewport?.Range.Length ?? layout.Capacity) / 1024d:0.###} KiB")
                : string.Empty,
            rangeLabel, start, end, coverageRows,
            warningCount: currentSlots.Count(static slot => slot.IsInputInspectionWarning),
            errorCount: admission is { Issues.Count: > 0 } ? admission.Issues.Count : slotErrors,
            notice: notice, writeDisposition: MemoryWorkflowDisposition.WillReplace,
            text: text, sharedInputHint: SharedInputHint(lanes, text),
            rowOptions: visibleRows.Where(static segment => segment.FillRole == MemoryCoverageFillRole.Conflict)
                .Select(static segment => new OutputLayoutRowOptions(OutputLayoutRowIdentity.For(segment), OutputLayoutGroupKind.Write)),
            summaryText: summary, bank: notice is null ? bank : null,
            laneLinks: lanes.SelectMany(static lane => lane.Ranges.Select(range =>
                new OutputLayoutLaneLink(OutputLayoutRowIdentity.For(range), lane))),
            rowSegments: visibleRows);
        return new(rangeLabel, Array.AsReadOnly(currentRows), Array.AsReadOnly(segments), coverageRows,
            null, context.Layout?.AuthoringRevision, panel);
    }

    private static OutputLayoutNotice? Notice(ReplaceMemoryDisplayContext context, ShellTextResources text,
        string rangeLabel, IReadOnlyList<MemoryMapRowViewModel> rows)
    {
        if (context.IsUnavailable)
        {
            return new(OutputLayoutNoticeKind.Unavailable, text.MemoryLayoutUnavailableTitle, text.MemoryLayoutUnavailableDetail);
        }
        return context.Pending is { } pending ? new(
            pending.Readiness == MemoryLayoutReadiness.Blocked ? OutputLayoutNoticeKind.Blocked : OutputLayoutNoticeKind.Waiting,
            rangeLabel, rows.FirstOrDefault()?.Detail ?? string.Empty) : null;
    }

    private static string? SharedInputHint(IEnumerable<MemoryFocusLaneViewModel> lanes, ShellTextResources text)
    {
        // Preserve the existing shared-source projection, exposing absence before localization.
        string sources = string.Join(" / ", lanes.SelectMany(static lane => lane.Ranges)
            .Where(static range => range.IsSelectedForWrite && range.SourceSlotId is not null)
            .GroupBy(static range => range.SourceSlotId)
            .Where(static group => group.Select(static range => range.RegionGroup).Distinct().Count() > 1)
            .Select(static group => ShellTextResources.GetMemoryFocusLabel(group.First().CtrlRamRegionRole)).Distinct());
        return sources.Length == 0 ? null : text.FormatMemorySharedInputHint(sources);
    }
}

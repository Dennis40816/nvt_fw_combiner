// Copyright (c) 2026 Dennis Liu. All rights reserved.
using NvtFwCombiner.Application.MemoryLayout;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal enum OutputLayoutNoticeKind { Waiting, Blocked, Unavailable }
internal enum OutputLayoutGroupKind { Write, Kept, NotWritten }

/// <summary>Host-owned display state and complete reason; no readiness is inferred by the panel.</summary>
internal sealed record OutputLayoutNotice(OutputLayoutNoticeKind Kind, string Title, string Reason);

/// <summary>Optional host-owned display coordinates where the old segment has only formatted range text.</summary>
internal sealed record OutputLayoutDisplayRange(string Space, long Start, long EndExclusive);

/// <summary>Explicit absent-by-default display facts; never inferred from diagnostics or localized labels.</summary>
internal sealed record OutputLayoutRowOptions(OutputLayoutRowIdentity Identity,
    OutputLayoutGroupKind? Group = null, OutputLayoutDisplayRange? Placement = null);

/// <summary>Stable display identity, independent of localization, observed result and input hash.</summary>
internal sealed record OutputLayoutRowIdentity(string? Space, long? Start, long? End,
    string? Region, string? Slot, string? LogicalGroup)
{
    internal static OutputLayoutRowIdentity For(MemoryCoverageSegmentViewModel segment)
    {
        return new(segment.AddressSpaceId, segment.RangeStart, segment.RangeEndExclusive,
            segment.RegionId, segment.SourceSlotId, segment.LogicalCoverageGroupId);
    }
}

/// <summary>A numbered display run. Members retain their original details and physical geometry.</summary>
internal sealed record OutputLayoutPanelRow(int FirstPin, IReadOnlyList<MemoryCoverageSegmentViewModel> Members, OutputLayoutGroupKind Group)
{
    public int LastPin => FirstPin + Members.Count - 1;
    public string PinLabel => FirstPin == LastPin ? FormattableString.Invariant($"{FirstPin}") :
        FormattableString.Invariant($"{FirstPin}–{LastPin}");
    internal static OutputLayoutGroupKind? GroupFor(MemoryCoverageSegmentViewModel segment)
    {
        return segment.IsSelectedForWrite ? OutputLayoutGroupKind.Write :
        segment.Disposition == MemoryWorkflowDisposition.Kept ? OutputLayoutGroupKind.Kept :
        segment.Disposition == MemoryWorkflowDisposition.Blank ? OutputLayoutGroupKind.NotWritten : null;
    }
}

/// <summary>
/// Display-only input for the standalone panel. Pass the old map's segments/coverage rows and formatted
/// capacity/range/endpoints from one publication. Notices and issue counts are explicit host inputs.
/// Replace this snapshot, including localized segment strings, when the shell language changes.
/// </summary>
internal sealed class OutputLayoutPanelModel
{
    private readonly IReadOnlyDictionary<OutputLayoutRowIdentity, OutputLayoutRowOptions> _rowOptions;

    internal OutputLayoutPanelModel(IEnumerable<MemoryCoverageSegmentViewModel> segments,
        string capacityLabel = "", string rangeLabel = "", string startAddress = "", string endAddress = "",
        IEnumerable<MemoryCoverageSegmentViewModel>? coverageRows = null,
        int warningCount = 0, int errorCount = 0, OutputLayoutNotice? notice = null,
        MemoryWorkflowDisposition writeDisposition = MemoryWorkflowDisposition.WillWrite,
        ShellTextResources? text = null, string? sharedInputHint = null,
        IEnumerable<OutputLayoutRowOptions>? rowOptions = null)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentOutOfRangeException.ThrowIfNegative(warningCount);
        ArgumentOutOfRangeException.ThrowIfNegative(errorCount);
        if (notice is not null && !Enum.IsDefined(notice.Kind)) { throw new ArgumentOutOfRangeException(nameof(notice)); }
        if (writeDisposition is not (MemoryWorkflowDisposition.WillWrite or MemoryWorkflowDisposition.WillReplace))
        {
            throw new ArgumentOutOfRangeException(nameof(writeDisposition));
        }
        Segments = Array.AsReadOnly(segments.ToArray());
        CoverageRows = Array.AsReadOnly((coverageRows ?? []).ToArray());
        CapacityLabel = capacityLabel;
        RangeLabel = rangeLabel;
        StartAddress = startAddress;
        EndAddress = endAddress;
        WarningCount = warningCount;
        ErrorCount = errorCount;
        Notice = notice;
        WriteDisposition = writeDisposition;
        SharedInputHint = sharedInputHint;
        _rowOptions = (rowOptions ?? []).ToDictionary(static option => option.Identity);
        foreach (OutputLayoutRowOptions option in _rowOptions.Values)
        {
            if (option.Group is { } group && !Enum.IsDefined(group)) { throw new ArgumentOutOfRangeException(nameof(rowOptions)); }
            if (option.Placement is { } range && (string.IsNullOrWhiteSpace(range.Space) || range.Start < 0 || range.EndExclusive <= range.Start))
            {
                throw new ArgumentOutOfRangeException(nameof(rowOptions));
            }
        }
        Rows = CreateRows(text ?? ShellTextResources.For(ShellLanguage.English));
    }

    private System.Collections.ObjectModel.ReadOnlyCollection<OutputLayoutPanelRow> CreateRows(ShellTextResources text)
    {
        // Reuse the old rail's content runs and tiny-range policy, including diagnostic exceptions.
        MemoryCoverageSegmentViewModel[] ordered = [.. Segments.OrderBy(segment => RangeFor(segment) is null)
            .ThenBy(segment => RangeFor(segment)?.Space, StringComparer.Ordinal).ThenBy(segment => RangeFor(segment)?.Start)];
        IReadOnlyList<MemoryCoverageSegmentViewModel> display = MemoryCoverageBarProjection.CoalesceContent(ordered, text);
        var rows = new List<OutputLayoutPanelRow>();
        int pin = 1;
        foreach (MemoryCoverageBarItem item in MemoryCoverageBarProjection.Create(display))
        {
            MemoryCoverageSegmentViewModel[] primary = [.. item.Slices.Where(slice => slice.IsPrimaryContent && GroupFor(slice).HasValue)];
            if (primary.Length == 0 || GroupFor(primary[0]) is not { } commonGroup) { continue; }
            if (primary.Length == item.Slices.Count && primary.All(slice => GroupFor(slice) == commonGroup))
            {
                rows.Add(new(pin, Array.AsReadOnly(primary), commonGroup));
                pin += primary.Length;
            }
            else
            {
                foreach (MemoryCoverageSegmentViewModel slice in primary)
                {
                    if (GroupFor(slice) is { } group) { rows.Add(new(pin++, Array.AsReadOnly([slice]), group)); }
                }
            }
        }
        return rows.AsReadOnly();
    }

    public IReadOnlyList<MemoryCoverageSegmentViewModel> Segments { get; }
    public IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageRows { get; }
    public IReadOnlyList<OutputLayoutPanelRow> Rows { get; }
    public string CapacityLabel { get; }
    public string RangeLabel { get; }
    public string StartAddress { get; }
    public string EndAddress { get; }
    public int WarningCount { get; }
    public int ErrorCount { get; }
    public OutputLayoutNotice? Notice { get; }
    public MemoryWorkflowDisposition WriteDisposition { get; }
    public string? SharedInputHint { get; }

    private OutputLayoutGroupKind? GroupFor(MemoryCoverageSegmentViewModel segment)
    {
        return _rowOptions.GetValueOrDefault(OutputLayoutRowIdentity.For(segment))?.Group ?? OutputLayoutPanelRow.GroupFor(segment);
    }

    internal OutputLayoutDisplayRange? RangeFor(MemoryCoverageSegmentViewModel segment)
    {
        return segment.RangeStart is { } start && segment.RangeEndExclusive is { } end && segment.AddressSpaceId is { Length: > 0 } space
            ? new(space, start, end) : _rowOptions.GetValueOrDefault(OutputLayoutRowIdentity.For(segment))?.Placement;
    }
}

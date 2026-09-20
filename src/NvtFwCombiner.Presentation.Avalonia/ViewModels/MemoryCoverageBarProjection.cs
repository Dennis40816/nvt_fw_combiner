using NvtFwCombiner.Application.MemoryLayout;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One proportional display item; a group keeps every original slice and its interaction.</summary>
internal sealed class MemoryCoverageBarItem(IReadOnlyList<MemoryCoverageSegmentViewModel> slices)
{
    public IReadOnlyList<MemoryCoverageSegmentViewModel> Slices { get; } = slices;
    public double BarWidth { get; } = slices.Sum(static slice => slice.BarWidth);
    public bool IsGroup => Slices.Count > 1;
    public bool IsPrimaryContent => Slices[0].IsPrimaryContent;
    public string AddressSpace => Slices[0].AddressSpaceId ?? string.Empty;
    public string StartLabel => FormattableString.Invariant($"0x{Slices[0].RangeStart:X5}");
    public string EndLabel => FormattableString.Invariant($"0x{Slices[^1].RangeEndExclusive - 1:X5}");
    public string SizeLabel => FormattableString.Invariant($"{(Slices[^1].RangeEndExclusive - Slices[0].RangeStart) / 1024d:0.###} KiB");
}

/// <summary>Display-only reduction; never coalesces firmware facts or the supporting information rows.</summary>
internal static class MemoryCoverageBarProjection
{
    internal const double SmallSliceFraction = 0.02;

    /// <summary>Joins adjacent display ranges of one loaded BIN; raw/focus slices remain unchanged.</summary>
    internal static IReadOnlyList<MemoryCoverageSegmentViewModel> CoalesceContent(
        IReadOnlyList<MemoryCoverageSegmentViewModel> slices, ShellTextResources text)
    {
        var result = new List<MemoryCoverageSegmentViewModel>();
        for (int index = 0; index < slices.Count;)
        {
            var parts = new List<MemoryCoverageSegmentViewModel> { slices[index++] };
            while (index < slices.Count && parts[^1].IsPrimaryContent && slices[index].IsPrimaryContent &&
                !string.IsNullOrWhiteSpace(parts[^1].ContentArtifactIdentity) &&
                StringComparer.Ordinal.Equals(parts[^1].ContentArtifactIdentity, slices[index].ContentArtifactIdentity) &&
                slices[index].ImmediatelyFollows(parts[^1]))
            {
                parts.Add(slices[index++]);
            }
            result.Add(parts.Count == 1 ? parts[0] : ContentRun(parts, text));
        }
        return result.AsReadOnly();
    }

    private static MemoryCoverageSegmentViewModel ContentRun(
        List<MemoryCoverageSegmentViewModel> parts, ShellTextResources text)
    {
        MemoryCoverageSegmentViewModel first = parts[0];
        long start = first.RangeStart!.Value;
        long end = parts[^1].RangeEndExclusive!.Value;
        string address = FormattableString.Invariant($"0x{start:X5}-0x{end - 1:X5}");
        string length = FormattableString.Invariant($"len 0x{end - start:X}");
        bool partial = parts.Any(static part => part.IsSelectedForWrite) && parts.Any(static part => part.UsesKeptPattern);
        bool sameRole = parts.All(part => part.ContentRole == first.ContentRole && part.CtrlRamRegionRole == first.CtrlRamRegionRole);
        return new MemoryCoverageSegmentViewModel(
            $"{address} ({length})", Join(parts.Select(static part => part.SourceLabel)),
            Join(parts.Select(static part => part.Detail)), first.FillRole, parts.Sum(static part => part.BarWidth),
            disposition: parts.FirstOrDefault(static part => part.IsSelectedForWrite)?.Disposition ?? first.Disposition,
            observedChange: parts.Any(static part => part.IsChanged) ? MemoryObservedChange.Changed :
                parts.All(static part => part.ObservedChange == MemoryObservedChange.Unchanged) ? MemoryObservedChange.Unchanged : MemoryObservedChange.NotObserved,
            diagnosticSeverity: parts.Max(static part => part.DiagnosticSeverity),
            usesBaseFirmwarePattern: parts.All(static part => part.UsesKeptPattern),
            regionId: parts.All(part => part.RegionId == first.RegionId) ? first.RegionId : null,
            sourceSlotId: parts.All(part => part.SourceSlotId == first.SourceSlotId) ? first.SourceSlotId : null,
            text: text, regionGroup: parts.All(part => part.RegionGroup == first.RegionGroup) ? first.RegionGroup : ReplaceRegionGroup.Common,
            rangeStart: start, rangeEndExclusive: end, addressRangeLabel: address, lengthLabel: length,
            compactDetail: Join(parts.Select(static part => part.CompactDetail)),
            changeLabel: partial ? text.GetMemoryCoveragePartialReplaceLabel() : Join(parts.Select(static part => part.ChangeLabel)),
            contentRole: sameRole ? first.ContentRole : MemoryContentRole.General,
            ctrlRamRegionRole: sameRole ? first.CtrlRamRegionRole : CtrlRamRegionRole.Other,
            processingFacts: [.. parts.SelectMany(static part => part.ProcessingFacts).Distinct()],
            displayTitle: parts.All(part => part.DisplayTitle == first.DisplayTitle) ? first.DisplayTitle : Join(parts.Select(static part => part.SourceLabel)),
            addressSpaceId: first.AddressSpaceId,
            contentArtifactIdentity: first.ContentArtifactIdentity, displayParts: parts.AsReadOnly());
    }

    private static string Join(IEnumerable<string> values)
    {
        return string.Join(" / ", values.Distinct(StringComparer.Ordinal));
    }

    internal static IReadOnlyList<MemoryCoverageBarItem> Create(IReadOnlyList<MemoryCoverageSegmentViewModel> slices)
    {
        ArgumentNullException.ThrowIfNull(slices);
        double total = slices.Sum(static slice => slice.BarWidth);
        var result = new List<MemoryCoverageBarItem>();
        for (int index = 0; index < slices.Count;)
        {
            var group = new List<MemoryCoverageSegmentViewModel> { slices[index++] };
            while (index < slices.Count && IsSmall(group[^1], total) && IsSmall(slices[index], total) &&
                slices[index].ImmediatelyFollows(group[^1]))
            {
                group.Add(slices[index++]);
            }
            result.Add(new MemoryCoverageBarItem(group.AsReadOnly()));
        }
        return result.AsReadOnly();
    }

    private static bool IsSmall(MemoryCoverageSegmentViewModel slice, double total)
    {
        return slice.IsPrimaryContent && double.IsFinite(total) && total > 0 && slice.BarWidth > 0 &&
            slice.BarWidth / total < SmallSliceFraction &&
            !string.IsNullOrWhiteSpace(slice.AddressSpaceId) && slice.RangeStart is >= 0 &&
            slice.RangeEndExclusive > slice.RangeStart && !slice.HasAttentionDiagnostic;
    }
}

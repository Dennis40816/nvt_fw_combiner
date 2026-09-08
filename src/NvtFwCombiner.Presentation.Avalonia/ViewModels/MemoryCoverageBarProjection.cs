namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One proportional display item; a group keeps every original slice and its interaction.</summary>
internal sealed class MemoryCoverageBarItem(IReadOnlyList<MemoryCoverageSegmentViewModel> slices)
{
    public IReadOnlyList<MemoryCoverageSegmentViewModel> Slices { get; } = slices;
    public double BarWidth { get; } = slices.Sum(static slice => slice.BarWidth);
    public bool IsGroup => Slices.Count > 1;
    public string AddressSpace => Slices[0].AddressSpaceId ?? string.Empty;
    public string StartLabel => FormattableString.Invariant($"0x{Slices[0].RangeStart:X5}");
    public string EndLabel => FormattableString.Invariant($"0x{Slices[^1].RangeEndExclusive - 1:X5}");
    public string SizeLabel => FormattableString.Invariant($"{(Slices[^1].RangeEndExclusive - Slices[0].RangeStart) / 1024d:0.###} KiB");
}

/// <summary>Display-only reduction; never coalesces firmware facts or the supporting information rows.</summary>
internal static class MemoryCoverageBarProjection
{
    internal const double SmallSliceFraction = 0.02;

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
        return double.IsFinite(total) && total > 0 && slice.BarWidth > 0 &&
            slice.BarWidth / total < SmallSliceFraction &&
            !string.IsNullOrWhiteSpace(slice.AddressSpaceId) && slice.RangeStart is >= 0 &&
            slice.RangeEndExclusive > slice.RangeStart && !slice.HasAttentionDiagnostic;
    }
}

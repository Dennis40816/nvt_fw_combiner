// Copyright (c) 2026 Dennis Liu. All rights reserved.
namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>An opaque immutable lane snapshot, public so Avalonia hosts can bind the zoom input.</summary>
public sealed class CtrlRamZoomData
{
    internal CtrlRamZoomData(long start, long endExclusive, string positionLabel, string addressSpace,
        string rangeLabel, string sizeLabel, IEnumerable<CtrlRamZoomSlice> slices)
    {
        ArgumentNullException.ThrowIfNull(slices);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(endExclusive, start);
        Start = start;
        EndExclusive = endExclusive;
        PositionLabel = positionLabel;
        AddressSpace = addressSpace;
        RangeLabel = rangeLabel;
        SizeLabel = sizeLabel;
        Slices = Array.AsReadOnly(slices.Select(static slice => slice with
        {
            Fills = Array.AsReadOnly(slice.Fills.ToArray()),
        }).ToArray());
        long cursor = start;
        foreach (CtrlRamZoomSlice slice in Slices)
        {
            if (slice.Start < cursor || slice.EndExclusive <= slice.Start || slice.EndExclusive > endExclusive)
            {
                throw new ArgumentException("Display slices must be ordered within their lane.", nameof(slices));
            }
            cursor = slice.EndExclusive;
        }
    }

    internal long Start { get; }
    internal long EndExclusive { get; }
    internal string PositionLabel { get; }
    internal string AddressSpace { get; }
    internal string RangeLabel { get; }
    internal string SizeLabel { get; }
    internal IReadOnlyList<CtrlRamZoomSlice> Slices { get; }
}

/// <summary>Literal display facts; the host supplies identity, text and theme resource keys.</summary>
internal sealed record CtrlRamZoomSlice(long Start, long EndExclusive, string Label, string AccessibleName,
    string RangeLabel, string SizeLabel, string SourceLabel, string StateLabel, string Detail,
    IReadOnlyList<CtrlRamZoomFill> Fills);

/// <summary>A physical fill part retaining its supplied half-open coordinates and kept pattern.</summary>
internal sealed record CtrlRamZoomFill(long Start, long EndExclusive, string ResourceKey, bool IsKept);

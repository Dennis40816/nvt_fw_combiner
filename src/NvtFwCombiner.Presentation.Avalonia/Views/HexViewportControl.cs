using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using NvtFwCombiner.Presentation.Avalonia.HexViewport;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>Draws one bounded, always-read-only hexadecimal snapshot without source authority.</summary>
public sealed partial class HexViewportControl : Control
{
    private const double AddressWidth = 112;
    private const double AsciiWidth = 144;
    private const double ColumnGap = 4;
    private const double RowHeight = 25;
    private const int BytesPerRow = 16;

    private static readonly Typeface NormalTypeface = new(new FontFamily("Cascadia Mono, Consolas"));
    private static readonly Typeface StrongTypeface = new(
        new FontFamily("Cascadia Mono, Consolas"),
        FontStyle.Normal,
        FontWeight.SemiBold);

    internal static readonly StyledProperty<HexViewportSnapshot?> SnapshotProperty =
        AvaloniaProperty.Register<HexViewportControl, HexViewportSnapshot?>(nameof(Snapshot));

    internal static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<HexViewportControl, ICommand?>(nameof(InteractionCommand));

    internal static readonly StyledProperty<string> ComparisonRowLabelProperty =
        AvaloniaProperty.Register<HexViewportControl, string>(
            nameof(ComparisonRowLabel),
            "orig");

    private FormattedText[] _normalHex = [];
    private FormattedText[] _selectedHex = [];
    private FormattedText[] _changedHex = [];
    private FormattedText[] _structuralHex = [];
    private FormattedText[] _referenceHex = [];
    private FormattedText[] _referenceChangedHex = [];
    private FormattedText[] _referenceStructuralHex = [];
    private FormattedText[] _searchMatchHex = [];
    private FormattedText[] _normalAscii = [];
    private FormattedText[] _changedAscii = [];
    private FormattedText[] _referenceAscii = [];
    private FormattedText[] _referenceChangedAscii = [];
    private FormattedText[] _structuralAscii = [];
    private FormattedText[] _searchMatchAscii = [];

    internal long? HoveredAddress { get; private set; }

    /// <summary>Creates the low-allocation renderer and its source-neutral interaction surface.</summary>
    public HexViewportControl()
    {
        Focusable = true;
        ClipToBounds = true;
        PointerPressed += OnPointerPressed;
        DoubleTapped += OnDoubleTapped;
        KeyDown += OnKeyDown;
        InitializeThemePalette();
        InitializeHistoryFeedback();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new ControlAutomationPeer(this);
    }

    internal HexViewportSnapshot? Snapshot
    {
        get => GetValue(SnapshotProperty);
        set => SetValue(SnapshotProperty, value);
    }

    internal ICommand? InteractionCommand
    {
        get => GetValue(InteractionCommandProperty);
        set => SetValue(InteractionCommandProperty, value);
    }

    internal string ComparisonRowLabel
    {
        get => GetValue(ComparisonRowLabelProperty);
        set => SetValue(ComparisonRowLabelProperty, value);
    }

    internal event EventHandler<HexViewportInteractionEventArgs>? InteractionRequested;

    /// <summary>Draws the immutable visible window without creating child controls.</summary>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        EnsureThemePalette();
        if (Snapshot is not { } snapshot || Bounds.Width <= AddressWidth + AsciiWidth)
        {
            return;
        }

        double y = 0;
        var structuralSegments = new Dictionary<int, List<HexViewportStructuralVisualSegment>>();
        foreach (HexViewportRow row in snapshot.Rows)
        {
            CollectStructuralVisualSegments(structuralSegments, row, y);
            DrawCurrentRow(context, snapshot, row, y);
            y += RowHeight;
            if (IsComparisonRowVisible(snapshot, row))
            {
                DrawReferenceRow(context, row, y);
                y += RowHeight;
            }
        }

        DrawAsciiStructuralBlocks(context, structuralSegments);
    }

    internal bool TryGetCellBounds(long address, out Rect bounds)
    {
        bounds = default;
        if (Snapshot is not { } snapshot)
        {
            return false;
        }

        double y = 0;
        foreach (HexViewportRow row in snapshot.Rows)
        {
            long index = address - row.Address;
            if ((ulong)index < (ulong)row.Cells.Count)
            {
                bounds = GetCellRect((int)index, y);
                return true;
            }

            y += IsComparisonRowVisible(snapshot, row) ? RowHeight * 2 : RowHeight;
        }

        return false;
    }

    internal bool TryGetCellAt(Point point, out HexViewportCell cell, out Rect bounds)
    {
        return TryHitTest(point, out cell, out bounds);
    }

    internal bool TryGetAsciiCellAt(Point point, out HexViewportCell cell, out Rect bounds)
    {
        return TryHitTestAscii(point, out cell, out bounds);
    }

    internal bool TryGetStructuralBlockAt(Point point, out HexViewportCell cell, out Rect bounds)
    {
        return TryHitTestStructuralAscii(point, out cell, out bounds);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? 1080 : availableSize.Width;
        int displayRows = Snapshot is { } snapshot
            ? snapshot.Rows.Sum(row => IsComparisonRowVisible(snapshot, row) ? 2 : 1)
            : 1;
        return new Size(width, Math.Max(RowHeight, displayRows * RowHeight));
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == SnapshotProperty)
        {
            HoveredAddress = null;
            StartHistoryFeedback();
            InvalidateMeasure();
            InvalidateVisual();
        }
        else if (change.Property == ComparisonRowLabelProperty)
        {
            InvalidateVisual();
        }
    }

    private void DrawCurrentRow(DrawingContext context, HexViewportSnapshot snapshot, HexViewportRow row, double y)
    {
        bool isSelected = snapshot.SelectedAddress is long selected &&
                          selected >= row.Address &&
                          selected < row.Address + row.Cells.Count;
        IBrush? rowBrush = isSelected ? SelectedRowBrush : row.HasDataChanges ? ChangedRowBrush : null;
        if (rowBrush is not null)
        {
            DrawRoundedRectangle(context, rowBrush, null, new Rect(0, y, Bounds.Width, RowHeight), 0);
        }

        if (isSelected)
        {
            DrawRoundedRectangle(context, SelectedBrush, null, new Rect(0, y, AddressWidth, RowHeight), 3);
        }

        DrawText(context, FormatAddress(row.Address), isSelected ? SelectedTextBrush : NormalTextBrush, StrongTypeface, 4, y);
        if (row.HasDataChanges || row.HasStructuralBoundary)
        {
            DrawRoundedRectangle(
                context,
                ChangedMarkerBrush,
                null,
                new Rect(AddressWidth - 10, y + ((RowHeight - 5) / 2), 5, 5),
                3);
        }

        for (int index = 0; index < row.Cells.Count; index++)
        {
            DrawByte(context, snapshot, row.Cells[index], index, y, isReference: false);
        }

        DrawAscii(context, snapshot, row.Cells, y, isReference: false);
    }

    private void DrawReferenceRow(DrawingContext context, HexViewportRow row, double y)
    {
        DrawRoundedRectangle(context, ReferenceRowBrush, null, new Rect(0, y, Bounds.Width, RowHeight), 0);
        DrawRoundedRectangle(context, ReferenceMarkerBrush, null, new Rect(0, y + 2, 4, RowHeight - 4), 2);
        DrawText(context, FormatReferenceLabel(row.Address), ReferenceTextBrush, StrongTypeface, 4, y);
        for (int index = 0; index < row.Cells.Count; index++)
        {
            DrawByte(context, snapshot: null, row.Cells[index], index, y, isReference: true);
        }

        DrawAscii(context, snapshot: null, row.Cells, y, isReference: true);
    }

    private void DrawByte(
        DrawingContext context,
        HexViewportSnapshot? snapshot,
        HexViewportCell cell,
        int index,
        double y,
        bool isReference)
    {
        Rect rect = GetCellRect(index, y).Deflate(1);
        bool isSelected = !isReference && snapshot?.SelectedAddress == cell.Address;
        bool isHovered = !isReference && HoveredAddress == cell.Address;
        IBrush? background = ResolveCellBackground(cell, isReference, isSelected);
        IPen? pen = ResolveCellPen(cell, isReference, isSelected);
        if (background is not null || pen is not null)
        {
            DrawRoundedRectangle(context, background, pen, rect, 3);
        }

        HexViewportCellVisualState state = GetVisualState(cell, isSelected);
        DrawHoverOutline(context, rect, isReference, isHovered, state);
        DrawHistoryFeedback(context, cell, rect, isReference);

        byte? value = isReference ? cell.ComparisonValue : cell.PrimaryValue;
        FormattedText text = GetHexText(value, isSelected, cell, isReference);
        Point origin = new(
            rect.X + ((rect.Width - text.Width) / 2),
            rect.Y + ((rect.Height - text.Height) / 2));
        context.DrawText(text, origin);
    }

    private FormattedText GetHexText(byte? value, bool selected, HexViewportCell cell, bool reference)
    {
        return value is not byte parsed
            ? CreateText("--", reference ? ReferenceTextBrush : NormalTextBrush, NormalTypeface)
            : reference
                ? cell.IsDataChanged
                    ? _referenceChangedHex[parsed]
                    : cell.IsStructuralChanged
                        ? _referenceStructuralHex[parsed]
                        : _referenceHex[parsed]
                : selected
                    ? _selectedHex[parsed]
                    : cell.IsSearchMatch
                        ? _searchMatchHex[parsed]
                        : cell.IsDataChanged
                            ? _changedHex[parsed]
                            : cell.IsStructuralChanged
                                ? _structuralHex[parsed]
                                : _normalHex[parsed];
    }

    private IBrush? ResolveCellBackground(HexViewportCell cell, bool isReference, bool isSelected)
    {
        return (isReference, isSelected, cell.IsSearchMatch, cell.IsDataChanged) switch
        {
            (true, _, _, true) => ReferenceChangedBrush,
            (true, _, _, false) => null,
            (false, true, _, _) => SelectedBrush,
            (false, false, true, _) => SearchMatchBrush,
            (false, false, false, true) => ChangedBrush,
            _ => null,
        };
    }

    private IPen? ResolveCellPen(HexViewportCell cell, bool isReference, bool isSelected)
    {
        return (isReference, isSelected, cell.IsSearchMatch, cell.IsDataChanged) switch
        {
            (true, _, _, true) => ReferenceChangedPen,
            (true, _, _, false) => null,
            (false, true, _, _) => SelectedPen,
            (false, false, true, _) => SearchMatchPen,
            (false, false, false, true) => ChangedPen,
            _ => null,
        };
    }

    internal static bool ShouldDrawHoverOutline(
        bool isReference,
        bool isHovered,
        HexViewportCellVisualState visualState)
    {
        return !isReference && isHovered && visualState is
            HexViewportCellVisualState.Normal or
            HexViewportCellVisualState.Selected or
            HexViewportCellVisualState.Changed or
            HexViewportCellVisualState.SearchMatch or
            HexViewportCellVisualState.Structural;
    }

    private void DrawHoverOutline(
        DrawingContext context,
        Rect rect,
        bool isReference,
        bool isHovered,
        HexViewportCellVisualState visualState)
    {
        if (ShouldDrawHoverOutline(isReference, isHovered, visualState))
        {
            DrawRoundedRectangle(context, null, HoverPen, rect, 3);
        }
    }

    private static HexViewportCellVisualState GetVisualState(HexViewportCell cell, bool selected)
    {
        return selected
            ? HexViewportCellVisualState.Selected
            : cell.IsSearchMatch
                ? HexViewportCellVisualState.SearchMatch
                : cell.IsDataChanged
                    ? HexViewportCellVisualState.Changed
                    : cell.IsStructuralChanged
                        ? HexViewportCellVisualState.Structural
                        : HexViewportCellVisualState.Normal;
    }

    private void DrawAscii(
        DrawingContext context,
        HexViewportSnapshot? snapshot,
        ReadOnlyCollection<HexViewportCell> cells,
        double y,
        bool isReference)
    {
        if (!isReference)
        {
            DrawAsciiSearchRanges(context, cells, y);
        }

        for (int index = 0; index < cells.Count; index++)
        {
            HexViewportCell cell = cells[index];
            Rect rect = GetAsciiCellRect(index, y).Deflate(1);
            bool isSelected = !isReference && snapshot?.SelectedAddress == cell.Address;
            bool isSearchMatch = cell.IsSearchMatch && !isReference;
            bool isHovered = !isReference && HoveredAddress == cell.Address;
            if (isReference && cell.IsDataChanged)
            {
                DrawRoundedRectangle(context, ReferenceChangedBrush, ReferenceChangedPen, rect, 3);
            }
            else if (!isReference && !isSearchMatch && cell.IsDataChanged)
            {
                DrawRoundedRectangle(context, ChangedBrush, ChangedPen, rect, 3);
            }

            DrawHoverOutline(context, rect, isReference, isHovered, GetVisualState(cell, isSelected));
            DrawHistoryFeedback(context, cell, rect, isReference);

            byte? rawValue = isReference ? cell.ComparisonValue : cell.PrimaryValue;
            char value = ResolveAsciiCharacter(rawValue, isReference);
            FormattedText text = GetAsciiText(value, isReference, isSearchMatch, cell.IsDataChanged, cell.IsStructuralChanged);
            context.DrawText(
                text,
                new Point(
                    rect.X + ((rect.Width - text.Width) / 2),
                    rect.Y + ((rect.Height - text.Height) / 2)));
        }
    }

    internal static char ResolveAsciiCharacter(byte? value, bool isReference)
    {
        return isReference && value is null
            ? ' '
            : value is >= 0x20 and <= 0x7E ? (char)value.Value : '.';
    }

    private void DrawAsciiSearchRanges(DrawingContext context, ReadOnlyCollection<HexViewportCell> cells, double y)
    {
        int start = 0;
        while (start < cells.Count)
        {
            if (!cells[start].IsSearchMatch)
            {
                start++;
                continue;
            }

            int end = start + 1;
            while (end < cells.Count && cells[end].IsSearchMatch)
            {
                end++;
            }

            Rect first = GetAsciiCellRect(start, y).Deflate(1);
            Rect last = GetAsciiCellRect(end - 1, y).Deflate(1);
            DrawRoundedRectangle(
                context,
                SearchMatchBrush,
                SearchMatchPen,
                new Rect(first.X, first.Y, last.Right - first.X, first.Height),
                3);
            start = end;
        }
    }

    private FormattedText GetAsciiText(char value, bool reference, bool search, bool data, bool structural)
    {
        int index = value < 128 ? value : '.';
        return reference
            ? data ? _referenceChangedAscii[index] : structural ? _structuralAscii[index] : _referenceAscii[index]
            : search ? _searchMatchAscii[index] : data ? _changedAscii[index] : structural ? _structuralAscii[index] : _normalAscii[index];
    }

    private Rect GetCellRect(int index, double y)
    {
        double width = GetCellWidth();
        return new Rect(GetByteStart() + (index * width), y, width, RowHeight);
    }

    private Rect GetAsciiCellRect(int index, double y)
    {
        double width = Math.Max(1, (AsciiWidth - 4) / BytesPerRow);
        return new Rect(GetAsciiStart() + 2 + (index * width), y, width, RowHeight);
    }

    private static double GetByteStart()
    {
        return AddressWidth + ColumnGap;
    }

    private double GetAsciiStart()
    {
        return Bounds.Width - AsciiWidth;
    }

    private double GetCellWidth()
    {
        return Math.Max(1, (GetAsciiStart() - ColumnGap - GetByteStart()) / BytesPerRow);
    }

    private static bool IsComparisonRowVisible(HexViewportSnapshot snapshot, HexViewportRow row)
    {
        return snapshot.ShowComparisonRows && row.HasComparison;
    }

    private static string FormatAddress(long address)
    {
        return FormattableString.Invariant($"0x{address:X6}");
    }

}

internal enum HexViewportCellVisualState
{
    Normal,
    Selected,
    Changed,
    SearchMatch,
    Structural,
}

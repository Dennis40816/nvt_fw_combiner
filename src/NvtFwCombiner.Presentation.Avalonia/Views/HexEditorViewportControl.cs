using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>
/// Draws the bounded Hex Editor window as one visual. Byte interaction is resolved by coordinates,
/// avoiding hundreds of controls, bindings, and templates during document scrolling.
/// </summary>
public sealed partial class HexEditorViewportControl : Control
{
    private const double AddressWidth = 112;
    private const double AsciiWidth = 144;
    private const double ColumnGap = 4;
    private const double RowHeight = 25;
    private const int BytesPerRow = 16;

    private static readonly IBrush NormalTextBrush = Brush.Parse("#334155");
    private static readonly IBrush SecondaryTextBrush = Brush.Parse("#475569");
    private static readonly IBrush SelectedTextBrush = Brush.Parse("#1D4ED8");
    private static readonly IBrush ChangedTextBrush = Brush.Parse("#92400E");
    private static readonly IBrush StructuralTextBrush = Brush.Parse("#4338CA");
    private static readonly IBrush ReferenceTextBrush = Brush.Parse("#9D174D");
    private static readonly IBrush SearchMatchTextBrush = Brush.Parse("#166534");
    private static readonly IBrush SelectedBrush = Brush.Parse("#BFDBFE");
    private static readonly IBrush SelectedRowBrush = Brush.Parse("#E6F0FF");
    private static readonly IBrush ChangedBrush = Brush.Parse("#FEF3C7");
    private static readonly IBrush ChangedRowBrush = Brush.Parse("#FFFBEB");
    private static readonly IBrush ReferenceRowBrush = Brush.Parse("#FDF2F8");
    private static readonly IBrush ReferenceChangedBrush = Brush.Parse("#FBCFE8");
    private static readonly IBrush ReferenceMarkerBrush = Brush.Parse("#DB2777");
    private static readonly IBrush StructuralLabelBrush = Brush.Parse("#EEF2FF");
    private static readonly IBrush SearchMatchBrush = Brush.Parse("#DCFCE7");
    private static readonly IBrush ChangedMarkerBrush = Brush.Parse("#D97706");
    private static readonly IPen SelectedPen = new Pen(Brush.Parse("#2563EB"), 1);
    private static readonly IPen ChangedPen = new Pen(Brush.Parse("#FDE68A"), 1);
    private static readonly IPen StructuralPen = new Pen(Brush.Parse("#6366F1"), 1.5);
    private static readonly IPen HoverPen = new Pen(Brush.Parse("#0EA5E9"), 1.5);
    private static readonly IPen SearchMatchPen = new Pen(Brush.Parse("#86EFAC"), 1);
    private static readonly IPen ReferenceChangedPen = new Pen(Brush.Parse("#F472B6"), 1);
    private static readonly Typeface NormalTypeface = new(new FontFamily("Cascadia Mono, Consolas"));
    private static readonly Typeface StrongTypeface = new(
        new FontFamily("Cascadia Mono, Consolas"),
        FontStyle.Normal,
        FontWeight.SemiBold);

    private readonly FormattedText[] _normalHex = CreateHexTextCache(NormalTextBrush, NormalTypeface);
    private readonly FormattedText[] _selectedHex = CreateHexTextCache(SelectedTextBrush, StrongTypeface);
    private readonly FormattedText[] _changedHex = CreateHexTextCache(ChangedTextBrush, StrongTypeface);
    private readonly FormattedText[] _structuralHex = CreateHexTextCache(StructuralTextBrush, NormalTypeface);
    private readonly FormattedText[] _referenceHex = CreateHexTextCache(ReferenceTextBrush, NormalTypeface);
    private readonly FormattedText[] _referenceChangedHex = CreateHexTextCache(ReferenceTextBrush, StrongTypeface);
    private readonly FormattedText[] _referenceStructuralHex = CreateHexTextCache(StructuralTextBrush, NormalTypeface);
    private readonly FormattedText[] _searchMatchHex = CreateHexTextCache(SearchMatchTextBrush, StrongTypeface);
    private readonly FormattedText[] _normalAscii = CreateAsciiTextCache(SecondaryTextBrush, NormalTypeface);
    private readonly FormattedText[] _changedAscii = CreateAsciiTextCache(ChangedTextBrush, StrongTypeface);
    private readonly FormattedText[] _referenceAscii = CreateAsciiTextCache(ReferenceTextBrush, NormalTypeface);
    private readonly FormattedText[] _referenceChangedAscii = CreateAsciiTextCache(ReferenceTextBrush, StrongTypeface);
    private readonly FormattedText[] _structuralAscii = CreateAsciiTextCache(StructuralTextBrush, NormalTypeface);
    private readonly FormattedText[] _searchMatchAscii = CreateAsciiTextCache(SearchMatchTextBrush, StrongTypeface);
    private HexEditorWorkspaceViewModel? _workspace;
    private string? _hoveredAddress;

    /// <summary>Creates the low-cost viewport and hooks its single interaction surface.</summary>
    public HexEditorViewportControl()
    {
        Focusable = true;
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Arrow);
        DataContextChanged += (_, _) => AttachWorkspace(DataContext as HexEditorWorkspaceViewModel);
        PointerMoved += OnPointerMoved;
        PointerExited += OnPointerExited;
        PointerPressed += OnPointerPressed;
        DoubleTapped += OnDoubleTapped;
        KeyDown += OnKeyDown;
        InitializeHistoryFeedback();
    }

    /// <summary>Raised when direct editing should place the shared inline editor over one byte.</summary>
    public event EventHandler<HexEditorViewportCellEventArgs>? EditRequested;

    /// <summary>Raised when the shared byte context menu should open for one byte.</summary>
    public event EventHandler<HexEditorViewportCellEventArgs>? ContextMenuRequested;

    /// <summary>Raised when a structural ASCII outline requests head/tail navigation.</summary>
    public event EventHandler<HexEditorViewportCellEventArgs>? StructuralBlockContextMenuRequested;

    /// <summary>Raised by the source visual so wheel input never falls through to the shell page.</summary>
    public event EventHandler<HexEditorViewportScrollEventArgs>? ScrollRequested;

    /// <summary>Draws the current data window without creating child controls.</summary>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_workspace is not { } workspace || Bounds.Width <= AddressWidth + AsciiWidth)
        {
            return;
        }

        double y = 0;
        var structuralSegments = new Dictionary<int, List<HexEditorStructuralVisualSegment>>();
        foreach (HexEditorViewportRowViewModel row in workspace.ViewportRows)
        {
            CollectStructuralVisualSegments(structuralSegments, row, y);
            DrawCurrentRow(context, row, y);
            y += RowHeight;
            if (row.IsOriginalRowVisible)
            {
                DrawReferenceRow(context, row, y);
                y += RowHeight;
            }
        }

        DrawAsciiStructuralBlocks(context, structuralSegments);
    }

    /// <summary>Returns the current screen-relative byte rectangle for the shared inline editor.</summary>
    public bool TryGetCellBounds(HexEditorByteCellViewModel cell, out Rect bounds)
    {
        ArgumentNullException.ThrowIfNull(cell);
        bounds = default;
        if (_workspace is null)
        {
            return false;
        }

        double y = 0;
        foreach (HexEditorViewportRowViewModel row in _workspace.ViewportRows)
        {
            int index = IndexOfReference(row.Bytes, cell);
            if (index >= 0)
            {
                bounds = GetCellRect(index, y);
                return true;
            }

            y += RowHeight;
            if (row.IsOriginalRowVisible)
            {
                y += RowHeight;
            }
        }

        return false;
    }

    /// <summary>Resolves one byte from a point supplied by the transparent document hit-test surface.</summary>
    public bool TryGetCellAt(Point point, out HexEditorByteCellViewModel? cell, out Rect bounds)
    {
        return TryHitTest(point, out cell, out bounds);
    }

    /// <summary>Resolves the current-data byte represented by one visible ASCII character.</summary>
    public bool TryGetAsciiCellAt(Point point, out HexEditorByteCellViewModel? cell, out Rect bounds)
    {
        return TryHitTestAscii(point, out cell, out bounds);
    }

    /// <summary>Resolves any point inside a visible structural ASCII outline, including whitespace.</summary>
    public bool TryGetStructuralBlockAt(
        Point point,
        out HexEditorByteCellViewModel? cell,
        out Rect bounds)
    {
        return TryHitTestStructuralAscii(point, out cell, out bounds);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? 1080 : availableSize.Width;
        int displayRows = _workspace?.ViewportRows.Sum(row => row.IsOriginalRowVisible ? 2 : 1) ?? 1;
        return new Size(width, Math.Max(RowHeight, displayRows * RowHeight));
    }

    private void AttachWorkspace(HexEditorWorkspaceViewModel? workspace)
    {
        if (ReferenceEquals(_workspace, workspace))
        {
            return;
        }

        if (_workspace is not null)
        {
            _workspace.PropertyChanged -= OnWorkspacePropertyChanged;
            _workspace.ViewportRows.CollectionChanged -= OnViewportRowsChanged;
        }

        StopHistoryFeedback();

        _workspace = workspace;
        if (_workspace is not null)
        {
            _workspace.PropertyChanged += OnWorkspacePropertyChanged;
            _workspace.ViewportRows.CollectionChanged += OnViewportRowsChanged;
        }

        _hoveredAddress = null;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HexEditorWorkspaceViewModel.IsOriginalRowsVisible))
        {
            InvalidateMeasure();
        }

        if (e.PropertyName is nameof(HexEditorWorkspaceViewModel.HistoryFeedbackVersion))
        {
            StartHistoryFeedback();
        }

        if (e.PropertyName is nameof(HexEditorWorkspaceViewModel.SelectedByteAddress) or
            nameof(HexEditorWorkspaceViewModel.IsOriginalRowsVisible))
        {
            InvalidateVisual();
        }
    }

    private void OnViewportRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _hoveredAddress = null;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void DrawCurrentRow(DrawingContext context, HexEditorViewportRowViewModel row, double y)
    {
        Rect rowRect = new(0, y, Bounds.Width, RowHeight);
        IBrush? rowBrush = row.IsSelected
            ? SelectedRowBrush
            : row.HasDataChanges
                ? ChangedRowBrush
                : null;
        if (rowBrush is not null)
        {
            DrawRoundedRectangle(context, rowBrush, null, rowRect, 0);
        }

        if (row.IsSelected)
        {
            DrawRoundedRectangle(context, SelectedBrush, null, new Rect(0, y, AddressWidth, RowHeight), 3);
        }

        DrawText(context, row.Address, row.IsSelected ? SelectedTextBrush : NormalTextBrush, StrongTypeface, 4, y);
        if (row.HasDataChanges || row.HasStructuralBoundary)
        {
            DrawRoundedRectangle(
                context,
                ChangedMarkerBrush,
                null,
                new Rect(AddressWidth - 10, y + ((RowHeight - 5) / 2), 5, 5),
                3);
        }

        for (int index = 0; index < row.Bytes.Count; index++)
        {
            DrawByte(context, row.Bytes[index], index, y, isReference: false);
        }

        DrawAscii(context, row.Bytes, row.CurrentAscii, y, isReference: false);
    }

    private void DrawReferenceRow(DrawingContext context, HexEditorViewportRowViewModel row, double y)
    {
        DrawRoundedRectangle(context, ReferenceRowBrush, null, new Rect(0, y, Bounds.Width, RowHeight), 0);
        DrawRoundedRectangle(context, ReferenceMarkerBrush, null, new Rect(0, y + 2, 4, RowHeight - 4), 2);
        DrawText(context, FormatReferenceLabel(row.Address), ReferenceTextBrush, StrongTypeface, 4, y);
        for (int index = 0; index < row.OriginalBytes.Count; index++)
        {
            DrawByte(context, row.OriginalBytes[index], index, y, isReference: true);
        }

        DrawAscii(context, row.OriginalBytes, row.OriginalAscii, y, isReference: true);
    }

    private void DrawByte(
        DrawingContext context,
        HexEditorByteCellViewModel cell,
        int index,
        double y,
        bool isReference)
    {
        Rect rect = GetCellRect(index, y).Deflate(1);
        bool isHovered = !isReference && string.Equals(_hoveredAddress, cell.Address, StringComparison.Ordinal);
        IBrush? background = ResolveCellBackground(cell, isReference);
        IPen? pen = ResolveCellPen(cell, isReference);
        if (background is not null || pen is not null)
        {
            DrawRoundedRectangle(context, background, pen, rect, 3);
        }

        DrawHoverOutline(context, rect, isReference, isHovered, GetVisualState(cell));

        DrawHistoryFeedback(context, cell, rect, isReference);

        FormattedText text = GetHexText(
            cell.ValueHex,
            cell.IsSelected,
            cell.IsAsciiSearchMatch,
            cell.IsDataChanged,
            cell.IsStructuralChanged,
            isReference);
        Point origin = new(
            rect.X + ((rect.Width - text.Width) / 2),
            rect.Y + ((rect.Height - text.Height) / 2));
        context.DrawText(text, origin);
    }

    private FormattedText GetHexText(
        string value,
        bool selected,
        bool searchMatch,
        bool dataChanged,
        bool structuralChanged,
        bool reference)
    {
        return !byte.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte parsed)
            ? CreateText(value, reference ? ReferenceTextBrush : NormalTextBrush, NormalTypeface)
            : reference
                ? dataChanged
                    ? _referenceChangedHex[parsed]
                    : structuralChanged
                        ? _referenceStructuralHex[parsed]
                        : _referenceHex[parsed]
                : selected
                    ? _selectedHex[parsed]
                    : searchMatch
                        ? _searchMatchHex[parsed]
                        : dataChanged
                            ? _changedHex[parsed]
                            : structuralChanged
                                ? _structuralHex[parsed]
                                : _normalHex[parsed];
    }

    private static IBrush? ResolveCellBackground(
        HexEditorByteCellViewModel cell,
        bool isReference)
    {
        return (isReference, cell.IsSelected, cell.IsAsciiSearchMatch, cell.IsDataChanged) switch
        {
            (true, _, _, true) => ReferenceChangedBrush,
            (true, _, _, false) => null,
            (false, true, _, _) => SelectedBrush,
            (false, false, true, _) => SearchMatchBrush,
            (false, false, false, true) => ChangedBrush,
            _ => null,
        };
    }

    private static IPen? ResolveCellPen(
        HexEditorByteCellViewModel cell,
        bool isReference)
    {
        return (isReference, cell.IsSelected, cell.IsAsciiSearchMatch, cell.IsDataChanged) switch
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
        HexEditorCellVisualState visualState)
    {
        return (isReference, isHovered, visualState) switch
        {
            (false, true, HexEditorCellVisualState.Normal or
                HexEditorCellVisualState.Selected or
                HexEditorCellVisualState.Changed or
                HexEditorCellVisualState.SearchMatch or
                HexEditorCellVisualState.Structural) => true,
            _ => false,
        };
    }

    private static void DrawHoverOutline(
        DrawingContext context,
        Rect rect,
        bool isReference,
        bool isHovered,
        HexEditorCellVisualState visualState)
    {
        if (ShouldDrawHoverOutline(isReference, isHovered, visualState))
        {
            DrawRoundedRectangle(context, null, HoverPen, rect, 3);
        }
    }

    private static HexEditorCellVisualState GetVisualState(HexEditorByteCellViewModel cell)
    {
        return cell.IsSelected
            ? HexEditorCellVisualState.Selected
            : cell.IsAsciiSearchMatch
                ? HexEditorCellVisualState.SearchMatch
                : cell.IsDataChanged
                    ? HexEditorCellVisualState.Changed
                    : cell.IsStructuralChanged
                        ? HexEditorCellVisualState.Structural
                        : HexEditorCellVisualState.Normal;
    }

    private void DrawAscii(
        DrawingContext context,
        IReadOnlyList<HexEditorByteCellViewModel> cells,
        string ascii,
        double y,
        bool isReference)
    {
        if (!isReference)
        {
            DrawAsciiSearchRanges(context, cells, y);
        }

        for (int index = 0; index < cells.Count; index++)
        {
            HexEditorByteCellViewModel cell = cells[index];
            Rect rect = GetAsciiCellRect(index, y).Deflate(1);
            bool isSearchMatch = cell.IsAsciiSearchMatch && !isReference;
            bool isHovered = !isReference && string.Equals(
                _hoveredAddress,
                cell.Address,
                StringComparison.Ordinal);
            if (isReference && cell.IsDataChanged)
            {
                DrawRoundedRectangle(context, ReferenceChangedBrush, ReferenceChangedPen, rect, 3);
            }
            else if (!isReference && !isSearchMatch && cell.IsDataChanged)
            {
                DrawRoundedRectangle(context, ChangedBrush, ChangedPen, rect, 3);
            }

            DrawHoverOutline(context, rect, isReference, isHovered, GetVisualState(cell));

            DrawHistoryFeedback(context, cell, rect, isReference);

            char value = index < ascii.Length ? ascii[index] : ' ';
            FormattedText text = GetAsciiText(
                value,
                isReference,
                isSearchMatch,
                cell.IsDataChanged,
                cell.IsStructuralChanged);
            context.DrawText(
                text,
                new Point(
                    rect.X + ((rect.Width - text.Width) / 2),
                    rect.Y + ((rect.Height - text.Height) / 2)));
        }
    }

    private void DrawAsciiSearchRanges(
        DrawingContext context,
        IReadOnlyList<HexEditorByteCellViewModel> cells,
        double y)
    {
        int start = 0;
        while (start < cells.Count)
        {
            if (!cells[start].IsAsciiSearchMatch)
            {
                start++;
                continue;
            }

            int end = start + 1;
            while (end < cells.Count && cells[end].IsAsciiSearchMatch)
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

    private FormattedText GetAsciiText(
        char value,
        bool isReference,
        bool searchMatch,
        bool dataChanged,
        bool structuralChanged)
    {
        int index = value < 128 ? value : '.';
        return isReference
            ? dataChanged
                ? _referenceChangedAscii[index]
                : structuralChanged
                    ? _structuralAscii[index]
                    : _referenceAscii[index]
            : searchMatch
                ? _searchMatchAscii[index]
                : dataChanged
                    ? _changedAscii[index]
                    : structuralChanged
                        ? _structuralAscii[index]
                        : _normalAscii[index];
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

    private static int IndexOfReference(
        IReadOnlyList<HexEditorByteCellViewModel> cells,
        HexEditorByteCellViewModel target)
    {
        for (int index = 0; index < cells.Count; index++)
        {
            if (ReferenceEquals(cells[index], target))
            {
                return index;
            }
        }

        return -1;
    }

}

internal enum HexEditorCellVisualState
{
    Normal,
    Selected,
    Changed,
    SearchMatch,
    Structural,
}

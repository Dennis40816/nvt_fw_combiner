using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One visible row in the raw-BIN Hex Editor viewport.</summary>
public sealed partial class HexEditorViewportRowViewModel : ObservableObject
{
    /// <summary>Creates one authoring or optional original-reference row.</summary>
    public HexEditorViewportRowViewModel(
        string address,
        IReadOnlyList<HexEditorByteCellViewModel> bytes,
        IReadOnlyList<HexEditorByteCellViewModel> originalBytes,
        string originalAscii,
        string currentAscii,
        bool hasChanges,
        bool hasReferenceComparison)
    {
        Address = address;
        Bytes = bytes;
        OriginalBytes = originalBytes;
        OriginalAscii = originalAscii;
        CurrentAscii = currentAscii;
        HasChanges = hasChanges;
        HasReferenceComparison = hasReferenceComparison;
    }

    /// <summary>First displayed byte offset for the row.</summary>
    public string Address { get; }

    /// <summary>Fixed-width byte cells in display order.</summary>
    public IReadOnlyList<HexEditorByteCellViewModel> Bytes { get; }

    /// <summary>Read-only source-byte cells displayed below changed rows when the reference toggle is active.</summary>
    public IReadOnlyList<HexEditorByteCellViewModel> OriginalBytes { get; }

    /// <summary>ASCII view of the source document at the same displayed offsets.</summary>
    [ObservableProperty]
    public partial string OriginalAscii { get; set; }

    /// <summary>ASCII view of the current memory work buffer.</summary>
    [ObservableProperty]
    public partial string CurrentAscii { get; set; }

    /// <summary>True when the current row has bytes that differ from the loaded source offsets.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOriginalRowVisible))]
    public partial bool HasChanges { get; set; }

    /// <summary>True when one or more current values differ from their retained source identities.</summary>
    public bool HasDataChanges => Bytes.Any(cell => cell.IsDataChanged);

    /// <summary>True when insert/delete shifted one or more source-address identities.</summary>
    public bool HasStructuralChanges => Bytes.Any(cell => cell.IsStructuralChanged);

    /// <summary>True when this row contains a visible structural block endpoint.</summary>
    public bool HasStructuralBoundary => Bytes.Any(cell => cell.IsStructuralBoundary);

    /// <summary>True when this row contains a value edit or a structural block boundary worth comparing.</summary>
    public bool HasReferenceComparison { get; }

    /// <summary>Controls whether this row may expose its secondary original-data reference row.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOriginalRowVisible))]
    public partial bool IsOriginalRowsVisible { get; set; }

    /// <summary>True when an original-data reference line should be rendered below this changed working row.</summary>
    public bool IsOriginalRowVisible => IsOriginalRowsVisible && HasReferenceComparison;

    /// <summary>True when the selected byte belongs to this current-data row.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>One interactive current-data byte cell in the raw-BIN Hex Editor.</summary>
public sealed partial class HexEditorByteCellViewModel : ObservableObject
{
    /// <summary>Creates one hexadecimal byte cell.</summary>
    public HexEditorByteCellViewModel(
        string address,
        string originalHex,
        string valueHex,
        bool hasOriginalValue,
        bool isDataChanged,
        bool isStructuralChanged,
        bool isStructuralBoundaryStart,
        bool isStructuralBoundaryEnd,
        int structuralBlockIndex,
        string structuralBoundaryLabel,
        bool isReference,
        bool isAsciiSearchMatch)
    {
        Address = address;
        OriginalHex = originalHex;
        ValueHex = valueHex;
        HasOriginalValue = hasOriginalValue;
        IsDataChanged = isDataChanged;
        IsStructuralChanged = isStructuralChanged;
        IsStructuralBoundaryStart = isStructuralBoundaryStart;
        IsStructuralBoundaryEnd = isStructuralBoundaryEnd;
        StructuralBlockIndex = structuralBlockIndex;
        StructuralBoundaryLabel = structuralBoundaryLabel;
        IsReference = isReference;
        IsAsciiSearchMatch = isAsciiSearchMatch;
        EditValue = valueHex;
    }

    /// <summary>Absolute work-buffer address shown in the grid.</summary>
    public string Address { get; }

    /// <summary>Value from the opened source at this same display address, or two dashes past source end.</summary>
    [ObservableProperty]
    public partial string OriginalHex { get; set; }

    /// <summary>Current in-memory work-buffer value.</summary>
    [ObservableProperty]
    public partial string ValueHex { get; set; }

    /// <summary>True when this byte retains an identity from the opened source document.</summary>
    [ObservableProperty]
    public partial bool HasOriginalValue { get; set; }

    /// <summary>True when this byte value differs from its retained source identity.</summary>
    public bool IsDataChanged { get; }

    /// <summary>True when insert/delete shifted this byte's source-address identity.</summary>
    public bool IsStructuralChanged { get; }

    /// <summary>True at the first displayed byte of one structural changed block.</summary>
    public bool IsStructuralBoundaryStart { get; }

    /// <summary>True at the last displayed byte of one structural changed block.</summary>
    public bool IsStructuralBoundaryEnd { get; }

    /// <summary>Zero-based edited-block index for one shifted address, or -1 outside a structural block.</summary>
    public int StructuralBlockIndex { get; }

    /// <summary>One-based block number shared with the Edit blocks inspector.</summary>
    public string StructuralBoundaryLabel { get; }

    /// <summary>True when this cell is one visible endpoint of a structural block.</summary>
    public bool IsStructuralBoundary => IsStructuralBoundaryStart || IsStructuralBoundaryEnd;

    /// <summary>True when this byte belongs to an insert/delete address-offset block.</summary>
    public bool HasStructuralBlock => StructuralBlockIndex >= 0;

    /// <summary>True when value or source-address mapping differs.</summary>
    public bool IsChanged => IsDataChanged || IsStructuralChanged;

    /// <summary>True for the optional non-editable original-data reference row.</summary>
    public bool IsReference { get; }

    /// <summary>True when this working byte falls within the active printable-ASCII search result set.</summary>
    [ObservableProperty]
    public partial bool IsAsciiSearchMatch { get; set; }

    /// <summary>True when this is the active selected byte.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>True only while the byte uses the inline two-character editor.</summary>
    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    /// <summary>Current inline editor value.</summary>
    [ObservableProperty]
    public partial string EditValue { get; set; }

    /// <summary>True when this cell is editable current work-buffer data.</summary>
    public bool IsEditable => !IsReference;

    /// <summary>Accessible concise byte identity and changed-state text.</summary>
    public string AccessibleLabel => IsDataChanged
        ? $"{Address}: {OriginalHex} changed to {ValueHex}"
        : IsStructuralChanged
            ? $"{Address}: {ValueHex}, source address shifted"
            : $"{Address}: {ValueHex}";
}

/// <summary>One stable relative hexadecimal column label.</summary>
public sealed partial class HexEditorColumnHeaderViewModel : ObservableObject
{
    /// <summary>Creates one byte-offset column label.</summary>
    public HexEditorColumnHeaderViewModel(int index)
    {
        Index = index;
        Label = index.ToString("X2", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Zero-based byte offset within a displayed row.</summary>
    public int Index { get; }

    /// <summary>Two-digit hexadecimal label.</summary>
    public string Label { get; }

    /// <summary>True when the selected byte has this row-relative offset.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One visible row in the raw-BIN Hex Editor viewport.</summary>
public sealed partial class HexEditorViewportRowViewModel : ObservableObject
{
    /// <summary>Creates one authoring or optional original-reference row.</summary>
    public HexEditorViewportRowViewModel(
        string address,
        IReadOnlyList<HexEditorByteCellViewModel> bytes,
        string originalAscii,
        string currentAscii,
        bool isReferenceRow,
        bool hasChanges)
    {
        Address = address;
        Bytes = bytes;
        OriginalAscii = originalAscii;
        CurrentAscii = currentAscii;
        IsReferenceRow = isReferenceRow;
        HasChanges = hasChanges;
    }

    /// <summary>First displayed byte offset for the row.</summary>
    public string Address { get; }

    /// <summary>Fixed-width byte cells in display order.</summary>
    public IReadOnlyList<HexEditorByteCellViewModel> Bytes { get; }

    /// <summary>ASCII view of the source document at the same displayed offsets.</summary>
    [ObservableProperty]
    public partial string OriginalAscii { get; set; }

    /// <summary>ASCII view of the current memory work buffer.</summary>
    [ObservableProperty]
    public partial string CurrentAscii { get; set; }

    /// <summary>True for the optional original-data reference row.</summary>
    public bool IsReferenceRow { get; }

    /// <summary>True when the current row has bytes that differ from the loaded source offsets.</summary>
    [ObservableProperty]
    public partial bool HasChanges { get; set; }

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
        bool isChanged,
        bool isReference)
    {
        Address = address;
        OriginalHex = originalHex;
        ValueHex = valueHex;
        HasOriginalValue = hasOriginalValue;
        IsChanged = isChanged;
        IsReference = isReference;
        EditValue = valueHex;
    }

    /// <summary>Absolute work-buffer address shown in the grid.</summary>
    public string Address { get; }

    /// <summary>Source value at the same displayed address, or two dashes when the source ended earlier.</summary>
    [ObservableProperty]
    public partial string OriginalHex { get; set; }

    /// <summary>Current in-memory work-buffer value.</summary>
    [ObservableProperty]
    public partial string ValueHex { get; set; }

    /// <summary>True when the source document had a byte at this displayed address.</summary>
    [ObservableProperty]
    public partial bool HasOriginalValue { get; set; }

    /// <summary>True when this byte differs from the loaded source at the same address.</summary>
    [ObservableProperty]
    public partial bool IsChanged { get; set; }

    /// <summary>True for the optional non-editable original-data reference row.</summary>
    public bool IsReference { get; }

    /// <summary>True when this is the active selected byte.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>True only while the byte uses the inline two-character editor.</summary>
    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    /// <summary>Current inline editor value.</summary>
    [ObservableProperty]
    public partial string EditValue { get; set; }

    /// <summary>Inline validation text, shown only after a rejected direct edit.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInlineValidationMessage))]
    public partial string InlineValidationMessage { get; set; } = string.Empty;

    /// <summary>True when the inline editor should render its validation state.</summary>
    public bool HasInlineValidationMessage => !string.IsNullOrWhiteSpace(InlineValidationMessage);

    /// <summary>True when this cell is editable current work-buffer data.</summary>
    public bool IsEditable => !IsReference;

    /// <summary>Accessible concise byte identity and changed-state text.</summary>
    public string AccessibleLabel => IsChanged
        ? $"{Address}: {OriginalHex} changed to {ValueHex}"
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

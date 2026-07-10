using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One visible 16-byte hexadecimal editor row.</summary>
public sealed record GeneralReplaceHexViewportRowViewModel(
    string Address,
    IReadOnlyList<GeneralReplaceHexByteCellViewModel> Bytes,
    string BeforeAscii,
    string AfterAscii,
    bool IsReferenceRow,
    bool HasChanges)
{
    /// <summary>True only when the virtual staged row differs from the immutable base-reference row.</summary>
    public bool IsEditedRow => !IsReferenceRow && HasChanges;
}

/// <summary>One selectable byte cell in the General Replace hexadecimal editor.</summary>
public sealed partial class GeneralReplaceHexByteCellViewModel : ObservableObject
{
    /// <summary>Creates one byte cell backed by immutable base and virtual staged values.</summary>
    public GeneralReplaceHexByteCellViewModel(
        string address,
        string beforeHex,
        string valueHex,
        bool isChanged,
        bool isSelected,
        bool isReference,
        string editMenuLabel,
        string rangeStartMenuLabel,
        string rangeEndMenuLabel,
        string clearMenuLabel)
    {
        Address = address;
        BeforeHex = beforeHex;
        ValueHex = valueHex;
        IsChanged = isChanged;
        IsSelected = isSelected;
        IsReference = isReference;
        EditMenuLabel = editMenuLabel;
        RangeStartMenuLabel = rangeStartMenuLabel;
        RangeEndMenuLabel = rangeEndMenuLabel;
        ClearMenuLabel = clearMenuLabel;
        EditValue = valueHex;
    }

    /// <summary>Absolute byte address.</summary>
    public string Address { get; }

    /// <summary>Immutable base byte in uppercase hexadecimal.</summary>
    public string BeforeHex { get; }

    /// <summary>Current virtual staged byte in uppercase hexadecimal.</summary>
    public string ValueHex { get; }

    /// <summary>True when the virtual staged byte differs from the immutable base byte.</summary>
    public bool IsChanged { get; }

    /// <summary>True for cells rendered as immutable base-reference rows.</summary>
    public bool IsReference { get; }

    /// <summary>Localized context-menu label for direct byte editing.</summary>
    public string EditMenuLabel { get; }

    /// <summary>Localized context-menu label for choosing this byte as the range start.</summary>
    public string RangeStartMenuLabel { get; }

    /// <summary>Localized context-menu label for choosing this byte as the range end.</summary>
    public string RangeEndMenuLabel { get; }

    /// <summary>Localized context-menu label for fixed-address byte clearing.</summary>
    public string ClearMenuLabel { get; }

    /// <summary>True when this cell is the active authoring target.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>True while the byte accepts direct inline hexadecimal input.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDisplayVisible))]
    [NotifyPropertyChangedFor(nameof(IsEditorVisible))]
    public partial bool IsEditing { get; set; }

    /// <summary>Temporary two-character hexadecimal input for direct editing.</summary>
    [ObservableProperty]
    public partial string EditValue { get; set; }

    /// <summary>Inline validation feedback retained beside the byte when staging is blocked.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInlineValidationMessage))]
    public partial string InlineValidationMessage { get; set; } = string.Empty;

    /// <summary>True while the compact button presentation should remain visible.</summary>
    public bool IsDisplayVisible => !IsEditing;

    /// <summary>True while the inline hexadecimal text input should be visible.</summary>
    public bool IsEditorVisible => IsEditing;

    /// <summary>True when the inline input has a validation failure to expose through its tooltip.</summary>
    public bool HasInlineValidationMessage => !string.IsNullOrWhiteSpace(InlineValidationMessage);

    /// <summary>Accessible description of the immutable base and virtual staged byte values.</summary>
    public string AccessibleLabel => IsChanged
        ? $"{Address}: {BeforeHex} changed to {ValueHex}"
        : $"{Address}: {ValueHex}";
}

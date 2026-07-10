namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One visible 16-byte hexadecimal editor row.</summary>
public sealed record GeneralReplaceHexViewportRowViewModel(
    string Address,
    IReadOnlyList<GeneralReplaceHexByteCellViewModel> Bytes,
    string BeforeAscii,
    string AfterAscii);

/// <summary>One selectable byte cell in the General Replace hexadecimal editor.</summary>
public sealed record GeneralReplaceHexByteCellViewModel(
    string Address,
    string BeforeHex,
    string ValueHex,
    bool IsChanged,
    bool IsSelected)
{
    /// <summary>Accessible description of the immutable base and virtual staged byte values.</summary>
    public string AccessibleLabel => IsChanged
        ? $"{Address}: {BeforeHex} changed to {ValueHex}"
        : $"{Address}: {ValueHex}";
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

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

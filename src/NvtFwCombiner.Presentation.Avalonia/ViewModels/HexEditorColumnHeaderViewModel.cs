using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class HexEditorColumnHeaderViewModel : ObservableObject
{
    /// <summary>Creates one byte-offset column label.</summary>
    public HexEditorColumnHeaderViewModel(int index)
    {
        Index = index;
        Label = index.ToString("X2", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Zero-based byte offset within a displayed row.</summary>
    public int Index { get; }

    public string Label { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

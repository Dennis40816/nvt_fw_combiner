using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One editable General Replace mapping row in the UI authoring list.</summary>
public sealed partial class GeneralReplaceMappingViewModel : ObservableObject
{
    /// <summary>Creates a mapping row.</summary>
    public GeneralReplaceMappingViewModel(string mappingId, int index)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mappingId);

        MappingId = mappingId;
        Index = index;
    }

    /// <summary>Stable row id used by browse/drop handlers.</summary>
    public string MappingId { get; }

    /// <summary>One-based row number displayed to the user.</summary>
    public int Index { get; private set; }

    /// <summary>Displayed file name or empty-slot state.</summary>
    public string DisplayName => HasFile ? Path.GetFileName(FilePath!) : "No replacement BIN selected";

    /// <summary>Displayed selected file path.</summary>
    public string DisplayDetail => HasFile ? FilePath! : string.Empty;

    /// <summary>True when a local input file is selected.</summary>
    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);

    /// <summary>Updates the one-based display index after rows are added or removed.</summary>
    public void SetIndex(int index)
    {
        Index = index;
        OnPropertyChanged(nameof(Index));
    }

    /// <summary>Target start address text.</summary>
    [ObservableProperty]
    public partial string StartAddress { get; set; } = "0x00000";

    /// <summary>Target end address text.</summary>
    [ObservableProperty]
    public partial string EndAddress { get; set; } = "0x00000";

    /// <summary>Selected local file path.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(DisplayDetail))]
    public partial string? FilePath { get; set; }
}

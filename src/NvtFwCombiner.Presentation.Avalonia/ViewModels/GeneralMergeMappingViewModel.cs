using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One editable General Merge source-to-target mapping row in the UI authoring list.</summary>
public sealed partial class GeneralMergeMappingViewModel : ObservableObject
{
    /// <summary>Creates a mapping row.</summary>
    public GeneralMergeMappingViewModel(string mappingId, int index)
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
    public string DisplayName => HasFile ? Path.GetFileName(FilePath!) : "No source BIN selected";

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

    /// <summary>Source start address text inside the selected source BIN.</summary>
    [ObservableProperty]
    public partial string SourceStartAddress { get; set; } = "0x00000";

    /// <summary>Target start address text inside the output image.</summary>
    [ObservableProperty]
    public partial string TargetStartAddress { get; set; } = "0x00000";

    /// <summary>Byte length text copied from source to target.</summary>
    [ObservableProperty]
    public partial string Length { get; set; } = "0x00000";

    /// <summary>Selected local source file path.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(DisplayDetail))]
    public partial string? FilePath { get; set; }
}

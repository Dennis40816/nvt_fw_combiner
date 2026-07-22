using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Shared file and list state for one General mapping row.</summary>
public abstract partial class GeneralMappingRowViewModel : ObservableObject
{
    /// <summary>Creates shared mapping-row state.</summary>
    protected GeneralMappingRowViewModel(string mappingId, int index, string emptyDisplayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mappingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(emptyDisplayName);

        MappingId = mappingId;
        Index = index;
        DisplayName = emptyDisplayName;
    }

    /// <summary>Stable row id used by browse/drop handlers.</summary>
    public string MappingId { get; }

    /// <summary>One-based row number displayed to the user.</summary>
    public int Index { get; private set; }

    /// <summary>Displayed file name or empty-slot state.</summary>
    public string DisplayName => HasFile ? Path.GetFileName(FilePath!) : field;

    /// <summary>Displayed selected file path.</summary>
    public string DisplayDetail => HasFile ? FirmwarePathDisplay.Normalize(FilePath!) : string.Empty;

    /// <summary>True when a local input file is selected.</summary>
    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);

    /// <summary>True while the row displays its empty-slot guidance.</summary>
    public bool IsGuidanceVisible => !HasFile;

    /// <summary>Updates the one-based display index after rows are added or removed.</summary>
    public void SetIndex(int index)
    {
        Index = index;
        OnPropertyChanged(nameof(Index));
    }

    /// <summary>Selected local input file path.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    [NotifyPropertyChangedFor(nameof(IsGuidanceVisible))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(DisplayDetail))]
    public partial string? FilePath { get; set; }
}

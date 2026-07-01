using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>UI file slot for one firmware input artifact.</summary>
public sealed partial class FirmwareSlotViewModel : ObservableObject
{
    /// <summary>Creates a firmware slot.</summary>
    public FirmwareSlotViewModel(string slotId, string title, string description, bool isOptional = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        SlotId = slotId;
        Title = title;
        Description = description;
        IsOptional = isOptional;
    }

    /// <summary>Stable slot id used by drag/drop handlers.</summary>
    public string SlotId { get; }

    /// <summary>Displayed slot title.</summary>
    public string Title { get; }

    /// <summary>Short slot description.</summary>
    public string Description { get; }

    /// <summary>Requirement label for the active workflow.</summary>
    public string RequirementLabel => IsOptional ? "Optional" : "Required";

    /// <summary>True when a local file is selected.</summary>
    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);

    /// <summary>Displayed file name or drop hint.</summary>
    public string DisplayName => HasFile ? Path.GetFileName(FilePath!) : "Drop BIN here";

    /// <summary>Displayed path or browse hint.</summary>
    public string DisplayDetail => HasFile ? FilePath! : "Drag a .bin file or browse";

    /// <summary>Selected local file path.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(DisplayDetail))]
    public partial string? FilePath { get; set; }

    /// <summary>True when this input is optional for the active workflow.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequirementLabel))]
    public partial bool IsOptional { get; set; }
}

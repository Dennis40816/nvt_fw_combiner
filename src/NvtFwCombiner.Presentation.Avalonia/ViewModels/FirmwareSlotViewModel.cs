using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>UI file slot for one firmware input artifact.</summary>
public sealed partial class FirmwareSlotViewModel : ObservableObject
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
    private string RequiredText { get; set; } = "Required";
    private string OptionalText { get; set; } = "Optional";
    private string EmptyDisplayName { get; set; } = "No BIN selected";

    /// <summary>Creates a firmware slot.</summary>
    public FirmwareSlotViewModel(
        string slotId,
        string title,
        string description,
        bool isOptional = false,
        FirmwareSlotKind kind = FirmwareSlotKind.Unknown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        SlotId = slotId;
        Title = title;
        Description = description;
        SlotKind = kind is FirmwareSlotKind.Unknown ? FirmwareSlotKindResolver.Resolve(slotId, title) : kind;
        IsOptional = isOptional;
    }

    /// <summary>Stable slot id used by drag/drop handlers.</summary>
    public string SlotId { get; }

    /// <summary>Displayed slot title.</summary>
    public string Title { get; private set; }

    /// <summary>Short slot description.</summary>
    public string Description { get; private set; }

    /// <summary>Display-only slot kind used by the slot card icon.</summary>
    public FirmwareSlotKind SlotKind { get; }

    /// <summary>Requirement label for the active workflow.</summary>
    public string RequirementLabel => IsOptional ? OptionalText : RequiredText;

    /// <summary>True when a local file is selected.</summary>
    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);

    /// <summary>Displayed file name or empty-slot state.</summary>
    public string DisplayName => HasFile ? Path.GetFileName(FilePath!) : EmptyDisplayName;

    /// <summary>Displayed selected file path.</summary>
    public string DisplayDetail => HasFile ? FirmwarePathDisplay.Normalize(FilePath!) : string.Empty;

    /// <summary>Firmware facts decoded from the selected file, when the active IC has a FWConfig map.</summary>
    public ObservableCollection<FirmwareSlotFactViewModel> FirmwareFacts { get; } = [];

    internal WorkbenchFirmwareArtifactSnapshot? ArtifactSnapshot { get; private set; }

    internal WorkbenchFirmwareInspection? FirmwareInspection { get; private set; }

    /// <summary>True when the slot has decoded firmware facts to show.</summary>
    public bool HasFirmwareFacts => FirmwareFacts.Count > 0;

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

    /// <summary>Updates localizable display-only slot text without changing the stable slot id or file state.</summary>
    public void ApplyDisplayText(
        string title,
        string description,
        string requiredLabel,
        string optionalLabel,
        string emptyDisplayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(optionalLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(emptyDisplayName);

        Title = title;
        Description = description;
        RequiredText = requiredLabel;
        OptionalText = optionalLabel;
        EmptyDisplayName = emptyDisplayName;

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(RequirementLabel));
        OnPropertyChanged(nameof(DisplayName));
    }

    /// <summary>Replaces decoded firmware facts for this slot.</summary>
    public void SetFirmwareFacts(IEnumerable<FirmwareSlotFactViewModel> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        FirmwareFacts.Clear();
        foreach (FirmwareSlotFactViewModel fact in facts)
        {
            FirmwareFacts.Add(fact);
        }

        OnPropertyChanged(nameof(HasFirmwareFacts));
    }

    internal void SetFirmwareInspection(
        WorkbenchFirmwareArtifactSnapshot snapshot,
        WorkbenchFirmwareInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(inspection);

        if (!string.Equals(FilePath, snapshot.ArtifactPath, PathComparison) &&
            !string.Equals(
                FilePath is null ? null : Path.GetFullPath(FilePath),
                Path.GetFullPath(snapshot.ArtifactPath),
                PathComparison))
        {
            throw new InvalidOperationException("Firmware inspection snapshot does not match the selected slot path.");
        }

        ArtifactSnapshot = snapshot;
        FirmwareInspection = inspection;
    }

    internal void SetFirmwareInspection(WorkbenchFirmwareInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        if (ArtifactSnapshot is null)
        {
            throw new InvalidOperationException("A firmware artifact snapshot is required before projection.");
        }

        FirmwareInspection = inspection;
    }

    partial void OnFilePathChanged(string? value)
    {
        if (ArtifactSnapshot is null ||
            string.Equals(value, ArtifactSnapshot.ArtifactPath, PathComparison))
        {
            return;
        }

        ArtifactSnapshot = null;
        FirmwareInspection = null;
    }

}

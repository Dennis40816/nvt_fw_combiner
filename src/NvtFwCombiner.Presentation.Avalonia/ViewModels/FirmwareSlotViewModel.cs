using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Bootstrap;
using System.Collections.ObjectModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>UI file slot for one firmware input artifact.</summary>
public sealed partial class FirmwareSlotViewModel : ObservableObject
{
    private string RequiredText { get; set; } = "Required";
    private string OptionalText { get; set; } = "Optional";
    private string EmptyDisplayName { get; set; } = "No BIN selected";

    /// <summary>Creates a firmware slot.</summary>
    public FirmwareSlotViewModel(
        string slotId,
        string title,
        string description,
        FirmwareSlotKind kind,
        bool isOptional = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        SlotId = slotId;
        Title = title;
        Description = description;
        SlotKind = kind;
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

    /// <summary>True while the empty slot still needs its selection guidance.</summary>
    public bool IsGuidanceVisible => !HasFile;

    /// <summary>Displayed file name or empty-slot state.</summary>
    public string DisplayName => HasFile ? Path.GetFileName(FilePath!) : EmptyDisplayName;

    /// <summary>Displayed selected file path.</summary>
    public string DisplayDetail => HasFile ? FirmwarePathDisplay.Normalize(FilePath!) : string.Empty;

    /// <summary>Firmware facts decoded from the selected file, when the active IC has a FWConfig map.</summary>
    public ObservableCollection<FirmwareSlotFactViewModel> FirmwareFacts { get; } = [];

    /// <summary>True when the slot has decoded firmware facts to show.</summary>
    public bool HasFirmwareFacts => FirmwareFacts.Count > 0;

    /// <summary>True when the selected source has a current health or pending inspection state.</summary>
    public bool HasInputInspectionStatus => IsInputInspectionPending || InputInspectionSeverity is not null;

    /// <summary>True when the latest completed input inspection blocks Build.</summary>
    public bool BlocksBuild => InputInspectionSeverity == WorkbenchInputInspectionSeverity.Blocking;

    /// <summary>True while the selected source awaits a current inspection.</summary>
    public bool IsInputInspectionPending { get; private set; }

    /// <summary>Highest completed typed input health, or null before inspection.</summary>
    public WorkbenchInputInspectionSeverity? InputInspectionSeverity { get; private set; }

    /// <summary>Concise localized health and next-action line.</summary>
    public string InputInspectionStatus { get; private set; } = string.Empty;

    /// <summary>True when the highest completed input health is blocking.</summary>
    public bool IsInputInspectionBlocking => InputInspectionSeverity == WorkbenchInputInspectionSeverity.Blocking;

    /// <summary>True when the highest completed input health is warning.</summary>
    public bool IsInputInspectionWarning => InputInspectionSeverity == WorkbenchInputInspectionSeverity.Warning;

    /// <summary>True when the highest completed input health is valid.</summary>
    public bool IsInputInspectionValid => InputInspectionSeverity == WorkbenchInputInspectionSeverity.Valid;

    /// <summary>Vector icon path for the highest completed or pending input health.</summary>
    public string InputInspectionIconPathData => IsInputInspectionPending
        ? "M12 3A9 9 0 1 0 21 12 M12 7V12L15 14"
        : InputInspectionSeverity switch
        {
            WorkbenchInputInspectionSeverity.Blocking => "M12 3A9 9 0 1 0 12 21A9 9 0 1 0 12 3 M12 7V13 M12 17H12.01",
            WorkbenchInputInspectionSeverity.Warning => "M12 3L22 20H2L12 3 M12 9V14 M12 17H12.01",
            WorkbenchInputInspectionSeverity.Valid => "M4 12L9 17L20 6",
            _ => string.Empty,
        };

    /// <summary>Selected local file path.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    [NotifyPropertyChangedFor(nameof(IsGuidanceVisible))]
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

    /// <summary>Marks the current selected source as awaiting a fresh typed inspection.</summary>
    public void SetInputInspectionPending(string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        IsInputInspectionPending = true;
        InputInspectionSeverity = null;
        InputInspectionStatus = status;
        NotifyInputInspectionChanged();
    }

    /// <summary>Applies one completed highest-severity typed input diagnostic.</summary>
    public void SetInputInspection(
        WorkbenchInputInspectionSeverity severity,
        string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
        }

        IsInputInspectionPending = false;
        InputInspectionSeverity = severity;
        InputInspectionStatus = status;
        NotifyInputInspectionChanged();
    }

    /// <summary>Clears stale input health when the selected path or compiled context changes.</summary>
    public void ClearInputInspection()
    {
        IsInputInspectionPending = false;
        InputInspectionSeverity = null;
        InputInspectionStatus = string.Empty;
        NotifyInputInspectionChanged();
    }

    private void NotifyInputInspectionChanged()
    {
        OnPropertyChanged(nameof(HasInputInspectionStatus));
        OnPropertyChanged(nameof(BlocksBuild));
        OnPropertyChanged(nameof(IsInputInspectionPending));
        OnPropertyChanged(nameof(InputInspectionSeverity));
        OnPropertyChanged(nameof(InputInspectionStatus));
        OnPropertyChanged(nameof(IsInputInspectionBlocking));
        OnPropertyChanged(nameof(IsInputInspectionWarning));
        OnPropertyChanged(nameof(IsInputInspectionValid));
        OnPropertyChanged(nameof(InputInspectionIconPathData));
    }

}

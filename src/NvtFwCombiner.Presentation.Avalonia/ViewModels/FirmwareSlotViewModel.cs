using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>UI file slot for one firmware input artifact.</summary>
public sealed partial class FirmwareSlotViewModel : ObservableObject
{
    private static readonly IBrush OptionalSlotBackground = Brush.Parse("#F8FAFC");
    private static readonly IBrush OptionalSlotBorder = Brush.Parse("#CBD5E1");
    private static readonly IBrush OptionalBadgeBackground = Brush.Parse("#EAF3FF");
    private static readonly IBrush OptionalBadgeBorder = Brush.Parse("#BFDBFE");
    private static readonly IBrush OptionalBadgeForeground = Brush.Parse("#1D4ED8");
    private static readonly IBrush MissingRequiredSlotBackground = Brush.Parse("#FFF7F7");
    private static readonly IBrush MissingRequiredSlotBorder = Brush.Parse("#FCA5A5");
    private static readonly IBrush MissingRequiredBadgeBackground = Brush.Parse("#FEE2E2");
    private static readonly IBrush MissingRequiredBadgeBorder = Brush.Parse("#FECACA");
    private static readonly IBrush MissingRequiredBadgeForeground = Brush.Parse("#B91C1C");
    private static readonly IBrush SelectedRequiredSlotBackground = Brush.Parse("#F0FDF4");
    private static readonly IBrush SelectedRequiredSlotBorder = Brush.Parse("#86EFAC");
    private static readonly IBrush SelectedRequiredBadgeBackground = Brush.Parse("#DCFCE7");
    private static readonly IBrush SelectedRequiredBadgeBorder = Brush.Parse("#BBF7D0");
    private static readonly IBrush SelectedRequiredBadgeForeground = Brush.Parse("#15803D");

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

    /// <summary>Slot card background for requirement completion state.</summary>
    public IBrush SlotBackgroundBrush => IsOptional
        ? OptionalSlotBackground
        : HasFile ? SelectedRequiredSlotBackground : MissingRequiredSlotBackground;

    /// <summary>Slot card border for requirement completion state.</summary>
    public IBrush SlotBorderBrush => IsOptional
        ? OptionalSlotBorder
        : HasFile ? SelectedRequiredSlotBorder : MissingRequiredSlotBorder;

    /// <summary>Requirement badge background for completion state.</summary>
    public IBrush RequirementBadgeBackgroundBrush => IsOptional
        ? OptionalBadgeBackground
        : HasFile ? SelectedRequiredBadgeBackground : MissingRequiredBadgeBackground;

    /// <summary>Requirement badge border for completion state.</summary>
    public IBrush RequirementBadgeBorderBrush => IsOptional
        ? OptionalBadgeBorder
        : HasFile ? SelectedRequiredBadgeBorder : MissingRequiredBadgeBorder;

    /// <summary>Requirement badge foreground for completion state.</summary>
    public IBrush RequirementBadgeForegroundBrush => IsOptional
        ? OptionalBadgeForeground
        : HasFile ? SelectedRequiredBadgeForeground : MissingRequiredBadgeForeground;

    /// <summary>Selected local file path.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(DisplayDetail))]
    [NotifyPropertyChangedFor(nameof(SlotBackgroundBrush))]
    [NotifyPropertyChangedFor(nameof(SlotBorderBrush))]
    [NotifyPropertyChangedFor(nameof(RequirementBadgeBackgroundBrush))]
    [NotifyPropertyChangedFor(nameof(RequirementBadgeBorderBrush))]
    [NotifyPropertyChangedFor(nameof(RequirementBadgeForegroundBrush))]
    public partial string? FilePath { get; set; }

    /// <summary>True when this input is optional for the active workflow.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequirementLabel))]
    [NotifyPropertyChangedFor(nameof(SlotBackgroundBrush))]
    [NotifyPropertyChangedFor(nameof(SlotBorderBrush))]
    [NotifyPropertyChangedFor(nameof(RequirementBadgeBackgroundBrush))]
    [NotifyPropertyChangedFor(nameof(RequirementBadgeBorderBrush))]
    [NotifyPropertyChangedFor(nameof(RequirementBadgeForegroundBrush))]
    public partial bool IsOptional { get; set; }
}

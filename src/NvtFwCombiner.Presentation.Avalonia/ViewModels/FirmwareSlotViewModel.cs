using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>UI file slot for one firmware input artifact.</summary>
public sealed partial class FirmwareSlotViewModel : ObservableObject
{
    private const string BaseIconPathData =
        "M5,2 L12,2 L16,6 L16,18 L5,18 Z M12,2 L12,6 L16,6 M7,10 L14,10 M7,13 L14,13";
    private const string DpIconPathData =
        "M3,4 L17,4 L17,14 L3,14 Z M8,18 L12,18 M10,14 L10,18";
    private const string TpIconPathData =
        "M8,10 L8,5 L10,5 L10,11 L11,10 L12,11 L13,10 L14,12 L14,17 L8,17 L5,13 L6,12 L8,14";
    private const string CtrlRamIconPathData =
        "M6,5 L14,5 L15,6 L15,14 L14,15 L6,15 L5,14 L5,6 Z M8,8 L12,8 L12,12 L8,12 Z M8,2 L8,5 M12,2 L12,5 M8,15 L8,18 M12,15 L12,18 M2,8 L5,8 M2,12 L5,12 M15,8 L18,8 M15,12 L18,12";
    private const string BinIconPathData =
        "M5,3 L15,3 L15,17 L5,17 Z M7,7 L13,7 M7,10 L13,10 M7,13 L11,13";
    private static readonly IBrush BaseIconBackground = Brush.Parse("#F8FAFC");
    private static readonly IBrush BaseIconBorder = Brush.Parse("#CBD5E1");
    private static readonly IBrush BaseIconForeground = Brush.Parse("#475569");
    private static readonly IBrush DpIconBackground = Brush.Parse("#EFF6FF");
    private static readonly IBrush DpIconBorder = Brush.Parse("#BFDBFE");
    private static readonly IBrush DpIconForeground = Brush.Parse("#1D4ED8");
    private static readonly IBrush TpIconBackground = Brush.Parse("#F0FDF4");
    private static readonly IBrush TpIconBorder = Brush.Parse("#BBF7D0");
    private static readonly IBrush TpIconForeground = Brush.Parse("#15803D");
    private static readonly IBrush CtrlRamIconBackground = Brush.Parse("#F5F3FF");
    private static readonly IBrush CtrlRamIconBorder = Brush.Parse("#DDD6FE");
    private static readonly IBrush CtrlRamIconForeground = Brush.Parse("#6D28D9");
    private static readonly IBrush BinIconBackground = Brush.Parse("#FFFBEB");
    private static readonly IBrush BinIconBorder = Brush.Parse("#FDE68A");
    private static readonly IBrush BinIconForeground = Brush.Parse("#92400E");
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
    private static readonly Thickness NormalSlotBorderThickness = new(1);
    private static readonly Thickness MissingRequiredSlotBorderThickness = new(1.5);

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
        SlotKind = kind is FirmwareSlotKind.Unknown ? InferSlotKind(slotId, title) : kind;
        IsOptional = isOptional;
    }

    /// <summary>Stable slot id used by drag/drop handlers.</summary>
    public string SlotId { get; }

    /// <summary>Displayed slot title.</summary>
    public string Title { get; }

    /// <summary>Short slot description.</summary>
    public string Description { get; }

    /// <summary>Display-only slot kind used by the slot card icon.</summary>
    public FirmwareSlotKind SlotKind { get; }

    /// <summary>Vector path data for this slot type icon.</summary>
    public string SlotIconPathData => SlotKind switch
    {
        FirmwareSlotKind.Unknown => BinIconPathData,
        FirmwareSlotKind.Base => BaseIconPathData,
        FirmwareSlotKind.Dp => DpIconPathData,
        FirmwareSlotKind.Tp => TpIconPathData,
        FirmwareSlotKind.CtrlRam => CtrlRamIconPathData,
        _ => BinIconPathData,
    };

    /// <summary>Accessible description for the slot type icon.</summary>
    public string SlotIconTooltip => SlotKind switch
    {
        FirmwareSlotKind.Unknown => "BIN input",
        FirmwareSlotKind.Base => "Base firmware BIN",
        FirmwareSlotKind.Dp => "DP BIN",
        FirmwareSlotKind.Tp => "TP BIN",
        FirmwareSlotKind.CtrlRam => "CtrlRAM BIN",
        _ => "BIN input",
    };

    /// <summary>Slot type icon background.</summary>
    public IBrush SlotIconBackgroundBrush => SlotKind switch
    {
        FirmwareSlotKind.Unknown => BinIconBackground,
        FirmwareSlotKind.Base => BaseIconBackground,
        FirmwareSlotKind.Dp => DpIconBackground,
        FirmwareSlotKind.Tp => TpIconBackground,
        FirmwareSlotKind.CtrlRam => CtrlRamIconBackground,
        _ => BinIconBackground,
    };

    /// <summary>Slot type icon border.</summary>
    public IBrush SlotIconBorderBrush => SlotKind switch
    {
        FirmwareSlotKind.Unknown => BinIconBorder,
        FirmwareSlotKind.Base => BaseIconBorder,
        FirmwareSlotKind.Dp => DpIconBorder,
        FirmwareSlotKind.Tp => TpIconBorder,
        FirmwareSlotKind.CtrlRam => CtrlRamIconBorder,
        _ => BinIconBorder,
    };

    /// <summary>Slot type icon foreground.</summary>
    public IBrush SlotIconForegroundBrush => SlotKind switch
    {
        FirmwareSlotKind.Unknown => BinIconForeground,
        FirmwareSlotKind.Base => BaseIconForeground,
        FirmwareSlotKind.Dp => DpIconForeground,
        FirmwareSlotKind.Tp => TpIconForeground,
        FirmwareSlotKind.CtrlRam => CtrlRamIconForeground,
        _ => BinIconForeground,
    };

    /// <summary>Requirement label for the active workflow.</summary>
    public string RequirementLabel => IsOptional ? "Optional" : "Required";

    /// <summary>True when a local file is selected.</summary>
    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);

    /// <summary>Displayed file name or empty-slot state.</summary>
    public string DisplayName => HasFile ? Path.GetFileName(FilePath!) : "No BIN selected";

    /// <summary>Displayed selected file path.</summary>
    public string DisplayDetail => HasFile ? FilePath! : string.Empty;

    /// <summary>Firmware facts decoded from the selected file, when the active IC has a FWConfig map.</summary>
    public ObservableCollection<FirmwareSlotFactViewModel> FirmwareFacts { get; } = [];

    /// <summary>True when the slot has decoded firmware facts to show.</summary>
    public bool HasFirmwareFacts => FirmwareFacts.Count > 0;

    /// <summary>Slot card background for requirement completion state.</summary>
    public IBrush SlotBackgroundBrush => IsOptional
        ? OptionalSlotBackground
        : HasFile ? SelectedRequiredSlotBackground : MissingRequiredSlotBackground;

    /// <summary>Slot card border for requirement completion state.</summary>
    public IBrush SlotBorderBrush => IsOptional
        ? OptionalSlotBorder
        : HasFile ? SelectedRequiredSlotBorder : MissingRequiredSlotBorder;

    /// <summary>Slot card border thickness for completion state.</summary>
    public Thickness SlotBorderThickness => !IsOptional && !HasFile
        ? MissingRequiredSlotBorderThickness
        : NormalSlotBorderThickness;

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
    [NotifyPropertyChangedFor(nameof(SlotBorderThickness))]
    [NotifyPropertyChangedFor(nameof(RequirementBadgeBackgroundBrush))]
    [NotifyPropertyChangedFor(nameof(RequirementBadgeBorderBrush))]
    [NotifyPropertyChangedFor(nameof(RequirementBadgeForegroundBrush))]
    public partial string? FilePath { get; set; }

    /// <summary>True when this input is optional for the active workflow.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequirementLabel))]
    [NotifyPropertyChangedFor(nameof(SlotBackgroundBrush))]
    [NotifyPropertyChangedFor(nameof(SlotBorderBrush))]
    [NotifyPropertyChangedFor(nameof(SlotBorderThickness))]
    [NotifyPropertyChangedFor(nameof(RequirementBadgeBackgroundBrush))]
    [NotifyPropertyChangedFor(nameof(RequirementBadgeBorderBrush))]
    [NotifyPropertyChangedFor(nameof(RequirementBadgeForegroundBrush))]
    public partial bool IsOptional { get; set; }

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

    private static FirmwareSlotKind InferSlotKind(string slotId, string title)
    {
        return true switch
        {
            _ when Contains(slotId, "base") || Contains(title, "Base") => FirmwareSlotKind.Base,
            _ when Contains(slotId, "ctrlram") || Contains(title, "CtrlRAM") => FirmwareSlotKind.CtrlRam,
            _ when Contains(slotId, "-tp") || Contains(slotId, "tp-") || Contains(title, "TP ") => FirmwareSlotKind.Tp,
            _ when Contains(slotId, "-dp") ||
                Contains(slotId, "-ld") ||
                Contains(slotId, "ldc") ||
                Contains(title, "DP ") ||
                Contains(title, "LD ") => FirmwareSlotKind.Dp,
            _ => FirmwareSlotKind.Unknown,
        };
    }

    private static bool Contains(string value, string expected)
    {
        return value.Contains(expected, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>One compact firmware fact displayed below a selected BIN file name.</summary>
public sealed record FirmwareSlotFactViewModel(string Label, string Value, bool IsWarning = false);

/// <summary>Display-only firmware slot category for consistent input card icons.</summary>
public enum FirmwareSlotKind
{
    /// <summary>Unknown or generic BIN input.</summary>
    Unknown,

    /// <summary>Base/reference firmware image.</summary>
    Base,

    /// <summary>Display/DP-family payload.</summary>
    Dp,

    /// <summary>Touch-panel payload.</summary>
    Tp,

    /// <summary>CtrlRAM payload.</summary>
    CtrlRam,
}

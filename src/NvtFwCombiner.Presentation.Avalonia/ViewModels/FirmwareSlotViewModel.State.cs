using Avalonia;
using Avalonia.Media;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class FirmwareSlotViewModel
{
    private static readonly IBrush OptionalSlotBackground = Brush.Parse("#F8FAFC");
    private static readonly IBrush OptionalSlotBorder = Brush.Parse("#CBD5E1");
    private static readonly IBrush OptionalBadgeBackground = Brush.Parse("#EAF3FF");
    private static readonly IBrush OptionalBadgeBorder = Brush.Parse("#BFDBFE");
    private static readonly IBrush OptionalBadgeForeground = Brush.Parse("#1D4ED8");
    private static readonly IBrush MissingRequiredSlotBackground = Brush.Parse("#FEF2F2");
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
}

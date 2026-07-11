using Avalonia.Media;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>One compact firmware fact displayed below a selected BIN file name.</summary>
public sealed record FirmwareSlotFactViewModel(string Label, string Value, bool IsWarning = false)
{
    private static readonly IBrush NormalBackground = Brush.Parse("#EEF6FF");
    private static readonly IBrush NormalBorder = Brush.Parse("#BFDBFE");
    private static readonly IBrush NormalLabelForeground = Brush.Parse("#475569");
    private static readonly IBrush NormalValueForeground = Brush.Parse("#0F172A");
    private static readonly IBrush WarningBackground = Brush.Parse("#FFF7ED");
    private static readonly IBrush WarningBorder = Brush.Parse("#FDBA74");
    private static readonly IBrush WarningLabelForeground = Brush.Parse("#9A3412");
    private static readonly IBrush WarningValueForeground = Brush.Parse("#9A3412");

    /// <summary>Fact badge background.</summary>
    public IBrush BackgroundBrush => IsWarning ? WarningBackground : NormalBackground;

    /// <summary>Fact badge border.</summary>
    public IBrush BorderBrush => IsWarning ? WarningBorder : NormalBorder;

    /// <summary>Fact label foreground.</summary>
    public IBrush LabelForegroundBrush => IsWarning ? WarningLabelForeground : NormalLabelForeground;

    /// <summary>Fact value foreground.</summary>
    public IBrush ValueForegroundBrush => IsWarning ? WarningValueForeground : NormalValueForeground;
}

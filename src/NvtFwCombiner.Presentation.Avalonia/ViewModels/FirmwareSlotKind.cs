namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

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

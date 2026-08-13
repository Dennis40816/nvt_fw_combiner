namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Display-only firmware slot category for consistent input card icons.</summary>
internal enum FirmwareSlotKind
{
    Unknown,

    /// <summary>Base/reference firmware image.</summary>
    Base,

    Dp,

    Tp,

    CtrlRam,
}

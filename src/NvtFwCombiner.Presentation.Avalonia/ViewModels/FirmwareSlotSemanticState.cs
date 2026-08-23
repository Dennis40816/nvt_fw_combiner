namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Presentation-only state shown by every firmware input slot.</summary>
internal enum FirmwareSlotSemanticState
{
    Empty,

    Checking,

    Inspected,

    Verified,

    Warning,

    Error,

    NotApplicable,
}

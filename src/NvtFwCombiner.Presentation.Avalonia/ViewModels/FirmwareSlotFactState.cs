namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Typed presentation state for one projected firmware metadata fact.</summary>
internal enum FirmwareSlotFactState
{
    Ordinary,

    Unknown,

    /// <summary>A prerequisite is required before the value can be inspected.</summary>
    PendingInput,

    NotApplicable,

    Warning,

    Error,
}

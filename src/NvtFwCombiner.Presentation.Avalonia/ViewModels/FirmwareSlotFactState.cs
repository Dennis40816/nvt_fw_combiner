namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Typed presentation state for one projected firmware metadata fact.</summary>
public enum FirmwareSlotFactState
{
    /// <summary>A known ordinary value.</summary>
    Ordinary,

    /// <summary>The value could not be determined.</summary>
    Unknown,

    /// <summary>A prerequisite is required before the value can be inspected.</summary>
    PendingInput,

    /// <summary>The resolved projection excludes this fact.</summary>
    NotApplicable,

    /// <summary>The value is usable but requires attention.</summary>
    Warning,

    /// <summary>The value blocks the current action.</summary>
    Error,
}

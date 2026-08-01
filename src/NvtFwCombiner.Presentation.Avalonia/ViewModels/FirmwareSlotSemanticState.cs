namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Presentation-only state shown by every firmware input slot.</summary>
public enum FirmwareSlotSemanticState
{
    /// <summary>No file or resolved child state is present.</summary>
    Empty,

    /// <summary>The selected input or its prerequisite is still being resolved.</summary>
    Checking,

    /// <summary>The selected input passed its typed inspection.</summary>
    Verified,

    /// <summary>The selected input is usable but requires attention.</summary>
    Warning,

    /// <summary>The input or prerequisite blocks the workflow.</summary>
    Error,

    /// <summary>The compiled selection excludes this slot.</summary>
    NotApplicable,
}

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Gets short Replace memory-map summary text.</summary>
    public string ReplaceMemorySummary => SelectedReplaceMode switch
    {
        DpReplaceMode => "Base flash is kept; only DP/LD ranges listed below are written.",
        CtrlRamReplaceMode => "Base flash is kept; CtrlRAM ranges are staged, then combiner.exe refreshes CRC/header.",
        GeneralReplaceMode => "Base flash is kept; explicit ranges are validated before write access.",
        _ => "Select a replace mode to inspect its target ranges.",
    };

    /// <summary>Status shown in the replace inspector.</summary>
    public string ReplaceReadinessStatus => SelectedReplaceMode switch
    {
        DpReplaceMode => "Preview blocked: base BIN and DP/LD replacement inputs are required.",
        CtrlRamReplaceMode => "Preview blocked: base BIN, CtrlRAM BIN, and combiner readiness are required.",
        GeneralReplaceMode => "Preview blocked: base BIN, replacement BIN, and an approved range are required.",
        _ => "Preview blocked: select a Replace mode.",
    };

    /// <summary>Gets the compact reason shown on disabled Replace preview.</summary>
    public string ReplacePreviewUnavailableReason => ReplaceReadinessStatus;

    /// <summary>Gets the compact reason shown on disabled Replace build.</summary>
    public string ReplaceBuildUnavailableReason => $"Build blocked: run a valid {SelectedReplaceMode} Preview first.";
}

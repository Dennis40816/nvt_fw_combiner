namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Primes page-owned projections without navigating or reading a user firmware path.</summary>
    internal void WarmDeferredShellState()
    {
        _deferredState.EnsureSettings(RefreshSettingsState);
        RefreshContextState();
        _ = HexEditorWorkspace;
    }
}

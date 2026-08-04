namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>Loads and opens the independent raw-BIN utility.</summary>
    private void ShowHexEditor()
    {
        _ = HexEditorWorkspace;
        OnPropertyChanged(nameof(LoadedHexEditorWorkspace));
        NavigateToPage(ShellPage.HexEditor);
    }

}

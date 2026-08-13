namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class MainWindowViewModel
{
    private void ShowHexEditor()
    {
        _ = HexEditorWorkspace;
        OnPropertyChanged(nameof(LoadedHexEditorWorkspace));
        NavigateToPage(ShellPage.HexEditor);
    }

}

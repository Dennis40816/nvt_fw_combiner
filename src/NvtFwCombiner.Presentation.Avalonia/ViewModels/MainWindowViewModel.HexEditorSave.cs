using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>True while Ctrl+S or Save asks the user to confirm a safe generated-BIN export.</summary>
    [ObservableProperty]
    public partial bool IsHexEditorSaveConfirmationOpen { get; set; }

    /// <summary>Command that opens the safe generated-BIN export confirmation.</summary>
    public IRelayCommand RequestHexEditorSaveCommand { get; }

    /// <summary>Command that closes the safe generated-BIN export confirmation without writing a file.</summary>
    public IRelayCommand CancelHexEditorSaveCommand { get; }

    private void RequestHexEditorSave()
    {
        if (!IsHexEditorVisible || !CanBuildHexEditor)
        {
            return;
        }

        IsHexEditorSaveConfirmationOpen = true;
    }

    private void CancelHexEditorSave()
    {
        IsHexEditorSaveConfirmationOpen = false;
    }
}
